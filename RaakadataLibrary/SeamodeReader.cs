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

        private bool IsOtsikkoTehty = false;
        private readonly DateTime startTime;
        private readonly DateTime endTime;
        private const string startPattern_c = "^SeaMODE_";
        private readonly string startPattern;
        private readonly string endPattern;
        private readonly CultureInfo cultureInfo = new CultureInfo("fi-FI");
        private int columnCount;
        // liian suuri ajanmuutos = virhe, mutta missä on raja?
        private TimeSpan maximumTimeStep = TimeSpan.FromSeconds(1);
        private DateTime? previousTime = null;

        public ArrayList Rivit { get; }
        public string OutDire { get; set; }
        public List<GpxLine> gpxLines { get; set; }
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
            int rowNum = 1;
            using (StreamReader sr = File.OpenText(fileName))
            {
                string luettu = "";
                bool validFile = true;
                while ((luettu = sr.ReadLine()) != null && validFile)
                {
                    if (isOtsikkoOhi)
                    {
                        // Tarkistetaan sopiiko aika -> string splitillä haetaan aika
                        string[] rowValues = luettu.Split(seperator, StringSplitOptions.RemoveEmptyEntries);
                        if (TarkistaAika(rowValues, rowNum))
                        {
                            if (rowValues.Length == columnCount)
                            {
                                DataRowCount++;
                                Rivit.Add(luettu);
                                // tähän write luettu
                            }
                            else
                                ReaderErrors.Add($"There was missing data on row {rowNum}. The row was disregarded.");
                        }
                    }
                    // Otsikkotiedot vain kerran
                    if (!IsOtsikkoTehty && !isOtsikkoOhi)
                    {
                        Match headerMatch = Regex.Match(luettu, riviOtsikkoPattern);
                        if (headerMatch.Success)
                        {
                            columnCount = luettu.Split(seperator, StringSplitOptions.RemoveEmptyEntries).Length;
                            IsOtsikkoTehty = true;
                            isOtsikkoOhi = true;
                            seperator[0] = headerMatch.Groups[1].ToString();
                            // tähän writealltext rivit + write luettu?
                        }
                        if (!isOtsikkoOhi)
                            validFile = FileValidation(rowNum, luettu, validFile);
                        Rivit.Add(luettu);
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
                    rowNum++;
                }
            }
        }

        private static bool FileValidation(int rowNum, string luettu, bool validFile)
        {
            // testaus näyttääkö tiedosto oikealta, jos ei muistiin kirjoittaminen lopetetaan.
            switch (rowNum)
            {
                case 1:
                    if (!luettu.StartsWith("<?xml version="))
                        validFile = false;
                    break;
                case 2:
                    if (!luettu.StartsWith("<CalibrationData>"))
                        validFile = false;
                    break;
                default:
                    if (!Regex.IsMatch(luettu, @"^\s+<") && !luettu.StartsWith("</CalibrationData>"))
                        validFile = false;
                    break;
            }

            return validFile;
        }

        public void haeGpxData(string fileName)
        {
            string riviOtsikkoPattern = "^Date_PC(.*)Time_PC";
            string[] seperator = { ";" };
            bool isOtsikkoOhi = false;
            using (StreamReader sr = File.OpenText(fileName))
            {
                string luettu = "";
                DateTime prevDateTime = DateTime.MinValue;
                while ((luettu = sr.ReadLine()) != null)
                {
                    string[] rowValues = luettu.Split(seperator, StringSplitOptions.RemoveEmptyEntries);
                    if (isOtsikkoOhi && TarkistaAika(rowValues))
                    {
                        //Nullable<DateTime> prevDateTime = null;
                        DateTime newDateTime;
                        // Tehdään gpx instanssin luonti sekunnin välein
                        newDateTime = muodostoGpxAika(luettu);
                        TimeSpan tp = newDateTime - prevDateTime;
                        if (tp.TotalSeconds >= 1)
                        {
                            TeeGpx(luettu);
                            prevDateTime = muodostoGpxAika(luettu);
                        }
                    }
                    if (Regex.IsMatch(luettu, riviOtsikkoPattern))
                    {
                        isOtsikkoOhi = true;
                    }
                }
            }

        }
        private void TeeGpx(string luettuRivi)
        {
            if (gpxLines == null)
                gpxLines = new List<GpxLine>();

            string[] arvot = luettuRivi.Split(';');
            DateTime aika = DateTime.ParseExact(arvot[23] + " " + arvot[24], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            //GpxLine gpxLine = new GpxLine(aika, arvot[25], arvot[27], arvot[29]);  
            GpxLine gpxLine = new GpxLine(aika);
            gpxLine.setLatitude(arvot[25]);
            gpxLine.setLongitude(arvot[27]);
            gpxLines.Add(gpxLine);
        }
        // Tarkistetaan aika
        private bool TarkistaAika(string[] values, int rowNum)
        {
            // ensimmäisessä alkiossa pvm muodossa pp.kk.vvvv ja toisessa aika hh:mm:ss.nnn
            DateTime tapahtumaAika = DateTime.ParseExact(values[0] + " " + values[1], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            if (tapahtumaAika >= startTime && tapahtumaAika <= endTime)
            {
                if (previousTime != null && tapahtumaAika - previousTime > maximumTimeStep)
                {
                    ReaderErrors.Add($"There was a large time difference at row {rowNum}.");
                    previousTime = tapahtumaAika;
                    return false;
                }
                previousTime = tapahtumaAika;
                return true;
            }
            else
                return false;
        }
        private DateTime muodostoGpxAika(string luettuRivi)
        {
            string[] arvot = luettuRivi.Split(';');
            DateTime aika = DateTime.ParseExact(arvot[23] + " " + arvot[24], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            return aika;
        }
    }
}
