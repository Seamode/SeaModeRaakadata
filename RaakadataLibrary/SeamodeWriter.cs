using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SeaMODEParcerLibrary
{
    // Kirjoitetaan valikoidut rivit yhdistelmätiedostoon.
    public class SeamodeWriter
    { 
        
        public string OutFile { get; set; }
        public bool WriteFile(ArrayList rows)
        {
            string[] asArr = new string[rows.Count];
            rows.CopyTo(asArr);
            File.WriteAllLines(OutFile, asArr);
            return true;
        }
    }
}
