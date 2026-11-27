using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BnbcUltimatePro
{
    public class WindStoryRow
    {
        public string Story { get; set; }
        public string Elevation { get; set; } // m above ground
    }

    internal class ExposureRow { public string Name; public double Alpha, Zg; public override string ToString() { return Name; } }
    internal class WindRow { public string Name; public double Z, Kz, Qz, Pw, Trib, F; }

    /// <summary>
    /// Wind loads on the main wind force resisting system of a rigid enclosed building, BNBC 2020 Part 6
    /// Sec 2.4.11.2 (Eq 6.2.17, 6.2.19). Horizontal force from the wall pressures (internal pressure cancels
    /// between the windward and leeward walls). Roof pressures are not included.
    /// </summary>
    internal static class WindCalc
    {
        // Table 6.2.10 (PDF p.605)
        public static readonly List<ExposureRow> Exposures = new List<ExposureRow>
        {
            new ExposureRow { Name = "Exposure A (urban, suburban, wooded)", Alpha = 7.0, Zg = 365.76 },
            new ExposureRow { Name = "Exposure B (open terrain)", Alpha = 9.5, Zg = 274.32 },
            new ExposureRow { Name = "Exposure C (flat, unobstructed)", Alpha = 11.5, Zg = 213.36 }
        };

        public const double Kd = 0.85;   // Table 6.2.12, buildings MWFRS (PDF p.606)
        public const double G = 0.85;    // Sec 2.4.8.1, rigid structures (PDF p.556)
        public const double CpWindward = 0.8; // Figure 6.2.6 (PDF p.574)

        /// <summary>Kz, Table 6.2.11 note 2 (PDF p.606), Case 2.</summary>
        public static double Kz(double z, ExposureRow e) { return 2.01 * Math.Pow(Math.Max(z, 4.57) / e.Zg, 2.0 / e.Alpha); }

        /// <summary>Eq 6.2.17: kN/m2, V in m/s.</summary>
        public static double Qz(double V, double kz, double kzt, double kd, double I) { return 0.000613 * kz * kzt * kd * V * V * I; }

        /// <summary>Leeward wall Cp, Figure 6.2.6, with linear interpolation of L/B (note 2, PDF p.575).</summary>
        public static double CpLeeward(double lb)
        {
            if (lb <= 1.0) return -0.5;
            if (lb <= 2.0) return -0.5 + (lb - 1.0) * 0.2;
            if (lb < 4.0) return -0.3 + (lb - 2.0) * 0.05;
            return -0.2;
        }

        /// <summary>Table 6.2.9 (PDF p.604). cyclone44 = cyclone-prone region with V above 44 m/s.</summary>
        public static double Importance(string category, bool cyclone44)
        {
            switch (category)
            {
                case "I": return cyclone44 ? 0.77 : 0.87;
                case "II": return 1.00;
                case "III": return 1.15;
                case "IV": return 1.15;
            }
            return double.NaN;
        }

        /// <summary>Forces at story levels. z ascending (m above ground). B normal to the wind, L parallel.</summary>
        public static List<WindRow> Forces(List<string> names, List<double> z, double B, double L, ExposureRow e, double V, double I,
                                           double kzt, out double qh, out double cpLee, out double pLee)
        {
            double h = z[z.Count - 1];
            qh = Qz(V, Kz(h, e), kzt, Kd, I);
            cpLee = CpLeeward(L / B);
            pLee = qh * G * Math.Abs(cpLee);
            var rows = new List<WindRow>();
            for (int i = 0; i < z.Count; i++)
            {
                double zPrev = i == 0 ? 0.0 : z[i - 1];
                double below = (zPrev + z[i]) / 2.0;
                double above = i + 1 < z.Count ? (z[i] + z[i + 1]) / 2.0 : z[i];
                double trib = above - below;
                double kz = Kz(z[i], e);
                double q = Qz(V, kz, kzt, Kd, I);
                double pw = q * G * CpWindward;
                rows.Add(new WindRow { Name = names[i], Z = z[i], Kz = kz, Qz = q, Pw = pw, Trib = trib, F = (pw + pLee) * B * trib });
            }
            return rows;
        }

        public static string Report(string location, double V, string vSource, string category, bool cyc44, double I, ExposureRow e,
                                    double dimX, double dimY, double T, List<string> names, List<double> z,
                                    out List<WindRow> fx, out List<WindRow> fy)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("WIND LOAD - BNBC 2020 Part 6 Sec 2.4 (Method 2, rigid enclosed building)");
            sb.AppendLine(string.Format(inv, "Location: {0}   V = {1} m/s ({2})", location, V, vSource));
            sb.AppendLine(string.Format(inv, "Occupancy category {0}{1}: I = {2:0.00} (Table 6.2.9, PDF p.604)", category, cyc44 ? ", cyclone-prone region V > 44 m/s" : ", non-cyclone / V up to 44 m/s column", I));
            sb.AppendLine(string.Format(inv, "{0}: alpha = {1}, zg = {2} m (Table 6.2.10)", e.Name, e.Alpha, e.Zg));
            sb.AppendLine(string.Format(inv, "Kd = {0} (Table 6.2.12), Kzt = 1.0 (Sec 2.4.7.2, you confirmed the topographic conditions are not met), G = {1} (Sec 2.4.8.1, rigid: T = {2} s)", Kd, G, T));
            sb.AppendLine("qz = 0.000613 Kz Kzt Kd V^2 I (kN/m2)  (Eq 6.2.17);   Kz = 2.01 (z/zg)^(2/alpha) (Table 6.2.11 note 2, z not below 4.57 m)");
            sb.AppendLine("p = q G Cp (Eq 6.2.19). Windward Cp = 0.8 with qz; leeward Cp from L/B with qh (Figure 6.2.6). Internal pressure cancels in the horizontal sum.");
            sb.AppendLine();
            double qh, cp, pl;
            fx = Forces(names, z, dimY, dimX, e, V, I, 1.0, out qh, out cp, out pl);
            sb.AppendLine(string.Format(inv, "WIND ALONG X: B = {0} m (normal), L = {1} m, L/B = {2:0.00}, leeward Cp = {3:0.000}, qh = {4:0.000} kN/m2, leeward pressure = {5:0.000} kN/m2", dimY, dimX, dimX / dimY, cp, qh, pl));
            Table(sb, fx, inv);
            fy = Forces(names, z, dimX, dimY, e, V, I, 1.0, out qh, out cp, out pl);
            sb.AppendLine();
            sb.AppendLine(string.Format(inv, "WIND ALONG Y: B = {0} m (normal), L = {1} m, L/B = {2:0.00}, leeward Cp = {3:0.000}, qh = {4:0.000} kN/m2, leeward pressure = {5:0.000} kN/m2", dimX, dimY, dimY / dimX, cp, qh, pl));
            Table(sb, fy, inv);
            sb.AppendLine();
            sb.AppendLine("NOT included: roof pressures and sloped-roof effects, torsional load cases of Figure 6.2.9, parapets, across-wind and flexible-building effects, partially enclosed buildings.");
            sb.AppendLine("The story forces above are not sent to ETABS automatically. Enter them as user wind loads, or use the exported CSV.");
            return sb.ToString();
        }

        static void Table(StringBuilder sb, List<WindRow> rows, CultureInfo inv)
        {
            sb.AppendLine("Story            z (m)     Kz     qz (kN/m2)  windward p  trib (m)   Force (kN)   Storey shear (kN)");
            for (int i = 0; i < rows.Count; i++)
            {
                double v = 0; for (int j = i; j < rows.Count; j++) v += rows[j].F;
                var r = rows[i];
                sb.AppendLine(string.Format(inv, "{0,-14} {1,7:0.00} {2,7:0.000} {3,11:0.000} {4,11:0.000} {5,9:0.00} {6,12:0.00} {7,15:0.00}", r.Name, r.Z, r.Kz, r.Qz, r.Pw, r.Trib, r.F, v));
            }
            sb.AppendLine(string.Format(inv, "Base shear = {0:0.00} kN", rows.Sum(r => r.F)));
        }
    }
}
