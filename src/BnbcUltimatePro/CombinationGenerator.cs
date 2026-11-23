using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BnbcUltimatePro
{
    public class ComboRow
    {
        public string Name { get; set; }
        public string Definition { get; set; }
        public string Reference { get; set; }
        public string Status { get; set; }
        public List<KeyValuePair<string, double>> Items = new List<KeyValuePair<string, double>>();
    }

    public class ComboOptions
    {
        public bool Uls, Asd, PlusMinus, LiveFactorHalf, Wind13;
    }

    /// <summary>
    /// Load combinations exactly as printed in BNBC 2020 Part 6 Ch.2:
    /// Sec 2.7.2.1 (allowable stress/strength, PDF p.687) and Sec 2.7.3.1 (strength design, PDF p.688).
    /// Flood / tidal-surge variants (Fa) are NOT generated.
    /// </summary>
    internal static class CombinationGenerator
    {
        class Alt { public double F; public string Sym; }
        class Term { public bool Required; public List<Alt> Alts = new List<Alt>(); }
        class Def { public string Id; public string Ref; public List<Term> Terms = new List<Term>(); }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static Alt A(double f, string s) { return new Alt { F = f, Sym = s }; }
        static Term T(bool req, params Alt[] alts) { var t = new Term { Required = req }; t.Alts.AddRange(alts); return t; }
        static Def D(string id, string reference, params Term[] terms) { var d = new Def { Id = id, Ref = reference }; d.Terms.AddRange(terms); return d; }

        static List<Def> Strength(ComboOptions o)
        {
            double lf = o.LiveFactorHalf ? 0.5 : 1.0;   // Exception 1 (Sec 2.7.3.1)
            double wf = o.Wind13 ? 1.3 : 1.6;           // Exception 3 (Sec 2.7.3.1)
            string r = "BNBC 2020 Sec 2.7.3.1 Eq.";
            return new List<Def>
            {
                D("ULS1", r + "1 (p.688)", T(true, A(1.4, "D")), T(false, A(1.4, "F"))),
                D("ULS2", r + "2 (p.688)", T(true, A(1.2, "D")), T(false, A(1.2, "F")), T(false, A(1.2, "T")), T(false, A(1.6, "L")), T(false, A(1.6, "H")), T(false, A(0.5, "Lr"), A(0.5, "R"))),
                D("ULS3", r + "3 (p.688)", T(true, A(1.2, "D")), T(false, A(1.6, "Lr"), A(1.6, "R")), T(false, A(lf, "L"), A(0.8, "W"))),
                D("ULS4", r + "4 (p.688)", T(true, A(1.2, "D")), T(true, A(wf, "W")), T(false, A(lf, "L")), T(false, A(0.5, "Lr"), A(0.5, "R"))),
                D("ULS5", r + "5 (p.688)", T(true, A(1.2, "D")), T(true, A(1.0, "E")), T(false, A(lf, "L"))),
                D("ULS6", r + "6 (p.688)", T(true, A(0.9, "D")), T(true, A(wf, "W")), T(false, A(1.6, "H"))),
                D("ULS7", r + "7 (p.688)", T(true, A(0.9, "D")), T(true, A(1.0, "E")), T(false, A(1.6, "H")))
            };
        }

        static List<Def> Asd()
        {
            string r = "BNBC 2020 Sec 2.7.2.1 Comb.";
            double e7 = 0.7, e75 = Math.Round(0.75 * 0.7, 6);
            return new List<Def>
            {
                D("ASD1", r + "1 (p.687)", T(true, A(1, "D")), T(false, A(1, "F"))),
                D("ASD2", r + "2 (p.687)", T(true, A(1, "D")), T(false, A(1, "H")), T(false, A(1, "F")), T(false, A(1, "L")), T(false, A(1, "T"))),
                D("ASD3", r + "3 (p.687)", T(true, A(1, "D")), T(false, A(1, "H")), T(false, A(1, "F")), T(false, A(1, "Lr"), A(1, "R"))),
                D("ASD4", r + "4 (p.687)", T(true, A(1, "D")), T(false, A(1, "H")), T(false, A(1, "F")), T(false, A(0.75, "L")), T(false, A(0.75, "T")), T(false, A(1, "Lr"), A(1, "R"))),
                D("ASD5", r + "5 (p.687)", T(true, A(1, "D")), T(false, A(1, "H")), T(false, A(1, "F")), T(true, A(1, "W"), A(e7, "E"))),
                D("ASD6", r + "6 (p.687)", T(true, A(1, "D")), T(false, A(1, "H")), T(false, A(1, "F")), T(true, A(0.75, "W"), A(e75, "E")), T(false, A(0.75, "L")), T(false, A(0.75, "Lr"), A(0.75, "R"))),
                D("ASD7", r + "7 (p.687)", T(true, A(0.6, "D")), T(true, A(1, "W")), T(false, A(1, "H"))),
                D("ASD8", r + "8 (p.687)", T(true, A(0.6, "D")), T(true, A(e7, "E")), T(false, A(1, "H")))
            };
        }

        static KeyValuePair<string, double> KV(string n, double f) { return new KeyValuePair<string, double>(n, f); }

        public static string Describe(List<KeyValuePair<string, double>> items)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                double f = items[i].Value;
                if (i == 0) sb.Append(f < 0 ? "-" : "");
                else sb.Append(f < 0 ? " - " : " + ");
                sb.Append(Math.Abs(f).ToString("0.0##", Inv)).Append(" ").Append(items[i].Key);
            }
            return sb.ToString();
        }

        /// <param name="map">symbol (D,F,H,L,Lr,R,T,W,E) to ETABS load names.
        /// W and E: every name gives its own combinations. Others: all names are summed.</param>
        public static List<ComboRow> Generate(ComboOptions o, Dictionary<string, List<string>> map)
        {
            var result = new List<ComboRow>();
            var seen = new HashSet<string>();
            var defs = new List<Def>();
            if (o.Uls) defs.AddRange(Strength(o));
            if (o.Asd) defs.AddRange(Asd());

            foreach (var d in defs)
            {
                var termChoices = new List<List<List<KeyValuePair<string, double>>>>();
                bool ok = true;
                foreach (var t in d.Terms)
                {
                    var choices = new List<List<KeyValuePair<string, double>>>();
                    foreach (var a in t.Alts)
                    {
                        List<string> names;
                        if (!map.TryGetValue(a.Sym, out names) || names.Count == 0) continue;
                        if (a.Sym == "W" || a.Sym == "E")
                        {
                            foreach (var n in names)
                            {
                                choices.Add(new List<KeyValuePair<string, double>> { KV(n, a.F) });
                                if (o.PlusMinus) choices.Add(new List<KeyValuePair<string, double>> { KV(n, -a.F) });
                            }
                        }
                        else choices.Add(names.Select(n => KV(n, a.F)).ToList());
                    }
                    if (choices.Count == 0) { if (t.Required) { ok = false; break; } continue; }
                    termChoices.Add(choices);
                }
                if (!ok) continue;

                var combos = new List<List<KeyValuePair<string, double>>> { new List<KeyValuePair<string, double>>() };
                foreach (var choices in termChoices)
                {
                    var next = new List<List<KeyValuePair<string, double>>>();
                    foreach (var b in combos)
                        foreach (var c in choices) { var l = new List<KeyValuePair<string, double>>(b); l.AddRange(c); next.Add(l); }
                    combos = next;
                }

                var rows = new List<ComboRow>();
                foreach (var items in combos)
                {
                    string sig = string.Join("|", items.OrderBy(i => i.Key, StringComparer.Ordinal).Select(i => i.Key + ":" + i.Value.ToString("R", Inv)));
                    if (!seen.Add(sig)) continue;
                    rows.Add(new ComboRow { Items = items, Reference = d.Ref, Definition = Describe(items) });
                }
                for (int i = 0; i < rows.Count; i++)
                    rows[i].Name = rows.Count == 1 ? d.Id : d.Id + "_" + (i + 1);
                result.AddRange(rows);
            }
            return result;
        }
    }
}
