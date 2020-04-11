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
    }
}
