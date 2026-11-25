using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BnbcUltimatePro
{
    internal class Town { public string Name; public double Z; }
    internal class LiveRow { public string Name; public double LL; public string Notes; public int Page; public override string ToString() { return Name; } }
    internal class SysRow { public string Name; public double R, Omega, Cd; public string HB, HC, HD; public int Page; public override string ToString() { return Name; } }
    internal class SoilRow { public string Name; public double S, TB, TC, TD; }

    internal static class DataStore
    {
        public static List<Town> Towns = new List<Town>();
        public static List<SoilRow> Soils = new List<SoilRow>();
        public static List<LiveRow> Lives = new List<LiveRow>();
        public static List<SysRow> Systems = new List<SysRow>();

        // Short pointers to the notes of BNBC 2020 Table 6.2.3 (PDF p.540-541)
        public static readonly Dictionary<string, string> LiveNotes = new Dictionary<string, string>
        {
            {"b", "Garages: use the table value or the concentrated load in note b (13.35 kN jack load, or 10 kN per wheel for mechanical parking)."},
            {"c", "Garages for trucks and buses need an approved method with truck/bus loading."},
            {"d", "Stack-room value applies only to non-mobile double-faced stacks within the height, depth and aisle limits of note d."},
            {"e", "Subject to live load reduction of Sec 2.3.13."},
            {"f", "Valid for floor height up to 3.5 m; for higher floors increase the loads proportionally."},
            {"g", "Also add the horizontal swaying forces for seat rows given in note g."},
            {"h", "See note h on reduced roof live loads and continuity."},
            {"j", "Other truck-loading provisions of an approved method must also be considered."},
            {"l", "Stairs: minimum concentrated load on a tread is 1.33 kN on 2580 mm2."}
        };
        public const string LiveGeneralNote = "Table note a: the average weight of equipment, machinery, materials and products on the floor must be less than the table value, otherwise use the actual probable weight.";

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
            Lives = Rows("live_loads.csv", '|').Select(r => new LiveRow { Name = r[0], LL = D(r[1]), Notes = r[2], Page = int.Parse(r[3], CultureInfo.InvariantCulture) }).ToList();
            Systems = Rows("seismic_systems.csv", '|').Select(r => new SysRow { Name = r[0], R = D(r[1]), Omega = D(r[2]), Cd = D(r[3]), HB = r[4], HC = r[5], HD = r[6], Page = int.Parse(r[7], CultureInfo.InvariantCulture) }).ToList();
            Soils = Rows("soil_spectrum.csv").Select(r => new SoilRow { Name = r[0], S = D(r[1]), TB = D(r[2]), TC = D(r[3]), TD = D(r[4]) }).ToList();
        }

        static List<string[]> Rows(string resource, char sep = ',')
        {
            var list = new List<string[]>();
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            using (var r = new StreamReader(s))
            {
                r.ReadLine();
                string l;
                while ((l = r.ReadLine()) != null)
                    if (l.Trim().Length > 0) list.Add(l.Split(sep));
            }
            return list;
        }
    }
}
