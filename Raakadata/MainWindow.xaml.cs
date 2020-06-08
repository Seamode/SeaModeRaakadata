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
using System.Collections;
using System.Linq.Expressions;
using System.Windows.Media.Animation;

namespace Raakadata
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<DateTime> minDTs = new List<DateTime>();
        private List<DateTime> maxDTs = new List<DateTime>();
        private Dictionary<TextBox, int> prevCaretIndex = new Dictionary<TextBox, int>();
        private Dictionary<TextBox, string> prevText = new Dictionary<TextBox, string>();
        private Dictionary<TextBox, int> selectedChar = new Dictionary<TextBox, int>();
        private Dictionary<TextBox, int> selectedCount = new Dictionary<TextBox, int>();
        private Dictionary<TextBox, Border> caret = new Dictionary<TextBox, Border>();
        private Dictionary<TextBox, Border> selectionCaret = new Dictionary<TextBox, Border>();
        private Dictionary<TextBox, Stack<string>> undoHistory = new Dictionary<TextBox, Stack<string>>();
        private Dictionary<TextBox, Stack<string>> redoHistory = new Dictionary<TextBox, Stack<string>>();
        private Dictionary<TextBox, bool> isModified = new Dictionary<TextBox, bool>();
        private Dictionary<TextBox, Dictionary<int, Border>> underscoreChars = new Dictionary<TextBox, Dictionary<int, Border>>();
        private bool mouseLeftButtonSelect = false;
        private double prevMouseX;
        private readonly string timePlacehoder = "HH:mm:ss";
        private struct TxtChange
        {
            public int Offset;
            public int AddedLength;
            public int RemovedLength;
        }
        public MainWindow()
        {
            InitializeComponent();

            tbEventStartTime.TextChanged += TbTime_TextChanged;
            tbEventStartTime.SelectionChanged += TbTime_SelectionChanged;
            tbEventEndTime.TextChanged += TbTime_TextChanged;
            tbEventEndTime.SelectionChanged += TbTime_SelectionChanged;
            AddHandler(Window.MouseMoveEvent, new MouseEventHandler(Window_MouseMove), true);

            string defaultPath = FindDatDirectory();
            tbFolderPath.Text = defaultPath;
            tbSavePath.Text = defaultPath;
            if (!string.IsNullOrEmpty(defaultPath))
                ListFilesInFolder();
            // jotta tiedostot on helppo valita testausta varten.
            dpEventStartDate.DisplayDate = new DateTime(2019, 09, 28);
            dpEventEndDate.DisplayDate = new DateTime(2019, 09, 28);
        }
        private void Cut(object sender)
        {
            TextBox tbTime = sender as TextBox;
            tbTime.SelectionChanged -= TbTime_SelectionChanged;
            tbTime.TextChanged -= TbTime_TextChanged;

            if (tbTime.SelectionLength > 0)
            {
                int tempCaretIndex = tbTime.CaretIndex;
                int tempSelectionLength = tbTime.SelectionLength;

                Clipboard.SetText(tbTime.SelectedText.Replace(' ', '0'));
                tbTime.Text = tbTime.Text.Remove(tbTime.CaretIndex, tbTime.SelectionLength);
                List<TxtChange> changes = new List<TxtChange>();
                changes.Add(new TxtChange()
                {
                    AddedLength = 0,
                    RemovedLength = tempSelectionLength,
                    Offset = tempCaretIndex
                });
                TextChanged(tbTime, changes);

                tbTime.CaretIndex = tempCaretIndex;
                tbTime.SelectionLength = tempSelectionLength;
            }

            tbTime.SelectionChanged += TbTime_SelectionChanged;
            tbTime.TextChanged += TbTime_TextChanged;
        }
        private void Copy(object sender)
        {
            TextBox tbTime = sender as TextBox;

            if (tbTime.SelectionLength > 0)
            {
                Clipboard.SetText(tbTime.SelectedText.Replace(' ', '0'));
            }
        }
        private void Paste(object sender)
        {
            TextBox tbTime = sender as TextBox;
            tbTime.SelectionChanged -= TbTime_SelectionChanged;
            tbTime.TextChanged -= TbTime_TextChanged;

            if (Clipboard.ContainsText() == true)
            {
                int addedLength = 0;
                int offset = 0;
                int digits = 0;
                int i;
                int tempCaretIndex = tbTime.CaretIndex;
                int tempSelectionLength = tbTime.SelectionLength;
                string pasteString = Clipboard.GetText();
                StringBuilder zeroed = new StringBuilder(pasteString);

                for (i = 0; i < pasteString.Length; i++)
                {
                    if (char.IsDigit(pasteString[i]))
                    {
                        digits++;
                    }
                    else
                    {
                        if (pasteString[i] != ':')
                        {
                            zeroed[offset + digits] = ':';
                        }

                        addedLength = 2 - digits;

                        if (addedLength < 0)
                        {
                            zeroed.Remove(offset, Math.Abs(addedLength));
                        }
                        else
                        {
                            for (int j2 = 0; j2 < addedLength; j2++)
                            {
                                zeroed.Insert(offset, '0');
                            }
                        }

                        offset += addedLength + digits + 1;
                        digits = 0;
                    }
                }

                addedLength = 2 - digits;

                if (addedLength < 0)
                {
                    zeroed.Remove(offset, Math.Abs(addedLength));
                }
                else
                {
                    for (int j2 = 0; j2 < addedLength; j2++)
                    {
                        zeroed.Insert(offset, '0');
                    }
                }

                int insOffset = tbTime.Text.Substring(0, tbTime.CaretIndex).LastIndexOf(':');

                if (insOffset == -1)
                {
                    insOffset = 0;
                }
                else
                {
                    insOffset++;
                }

                tbTime.Text = tbTime.Text.Remove(insOffset, insOffset + zeroed.Length > 8 ? tbTime.Text.Length - insOffset : zeroed.Length);
                tbTime.Text = tbTime.Text.Replace(' ', '0');


                if (insOffset > tbTime.Text.Length - 1)
                {
                    tbTime.Text = tbTime.Text + zeroed;
                }
                else
                {
                    tbTime.Text = tbTime.Text.Insert(insOffset, zeroed.ToString());
                }

                List<TxtChange> changes = new List<TxtChange>();
                changes.Add(new TxtChange()
                {
                    AddedLength = zeroed.Length,
                    RemovedLength = insOffset + zeroed.Length > 8 ? tbTime.Text.Length - insOffset : zeroed.Length,
                    Offset = insOffset
                });

                TextChanged(tbTime, changes);

                tbTime.CaretIndex = tempCaretIndex;
                tbTime.SelectionLength = tempSelectionLength;
            }

            tbTime.SelectionChanged += TbTime_SelectionChanged;
            tbTime.TextChanged += TbTime_TextChanged;
        }
        bool canUndo(object sender)
        {
            TextBox tbTime = sender as TextBox;

            if (undoHistory[tbTime].Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        void undo(object sender)
        {
            TextBox tbTime = sender as TextBox;
            tbTime.TextChanged -= TbTime_TextChanged;
            tbTime.SelectionChanged -= TbTime_SelectionChanged;

            int tempCaretIndex = tbTime.CaretIndex;
            int tempSelectionLength = tbTime.SelectionLength;


            if (isModified[tbTime] == true)
            {
                redoHistory[tbTime].Push(tbTime.Text);
            }
            else
            {
                isModified[tbTime] = true;
            }
            tbTime.Text = undoHistory[tbTime].Pop();
            prevText[tbTime] = tbTime.Text;

            if (selectedChar[tbTime] == tempCaretIndex)
            {
                tbTime.CaretIndex = tempCaretIndex;

                for (int i = 0; i < tempSelectionLength; i++)
                {
                    EditingCommands.SelectRightByCharacter.Execute(null, tbTime);
                }
            }
            else
            {
                tbTime.CaretIndex = tempCaretIndex + tempSelectionLength;

                for (int i = 0; i < tempSelectionLength; i++)
                {
                    EditingCommands.SelectLeftByCharacter.Execute(null, tbTime);
                }
            }

            for (int i = 0; i < tbTime.Text.Length; i++)
            {
                if (tbTime.Text[i] == ' ' && (underscoreChars[tbTime])[i].Visibility == Visibility.Collapsed)
                {
                    (underscoreChars[tbTime])[i].Visibility = Visibility.Visible;
                }
                else if (tbTime.Text[i] != ':' && tbTime.Text[i] != ' ' && (underscoreChars[tbTime])[i].Visibility == Visibility.Visible)
                {
                    (underscoreChars[tbTime])[i].Visibility = Visibility.Collapsed;
                }
            }

            tbTime.TextChanged += TbTime_TextChanged;
            tbTime.SelectionChanged += TbTime_SelectionChanged;
        }
        bool canRedo(object sender)
        {
            TextBox tbTime = sender as TextBox;

            if (redoHistory[tbTime].Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        void redo(object sender)
        {
            TextBox tbTime = sender as TextBox;
            tbTime.TextChanged -= TbTime_TextChanged;
            tbTime.SelectionChanged -= TbTime_SelectionChanged;

            int tempCaretIndex = tbTime.CaretIndex;
            int tempSelectionLength = tbTime.SelectionLength;

            if (redoHistory[tbTime].Count > 0)
            {
                undoHistory[tbTime].Push(tbTime.Text);
                tbTime.Text = redoHistory[tbTime].Pop();
                prevText[tbTime] = tbTime.Text;
            }

            if (selectedChar[tbTime] == tempCaretIndex)
            {
                tbTime.CaretIndex = tempCaretIndex;

                for (int i = 0; i < tempSelectionLength; i++)
                {
                    EditingCommands.SelectRightByCharacter.Execute(null, tbTime);
                }
            }
            else
            {
                tbTime.CaretIndex = tempCaretIndex + tempSelectionLength;

                for (int i = 0; i < tempSelectionLength; i++)
                {
                    EditingCommands.SelectLeftByCharacter.Execute(null, tbTime);
                }
            }

            for (int i = 0; i < tbTime.Text.Length; i++)
            {
                if (tbTime.Text[i] == ' ' && (underscoreChars[tbTime])[i].Visibility == Visibility.Collapsed)
                {
                    (underscoreChars[tbTime])[i].Visibility = Visibility.Visible;
                }
                else if (tbTime.Text[i] != ':' && tbTime.Text[i] != ' ' && (underscoreChars[tbTime])[i].Visibility == Visibility.Visible)
                {
                    (underscoreChars[tbTime])[i].Visibility = Visibility.Collapsed;
                }
            }

            tbTime.TextChanged += TbTime_TextChanged;
            tbTime.SelectionChanged += TbTime_SelectionChanged;
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

            List<String> fileList = SeamodeReader.FetchFilesToList(tbFolderPath.Text);

            if (fileList.Count > 0)
            {
                PickDateTimes(fileList);
            }
        }

        private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            // https://stackoverflow.com/questions/1922204/open-directory-dialog
            // Ookii.Diologs.Wpf
            var fd = new VistaFolderBrowserDialog();
            if (fd.ShowDialog(this).GetValueOrDefault())
            {
                tbEventStartTime.TextChanged -= TbTime_TextChanged;
                tbEventEndTime.TextChanged -= TbTime_TextChanged;
                tbEventStartTime.Text = timePlacehoder;
                tbEventEndTime.Text = timePlacehoder;
                tbEventStartTime.TextChanged += TbTime_TextChanged;
                tbEventEndTime.TextChanged += TbTime_TextChanged;
                prevCaretIndex[tbEventStartTime] = -1;
                prevText[tbEventStartTime] = tbEventStartTime.Text;
                prevCaretIndex[tbEventEndTime] = -1;
                prevText[tbEventEndTime] = tbEventEndTime.Text;
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
            string fullFilePath = $"{tbSavePath.Text}\\SeaMODE_{dpEventStartDate.SelectedDate:yyyyMMdd}_{string.Join("", tbEventStartTime.Text.Split(':', '.'))}_{tbEventName.Text}.csv";
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
      
        private async void btnMakeGpxFile_Click(object sender, RoutedEventArgs e)
        {

            if (tbSavePath.Text == "")
            {
                MessageBox.Show("Parse to must be filled");
                tbSavePath.Focus();
                return;
            }
            if (tbEventName.Text == "")
            {
                MessageBox.Show("Output file must be filled");
                tbEventName.Focus();
                return;
            }

            string chosenFile = null;
            OpenFileDialog openFile = new OpenFileDialog() { Filter = "csv file (*.csv)|*.csv" };
            if (openFile.ShowDialog() == true)
            {
                var res = MessageBox.Show("Are you sure", $"make GPX file from {openFile.FileName}", MessageBoxButton.YesNo);
                if (res == MessageBoxResult.No)
                    return;
                chosenFile = openFile.FileName;
            } else
            {
                MessageBox.Show("No file chosen");
                return;
            }


            int luku = tbFilesInFolder.SelectedItems.Count;

            if (chosenFile == null || chosenFile == "")
            {
                MessageBox.Show("No file selected");  // Tätä ei ehkä tarvita
                return;
            }
            // Tarkistukset tehty - nyt generoidaan tiedosto yhdestä tiedostosta
            // Aijojen ei ole väliä
            DateTime start = DateTime.MinValue;
            DateTime end = DateTime.MaxValue;
            SeamodeReader sr = new SeamodeReader(start, end);

            // Muutetaan kursori kertomaan käyttäjälle käynnissä olevasta datan käsittelystä.
            Cursor tempCursor = Cursor;
            Cursor = Cursors.Wait;
            ForceCursor = true;

            await Task.Run(() => sr.haeGpxData(chosenFile));
            //sr.haeGpxData(chosenFile);

            // Kursorin palautus.
            Cursor = tempCursor;
            ForceCursor = false;

            if (sr.gpxLines != null && sr.gpxLines.Count > 0)
            {
                // Tehdään funktio joka hakee riviltä noin 14 aloituspäivän
                string saveGpxFile = getGpxFileName();
                SeamodeGpxWriter gpxWriter = new SeamodeGpxWriter(sr.gpxRaceTime);
                gpxWriter.writeGpx(sr.gpxLines, saveGpxFile);
                var res = MessageBox.Show("Would you like open file  folder", $"File {saveGpxFile} was created", MessageBoxButton.YesNo);
                // avaa File Explorerin
                if (res == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(tbSavePath.Text);
            }
            else
            {
                if (sr.DataRowErrors.Count > 0)
                {
                    StringBuilder msg = new StringBuilder();
                    msg.Append($"There were {sr.DataRowErrors.Count} errors on the file");
                    msg.Append("The first errorline is: ");
                    msg.Append(sr.DataRowErrors[0]);
                    var res = MessageBox.Show("Would you like to create error log file", msg.ToString(), MessageBoxButton.YesNo);
                    if (res == MessageBoxResult.Yes)
                        CreateErrorLog(sr.DataRowErrors);
        
                }
            }
        }
        private async void CreateErrorLog(List<string> errors)
        {
            SeamodeWriter sw = new SeamodeWriter();
            sw.OutFile = tbSavePath.Text + @"/" + "errorGpxParse.log";
            ArrayList arrayListErrors = new ArrayList(errors);
            Cursor tempCursor = Cursor;
            Cursor = Cursors.Wait;
            ForceCursor = true;
            await Task.Run(() => sw.Kirjoita(arrayListErrors));
            Cursor = tempCursor;
            ForceCursor = false;
            var res = MessageBox.Show("Would you like open errorLog folder", $"Errors written to file{sw.OutFile}", MessageBoxButton.YesNo);
            if (res == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(tbSavePath.Text);
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
            tbEventStartTime.TextChanged -= TbTime_TextChanged;
            tbEventEndTime.TextChanged -= TbTime_TextChanged;
            tbEventStartTime.Text = timePlacehoder;
            tbEventEndTime.Text = timePlacehoder;
            tbEventStartTime.TextChanged += TbTime_TextChanged;
            tbEventEndTime.TextChanged += TbTime_TextChanged;
            prevCaretIndex[tbEventStartTime] = -1;
            prevText[tbEventStartTime] = tbEventStartTime.Text;
            prevCaretIndex[tbEventEndTime] = -1;
            prevText[tbEventEndTime] = tbEventEndTime.Text;
            minDTs.Clear();
            maxDTs.Clear();
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
            TextBox tbFile = e.Source as TextBox;
            tbFile.ClearValue(BorderBrushProperty);
            if (tbFile == tbFolderPath)
                lblFolderPathError.ClearValue(ContentProperty);
            else if (tbFile == tbSavePath)
                lblSavePathError.ClearValue(ContentProperty);
            else if (tbFile == tbEventName)
                lblEventNameError.ClearValue(ContentProperty);
        }
        private void TextChanged(TextBox tbTime, List<TxtChange> changes)
        {
            const string template = "  :  :  ";
            bool copyText = true;

            //tbTime.SelectionChanged -= TbTime_SelectionChanged;

            foreach (var change in changes)
            {
                if (change.AddedLength > 0)
                {
                    char[] prevString;

                    tbTime.Text = tbTime.Text.Replace('H', ' ');
                    tbTime.Text = tbTime.Text.Replace('m', ' ');
                    tbTime.Text = tbTime.Text.Replace('s', ' ');

                    if (change.Offset + change.AddedLength < 9 && change.RemovedLength > change.AddedLength)
                    {
                        prevString = (prevText[tbTime].Substring(0, change.Offset + change.AddedLength) + prevText[tbTime].Substring(change.Offset + change.AddedLength + (change.RemovedLength - change.AddedLength))).ToArray();
                    }
                    else
                    {
                        prevString = prevText[tbTime].ToArray();
                    }

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

                            case 0 when /*change.RemovedLength > change.AddedLength && change.Offset + change.AddedLength != 1  &&*/ (change.AddedLength == 1 && !(change.RemovedLength > 1) || change.AddedLength > 1) && int.Parse(addedChar.ToString()) >= 2 && int.Parse(char.IsDigit(conv = (change.Offset + i + 1 > change.Offset + change.AddedLength - 1 ? tbTime.Text[1] : newString[i + 1])) ? conv.ToString() : "0") > 3:
                                if (change.Offset + i + 1 > change.Offset + change.AddedLength - 1)
                                {
                                    prevString[1] = '3';
                                }
                                else
                                {
                                    newString[i + 1] = '3';
                                }

                                if (int.Parse(addedChar.ToString()) >= 2)
                                {
                                    newString[i] = '2';
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
                                copyText = false;
                                break;

                            default:
                                break;
                        }

                        if (copyText == false)
                        {
                            tbTime.CaretIndex = prevCaretIndex[tbTime] == -1 ? 0 : prevCaretIndex[tbTime];
                            break;
                        }
                    }

                    if (copyText == true)
                    {
                        newString.CopyTo(prevString, change.Offset);
                        tbTime.Text = new string(prevString);
                        tbTime.CaretIndex = change.Offset + change.AddedLength;
                    }
                }

                if (change.RemovedLength > change.AddedLength && copyText == true)
                {
                    if (change.Offset < 8)
                    {
                        if (template[change.Offset] == ':')
                        {
                            char[] newString = tbTime.Text.ToArray();
                            newString[change.Offset - 1] = ' ';
                            tbTime.Text = new string(newString);
                        }

                        tbTime.Text = tbTime.Text.Insert(change.Offset + change.AddedLength, template.Substring(change.Offset + change.AddedLength, change.RemovedLength - change.AddedLength));

                        if (change.Offset != 0)
                        {
                            tbTime.CaretIndex = change.Offset;
                        }
                    }
                }
            }

            if (tbTime.Text != prevText[tbTime])
            {
                if (isModified[tbTime] == true)
                {
                    undoHistory[tbTime].Push(prevText[tbTime]);
                }
                else
                {
                    isModified[tbTime] = true;
                }

                if (redoHistory[tbTime].Count > 0)
                {
                    redoHistory[tbTime].Clear();
                }
            }

            for (int i = 0; i < tbTime.Text.Length; i++)
            {
                if (tbTime.Text[i] == ' ' && (underscoreChars[tbTime])[i].Visibility == Visibility.Collapsed)
                {
                    (underscoreChars[tbTime])[i].Visibility = Visibility.Visible;
                }
                else if (tbTime.Text[i] != ':' && tbTime.Text[i] != ' ' && (underscoreChars[tbTime])[i].Visibility == Visibility.Visible)
                {
                    (underscoreChars[tbTime])[i].Visibility = Visibility.Collapsed;
                }
            }

            prevText[tbTime] = tbTime.Text;
        }
        private void TbTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tbTime = e.Source as TextBox;
            tbTime.ClearValue(BorderBrushProperty);

            tbTime.TextChanged -= TbTime_TextChanged;
            List<TxtChange> changes = new List<TxtChange>();
            foreach (var change in e.Changes)
            {
                TxtChange txtChange = new TxtChange();
                txtChange.AddedLength = change.AddedLength;
                txtChange.Offset = change.Offset;
                txtChange.RemovedLength = change.RemovedLength;
                changes.Add(txtChange);
            }
            TextChanged(tbTime, changes);
            tbTime.TextChanged += TbTime_TextChanged;
        }

        private void TbTime_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tbTime = e.Source as TextBox;
            tbTime.TextChanged -= TbTime_TextChanged;
            if (tbTime.Text.Contains('H') || tbTime.Text.Contains('m') || tbTime.Text.Contains('s'))
            {
                tbTime.Text = tbTime.Text.Replace('H', ' ');
                tbTime.Text = tbTime.Text.Replace('m', ' ');
                tbTime.Text = tbTime.Text.Replace('s', ' ');

                prevText[tbTime] = tbTime.Text;
            }
            if (tbTime.CaretIndex != 8)
            {
                caret[tbTime].Visibility = Visibility.Visible;
            }
            for (int i = 0; i < tbTime.Text.Length; i++)
            {
                if (tbTime.Text[i] == ' ' && (underscoreChars[tbTime])[i].Visibility == Visibility.Collapsed)
                {
                    (underscoreChars[tbTime])[i].Visibility = Visibility.Visible;
                }
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

            if (!(FocusManager.GetFocusedElement(this) is ListBoxItem))
            {
                int underscores = tbTime.Text.Count((c) => c == ' ');
                if (underscores > 0)
                {
                    if (underscores == 6)
                    {
                        tbTime.Text = timePlacehoder;
                    }
                    else
                    {
                        undoHistory[tbTime].Push(tbTime.Text);
                        tbTime.Text = tbTime.Text.Replace(' ', '0');
                        prevText[tbTime] = tbTime.Text;
                        isModified[tbTime] = false;
                    }
                }
            }
            caret[tbTime].Visibility = Visibility.Collapsed;
            selectionCaret[tbTime].Visibility = Visibility.Collapsed;
            for (int i = 0; i < tbTime.Text.Length; i++)
            {
                if (tbTime.Text[i] != ':' && (underscoreChars[tbTime])[i].Visibility == Visibility.Visible)
                {
                    (underscoreChars[tbTime])[i].Visibility = Visibility.Collapsed;
                }
            }
            tbTime.SelectionChanged += TbTime_SelectionChanged;
            tbTime.TextChanged += TbTime_TextChanged;

            if (tbTime == tbEventStartTime && Regex.IsMatch(tbTime.Text, "^((0[0-9])|(1[0-9])|(2[0-3])):([0-5][0-9]):([0-5][0-9])$"))
                lblEventStartTimeError.ClearValue(ContentProperty);
            else if (tbTime == tbEventEndTime && Regex.IsMatch(tbTime.Text, "^((0[0-9])|(1[0-9])|(2[0-3])):([0-5][0-9]):([0-5][0-9])$"))
                lblEventEndTimeError.ClearValue(ContentProperty);
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
                    string p = $"{tbFolderPath.Text}\\{(string)item}";
                    await Task.Run(() => sr.haeGpxData(p));
                }
            }

            // muuten tulee tyhjä tiedosto
            if(sr.DataRowErrors != null && sr.DataRowErrors.Count > 0)
            {
                StringBuilder msg2= new StringBuilder();
                msg2.Append($"There were {sr.DataRowErrors.Count} errors on the file");
                msg2.Append("The first errorline is: ");
                msg2.Append(sr.DataRowErrors[0]);
                var res2 = MessageBox.Show("Would you like to create error log file", msg2.ToString(), MessageBoxButton.YesNo);
                if (res2 == MessageBoxResult.Yes)
                    CreateErrorLog(sr.DataRowErrors);
                Cursor = tempCursor;
                ForceCursor = false;
                return;
            }
            if (sr.gpxLines == null || sr.gpxLines.Count == 0)
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
            //string fullFilePath = $"{tbSavePath.Text}\\SeaMODE_{dpEventStartDate.SelectedDate}_{string.Join("", tbEventStartTime.Text.Split(':', '.'))}_{tbEventName.Text}.GPX";
            string saveGpxFile = getGpxFileName();
            await Task.Run(() => wr.writeGpx(sr.gpxLines, saveGpxFile));

            Cursor = tempCursor;
            ForceCursor = false;

            StringBuilder msg = new StringBuilder();
            msg.AppendLine($"File {saveGpxFile} was created.");
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

        void PickDateTimes(List<string> fileList)
        {
            Func<FileStream, int> readByte = (fs) => 
            {
            int charValue = fs.ReadByte();

            if (charValue == -1)
            {
                    throw new EndOfStreamException();
            }
            
            return charValue;
            };

            for (int i = 0; i < fileList.Count; i++)
            {
               try
               {
                    using (FileStream fileStream = new FileStream($"{tbFolderPath.Text}\\{fileList[i]}", FileMode.Open, FileAccess.Read))
                    {
                        fileStream.Seek(0, SeekOrigin.Begin);
                        bool isDigit = false;
                        bool isNewLine = false;
                        char delim = ';';

                        while (!isDigit)
                        {
                            char c = Convert.ToChar(readByte(fileStream));

                            if (isNewLine && char.IsDigit(c))
                            {
                                List<char> stringBuilder = new List<char>();
                                isDigit = true;
                                isNewLine = false;

                                stringBuilder.Add(c);

                                while ((c = Convert.ToChar(readByte(fileStream))) != delim)
                                {
                                    stringBuilder.Add(c);
                                }

                                string tempDateTimeStart = new string(stringBuilder.ToArray());
                                stringBuilder.Clear();

                                DateTime startDateTime = DateTime.Parse(tempDateTimeStart, CultureInfo.CreateSpecificCulture("fi-FI"));

                                while ((c = Convert.ToChar(readByte(fileStream))) != delim)
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

                                delim = Convert.ToChar(readByte(fileStream));
                                while (char.IsLetterOrDigit(delim) || delim == '_')
                                {
                                    delim = Convert.ToChar(readByte(fileStream));
                                }

                                while (c != '\n')
                                {
                                    c = Convert.ToChar(readByte(fileStream));
                                }

                                isNewLine = true;
                            }
                            else if (c == '\n')
                            {
                                isNewLine = true;
                            }
                            else
                            {
                                isNewLine = false;
                            }
                        }

                        Stack<char> stringBuilderStack = new Stack<char>();
                        long offset = 2;
                        isNewLine = false;

                        while (!isNewLine)
                        {
                            fileStream.Seek(-offset, SeekOrigin.End);
                            char c = Convert.ToChar(readByte(fileStream));

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

                        tbFilesInFolder.Items.Add(fileList[i]);
                    }
                }
                catch (EndOfStreamException)
                {
                    if (minDTs.Count > maxDTs.Count)
                    {
                        minDTs.RemoveAt(minDTs.Count - 1);
                        Console.Error.WriteLine($"Error: corrupted file '{fileList[i]}'");
                    }
                    else
                    {
                        Console.Error.WriteLine($"Error: no usable data found in file '{fileList[i]}'");
                    }
                }
                catch (Exception)
                {
                    if (minDTs.Count > maxDTs.Count)
                    {
                        minDTs.RemoveAt(minDTs.Count - 1);
                    }

                    Console.Error.WriteLine($"Error while reading file '{fileList[i]}'\n");
                }
            }

            if (tbFilesInFolder.Items.Count > 1)
            {
                lbiCheckBox.IsEnabled = true;
                cbSelectAll.IsEnabled = true;
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

                tbEventStartTime.TextChanged -= TbTime_TextChanged;
                tbEventEndTime.TextChanged -= TbTime_TextChanged;
                tbEventStartTime.SelectionChanged -= TbTime_SelectionChanged;
                tbEventEndTime.SelectionChanged -= TbTime_SelectionChanged;

                tbEventStartTime.Text = $"{minDate.ToString("HH':'mm':'ss")}";
                if (isModified[tbEventStartTime] != false)
                {
                    undoHistory[tbEventStartTime].Push(prevText[tbEventStartTime] == "HH:mm:ss" ? "  :  :  " : prevText[tbEventStartTime]);

                }
                prevText[tbEventStartTime] = tbEventStartTime.Text;

                tbEventEndTime.Text = $"{maxDate.ToString("HH':'mm':'ss")}";
                if (isModified[tbEventEndTime] != false)
                {
                    undoHistory[tbEventEndTime].Push(prevText[tbEventEndTime] == "HH:mm:ss" ? "  :  :  " : prevText[tbEventEndTime]);
                }
                prevText[tbEventEndTime] = tbEventEndTime.Text;

                tbEventStartTime.SelectionChanged += TbTime_SelectionChanged;
                tbEventEndTime.SelectionChanged += TbTime_SelectionChanged;
                tbEventStartTime.TextChanged += TbTime_TextChanged;
                tbEventEndTime.TextChanged += TbTime_TextChanged;
                isModified[tbEventStartTime] = false;
                isModified[tbEventEndTime] = false;

                lblEventStartTimeError.ClearValue(ContentProperty);
                lblEventEndTimeError.ClearValue(ContentProperty);
            }
            else
            {
                dpEventStartDate.SelectedDate = null;
                dpEventEndDate.SelectedDate = null;

                tbEventStartTime.TextChanged -= TbTime_TextChanged;
                tbEventEndTime.TextChanged -= TbTime_TextChanged;
                tbEventStartTime.SelectionChanged -= TbTime_SelectionChanged;
                tbEventEndTime.SelectionChanged -= TbTime_SelectionChanged;
                tbEventStartTime.Text = timePlacehoder;
                tbEventEndTime.Text = timePlacehoder;
                tbEventStartTime.TextChanged += TbTime_TextChanged;
                tbEventEndTime.TextChanged += TbTime_TextChanged;
                tbEventStartTime.SelectionChanged += TbTime_SelectionChanged;
                tbEventEndTime.SelectionChanged += TbTime_SelectionChanged;
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
        private void DrawSelectionCaret(TextBox tbTime, bool trailing)
        {
            Rect charRect = tbTime.GetRectFromCharacterIndex(trailing == true ? tbTime.CaretIndex + tbTime.SelectionLength - 1 : tbTime.CaretIndex, trailing == true ? true : false);

            Canvas.SetLeft(selectionCaret[tbTime], charRect.Location.X);
            Canvas.SetTop(selectionCaret[tbTime], charRect.Location.Y);
        }

        private void TbTime_SelectionChanged(object sender, RoutedEventArgs e)
        {
            TextBox tbTime = e.Source as TextBox;

            if (tbTime.IsFocused == false)
            {
                tbTime.RaiseEvent(new RoutedEventArgs { RoutedEvent = TextBox.LostFocusEvent });
                return;
            }

            tbTime.SelectionChanged -= TbTime_SelectionChanged;
            double mouseX = Mouse.GetPosition(tbTime).X;
            int moveCaret = 0;
            bool trailing;
            int movingEdge;
            Action drawCaret;

            if (tbTime.SelectionLength == 0 && selectedChar[tbTime] != -1)
            {
                selectedChar[tbTime] = -1;
                selectionCaret[tbTime].Visibility = Visibility.Collapsed;
                caret[tbTime].Visibility = Visibility.Visible;
            }

            if (tbTime.SelectionLength > 0)
            {
                if (selectedChar[tbTime] == -1)
                {
                    if (prevCaretIndex[tbTime] <= tbTime.CaretIndex)
                    {
                        trailing = true;
                        selectedChar[tbTime] = tbTime.CaretIndex;
                    }
                    else // if (prevCaretIndex[tbTime] > tbTime.CaretIndex)
                    {
                        trailing = false;
                        selectedChar[tbTime] = tbTime.CaretIndex + tbTime.SelectionLength - 1;
                    }

                    //movingEdge = tbTime.CaretIndex < selectedChar[tbTime] ? tbTime.CaretIndex : tbTime.CaretIndex + tbTime.SelectionLength - 1;
                    selectionCaret[tbTime].Visibility = Visibility.Visible;
                    caret[tbTime].Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (selectedChar[tbTime] != tbTime.CaretIndex && selectedChar[tbTime] != tbTime.CaretIndex + tbTime.SelectionLength - 1)
                    {
                        if (tbTime.CaretIndex == selectedChar[tbTime] + 1)
                        {
                            selectedChar[tbTime] = tbTime.CaretIndex;
                            //movingEdge = tbTime.CaretIndex + tbTime.SelectionLength - 1;
                            trailing = true;
                        }
                        else
                        {
                            selectedChar[tbTime] = tbTime.CaretIndex + tbTime.SelectionLength - 1;
                            //movingEdge = tbTime.CaretIndex;
                            trailing = false;
                        }
                    }
                    else
                    {
                        //movingEdge = tbTime.CaretIndex < selectedChar[tbTime] ? tbTime.CaretIndex : tbTime.CaretIndex + tbTime.SelectionLength - 1;
                        int carets = tbTime.CaretIndex < selectedChar[tbTime] ? tbTime.CaretIndex : tbTime.CaretIndex + tbTime.SelectionLength - 1;

                        if (carets > selectedChar[tbTime])
                        {
                            trailing = true;
                        }
                        else if (carets < selectedChar[tbTime])
                        {
                            trailing = false;
                        }
                        else if (prevCaretIndex[tbTime] > selectedChar[tbTime])
                        {
                            trailing = true;
                        }
                        else // if (prevCaretIndex[tbTime] < selectedChar[tbTime])
                        {
                            trailing = false;
                        }
                    }
                }

                if (tbTime.Text[selectedChar[tbTime]] == ':')
                {
                    int newSelectionLength = tbTime.SelectionLength == 1 ? 1 : tbTime.SelectionLength - 1;

                    if (trailing == true)
                    {
                        tbTime.CaretIndex = selectedChar[tbTime] + 1;
                    }
                    else if (trailing == false)
                    {
                        tbTime.CaretIndex = selectedChar[tbTime];
                    }

                    for (int i = 0; i < newSelectionLength; i++)
                    {
                        if (trailing == true)
                        {
                            EditingCommands.SelectRightByCharacter.Execute(null, tbTime);
                        }
                        else if (trailing == false)
                        {
                            EditingCommands.SelectLeftByCharacter.Execute(null, tbTime);
                        }
                    }

                    if (trailing == true)
                    {
                        selectedChar[tbTime] = tbTime.CaretIndex;
                    }
                    else if (trailing == false)
                    {
                        selectedChar[tbTime] = tbTime.CaretIndex + newSelectionLength - 1;
                    }
                }

                movingEdge = trailing == true ? tbTime.CaretIndex + tbTime.SelectionLength - 1 : tbTime.CaretIndex;

                drawCaret = () =>
                {
                    if (moveCaret != 0)
                    {
                        if (moveCaret == -1)
                        {
                            EditingCommands.SelectLeftByCharacter.Execute(null, tbTime);
                        }
                        else if (moveCaret == 1)
                        {
                            EditingCommands.SelectRightByCharacter.Execute(null, tbTime);
                        }

                        movingEdge = tbTime.CaretIndex < selectedChar[tbTime] ? tbTime.CaretIndex : tbTime.CaretIndex + tbTime.SelectionLength - 1;
                    }

                    DrawSelectionCaret(tbTime, trailing);
                };
            }
            else
            {
                movingEdge = tbTime.CaretIndex;

                drawCaret = () =>
                {
                    if (moveCaret != 0)
                    {
                        if (moveCaret == -1)
                        {
                            tbTime.CaretIndex--;
                        }
                        else if (moveCaret == 1)
                        {
                            tbTime.CaretIndex++;
                        }

                        movingEdge = tbTime.CaretIndex;
                    }

                    if (selectedChar[tbTime] != -1)
                    {
                        selectedChar[tbTime] = -1;
                        selectionCaret[tbTime].Visibility = Visibility.Collapsed;
                        caret[tbTime].Visibility = Visibility.Visible;
                    }
                    else if (selectedChar[tbTime] == -1 && prevCaretIndex[tbTime] == 8)
                    {
                        selectionCaret[tbTime].Visibility = Visibility.Collapsed;
                        caret[tbTime].Visibility = Visibility.Visible;
                    }

                    if (tbTime.CaretIndex == 8)
                    {
                        selectionCaret[tbTime].Visibility = Visibility.Visible;
                        caret[tbTime].Visibility = Visibility.Collapsed;
                        DrawSelectionCaret(tbTime, false);
                    }
                    else
                    {
                        Rect charRect = tbTime.GetRectFromCharacterIndex(tbTime.CaretIndex, false);
                        Point rightSide = tbTime.GetRectFromCharacterIndex(tbTime.CaretIndex, true).Location;

                        caret[tbTime].Width = rightSide.X - charRect.Location.X;
                        caret[tbTime].Height = charRect.Height;

                        Canvas.SetLeft(caret[tbTime], charRect.Location.X);
                        Canvas.SetTop(caret[tbTime], charRect.Location.Y);
                    }
                };
            }

            if (movingEdge != tbTime.Text.Length)
            {
                if (tbTime.Text[movingEdge] == ':')
                {
                    if (Mouse.Captured == tbTime && Mouse.LeftButton == MouseButtonState.Pressed)
                    {
                        if (prevMouseX < mouseX)
                        {
                            moveCaret = 1;
                        }
                        else if (prevMouseX > mouseX)
                        {
                            moveCaret = -1;
                        }
                        else
                        {
                            moveCaret = 1;
                        }
                    }
                    else
                    {
                        if (prevCaretIndex[tbTime] < movingEdge)
                        {
                            moveCaret = 1;
                        }
                        else if (prevCaretIndex[tbTime] > movingEdge)
                        {
                            moveCaret = -1;
                        }
                        else
                        {
                            moveCaret = 1;
                        }
                    }
                }
            }

            drawCaret();
            prevCaretIndex[tbTime] = movingEdge;
            tbTime.SelectionChanged += TbTime_SelectionChanged;
        }
        private void TbTime_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            TextBox tbTime = sender as TextBox;

            if (mouseLeftButtonSelect == true)
            {
                int charIndex;
                double charBaseline = tbTime.GetRectFromCharacterIndex(2).Y;
                double minMouseX = tbTime.GetRectFromCharacterIndex(0, false).X;
                double maxMouseX = tbTime.GetRectFromCharacterIndex(7, true).X;
                Point mousePoint = new Point(e.GetPosition(tbTime).X, charBaseline);

                if (mousePoint.X < minMouseX)
                {
                    charIndex = 0;
                }
                else if (mousePoint.X > maxMouseX)
                {
                    charIndex = 7;
                }
                else
                {
                    charIndex = tbTime.GetCharacterIndexFromPoint(mousePoint, true);
                }

                bool trailing;
                Action<object, IInputElement> selectCharacter;
                Action<object, IInputElement> selectWord;
                int tempSelectionLength = 2 + ((selectedCount[tbTime] - 1) * 3);

                if (charIndex < selectedChar[tbTime])
                {
                    if (tbTime.Text[selectedChar[tbTime] - 1] == ':')
                    {
                        selectedChar[tbTime] = tbTime.CaretIndex + tbTime.SelectionLength - 1;
                    }

                    selectCharacter = EditingCommands.SelectLeftByCharacter.Execute;
                    selectWord = EditingCommands.SelectLeftByWord.Execute;
                    trailing = false;
                    tbTime.CaretIndex = selectedChar[tbTime] + 1;
                }
                else if (charIndex > selectedChar[tbTime])
                {
                    if (tbTime.Text[selectedChar[tbTime] + 1] == ':')
                    {
                        selectedChar[tbTime] = tbTime.CaretIndex;
                    }

                    selectCharacter = EditingCommands.SelectRightByCharacter.Execute;
                    selectWord = EditingCommands.SelectRightByWord.Execute;
                    trailing = true;
                    tbTime.CaretIndex = selectedChar[tbTime];
                }
                else
                {
                    tbTime.CaretIndex = charIndex == tbTime.Text.Length - 1 ? tbTime.CaretIndex : tbTime.CaretIndex + tempSelectionLength;

                    if (charIndex == tbTime.Text.Length - 1)
                    {
                        selectCharacter = EditingCommands.SelectRightByCharacter.Execute;
                        selectWord = EditingCommands.SelectRightByWord.Execute;
                    }
                    else
                    {
                        selectCharacter = EditingCommands.SelectLeftByCharacter.Execute;
                        selectWord = EditingCommands.SelectLeftByWord.Execute;
                    }

                    trailing = (charIndex == tbTime.Text.Length - 1 ? true : false);
                }

                string newString = charIndex < selectedChar[tbTime] || selectedChar[tbTime] == 7 ? new string(tbTime.Text.Reverse().ToArray()) : tbTime.Text;
                int newCharIndex = charIndex < selectedChar[tbTime] || selectedChar[tbTime] == 7 ? tbTime.Text.Length - 1 - charIndex : charIndex;
                int newSelectedChar = charIndex < selectedChar[tbTime] || selectedChar[tbTime] == 7 ? tbTime.Text.Length - 1 - selectedChar[tbTime] : selectedChar[tbTime];
                int selectionCount = newString.Substring(newSelectedChar + tempSelectionLength - 1, newCharIndex - (newSelectedChar + tempSelectionLength - 1) < 0 ? 0 :
                                    newCharIndex - (newSelectedChar + tempSelectionLength - 1)).Count(c => c == ':');
                selectWord(null, tbTime);

                for (int i = 0; i < (selectedCount[tbTime] - 1) + selectionCount; i++)
                {
                    selectCharacter(null, tbTime);
                    selectWord(null, tbTime);
                }

                DrawSelectionCaret(tbTime, trailing);
                e.Handled = true;
                prevMouseX = e.GetPosition(tbTime).X;
            }
        }
        private void TbTime_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            TextBox tbTime = sender as TextBox;
            tbTime.SelectionChanged -= TbTime_SelectionChanged;

            tbTime.CaptureMouse();
            mouseLeftButtonSelect = true;
            double charBaseline = tbTime.GetRectFromCharacterIndex(2).Y;
            double minMouseX = tbTime.GetRectFromCharacterIndex(0, false).X;
            double maxMouseX = tbTime.GetRectFromCharacterIndex(7, true).X;
            Point mousePoint = new Point(e.GetPosition(tbTime).X, charBaseline);
            int charIndex = tbTime.GetCharacterIndexFromPoint(mousePoint, false);

            if (mousePoint.X < minMouseX)
            {
                charIndex = 0;
            }
            else if (mousePoint.X > maxMouseX)
            {
                charIndex = 7;
            }
            else
            {
                charIndex = tbTime.GetCharacterIndexFromPoint(mousePoint, true);
            }

            caret[tbTime].Visibility = Visibility.Collapsed;
            selectionCaret[tbTime].Visibility = Visibility.Visible;

            bool trailing;

            if (charIndex == tbTime.Text.Length - 1 || tbTime.Text[charIndex + 1] == ':')
            {
                trailing = true;
                selectedCount[tbTime] = 1;
                selectedChar[tbTime] = charIndex - 1;
                tbTime.CaretIndex = selectedChar[tbTime];
                EditingCommands.SelectRightByWord.Execute(null, tbTime);
            }
            else if (tbTime.Text[charIndex] == ':')
            {
                selectedCount[tbTime] = 2;

                if (charIndex < tbTime.Text.Length / 2)
                {
                    tbTime.CaretIndex = 0;
                    selectedChar[tbTime] = 0;
                    EditingCommands.SelectRightByWord.Execute(null, tbTime);
                    EditingCommands.SelectRightByCharacter.Execute(null, tbTime);
                    EditingCommands.SelectRightByWord.Execute(null, tbTime);
                    trailing = true;
                }
                else
                {
                    tbTime.CaretIndex = tbTime.Text.Length;
                    selectedChar[tbTime] = tbTime.Text.Length - 1;
                    EditingCommands.SelectLeftByWord.Execute(null, tbTime);
                    EditingCommands.SelectLeftByCharacter.Execute(null, tbTime);
                    EditingCommands.SelectLeftByWord.Execute(null, tbTime);
                    trailing = false;
                }
            }
            else
            {
                trailing = false;
                selectedCount[tbTime] = 1;
                selectedChar[tbTime] = charIndex + 1;
                tbTime.CaretIndex = selectedChar[tbTime] + 1;
                EditingCommands.SelectLeftByWord.Execute(null, tbTime);
            }

            DrawSelectionCaret(tbTime, trailing);
            e.Handled = true;
        }
        private void TbTime_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            TextBox tbTime = sender as TextBox;

            if (mouseLeftButtonSelect)
            {
                mouseLeftButtonSelect = false;
                e.MouseDevice.Capture(tbTime, CaptureMode.None);                
                e.Handled = true;

                tbTime.SelectionChanged += TbTime_SelectionChanged;
            }
        }
        private void TbTime_Loaded(object sender, RoutedEventArgs e)
        {
            TextBox tbTime = sender as TextBox;

            var redirectCopyCutPaste = new Action<TextBox>((element) =>
            {
                CommandManager.AddPreviewCanExecuteHandler(element,
                    new CanExecuteRoutedEventHandler((tbSender, eventArgs) =>
                    {
                        if (eventArgs.Command == ApplicationCommands.Paste || eventArgs.Command == ApplicationCommands.Copy || eventArgs.Command == ApplicationCommands.Cut)
                        {
                            eventArgs.CanExecute = true;
                        }
                    }));

                CommandManager.AddPreviewExecutedHandler(element,
                new ExecutedRoutedEventHandler((tbSender, eventArgs) =>
                {
                    if (eventArgs.Command == ApplicationCommands.Paste)
                    {
                        this.Paste(tbSender);

                        eventArgs.Handled = true;
                    }
                    else if (eventArgs.Command == ApplicationCommands.Copy)
                    {
                        this.Copy(tbSender);

                        eventArgs.Handled = true;
                    }
                    else if (eventArgs.Command == ApplicationCommands.Cut)
                    {
                        this.Cut(tbSender);

                        eventArgs.Handled = true;
                    }
                }));
            });

            var redirectUndoRedo = new Action<UIElement>((element) =>
            {
                CommandManager.AddPreviewCanExecuteHandler(element,
                    new CanExecuteRoutedEventHandler((tbSender, eventArgs) =>
                    {
                        if (eventArgs.Command == ApplicationCommands.Undo || eventArgs.Command == ApplicationCommands.Redo)
                        {
                            eventArgs.CanExecute = true;
                        }
                    }));

                CommandManager.AddPreviewExecutedHandler(element,
                    new ExecutedRoutedEventHandler((tbSender, eventArgs) =>
                    {
                        if (eventArgs.Command == ApplicationCommands.Undo || eventArgs.Command == ApplicationCommands.Redo)
                        {
                            eventArgs.Handled = true;
                        }

                        if (eventArgs.Command == ApplicationCommands.Undo)
                        {
                            if (this.canUndo(tbSender))
                            {
                                this.undo(tbSender);
                            }

                            eventArgs.Handled = true;

                        }
                        else if (eventArgs.Command == ApplicationCommands.Redo)
                        {
                            if (this.canRedo(tbSender))
                            {
                                this.redo(tbSender);
                            }
                        }
                    }));
            });

            Rect charRect;
            Point charRightSide;

            prevCaretIndex.Add(tbTime, -1);
            prevText.Add(tbTime, tbTime.Text);
            selectedChar.Add(tbTime, -1);
            selectedCount.Add(tbTime, -1);
            EditingCommands.ToggleInsert.Execute(null, tbTime);            

            undoHistory[tbTime] = new Stack<string>();
            redoHistory[tbTime] = new Stack<string>();
            isModified[tbTime] = true;
            redirectUndoRedo(tbTime);
            redirectCopyCutPaste(tbTime);

            caret.Add(tbTime, VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(VisualTreeHelper.GetParent(tbTime), 1), 0) as Border);
            selectionCaret.Add(tbTime, VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(VisualTreeHelper.GetParent(tbTime), 1), 1) as Border);
            charRect = tbTime.GetRectFromCharacterIndex(2);
            charRightSide = tbTime.GetRectFromCharacterIndex(2, true).Location;
            selectionCaret[tbTime].Width = (charRightSide.X - charRect.Location.X) * 0.20;
            selectionCaret[tbTime].Height = charRect.Height;

            caret[tbTime].BeginStoryboard((Storyboard)this.FindResource("CaretStoryboard"));
            selectionCaret[tbTime].BeginStoryboard((Storyboard)this.FindResource("SelectionCaretStoryboard"));
            underscoreChars[tbTime] = new Dictionary<int, Border>();
            for (int i = 0; i < tbTime.Text.Length; i++)
            {
                if (tbTime.Text[i] != ':')
                {
                    Border underscoreChar = new Border
                    {
                        Visibility = Visibility.Collapsed,
                        IsHitTestVisible = false,
                        Opacity = 1.0,
                        Background = new SolidColorBrush(Colors.Red)
                    };

                    (VisualTreeHelper.GetChild(VisualTreeHelper.GetParent(tbTime), 1) as Canvas).Children.Add(underscoreChar);

                    underscoreChar.Width = tbTime.GetRectFromCharacterIndex(i, true).Right - tbTime.GetRectFromCharacterIndex(i, false).Left;
                    underscoreChar.Height = 1.0;

                    Canvas.SetLeft(underscoreChar, tbTime.GetRectFromCharacterIndex(i).Location.X);
                    Canvas.SetTop(underscoreChar, tbTime.GetRectFromCharacterIndex(i).Bottom);
                    underscoreChar.BeginStoryboard((Storyboard)this.FindResource("SelectionCaretStoryboard"));
                    (underscoreChars[tbTime])[i] = underscoreChar;
                }
            }
        }
        //
        private string  getGpxFileName()
        {
            string fullFileName;
            if(Regex.IsMatch(tbEventName.Text, ".gpx$") || Regex.IsMatch(tbEventName.Text, ".GPX$"))
                fullFileName = tbSavePath.Text + @"/" + tbEventName.Text;
            else
                fullFileName = tbSavePath.Text + @"/" + tbEventName.Text + ".gpx";

            return fullFileName;
        }
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            IInputElement capturedElement = Mouse.Captured;

            if (capturedElement != null && capturedElement is TextBox)
            {
                prevMouseX = Mouse.GetPosition(capturedElement).X;
            }
        }
    }
}
