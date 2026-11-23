using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace BnbcUltimatePro
{
    public partial class MainWindow : Window
    {
        readonly EtabsConnector etabs = new EtabsConnector();
        readonly ObservableCollection<string> log = new ObservableCollection<string>();
        readonly ObservableCollection<PatternSpec> patterns = new ObservableCollection<PatternSpec>();
        readonly string[] pageNames = { "PageDashboard", "PageProject", "PageSeismic", "PageLoads" };
        static readonly string[] PatternTypes = { "Dead", "SuperDead", "Live", "ReduceLive", "RoofLive", "Quake", "Wind", "Snow", "Other" };
        bool loading;

        public MainWindow()
        {
            InitializeComponent();
            DataStore.Load();
            LogList.ItemsSource = log;
            CmbTown.ItemsSource = DataStore.Towns.Select(t => t.Name).ToList();
            CmbSoil.ItemsSource = DataStore.Soils.Select(s => s.Name).ToList();
            CmbOcc.ItemsSource = DataStore.Occupancies;
            ((DataGridComboBoxColumn)LoadGrid.Columns[1]).ItemsSource = PatternTypes;
            LoadGrid.ItemsSource = patterns;
            patterns.Add(new PatternSpec { Name = "DEAD", Type = "Dead", SelfWeight = 1, Status = "not checked" });
            patterns.Add(new PatternSpec { Name = "SDL", Type = "SuperDead", SelfWeight = 0, Status = "not checked" });
            patterns.Add(new PatternSpec { Name = "LIVE", Type = "Live", SelfWeight = 0, Status = "not checked" });
            Log("INFO", "Ready. Press 'Detect / Connect ETABS'.");
            etabs.Detect();
            ShowStatus();
            RefreshDashboard();
        }

        void Log(string level, string msg)
        {
            log.Insert(0, DateTime.Now.ToString("HH:mm:ss") + " [" + level + "] " + msg);
        }

        void ShowStatus()
        {
            StatusDot.Fill = new SolidColorBrush(etabs.Connected ? Color.FromRgb(34, 197, 94) : etabs.IsRunning ? Color.FromRgb(234, 179, 8) : Color.FromRgb(239, 68, 68));
            StatusText.Text = "ETABS: " + etabs.Message + (etabs.DllPath != null ? "   [API: " + etabs.DllPath + "]" : "");
        }

        void Nav_Click(object sender, RoutedEventArgs e)
        {
            var tag = (string)((Button)sender).Tag;
            foreach (var n in pageNames)
                ((UIElement)FindName(n)).Visibility = n == tag ? Visibility.Visible : Visibility.Collapsed;
            if (tag == "PageDashboard") RefreshDashboard();
        }

        void Connect_Click(object sender, RoutedEventArgs e)
        {
            bool ok = etabs.Connect();
            ShowStatus();
            Log(ok ? "INFO" : "ERROR", etabs.Message);
        }

        // ---------- project ----------
        ProjectInfo ReadProject()
        {
            return new ProjectInfo
            {
                Name = TxtName.Text, Town = CmbTown.SelectedItem as string, Soil = CmbSoil.SelectedItem as string,
                Occupancy = CmbOcc.SelectedItem as string, Stories = TxtStories.Text, Notes = TxtNotes.Text,
                Patterns = patterns.ToList()
            };
        }

        void Field_Changed(object sender, RoutedEventArgs e)
        {
            if (loading || !IsLoaded) return;
            var town = DataStore.Towns.FirstOrDefault(t => t.Name == (CmbTown.SelectedItem as string));
            TxtZ.Text = town == null ? "" : town.Z.ToString("0.00", CultureInfo.InvariantCulture);
            RefreshDashboard();
        }

        void RefreshDashboard()
        {
            var p = ReadProject();
            int stories; 
            var sb = new StringBuilder();
            sb.AppendLine((string.IsNullOrWhiteSpace(p.Name) ? "[MISSING]" : "[OK]     ") + " Project name");
            sb.AppendLine((p.Town == null ? "[MISSING]" : "[OK]     ") + " Location / seismic zone");
            sb.AppendLine((p.Soil == null ? "[MISSING]" : "[OK]     ") + " Soil type (user input from geotechnical report)");
            sb.AppendLine((p.Occupancy == null ? "[MISSING]" : "[OK]     ") + " Occupancy category");
            sb.AppendLine((int.TryParse(p.Stories, out stories) && stories > 0 ? "[OK]     " : "[MISSING]") + " Number of stories");
            TxtCompleteness.Text = sb.ToString();
        }

        void Save_Click(object sender, RoutedEventArgs e)
        {
            var d = new SaveFileDialog { Filter = "BNBC project (*.bnbc.json)|*.bnbc.json", FileName = "project.bnbc.json" };
            if (d.ShowDialog() != true) return;
            try { ProjectInfo.Save(d.FileName, ReadProject()); Log("INFO", "Saved " + d.FileName); }
            catch (Exception ex) { Log("ERROR", "Save failed: " + ex.Message); }
        }

        void Load_Click(object sender, RoutedEventArgs e)
        {
            var d = new OpenFileDialog { Filter = "BNBC project (*.bnbc.json)|*.bnbc.json" };
            if (d.ShowDialog() != true) return;
            try
            {
                var p = ProjectInfo.Load(d.FileName);
                loading = true;
                TxtName.Text = p.Name; TxtStories.Text = p.Stories; TxtNotes.Text = p.Notes;
                CmbTown.SelectedItem = p.Town; CmbSoil.SelectedItem = p.Soil; CmbOcc.SelectedItem = p.Occupancy;
                patterns.Clear();
                foreach (var x in p.Patterns ?? new List<PatternSpec>()) { x.Status = "not checked"; patterns.Add(x); }
                loading = false;
                Field_Changed(null, null);
                Log("INFO", "Loaded " + d.FileName);
            }
            catch (Exception ex) { loading = false; Log("ERROR", "Load failed: " + ex.Message); }
        }

        // ---------- seismic ----------
        string BuildSeismicText()
        {
            var p = ReadProject();
            var sb = new StringBuilder();
            var missing = new List<string>();
            sb.AppendLine("PROJECT: " + (string.IsNullOrWhiteSpace(p.Name) ? "(not set)" : p.Name));
            sb.AppendLine("Reference: BNBC 2020, Part 6, Chapter 2 (PDF supplied by user)");
            sb.AppendLine();

            var town = DataStore.Towns.FirstOrDefault(t => t.Name == p.Town);
            if (town == null) { missing.Add("Location"); sb.AppendLine("Z : MISSING (select location)"); }
            else sb.AppendLine("Z = " + town.Z.ToString("0.00", CultureInfo.InvariantCulture) + "   [Table 6.2.15, town: " + town.Name + "; zones in Table 6.2.14]");

            double I = DataStore.ImportanceFactor(p.Occupancy);
            if (double.IsNaN(I)) { missing.Add("Occupancy category"); sb.AppendLine("I : MISSING (select occupancy category)"); }
            else sb.AppendLine("I = " + I.ToString("0.00", CultureInfo.InvariantCulture) + "   [Table 6.2.17, category " + p.Occupancy + "]");

            var soil = DataStore.Soils.FirstOrDefault(s => s.Name == p.Soil);
            if (soil == null) { missing.Add("Soil type"); sb.AppendLine("Soil: MISSING (enter from geotechnical report; not assumed)"); }
            else
            {
                sb.AppendLine("Soil = " + soil.Name + "   (user-supplied)");
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "S = {0}, TB = {1} s, TC = {2} s, TD = {3} s   [Table 6.2.16]", soil.S, soil.TB, soil.TC, soil.TD));
            }
            sb.AppendLine();
            if (missing.Count > 0)
                sb.AppendLine("STATUS: INCOMPLETE - missing: " + string.Join(", ", missing) + ". No PASS can be given.");
            else
                sb.AppendLine("STATUS: Parameters complete. (Values are table look-ups only.)");
            sb.AppendLine();
            sb.AppendLine("NOT YET IMPLEMENTED: response spectrum ordinates, R factor, base shear, drift checks.");
            sb.AppendLine("Assumptions: Z, I, S, TB, TC, TD were read from the BNBC 2020 tables; soil type and occupancy category are user inputs.");
            return sb.ToString();
        }

        void SeismicPreview_Click(object sender, RoutedEventArgs e) { TxtSeismic.Text = BuildSeismicText(); }

        void Export_Click(object sender, RoutedEventArgs e)
        {
            var d = new SaveFileDialog { Filter = "Text report (*.txt)|*.txt", FileName = "BNBC_report.txt" };
            if (d.ShowDialog() != true) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("BNBC ULTIMATE PRO - Calculation Report (MVP 0.1)");
                sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                sb.AppendLine(new string('-', 60));
                sb.Append(BuildSeismicText());
                sb.AppendLine(new string('-', 60));
                sb.AppendLine("Load patterns planned:");
                foreach (var x in patterns) sb.AppendLine("  " + x.Name + " | " + x.Type + " | SW=" + x.SelfWeight.ToString(CultureInfo.InvariantCulture));
                File.WriteAllText(d.FileName, sb.ToString(), Encoding.UTF8);
                Log("INFO", "Report exported: " + d.FileName);
            }
            catch (Exception ex) { Log("ERROR", "Export failed: " + ex.Message); }
        }

        // ---------- load patterns ----------
        void AddRow_Click(object sender, RoutedEventArgs e)
        {
            patterns.Add(new PatternSpec { Name = "NEW", Type = "Other", SelfWeight = 0, Status = "not checked" });
        }

        bool EnsureModel()
        {
            if (!etabs.HasModel)
            {
                Log("ERROR", "Connect to ETABS 22 with a model open first.");
                return false;
            }
            return true;
        }

        void LoadPreview_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureModel()) return;
            try
            {
                var existing = new HashSet<string>(etabs.GetLoadPatternNames(), StringComparer.OrdinalIgnoreCase);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in patterns)
                {
                    if (string.IsNullOrWhiteSpace(p.Name)) p.Status = "ERROR: empty name";
                    else if (!seen.Add(p.Name.Trim())) p.Status = "ERROR: duplicate in list";
                    else if (existing.Contains(p.Name.Trim())) p.Status = "EXISTS - will skip";
                    else p.Status = "NEW - will create";
                }
                LoadGrid.Items.Refresh();
                Log("INFO", "Preview done. ETABS has " + existing.Count + " load pattern(s).");
            }
            catch (Exception ex) { Log("ERROR", "Preview failed: " + ex.Message); }
        }

        void CreatePatterns_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureModel()) return;
            LoadPreview_Click(null, null);
            var todo = patterns.Where(p => p.Status == "NEW - will create").ToList();
            if (patterns.Any(p => p.Status != null && p.Status.StartsWith("ERROR")))
            { Log("ERROR", "Fix the rows marked ERROR before creating."); return; }
            if (todo.Count == 0) { Log("INFO", "Nothing to create."); return; }
            var msg = "Create " + todo.Count + " load pattern(s) in ETABS?\n\n" + string.Join("\n", todo.Select(p => p.Name + " (" + p.Type + ")"));
            if (MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            foreach (var p in todo)
            {
                try
                {
                    int ret = etabs.AddLoadPattern(p.Name.Trim(), p.Type, p.SelfWeight);
                    p.Status = ret == 0 ? "CREATED" : "FAILED (code " + ret + ")";
                    Log(ret == 0 ? "INFO" : "ERROR", p.Name + ": " + p.Status);
                }
                catch (Exception ex)
                {
                    var inner = ex.InnerException ?? ex;
                    p.Status = "FAILED"; Log("ERROR", p.Name + ": " + inner.Message);
                }
            }
            LoadGrid.Items.Refresh();
        }
    }
}
