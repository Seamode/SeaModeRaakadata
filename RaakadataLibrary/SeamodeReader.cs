using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace RaakadataLibrary
{
    public class SeamodeReader
    {
        // Annetaan start date ja time 
        public SeamodeReader(DateTime aloitusAika, DateTime lopetusAika)
        {
            startTime = aloitusAika;
            endTime = lopetusAika;
            string pvmFormator = string.Format("yyyyMMdd");
            startPattern = startPattern_c + startTime.ToString(pvmFormator);
            Rivit = new ArrayList();

        }

        // ei käytetä ollenkaan?
        //private const string riviOtsikkoPattern_c = "^Date_PC;Time_PC";
        private bool IsOtsikkoTehty = false;
        private readonly DateTime startTime;
        private readonly DateTime endTime;
        private const string startPattern_c = "^SeaMODE_";
        private readonly string startPattern;
        private readonly CultureInfo cultureInfo = new CultureInfo("fi-FI");


        public ArrayList Rivit { get; }
        public string OutDire { get; set; }

        public static List<string> HaeTiedostotListaan(string polku)
        {
            List<string> filekset = new List<string>();
            DirectoryInfo di = new DirectoryInfo(polku);
            string esimerkki = "SeaMODE_20190928_112953.csv";
            int pit = esimerkki.Length;
            foreach (var fi in di.GetFiles())
            {
                if (Regex.IsMatch(fi.Name, "^SeaMODE_") && Regex.IsMatch(fi.Name, ".csv$") && fi.Name.Length == pit)
                    filekset.Add(fi.FullName);
            }

            return filekset;
        }

        public List<string> HaeTiedostot(string polku)
        {
            List<string> filekset = new List<string>();
            // Muuta seuraavat syötteeksi tai jostain configista haettavaksi
            DirectoryInfo di = new DirectoryInfo(polku);
            string esimerkki = "SeaMODE_20190928_112953.csv";
            int pit = esimerkki.Length;
            foreach (var fi in di.GetFiles())
            {
                if (Regex.IsMatch(fi.Name, startPattern) && Regex.IsMatch(fi.Name, ".csv$") && fi.Name.Length == pit)
                    filekset.Add(fi.FullName);
                if (OutDire == null)
                    OutDire = new string(fi.DirectoryName.ToCharArray());
            }

            return filekset;
        }
        // Haetaan rivit yhdelle tiedostolle
        public void LueTiedosto(string fileName)
        {
            string riviOtsikkoPattern = "^Date_PC;Time_PC";
            bool isOtsikkoOhi = false;
            using (StreamReader sr = File.OpenText(fileName))
            {
                string luettu = "";
                while ((luettu = sr.ReadLine()) != null)
                {
                    if (isOtsikkoOhi)
                    {
                        // Tarkistetaan sopiiko aika -> string splitillä haetaan aika
                        if (TarkistaAika(luettu))
                            Rivit.Add(luettu);
                    }
                    // Otsikkotiedot vain kerran
                    if (!IsOtsikkoTehty && !isOtsikkoOhi)
                    {
                        Rivit.Add(luettu);
                        if (Regex.IsMatch(luettu, riviOtsikkoPattern))
                        {
                            IsOtsikkoTehty = true;
                            isOtsikkoOhi = true;
                        }

                    }
                    // Otsikko luettu myös ensimmäisen tiedoston jälkeen.
                    if (IsOtsikkoTehty && !isOtsikkoOhi && Regex.IsMatch(luettu, riviOtsikkoPattern))
                        isOtsikkoOhi = true;
                }
            }
        }
        // Tarkistetaan aika
        private bool TarkistaAika(string luettuRivi)
        {
            string[] arvot = luettuRivi.Split(';');
            // Rivissä jotain vikaa
            if (arvot.Length < 2)
                return false;
            // ensimmäisessä alkiossa pvm muodossa pp.kk.vvvv ja toisessa aika hh:mm:ss.nnn
            DateTime tapahtumaAika = DateTime.ParseExact(arvot[0] + " " + arvot[1], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            if (tapahtumaAika >= startTime && tapahtumaAika <= endTime)
                return true;
            else
                return false;
        }

    }


}
