using System;
using System.Linq;
using System.Windows;

namespace BnbcUltimatePro
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Contains("--selftest"))
            {
                int code = 1;
                try { code = SelfTest.Run() ? 0 : 1; } catch { code = 2; }
                Environment.Exit(code);
                return;
            }
            base.OnStartup(e);
        }
    }

    internal static class SelfTest
    {
        public static bool Run()
        {
            DataStore.Load();
            bool ok = DataStore.Towns.Count == 66;
            var dhaka = DataStore.Towns.FirstOrDefault(t => t.Name == "Dhaka");
            ok &= dhaka != null && Math.Abs(dhaka.Z - 0.20) < 1e-9;
            var sylhet = DataStore.Towns.FirstOrDefault(t => t.Name == "Sylhet");
            ok &= sylhet != null && Math.Abs(sylhet.Z - 0.36) < 1e-9;
            ok &= DataStore.Soils.Count == 5;
            var sc = DataStore.Soils.FirstOrDefault(s => s.Name == "SC");
            ok &= sc != null && Math.Abs(sc.S - 1.15) < 1e-9 && Math.Abs(sc.TC - 0.60) < 1e-9;
            var map = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>
            {
                {"D", new System.Collections.Generic.List<string>{"DEAD","SDL"}}, {"F", new System.Collections.Generic.List<string>()},
                {"H", new System.Collections.Generic.List<string>()}, {"L", new System.Collections.Generic.List<string>{"LIVE"}},
                {"Lr", new System.Collections.Generic.List<string>()}, {"R", new System.Collections.Generic.List<string>()},
                {"T", new System.Collections.Generic.List<string>()}, {"W", new System.Collections.Generic.List<string>{"WX"}},
                {"E", new System.Collections.Generic.List<string>{"EX"}}
            };
            var u = CombinationGenerator.Generate(new ComboOptions { Uls = true, PlusMinus = true }, map);
            var a = CombinationGenerator.Generate(new ComboOptions { Asd = true, PlusMinus = true }, map);
            ok &= u.Count == 13 && a.Count == 15;
            ok &= a.Any(r => r.Items.Any(i => i.Key == "WX" && Math.Abs(i.Value - 0.75) < 1e-9));
            ok &= a.Any(r => r.Items.Any(i => i.Key == "EX" && Math.Abs(i.Value + 0.525) < 1e-9));
            ok &= u.Any(r => r.Name.StartsWith("ULS3") && r.Items.Any(i => i.Key == "WX" && Math.Abs(i.Value - 0.8) < 1e-9));
            ok &= DataStore.Lives.Count == 64;
            var off = DataStore.Lives.FirstOrDefault(l => l.Name == "Office - Offices");
            ok &= off != null && Math.Abs(off.LL - 2.40) < 1e-9;
            var stack = DataStore.Lives.FirstOrDefault(l => l.Name == "Libraries - Stack rooms");
            ok &= stack != null && Math.Abs(stack.LL - 7.20) < 1e-9 && stack.Notes == "d";
            ok &= DataStore.Lives.Count(l => l.Name.ToLowerInvariant().Contains("residential")) == 6;
            ok &= Math.Abs(WallCalc.LineLoad(18.9, 0.125, 3.0, 0, 0, 0) - 7.0875) < 1e-9;
            ok &= Math.Abs(WallCalc.LineLoad(18.9, 0.125, 3.0, 0.5, 2, 0.1) - (18.9 * 0.125 + 1.0) * 3.0 * 0.9) < 1e-9;
            ok &= DataStore.Systems.Count == 28;
            ok &= SeismicCalc.Sdc("SC", 2, false) == "C" && SeismicCalc.Sdc("SD", 1, false) == "C" && SeismicCalc.Sdc("SB", 3, true) == "D";
            var sys = DataStore.Systems.First(x => x.Name.Contains("Intermediate reinforced concrete moment"));
            ok &= sys.R == 5.0;
            var hs = new System.Collections.Generic.List<double> { 3, 6, 9, 12, 15, 18 };
            var ws = new System.Collections.Generic.List<double> { 1075, 1075, 1075, 1075, 1075, 1075 };
            var nm = new System.Collections.Generic.List<string> { "S1", "S2", "S3", "S4", "S5", "S6" };
            double vv, tt;
            SeismicCalc.Run(0.20, 1.0, "SC", sc, sys, SeismicCalc.PeriodTypes[0], 18.0, null, true, false, hs, ws, nm, out vv, out tt);
            ok &= Math.Abs(vv - 472.26566793718496) < 1e-4 && Math.Abs(tt - 0.6282480818391045) < 1e-9;
            ok &= DataStore.WindLocs.Count == 74;
            var dh = DataStore.WindLocs.FirstOrDefault(x => x.Name == "Dhaka");
            ok &= dh != null && Math.Abs(dh.V - 65.7) < 1e-9;
            var eB = WindCalc.Exposures[1];
            ok &= Math.Abs(WindCalc.Kz(30.5, eB) - 1.26) < 0.01 && Math.Abs(WindCalc.Kz(9.1, eB) - 0.98) < 0.01;
            ok &= Math.Abs(WindCalc.Kz(4.0, WindCalc.Exposures[0]) - 0.57) < 0.01 && Math.Abs(WindCalc.Kz(61.0, WindCalc.Exposures[2]) - 1.61) < 0.01;
            ok &= Math.Abs(WindCalc.CpLeeward(1.5) + 0.4) < 1e-9 && Math.Abs(WindCalc.CpLeeward(3.0) + 0.25) < 1e-9 && WindCalc.CpLeeward(5) == -0.2;
            ok &= WindCalc.Importance("I", false) == 0.87 && WindCalc.Importance("I", true) == 0.77 && WindCalc.Importance("IV", false) == 1.15;
            var wz = new System.Collections.Generic.List<double> { 3, 6, 9, 12, 15, 18 };
            var wn = new System.Collections.Generic.List<string> { "S1", "S2", "S3", "S4", "S5", "S6" };
            double qh0, cp0, pl0;
            var wf = WindCalc.Forces(wn, wz, 20.0, 30.0, eB, 65.7, 1.0, 1.0, out qh0, out cp0, out pl0);
            ok &= Math.Abs(wf.Sum(r => r.F) - 783.5165631014145) < 1e-3;
            var rs = new System.Collections.Generic.List<ReportSheet>();
            var r1 = new ReportSheet("Project"); r1.Add("Item", "Value"); r1.Add("Name", "A & B <test>"); r1.Add("Z", "0.20"); rs.Add(r1);
            var r2 = new ReportSheet("Text", true); r2.AddLines("line one\nline two"); rs.Add(r2);
            var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bnbc_selftest_" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                ReportBuilder.WriteXlsx(tmp, rs);
                using (var zip = System.IO.Compression.ZipFile.OpenRead(tmp))
                {
                    ok &= zip.GetEntry("xl/workbook.xml") != null && zip.GetEntry("xl/worksheets/sheet1.xml") != null && zip.GetEntry("xl/worksheets/sheet2.xml") != null;
                    using (var rd = new System.IO.StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml").Open()))
                    {
                        var x = rd.ReadToEnd();
                        ok &= x.Contains("A &amp; B &lt;test&gt;") && x.Contains("<v>0.2</v>");
                    }
                }
            }
            finally { try { System.IO.File.Delete(tmp); } catch { } }
            var html = ReportBuilder.Html("T", rs);
            ok &= html.Contains("<table>") && html.Contains("A &amp; B &lt;test&gt;") && html.Contains("<pre>line one");
            ok &= ComplianceRules.ClassifyWarning("This structural system is NOT PERMITTED in seismic design category D.") == "FAIL";
            ok &= ComplianceRules.ClassifyWarning("Building height 60 m exceeds the limit 50 m for this system in category D.") == "FAIL";
            ok &= ComplianceRules.ClassifyWarning("Regularity in elevation is not confirmed (Sec 2.5.6b).") == "INCOMPLETE";
            ok &= ComplianceRules.ClassifyWarning("I/R is greater than 1.") == "WARN";
            ok &= ComplianceRules.ClassifyWarning("Period is above 4 s: outside the code spectrum, so no result was produced.") == "INCOMPLETE";
            ok &= ComplianceRules.Worst(new[] { "PASS", "WARN", "FAIL" }) == "FAIL";
            ok &= ComplianceRules.Worst(new[] { "PASS", "NOT CHECKED" }) == "WARN";
            ok &= ComplianceRules.Worst(new[] { "PASS", "PASS" }) == "PASS";
            return ok;
        }
    }
}
