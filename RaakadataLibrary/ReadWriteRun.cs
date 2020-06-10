using System;
using System.Collections.Generic;
using System.Globalization;

namespace SeaModeReadWrite
{
    class Program
    {
        static void Main(string[] args)
        {
            // Haetaan tiedostot
            string aloitusAika = "28.09.2019" + " 11:29:56";
            string lopetusAika = "28.09.2019" + " 11:30:15";
            CultureInfo cultureInfo = new CultureInfo("fi-FI");
            DateTime aloitus = DateTime.ParseExact(aloitusAika, "dd.MM.yyyy HH:mm:ss", cultureInfo);
            DateTime lopetus = DateTime.ParseExact(lopetusAika, "dd.MM.yyyy HH:mm:ss", cultureInfo);
            Console.WriteLine("Haetaanpa tiedostot");
            SeamodeReader seamodeReader
                = new SeamodeReader(aloitus, lopetus);
            
            List<String> filekset = seamodeReader.haeTiedostot();
            Console.WriteLine("Tiedostoja tuli haettua==> " + filekset.Count + " kappaletta");
            foreach(string tiedosto in filekset)
            {
                Console.WriteLine("Tiedosto: " + tiedosto);
                seamodeReader.lueTiedosto(tiedosto);
            }
            Console.WriteLine("Rivejä haettiin mukaanlukien otsikko: " + seamodeReader.rivit.Count + " kappaletta");
            
            // Kirjoitetaan
            Console.WriteLine("Kitjoitetaan tiedostoon: " + seamodeReader.outDire + "\\koonti.csv");
            SeamodeWriter seamodeWriter = new SeamodeWriter();
            // Molemmat pitää jatkossa hakea confauksesta
            seamodeWriter.outFile = seamodeReader.outDire + "\\koonti.csv";
            seamodeWriter.kirjoita(seamodeReader.rivit);

        }
    }
}
