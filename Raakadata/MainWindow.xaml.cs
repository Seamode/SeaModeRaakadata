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
using System.Globalization;

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
        public MainWindow()
        {
            InitializeComponent();

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
            BtnCreateEventFile.IsEnabled = false;
            if (dpEventStartDate.SelectedDate == null || dpEventEndDate.SelectedDate == null)
            {
                MessageBox.Show("Please select start and end dates for the race.");
                BtnCreateEventFile.IsEnabled = true;
                return;
            }
            if (!TimeSpan.TryParse(tbEventStartTime.Text, out TimeSpan startTime) ||
                !TimeSpan.TryParse(tbEventEndTime.Text, out TimeSpan endTime))
            {
                MessageBox.Show("Please enter start and end times for the race.");
                BtnCreateEventFile.IsEnabled = true;
                return;
            }
            if (string.IsNullOrEmpty(tbEventName.Text))
            {
                MessageBox.Show("Please enter a name for the race.");
                BtnCreateEventFile.IsEnabled = true;
                return;
            }
            // kisan alku ja loppu datetimen muodostus.
            // ei pitäisi päästä tähän, jos on virheellisesti syötetty.
            DateTime eventStart = (DateTime)dpEventStartDate.SelectedDate;
            eventStart = eventStart.Add(startTime);
            DateTime eventEnd = (DateTime)dpEventEndDate.SelectedDate;
            eventEnd = eventEnd.Add(endTime);
            if (eventEnd <= eventStart)
            {
                MessageBox.Show("Check the dates and times.\nRace start must be before the ending.");
                BtnCreateEventFile.IsEnabled = true;
                return;
            }
            // tiedostojen luku
            SeamodeReader sr = new SeamodeReader(eventStart, eventEnd);
            await sr.ReadAndWriteFilesAsync(tbFolderPath.Text);
            // muuten tulee tyhjä tiedosto
            if (sr.DataRowCount == 0)
            {
                MessageBox.Show("No data found for specified time period.");
                BtnCreateEventFile.IsEnabled = true;
                return;
            }
            // kisatiedoston luonti
            File.Move(sr.TmpFile, tbEventFilePath.Text);
            MessageBox.Show($"File {tbEventFilePath.Text} was created.");
            if (sr.DataRowErrors.Count > 0)
            {
                MessageBox.Show($"{string.Join("\n", sr.DataRowErrors)}");
            }
            BtnCreateEventFile.IsEnabled = true;
            // syötetyt arvot tyhjennetään
            dpEventStartDate.ClearValue(DatePicker.SelectedDateProperty);
            dpEventEndDate.ClearValue(DatePicker.SelectedDateProperty);
            tbEventStartTime.Text = "HH:mm:ss";
            tbEventEndTime.Text = "HH:mm:ss";
            tbEventName.Clear();
            ListFilesInFolder();
        }

        private void TbEventFilePath_TextChanged(object sender, TextChangedEventArgs e) 
            => UpdateEventFilePath();

        private void UpdateEventFilePath()
        {
            string polku = tbSavePath == null ? "<Path>" : tbSavePath.Text;
            string pvm = dpEventStartDate.SelectedDate == null ? "<Date>" : $"{dpEventStartDate.SelectedDate:yyyyMMdd}";
            string aika = tbEventStartTime.Text == "HH:mm:ss" ? "<Time>" : string.Join("", tbEventStartTime.Text.Split(':', '.'));
            string nimi = string.IsNullOrEmpty(tbEventName.Text) ? "<RaceName>" : tbEventName.Text;
            tbEventFilePath.Text = $"{polku}\\SeaMODE_{pvm}_{aika}_{nimi}.csv";
        }

        private void TbTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tbTime = e.Source as TextBox;
            const string template = "__:__:__";

            tbTime.SelectionChanged -= TbTime_SelectionChanged;
            tbTime.TextChanged -= TbTime_TextChanged;

            if (tbTime.IsFocused)
            {
                foreach (var change in e.Changes)
                {
                    if (change.AddedLength > 0)
                    {
                        char addedChar = tbTime.Text.Substring(change.Offset, change.AddedLength)[0];

                        if (!char.IsDigit(addedChar))
                        {
                            tbTime.Text = prevText[tbTime];
                            tbTime.CaretIndex = change.Offset;
                            tbTime.SelectionLength = 1;
                        }
                        else
                        {
                            char[] maxValue = null;
                            char conv;

                            switch (change.Offset)
                            {
                                case 0 when int.Parse(addedChar.ToString()) == 2 && int.Parse(char.IsDigit(conv = tbTime.Text[1]) ? conv.ToString() : "0") > 3:
                                    "3".CopyTo(0, maxValue = tbTime.Text.ToArray(), 1, 1);
                                    goto CopyValue;

                                case 0 when int.Parse(addedChar.ToString()) > 2:
                                    "2".CopyTo(0, maxValue = tbTime.Text.ToArray(), change.Offset, 1);
                                    goto CopyValue;

                                case 1 when int.Parse(addedChar.ToString()) > 3 && int.Parse(char.IsDigit(conv = tbTime.Text[0]) ? conv.ToString() : "0") == 2:
                                    "3".CopyTo(0, maxValue = tbTime.Text.ToArray(), change.Offset, 1);
                                    goto CopyValue;

                                case 3 when int.Parse(addedChar.ToString()) > 5:
                                    "5".CopyTo(0, maxValue = tbTime.Text.ToArray(), change.Offset, 1);
                                    goto CopyValue;

                                case 6 when int.Parse(addedChar.ToString()) > 5:
                                    "5".CopyTo(0, maxValue = tbTime.Text.ToArray(), change.Offset, 1);
                                    goto CopyValue;

                                CopyValue:
                                    tbTime.Text = new string(maxValue);
                                    goto default;

                                default:
                                    tbTime.CaretIndex = change.Offset + 1;
                                    tbTime.SelectionLength = 1;
                                    break;
                            }

                        }
                    }

                    else if (change.AddedLength == 0 && change.RemovedLength > 0)
                    {
                        tbTime.Text = tbTime.Text.Insert(change.Offset, template.Substring(change.Offset, change.RemovedLength));
                        if (change.Offset != 0)
                        {
                            if ((tbTime.Text[change.Offset] == '0' && tbTime.Text[change.Offset] != ':') && (tbTime.Text[change.Offset - 1] != '0' && tbTime.Text[change.Offset - 1] != ':'))
                            {
                                char[] tempString = tbTime.Text.ToArray();
                                tempString[change.Offset] = tbTime.Text[change.Offset - 1];
                                tempString[change.Offset - 1] = tbTime.Text[change.Offset];

                                tbTime.Text = new string(tempString);
                            }

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
            if (tbTime.Text == "HH:mm:ss")
            {
                tbTime.Text = "__:__:__";
                prevText[tbTime] = tbTime.Text;
            }
            tbTime.TextChanged += TbTime_TextChanged;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            string s = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            //string s = Directory.GetParent(Assembly.GetExecutingAssembly().Location).ToString();
            //string s = System.IO.Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            if (s.EndsWith("prg"))
            {
                s = s.Replace("prg", "dat");
            }
            MessageBox.Show(s);
        }

        private void DpEventStartDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e) 
            => UpdateEventFilePath();

        private void TbTime_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tbTime = e.Source as TextBox;
            if (tbTime.Text[0] == '_' && tbTime.Text[1] == '_' && tbTime.Text[3] == '_' &&
                tbTime.Text[4] == '_' && tbTime.Text[6] == '_' && tbTime.Text[7] == '_')
            {
                tbTime.Text = "HH:mm:ss";
            }
            else
            {
                tbTime.Text = tbTime.Text.Replace('_', '0');
            }
        }

        private async void BtnCreatePgxFile_Click(object sender, RoutedEventArgs e)
        {
            // jotta ei tapahdu tupla klikkausta.
            BtnCreatePgxFile.IsEnabled = false;
            BtnCreateEventFile.IsEnabled = false;
            if (dpEventStartDate.SelectedDate == null || dpEventEndDate.SelectedDate == null)
            {
                MessageBox.Show("Please select start and end dates for the race.");
                BtnCreatePgxFile.IsEnabled = true;
                BtnCreateEventFile.IsEnabled = true;
                return;
            }
            if (!TimeSpan.TryParse(tbEventStartTime.Text, out TimeSpan startTime) ||
                !TimeSpan.TryParse(tbEventEndTime.Text, out TimeSpan endTime))
            {
                MessageBox.Show("Please enter start and end times for the race.");
                BtnCreatePgxFile.IsEnabled = true;
                BtnCreateEventFile.IsEnabled = true;
                return;
            }
            
            /*
            if (string.IsNullOrEmpty(tbEventName.Text))
            {
                MessageBox.Show("Please enter a name for the race.");
                BtnCreatePgxFile.IsEnabled = true;
                return;
            }
            */
            
            // kisan alku ja loppu datetimen muodostus.
            // ei pitäisi päästä tähän, jos on virheellisesti syötetty.
            DateTime eventStart = (DateTime)dpEventStartDate.SelectedDate;
            eventStart = eventStart.Add(startTime);
            DateTime eventEnd = (DateTime)dpEventEndDate.SelectedDate;
            eventEnd = eventEnd.Add(endTime);
            if (eventEnd <= eventStart)
            {
                MessageBox.Show("Check the dates and times.\nRace start must be before the ending.");
                BtnCreatePgxFile.IsEnabled = true;
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
                BtnCreatePgxFile.IsEnabled = true;
                BtnCreateEventFile.IsEnabled = true;
                return;
            }

            SeamodeGpxWriter wr = new SeamodeGpxWriter(sr.gpxRaceTime);
            
            await Task.Run(() => wr.writeGpx(sr.gpxLines));

            Cursor = tempCursor;
            ForceCursor = false;

            MessageBox.Show($"File {ConfigurationManager.AppSettings["gpxFile"]} was created.");
            if (sr.DataRowErrors.Count > 0)
            {
                MessageBox.Show($"{string.Join("\n", sr.DataRowErrors)}");
            }
            BtnCreatePgxFile.IsEnabled = true;
            BtnCreateEventFile.IsEnabled = true;
            // syötetyt arvot tyhjennetään
            dpEventStartDate.ClearValue(DatePicker.SelectedDateProperty);
            dpEventEndDate.ClearValue(DatePicker.SelectedDateProperty);
            tbEventStartTime.Text = "HH:mm:ss";
            tbEventEndTime.Text = "HH:mm:ss";
            tbEventName.Clear();
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
