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
            tbValitutTiedostot.Text = String.Join("\r\n", SeamodeReader.HaeTiedostotListaan(tbTiedostoPolku.Text));

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
            {
                tbTallennusPolku.Text = fd.SelectedPath;
            }
        }

        private void BtnLuoKisaTiedosto_Click(object sender, RoutedEventArgs e)
        {
            if (dpAlkuPvm.SelectedDate == null || dpLoppuPvm.SelectedDate == null)
            {
                MessageBox.Show("Please select start and end dates for the race.");
                return;
            }
            DateTime alku = (DateTime)dpAlkuPvm.SelectedDate;
            DateTime loppu = (DateTime)dpLoppuPvm.SelectedDate;
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

        private void TbKilpailuNimi_TextChanged(object sender, TextChangedEventArgs e)
        {
            string aika = textBoxStart == null ? "" : String.Join("", textBoxStart.Text.Split(':'));
            tbKilpaTiedostoPolku.Text = $"{tbTallennusPolku.Text}\\SeaMODE_{dpAlkuPvm.DisplayDate:yyyyMMdd}_{aika}_{tbKilpailuNimi.Text}.csv";
        }

        private void TbTallennusPolku_TextChanged(object sender, TextChangedEventArgs e)
        {
            string nimi = tbKilpailuNimi == null ? "" : tbKilpailuNimi.Text;
            if (tbKilpaTiedostoPolku != null)
            {
                string aika = textBoxStart == null ? "" : String.Join("", textBoxStart.Text.Split(':'));
                tbKilpaTiedostoPolku.Text = $"{tbTallennusPolku.Text}\\SeaMODE_{dpAlkuPvm.DisplayDate:yyyyMMdd}_{aika}_{nimi}.csv";
            }
        }

        private void TbAika_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = e.Source as TextBox;
            if (tb.Text == "HH.mm:ss" || String.IsNullOrEmpty(tb.Text))
            {
                return;
            }
            int pituus = tb.Text.Length;
            if ((pituus != 3 && pituus != 6) && !char.IsDigit(tb.Text, pituus - 1))
            {
                tb.Text = tb.Text.Substring(0, pituus - 1);
            }
            if (pituus == 2)
            {
                tb.Text += ":";
            }
            else if (pituus == 5)
            {
                tb.Text += ":";
            }
            tb.SelectionStart = tb.Text.Length;
        }

        private void TbAika_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = e.Source as TextBox;
            if (tb.Text == "HH.mm:ss")
            {
                tb.Clear(); 
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }
    }
}
