using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SeaModeReadWrite
{
    class SeamodeReader
    {
        // Annetaan start date ja time 
        public SeamodeReader(DateTime aloitusAika, DateTime lopetusAika)
        {
            startTime = aloitusAika;
            endTime = lopetusAika;
            string pvmFormator = String.Format("yyyyMMdd");
            startPattern = startPattern_c + startTime.ToString(pvmFormator);
            rivit = new ArrayList();
         
        }
        
        private const string riviOtsikkoPattern_c = "^Date_PC;Time_PC";
        private Boolean IsOtsikkoTehty = false;
        private DateTime startTime;
        private DateTime endTime;
        private const string startPattern_c = "^SeaMODE_";
        private string startPattern;
        private CultureInfo cultureInfo = new CultureInfo("fi-FI");


        public ArrayList rivit { get; }
        public string outDire { get; set; }

        public List<String> haeTiedostot()
        {
            List<String> filekset = new List<String>();
            // Muuta seuraavat syötteeksi tai jostain configista haettavaksi
            DirectoryInfo di = new DirectoryInfo(@"D:\scrum projekti\myairbridge-kSWEva1J3");
            String esimerkki = "SeaMODE_20190928_112953.csv";
            int pit = esimerkki.Length;
            foreach (var fi in di.GetFiles())
            {
                if (Regex.IsMatch(fi.Name, startPattern) && Regex.IsMatch(fi.Name, ".csv$") && fi.Name.Length == pit)
                    filekset.Add(fi.FullName);
                if (outDire == null)
                    outDire = new String(fi.DirectoryName);
            }

            return filekset;
        }
        // Haetaan rivit yhdelle tiedostolle
        public void lueTiedosto(string fileName)
        {
            String riviOtsikkoPattern = "^Date_PC;Time_PC";
            Boolean isOtsikkoOhi = false;
            using (StreamReader sr = File.OpenText(fileName))
            {
                String luettu = "";
                while ((luettu = sr.ReadLine()) != null)
                {
                    if(isOtsikkoOhi)
                    {
                        // Tarkistetaan sopiiko aika -> string splitillä haetaan aika
                        if (tarkistaAika(luettu))
                            rivit.Add(luettu);
                    }
                    // Otsikkotiedot vain kerran
                    if(!IsOtsikkoTehty && !isOtsikkoOhi)
                    {
                        rivit.Add(luettu);
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
        private Boolean tarkistaAika(string luettuRivi)
        {
            string[]  arvot = luettuRivi.Split(';');
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
