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
            endPattern = startPattern_c + endTime.ToString(pvmFormator);
            Rivit = new ArrayList();
            ReaderErrors = new List<string>();
        }

        // ei käytetä ollenkaan?
        //private const string riviOtsikkoPattern_c = "^Date_PC;Time_PC";
        private bool IsOtsikkoTehty = false;
        private readonly DateTime startTime;
        private readonly DateTime endTime;
        private const string startPattern_c = "^SeaMODE_";
        private readonly string startPattern;
        private readonly string endPattern;
        private readonly CultureInfo cultureInfo = new CultureInfo("fi-FI");
        private int columnCount;
        // liian suuri ajanmuutos = virhe, mutta missä on raja?
        private TimeSpan maximumTimeStep = TimeSpan.FromMilliseconds(250);
        private DateTime previousTime;

        public ArrayList Rivit { get; }
        public string OutDire { get; set; }
        public int DataRowCount { get; private set; } = 0;
        public List<string> ReaderErrors { get; private set; }
        // tämä on vain tiedostojen listausta wpf:ää varten.
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
                if ((Regex.IsMatch(fi.Name, startPattern) || Regex.IsMatch(fi.Name, endPattern)) && Regex.IsMatch(fi.Name, ".csv$") && fi.Name.Length == pit)
                    filekset.Add(fi.FullName);
                if (OutDire == null)
                    OutDire = new string(fi.DirectoryName.ToCharArray());
            }

            return filekset;
        }
        // Haetaan rivit yhdelle tiedostolle
        public void LueTiedosto(string fileName)
        {
            string riviOtsikkoPattern = "^Date_PC(.*)Time_PC";
            string[] seperator = { ";" };
            bool isOtsikkoOhi = false;
            using (StreamReader sr = File.OpenText(fileName))
            {
                string luettu = "";
                while ((luettu = sr.ReadLine()) != null)
                {
                    if (isOtsikkoOhi)
                    {
                        // Tarkistetaan sopiiko aika -> string splitillä haetaan aika
                        string[] rowValues = luettu.Split(seperator, StringSplitOptions.RemoveEmptyEntries);
                        if (TarkistaAika(rowValues))
                        {
                            if (rowValues.Length == columnCount)
                            {
                                DataRowCount++;
                                Rivit.Add(luettu);
                            }
                            else
                                ReaderErrors.Add($"There was missing data on row {Rivit.Count}. The row was disregarded.");
                        }
                    }
                    // Otsikkotiedot vain kerran
                    if (!IsOtsikkoTehty && !isOtsikkoOhi)
                    {
                        Rivit.Add(luettu);
                        Match headerMatch = Regex.Match(luettu, riviOtsikkoPattern);
                        if (headerMatch.Success)
                        {
                            columnCount = luettu.Split(seperator, StringSplitOptions.RemoveEmptyEntries).Length;
                            IsOtsikkoTehty = true;
                            isOtsikkoOhi = true;
                            seperator[0] = headerMatch.Groups[1].ToString();
                        }

                    }
                    // Otsikko luettu myös ensimmäisen tiedoston jälkeen.
                    if (IsOtsikkoTehty && !isOtsikkoOhi)
                    {
                        Match headerMatch = Regex.Match(luettu, riviOtsikkoPattern);
                        if (headerMatch.Success)
                        {
                            seperator[0] = headerMatch.Groups[1].ToString();
                            isOtsikkoOhi = true;
                        }
                    }
                }
            }
        }
        // Tarkistetaan aika
        private bool TarkistaAika(string[] arvot)
        {
            // ensimmäisessä alkiossa pvm muodossa pp.kk.vvvv ja toisessa aika hh:mm:ss.nnn
            DateTime tapahtumaAika = DateTime.ParseExact(arvot[0] + " " + arvot[1], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            if (tapahtumaAika >= startTime && tapahtumaAika <= endTime)
            {
                if (previousTime == null)
                    previousTime = tapahtumaAika;
                else if (tapahtumaAika - previousTime > maximumTimeStep)
                {
                    ReaderErrors.Add($"There was a large time difference at row {Rivit.Count}. The row was disregarded.");
                    previousTime = tapahtumaAika;
                    return false;
                }
                previousTime = tapahtumaAika;
                return true;
            }
            else
                return false;
        }

    }


}
