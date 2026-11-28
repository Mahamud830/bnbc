using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BnbcUltimatePro
{
    public class StoryRow
    {
        public string Story { get; set; }
        public string Height { get; set; }   // h_x in m from the base
        public string Dead { get; set; }     // kN, includes superimposed dead, walls, permanent equipment
        public string Live { get; set; }     // kN
        public string LiveIntensity { get; set; } // kN/m2, decides the 25% / 50% rule
    }

    internal class PeriodType { public string Name; public double Ct, M; public override string ToString() { return Name; } }

    /// <summary>Equivalent static seismic analysis, BNBC 2020 Part 6 Sec 2.5.4.3 and 2.5.7 (PDF p.615-632).</summary>
    internal static class SeismicCalc
    {
        // Table 6.2.20 (PDF p.631)
        public static readonly List<PeriodType> PeriodTypes = new List<PeriodType>
        {
            new PeriodType { Name = "Concrete moment-resisting frames", Ct = 0.0466, M = 0.9 },
            new PeriodType { Name = "Steel moment-resisting frames", Ct = 0.0724, M = 0.8 },
            new PeriodType { Name = "Eccentrically braced steel frame", Ct = 0.0731, M = 0.75 },
            new PeriodType { Name = "All other structural systems", Ct = 0.0488, M = 0.75 }
        };

        /// <summary>Story forces Fx (kN) of the last successful run, in the order the stories were given.</summary>
        public static List<KeyValuePair<string, double>> LastStoryForces = new List<KeyValuePair<string, double>>();

        public const double Beta = 0.11; // Sec 2.5.4.3, recommended value

        public static double Eta(double xiPercent) { return Math.Max(Math.Sqrt(10.0 / (5.0 + xiPercent)), 0.55); } // Eq 6.2.36

        /// <summary>Normalized spectrum Cs, Eq 6.2.35a-d. NaN above 4 s.</summary>
        public static double Cs(double T, double S, double TB, double TC, double TD, double eta)
        {
            if (T <= TB) return S * (1.0 + T / TB * (2.5 * eta - 1.0));
            if (T <= TC) return 2.5 * S * eta;
            if (T <= TD) return 2.5 * S * eta * (TC / T);
            if (T <= 4.0) return 2.5 * S * eta * (TC * TD / (T * T));
            return double.NaN;
        }

        public static double Sa(double Z, double I, double R, double cs) { return 2.0 / 3.0 * (Z * I / R) * cs; } // Eq 6.2.34
        public static double SaMin(double Z, double I, double S) { return 0.67 * Beta * Z * I * S; }
        public static double Period(double ct, double m, double hn) { return ct * Math.Pow(hn, m); } // Eq 6.2.38

        public static double K(double T) // Sec 2.5.7.4
        {
            if (T <= 0.5) return 1.0;
            if (T >= 2.5) return 2.0;
            return 1.0 + (T - 0.5) / 2.0;
        }

        public static double LiveFraction(double intensity) { return intensity <= 3.0 ? 0.25 : 0.50; } // Sec 2.5.7.3

        public static int ZoneOf(double z)
        {
            if (Math.Abs(z - 0.12) < 1e-6) return 1;
            if (Math.Abs(z - 0.20) < 1e-6) return 2;
            if (Math.Abs(z - 0.28) < 1e-6) return 3;
            if (Math.Abs(z - 0.36) < 1e-6) return 4;
            return 0;
        }

        // Table 6.2.18 (PDF p.620). Rows SA..SE, columns zone 1..4. Occupancy I-III, then IV.
        static readonly Dictionary<string, string> SdcLow = new Dictionary<string, string>
        { {"SA","BCCD"}, {"SB","BCDD"}, {"SC","BCDD"}, {"SD","CDDD"}, {"SE","DDDD"} };
        static readonly Dictionary<string, string> SdcIv = new Dictionary<string, string>
        { {"SA","CDDD"}, {"SB","CDDD"}, {"SC","CDDD"}, {"SD","DDDD"}, {"SE","DDDD"} };

        public static string Sdc(string soil, int zone, bool occupancyIV)
        {
            if (zone < 1 || zone > 4) return null;
            string row;
            return (occupancyIV ? SdcIv : SdcLow).TryGetValue(soil, out row) ? row.Substring(zone - 1, 1) : null;
        }

        public static string HeightLimit(SysRow s, string sdc)
        {
            return sdc == "B" ? s.HB : sdc == "C" ? s.HC : s.HD;
        }

        /// <summary>Full calculation. Returns the report text; fills the numbers used by the tests.</summary>
        public static string Run(double Z, double I, string soil, SoilRow sr, SysRow sys, PeriodType pt, double hn, double? tAnalysis,
                                 bool regular, bool occIV, List<double> h, List<double> w, List<string> names,
                                 out double V, out double Ttrue)
        {
            var inv = CultureInfo.InvariantCulture;
            LastStoryForces = new List<KeyValuePair<string, double>>();
            var sb = new StringBuilder();
            double eta = Eta(5.0);
            double Ta = Period(pt.Ct, pt.M, hn);
            double T = Ta;
            sb.AppendLine("SEISMIC STATIC ANALYSIS - BNBC 2020 Part 6 Sec 2.5.7");
            sb.AppendLine(string.Format(inv, "Z = {0:0.00} (Table 6.2.15)   I = {1:0.00} (Table 6.2.17)   Soil = {2} (your input)", Z, I, soil));
            sb.AppendLine(string.Format(inv, "S = {0}, TB = {1}, TC = {2}, TD = {3} (Table 6.2.16)", sr.S, sr.TB, sr.TC, sr.TD));
            sb.AppendLine(string.Format(inv, "System: {0}", sys.Name));
            sb.AppendLine(string.Format(inv, "R = {0}, Omega = {1}, Cd = {2} (Table 6.2.19, PDF p.{3})", sys.R, sys.Omega, sys.Cd, sys.Page));
            sb.AppendLine();
            sb.AppendLine(string.Format(inv, "Damping 5%: eta = sqrt(10/(5+5)) = {0:0.000} (Eq 6.2.36)", eta));
            sb.AppendLine(string.Format(inv, "Approximate period: Ta = Ct x hn^m = {0} x {1}^{2} = {3:0.000} s (Eq 6.2.38, Table 6.2.20)", pt.Ct, hn, pt.M, Ta));
            if (tAnalysis.HasValue)
            {
                double cap = 1.4 * Ta;
                T = Math.Min(tAnalysis.Value, cap);
                sb.AppendLine(string.Format(inv, "Analysis period {0:0.000} s; limit 1.4 x Ta = {1:0.000} s (Sec 2.5.7.2a). T used = {2:0.000} s", tAnalysis.Value, cap, T));
            }
            else sb.AppendLine(string.Format(inv, "T used = Ta = {0:0.000} s", T));
            Ttrue = T;

            var warnings = new List<string>();
            double cs = Cs(T, sr.S, sr.TB, sr.TC, sr.TD, eta);
            if (double.IsNaN(cs)) { V = double.NaN; sb.AppendLine("T is above 4 s: outside the spectrum of Eq 6.2.35. Stop; use a site-specific or dynamic method."); return sb.ToString(); }
            sb.AppendLine(string.Format(inv, "Cs(T) = {0:0.0000} (Eq 6.2.35a-d)", cs));
            if (I / sys.R > 1.0) warnings.Add("I/R is greater than 1, which Sec 2.5.4.3 does not allow.");
            double sa = Sa(Z, I, sys.R, cs), min = SaMin(Z, I, sr.S);
            sb.AppendLine(string.Format(inv, "Sa = (2/3)(Z I / R) Cs = {0:0.0000} g (Eq 6.2.34)", sa));
            sb.AppendLine(string.Format(inv, "Lower bound 0.67 x beta x Z x I x S = 0.67 x {0} x {1} x {2} x {3} = {4:0.0000} g", Beta, Z, I, sr.S, min));
            double saUsed = Math.Max(sa, min);
            if (min > sa) sb.AppendLine("Lower bound governs.");
            double W = w.Sum();
            V = saUsed * W;
            sb.AppendLine(string.Format(inv, "Seismic weight W = {0:0.0} kN (Sec 2.5.7.3)", W));
            sb.AppendLine(string.Format(inv, "Base shear V = Sa x W = {0:0.0000} x {1:0.0} = {2:0.0} kN (Eq 6.2.37)", saUsed, W, V));
            double k = K(T);
            sb.AppendLine(string.Format(inv, "Distribution exponent k = {0:0.000} (Sec 2.5.7.4)", k));
            double den = 0; for (int i = 0; i < h.Count; i++) den += w[i] * Math.Pow(h[i], k);
            sb.AppendLine("Story            h (m)     w (kN)      Fx (kN)     Storey shear Vx (kN)");
            var fx = new List<double>();
            for (int i = 0; i < h.Count; i++) fx.Add(V * w[i] * Math.Pow(h[i], k) / den);
            for (int i = 0; i < h.Count; i++) LastStoryForces.Add(new KeyValuePair<string, double>(names[i], fx[i]));
            for (int i = 0; i < h.Count; i++)
            {
                double vx = 0; for (int j = 0; j < h.Count; j++) if (h[j] >= h[i]) vx += fx[j];
                sb.AppendLine(string.Format(inv, "{0,-14} {1,8:0.00} {2,10:0.0} {3,12:0.00} {4,14:0.00}", names[i], h[i], w[i], fx[i], vx));
            }
            sb.AppendLine("(Eq 6.2.41 and 6.2.42; storey shear assumes stories sorted by height)");
            sb.AppendLine();
            if (T >= Math.Min(4 * sr.TC, 2.0)) warnings.Add(string.Format(inv, "T = {0:0.000} s is not below min(4 TC, 2 s) = {1:0.00} s: the equivalent static method is not permitted (Sec 2.5.6a). Use response spectrum analysis.", T, Math.Min(4 * sr.TC, 2.0)));
            if (!regular) warnings.Add("Regularity in elevation is not confirmed (Sec 2.5.6b, 2.5.5.3.2). The equivalent static method needs a building without vertical irregularity.");
            int zone = ZoneOf(Z);
            string sdc = Sdc(soil, zone, occIV);
            if (sdc == null) warnings.Add("Seismic design category could not be found (Table 6.2.18).");
            else
            {
                string lim = HeightLimit(sys, sdc);
                sb.AppendLine(string.Format(inv, "Seismic design category {0} (Table 6.2.18, zone {1}). Height limit for this system: {2} (Table 6.2.19).", sdc, zone, lim));
                double lm;
                if (lim == "NP") warnings.Add("This structural system is NOT PERMITTED in seismic design category " + sdc + ".");
                else if (lim != "NL" && double.TryParse(lim, NumberStyles.Float, inv, out lm) && hn > lm) warnings.Add(string.Format(inv, "Building height {0} m exceeds the limit {1} m for this system in category {2}.", hn, lm, sdc));
            }
            if (warnings.Count == 0) sb.AppendLine("CHECKS: no violations found among the checks above. (Irregularity beyond the elevation confirmation, torsion and drift are not checked here.)");
            else foreach (var wmsg in warnings) sb.AppendLine("WARNING: " + wmsg);
            sb.AppendLine();
            sb.AppendLine("NOT included: accidental torsion (Sec 2.5.7.6), drift checks, shear-wall period (Eq 6.2.39), vertical seismic component.");
            return sb.ToString();
        }
    }
}
