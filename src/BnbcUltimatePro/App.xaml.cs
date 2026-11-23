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
            return ok;
        }
    }
}
