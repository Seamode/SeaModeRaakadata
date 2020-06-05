﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace SeaMODEParcerLibrary
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
        private TimeSpan maximumTimeStep = TimeSpan.FromSeconds(10);
        private DateTime? prevEventTime = null;
        private readonly string headerRowPattern = "^Date_PC(.*)Time_PC";
        private char separatorChar = ';'; 

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
            foreach (var fi in di.GetFiles("*.csv"))
            {
                files.Add(fi.Name);
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
                        separatorChar = chArray[0];
                        headersFound = true;
                    }
                    break;
            }
            if (validFile && !headersWritten)
                headerRows.Add(row);
        }

        public void fetchGPXData(string fileName)
        {
            string[] separator = { ";" };
            bool validFile = true;
            bool headersFound = false;
            List<string> headerRows = new List<string>();
            int rowNum = 1;
            int columnCount = 0;
            int columnContErrors = 0;
            using (StreamReader sr = File.OpenText(fileName))
            {
                string row = "";
                DateTime prevDateTime = DateTime.MinValue;
                while ((row = sr.ReadLine()) != null)
                {
                    if (headersFound)
                    {
                        string[] rowValues = row.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                        if(rowValues.Length != columnCount)
                        {
                            columnContErrors += 1;
                            DataRowErrors.Add($"The number of columns {rowValues.Length} in row {rowNum} did not match the number of columns of the headerline");
                            rowNum++;
                            continue;
                        } 
                        if (TimeValidation(rowValues))
                        {
                            DateTime newDateTime;
                            // Tehdään gpx instanssin luonti sekunnin välein
                            newDateTime = formGPXTime(row);
                            TimeSpan tp = newDateTime - prevDateTime;
                            if (tp.TotalSeconds >= 1)
                            {
                                makeGPX(row, columnCount, rowNum);
                                prevDateTime = formGPXTime(row);
                                // Aloitusaika otsikolle
                                if (gpxRaceTime == DateTime.MinValue)
                                {
                                    gpxRaceTime = prevDateTime;
                                }
                            }
                        }

                    }
                    else
                    {
                        FileValidation(headerRows, rowNum, row, ref validFile, ref headersFound, ref separator);
                        if (Regex.IsMatch(row, headerRowPattern))
                        {
                            columnCount = row.Split(separator, StringSplitOptions.RemoveEmptyEntries).Length;
                        }
                    }
                    rowNum++;
                }
            }
        }

        private void makeGPX(string luettuRivi, int columnCount, int rowNumber)
        {
            if (gpxLines == null)
                gpxLines = new List<GpxLine>();

            string[] arvot = luettuRivi.Split(separatorChar);
            // Rivillä oltava sama määrä sarakkeita kuin otsikollakin
            if(arvot.Length != columnCount)
            {
                DataRowErrors.Add($"The number of columns {arvot.Length} in row {rowNumber} did not match to the headerline");
                return;
            }
            DateTime aika;
            try
            {
                aika = DateTime.ParseExact(arvot[23] + " " + arvot[24], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            }
            catch (System.IndexOutOfRangeException e)
            {
                DataRowErrors.Add($"Parsing time at row {rowNumber} failed"); 
                return;
            }
            // Tarkistetaan longitue ja latitude
            Boolean isCorrect = true;
            if(!Regex.IsMatch(arvot[25], ConfigurationManager.AppSettings["patLatitude"]))
            {
                DataRowErrors.Add($"Latitude at {rowNumber} empty or not in correct format");
                isCorrect = false;
            }
            if (!Regex.IsMatch(arvot[27], ConfigurationManager.AppSettings["patLongitude"]))
            {
                DataRowErrors.Add($"Longitude at {rowNumber} empty or not in correct format");
                isCorrect = false;
            }
            //GpxLine gpxLine = new GpxLine(aika, arvot[25], arvot[27], arvot[29]);  
            // Tarkistetaan latitude ja longitude
            GpxLine gpxLine = new GpxLine(aika);

            gpxLine.setLatitude(arvot[25]);
            gpxLine.latPosition = arvot[26];
            gpxLine.setLongitude(arvot[27]);
            gpxLine.longPosition = arvot[28];
            gpxLines.Add(gpxLine);
        }

        private bool TimeValidation(string[] values)
        {
            // ensimmäisessä alkiossa pvm muodossa pp.kk.vvvv ja toisessa aika hh:mm:ss.nnn
            DateTime eventTime = DateTime.ParseExact(values[0] + " " + values[1], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            return (eventTime >= startTime && eventTime <= endTime);
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

        private DateTime formGPXTime(string luettuRivi)
        {
            // Väärä erotinmerkki
            string[] values = luettuRivi.Split(separatorChar);
            DateTime time;
            try
            {
                time = DateTime.ParseExact(values[23] + " " + values[24], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            }
            catch (System.IndexOutOfRangeException e)
            {
                // Laitetaan ajalle jokin outo arvo, jos rikkinäinen data
                time = DateTime.MinValue;
            }
            return time;
        }
    }
}
