using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        readonly string[] pageNames = { "PageDashboard", "PageProject", "PageSeismic", "PageLoads", "PageCombos", "PageFloors", "PageReact", "PageSeis", "PageWind", "PageCheck", "PageComp", "PageReport" };
        static readonly string[] PatternTypes = { "Dead", "SuperDead", "Live", "ReduceLive", "RoofLive", "Quake", "Wind", "Snow", "Other" };
        bool loading;
        readonly ObservableCollection<MapRow> mapRows = new ObservableCollection<MapRow>();
        readonly ObservableCollection<ComboRow> comboRows = new ObservableCollection<ComboRow>();
        readonly ObservableCollection<CheckRow> checks = new ObservableCollection<CheckRow>();
        readonly ObservableCollection<CheckRow> compliance = new ObservableCollection<CheckRow>();
        bool seisDone, windDone;
        List<string> seisWarnings = new List<string>();
        readonly ObservableCollection<WindStoryRow> windRows = new ObservableCollection<WindStoryRow>();
        List<WindRow> lastFx, lastFy;
        readonly ObservableCollection<CheckRow> checks = new ObservableCollection<CheckRow>();
        readonly ObservableCollection<StoryRow> seisRows = new ObservableCollection<StoryRow>();
        readonly ObservableCollection<FloorRow> floors = new ObservableCollection<FloorRow>();
        readonly ObservableCollection<EtabsConnector.ReactionRow> reactions = new ObservableCollection<EtabsConnector.ReactionRow>();

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
            mapRows.Add(new MapRow { Symbol = "D", Description = "Dead load (self weight, superimposed dead, walls)", Patterns = "DEAD, SDL, WALL" });
            mapRows.Add(new MapRow { Symbol = "F", Description = "Load due to fluids", Patterns = "" });
            mapRows.Add(new MapRow { Symbol = "H", Description = "Lateral earth / soil pressure load", Patterns = "" });
            mapRows.Add(new MapRow { Symbol = "L", Description = "Live load", Patterns = "LIVE" });
            mapRows.Add(new MapRow { Symbol = "Lr", Description = "Roof live load", Patterns = "" });
            mapRows.Add(new MapRow { Symbol = "R", Description = "Rain load", Patterns = "" });
            mapRows.Add(new MapRow { Symbol = "T", Description = "Self-straining load (temperature, shrinkage, etc.)", Patterns = "" });
            mapRows.Add(new MapRow { Symbol = "W", Description = "Wind load (each name = separate set)", Patterns = "" });
            mapRows.Add(new MapRow { Symbol = "E", Description = "Earthquake load effect (each name = separate set)", Patterns = "" });
            MapGrid.ItemsSource = mapRows;
            ComboGrid.ItemsSource = comboRows;
            LstOcc.ItemsSource = DataStore.Lives.Select(l => l.Name).ToList();
            ReactGrid.ItemsSource = reactions;
            SeisGrid.ItemsSource = seisRows;
            WindGrid.ItemsSource = windRows;
            CheckGrid.ItemsSource = checks;
            CompGrid.ItemsSource = compliance;
            CheckGrid.ItemsSource = checks;
            CmbWindLoc.ItemsSource = DataStore.WindLocs;
            CmbWindCat.ItemsSource = new[] { "I", "II", "III", "IV" };
            CmbWindRegion.ItemsSource = new[] { "Non-cyclone region, or cyclone-prone with V = 38-44 m/s", "Cyclone-prone region with V above 44 m/s" };
            CmbExposure.ItemsSource = WindCalc.Exposures;
            CmbSys.ItemsSource = DataStore.Systems;
            CmbPeriod.ItemsSource = SeismicCalc.PeriodTypes;
            FloorGrid.ItemsSource = floors;
            FloorGrid.CellEditEnding += (s2, e2) => Dispatcher.BeginInvoke(new Action(() => FloorGrid_SelectionChanged(null, null)));
            CmbWallMat.ItemsSource = WallCalc.Materials;
            CmbWallMat.SelectedIndex = 0;
            TxtGamma.Text = "18.9";
            TxtFinish.Text = "0"; TxtFaces.Text = "0"; TxtOpen.Text = "0";
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
            if (tag == "PageReport") RefreshReportInfo();
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

        // ---------- load combinations ----------
        Dictionary<string, List<string>> ReadMap()
        {
            var map = new Dictionary<string, List<string>>();
            foreach (var r in mapRows)
            {
                var names = (r.Patterns ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                map[r.Symbol] = names;
            }
            return map;
        }

        void ComboPreview_Click(object sender, RoutedEventArgs e)
        {
            MapGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var o = new ComboOptions
            {
                Uls = ChkUls.IsChecked == true, Asd = ChkAsd.IsChecked == true, PlusMinus = ChkPm.IsChecked == true,
                LiveFactorHalf = ChkL05.IsChecked == true, Wind13 = Chk13W.IsChecked == true
            };
            if (!o.Uls && !o.Asd) { Log("ERROR", "Select at least one method (strength or allowable stress)."); return; }
            var map = ReadMap();
            if (map["D"].Count == 0) { Log("ERROR", "Dead load (D) has no ETABS load name. No combination can be made."); return; }
            var rows = CombinationGenerator.Generate(o, map);
            comboRows.Clear();
            List<string> cases = null, existing = null;
            if (etabs.HasModel)
            {
                try { cases = etabs.GetLoadCaseNames(); existing = etabs.GetComboNames(); }
                catch (Exception ex) { Log("WARN", "Could not read ETABS cases/combinations: " + (ex.InnerException ?? ex).Message); }
            }
            foreach (var r in rows)
            {
                if (!etabs.HasModel) r.Status = "not checked (connect ETABS)";
                else if ((existing != null && existing.Contains(r.Name, StringComparer.OrdinalIgnoreCase)) || etabs.ComboExists(r.Name))
                    r.Status = "EXISTS - will skip";
                else if (cases == null) r.Status = "NEW - cases not verified";
                else
                {
                    var missing = r.Items.Select(i => i.Key).Distinct().Where(n => !cases.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
                    r.Status = missing.Count > 0 ? "MISSING CASE: " + string.Join(",", missing) : "NEW - will create";
                }
                comboRows.Add(r);
            }
            if (rows.Count == 0) Log("WARN", "No combinations generated. Required loads (for example W or E) are not mapped.");
            else Log("INFO", rows.Count + " combination(s) generated. Check each one before creating.");
            foreach (var sym in new[] { "W", "E" })
                if (map[sym].Count == 0) Log("WARN", "No ETABS name for " + sym + ": combinations that need " + sym + " were not generated.");
        }

        void ComboCreate_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureModel()) return;
            ComboPreview_Click(null, null);
            var todo = comboRows.Where(c => c.Status != null && c.Status.StartsWith("NEW")).ToList();
            var blocked = comboRows.Where(c => c.Status != null && c.Status.StartsWith("MISSING")).ToList();
            if (blocked.Count > 0) Log("ERROR", blocked.Count + " combination(s) use load names that do not exist in ETABS and will NOT be created.");
            if (todo.Count == 0) { Log("INFO", "Nothing to create."); return; }
            var msg = "Create " + todo.Count + " load combination(s) in ETABS?\n\nFirst items:\n" + string.Join("\n", todo.Take(8).Select(c => c.Name + ": " + c.Definition));
            if (MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            foreach (var c in todo)
            {
                try
                {
                    int ret = etabs.AddCombo(c.Name, c.Items);
                    if (ret == 0) c.Status = "CREATED";
                    else if (etabs.ComboExists(c.Name)) { c.Status = "EXISTS - already in ETABS (code " + ret + ")"; Log("WARN", c.Name + ": already exists in ETABS, left unchanged."); }
                    else { c.Status = "FAILED (code " + ret + ")"; Log("ERROR", c.Name + ": " + c.Status); }
                }
                catch (Exception ex) { c.Status = "FAILED"; Log("ERROR", c.Name + ": " + (ex.InnerException ?? ex).Message); }
            }
            ComboGrid.Items.Refresh();
            Log("INFO", "Done: " + todo.Count(c => c.Status == "CREATED") + " created, " + todo.Count(c => c.Status.StartsWith("EXISTS")) + " already existed, " + todo.Count(c => c.Status.StartsWith("FAILED")) + " failed.");
        }

        // ---------- floor live loads ----------
        void AddFloor_Click(object sender, RoutedEventArgs e) { floors.Add(new FloorRow { Floor = "Story" + (floors.Count + 1) }); }

        void ReadStories_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureModel()) return;
            try
            {
                var names = etabs.GetStoryNames();
                if (names == null || names.Count == 0) { Log("ERROR", "Could not read stories from ETABS. Add floors by hand."); return; }
                foreach (var n in names)
                    if (!floors.Any(f => string.Equals(f.Floor, n, StringComparison.OrdinalIgnoreCase)))
                        floors.Add(new FloorRow { Floor = n });
                Log("INFO", "Stories read from ETABS: " + string.Join(", ", names));
            }
            catch (Exception ex) { Log("ERROR", "Read stories failed: " + (ex.InnerException ?? ex).Message); }
        }

        void FloorGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var f = FloorGrid.SelectedItem as FloorRow;
            var row = f == null ? null : DataStore.Lives.FirstOrDefault(l => l.Name == f.Occupancy);
            if (row == null) { TxtLiveNote.Text = ""; return; }
            var notes = (row.Notes ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(n => DataStore.LiveNotes.ContainsKey(n.Trim()) ? "Note " + n.Trim() + ": " + DataStore.LiveNotes[n.Trim()] : "").Where(x => x.Length > 0).ToList();
            TxtLiveNote.Text = "Source: BNBC 2020 Table 6.2.3 (PDF p." + row.Page + "). " + DataStore.LiveGeneralNote + (notes.Count > 0 ? "\n" + string.Join("\n", notes) : "");
        }

        void AssignLive_Click(object sender, RoutedEventArgs e)
        {
            FloorGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (!EnsureModel()) return;
            var f = FloorGrid.SelectedItem as FloorRow;
            var row = f == null ? null : DataStore.Lives.FirstOrDefault(l => l.Name == f.Occupancy);
            if (row == null) { Log("ERROR", "Select a floor row that has an occupancy chosen."); return; }
            string pat = TxtLivePat.Text.Trim();
            try
            {
                if (!etabs.GetLoadPatternNames().Contains(pat, StringComparer.OrdinalIgnoreCase)) { Log("ERROR", "Load pattern '" + pat + "' does not exist in ETABS."); return; }
                int areas, frames; etabs.GetSelectionCounts(out areas, out frames);
                if (areas == 0) { Log("ERROR", "No slabs (area objects) are selected in ETABS."); return; }
                var msg = "Assign " + row.LL.ToString("0.00", CultureInfo.InvariantCulture) + " kN/m2 (" + row.Name + ") from floor '" + f.Floor + "' to " + areas + " selected area object(s) under pattern " + pat + "?\n\nAn existing uniform load of this pattern on them will be replaced.";
                if (MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                int ret = etabs.AssignAreaUniform(pat, row.LL);
                Log(ret == 0 ? "INFO" : "ERROR", ret == 0 ? "Live load " + row.LL.ToString("0.00", CultureInfo.InvariantCulture) + " kN/m2 assigned to " + areas + " area(s), floor " + f.Floor + "." : "Assign failed (code " + ret + "). Is the model unlocked?");
            }
            catch (Exception ex) { Log("ERROR", "Assign failed: " + (ex.InnerException ?? ex).Message); }
        }


        // ---------- occupancy search ----------
        void OccFilter_Changed(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            var tokens = (TxtOccFilter.Text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.ToLowerInvariant()).ToList();
            LstOcc.ItemsSource = DataStore.Lives.Select(l => l.Name)
                .Where(n => tokens.All(t => n.ToLowerInvariant().Contains(t))).ToList();
        }

        void SetOcc_Click(object sender, RoutedEventArgs e)
        {
            var occ = LstOcc.SelectedItem as string;
            if (occ == null) { Log("ERROR", "Pick an occupancy from the list first."); return; }
            var rows = FloorGrid.SelectedItems.Cast<object>().OfType<FloorRow>().ToList();
            if (rows.Count == 0) { Log("ERROR", "Select one or more floor rows in the table first."); return; }
            foreach (var r in rows) r.Occupancy = occ;
            FloorGrid_SelectionChanged(null, null);
            Log("INFO", "Occupancy set for " + rows.Count + " floor(s): " + occ);
        }

        void LstOcc_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (LstOcc.SelectedItem != null) SetOcc_Click(null, null);
        }

        // ---------- foundation reactions ----------
        void Extract_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureModel()) return;
            try
            {
                if (etabs.CountSelected(1) == 0) { Log("ERROR", "No joints are selected in ETABS. Select the base support joints first."); return; }
                List<string> combos = (TxtReactCombos.Text ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                if (combos.Count == 0)
                    combos = etabs.GetComboNames().Where(n => n.StartsWith("ULS", StringComparison.OrdinalIgnoreCase) || n.StartsWith("ASD", StringComparison.OrdinalIgnoreCase)).ToList();
                if (combos.Count == 0) { Log("ERROR", "No combinations to use. Create them first or type their names."); return; }
                int code;
                var rows = etabs.GetJointReactions(combos, out code);
                if (code != 0) { Log("ERROR", "ETABS returned code " + code + ". Has the analysis been run, and do these combinations exist?"); return; }
                reactions.Clear();
                foreach (var r in rows) reactions.Add(r);
                var sb = new StringBuilder();
                sb.AppendLine("Joints: " + rows.Select(r => r.Joint).Distinct().Count() + "   Combinations: " + rows.Select(r => r.Case).Distinct().Count() + "   Rows: " + rows.Count);
                foreach (var g in rows.GroupBy(r => r.Joint))
                {
                    var best = g.OrderByDescending(r => r.F3).First();
                    sb.AppendLine("Joint " + g.Key + ": max F3 = " + best.F3.ToString("0.00", CultureInfo.InvariantCulture) + " kN (combo " + best.Case + ")");
                }
                foreach (var g in rows.GroupBy(r => r.Case))
                    sb.AppendLine("Total F3, " + g.Key + " = " + g.Sum(r => r.F3).ToString("0.00", CultureInfo.InvariantCulture) + " kN");
                TxtReactSummary.Text = sb.ToString();
                Log("INFO", "Reactions extracted: " + rows.Count + " row(s).");
            }
            catch (Exception ex) { Log("ERROR", "Extract failed: " + (ex.InnerException ?? ex).Message); }
        }

        void ExportReact_Click(object sender, RoutedEventArgs e)
        {
            if (reactions.Count == 0) { Log("ERROR", "Nothing to export. Extract first."); return; }
            var d = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "foundation_reactions.csv" };
            if (d.ShowDialog() != true) return;
            try
            {
                var sb = new StringBuilder("Joint,Combo,Fx_kN,Fy_kN,Fz_kN,Mx_kNm,My_kNm,Mz_kNm\n");
                foreach (var r in reactions)
                    sb.AppendLine(string.Join(",", new[] { r.Joint, r.Case }.Concat(new[] { r.F1, r.F2, r.F3, r.M1, r.M2, r.M3 }.Select(v => v.ToString("0.000", CultureInfo.InvariantCulture)))));
                File.WriteAllText(d.FileName, sb.ToString(), Encoding.UTF8);
                Log("INFO", "Exported " + d.FileName);
            }
            catch (Exception ex) { Log("ERROR", "Export failed: " + ex.Message); }
        }


        // ---------- seismic static analysis ----------
        void SeisAdd_Click(object sender, RoutedEventArgs e) { seisRows.Add(new StoryRow { Story = "Story" + (seisRows.Count + 1) }); }

        void SeisRead_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureModel()) return;
            try
            {
                var list = etabs.GetStoryElevations();
                if (list == null || list.Count == 0) { Log("ERROR", "Could not read story elevations from ETABS. Enter them by hand."); return; }
                double baseElev = list.Min(x => x.Value);
                foreach (var kv in list.OrderBy(x => x.Value))
                {
                    double h = kv.Value - baseElev;
                    if (h <= 0) continue;
                    if (seisRows.Any(r => string.Equals(r.Story, kv.Key, StringComparison.OrdinalIgnoreCase))) continue;
                    seisRows.Add(new StoryRow { Story = kv.Key, Height = h.ToString("0.###", CultureInfo.InvariantCulture) });
                }
                Log("INFO", "Stories read. Heights are measured from the lowest level (" + baseElev.ToString("0.###", CultureInfo.InvariantCulture) + " m). Enter weights yourself.");
            }
            catch (Exception ex) { Log("ERROR", "Read stories failed: " + (ex.InnerException ?? ex).Message); }
        }

        void SeisCalc_Click(object sender, RoutedEventArgs e)
        {
            SeisGrid.CommitEdit(DataGridEditingUnit.Row, true);
            seisDone = false; seisWarnings = new List<string>();
            var p = ReadProject();
            var missing = new List<string>();
            var town = DataStore.Towns.FirstOrDefault(t => t.Name == p.Town);
            var soil = DataStore.Soils.FirstOrDefault(x => x.Name == p.Soil);
            double I = DataStore.ImportanceFactor(p.Occupancy);
            var sys = CmbSys.SelectedItem as SysRow;
            var pt = CmbPeriod.SelectedItem as PeriodType;
            double hn; double tan; double? tAnalysis = null;
            if (town == null) missing.Add("Location (Project Setup)");
            if (soil == null) missing.Add("Soil type (Project Setup)");
            if (double.IsNaN(I)) missing.Add("Occupancy category (Project Setup)");
            if (sys == null) missing.Add("Structural system");
            if (pt == null) missing.Add("Structure type for the period");
            if (!Num(TxtHn.Text, out hn) || hn <= 0) missing.Add("Building height hn");
            if (!string.IsNullOrWhiteSpace(TxtTan.Text)) { if (Num(TxtTan.Text, out tan) && tan > 0) tAnalysis = tan; else missing.Add("Analysis period (or leave blank)"); }
            var h = new List<double>(); var w = new List<double>(); var names = new List<string>();
            var wlines = new StringBuilder();
            if (seisRows.Count == 0) missing.Add("Story data");
            foreach (var r in seisRows)
            {
                double hh, d, l, inten;
                if (!Num(r.Height, out hh) || hh <= 0 || !Num(r.Dead, out d) || d < 0 || !Num(r.Live, out l) || l < 0 || !Num(r.LiveIntensity, out inten) || inten < 0)
                { missing.Add("Story data for " + r.Story + " (height, dead, live, live intensity)"); continue; }
                double f = SeismicCalc.LiveFraction(inten);
                h.Add(hh); names.Add(r.Story); w.Add(d + f * l);
                wlines.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0}: w = {1} + {2} x {3} = {4:0.0} kN", r.Story, d, f, l, d + f * l));
            }
            if (missing.Count > 0)
            {
                TxtSeisResult.Text = "INCOMPLETE - missing or invalid:\n  - " + string.Join("\n  - ", missing) + "\nNo result is given.";
                Log("ERROR", "Seismic calculation incomplete: " + missing.Count + " item(s) missing.");
                return;
            }
            double V, T;
            string rep = SeismicCalc.Run(town.Z, I, soil.Name, soil, sys, pt, hn, tAnalysis, ChkRegular.IsChecked == true, p.Occupancy == "IV", h, w, names, out V, out T);
            TxtSeisResult.Text = "Seismic weights (Sec 2.5.7.3):\n" + wlines + "\n" + rep;
            seisWarnings = rep.Replace("\r", "").Split('\n').Where(ln => ln.StartsWith("WARNING:")).Select(ln => ln.Substring(8).Trim()).ToList();
            seisDone = !double.IsNaN(V);
            if (!seisDone) seisWarnings.Add("Period is above 4 s: outside the code spectrum, so no result was produced.");
            Log("INFO", "Seismic static analysis done. V = " + V.ToString("0.0", CultureInfo.InvariantCulture) + " kN, T = " + T.ToString("0.000", CultureInfo.InvariantCulture) + " s.");
        }

        void SeisExport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSeisResult.Text)) { Log("ERROR", "Calculate first."); return; }
            var d = new SaveFileDialog { Filter = "Text report (*.txt)|*.txt", FileName = "seismic_static_report.txt" };
            if (d.ShowDialog() != true) return;
            try { File.WriteAllText(d.FileName, "BNBC ULTIMATE PRO - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n\n" + TxtSeisResult.Text, Encoding.UTF8); Log("INFO", "Exported " + d.FileName); }
            catch (Exception ex) { Log("ERROR", "Export failed: " + ex.Message); }
        }


        // ---------- wind load ----------
        void WindLoc_Changed(object sender, SelectionChangedEventArgs e)
        {
            var loc = CmbWindLoc.SelectedItem as WindLoc;
            if (loc != null) TxtWindV.Text = loc.V.ToString("0.0", CultureInfo.InvariantCulture);
        }

        void WindAdd_Click(object sender, RoutedEventArgs e) { windRows.Add(new WindStoryRow { Story = "Story" + (windRows.Count + 1) }); }

        void WindCopy_Click(object sender, RoutedEventArgs e)
        {
            int n = 0;
            foreach (var r in seisRows)
            {
                if (string.IsNullOrWhiteSpace(r.Story) || string.IsNullOrWhiteSpace(r.Height)) continue;
                if (windRows.Any(w => string.Equals(w.Story, r.Story, StringComparison.OrdinalIgnoreCase))) continue;
                windRows.Add(new WindStoryRow { Story = r.Story, Elevation = r.Height }); n++;
            }
            Log("INFO", n + " story level(s) copied. Check that the heights are measured from ground level.");
        }

        void WindRead_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureModel()) return;
            try
            {
                var list = etabs.GetStoryElevations();
                if (list == null || list.Count == 0) { Log("ERROR", "Could not read story elevations from ETABS. Enter them by hand."); return; }
                double baseElev = list.Min(x => x.Value);
                foreach (var kv in list.OrderBy(x => x.Value))
                {
                    double z = kv.Value - baseElev;
                    if (z <= 0) continue;
                    if (windRows.Any(w => string.Equals(w.Story, kv.Key, StringComparison.OrdinalIgnoreCase))) continue;
                    windRows.Add(new WindStoryRow { Story = kv.Key, Elevation = z.ToString("0.###", CultureInfo.InvariantCulture) });
                }
                Log("INFO", "Stories read. Elevations are measured from the lowest level (" + baseElev.ToString("0.###", CultureInfo.InvariantCulture) + " m); confirm this is ground level.");
            }
            catch (Exception ex) { Log("ERROR", "Read stories failed: " + (ex.InnerException ?? ex).Message); }
        }

        void WindCalc_Click(object sender, RoutedEventArgs e)
        {
            WindGrid.CommitEdit(DataGridEditingUnit.Row, true);
            windDone = false;
            var missing = new List<string>();
            var loc = CmbWindLoc.SelectedItem as WindLoc;
            string cat = CmbWindCat.SelectedItem as string;
            var exp = CmbExposure.SelectedItem as ExposureRow;
            double V, dx, dy, T;
            if (!Num(TxtWindV.Text, out V) || V <= 0) missing.Add("Basic wind speed V");
            if (CmbWindCat.SelectedItem == null) missing.Add("Occupancy category");
            if (CmbWindRegion.SelectedIndex < 0) missing.Add("Region type for the importance factor");
            if (exp == null) missing.Add("Exposure category");
            if (!Num(TxtDimX.Text, out dx) || dx <= 0) missing.Add("Plan dimension along X");
            if (!Num(TxtDimY.Text, out dy) || dy <= 0) missing.Add("Plan dimension along Y");
            if (!Num(TxtWindT.Text, out T) || T <= 0) missing.Add("Fundamental period T");
            if (ChkWindScope.IsChecked != true) missing.Add("Confirmation of Sec 2.4.3.1 limits");
            if (ChkNoTopo.IsChecked != true) missing.Add("Confirmation that topographic effects do not apply (Sec 2.4.7.1)");
            var pairs = new List<KeyValuePair<string, double>>();
            foreach (var r in windRows)
            {
                double z;
                if (!Num(r.Elevation, out z) || z <= 0) { missing.Add("Elevation of " + r.Story); continue; }
                pairs.Add(new KeyValuePair<string, double>(r.Story, z));
            }
            if (pairs.Count == 0) missing.Add("Story levels");
            if (missing.Count > 0)
            {
                TxtWindResult.Text = "INCOMPLETE - missing or invalid:\n  - " + string.Join("\n  - ", missing) + "\nNo result is given.";
                Log("ERROR", "Wind calculation incomplete: " + missing.Count + " item(s).");
                return;
            }
            if (T > 1.0)
            {
                TxtWindResult.Text = "T = " + T.ToString("0.###", CultureInfo.InvariantCulture) + " s is above 1 s (frequency below 1 Hz): the building is flexible (Sec 2.1.3). The gust-effect factor of Eq 6.2.8-6.2.16 is not implemented here, so no result is given.";
                Log("ERROR", "Flexible building: wind calculation not available.");
                return;
            }
            pairs = pairs.OrderBy(x => x.Value).ToList();
            bool cyc = CmbWindRegion.SelectedIndex == 1;
            double I = WindCalc.Importance(cat, cyc);
            string src = loc != null && Math.Abs(loc.V - V) < 1e-9 ? "Table 6.2.8, PDF p.603" : "user entered";
            string locName = loc != null ? loc.Name : "(not selected)";
            List<WindRow> fx, fy;
            TxtWindResult.Text = WindCalc.Report(locName, V, src, cat, cyc, I, exp, dx, dy, T, pairs.Select(x => x.Key).ToList(), pairs.Select(x => x.Value).ToList(), out fx, out fy);
            lastFx = fx; lastFy = fy; windDone = true;
            Log("INFO", "Wind load done. Base shear X = " + fx.Sum(r => r.F).ToString("0.0", CultureInfo.InvariantCulture) + " kN, Y = " + fy.Sum(r => r.F).ToString("0.0", CultureInfo.InvariantCulture) + " kN.");
        }

        void WindCsv_Click(object sender, RoutedEventArgs e)
        {
            if (lastFx == null || lastFy == null) { Log("ERROR", "Calculate first."); return; }
            var d = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "wind_story_forces.csv" };
            if (d.ShowDialog() != true) return;
            try
            {
                var sb = new StringBuilder("Story,Elevation_m,Fx_kN_windX,Fy_kN_windY\n");
                for (int i = 0; i < lastFx.Count; i++)
                    sb.AppendLine(string.Join(",", new[] { lastFx[i].Name, lastFx[i].Z.ToString("0.###", CultureInfo.InvariantCulture), lastFx[i].F.ToString("0.000", CultureInfo.InvariantCulture), lastFy[i].F.ToString("0.000", CultureInfo.InvariantCulture) }));
                File.WriteAllText(d.FileName, sb.ToString(), Encoding.UTF8);
                Log("INFO", "Exported " + d.FileName);
            }
            catch (Exception ex) { Log("ERROR", "Export failed: " + ex.Message); }
        }

        void WindTxt_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtWindResult.Text)) { Log("ERROR", "Calculate first."); return; }
            var d = new SaveFileDialog { Filter = "Text report (*.txt)|*.txt", FileName = "wind_report.txt" };
            if (d.ShowDialog() != true) return;
            try { File.WriteAllText(d.FileName, "BNBC ULTIMATE PRO - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n\n" + TxtWindResult.Text, Encoding.UTF8); Log("INFO", "Exported " + d.FileName); }
            catch (Exception ex) { Log("ERROR", "Export failed: " + ex.Message); }
        }


        // ---------- model checker ----------
        void AddCheck(string name, string status, string details) { checks.Add(new CheckRow { Check = name, Status = status, Details = details }); }

        void RunChecks_Click(object sender, RoutedEventArgs e)
        {
            MapGrid.CommitEdit(DataGridEditingUnit.Row, true);
            checks.Clear();

            // project data (works without ETABS)
            var p = ReadProject();
            int stories;
            var miss = new List<string>();
            if (string.IsNullOrWhiteSpace(p.Name)) miss.Add("project name");
            if (p.Town == null) miss.Add("location");
            if (p.Soil == null) miss.Add("soil type");
            if (p.Occupancy == null) miss.Add("occupancy category");
            if (!int.TryParse(p.Stories, out stories) || stories <= 0) miss.Add("number of stories");
            AddCheck("Project data", miss.Count == 0 ? "PASS" : "INCOMPLETE", miss.Count == 0 ? "All project fields are filled." : "Missing: " + string.Join(", ", miss));

            if (!etabs.HasModel)
            {
                AddCheck("ETABS model", "FAIL", "Not connected, or no model is open. Connect to ETABS 22 first.");
                FinishChecks();
                return;
            }
            AddCheck("ETABS model", "PASS", etabs.Message);

            try
            {
                bool locked = etabs.IsLocked();
                AddCheck("Model lock", locked ? "WARN" : "PASS", locked ? "Model is locked (analysis results exist). Loads and combinations cannot be changed until you unlock it." : "Model is unlocked and can be edited.");
            }
            catch (Exception ex) { AddCheck("Model lock", "UNKNOWN", Inner(ex)); }

            try
            {
                int pts = etabs.CountObjects("PointObj", "ETABSv1.cPointObj");
                int frs = etabs.CountObjects("FrameObj", "ETABSv1.cFrameObj");
                int ars = etabs.CountObjects("AreaObj", "ETABSv1.cAreaObj");
                string det = pts + " joints, " + frs + " frame objects, " + ars + " area objects.";
                AddCheck("Model objects", frs + ars == 0 ? "FAIL" : (ars == 0 ? "WARN" : "PASS"), frs + ars == 0 ? "The model has no frame or area objects. " + det : (ars == 0 ? "No area objects (slabs, walls). " + det : det));
            }
            catch (Exception ex) { AddCheck("Model objects", "UNKNOWN", Inner(ex)); }

            try
            {
                var st = etabs.GetStoryNames();
                if (st == null) AddCheck("Stories", "UNKNOWN", "Could not read stories.");
                else AddCheck("Stories", st.Count > 0 ? "PASS" : "FAIL", st.Count + " level(s): " + string.Join(", ", st));
            }
            catch (Exception ex) { AddCheck("Stories", "UNKNOWN", Inner(ex)); }

            List<string> cases = null;
            try { cases = etabs.GetLoadCaseNames(); } catch (Exception ex) { AddCheck("Load cases", "UNKNOWN", Inner(ex)); }
            if (cases != null)
            {
                var map = ReadMap();
                var absent = new List<string>();
                int used = 0;
                foreach (var kv in map)
                    foreach (var n in kv.Value) { used++; if (!cases.Contains(n, StringComparer.OrdinalIgnoreCase)) absent.Add(kv.Key + ": " + n); }
                if (used == 0) AddCheck("Load names of the combination page", "INCOMPLETE", "No ETABS load names are entered on the Load Combinations page.");
                else AddCheck("Load names of the combination page", absent.Count == 0 ? "PASS" : "FAIL", absent.Count == 0 ? used + " name(s) all exist as ETABS load cases." : "Not found in ETABS: " + string.Join(", ", absent));
            }

            try
            {
                var combos = etabs.GetComboNames();
                int n2 = combos.Count(c => c.StartsWith("ULS", StringComparison.OrdinalIgnoreCase) || c.StartsWith("ASD", StringComparison.OrdinalIgnoreCase));
                AddCheck("BNBC combinations (ULS / ASD)", n2 > 0 ? "PASS" : "WARN", n2 > 0 ? n2 + " combination(s) made by this tool exist. (Their factors are not re-verified here.)" : "None found. Create them on the Load Combinations page.");
            }
            catch (Exception ex) { AddCheck("BNBC combinations (ULS / ASD)", "UNKNOWN", Inner(ex)); }

            try
            {
                string pat = TxtLivePat.Text.Trim();
                var areas = etabs.GetAreaNames();
                if (areas == null || areas.Count == 0) AddCheck("Live load on slabs", "NOT CHECKED", "No area objects to check.");
                else if (areas.Count > 3000) AddCheck("Live load on slabs", "NOT CHECKED", areas.Count + " area objects: too many to scan here.");
                else
                {
                    int withLoad = etabs.CountAreasWithUniformLoad(pat, areas);
                    if (withLoad < 0) AddCheck("Live load on slabs", "UNKNOWN", "Could not read area loads from ETABS.");
                    else if (withLoad == areas.Count) AddCheck("Live load on slabs", "PASS", "All " + areas.Count + " area objects have a uniform load in pattern " + pat + ".");
                    else AddCheck("Live load on slabs", "WARN", (areas.Count - withLoad) + " of " + areas.Count + " area objects have no uniform load in pattern " + pat + ". Walls and openings may be fine; slabs are not.");
                }
            }
            catch (Exception ex) { AddCheck("Live load on slabs", "UNKNOWN", Inner(ex)); }

            AddCheck("Mass source", "NOT CHECKED", "Define > Mass Source must match the seismic weight (dead + 25% or 50% of live). This tool does not read it.");
            AddCheck("Soil, structural system, diaphragms, irregularity", "NOT CHECKED", "These need your engineering judgement and are not read from the model.");
            FinishChecks();
        }

        static string Inner(Exception ex) { return (ex.InnerException ?? ex).Message; }

        void FinishChecks()
        {
            var g = checks.GroupBy(c => c.Status).ToDictionary(x => x.Key, x => x.Count());
            Func<string, int> n = k => g.ContainsKey(k) ? g[k] : 0;
            TxtCheckSummary.Text = "PASS " + n("PASS") + "   WARN " + n("WARN") + "   FAIL " + n("FAIL") + "   INCOMPLETE " + n("INCOMPLETE") + "   UNKNOWN " + n("UNKNOWN") + "   NOT CHECKED " + n("NOT CHECKED");
            Log(n("FAIL") + n("INCOMPLETE") > 0 ? "WARN" : "INFO", "Model check finished: " + TxtCheckSummary.Text);
        }

        void CheckExport_Click(object sender, RoutedEventArgs e)
        {
            if (checks.Count == 0) { Log("ERROR", "Run the checks first."); return; }
            var d = new SaveFileDialog { Filter = "Text report (*.txt)|*.txt", FileName = "model_check_report.txt" };
            if (d.ShowDialog() != true) return;
            try
            {
                var sb = new StringBuilder("BNBC ULTIMATE PRO - Model check - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n" + TxtCheckSummary.Text + "\n\n");
                foreach (var c in checks) sb.AppendLine("[" + c.Status + "] " + c.Check + ": " + c.Details);
                File.WriteAllText(d.FileName, sb.ToString(), Encoding.UTF8);
                Log("INFO", "Exported " + d.FileName);
            }
            catch (Exception ex) { Log("ERROR", "Export failed: " + ex.Message); }
        }


        // ---------- model checker ----------
        void AddCheck(string name, string result, string detail) { checks.Add(new CheckRow { Check = name, Status = result, Details = detail }); }

        void RunChecks_Click(object sender, RoutedEventArgs e)
        {
            checks.Clear();
            var p = ReadProject();
            int tmp;
            AddCheck("Project name", string.IsNullOrWhiteSpace(p.Name) ? "INCOMPLETE" : "PASS", string.IsNullOrWhiteSpace(p.Name) ? "Enter it in Project Setup." : p.Name);
            AddCheck("Location and seismic zone", p.Town == null ? "INCOMPLETE" : "PASS", p.Town == null ? "Choose a location in Project Setup." : p.Town + " (Table 6.2.15)");
            AddCheck("Soil type", p.Soil == null ? "INCOMPLETE" : "PASS", p.Soil == null ? "Enter the soil type from your geotechnical report. It is never assumed." : p.Soil + " (user input)");
            AddCheck("Occupancy category", p.Occupancy == null ? "INCOMPLETE" : "PASS", p.Occupancy == null ? "Choose it in Project Setup." : p.Occupancy);
            AddCheck("Number of stories", int.TryParse(p.Stories, out tmp) && tmp > 0 ? "PASS" : "INCOMPLETE", int.TryParse(p.Stories, out tmp) && tmp > 0 ? p.Stories : "Enter it in Project Setup.");

            if (!etabs.HasModel)
            {
                AddCheck("ETABS connection and model", "FAIL", "Connect to ETABS 22 with a model open. All ETABS checks below were skipped.");
                Summarize(); return;
            }
            AddCheck("ETABS connection and model", "PASS", "ETABS " + etabs.Version + ", model " + etabs.ModelFile);
            try { AddCheck("Analysis status", etabs.IsModelLocked() ? "PASS" : "WARN", etabs.IsModelLocked() ? "Model is locked: an analysis has been run." : "Model is unlocked: no analysis results yet. Reactions and results need an analysis run."); }
            catch (Exception ex) { AddCheck("Analysis status", "NOT CHECKED", Msg(ex)); }

            int pts = -1, frs = -1, ars = -1;
            try { pts = etabs.ObjectCount("PointObj", "ETABSv1.cPointObj"); frs = etabs.ObjectCount("FrameObj", "ETABSv1.cFrameObj"); ars = etabs.ObjectCount("AreaObj", "ETABSv1.cAreaObj"); }
            catch (Exception ex) { AddCheck("Objects in the model", "NOT CHECKED", Msg(ex)); }
            if (pts >= 0)
                AddCheck("Objects in the model", frs + ars > 0 ? "PASS" : "FAIL", pts + " joints, " + frs + " frames, " + ars + " areas." + (frs + ars == 0 ? " The model is empty." : ""));

            try
            {
                var st = etabs.GetStoryNames();
                int expected; bool haveExpected = int.TryParse(p.Stories, out expected) && expected > 0;
                if (st == null) AddCheck("Stories", "NOT CHECKED", "Story list could not be read.");
                else
                {
                    int levels = Math.Max(st.Count - 1, 0); // ETABS lists the Base level too
                    string res = !haveExpected ? "WARN" : levels == expected ? "PASS" : "WARN";
                    AddCheck("Stories", res, st.Count + " levels listed (" + levels + " above the base)." + (haveExpected ? " Project Setup says " + expected + "." : " Number of stories not entered in Project Setup."));
                }
            }
            catch (Exception ex) { AddCheck("Stories", "NOT CHECKED", Msg(ex)); }

            try { int n = etabs.DiaphragmCount(); AddCheck("Diaphragms", n > 0 ? "PASS" : "WARN", n > 0 ? n + " diaphragm definition(s)." : "No diaphragm is defined. Rigid-floor assumptions of the equivalent static method then do not hold."); }
            catch (Exception ex) { AddCheck("Diaphragms", "NOT CHECKED", Msg(ex)); }

            try
            {
                int total; int rest = etabs.RestrainedPointCount(out total);
                AddCheck("Support restraints", rest > 0 ? "PASS" : "FAIL", rest > 0 ? rest + " of " + total + " joints have restraints." : "No joint has a restraint: the model is unsupported.");
            }
            catch (Exception ex) { AddCheck("Support restraints", "NOT CHECKED", Msg(ex)); }

            try
            {
                var existing = new HashSet<string>(etabs.GetLoadPatternNames(), StringComparer.OrdinalIgnoreCase);
                var wanted = new List<string>();
                foreach (var r in patterns) if (!string.IsNullOrWhiteSpace(r.Name)) wanted.Add(r.Name.Trim());
                foreach (var r in mapRows) foreach (var n in (r.Patterns ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)) wanted.Add(n.Trim());
                var missing = wanted.Distinct(StringComparer.OrdinalIgnoreCase).Where(n => n.Length > 0 && !existing.Contains(n)).ToList();
                AddCheck("Load patterns named in this tool", missing.Count == 0 ? "PASS" : "FAIL", missing.Count == 0 ? existing.Count + " patterns in ETABS; all named ones exist." : "Missing in ETABS: " + string.Join(", ", missing));
                var dup = existing.Count;
                AddCheck("Duplicate pattern names", "PASS", "ETABS keeps names unique, " + dup + " distinct patterns.");
            }
            catch (Exception ex) { AddCheck("Load patterns named in this tool", "NOT CHECKED", Msg(ex)); }

            try
            {
                var combos = etabs.GetComboNames();
                int n = combos.Count(c => c.StartsWith("ULS", StringComparison.OrdinalIgnoreCase) || c.StartsWith("ASD", StringComparison.OrdinalIgnoreCase));
                AddCheck("BNBC combinations (ULS / ASD)", n > 0 ? "PASS" : "WARN", n > 0 ? n + " combination(s) created by this tool." : "None found. Use Load Combinations to create them.");
            }
            catch (Exception ex) { AddCheck("BNBC combinations (ULS / ASD)", "NOT CHECKED", Msg(ex)); }
            Summarize();
        }

        static string Msg(Exception ex) { var e = ex.InnerException ?? ex; return "Could not read from ETABS: " + e.Message; }

        void Summarize()
        {
            TxtCheckSummary.Text = "PASS " + checks.Count(c => c.Status == "PASS") + "   WARN " + checks.Count(c => c.Status == "WARN") +
                "   FAIL " + checks.Count(c => c.Status == "FAIL") + "   INCOMPLETE " + checks.Count(c => c.Status == "INCOMPLETE") +
                "   NOT CHECKED " + checks.Count(c => c.Status == "NOT CHECKED");
            Log("INFO", "Model check done. " + TxtCheckSummary.Text);
        }

        void ExportChecks_Click(object sender, RoutedEventArgs e)
        {
            if (checks.Count == 0) { Log("ERROR", "Run the checks first."); return; }
            var d = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "model_check.csv" };
            if (d.ShowDialog() != true) return;
            try
            {
                var sb = new StringBuilder("Check,Result,Detail\n");
                foreach (var c in checks) sb.AppendLine("\"" + c.Check.Replace("\"", "'") + "\",\"" + c.Status + "\",\"" + c.Details.Replace("\"", "'") + "\"");
                File.WriteAllText(d.FileName, sb.ToString(), Encoding.UTF8);
                Log("INFO", "Exported " + d.FileName);
            }
            catch (Exception ex) { Log("ERROR", "Export failed: " + ex.Message); }
        }


        // ---------- report ----------
        ReportData CollectReport()
        {
            SeisGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var p = ReadProject();
            var d = new ReportData
            {
                ProjectName = string.IsNullOrWhiteSpace(p.Name) ? null : p.Name, Location = p.Town, Soil = p.Soil, Occupancy = p.Occupancy,
                Stories = string.IsNullOrWhiteSpace(p.Stories) ? null : p.Stories, Notes = p.Notes,
                Seismic = TxtSeisResult.Text, Wind = TxtWindResult.Text, WallLoad = TxtWallResult.Text
            };
            if (d.Seismic != null && d.Seismic.StartsWith("INCOMPLETE")) d.Seismic = null;
            if (d.Wind != null && (d.Wind.StartsWith("INCOMPLETE") || d.Wind.StartsWith("T = "))) d.Wind = null;
            if (d.WallLoad != null && d.WallLoad.StartsWith("INCOMPLETE")) d.WallLoad = null;
            d.Floors.Add(new[] { "Floor", "Occupancy or use", "LL kN/m2", "Table notes", "Source" });
            foreach (var f in floors)
            {
                var row = DataStore.Lives.FirstOrDefault(l => l.Name == f.Occupancy);
                d.Floors.Add(new[] { f.Floor ?? "", f.Occupancy ?? "NOT SELECTED", f.LL ?? "", f.Notes ?? "", row == null ? "" : "BNBC 2020 Table 6.2.3, PDF p." + row.Page });
            }
            d.Combos.Add(new[] { "Name", "Definition", "Code reference", "Status" });
            foreach (var c in comboRows) d.Combos.Add(new[] { c.Name, c.Definition, c.Reference, c.Status ?? "" });
            d.Reactions.Add(new[] { "Joint", "Combo", "Fx", "Fy", "Fz", "Mx", "My", "Mz" });
            foreach (var r in reactions)
                d.Reactions.Add(new[] { r.Joint, r.Case, r.F1.ToString("0.000", CultureInfo.InvariantCulture), r.F2.ToString("0.000", CultureInfo.InvariantCulture), r.F3.ToString("0.000", CultureInfo.InvariantCulture), r.M1.ToString("0.000", CultureInfo.InvariantCulture), r.M2.ToString("0.000", CultureInfo.InvariantCulture), r.M3.ToString("0.000", CultureInfo.InvariantCulture) });
            d.Checks.Add(new[] { "Check", "Status", "Detail" });
            foreach (var c in checks) d.Checks.Add(new[] { c.Check, c.Status, c.Details });
            return d;
        }

        static string ReportVersion() { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); }

        void ReportPreview_Click(object sender, RoutedEventArgs e)
        {
            var d = CollectReport();
            Func<bool, string> yn = b => b ? "included" : "NOT CALCULATED";
            TxtReportInfo.Text =
                "Project data      : " + (d.ProjectName != null && d.Location != null && d.Soil != null && d.Occupancy != null && d.Stories != null ? "complete" : "INCOMPLETE (missing items are marked MISSING)") + "\n" +
                "Seismic           : " + yn(!string.IsNullOrWhiteSpace(d.Seismic)) + "\n" +
                "Wind              : " + yn(!string.IsNullOrWhiteSpace(d.Wind)) + "\n" +
                "Floor live loads  : " + (d.Floors.Count > 1 ? (d.Floors.Count - 1) + " floor(s)" : "NOT DEFINED") + "\n" +
                "Wall load         : " + yn(!string.IsNullOrWhiteSpace(d.WallLoad)) + "\n" +
                "Combinations      : " + (d.Combos.Count > 1 ? (d.Combos.Count - 1) + " combination(s)" : "NOT GENERATED") + "\n" +
                "Reactions         : " + (d.Reactions.Count > 1 ? (d.Reactions.Count - 1) + " row(s)" : "NOT EXTRACTED") + "\n" +
                "Model checks      : " + (d.Checks.Count > 1 ? (d.Checks.Count - 1) + " check(s)" : "NOT RUN");
        }

        void ReportHtml_Click(object sender, RoutedEventArgs e)
        {
            var d = new SaveFileDialog { Filter = "HTML report (*.html)|*.html", FileName = "BNBC_report.html" };
            if (d.ShowDialog() != true) return;
            try
            {
                File.WriteAllText(d.FileName, ReportBuilder.Html(CollectReport(), ReportVersion(), DateTime.Now), new UTF8Encoding(true));
                Log("INFO", "Report saved: " + d.FileName);
                try { System.Diagnostics.Process.Start(d.FileName); } catch { Log("WARN", "Saved, but the browser could not be opened. Open the file by hand."); }
            }
            catch (Exception ex) { Log("ERROR", "Report failed: " + ex.Message); }
        }

        void ReportXlsx_Click(object sender, RoutedEventArgs e)
        {
            var d = new SaveFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx", FileName = "BNBC_report.xlsx" };
            if (d.ShowDialog() != true) return;
            try
            {
                int n = ReportBuilder.WriteXlsx(CollectReport(), d.FileName);
                Log("INFO", "Excel saved with " + n + " sheet(s): " + d.FileName);
            }
            catch (Exception ex) { Log("ERROR", "Excel export failed: " + ex.Message); }
        }


        // ---------- report ----------
        List<ReportSheet> CollectSheets()
        {
            var sheets = new List<ReportSheet>();
            var p = ReadProject();
            var town = DataStore.Towns.FirstOrDefault(t => t.Name == p.Town);

            var proj = new ReportSheet("Project");
            proj.Add("Item", "Value");
            proj.Add("Report generated", DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            proj.Add("Software", "BNBC ULTIMATE PRO " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
            proj.Add("Code", "BNBC 2020 (Part 6, Chapter 2), values read from the PDF supplied by the user");
            proj.Add("ETABS", etabs.Connected ? "ETABS " + etabs.Version + " - " + etabs.ModelFile : "not connected");
            proj.Add("Project name", string.IsNullOrWhiteSpace(p.Name) ? "NOT ENTERED" : p.Name);
            proj.Add("Location", p.Town ?? "NOT ENTERED");
            proj.Add("Seismic zone coefficient Z", town == null ? "NOT AVAILABLE" : town.Z.ToString("0.00", CultureInfo.InvariantCulture));
            proj.Add("Soil type (user input)", p.Soil ?? "NOT ENTERED");
            proj.Add("Occupancy category", p.Occupancy ?? "NOT ENTERED");
            proj.Add("Number of stories", string.IsNullOrWhiteSpace(p.Stories) ? "NOT ENTERED" : p.Stories);
            proj.Add("Notes", p.Notes ?? "");
            sheets.Add(proj);

            var sp = new ReportSheet("Seismic parameters", true); sp.AddLines(BuildSeismicText()); sheets.Add(sp);

            var ss = new ReportSheet("Seismic static", true);
            if (!string.IsNullOrWhiteSpace(TxtSeisResult.Text)) ss.AddLines(TxtSeisResult.Text);
            sheets.Add(ss);

            var wd = new ReportSheet("Wind", true);
            if (!string.IsNullOrWhiteSpace(TxtWindResult.Text)) wd.AddLines(TxtWindResult.Text);
            sheets.Add(wd);

            var wf = new ReportSheet("Wind story forces");
            if (lastFx != null && lastFy != null)
            {
                wf.Add("Story", "z (m)", "Fx kN (wind along X)", "Fy kN (wind along Y)");
                for (int i = 0; i < lastFx.Count; i++)
                    wf.Add(lastFx[i].Name, lastFx[i].Z.ToString("0.###", CultureInfo.InvariantCulture), lastFx[i].F.ToString("0.000", CultureInfo.InvariantCulture), lastFy[i].F.ToString("0.000", CultureInfo.InvariantCulture));
            }
            sheets.Add(wf);

            var fl = new ReportSheet("Floor live loads");
            if (floors.Count > 0)
            {
                fl.Add("Floor", "Occupancy or use (BNBC Table 6.2.3)", "Live load kN/m2", "Table notes");
                foreach (var f in floors) fl.Add(f.Floor, f.Occupancy ?? "NOT CHOSEN", string.IsNullOrEmpty(f.LL) ? "-" : f.LL, f.Notes ?? "");
            }
            sheets.Add(fl);

            var lp = new ReportSheet("Load patterns");
            lp.Add("Name", "Type", "Self-weight multiplier", "Status in last preview");
            foreach (var x in patterns) lp.Add(x.Name, x.Type, x.SelfWeight.ToString(CultureInfo.InvariantCulture), x.Status ?? "");
            sheets.Add(lp);

            var cb = new ReportSheet("Load combinations");
            if (comboRows.Count > 0)
            {
                cb.Add("Name", "Definition", "Code reference", "Status");
                foreach (var c in comboRows) cb.Add(c.Name, c.Definition, c.Reference, c.Status ?? "");
            }
            sheets.Add(cb);

            var rc = new ReportSheet("Foundation reactions");
            if (reactions.Count > 0)
            {
                rc.Add("Joint", "Combo", "Fx kN", "Fy kN", "Fz kN", "Mx kNm", "My kNm", "Mz kNm");
                foreach (var r in reactions)
                    rc.Add(r.Joint, r.Case, r.F1.ToString("0.000", CultureInfo.InvariantCulture), r.F2.ToString("0.000", CultureInfo.InvariantCulture), r.F3.ToString("0.000", CultureInfo.InvariantCulture),
                           r.M1.ToString("0.000", CultureInfo.InvariantCulture), r.M2.ToString("0.000", CultureInfo.InvariantCulture), r.M3.ToString("0.000", CultureInfo.InvariantCulture));
            }
            sheets.Add(rc);

            var ck = new ReportSheet("Model checks");
            if (checks.Count > 0)
            {
                ck.Add("Check", "Status", "Detail");
                foreach (var c in checks) ck.Add(c.Check, c.Status, c.Details);
            }
            sheets.Add(ck);

            var cp = new ReportSheet("Compliance");
            if (compliance.Count > 0)
            {
                cp.Add("Requirement", "Status", "Detail");
                foreach (var c in compliance) cp.Add(c.Check, c.Status, c.Details);
            }
            sheets.Add(cp);

            var lim = new ReportSheet("Assumptions and limits", true);
            lim.AddLines("Soil type, occupancy, exposure, structural system and all exceptions are user decisions.\n" +
                "Table values were read from the BNBC 2020 PDF supplied by the user; check them against the printed code before relying on them.\n" +
                "Not implemented: live load reduction, concentrated loads, accidental torsion, drift checks, roof wind pressures, torsional wind cases,\n" +
                "flexible-building wind, flood combinations, vertical seismic component.\n" +
                "This report supports, and does not replace, the design engineer's own checking.");
            sheets.Add(lim);
            return sheets;
        }

        void RefreshReportInfo()
        {
            var sb = new StringBuilder();
            foreach (var sh in CollectSheets())
                sb.AppendLine((sh.Rows.Count == 0 ? "[EMPTY]   " : "[" + sh.Rows.Count.ToString().PadLeft(4) + " rows] ") + sh.Name);
            TxtReportInfo.Text = sb.ToString();
        }

        void RptXlsx_Click(object sender, RoutedEventArgs e)
        {
            var d = new SaveFileDialog { Filter = "Excel workbook (*.xlsx)|*.xlsx", FileName = "BNBC_report.xlsx" };
            if (d.ShowDialog() != true) return;
            try { ReportBuilder.WriteXlsx(d.FileName, CollectSheets()); Log("INFO", "Excel report saved: " + d.FileName); }
            catch (Exception ex) { Log("ERROR", "Excel export failed: " + ex.Message); }
        }

        void RptHtml_Click(object sender, RoutedEventArgs e)
        {
            var d = new SaveFileDialog { Filter = "HTML report (*.html)|*.html", FileName = "BNBC_report.html" };
            if (d.ShowDialog() != true) return;
            try { File.WriteAllText(d.FileName, ReportBuilder.Html("BNBC ULTIMATE PRO - Calculation Report", CollectSheets()), new UTF8Encoding(false)); Log("INFO", "HTML report saved: " + d.FileName); }
            catch (Exception ex) { Log("ERROR", "HTML export failed: " + ex.Message); }
        }

        void RptOpen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = Path.Combine(Path.GetTempPath(), "BNBC_report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".html");
                File.WriteAllText(path, ReportBuilder.Html("BNBC ULTIMATE PRO - Calculation Report", CollectSheets()), new UTF8Encoding(false));
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                Log("INFO", "Report opened in the browser. Press Ctrl+P and choose 'Save as PDF'.");
            }
            catch (Exception ex) { Log("ERROR", "Could not open the report: " + ex.Message); }
        }


        // ---------- compliance checker ----------
        void AddComp(string requirement, string status, string detail) { compliance.Add(new CheckRow { Check = requirement, Status = status, Details = detail }); }

        void RunCompliance_Click(object sender, RoutedEventArgs e)
        {
            compliance.Clear();
            var p = ReadProject();
            int stories;

            // project data
            var miss = new List<string>();
            if (string.IsNullOrWhiteSpace(p.Name)) miss.Add("project name");
            if (p.Town == null) miss.Add("location");
            if (p.Soil == null) miss.Add("soil type");
            if (p.Occupancy == null) miss.Add("occupancy category");
            if (!(int.TryParse(p.Stories, out stories) && stories > 0)) miss.Add("number of stories");
            AddComp("Project data", miss.Count == 0 ? "PASS" : "INCOMPLETE", miss.Count == 0 ? "All project data entered. Soil type is your own input from the geotechnical report (Sec 2.5.3)." : "Missing: " + string.Join(", ", miss) + ".");

            // seismic parameters from tables
            bool seisParams = p.Town != null && p.Soil != null && p.Occupancy != null;
            AddComp("Seismic parameters Z, I, S, TB, TC, TD", seisParams ? "PASS" : "INCOMPLETE",
                seisParams ? "Looked up from Tables 6.2.15, 6.2.16 and 6.2.17." : "Needs location, soil type and occupancy category (Tables 6.2.15 to 6.2.17).");

            // seismic static analysis
            if (!seisDone && seisWarnings.Count == 0)
                AddComp("Equivalent static seismic analysis", "NOT CALCULATED", "Run Seismic Static Analysis first (Sec 2.5.7).");
            else
            {
                if (seisWarnings.Count == 0)
                    AddComp("Equivalent static seismic analysis", "PASS", "T limit, elevation regularity confirmation, permitted system and height limit show no violation (Sec 2.5.6, Table 6.2.18, Table 6.2.19). Torsion and drift are not checked.");
                foreach (var w in seisWarnings)
                    AddComp("Equivalent static seismic analysis", ComplianceRules.ClassifyWarning(w), w);
            }

            // wind
            AddComp("Wind load, Method 2", windDone ? "PASS" : "NOT CALCULATED",
                windDone ? "Calculated for a rigid, regular, enclosed building with your confirmations of Sec 2.4.3.1 and Sec 2.4.7.1." : "Run Wind Load first (Sec 2.4).");
            if (windDone) AddComp("Wind load limits", "WARN", "Roof pressures, torsional load cases (Figure 6.2.9), parapets and flexible-building effects are not included.");

            // load combinations
            if (comboRows.Count == 0) AddComp("Load combinations (Sec 2.7)", "NOT CALCULATED", "Generate them in Load Combinations.");
            else
            {
                int inEtabs = comboRows.Count(c => c.Status != null && (c.Status == "CREATED" || c.Status.StartsWith("EXISTS")));
                AddComp("Load combinations (Sec 2.7)", inEtabs == 0 ? "WARN" : inEtabs == comboRows.Count ? "PASS" : "WARN",
                    inEtabs + " of " + comboRows.Count + " generated combinations exist in ETABS (Sec 2.7.2.1 / 2.7.3.1).");
                AddComp("Flood and tidal-surge combinations", "WARN", "Not generated. Add them yourself if the site is in a flood or tidal-surge zone (end of Sec 2.7.3.1).");
            }

            // live loads
            if (floors.Count == 0) AddComp("Floor live loads (Table 6.2.3)", "NOT CALCULATED", "Add floors and occupancies in Floor & Wall Loads.");
            else
            {
                var none = floors.Where(f => string.IsNullOrEmpty(f.Occupancy)).Select(f => f.Floor).ToList();
                AddComp("Floor live loads (Table 6.2.3)", none.Count == 0 ? "PASS" : "INCOMPLETE",
                    none.Count == 0 ? floors.Count + " floor(s) have a Table 6.2.3 value. Live load reduction (Sec 2.3.13) is not applied." : "No occupancy chosen for: " + string.Join(", ", none) + ".");
            }

            // model checker
            if (checks.Count == 0) AddComp("ETABS model checks", "NOT CALCULATED", "Run the Model Checker.");
            else
            {
                var worst = ComplianceRules.Worst(checks.Select(c => c.Status));
                AddComp("ETABS model checks", worst, checks.Count(c => c.Status == "FAIL") + " fail, " + checks.Count(c => c.Status == "WARN" || c.Status == "NOT CHECKED") + " warn or not checked, " + checks.Count(c => c.Status == "INCOMPLETE") + " incomplete (see Model Checker).");
            }

            TxtCompSummary.Text = "PASS " + compliance.Count(c => c.Status == "PASS") + "   WARN " + compliance.Count(c => c.Status == "WARN") +
                "   FAIL " + compliance.Count(c => c.Status == "FAIL") + "   INCOMPLETE " + compliance.Count(c => c.Status == "INCOMPLETE") +
                "   NOT CALCULATED " + compliance.Count(c => c.Status == "NOT CALCULATED");
            Log("INFO", "Compliance check done. " + TxtCompSummary.Text);
        }

        void ExportCompliance_Click(object sender, RoutedEventArgs e)
        {
            if (compliance.Count == 0) { Log("ERROR", "Run the compliance check first."); return; }
            var d = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "compliance_check.csv" };
            if (d.ShowDialog() != true) return;
            try
            {
                var sb = new StringBuilder("Requirement,Status,Detail\n");
                foreach (var c in compliance) sb.AppendLine("\"" + c.Check.Replace("\"", "'") + "\",\"" + c.Status + "\",\"" + c.Details.Replace("\"", "'") + "\"");
                File.WriteAllText(d.FileName, sb.ToString(), Encoding.UTF8);
                Log("INFO", "Exported " + d.FileName);
            }
            catch (Exception ex) { Log("ERROR", "Export failed: " + ex.Message); }
        }

        // ---------- wall load ----------
        bool Num(string text, out double v) { return double.TryParse((text ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v); }

        bool TryWall(out double w, out string detail)
        {
            w = 0; detail = "";
            var mat = CmbWallMat.SelectedItem as WallMaterial;
            double g, t, h, fin, faces, op;
            if (mat == null) { detail = "INCOMPLETE: choose a wall material."; return false; }
            if (!double.IsNaN(mat.Gamma)) { TxtGamma.Text = mat.Gamma.ToString("0.0##", CultureInfo.InvariantCulture); TxtGamma.IsReadOnly = true; }
            else TxtGamma.IsReadOnly = false;
            if (!Num(TxtGamma.Text, out g) || g <= 0) { detail = "INCOMPLETE: unit weight."; return false; }
            if (!Num(TxtWallT.Text, out t) || t <= 0) { detail = "INCOMPLETE: wall thickness (m)."; return false; }
            if (!Num(TxtWallH.Text, out h) || h <= 0) { detail = "INCOMPLETE: wall height (m)."; return false; }
            if (!Num(TxtFinish.Text, out fin) || fin < 0) { detail = "INCOMPLETE: finish load (enter 0 if none)."; return false; }
            if (!Num(TxtFaces.Text, out faces) || (faces != 0 && faces != 1 && faces != 2)) { detail = "INCOMPLETE: faces must be 0, 1 or 2."; return false; }
            if (!Num(TxtOpen.Text, out op) || op < 0 || op >= 100) { detail = "INCOMPLETE: openings % (0 to <100)."; return false; }
            w = WallCalc.LineLoad(g, t, h, fin, (int)faces, op / 100.0);
            detail = string.Format(CultureInfo.InvariantCulture,
                "w = ({0} x {1} + {2} x {3}) x {4} x (1 - {5}) = {6:0.000} kN/m\nUnit weight source: {7}. Finish and openings: user input.",
                g, t, (int)faces, fin, h, op / 100.0, w, mat.Ref);
            return true;
        }

        void Wall_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            double w; string d;
            TryWall(out w, out d);
            TxtWallResult.Text = d;
        }

        void AssignWall_Click(object sender, RoutedEventArgs e)
        {
            double w; string d;
            bool ok = TryWall(out w, out d);
            TxtWallResult.Text = d;
            if (!ok) { Log("ERROR", d); return; }
            if (!EnsureModel()) return;
            string pat = TxtWallPat.Text.Trim();
            try
            {
                if (!etabs.GetLoadPatternNames().Contains(pat, StringComparer.OrdinalIgnoreCase)) { Log("ERROR", "Load pattern '" + pat + "' does not exist in ETABS."); return; }
                int areas, frames; etabs.GetSelectionCounts(out areas, out frames);
                if (frames == 0) { Log("ERROR", "No beams (frame objects) are selected in ETABS."); return; }
                var msg = "Assign " + w.ToString("0.000", CultureInfo.InvariantCulture) + " kN/m to " + frames + " selected frame object(s) under pattern " + pat + "?\n\nAn existing distributed load of this pattern on them will be replaced.";
                if (MessageBox.Show(msg, "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                int ret = etabs.AssignFrameLine(pat, w);
                Log(ret == 0 ? "INFO" : "ERROR", ret == 0 ? "Wall load " + w.ToString("0.000", CultureInfo.InvariantCulture) + " kN/m assigned to " + frames + " beam(s)." : "Assign failed (code " + ret + "). Is the model unlocked?");
            }
            catch (Exception ex) { Log("ERROR", "Assign failed: " + (ex.InnerException ?? ex).Message); }
        }
    }

    public class FloorRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void Raise(string n) { var h = PropertyChanged; if (h != null) h(this, new PropertyChangedEventArgs(n)); }
        public string Floor { get; set; }
        string occ;
        public string Occupancy { get { return occ; } set { occ = value; Raise("Occupancy"); Raise("LL"); Raise("Notes"); } }
        public string LL
        {
            get
            {
                var r = DataStore.Lives.FirstOrDefault(l => l.Name == occ);
                return r == null ? "" : r.LL.ToString("0.00", CultureInfo.InvariantCulture);
            }
        }
        public string Notes
        {
            get { var r = DataStore.Lives.FirstOrDefault(l => l.Name == occ); return r == null ? "" : r.Notes; }
        }
    }

    public class MapRow
    {
        public string Symbol { get; set; }
        public string Description { get; set; }
        public string Patterns { get; set; }
    }
}
