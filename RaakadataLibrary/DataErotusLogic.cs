using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaakadataLibrary
{
    public static class DataErotusLogic
    {
        /// <summary>
        /// Erottaa tiedoston datasta xml ja headerin osuuden. Palauttaa raakadatan.
        /// </summary>
        /// <param name="raakaTeksti">Koko teidoston sisältämä data string muodossa.</param>
        /// <returns>Palauttaa raakadatan string muodossa.</returns>
        public static string RaakaDatanErotus(this string raakaTeksti)
        {
            string xmlLoppu = "</CalibrationData>\r\n";
            raakaTeksti = raakaTeksti.Substring(raakaTeksti.IndexOf(xmlLoppu) + xmlLoppu.Length);
            raakaTeksti = raakaTeksti.Substring(raakaTeksti.IndexOf("\r\n") + 2);
            return raakaTeksti;
        }

        /// <summary>
        /// Erottaa tiedoston datasta xml osuuden. Mahdollista jättää headeri palautettuun raakadataan.
        /// </summary>
        /// <param name="raakaTeksti">Koko teidoston sisältämä data string muodossa.</param>
        /// <param name="headerinPoisto">Aseta true, jos headeriä ei haluta dataan.</param>
        /// <returns>Palauttaa raakadatan string muodossa.</returns>
        public static string RaakaDatanErotus(this string raakaTeksti, bool headerinPoisto)
        {
            string xmlLoppu = "</CalibrationData>\r\n";
            // raakadata headearin kanssa
            raakaTeksti = raakaTeksti.Substring(raakaTeksti.IndexOf(xmlLoppu) + xmlLoppu.Length);
            // jos headeria ei haluta mukaan dataan
            if (headerinPoisto)
            {
                raakaTeksti = raakaTeksti.Substring(raakaTeksti.IndexOf("\r\n") + 2);
            }
            return raakaTeksti;
        }
    }
}
