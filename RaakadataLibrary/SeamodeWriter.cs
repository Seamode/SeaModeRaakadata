using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RaakadataLibrary
{
    // Kirjoitetaan valikoidut rivit yhdistelmätiedostoon.
    public class SeamodeWriter
    { 
        
        public string OutFile { get; set; }
        public bool Kirjoita(ArrayList rivit)
        {
            string[] asArr = new string[rivit.Count];
            rivit.CopyTo(asArr);
            File.WriteAllLines(OutFile, asArr);
            return true;
        }
    }
}
