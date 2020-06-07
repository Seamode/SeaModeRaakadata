using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Text;
using System.Xml;

namespace SeaMODEParcerLibrary
{
    public class SeamodeGpxWriter
    {
        private readonly XmlDocument doc;
        private readonly XmlNode rootNode;
        private readonly DateTime metaDataRaceTime;


        public SeamodeGpxWriter(DateTime raceTime)
        {
            doc = new XmlDocument();
            rootNode = doc.CreateElement("gpx");
            XmlAttribute creator = doc.CreateAttribute("creator");
            XmlAttribute xmlns = doc.CreateAttribute("xmlns");
            creator.Value = "Seamode";
            xmlns.Value = "http://www.topografix.com/GPX/1/";
            rootNode.Attributes.Append(creator);
            rootNode.Attributes.Append(xmlns);
            doc.AppendChild(rootNode);

            metaDataRaceTime = raceTime;
        }
        public void WriteGpx(List<GpxLine> gpxLines, string path)
        {
            WriteHeader();
            WriteMetaData();
            // Kirjoitetaan rivit
            XmlNode trk = doc.CreateElement("trk");
            XmlNode trkseq = doc.CreateElement("trkseg");
            foreach (GpxLine gpxLine in gpxLines)
            {
                XmlNode trkpt = doc.CreateElement("trkpt");
                XmlAttribute attrLat = doc.CreateAttribute("lat");
                XmlAttribute attrLon = doc.CreateAttribute("lon");
                // Jos eteläisellä pallonpuoliskolla miinusmerkki eteen
                if(gpxLine.LatPosition == "S")
                    attrLat.Value = "-" + gpxLine.Latitude;
                else
                    attrLat.Value = gpxLine.Latitude;
                // Jos läntisella pallonpuoliskolla miinusmerkki eteen
                if(gpxLine.LongPosition == "W")
                    attrLon.Value = ($"-{gpxLine.Longitude}");
                else
                    attrLon.Value = gpxLine.Longitude;

                trkpt.Attributes.Append(attrLat);
                trkpt.Attributes.Append(attrLon);
                XmlNode time = doc.CreateElement("time");
                // Tämä toisenlaisella muotoilulla
                time.InnerText = gpxLine.EventTime.ToString("s") + "Z";
                trkpt.AppendChild(time);
                // Ohjelma ei osannut lukea tagia
                //XmlNode speed = doc.CreateElement("speed");
                //speed.InnerText = gpxLine.speed.ToString(CultureInfo.InvariantCulture);  
                
                //trkpt.AppendChild(speed);
                trkseq.AppendChild(trkpt);
                
            }
            trk.AppendChild(trkseq);
            rootNode.AppendChild(trk);
            //doc.Save(ConfigurationManager.AppSettings["gpxFile"]);
            doc.Save(path);
        }
        private void WriteHeader()
        {
            XmlDeclaration xmldecl = doc.CreateXmlDeclaration("1.0", "UTF-8", "yes");
            doc.InsertBefore(xmldecl, doc.FirstChild);
            
        }
        private void WriteMetaData()
        {
            XmlNode metaData = doc.CreateElement("metadata");
            XmlNode metaDataLink = doc.CreateElement("link");
            XmlAttribute metadataLinkAttribute = doc.CreateAttribute("href");
            metadataLinkAttribute.Value = "www.baltic-instruments.com";
            metaDataLink.Attributes.Append(metadataLinkAttribute);
            XmlNode metaDataText = doc.CreateElement("text");
            XmlNode metaDataTime = doc.CreateElement("time");
            metaDataText.InnerText = "GPX model trial";
            //metaDataAika.InnerText = "2019-09-28T11:30:43Z";
            metaDataTime.InnerText = GetRaceTimeFormatted();
            metaDataLink.AppendChild(metaDataText);
            metaData.AppendChild(metaDataLink);
            metaData.AppendChild(metaDataTime);
            rootNode.AppendChild(metaData);
        }
        private string GetRaceTimeFormatted()
        {
            return metaDataRaceTime.ToString("s") + "Z";
        }

    }
}
