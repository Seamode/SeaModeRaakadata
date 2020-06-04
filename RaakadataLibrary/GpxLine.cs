using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RaakadataLibrary
{
    public class GpxLine
    {
        public GpxLine(DateTime aika)
        {
            this.eventTime = aika;
        }

        public DateTime eventTime { get; set; }
        public string latitude { get; set; }
        public string longitude { get; set; }
        public double speed { get; set; }
        public string latPosition { get; set; }
        public string longPosition { get; set; }

        public void setLongitude(string longitudeIn)
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
                this.longitude = laskeMinuutit(longitudeIn);
            }

        }
        public void setLatitude(string latitudeIn)
        {
            if (Regex.IsMatch(latitudeIn, "^[0-9][0-9][0-9][0-9][.]"))
            {
                latitudeIn = latitudeIn.Replace(".", "");
                latitudeIn = latitudeIn.Insert(2, ".");
                this.latitude = laskeMinuutit(latitudeIn);
            }
            if (Regex.IsMatch(latitudeIn, "^[0-9][0-9][0-9][0-9][,]*"))
            {
                latitudeIn = latitudeIn.Replace(",", "");
                latitudeIn = latitudeIn.Insert(2, ".");
                this.latitude = laskeMinuutit(latitudeIn);
            }
        }
        private string laskeMinuutit(string s)
        {
            NumberFormatInfo provider = new NumberFormatInfo();
            provider.NumberDecimalSeparator = ".";
            string[] seperator = { "." };
            string[] a = s.Split('.');
            if (a.Length < 2) return null;
            string a2 = a[1].Insert(2, ".");
            double d;
            int aste = 0;
            try
            {
                d = Double.Parse(a2, provider);
                aste = Convert.ToInt32(a[0]);
            } catch(Exception e)
            {
                return null;
            }
            double d2 = Math.Round(d / 60, 6, MidpointRounding.ToEven) + aste;
            
            return d2.ToString().Replace(",", ".");
         }
    }
}
