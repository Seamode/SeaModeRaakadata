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

namespace Raakadata
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int alkuAikaPituus;
        private int loppuAikaPituus;
        public MainWindow()
        {
            InitializeComponent();
            tbTiedostoPolku.Text = ConfigurationManager.AppSettings["fileDirectory"];
            tbTallennusPolku.Text = ConfigurationManager.AppSettings["fileDirectory"];
            ListaaTiedostot();
            // jotta tiedostot on helppo valita testausta varten.
            dpAlkuPvm.DisplayDate = new DateTime(2019, 09, 28);
            dpLoppuPvm.DisplayDate = new DateTime(2019, 09, 28);
        }

        private void ListaaTiedostot() =>
            tbValitutTiedostot.Text = string.Join("\r\n", SeamodeReader.FetchFilesToList(tbTiedostoPolku.Text));

        private void BtnHaeTiedostoPolku_Click(object sender, RoutedEventArgs e)
        {
            // https://stackoverflow.com/questions/1922204/open-directory-dialog
            // Ookii.Diologs.Wpf
            var fd = new VistaFolderBrowserDialog();
            if (fd.ShowDialog(this).GetValueOrDefault())
            {
                tbTiedostoPolku.Text = fd.SelectedPath;
                ListaaTiedostot();
            }
        }

        private void BtnHaeTallennusPolku_Click(object sender, RoutedEventArgs e)
        {
            var fd = new VistaFolderBrowserDialog();
            if (fd.ShowDialog(this).GetValueOrDefault())
                tbTallennusPolku.Text = fd.SelectedPath;
        }

        private async void BtnLuoKisaTiedosto_Click(object sender, RoutedEventArgs e)
        {
            // jotta ei tapahdu tupla klikkausta.
            BtnLuoKisaTiedosto.IsEnabled = false;
            if (dpAlkuPvm.SelectedDate == null || dpLoppuPvm.SelectedDate == null)
            {
                MessageBox.Show("Please select start and end dates for the race.");
                BtnLuoKisaTiedosto.IsEnabled = true;
                return;
            }
            if (!TimeSpan.TryParse(textBoxStart.Text, out TimeSpan alkuAika) ||
                !TimeSpan.TryParse(textBoxEnd.Text, out TimeSpan loppuAika))
            {
                MessageBox.Show("Please enter start and end times for the race.");
                BtnLuoKisaTiedosto.IsEnabled = true;
                return;
            }
            if (string.IsNullOrEmpty(tbKilpailuNimi.Text))
            {
                MessageBox.Show("Please enter a name for the race.");
                BtnLuoKisaTiedosto.IsEnabled = true;
                return;
            }
            // kisan alku ja loppu datetimen muodostus.
            // ei pitäisi päästä tähän, jos on virheellisesti syötetty.
            DateTime alku = (DateTime)dpAlkuPvm.SelectedDate;
            alku = alku.Add(alkuAika);
            DateTime loppu = (DateTime)dpLoppuPvm.SelectedDate;
            loppu = loppu.Add(loppuAika);
            if (loppu <= alku)
            {
                MessageBox.Show("Check the dates and times.\nRace start must be before the ending.");
                BtnLuoKisaTiedosto.IsEnabled = true;
                return;
            }
            // tiedostojen luku
            SeamodeReader sr = new SeamodeReader(alku, loppu);
            await sr.ReadFilesAsync(tbTiedostoPolku.Text);
            // muuten tulee tyhjä tiedosto
            if (sr.DataRowCount == 0)
            {
                MessageBox.Show("No data found for specified time period.");
                BtnLuoKisaTiedosto.IsEnabled = true;
                return;
            }
            // kisatiedoston luonti
            File.Move(sr.TmpFile, tbKilpaTiedostoPolku.Text);
            MessageBox.Show($"File {tbKilpaTiedostoPolku.Text} was created.");
            if (sr.DataRowErrors.Count > 0)
            {
                MessageBox.Show($"{string.Join("\n", sr.DataRowErrors)}");
            }
            BtnLuoKisaTiedosto.IsEnabled = true;
            // syötetyt arvot tyhjennetään
            dpAlkuPvm.ClearValue(DatePicker.SelectedDateProperty);
            dpLoppuPvm.ClearValue(DatePicker.SelectedDateProperty);
            textBoxStart.Text = "HH:mm:ss";
            textBoxEnd.Text = "HH:mm:ss";
            tbKilpailuNimi.Clear();
            ListaaTiedostot();
        }

        private void TbKisaTiedostoPolku_TextChanged(object sender, TextChangedEventArgs e) 
            => KisaTiedostoPolku();

        private void KisaTiedostoPolku()
        {
            string polku = tbTallennusPolku == null ? "<Path>" : tbTallennusPolku.Text;
            string pvm = dpAlkuPvm.SelectedDate == null ? "<Date>" : $"{dpAlkuPvm.SelectedDate:yyyyMMdd}";
            string aika = textBoxStart.Text == "HH:mm:ss" ? "<Time>" : string.Join("", textBoxStart.Text.Split(':', '.'));
            string nimi = string.IsNullOrEmpty(tbKilpailuNimi.Text) ? "<RaceName>" : tbKilpailuNimi.Text;
            tbKilpaTiedostoPolku.Text = $"{polku}\\SeaMODE_{pvm}_{aika}_{nimi}.csv";
        }

        private void TbAika_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = e.Source as TextBox;
            if (tb.Text == "HH:mm:ss" || string.IsNullOrEmpty(tb.Text))
            {
                if (tb == textBoxStart)
                    alkuAikaPituus = 0;
                else if (tb == textBoxEnd)
                    loppuAikaPituus = 0;
                return;
            }
            // backspace/deleteä varten
            if (tb == textBoxStart && tb.Text.Length < alkuAikaPituus)
            {
                tb.Text.Remove(tb.Text.Length - 1);
                alkuAikaPituus = tb.Text.Length;
                tb.SelectionStart = tb.Text.Length;
                return;
            }
            else if (tb == textBoxEnd && tb.Text.Length < loppuAikaPituus)
            {
                tb.Text.Remove(tb.Text.Length - 1);
                loppuAikaPituus = tb.Text.Length;
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
                    if (int.TryParse(tb.Text, out int t) && t > 2)
                        tb.Text = $"0{t}:";
                    break;
                case 2:
                    if (int.TryParse(tb.Text, out int tt) && tt < 24)
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
            if (tb == textBoxStart)
                alkuAikaPituus = tb.Text.Length;
            else if (tb == textBoxEnd)
                loppuAikaPituus = tb.Text.Length;
        }

        private void TbAika_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = e.Source as TextBox;
            if (tb.Text == "HH:mm:ss")
                tb.Clear();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // tämä oli Artolla testausta varten?
        }

        private void DpAlkuPvm_SelectedDateChanged(object sender, SelectionChangedEventArgs e) 
            => KisaTiedostoPolku();

        private void TbAika_LostFocus(object sender, RoutedEventArgs e)
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
                if (tb == textBoxStart)
                    MessageBox.Show("Please re-enter a start time again.");
                else if (tb == textBoxEnd)
                    MessageBox.Show("Please re-enter an end time again.");
            }
            if (tb == textBoxStart)
                KisaTiedostoPolku();
        }
    }
}
