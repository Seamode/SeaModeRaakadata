using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SeaMODEParcerLibrary
{
    public class GpxLine
    {
        public GpxLine(DateTime aika)
        {
            EventTime = aika;
        }

        public DateTime EventTime { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public double Speed { get; set; }
        public string LatPosition { get; set; }
        public string LongPosition { get; set; }

        public void SetLongitude(string longitudeIn)
        {
            // Poistetaan etunolla ja siirretään  pilkku toisen merkitsevän numeron jälkeen
            if (Regex.IsMatch(longitudeIn, "^0")) {
                longitudeIn = longitudeIn.TrimStart(new Char[] { '0' });
            }
            // Poistetaan alkuperäinen piste
            if (Regex.IsMatch(longitudeIn, "^[0-6][0-9][0-9][/.]*"))
            {
             longitudeIn = longitudeIn.Replace(".", "");
            }
            if (Regex.IsMatch(longitudeIn, "^[0-6][0-9][0-9][/,]*"))
            {
                longitudeIn = longitudeIn.Replace(".", "");
            }
            // Laitetaan piste kahden merkitsevän numeron jälkeen
            if (Regex.IsMatch(longitudeIn, "^[0-6][0-9][0-9].*"))
            {
                longitudeIn = longitudeIn.Insert(2, ".");
                Longitude = CalculateMinutes(longitudeIn);
            }

        }
        public void SetLatitude(string latitudeIn)
        {
            if (Regex.IsMatch(latitudeIn, "^[0-9][0-9][0-9][0-9][.]"))
            {
                latitudeIn = latitudeIn.Replace(".", "");
                latitudeIn = latitudeIn.Insert(2, ".");
                Latitude = CalculateMinutes(latitudeIn);
            }
            if (Regex.IsMatch(latitudeIn, "^[0-9][0-9][0-9][0-9][,]*"))
            {
                latitudeIn = latitudeIn.Replace(",", "");
                latitudeIn = latitudeIn.Insert(2, ".");
                Latitude = CalculateMinutes(latitudeIn);
            }
        }
        private string CalculateMinutes(string s)
        {
            NumberFormatInfo provider = new NumberFormatInfo
            {
                NumberDecimalSeparator = "."
            };
            string[] a = s.Split('.');
            if (a.Length < 2) return null;
            string a2 = a[1].Insert(2, ".");
            double d;
            int aste;
            try
            {
                d = double.Parse(a2, provider);
                aste = Convert.ToInt32(a[0]);
            } catch (Exception)
            {
                return null;
            }
            double d2 = Math.Round(d / 60, 6, MidpointRounding.ToEven) + aste;
            
            return d2.ToString().Replace(",", ".");
         }
    }
}
