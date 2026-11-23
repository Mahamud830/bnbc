using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BnbcUltimatePro
{
    /// <summary>
    /// Talks to ETABS through ETABSv1.dll, which is located in the user's own ETABS
    /// installation and loaded at runtime by reflection. No CSI binaries are redistributed.
    /// </summary>
    internal sealed class EtabsConnector
    {
        public string DllPath;
        public bool IsRunning;
        public bool Connected;
        public string Version = "";
        public string ModelFile = "";
        public string Message = "Not checked yet";

        Assembly asm;
        object model;
        Type modelI;

        public void Detect()
        {
            DllPath = null; IsRunning = false;
            try
            {
                foreach (var p in Process.GetProcessesByName("ETABS"))
                {
                    IsRunning = true;
                    try
                    {
                        var f = Path.Combine(Path.GetDirectoryName(p.MainModule.FileName), "ETABSv1.dll");
                        if (File.Exists(f)) { DllPath = f; break; }
                    }
                    catch { }
                }
            }
            catch { }

            if (DllPath == null)
            {
                var roots = new[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)") };
                foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r)))
                {
                    var cs = Path.Combine(root, "Computers and Structures");
                    if (!Directory.Exists(cs)) continue;
                    foreach (var d in Directory.GetDirectories(cs, "ETABS*").OrderByDescending(x => x))
                    {
                        var f = Path.Combine(d, "ETABSv1.dll");
                        if (File.Exists(f)) { DllPath = f; break; }
                    }
                    if (DllPath != null) break;
                }
            }

            if (DllPath == null)
                Message = "ETABS installation (ETABSv1.dll) not found. Install ETABS 22 first.";
            else if (!IsRunning)
                Message = "ETABS found but not running. Open ETABS 22 and your model, then press Connect.";
            else
                Message = "ETABS is running. Press Connect.";
        }

        public bool Connect()
        {
            Connected = false; Version = ""; ModelFile = "";
            Detect();
            if (DllPath == null || !IsRunning) return false;
            try
            {
                asm = Assembly.LoadFrom(DllPath);
                var helperT = asm.GetType("ETABSv1.Helper", true);
                var helperI = asm.GetType("ETABSv1.cHelper", true);
                var helper = Activator.CreateInstance(helperT);
                var obj = helperI.GetMethod("GetObject").Invoke(helper, new object[] { "CSI.ETABS.API.ETABSObject" });
                if (obj == null) { Message = "Could not attach to the running ETABS instance."; return false; }
                var oapiI = asm.GetType("ETABSv1.cOAPI", true);
                model = oapiI.GetProperty("SapModel").GetValue(obj, null);
                modelI = asm.GetType("ETABSv1.cSapModel", true);

                var args = new object[] { null, 0.0 };
                modelI.GetMethod("GetVersion").Invoke(model, args);
                Version = (args[0] as string) ?? "";
                if (!Version.StartsWith("22"))
                {
                    Message = "API Version Mismatch: ETABS reports version '" + Version + "', this tool requires 22.x.";
                    return false;
                }
                ModelFile = (modelI.GetMethod("GetModelFilename").Invoke(model, new object[] { true }) as string) ?? "";
                Connected = true;
                Message = ModelFile.Length == 0
                    ? "Connected (ETABS " + Version + "), but no model is open. Open or create a model."
                    : "Connected to ETABS " + Version + " - model: " + ModelFile;
                return true;
            }
            catch (Exception ex)
            {
                var e = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                Message = "Connection failed: " + e.Message;
                return false;
            }
        }

        public bool HasModel { get { return Connected && ModelFile.Length > 0; } }

        object LoadPatterns()
        {
            return modelI.GetProperty("LoadPatterns").GetValue(model, null);
        }

        public List<string> GetLoadPatternNames()
        {
            var lp = LoadPatterns();
            var lpI = asm.GetType("ETABSv1.cLoadPatterns", true);
            var args = new object[] { 0, null };
            int ret = (int)lpI.GetMethod("GetNameList").Invoke(lp, args);
            if (ret != 0) throw new Exception("GetNameList returned " + ret);
            var names = args[1] as string[];
            return names == null ? new List<string>() : names.ToList();
        }

        public int AddLoadPattern(string name, string type, double selfWeight)
        {
            var lp = LoadPatterns();
            var lpI = asm.GetType("ETABSv1.cLoadPatterns", true);
            var enumT = asm.GetType("ETABSv1.eLoadPatternType", true);
            var val = Enum.Parse(enumT, type);
            return (int)lpI.GetMethod("Add").Invoke(lp, new object[] { name, val, selfWeight, true });
        }
    }
}
