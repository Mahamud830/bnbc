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
        readonly string[] pageNames = { "PageDashboard", "PageProject", "PageSeismic", "PageLoads", "PageCombos", "PageFloors" };
        static readonly string[] PatternTypes = { "Dead", "SuperDead", "Live", "ReduceLive", "RoofLive", "Quake", "Wind", "Snow", "Other" };
        bool loading;
        readonly ObservableCollection<MapRow> mapRows = new ObservableCollection<MapRow>();
        readonly ObservableCollection<ComboRow> comboRows = new ObservableCollection<ComboRow>();
        readonly ObservableCollection<FloorRow> floors = new ObservableCollection<FloorRow>();

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
            ((DataGridComboBoxColumn)FloorGrid.Columns[1]).ItemsSource = DataStore.Lives.Select(l => l.Name).ToList();
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
