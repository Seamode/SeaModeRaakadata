﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace RaakadataLibrary
{
    public class SeamodeReader
    {
        // Annetaan start date ja time 
        public SeamodeReader(DateTime _startTime, DateTime _endTime)
        {
            startTime = _startTime;
            endTime = _endTime;
            string dateFormat = string.Format("yyyyMMdd");
            startPattern = startPattern_c + startTime.ToString(dateFormat);
            endPattern = startPattern_c + endTime.ToString(dateFormat);
            DataRowErrors = new List<string>();
        }

        private bool headersWritten = false;
        private readonly DateTime startTime;
        private readonly DateTime endTime;
        public bool PastEnd { get; private set; } = false;
        private const string startPattern_c = "^SeaMODE_";
        private readonly string startPattern;
        private readonly string endPattern;
        private readonly CultureInfo cultureInfo = new CultureInfo("fi-FI");
        private int columnCount;
        // liian suuri ajanmuutos = virhe, mutta missä on raja?
        private TimeSpan maximumTimeStep = TimeSpan.FromSeconds(1);
        private DateTime? prevEventTime = null;
        private readonly string headerRowPattern = "^Date_PC(.*)Time_PC";
        private char erotinChar = ';'; 

        public string TmpFile = Path.GetTempFileName();
        public List<GpxLine> gpxLines { get; set; }
        public int DataRowCount { get; private set; } = 0;
        public List<string> DataRowErrors { get; private set; }
        public DateTime gpxRaceTime { get; private set;}
        // jotta käyttöliittymä ei lukkiudu tiedoston luku ja kirjoituksen ajaksi
        public async Task ReadAndWriteFilesAsync(string path) => await Task.Run(() => ForeachFileIn(FetchFilesToRead(path)));

        private void ForeachFileIn(List<string> files)
        {
            foreach (string file in files)
            {
                ReadAndWriteDataFile(file);
            }
        }

        // tämä on vain tiedostojen listausta wpf:ää varten.
        public static List<string> FetchFilesToList(string path)
        {
            List<string> files = new List<string>();
            DirectoryInfo di = new DirectoryInfo(path);
            string example = "SeaMODE_20190928_112953.csv";
            int len = example.Length;
            foreach (var fi in di.GetFiles())
            {
                if (Regex.IsMatch(fi.Name, "^SeaMODE_") && Regex.IsMatch(fi.Name, ".csv$") && fi.Name.Length == len)
                    files.Add(fi.FullName);
            }

            return files;
        }

        public List<string> FetchFilesToRead(string path)
        {
            // Muuta seuraavat syötteeksi tai jostain configista haettavaksi
            List<string> files = new List<string>();
            DirectoryInfo di = new DirectoryInfo(path);
            string example = "SeaMODE_20190928_112953.csv";
            int len = example.Length;
            foreach (var fi in di.GetFiles())
            {
                if (PastEnd)
                    break;
                if ((Regex.IsMatch(fi.Name, startPattern) || Regex.IsMatch(fi.Name, endPattern)) && Regex.IsMatch(fi.Name, ".csv$") && fi.Name.Length == len)
                    files.Add(fi.FullName);
            }

            return files;
        }

        // Haetaan rivit yhdelle tiedostolle
        private void ReadAndWriteDataFile(string filePath)
        {
            string[] separator = { ";" };
            bool validFile = true;
            bool headersFound = false;
            List<string> headerRows = new List<string>();
            int rowNum = 1;
            using (StreamWriter sw = File.AppendText(TmpFile))
            using (StreamReader sr = File.OpenText(filePath))
            {
                string row = "";
                while ((row = sr.ReadLine()) != null && validFile && !PastEnd)
                {
                    if (headersFound)
                    {
                        // Tarkistetaan sopiiko aika -> string splitillä haetaan aika
                        string[] rowValues = row.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                        if (TimeValidation(rowValues, rowNum))
                        {
                            if (!headersWritten)
                            {
                                foreach (string line in headerRows)
                                {
                                    sw.WriteLine(line);
                                }
                                columnCount = headerRows.Last().Split(separator, StringSplitOptions.RemoveEmptyEntries).Length;
                                headerRows.Clear();
                                headerRows.TrimExcess();
                                headersWritten = true;
                            }
                            if (rowValues.Length == columnCount)
                            {
                                DataRowCount++;
                                sw.WriteLine(row);
                            }
                            else
                                DataRowErrors.Add($"There was missing data on row {rowNum}. The row was disregarded.");
                        }
                    }
                    else
                        FileValidation(headerRows, rowNum, row, ref validFile, ref headersFound, ref separator);
                    rowNum++;
                }
                if (!validFile)
                    DataRowErrors.Add($"There was something wrong with the xml section in file:\n{filePath}");
            }
        }

        private void FileValidation(List<string> headerRows,
                                    int rowNum,
                                    string row,
                                    ref bool validFile,
                                    ref bool headersFound,
                                    ref string[] separator)
        {
            // testaus näyttääkö tiedosto oikealta, jos ei muistiin kirjoittaminen lopetetaan.
            switch (rowNum)
            {
                case 1:
                    if (!row.StartsWith("<?xml version="))
                        validFile = false;
                    break;
                case 2:
                    if (!row.StartsWith("<CalibrationData>"))
                        validFile = false;
                    break;
                default:
                    Match headerMatch = Regex.Match(row, headerRowPattern);
                    if (!Regex.IsMatch(row, @"^\s+<") && !row.StartsWith("</CalibrationData>") && !headerMatch.Success)
                        validFile = false;
                    else if (headerMatch.Success)
                    {
                        separator[0] = headerMatch.Groups[1].ToString();
                        char[] chArray = separator[0].ToCharArray();
                        erotinChar = chArray[0];
                        headersFound = true;
                    }
                    break;
            }
            if (validFile && !headersWritten)
                headerRows.Add(row);
        }

        public void haeGpxData(string fileName)
        {
            string[] separator = { ";" };
            bool validFile = true;
            bool headersFound = false;
            List<string> headerRows = new List<string>();
            int rowNum = 1;
            using (StreamReader sr = File.OpenText(fileName))
            {
                string row = "";
                DateTime prevDateTime = DateTime.MinValue;
                while ((row = sr.ReadLine()) != null)
                {
                    if (headersFound)
                    {
                        string[] rowValues = row.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                        if (TimeValidation(rowValues))
                        {
                            DateTime newDateTime;
                            // Tehdään gpx instanssin luonti sekunnin välein
                            newDateTime = muodostoGpxAika(row);
                            TimeSpan tp = newDateTime - prevDateTime;
                            if (tp.TotalSeconds >= 1)
                            {
                                TeeGpx(row);
                                prevDateTime = muodostoGpxAika(row);
                                // Aloitusaika otsikolle
                                if(gpxRaceTime == DateTime.MinValue)
                                {
                                    gpxRaceTime = prevDateTime;
                                }
                            }
                        }
                        /*if (Regex.IsMatch(row, headerRowPattern))
                        {
                            headersFound = true;
                        }*/
                    }
                    else
                        FileValidation(headerRows, rowNum, row, ref validFile, ref headersFound, ref separator);
                    rowNum++;
                }
            }
        }

        private void TeeGpx(string luettuRivi)
        {
            if (gpxLines == null)
                gpxLines = new List<GpxLine>();

            string[] arvot = luettuRivi.Split(erotinChar);
            DateTime aika = DateTime.ParseExact(arvot[23] + " " + arvot[24], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            //GpxLine gpxLine = new GpxLine(aika, arvot[25], arvot[27], arvot[29]);  
            GpxLine gpxLine = new GpxLine(aika);
            gpxLine.setLatitude(arvot[25]);
            gpxLine.setLongitude(arvot[27]);
            gpxLines.Add(gpxLine);
        }

        private bool TimeValidation(string[] values)
        {
            // ensimmäisessä alkiossa pvm muodossa pp.kk.vvvv ja toisessa aika hh:mm:ss.nnn
            DateTime eventTime = DateTime.ParseExact(values[0] + " " + values[1], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            return (eventTime >= startTime && eventTime <= endTime) ? true : false;
        }

        // Tarkistetaan aika
        private bool TimeValidation(string[] values, int rowNum)
        {
            // ensimmäisessä alkiossa pvm muodossa pp.kk.vvvv ja toisessa aika hh:mm:ss.nnn
            DateTime eventTime = DateTime.ParseExact(values[0] + " " + values[1], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            if (eventTime > endTime)
            {
                PastEnd = true;
                return false;
            }
            else if (eventTime >= startTime && eventTime <= endTime)
            {
                if (prevEventTime != null && eventTime - prevEventTime > maximumTimeStep)
                {
                    DataRowErrors.Add($"There was a large time difference between row {rowNum} and the previous row.");
                    prevEventTime = eventTime;
                    return false;
                }
                prevEventTime = eventTime;
                return true;
            }
            else
                return false;
        }

        private DateTime muodostoGpxAika(string luettuRivi)
        {
            // Väärä erotinmerkki
            string[] arvot = luettuRivi.Split(erotinChar);
            DateTime aika;
            try
            {
                aika = DateTime.ParseExact(arvot[23] + " " + arvot[24], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            }
            catch (System.IndexOutOfRangeException e)
            {
                // Laitetaan ajalle jokin outo arvo, jos rikkinäinen data
                aika = DateTime.MinValue;
            }
            return aika;
        }
    }
}
