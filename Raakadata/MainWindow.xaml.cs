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
        public MainWindow()
        {
            InitializeComponent();
            tbFolderPath.Text = ConfigurationManager.AppSettings["fileDirectory"];
            tbSavePath.Text = ConfigurationManager.AppSettings["fileDirectory"];
            ListFilesInFolder();
            // jotta tiedostot on helppo valita testausta varten.
            dpEventStartDate.DisplayDate = new DateTime(2019, 09, 28);
            dpEventEndDate.DisplayDate = new DateTime(2019, 09, 28);
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
            TextBox tb = e.Source as TextBox;
            if (tb.Text == "HH:mm:ss" || string.IsNullOrEmpty(tb.Text))
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
            if (tb.Text == "HH:mm:ss")
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
                tb.Text = "HH:mm:ss";
                return;
            }
            // täyttää ajan perään nollia, jos mahtuu
            string fill = "00:00:00";
            tb.Text += fill.Substring(tb.Text.Length);
            // jos aika on väärässä muodossa, se tyhjennetään ja
            // pyydetään käyttäjää lattamaan uusi
            if (!Regex.IsMatch(tb.Text, "^((0[0-9])|(1[0-9])|(2[0-3])):([0-5][0-9]):([0-5][0-9])$"))
            {
                tb.Text = "HH:mm:ss";
                if (tb == tbEventStartTime)
                    MessageBox.Show("Please re-enter a start time again.");
                else if (tb == tbEventEndTime)
                    MessageBox.Show("Please re-enter an end time again.");
            }
            if (tb == tbEventStartTime)
                UpdateEventFilePath();
        }
    }
}
