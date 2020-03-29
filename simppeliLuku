using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace myApp
{
    class Program
    {
        static void Main(string[] args)
        {
            string end = "</CalibrationData>";
            int loc = end.Length + 1;
            string[] files = Directory.GetFiles("/home/tero/Documents/dat", "*.csv");
            StringBuilder sb = new StringBuilder();
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                if (sb.Length > 0){
                    text = text.Substring(text.IndexOf(end) + loc);
                    text = text.Substring(text.IndexOf('\n') + 1);
                }
                sb.Append(text);
            }
            Console.WriteLine("Saving race file...");
            string fn = files[0].Substring(files[0].LastIndexOf('/') + 1);
            fn = String.Join("_Kisa.", fn.Split('.'));
            string path = $"/home/tero/Documents/dat/{fn}";
            File.WriteAllText(path, sb.ToString());
        }
    }
}
