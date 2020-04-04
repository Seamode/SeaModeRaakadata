using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SeaModeReadWrite
{
    // Kirjoitetaan valikoidut rivit yhdistelmätiedostoon.
    class SeamodeWriter
    { 
        
        public string outFile { get; set; }
        public Boolean kirjoita(ArrayList rivit)
        {
            string[] asArr = new string[rivit.Count];
            rivit.CopyTo(asArr);
            System.IO.File.WriteAllLines(outFile, asArr);
            return true;
        }
    }
}
