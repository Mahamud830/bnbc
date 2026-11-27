using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;

namespace BnbcUltimatePro
{
    internal class ReportSheet
    {
        public string Name;
        public bool Monospace;                       // true: one text line per row
        public List<string[]> Rows = new List<string[]>();
        public ReportSheet(string name, bool mono = false) { Name = name; Monospace = mono; }
        public void Add(params string[] cells) { Rows.Add(cells); }
        public void AddLines(string text)
        {
            foreach (var l in (text ?? "").Replace("\r", "").Split('\n')) Rows.Add(new[] { l });
        }
    }

    /// <summary>Writes the collected results as an .xlsx workbook (no external library) or a printable HTML page.</summary>
    internal static class ReportBuilder
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly System.Text.RegularExpressions.Regex NumberText = new System.Text.RegularExpressions.Regex(@"^-?(0|[1-9][0-9]*)(\.[0-9]+)?$");

        static string Clean(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder(s.Length);
            foreach (var c in s) if (c == '\t' || c == '\n' || c == '\r' || c >= ' ') sb.Append(c);
            return sb.ToString();
        }

        static string ColName(int i)
        {
            string n = "";
            i++;
            while (i > 0) { int r = (i - 1) % 26; n = (char)('A' + r) + n; i = (i - 1) / 26; }
            return n;
        }

        static string SheetName(string name, int index)
        {
            var bad = new[] { '[', ']', ':', '*', '?', '/', '\\' };
            var n = new string(name.Where(c => !bad.Contains(c)).ToArray());
            if (n.Length == 0) n = "Sheet" + (index + 1);
            return n.Length > 31 ? n.Substring(0, 31) : n;
        }

        static void Entry(ZipArchive zip, string name, string xml)
        {
            var e = zip.CreateEntry(name);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(xml);
        }

        public static void WriteXlsx(string path, List<ReportSheet> sheets)
        {
            using (var fs = File.Create(path))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var ct = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
                var wb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
                var rel = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
                for (int i = 0; i < sheets.Count; i++)
                {
                    int n = i + 1;
                    ct.Append("<Override PartName=\"/xl/worksheets/sheet" + n + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                    wb.Append("<sheet name=\"" + SecurityElement.Escape(SheetName(sheets[i].Name, i)) + "\" sheetId=\"" + n + "\" r:id=\"rId" + n + "\"/>");
                    rel.Append("<Relationship Id=\"rId" + n + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" + n + ".xml\"/>");
                    Entry(zip, "xl/worksheets/sheet" + n + ".xml", SheetXml(sheets[i]));
                }
                ct.Append("</Types>"); wb.Append("</sheets></workbook>"); rel.Append("</Relationships>");
                Entry(zip, "[Content_Types].xml", ct.ToString());
                Entry(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Entry(zip, "xl/workbook.xml", wb.ToString());
                Entry(zip, "xl/_rels/workbook.xml.rels", rel.ToString());
            }
        }

        static string SheetXml(ReportSheet sh)
        {
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            for (int r = 0; r < sh.Rows.Count; r++)
            {
                sb.Append("<row r=\"" + (r + 1) + "\">");
                var row = sh.Rows[r];
                for (int c = 0; c < row.Length; c++)
                {
                    string v = Clean(row[c]);
                    if (v.Length == 0) continue;
                    string cell = ColName(c) + (r + 1);
                    double d;
                    if (NumberText.IsMatch(v) && double.TryParse(v, NumberStyles.Float, Inv, out d))
                        sb.Append("<c r=\"" + cell + "\"><v>" + d.ToString("R", Inv) + "</v></c>");
                    else
                        sb.Append("<c r=\"" + cell + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" + SecurityElement.Escape(v) + "</t></is></c>");
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        public static string Html(string title, List<ReportSheet> sheets)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + WebUtility.HtmlEncode(title) + "</title>");
            sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#111}h1{color:#0B1730}h2{color:#0B1730;border-bottom:2px solid #22D3EE;padding-bottom:3px;margin-top:28px}table{border-collapse:collapse;width:100%;font-size:12px}td,th{border:1px solid #999;padding:3px 6px;vertical-align:top}pre{font-family:Consolas,monospace;font-size:11px;white-space:pre-wrap;margin:0}.note{color:#555;font-size:12px}@media print{h2{page-break-before:always}h2:first-of-type{page-break-before:avoid}}</style></head><body>");
            sb.AppendLine("<h1>" + WebUtility.HtmlEncode(title) + "</h1>");
            foreach (var sh in sheets)
            {
                sb.AppendLine("<h2>" + WebUtility.HtmlEncode(sh.Name) + "</h2>");
                if (sh.Rows.Count == 0) { sb.AppendLine("<p class=\"note\">Nothing calculated or entered yet.</p>"); continue; }
                if (sh.Monospace)
                {
                    sb.AppendLine("<pre>" + WebUtility.HtmlEncode(string.Join("\n", sh.Rows.Select(r => r.Length > 0 ? r[0] : ""))) + "</pre>");
                    continue;
                }
                sb.AppendLine("<table>");
                for (int i = 0; i < sh.Rows.Count; i++)
                {
                    string tag = i == 0 ? "th" : "td";
                    sb.Append("<tr>");
                    foreach (var c in sh.Rows[i]) sb.Append("<" + tag + ">" + WebUtility.HtmlEncode(c ?? "") + "</" + tag + ">");
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</table>");
            }
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }
    }
}
