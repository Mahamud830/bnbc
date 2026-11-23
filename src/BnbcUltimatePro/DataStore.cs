using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BnbcUltimatePro
{
    internal class Town { public string Name; public double Z; }
    internal class SoilRow { public string Name; public double S, TB, TC, TD; }

    internal static class DataStore
    {
        public static List<Town> Towns = new List<Town>();
        public static List<SoilRow> Soils = new List<SoilRow>();

        // BNBC 2020 Part 6, Table 6.2.17 (PDF p.619)
        public static readonly string[] Occupancies = { "I, II", "III", "IV" };
        public static double ImportanceFactor(string occ)
        {
            switch (occ) { case "I, II": return 1.00; case "III": return 1.25; case "IV": return 1.50; }
            return double.NaN;
        }

        static double D(string s) { return double.Parse(s, CultureInfo.InvariantCulture); }

        public static void Load()
        {
            Towns = Rows("seismic_towns.csv").Select(r => new Town { Name = r[0], Z = D(r[1]) }).OrderBy(t => t.Name).ToList();
            Soils = Rows("soil_spectrum.csv").Select(r => new SoilRow { Name = r[0], S = D(r[1]), TB = D(r[2]), TC = D(r[3]), TD = D(r[4]) }).ToList();
        }

        static List<string[]> Rows(string resource)
        {
            var list = new List<string[]>();
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            using (var r = new StreamReader(s))
            {
                r.ReadLine();
                string l;
                while ((l = r.ReadLine()) != null)
                    if (l.Trim().Length > 0) list.Add(l.Split(','));
            }
            return list;
        }
    }
}
