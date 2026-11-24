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
            return ok;
        }
    }
}
