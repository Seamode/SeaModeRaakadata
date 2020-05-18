using RaakadataLibrary;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ookii.Dialogs.Wpf;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.ComponentModel.Design;

namespace Raakadata
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<DateTime> minDTs = new List<DateTime>();
        List<DateTime> maxDTs = new List<DateTime>();
        Dictionary<TextBox, int> prevCaretIndex = new Dictionary<TextBox, int>();
        Dictionary<TextBox, string> prevText = new Dictionary<TextBox, string>();
        private int startTimeStringLocation;
        private int endTimeStringLocation;
        private readonly string timePlacehoder = "HH:mm:ss";
        public MainWindow()
        {
            InitializeComponent();

            string defaultPath = FindDatDirectory();
            tbFolderPath.Text = defaultPath;
            tbSavePath.Text = defaultPath;
            if (!string.IsNullOrEmpty(defaultPath))
                ListFilesInFolder();

            prevCaretIndex.Add(tbEventStartTime, -1);
            prevText.Add(tbEventStartTime, tbEventStartTime.Text);
            prevCaretIndex.Add(tbEventEndTime, -1);
            prevText.Add(tbEventEndTime, tbEventEndTime.Text);

            tbEventStartTime.TextChanged += TbTime_TextChanged;
            tbEventStartTime.SelectionChanged += TbTime_SelectionChanged;
            tbEventEndTime.TextChanged += TbTime_TextChanged;
            tbEventEndTime.SelectionChanged += TbTime_SelectionChanged;

            tbFolderPath.Text = ConfigurationManager.AppSettings["fileDirectory"];
            tbSavePath.Text = ConfigurationManager.AppSettings["fileDirectory"];
            ListFilesInFolder();
            // jotta tiedostot on helppo valita testausta varten.
            dpEventStartDate.DisplayDate = new DateTime(2019, 09, 28);
            dpEventEndDate.DisplayDate = new DateTime(2019, 09, 28);
        }


        private string FindDatDirectory()
        {
            if (Directory.Exists(ConfigurationManager.AppSettings["fileDirectory"]))
                return ConfigurationManager.AppSettings["fileDirectory"];
            //string altPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location).ToString();
            //string altPath = System.IO.Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            string altPath = System.IO.Path.GetDirectoryName(
                System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            // ylempi pitäisi palauttaa prg, alla se vaihdetaan dat
            altPath = $"{altPath.Substring(0, altPath.LastIndexOf(System.IO.Path.DirectorySeparatorChar) + 1)}dat";
            if (Directory.Exists(altPath))
                return altPath;
            return string.Empty;
        }

        private void ListFilesInFolder()
        {
            if (tbFilesInFolder.Items.Count > 1)
            {
                tbFilesInFolder.SelectionChanged -= tbFilesInFolder_SelectionChanged;
                cbSelectAll.Unchecked -= ListBox_UnselectAll;
                cbSelectAll.IsChecked = false;
                tbFilesInFolder.UnselectAll();

                int itemCount = tbFilesInFolder.Items.Count;

                for (int i = 0; i < itemCount;)
                {
                    if (tbFilesInFolder.Items[i] is string)
                    {
                        tbFilesInFolder.Items.Remove(tbFilesInFolder.Items[i]);
                        --itemCount;
                    }
                    else
                    {
                        i++;
                    }
                }

                minDTs.Clear();
                maxDTs.Clear();

                lbiCheckBox.IsEnabled = false;
                cbSelectAll.IsEnabled = false;

                cbSelectAll.Unchecked += ListBox_UnselectAll;
                tbFilesInFolder.SelectionChanged += tbFilesInFolder_SelectionChanged;
            }

            foreach (var file in SeamodeReader.FetchFilesToList(tbFolderPath.Text))
            {
                tbFilesInFolder.Items.Add(file);
            }

            if (tbFilesInFolder.Items.Count > 1)
            {
                PickDateTimes();

                lbiCheckBox.IsEnabled = true;
                cbSelectAll.IsEnabled = true;
            }
        }

        private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            // https://stackoverflow.com/questions/1922204/open-directory-dialog
            // Ookii.Diologs.Wpf
            var fd = new VistaFolderBrowserDialog();
            if (fd.ShowDialog(this).GetValueOrDefault())
            {
                tbFolderPath.Text = fd.SelectedPath;
                ListFilesInFolder();
            }
        }

        private void BtnSelectSavePath_Click(object sender, RoutedEventArgs e)
        {
            var fd = new VistaFolderBrowserDialog();
            if (fd.ShowDialog(this).GetValueOrDefault())
                tbSavePath.Text = fd.SelectedPath;
        }

        private async void BtnCreateEventFile_Click(object sender, RoutedEventArgs e)
        {
            // jotta ei tapahdu tupla klikkausta.
            BtnCreateGpxFile.IsEnabled = false;
            BtnCreateEventFile.IsEnabled = false;
            if (!ValidateParameters(out TimeSpan startTime, out TimeSpan endTime))
            {
                BtnCreateGpxFile.IsEnabled = true;
                BtnCreateEventFile.IsEnabled = true;
                return;
            }
            // kisan alku ja loppu datetimen muodostus.
            // ei pitäisi päästä tähän, jos on virheellisesti syötetty.
            if (!ValidTimePeriod(startTime, endTime, out DateTime eventStart, out DateTime eventEnd))
            {
                BtnCreateGpxFile.IsEnabled = true;
                BtnCreateEventFile.IsEnabled = true;
                return;
            }
            // Tarkistaa onko samannimistä tiedostoa kansiossa, jos on kysyy halutaanko sen päälle kirjoittaa.
            string fullFilePath = $"{tbSavePath.Text}\\SeaMODE_{dpEventEndDate.SelectedDate:yyyyMMdd}_{string.Join("", tbEventStartTime.Text.Split(':', '.'))}_{tbEventName.Text}.csv";
            if (File.Exists(fullFilePath))
            {
                var anws = MessageBox.Show("A file with that name already exists.\nIf you perceed, the file will be overwritten.\nDo you want to proceed?", "Warning, File Exists.", MessageBoxButton.YesNo);
                if (anws == MessageBoxResult.No)
                {
                    BtnCreateGpxFile.IsEnabled = true;
                    BtnCreateEventFile.IsEnabled = true;
                    tbEventName.Focus();
                    return;
                }
                else
                    File.Delete(fullFilePath);
            }
            // Muutetaan kursori kertomaan käyttäjälle käynnissä olevasta datan käsittelystä.
            Cursor tempCursor = Cursor;
            Cursor = Cursors.Wait;
            ForceCursor = true;
            // tiedostojen luku
            SeamodeReader sr = new SeamodeReader(eventStart, eventEnd);
            await sr.ReadAndWriteFilesAsync(tbFolderPath.Text);
            // Kursorin palautus.
            Cursor = tempCursor;
            ForceCursor = false;
            // muuten tulee tyhjä tiedosto
            if (sr.DataRowCount == 0)
            {
                MessageBox.Show("No data found for specified time period.");
                BtnCreateGpxFile.IsEnabled = true;
                BtnCreateEventFile.IsEnabled = true;
                return;
            }
            // kisatiedoston luonti
            File.Move(sr.TmpFile, fullFilePath);
            // Ilmoitus luonnista ja mahdolliset virheet.
            StringBuilder msg = new StringBuilder();
            msg.AppendLine($"File {fullFilePath} was created.");
            if (!sr.PastEnd)
                msg.AppendLine("Data logging ended before the specified endpoint.");
            foreach (string line in sr.DataRowErrors)
            {
                msg.AppendLine(line);
            }
            msg.AppendLine("Would you like to open the folder?");
            var res = MessageBox.Show(msg.ToString(), "File Created.", MessageBoxButton.YesNo);
            // avaa File Explorerin
            if (res == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(tbSavePath.Text);
            BtnCreateGpxFile.IsEnabled = true;
            BtnCreateEventFile.IsEnabled = true;
            // syötetyt arvot tyhjennetään
            ResetUI();
            ListFilesInFolder();
        }

        private bool ValidTimePeriod(TimeSpan startTime, TimeSpan endTime, out DateTime eventStart, out DateTime eventEnd)
        {
            eventStart = (DateTime)dpEventStartDate.SelectedDate;
            eventStart = eventStart.Add(startTime);
            eventEnd = (DateTime)dpEventEndDate.SelectedDate;
            eventEnd = eventEnd.Add(endTime);
            if (eventEnd <= eventStart)
            {
                lblEventLengthError.Content = "Event start must be before the ending.";
                return false;
            }
            return true;
        }


        private bool ValidateParameters(out TimeSpan startTime, out TimeSpan endTime)
        {
            bool valid = true;
            if (dpEventStartDate.SelectedDate == null)
            {
                dpEventStartDate.BorderBrush = Brushes.Red;
                lblEventStartDateError.Content = "Date required";
                valid = false;
            }
            if (dpEventEndDate.SelectedDate == null)
            {
                dpEventEndDate.BorderBrush = Brushes.Red;
                lblEventEndDateError.Content = "Date required";
                valid = false;
            }
            if (!TimeSpan.TryParse(tbEventStartTime.Text, out startTime))
            {
                tbEventStartTime.BorderBrush = Brushes.Red;
                lblEventStartTimeError.Content = "Time required";
                valid = false;
            }
            if (!TimeSpan.TryParse(tbEventEndTime.Text, out endTime))
            {
                tbEventEndTime.BorderBrush = Brushes.Red;
                lblEventEndTimeError.Content = "Time required";
                valid = false;
            }
            if (string.IsNullOrEmpty(tbEventName.Text))
            {
                tbEventName.BorderBrush = Brushes.Red;
                lblEventNameError.Content = "Name for file required";
                valid = false;
            }
            if (string.IsNullOrEmpty(tbFolderPath.Text) || !Directory.Exists(tbFolderPath.Text))
            {
                tbFolderPath.BorderBrush = Brushes.Red;
                lblFolderPathError.Content = "Valid folder path required";
                valid = false;
            }
            if (string.IsNullOrEmpty(tbSavePath.Text) || !Directory.Exists(tbSavePath.Text))
            {
                tbSavePath.BorderBrush = Brushes.Red;
                lblSavePathError.Content = "Valid folder path required";
                valid = false;
            }
            return valid;
        }

        private void ResetUI()
        {
            dpEventStartDate.SelectedDate = null;
            //dpEventStartDate.DisplayDate = DateTime.Today;
            dpEventEndDate.SelectedDate = null;
            //dpEventEndDate.DisplayDate = DateTime.Today;
            tbEventStartTime.Text = timePlacehoder;
            tbEventEndTime.Text = timePlacehoder;
            tbEventName.Clear();
            dpEventStartDate.ClearValue(BorderBrushProperty);
            dpEventEndDate.ClearValue(BorderBrushProperty);
            tbFolderPath.ClearValue(BorderBrushProperty);
            tbSavePath.ClearValue(BorderBrushProperty);
            tbEventStartTime.ClearValue(BorderBrushProperty);
            tbEventEndTime.ClearValue(BorderBrushProperty);
            tbEventName.ClearValue(BorderBrushProperty);
            lblFolderPathError.ClearValue(ContentProperty);
            lblSavePathError.ClearValue(ContentProperty);
            lblEventStartDateError.ClearValue(ContentProperty);
            lblEventStartTimeError.ClearValue(ContentProperty);
            lblEventEndDateError.ClearValue(ContentProperty);
            lblEventEndTimeError.ClearValue(ContentProperty);
            lblEventLengthError.ClearValue(ContentProperty);
            lblEventNameError.ClearValue(ContentProperty);
        }

        private void TbFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = e.Source as TextBox;
            tb.ClearValue(BorderBrushProperty);
            if (tb == tbFolderPath)
                lblFolderPathError.ClearValue(ContentProperty);
            else if (tb == tbSavePath)
                lblSavePathError.ClearValue(ContentProperty);
            else if (tb == tbEventName)
                lblEventNameError.ClearValue(ContentProperty);
        }

        private void TbTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tbTime = e.Source as TextBox;
            tbTime.ClearValue(BorderBrushProperty);
            const string template = "__:__:__";
            int moveCaret = 1;

            tbTime.SelectionChanged -= TbTime_SelectionChanged;
            tbTime.TextChanged -= TbTime_TextChanged;

            foreach (var change in e.Changes)
            {
                if (change.AddedLength > 0)
                {
                    tbTime.Text = tbTime.Text.Replace('H', '_');
                    tbTime.Text = tbTime.Text.Replace('m', '_');
                    tbTime.Text = tbTime.Text.Replace('s', '_');
                    char[] prevString = prevText[tbTime].ToArray();
                    char[] newString = new char[change.Offset + change.AddedLength > 8 ? (8 - change.Offset + change.AddedLength) - change.AddedLength : change.AddedLength];
                    tbTime.Text.CopyTo(change.Offset, newString, 0, newString.Length);

                    for (int i = 0; i < newString.Length; i++)
                    {
                        char addedChar = tbTime.Text[change.Offset + i];
                        char conv;

                        switch (change.Offset + i)
                        {
                            case 0 when !char.IsDigit(addedChar):
                                goto discard;

                            case 0 when int.Parse(addedChar.ToString()) == 2 && int.Parse(char.IsDigit(conv = (change.Offset + i + 1 > change.Offset + change.AddedLength - 1 ? tbTime.Text[1] : newString[i + 1])) ? conv.ToString() : "0") > 3:
                                if (change.Offset + i + 1 > change.Offset + change.AddedLength - 1)
                                {
                                    prevString[1] = '3';
                                }
                                else
                                {
                                    newString[i + 1] = '3';
                                }
                                break;

                            case 0 when int.Parse(addedChar.ToString()) > 2:
                                newString[i] = '2';
                                break;

                            case 1 when !char.IsDigit(addedChar):

                                goto discard;

                            case 1 when int.Parse(addedChar.ToString()) > 3 && int.Parse(char.IsDigit(conv = (change.Offset == 0 ? newString[0] : prevString[0])) ? conv.ToString() : "0") == 2:
                                newString[i] = '3';
                                break;

                            case 2 when addedChar != ':':
                                goto discard;

                            case 3 when !char.IsDigit(addedChar):
                                goto discard;

                            case 3 when int.Parse(addedChar.ToString()) > 5:
                                newString[i] = '5';
                                break;

                            case 4 when !char.IsDigit(addedChar):
                                goto discard;

                            case 5 when addedChar != ':':
                                goto discard;

                            case 6 when !char.IsDigit(addedChar):
                                goto discard;

                            case 6 when int.Parse(addedChar.ToString()) > 5:
                                newString[i] = '5';
                                break;

                            case 7 when !char.IsDigit(addedChar):
                                goto discard;

                            discard:
                                tbTime.Text = prevText[tbTime];
                                moveCaret = 0;
                                break;

                            default:
                                break;
                        }

                        if (moveCaret == 0)
                        {
                            tbTime.CaretIndex = change.Offset;
                            prevCaretIndex[tbTime] = -1;
                            break;
                        }
                    }

                    if (moveCaret == 1)
                    {

                        newString.CopyTo(prevString, change.Offset);
                        tbTime.Text = new string(prevString);
                        tbTime.CaretIndex = change.Offset + 1;
                    }
                }

                else if (change.RemovedLength > change.AddedLength)
                {
                    if (change.Offset < 8)
                    {
                        tbTime.Text = tbTime.Text.Insert(change.Offset + change.AddedLength, template.Substring(change.Offset + change.AddedLength, change.RemovedLength - change.AddedLength));

                        if (tbTime.Text.Length > 8)
                        {
                            tbTime.Text = prevText[tbTime];
                        }

                        if (change.Offset != 0)
                        {
                            tbTime.CaretIndex = change.Offset - 1;
                            tbTime.SelectionLength = 1;
                        }
                    }
                }
            }

            prevText[tbTime] = tbTime.Text;
            tbTime.TextChanged += TbTime_TextChanged;
            tbTime.SelectionChanged += TbTime_SelectionChanged;
        }

        private void TbTime_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tbTime = e.Source as TextBox;
            tbTime.TextChanged -= TbTime_TextChanged;
            if (tbTime.Text.Contains('H') || tbTime.Text.Contains('m') || tbTime.Text.Contains('s'))
            {
                tbTime.Text = tbTime.Text.Replace('H', '_');
                tbTime.Text = tbTime.Text.Replace('m', '_');
                tbTime.Text = tbTime.Text.Replace('s', '_');
                prevText[tbTime] = tbTime.Text;

            }
            tbTime.TextChanged += TbTime_TextChanged;
        }

        private void DpEventDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            DatePicker dp = e.Source as DatePicker;
            dp.ClearValue(BorderBrushProperty);
            if (dp == dpEventStartDate)
                lblEventStartDateError.ClearValue(ContentProperty);
            else if (dp == dpEventEndDate)
                lblEventEndDateError.ClearValue(ContentProperty);
            if (dp.SelectedDate != null && dp == dpEventStartDate)
                dpEventEndDate.DisplayDate = (DateTime)dp.SelectedDate; 
        }

        private void TbTime_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tbTime = e.Source as TextBox;
            tbTime.SelectionChanged -= TbTime_SelectionChanged;
            tbTime.TextChanged -= TbTime_TextChanged;

            if (tbTime.Text[0] == '_' && tbTime.Text[1] == '_' && tbTime.Text[3] == '_' &&
                tbTime.Text[4] == '_' && tbTime.Text[6] == '_' && tbTime.Text[7] == '_')
            {
                tb.Text = timePlacehoder;
            }
            else
            {
                tbTime.Text = tbTime.Text.Replace('_', '0');
            }
            tbTime.SelectionChanged += TbTime_SelectionChanged;
            tbTime.TextChanged += TbTime_TextChanged;
            }
            // täyttää ajan perään nollia, jos mahtuu
            string fill = "00:00:00";
            tb.Text += fill.Substring(tb.Text.Length);
            // jos aika on väärässä muodossa, se tyhjennetään ja
            // pyydetään käyttäjää lattamaan uusi
            if (!Regex.IsMatch(tb.Text, "^((0[0-9])|(1[0-9])|(2[0-3])):([0-5][0-9]):([0-5][0-9])$"))
            {
                tb.Text = timePlacehoder;
                tb.BorderBrush = Brushes.Red;
                if (tb == tbEventStartTime)
                    lblEventStartTimeError.Content = "Please re-enter a valid time.";
                else if (tb == tbEventEndTime)
                    lblEventEndTimeError.Content = "Please re-enter a valid time.";
            }
            else
            {
                if (tb == tbEventStartTime)
                    lblEventStartTimeError.ClearValue(ContentProperty);
                else if (tb == tbEventEndTime)
                    lblEventEndTimeError.ClearValue(ContentProperty);
            }

        }

        private async void BtnCreateGpxFile_Click(object sender, RoutedEventArgs e)
        {
            // jotta ei tapahdu tupla klikkausta.
            BtnCreateGpxFile.IsEnabled = false;
            BtnCreateEventFile.IsEnabled = false;
            if (!ValidateParameters(out TimeSpan startTime, out TimeSpan endTime))
            {
                BtnCreateGpxFile.IsEnabled = true;
                BtnCreateEventFile.IsEnabled = true;
                return;
            }
            // kisan alku ja loppu datetimen muodostus.
            // ei pitäisi päästä tähän, jos on virheellisesti syötetty.
            if (!ValidTimePeriod(startTime, endTime, out DateTime eventStart, out DateTime eventEnd))
            {
                BtnCreateGpxFile.IsEnabled = true;
                BtnCreateEventFile.IsEnabled = true;
                return;
            }

            // Muutetaan kursori kertomaan käyttäjälle käynnissä olevasta datan käsittelystä.
            Cursor tempCursor = Cursor;
            Cursor = Cursors.Wait;
            ForceCursor = true;

            // tiedostojen luku
            SeamodeReader sr = new SeamodeReader(eventStart, eventEnd);

            foreach (var item in tbFilesInFolder.SelectedItems)
            {
                if (item is string)
                {
                    await Task.Run(() => sr.haeGpxData((string)item));
                }
            }

            // muuten tulee tyhjä tiedosto
            if (sr.gpxLines == null)
            {
                // Kursorin palautus.
                Cursor = tempCursor;
                ForceCursor = false;
                MessageBox.Show("No data found for specified time period.");
                BtnCreateGpxFile.IsEnabled = true;
                BtnCreateEventFile.IsEnabled = true;
                return;
            }

            SeamodeGpxWriter wr = new SeamodeGpxWriter(sr.gpxRaceTime);
            //string fullFilePath = $"{tbSavePath.Text}\\SeaMODE_{dpEventEndDate.SelectedDate}_{string.Join("", tbEventStartTime.Text.Split(':', '.'))}_{tbEventName.Text}.GPX";
            await Task.Run(() => wr.writeGpx(sr.gpxLines));

            Cursor = tempCursor;
            ForceCursor = false;

            StringBuilder msg = new StringBuilder();
            msg.AppendLine($"File {"file name here"} was created.");
            //if (!sr.PastEnd)
            //    msg.AppendLine("Data logging ended before the specified endpoint.");
            foreach (string line in sr.DataRowErrors)
            {
                msg.AppendLine(line);
            }
            msg.AppendLine("Would you like to open the folder?");
            var res = MessageBox.Show(msg.ToString(), "File Created.", MessageBoxButton.YesNo);
            if (res == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(tbSavePath.Text);

            BtnCreateGpxFile.IsEnabled = true;
            BtnCreateEventFile.IsEnabled = true;
            // syötetyt arvot tyhjennetään
            ResetUI();
            ListFilesInFolder();
        }

        void PickDateTimes()
        {
            for (int i = 1; i < tbFilesInFolder.Items.Count; i++)
            {
                using (FileStream fileStream = new FileStream((string)tbFilesInFolder.Items[i], FileMode.Open, FileAccess.Read))
                {
                    fileStream.Seek(0, SeekOrigin.Begin);
                    bool isDigit = false;
                    bool isNewLine = false;
                    char delim = ';';

                    while (!isDigit)
                    {
                        char c = Convert.ToChar(fileStream.ReadByte());

                        if (isNewLine && char.IsDigit(c))
                        {
                            List<char> stringBuilder = new List<char>();
                            isDigit = true;
                            isNewLine = false;

                            stringBuilder.Add(c);

                            while ((c = Convert.ToChar(fileStream.ReadByte())) != delim)
                            {
                                stringBuilder.Add(c);
                            }

                            string tempDateTimeStart = new string(stringBuilder.ToArray());
                            stringBuilder.Clear();

                            DateTime startDateTime = DateTime.Parse(tempDateTimeStart, CultureInfo.CreateSpecificCulture("fi-FI"));

                            while ((c = Convert.ToChar(fileStream.ReadByte())) != delim)
                            {
                                stringBuilder.Add(c);
                            }

                            tempDateTimeStart = new string(stringBuilder.ToArray());

                            startDateTime = startDateTime.Add(TimeSpan.Parse(tempDateTimeStart, CultureInfo.CreateSpecificCulture("fi-FI")));
                            minDTs.Add(startDateTime);
                        }
                        else if (isNewLine && char.IsLetter(c))
                        {
                            isNewLine = false;

                            delim = Convert.ToChar(fileStream.ReadByte());
                            while (char.IsLetterOrDigit(delim) || delim == '_')
                            {
                                delim = Convert.ToChar(fileStream.ReadByte());
                            }

                            while (c != '\n')
                            {
                                c = Convert.ToChar(fileStream.ReadByte());
                            }

                            isNewLine = true;
                        }
                        else if (c == '\n')
                        {
                            isNewLine = true;
                        }
                    }

                    Stack<char> stringBuilderStack = new Stack<char>();
                    long offset = 2;
                    isNewLine = false;

                    while (!isNewLine)
                    {
                        fileStream.Seek(-offset, SeekOrigin.End);
                        char c = Convert.ToChar(fileStream.ReadByte());

                        if (c == '\n')
                        {
                            isNewLine = true;
                        }
                        else
                        {
                            stringBuilderStack.Push(c);
                            offset++;
                        }
                    }

                    string lastFileLine = new string(stringBuilderStack.ToArray());
                    DateTime endDateTime = DateTime.Parse(lastFileLine.Substring(0, lastFileLine.IndexOf(delim)), CultureInfo.CreateSpecificCulture("fi-FI"));
                    endDateTime = endDateTime.Add(TimeSpan.Parse(lastFileLine.Substring(lastFileLine.IndexOf(delim) + 1, lastFileLine.IndexOf(delim, lastFileLine.IndexOf(delim) + 1) - (lastFileLine.IndexOf(delim) + 1)), CultureInfo.CreateSpecificCulture("fi-FI")));

                    if (endDateTime.Millisecond > 0)
                    {
                        endDateTime = endDateTime.AddSeconds(1);
                    }

                    maxDTs.Add(endDateTime);
                }
            }
        }

        private void UpdateDateTime()
        {
            if (tbFilesInFolder.SelectedItems.Count > 0)
            {
                DateTime minDate = DateTime.MaxValue;
                DateTime maxDate = DateTime.MinValue;
                List<int> selectedInds = new List<int>();

                foreach (var selectedFile in tbFilesInFolder.SelectedItems)
                {
                    if (selectedFile is string)
                    {
                        selectedInds.Add(tbFilesInFolder.Items.IndexOf(selectedFile) - 1);
                    }
                }

                foreach (var index in selectedInds)
                {
                    if (minDTs[index] < minDate)
                    {
                        minDate = minDTs[index];
                    }

                    if (maxDTs[index] > maxDate)
                    {
                        maxDate = maxDTs[index];
                    }
                }

                dpEventStartDate.SelectedDate = minDate.Date;
                dpEventEndDate.SelectedDate = maxDate.Date;

                tbEventStartTime.Text = $"{minDate.Hour}:{minDate.Minute}:{minDate.Second}";
                tbEventEndTime.Text = $"{maxDate.Hour}:{maxDate.Minute}:{maxDate.Second}";
            }
            else
            {
                dpEventStartDate.SelectedDate = null;
                dpEventEndDate.SelectedDate = null;

                tbEventStartTime.Text = "HH:mm:ss";
                tbEventEndTime.Text = "HH:mm:ss";
            }
        }

        private void tbFilesInFolder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Contains(lbiCheckBox))
            {
                cbSelectAll.IsChecked = false;
            }
            else if (e.AddedItems.Contains(lbiCheckBox))
            {
                cbSelectAll.IsChecked = true;
            }
            else if (lbiCheckBox.IsSelected && tbFilesInFolder.SelectedItems.Count == tbFilesInFolder.Items.Count - 1)
            {
                cbSelectAll.Unchecked -= ListBox_UnselectAll;
                cbSelectAll.IsChecked = false;
                cbSelectAll.Unchecked += ListBox_UnselectAll;

                tbFilesInFolder.SelectionChanged -= tbFilesInFolder_SelectionChanged;
                lbiCheckBox.IsSelected = false;
                tbFilesInFolder.SelectionChanged += tbFilesInFolder_SelectionChanged;
                UpdateDateTime();
            }
            else if (!lbiCheckBox.IsSelected && tbFilesInFolder.SelectedItems.Count == tbFilesInFolder.Items.Count - 1)
            {
                cbSelectAll.IsChecked = true;
            }
            else
            {
                UpdateDateTime();
            }

        }

        private void ListBox_SelectAll(object sender, RoutedEventArgs e)
        {
            tbFilesInFolder.SelectionChanged -= tbFilesInFolder_SelectionChanged;
            tbFilesInFolder.SelectAll();
            UpdateDateTime();
            tbFilesInFolder.SelectionChanged += tbFilesInFolder_SelectionChanged;
        }

        private void ListBox_UnselectAll(object sender, RoutedEventArgs e)
        {
            tbFilesInFolder.SelectionChanged -= tbFilesInFolder_SelectionChanged;
            tbFilesInFolder.UnselectAll();
            UpdateDateTime();
            tbFilesInFolder.SelectionChanged += tbFilesInFolder_SelectionChanged;
        }

        private void TbTime_SelectionChanged(object sender, RoutedEventArgs e)
        {
            TextBox tbTime = e.Source as TextBox;

            tbTime.SelectionChanged -= TbTime_SelectionChanged;
            if (tbTime.CaretIndex >= 0 && tbTime.CaretIndex < tbTime.Text.Length)
            {

                if (tbTime.SelectionLength == 0 && tbTime.CaretIndex == prevCaretIndex[tbTime] && tbTime.CaretIndex != 0)
                {
                    tbTime.CaretIndex--;
                }

                if (tbTime.CaretIndex == tbTime.Text.Length)
                {
                    tbTime.CaretIndex--;
                }
                else if (tbTime.Text[tbTime.CaretIndex] == ':')
                {
                    if (prevCaretIndex[tbTime] > tbTime.CaretIndex)
                    {
                        tbTime.CaretIndex--;
                    }
                    else
                    {
                        tbTime.CaretIndex++;
                    }
                }

                tbTime.SelectionLength = 1;
            }

            if (tbTime.CaretIndex == 8)
            {
                tbTime.CaretIndex = 7;
                tbTime.SelectionLength = 1;
            }

            prevCaretIndex[tbTime] = tbTime.CaretIndex;
            tbTime.SelectionChanged += TbTime_SelectionChanged;
        }
    }
}
