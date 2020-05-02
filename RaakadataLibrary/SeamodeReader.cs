using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
            headerRows = new List<string>();
            DataRowErrors = new List<string>();
        }

        private bool headerRowsFound = false;
        private bool headerRowsWritten = false;
        private readonly DateTime startTime;
        private readonly DateTime endTime;
        private const string startPattern_c = "^SeaMODE_";
        private readonly string startPattern;
        private readonly string endPattern;
        private readonly CultureInfo cultureInfo = new CultureInfo("fi-FI");
        private int columnCount;
        // liian suuri ajanmuutos = virhe, mutta missä on raja?
        private TimeSpan maximumTimeStep = TimeSpan.FromSeconds(1);
        private DateTime? prevEventTime = null;
        private readonly List<string> headerRows;

        public string TmpFile = Path.GetTempFileName();
        public List<GpxLine> gpxLines { get; set; }
        public int DataRowCount { get; private set; } = 0;
        public List<string> DataRowErrors { get; private set; }

        public async Task ReadFilesAsync(string path) => await Task.Run(() => FetchFilesToRead(path));

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

        public void FetchFilesToRead(string directoryPath)
        {
            // Muuta seuraavat syötteeksi tai jostain configista haettavaksi
            DirectoryInfo di = new DirectoryInfo(directoryPath);
            string example = "SeaMODE_20190928_112953.csv";
            int len = example.Length;
            foreach (var fi in di.GetFiles())
            {
                if ((Regex.IsMatch(fi.Name, startPattern) || Regex.IsMatch(fi.Name, endPattern)) && Regex.IsMatch(fi.Name, ".csv$") && fi.Name.Length == len)
                    ReadDataFile(fi.FullName);
            }
        }
        // Haetaan rivit yhdelle tiedostolle
        public void ReadDataFile(string filePath)
        {
            string headerRowPattern = "^Date_PC(.*)Time_PC";
            string[] separator = { ";" };
            bool headerRowsFoundInCurrentFile = false;
            int rowNum = 1;
            using (StreamWriter sw = File.AppendText(TmpFile))
            using (StreamReader sr = File.OpenText(filePath))
            {
                string row = "";
                bool validFile = true;
                while ((row = sr.ReadLine()) != null && validFile)
                {
                    if (headerRowsFoundInCurrentFile)
                    {
                        // Tarkistetaan sopiiko aika -> string splitillä haetaan aika
                        string[] rowValues = row.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                        if (TimeValidation(rowValues, rowNum))
                        {
                            if (rowValues.Length == columnCount)
                            {
                                DataRowCount++;
                                if (headerRowsWritten)
                                    sw.WriteLine(row);
                                else
                                {
                                    foreach (string line in headerRows)
                                    {
                                        sw.WriteLine(line);
                                    }
                                    sw.WriteLine(row);
                                    headerRows.Clear();
                                    headerRows.TrimExcess();
                                    headerRowsWritten = true;
                                }
                            }
                            else
                                DataRowErrors.Add($"There was missing data on row {rowNum}. The row was disregarded.");
                        }
                    }
                    // Otsikkotiedot vain kerran
                    if (!headerRowsFound && !headerRowsFoundInCurrentFile)
                    {
                        Match headerMatch = Regex.Match(row, headerRowPattern);
                        if (headerMatch.Success)
                        {
                            columnCount = row.Split(separator, StringSplitOptions.RemoveEmptyEntries).Length;
                            headerRowsFound = true;
                            headerRowsFoundInCurrentFile = true;
                            separator[0] = headerMatch.Groups[1].ToString();
                        }
                        if (!headerRowsFoundInCurrentFile)
                            validFile = FileValidation(rowNum, row, validFile);
                        headerRows.Add(row);
                    }
                    // Otsikko luettu myös ensimmäisen tiedoston jälkeen.
                    if (headerRowsFound && !headerRowsFoundInCurrentFile)
                    {
                        Match headerMatch = Regex.Match(row, headerRowPattern);
                        if (headerMatch.Success)
                        {
                            separator[0] = headerMatch.Groups[1].ToString();
                            headerRowsFoundInCurrentFile = true;
                        }
                    }
                    rowNum++;
                }
                if (!validFile)
                    DataRowErrors.Add($"There was something wrong with the xml section in file:\n{filePath}");
            }
        }

        private static bool FileValidation(int rowNum, string row, bool validFile)
        {
            // testaus näyttääkö tiedosto oikealta, jos ei muistiin kirjoittaminen lopetetaan.
            // tarvitaanko tätä enää?
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
                    if (!Regex.IsMatch(row, @"^\s+<") && !row.StartsWith("</CalibrationData>"))
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
        private bool TimeValidation(string[] values, int rowNum)
        {
            // ensimmäisessä alkiossa pvm muodossa pp.kk.vvvv ja toisessa aika hh:mm:ss.nnn
            DateTime eventTime = DateTime.ParseExact(values[0] + " " + values[1], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            if (eventTime >= startTime && eventTime <= endTime)
            {
                if (prevEventTime != null && eventTime - prevEventTime > maximumTimeStep)
                {
                    DataRowErrors.Add($"There was a large time difference at row {rowNum}.");
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
            string[] arvot = luettuRivi.Split(';');
            DateTime aika = DateTime.ParseExact(arvot[23] + " " + arvot[24], "dd.MM.yyyy HH:mm:ss.fff", cultureInfo);
            return aika;
        }
    }
}
