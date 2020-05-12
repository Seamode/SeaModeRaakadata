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

namespace Raakadata
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
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
            // jotta tiedostot on helppo valita testausta varten.
            dpEventStartDate.DisplayDate = new DateTime(2019, 09, 28);
            dpEventEndDate.DisplayDate = new DateTime(2019, 09, 28);
        }

        private string FindDatDirectory()
        {
            if (Directory.Exists(ConfigurationManager.AppSettings["fileDirectory"]))
                return ConfigurationManager.AppSettings["fileDirectory"];
            string altPath = System.IO.Path.GetDirectoryName(
                System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            // ylempi pitäisi palauttaa prg, alla se vaihdetaan dat
            altPath = $"{altPath.Substring(0, altPath.LastIndexOf(System.IO.Path.DirectorySeparatorChar) + 1)}dat";
            if (Directory.Exists(altPath))
                return altPath;
            return string.Empty;
        }

        private void ListFilesInFolder() =>
            tbFilesInFolder.Text = string.Join("\r\n", SeamodeReader.FetchFilesToList(tbFolderPath.Text));

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
            BtnCreatePgxFile.IsEnabled = false;
            BtnCreateEventFile.IsEnabled = false;
            if (!ValidateParameters(out TimeSpan startTime, out TimeSpan endTime))
            {
                BtnCreatePgxFile.IsEnabled = true;
                BtnCreateEventFile.IsEnabled = true;
                return;
            }
            // kisan alku ja loppu datetimen muodostus.
            // ei pitäisi päästä tähän, jos on virheellisesti syötetty.
            if (!ValidTimePeriod(startTime, endTime, out DateTime eventStart, out DateTime eventEnd))
            {
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
            await sr.ReadAndWriteFilesAsync(tbFolderPath.Text);
            // Kursorin palautus.
            Cursor = tempCursor;
            ForceCursor = false;
            // muuten tulee tyhjä tiedosto
            if (sr.DataRowCount == 0)
            {
                MessageBox.Show("No data found for specified time period.");
                BtnCreatePgxFile.IsEnabled = true;
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
            BtnCreatePgxFile.IsEnabled = true;
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
                MessageBox.Show("Check the dates and times.\nRace start must be before the ending.");
                return false;
            }
            return true;
        }

        private bool ValidateParameters(out TimeSpan startTime, out TimeSpan endTime)
        {
            bool valid = true;
            if (dpEventStartDate.SelectedDate == null || dpEventEndDate.SelectedDate == null)
            {
                MessageBox.Show("Please select start and end dates for the race.");
                valid = false;
            }
            if (!TimeSpan.TryParse(tbEventStartTime.Text, out startTime))
            {
                MessageBox.Show("Please enter start and end times for the race.");
                valid = false;
            }
            if (!TimeSpan.TryParse(tbEventEndTime.Text, out endTime))
            {
                MessageBox.Show("Please enter start and end times for the race.");
                valid = false;
            }
            if (string.IsNullOrEmpty(tbEventName.Text))
            {
                MessageBox.Show("Please enter a name for the race.");
                valid = false;
            }
            if (string.IsNullOrEmpty(tbEventFilePath.Text) || string.IsNullOrEmpty(tbSavePath.Text))
            {
                MessageBox.Show("Please enter a folder for the files");
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
        }

        private void TbEventFilePath_TextChanged(object sender, TextChangedEventArgs e) 
            => UpdateEventFilePath();

        private void UpdateEventFilePath()
        {
            string polku = tbSavePath == null ? "<Path>" : tbSavePath.Text;
            string pvm = dpEventStartDate.SelectedDate == null ? "<Date>" : $"{dpEventStartDate.SelectedDate:yyyyMMdd}";
            string aika = tbEventStartTime.Text == timePlacehoder ? "<Time>" : string.Join("", tbEventStartTime.Text.Split(':', '.'));
            string nimi = string.IsNullOrEmpty(tbEventName.Text) ? "<RaceName>" : tbEventName.Text;
            tbEventFilePath.Text = $"{polku}\\SeaMODE_{pvm}_{aika}_{nimi}.csv";
        }

        private void TbTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = e.Source as TextBox;
            if (tb.Text == timePlacehoder || string.IsNullOrEmpty(tb.Text))
            {
                if (tb == tbEventStartTime)
                    startTimeStringLocation = 0;
                else if (tb == tbEventEndTime)
                    endTimeStringLocation = 0;
                return;
            }
            // backspace/deleteä varten
            if (tb == tbEventStartTime && tb.Text.Length < startTimeStringLocation)
            {
                tb.Text.Remove(tb.Text.Length - 1);
                startTimeStringLocation = tb.Text.Length;
                tb.SelectionStart = tb.Text.Length;
                return;
            }
            else if (tb == tbEventEndTime && tb.Text.Length < endTimeStringLocation)
            {
                tb.Text.Remove(tb.Text.Length - 1);
                endTimeStringLocation = tb.Text.Length;
                tb.SelectionStart = tb.Text.Length;
                return;
            }
            // syötetyn 'ei-luvun' poisto ja ':'-poikkeus
            if (tb.Text.Length != 3 && tb.Text.Length != 6 && !char.IsDigit(tb.Text, tb.Text.Length - 1))
                tb.Text = tb.Text.Substring(0, tb.Text.Length - 1);
            if (tb.Text.Length == 3 || tb.Text.Length == 6 && !tb.Text.EndsWith(":"))
                tb.Text = $"{tb.Text.Substring(0, tb.Text.Length - 1)}:";
            // syötetty aika oikeaan muotoon
            switch (tb.Text.Length)
            {
                case 1:
                    if (int.TryParse(tb.Text, out int h) && h > 2)
                        tb.Text = $"0{h}:";
                    break;
                case 2:
                    if (int.TryParse(tb.Text, out int hh) && hh < 24)
                        tb.Text += ":";
                    else
                        tb.Text = "23:";
                    break;
                case 4:
                    if (int.TryParse(tb.Text.Substring(3), out int m) && m > 5)
                        tb.Text = $"{tb.Text.Substring(0, 3)}5";
                    break;
                case 5:
                    tb.Text += ":";
                    break;
                case 7:
                    if (int.TryParse(tb.Text.Substring(6), out int s) && s > 5)
                        tb.Text = $"{tb.Text.Substring(0, 6)}5";
                    break;
                default:
                    break;
            }
            tb.SelectionStart = tb.Text.Length;
            // tarvitaan backspace/deleteä varten.
            if (tb == tbEventStartTime)
                startTimeStringLocation = tb.Text.Length;
            else if (tb == tbEventEndTime)
                endTimeStringLocation = tb.Text.Length;
        }

        private void TbTime_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = e.Source as TextBox;
            if (tb.Text == timePlacehoder)
                tb.Clear();
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
            TextBox tb = e.Source as TextBox;
            // tyhjä kenttä täytetään placeholderilla
            if (string.IsNullOrEmpty(tb.Text))
            {
                tb.Text = timePlacehoder;
                return;
            }
            // täyttää ajan perään nollia, jos mahtuu
            string fill = "00:00:00";
            tb.Text += fill.Substring(tb.Text.Length);
            // jos aika on väärässä muodossa, se tyhjennetään ja
            // pyydetään käyttäjää lattamaan uusi
            if (!Regex.IsMatch(tb.Text, "^((0[0-9])|(1[0-9])|(2[0-3])):([0-5][0-9]):([0-5][0-9])$"))
            {
                tb.Text = timePlacehoder;
                if (tb == tbEventStartTime)
                    MessageBox.Show("Please re-enter a start time again.");
                else if (tb == tbEventEndTime)
                    MessageBox.Show("Please re-enter an end time again.");
            }
            if (tb == tbEventStartTime)
                UpdateEventFilePath();
        }

        private async void BtnCreatePgxFile_Click(object sender, RoutedEventArgs e)
        {
            // jotta ei tapahdu tupla klikkausta.
            BtnCreatePgxFile.IsEnabled = false;
            BtnCreateEventFile.IsEnabled = false;
            if (!ValidateParameters(out TimeSpan startTime, out TimeSpan endTime))
            {
                BtnCreatePgxFile.IsEnabled = true;
                BtnCreateEventFile.IsEnabled = true;
                return;
            }
            // kisan alku ja loppu datetimen muodostus.
            // ei pitäisi päästä tähän, jos on virheellisesti syötetty.
            if (!ValidTimePeriod(startTime, endTime, out DateTime eventStart, out DateTime eventEnd))
            {
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

            foreach (string tiedosto in sr.FetchFilesToRead(tbFolderPath.Text))
            {
                await Task.Run(() => sr.haeGpxData(tiedosto));
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
            ResetUI();
            ListFilesInFolder();
        }

        private void DpEventStartDate_LostFocus(object sender, RoutedEventArgs e)
        {
            if (dpEventStartDate.SelectedDate != null)
                dpEventEndDate.DisplayDate = (DateTime)dpEventStartDate.SelectedDate;
        }
    }
}
