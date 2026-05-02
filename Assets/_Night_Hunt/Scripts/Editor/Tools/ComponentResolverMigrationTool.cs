using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace NightHunt.Editor.Tools
{
    /// <summary>
    /// Scans .cs files and replaces ONLY the GetComponent&lt;T&gt;() call itself.
    ///
    /// RULE: One line in → one line out. Nothing else is touched.
    ///
    /// BEFORE:
    ///   CapsuleCollider modelCap = GetComponent&lt;CapsuleCollider&gt;();
    ///
    /// AFTER:
    ///   CapsuleCollider modelCap = ComponentResolver.Find&lt;CapsuleCollider&gt;(this)
    ///       .OnSelf()
    ///       .InChildren()
    ///       .OrLogWarning("[Auto] CapsuleCollider not found")
    ///       .Resolve();
    ///
    /// LOCATION: Assets/Editor/ComponentResolverMigrationTool.cs
    /// OPEN:     Tools → NightHunt → ComponentResolver Migration Tool
    /// </summary>
    public class ComponentResolverMigrationTool : EditorWindow
    {
        [MenuItem("Tools/NightHunt/ComponentResolver Migration Tool")]
        public static void OpenWindow()
        {
            var w = GetWindow<ComponentResolverMigrationTool>("CR Migration");
            w.minSize = new Vector2(960, 580);
        }

        // ── State ──────────────────────────────────────────────────────────────
        private string _searchRoot   = "Assets";
        private bool   _skipEditor   = true;
        private bool   _skipGen      = true;
        private string _skipWords    = "Plugins,ThirdParty,Generated";

        private List<FileScanResult> _results     = new List<FileScanResult>();
        private FileScanResult       _preview;
        private Vector2              _scrollLeft, _scrollRight;
        private HashSet<string>      _selected    = new HashSet<string>();
        private HashSet<string>      _expanded    = new HashSet<string>();
        private string               _status      = "Press  🔍 Scan  to start.";
        private bool                 _statusErr;

        // ── Styles ─────────────────────────────────────────────────────────────
        private bool     _stylesOk;
        private GUIStyle _sTitle, _sBtn, _sHit, _sOld, _sNew, _sBadge, _sBox;

        // ══════════════════════════════════════════════════════════════════════
        // REGEX — matches the call expression only, nothing before/after it
        //
        //   GetComponent<T>()
        //   GetComponent<T>(true)
        //   GetComponentInChildren<T>()
        //   GetComponentInChildren<T>(true)
        //   GetComponentInParent<T>()
        //
        // Captured groups:
        //   method  — GetComponent | GetComponentInChildren | GetComponentInParent
        //   type    — T (may contain namespace, interface, generic params)
        //   args    — content inside ()
        // ══════════════════════════════════════════════════════════════════════
        private static readonly Regex RxCall = new Regex(
            @"\b(?<method>GetComponent(?:InChildren|InParent)?)" +
            @"\s*<\s*(?<type>[^>]+?)\s*>" +
            @"\s*\((?<args>[^)]*)\)",
            RegexOptions.Compiled);

        // ══════════════════════════════════════════════════════════════════════
        // GUI
        // ══════════════════════════════════════════════════════════════════════
        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(370)); DrawFileList();  EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();                      DrawPreview();  EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        // ── Toolbar ────────────────────────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginVertical(_sBox);

            // Title + status pill
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("⚙  ComponentResolver Migration Tool", _sTitle);
            GUILayout.FlexibleSpace();
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = _statusErr ? new Color(.9f,.3f,.3f) : new Color(.25f,.65f,.35f);
            GUILayout.Label(_status, _sBadge, GUILayout.Height(22));
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Options row
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Root:", GUILayout.Width(34));
            _searchRoot = EditorGUILayout.TextField(_searchRoot, GUILayout.Width(200));
            if (GUILayout.Button("…", GUILayout.Width(22)))
            {
                string p = EditorUtility.OpenFolderPanel("Root folder", _searchRoot, "");
                if (!string.IsNullOrEmpty(p))
                    _searchRoot = "Assets" + p.Substring(Application.dataPath.Length);
            }
            GUILayout.Space(8);
            _skipEditor = EditorGUILayout.ToggleLeft("Editor/",    _skipEditor, GUILayout.Width(64));
            _skipGen    = EditorGUILayout.ToggleLeft("Generated/", _skipGen,    GUILayout.Width(88));
            GUILayout.Label("Skip:", GUILayout.Width(30));
            _skipWords = EditorGUILayout.TextField(_skipWords, GUILayout.Width(170));
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("🔍 Scan",  GUILayout.Width(76),  GUILayout.Height(24))) RunScan();
            if (GUILayout.Button("☑ All",    GUILayout.Width(48),  GUILayout.Height(24))) ToggleAll(true);
            if (GUILayout.Button("☐ None",   GUILayout.Width(50),  GUILayout.Height(24))) ToggleAll(false);
            GUI.enabled = _selected.Count > 0;
            if (GUILayout.Button($"✅ Apply ({_selected.Count})", GUILayout.Width(108), GUILayout.Height(24)))
                ApplySelected();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ── File list ──────────────────────────────────────────────────────────
        private void DrawFileList()
        {
            if (_results.Count == 0)
            { EditorGUILayout.HelpBox("No results — run Scan.", MessageType.Info); return; }

            GUILayout.Label($"  {_results.Count} file(s)  ·  {_results.Sum(r => r.Hits.Count)} line(s)",
                EditorStyles.boldLabel);

            _scrollLeft = EditorGUILayout.BeginScrollView(_scrollLeft);
            foreach (var r in _results.OrderByDescending(x => x.Hits.Count))
                DrawFileRow(r);
            EditorGUILayout.EndScrollView();
        }

        private void DrawFileRow(FileScanResult r)
        {
            bool exp = _expanded.Contains(r.FilePath);
            bool sel = _selected.Contains(r.FilePath);

            EditorGUILayout.BeginHorizontal(_sBtn);
            bool newSel = EditorGUILayout.Toggle(sel, GUILayout.Width(18));
            if (newSel != sel) { if (newSel) _selected.Add(r.FilePath); else _selected.Remove(r.FilePath); }

            if (GUILayout.Button($"{(exp ? "▼" : "▶")}  {r.FileName}", _sBtn, GUILayout.ExpandWidth(true)))
            {
                _preview = r;
                if (exp) _expanded.Remove(r.FilePath); else _expanded.Add(r.FilePath);
            }

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = r.Applied ? new Color(.25f,.7f,.3f) : new Color(.9f,.6f,.2f);
            GUILayout.Label(r.Applied ? "✓" : r.Hits.Count.ToString(), _sBadge, GUILayout.Width(30));
            GUI.backgroundColor = prevBg;
            EditorGUILayout.EndHorizontal();

            if (exp)
            {
                foreach (var h in r.Hits)
                {
                    EditorGUILayout.BeginHorizontal(_sHit);
                    GUILayout.Label($"  L{h.LineNumber}", GUILayout.Width(44));
                    GUILayout.Label(h.Method, EditorStyles.miniLabel, GUILayout.Width(150));
                    GUILayout.Label(h.Type,   EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
                GUI.enabled = !r.Applied;
                if (GUILayout.Button($"  Apply  {r.FileName}", GUILayout.Height(20))) ApplyFile(r);
                GUI.enabled = true;
                GUILayout.Space(2);
            }
        }

        // ── Preview panel ──────────────────────────────────────────────────────
        private void DrawPreview()
        {
            if (_preview == null)
            { EditorGUILayout.HelpBox("Select a file on the left.", MessageType.None); return; }

            GUILayout.Label($"  {_preview.FileName}  —  {_preview.Hits.Count} hit(s)", EditorStyles.boldLabel);

            _scrollRight = EditorGUILayout.BeginScrollView(_scrollRight);
            foreach (var h in _preview.Hits)
            {
                EditorGUILayout.BeginVertical(_sBox);

                GUILayout.Label($"Line {h.LineNumber}  ·  {h.Method}<{h.Type}>",
                    EditorStyles.miniBoldLabel);

                // Before
                GUILayout.Label("  ✂  Before (full line):", EditorStyles.miniLabel);
                EditorGUILayout.SelectableLabel(h.OriginalLine, _sOld, GUILayout.Height(20));

                // After
                GUILayout.Label("  ＋  After (full line):", EditorStyles.miniLabel);
                int rows = h.ReplacedLine.Split('\n').Length;
                EditorGUILayout.SelectableLabel(h.ReplacedLine, _sNew,
                    GUILayout.Height(Mathf.Max(20, rows * 18 + 4)));

                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
            }
            EditorGUILayout.EndScrollView();
        }

        // ══════════════════════════════════════════════════════════════════════
        // SCAN  — only looks at lines, never looks at adjacent lines
        // ══════════════════════════════════════════════════════════════════════
        private void RunScan()
        {
            _results.Clear(); _selected.Clear(); _expanded.Clear(); _preview = null;
            _statusErr = false;

            try
            {
                string absRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", _searchRoot));

                if (!Directory.Exists(absRoot))
                { SetStatus($"Not found: {absRoot}", true); return; }

                var skip = _skipWords.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();

                var files = Directory.GetFiles(absRoot, "*.cs", SearchOption.AllDirectories)
                    .Select(f => f.Replace('\\', '/'))
                    .Where(f => !(_skipEditor && f.Contains("/Editor/")))
                    .Where(f => !(_skipGen    && f.Contains("/Generated/")))
                    .Where(f => !skip.Any(k => f.Contains(k)))
                    .Where(f => !f.EndsWith("ComponentResolver.cs"))
                    .Where(f => !f.EndsWith("ComponentResolverMigrationTool.cs"))
                    .ToArray();

                for (int i = 0; i < files.Length; i++)
                {
                    if (i % 20 == 0)
                        EditorUtility.DisplayProgressBar("Scanning…",
                            Path.GetFileName(files[i]), (float)i / files.Length);

                    var r = ScanFile(files[i]);
                    if (r != null && r.Hits.Count > 0) _results.Add(r);
                }

                foreach (var r in _results) _selected.Add(r.FilePath);
                SetStatus($"{_results.Count} file(s)  ·  {_results.Sum(r => r.Hits.Count)} line(s)");
            }
            catch (Exception ex) { SetStatus($"Error: {ex.Message}", true); Debug.LogException(ex); }
            finally { EditorUtility.ClearProgressBar(); Repaint(); }
        }

        private FileScanResult ScanFile(string path)
        {
            var lines  = File.ReadAllLines(path);
            var result = new FileScanResult { FilePath = path, FileName = Path.GetFileName(path), Lines = lines };

            for (int i = 0; i < lines.Length; i++)
            {
                string line    = lines[i];
                string trimmed = line.TrimStart();

                // Skip comment lines
                if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("///"))
                    continue;

                // Skip lines already using ComponentResolver
                if (line.Contains("ComponentResolver"))
                    continue;

                // Find every GetComponent call on this line
                foreach (Match m in RxCall.Matches(line))
                {
                    string method = m.Groups["method"].Value;
                    string type   = m.Groups["type"].Value.Trim();
                    string args   = m.Groups["args"].Value.Trim();

                    // The receiver immediately before this call (or empty = "this")
                    string receiver = ExtractReceiver(line, m.Index);

                    // Replace ONLY the matched call expression within the line
                    string callExpr  = m.Value;                              // e.g. GetComponent<CapsuleCollider>()
                    string chainExpr = BuildInlineChain(receiver, method, type, args);
                    string replaced  = line.Replace(callExpr, chainExpr);

                    result.Hits.Add(new HitInfo
                    {
                        LineNumber   = i + 1,
                        Method       = method,
                        Type         = type,
                        OriginalLine = line,
                        ReplacedLine = replaced,
                    });

                    break; // one hit per line is enough (multi-call lines are rare)
                }
            }

            return result;
        }

        // ── Receiver: token immediately before the dot preceding the call ──────
        private static string ExtractReceiver(string line, int callIndex)
        {
            // Walk left from the call to find a dot
            int dot = callIndex - 1;
            while (dot >= 0 && line[dot] == ' ') dot--;
            if (dot < 0 || line[dot] != '.') return "";

            // Walk left to collect identifier characters
            int end   = dot;
            int start = end - 1;
            while (start > 0 &&
                   (char.IsLetterOrDigit(line[start - 1])
                    || line[start - 1] == '_'
                    || line[start - 1] == '.'))
                start--;

            string token = line.Substring(start, end - start).Trim();

            // Discard non-identifier tokens
            if (token.Length == 0
                || token == "var" || token == "return" || token == "new"
                || token.EndsWith(")") || token.EndsWith(">"))
                return "";

            return token;
        }

        // ── Build the replacement expression (multiline, same indentation) ─────
        /// <summary>
        /// Produces:
        ///   ComponentResolver.Find&lt;T&gt;(ctx)
        ///       .OnSelf()
        ///       ...
        ///       .Resolve()
        ///
        /// Note: NO semicolon here — the semicolon already exists at the end
        /// of the original line and is preserved by the plain string Replace.
        /// </summary>
        private static string BuildInlineChain(string receiver, string method, string type, string args)
        {
            string ctx = string.IsNullOrEmpty(receiver) ? "this" : receiver;

            // We don't know the source line indent here — use 8 spaces as a
            // readable continuation indent (the call lives inside an existing indent).
            const string pad = "\n        ";

            var sb = new StringBuilder();
            sb.Append($"ComponentResolver.Find<{type}>({ctx})");

            switch (method)
            {
                case "GetComponent":
                    sb.Append($"{pad}.OnSelf()");
                    sb.Append($"{pad}.InChildren()");
                    break;
                case "GetComponentInChildren":
                    sb.Append($"{pad}.OnSelf()");
                    sb.Append($"{pad}.InChildren()");
                    sb.Append($"{pad}.InParent()");
                    break;
                case "GetComponentInParent":
                    sb.Append($"{pad}.InParent()");
                    sb.Append($"{pad}.InRootChildren()");
                    break;
            }

            sb.Append($"{pad}.OrLogWarning(\"[Auto] {type} not found\")");
            sb.Append($"{pad}.Resolve()");

            return sb.ToString();
        }

        // ══════════════════════════════════════════════════════════════════════
        // APPLY
        // ══════════════════════════════════════════════════════════════════════
        private void ApplySelected()
        {
            if (!EditorUtility.DisplayDialog("Apply",
                $"Modify {_selected.Count} file(s).\nMake sure git is clean first.\n\nProceed?",
                "Apply", "Cancel")) return;

            int done = 0;
            foreach (var r in _results.Where(r => _selected.Contains(r.FilePath)))
            { ApplyFile(r); done++; }

            AssetDatabase.Refresh();
            SetStatus($"Applied {done} file(s).");
        }

        private void ApplyFile(FileScanResult result)
        {
            if (result.Applied) return;
            try
            {
                var lines = result.Lines.ToList();

                // Apply hits in REVERSE order so line numbers stay correct
                foreach (var h in result.Hits.OrderByDescending(h => h.LineNumber))
                {
                    int idx = h.LineNumber - 1;   // 0-based
                    lines[idx] = h.ReplacedLine;  // swap only this one line
                }

                // Ensure using directive
                EnsureUsing(lines, "using NightHunt.Utilities;");

                File.WriteAllLines(result.FilePath, lines, new UTF8Encoding(false));
                result.Applied = true;
                SetStatus($"Applied: {result.FileName}");
                Repaint();
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {result.FileName} — {ex.Message}", true);
                Debug.LogException(ex);
            }
        }

        private static void EnsureUsing(List<string> lines, string directive)
        {
            if (lines.Any(l => l.TrimEnd() == directive.TrimEnd())) return;
            int last = -1;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].TrimStart().StartsWith("using ")) last = i;
            lines.Insert(last >= 0 ? last + 1 : 0, directive);
        }

        private void ToggleAll(bool on)
        {
            _selected.Clear();
            if (on) foreach (var r in _results) _selected.Add(r.FilePath);
        }

        private void SetStatus(string msg, bool err = false) { _status = msg; _statusErr = err; }

        // ══════════════════════════════════════════════════════════════════════
        // STYLES
        // ══════════════════════════════════════════════════════════════════════
        private void EnsureStyles()
        {
            if (_stylesOk) return; _stylesOk = true;

            _sTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _sTitle.normal.textColor = new Color(.9f,.85f,1f);

            _sBtn = new GUIStyle(EditorStyles.miniButton)
                { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6,6,3,3) };

            _sHit = new GUIStyle(EditorStyles.helpBox)
                { padding = new RectOffset(4,4,1,1), margin = new RectOffset(20,4,1,1) };

            _sOld = new GUIStyle(EditorStyles.textArea) { wordWrap = false };
            _sOld.normal.textColor  = new Color(.95f,.4f,.4f);
            _sOld.focused.textColor = _sOld.normal.textColor;

            _sNew = new GUIStyle(_sOld);
            _sNew.normal.textColor  = new Color(.4f,.95f,.5f);
            _sNew.focused.textColor = _sNew.normal.textColor;

            _sBadge = new GUIStyle(EditorStyles.miniLabel)
                { alignment = TextAnchor.MiddleCenter, padding = new RectOffset(4,4,1,1) };
            _sBadge.normal.textColor = Color.white;

            _sBox = new GUIStyle(EditorStyles.helpBox)
                { padding = new RectOffset(8,8,6,6), margin = new RectOffset(0,0,2,2) };
        }

        // ══════════════════════════════════════════════════════════════════════
        // DATA
        // ══════════════════════════════════════════════════════════════════════
        private class FileScanResult
        {
            public string        FilePath;
            public string        FileName;
            public string[]      Lines;
            public List<HitInfo> Hits    = new List<HitInfo>();
            public bool          Applied;
        }

        private class HitInfo
        {
            public int    LineNumber;
            public string Method;
            public string Type;
            public string OriginalLine;
            public string ReplacedLine;
        }
    }
}