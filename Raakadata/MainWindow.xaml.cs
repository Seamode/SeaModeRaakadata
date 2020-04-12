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
            dpAlkuPvm.DisplayDate = new DateTime(2019, 09, 28);
            dpLoppuPvm.DisplayDate = new DateTime(2019, 09, 28);
        }

        private void ListaaTiedostot() =>
            tbValitutTiedostot.Text = string.Join("\r\n", SeamodeReader.HaeTiedostotListaan(tbTiedostoPolku.Text));

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

        private void BtnLuoKisaTiedosto_Click(object sender, RoutedEventArgs e)
        {
            if (dpAlkuPvm.SelectedDate == null || dpLoppuPvm.SelectedDate == null)
            {
                MessageBox.Show("Please select start and end dates for the race.");
                return;
            }
            if (!TimeSpan.TryParse(textBoxStart.Text, out TimeSpan alkuAika) ||
                !TimeSpan.TryParse(textBoxEnd.Text, out TimeSpan loppuAika))
            {
                MessageBox.Show("Please enter proper start and end times for the race.");
                return;
            }
            if (string.IsNullOrEmpty(tbKilpailuNimi.Text))
            {
                MessageBox.Show("Please enter a name for the race.");
                return;
            }
            DateTime alku = (DateTime)dpAlkuPvm.SelectedDate;
            alku = alku.Add(alkuAika);
            DateTime loppu = (DateTime)dpLoppuPvm.SelectedDate;
            loppu = loppu.Add(loppuAika);
            if (loppu <= alku)
            {
                MessageBox.Show("Check the dates and times.\nRace start must be before the ending.");
                return;
            }
            BtnLuoKisaTiedosto.IsEnabled = false;
            SeamodeReader sr = new SeamodeReader(alku, loppu);
            foreach (string tiedosto in sr.HaeTiedostot(tbTiedostoPolku.Text))
            {
                sr.LueTiedosto(tiedosto);
            }
            SeamodeWriter sw = new SeamodeWriter() { OutFile = tbKilpaTiedostoPolku.Text };
            sw.Kirjoita(sr.Rivit);
            MessageBox.Show($"File {tbKilpaTiedostoPolku.Text} was created.");
            BtnLuoKisaTiedosto.IsEnabled = true;
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
                return;
            // backspace / delete
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
            // syötetyn 'ei-luvun' hallinta ja ':'-poikkeus
            if (tb.Text.Length != 3 && tb.Text.Length != 6 && !char.IsDigit(tb.Text, tb.Text.Length - 1))
                tb.Text = tb.Text.Substring(0, tb.Text.Length - 1);
            if (tb.Text.Length == 3 || tb.Text.Length == 6 && !char.Equals(tb.Text[tb.Text.Length - 1], ':'))
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
            // tarvitaan backspacea ja deleteä varten.
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

        }

        private void DpAlkuPvm_SelectedDateChanged(object sender, SelectionChangedEventArgs e) 
            => KisaTiedostoPolku();

        private void TbAika_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = e.Source as TextBox;
            if (string.IsNullOrEmpty(tb.Text))
            {
                tb.Text = "HH:mm:ss";
                return;
            }
            string fill = "00:00:00";
            tb.Text += fill.Substring(tb.Text.Length);
            if (tb == textBoxStart)
                KisaTiedostoPolku();
        }
    }
}
