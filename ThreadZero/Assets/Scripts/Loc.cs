using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ThreadZero
{
    /// v0.21 四语（中 / 日 / 英 / 韩）。界面代码照旧写中文，Ui.Text 画字前交给 Loc.Tr 换成当前语言：
    ///   1. 整句：中文原句 → 译文（Resources/Loc/ja.txt、en.txt、ko.txt；一行一条「中文<Tab>译文」，key 就是中文原文）；
    ///   2. 模板：拼接出来的句子写成正则（「re:升级到 Lv\.(\d+)<Tab>Lv.{1} に強化」），括号里抓到的部分也会再翻译一遍（装备名等）；
    ///   3. 片段：都没命中时，把句子里的中文按最长匹配逐段查表替换（兜底，英文 / 韩文会补空格、换标点）。
    /// 还有没翻到的中文片段记下来（开发用：Logs/Loc/missing_xx.txt），按它补表。中文模式不查表，零开销。
    public static class Loc
    {
        public enum Lang { Zh, Ja, En, Ko }
        public static readonly string[] Codes = { "zh", "ja", "en", "ko" };
        public static readonly string[] Names = { "中文", "日本語", "English", "한국어" };
        public static Lang Current { get; private set; }
        public static bool IsZh { get { return Current == Lang.Zh; } }
        public static bool Spaced { get { return Current == Lang.En || Current == Lang.Ko; } }   // 英文、韩文词和词之间要空格

        private static readonly Dictionary<string, string> exact = new Dictionary<string, string>();
        private static readonly List<KeyValuePair<Regex, string>> patterns = new List<KeyValuePair<Regex, string>>();
        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        private static readonly HashSet<string> missing = new HashSet<string>();
        private static readonly HashSet<string> names = new HashSet<string>(Names);
        private static int maxKey = 1;
        private static bool loaded;
        private const string Pref = "tz_lang";

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            int saved = PlayerPrefs.GetInt(Pref, 0);
            Current = (Lang)Mathf.Clamp(saved, 0, Codes.Length - 1);
            Load();
        }
        public static void Set(Lang lang)
        {
            EnsureLoaded();
            if (lang == Current) return;
            Current = lang; PlayerPrefs.SetInt(Pref, (int)lang); PlayerPrefs.Save();
            Load();
            Ui.ResetFonts();
        }
        public static Lang Get() { EnsureLoaded(); return Current; }

        private static void Load()
        {
            exact.Clear(); patterns.Clear(); cache.Clear(); maxKey = 1;
            if (Current == Lang.Zh) return;
            var asset = Resources.Load<TextAsset>("Loc/" + Codes[(int)Current]);
            if (asset == null) { Debug.LogWarning("THREAD ZERO: 缺少 Resources/Loc/" + Codes[(int)Current] + ".txt"); return; }
            foreach (string raw in asset.text.Replace("\r", "").Split('\n'))
            {
                if (raw.Length == 0 || raw[0] == '#') continue;
                int tab = raw.IndexOf('\t'); if (tab <= 0) continue;
                string k = Unescape(raw.Substring(0, tab)), v = Unescape(raw.Substring(tab + 1));
                if (k.StartsWith("re:"))
                {
                    try { patterns.Add(new KeyValuePair<Regex, string>(new Regex("^" + k.Substring(3) + "$"), v)); }
                    catch (System.ArgumentException e) { Debug.LogWarning("THREAD ZERO: 翻译模板写错了：" + k + "  " + e.Message); }
                }
                else { exact[k] = v; if (k.Length > maxKey) maxKey = k.Length; }
            }
        }
        private static string Unescape(string s) { return s.IndexOf('\\') < 0 ? s : s.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\"); }

        /// 把一句中文换成当前语言（没有中文的字符串原样返回）。
        public static string Tr(string s)
        {
            EnsureLoaded();
            if (Current == Lang.Zh || string.IsNullOrEmpty(s) || !HasCjk(s) || names.Contains(s)) return s;
            string r;
            if (cache.TryGetValue(s, out r)) return r;
            r = Translate(s);
            if (cache.Count > 6000) cache.Clear();
            cache[s] = r;
            return r;
        }
        /// 带参数的句子：Loc.F("升级到 Lv.{0}", n)。先翻模板再填参数（参数里的中文也翻）。
        public static string F(string template, params object[] args)
        {
            string t = Tr(template);
            for (int i = 0; i < args.Length; i++) args[i] = args[i] is string ? Tr((string)args[i]) : args[i];
            try { return string.Format(t, args); } catch (System.FormatException) { return string.Format(template, args); }
        }

        private static string Translate(string s)
        {
            string r;
            if (exact.TryGetValue(s, out r)) return r;
            string trimmed = s.Trim();
            if (trimmed.Length != s.Length && exact.TryGetValue(trimmed, out r)) return s.Substring(0, s.IndexOf(trimmed[0])) + r + s.Substring(s.IndexOf(trimmed[0]) + trimmed.Length);
            foreach (var p in patterns)
            {
                Match m = p.Key.Match(s);
                if (!m.Success) continue;
                string v = p.Value;
                for (int g = m.Groups.Count - 1; g >= 1; g--) v = v.Replace("{" + g + "}", Tr(m.Groups[g].Value));
                return v;
            }
            return Fragments(s);
        }

        private static bool IsCjk(char c)
        {
            return (c >= '一' && c <= '鿿') || (c >= '㐀' && c <= '䶿') || (c >= '　' && c <= '〿') || (c >= '＀' && c <= '￯') || c == '·' || c == '…' || c == '—' || c == '～';
        }
        private static bool IsHan(char c) { return (c >= '一' && c <= '鿿') || (c >= '㐀' && c <= '䶿'); }
        public static bool HasCjk(string s) { foreach (char c in s) if (IsHan(c)) return true; return false; }

        /// 兜底：按最长匹配把中文片段逐段换掉；换不掉的片段留中文并记进「没翻到」。
        private static string Fragments(string s)
        {
            var sb = new StringBuilder(s.Length * 2);
            bool untranslated = false;
            int i = 0;
            while (i < s.Length)
            {
                if (!IsCjk(s[i])) { sb.Append(s[i]); i++; continue; }
                int len = 0; string hit = null;
                for (int l = Mathf.Min(maxKey, s.Length - i); l >= 1; l--)
                {
                    string piece = s.Substring(i, l);
                    if (exact.TryGetValue(piece, out hit)) { len = l; break; }
                }
                if (len > 0) { AppendWord(sb, hit, s, i + len); i += len; continue; }
                char c = s[i];
                string punct = Spaced ? Punct(c) : null;
                if (punct != null) { AppendWord(sb, punct, s, i + 1); i++; continue; }
                if (IsHan(c)) untranslated = true;
                sb.Append(c); i++;
            }
            if (untranslated && Application.isEditor) missing.Add(s);
            return Spaced ? Regex.Replace(sb.ToString(), " {2,}", " ").Trim() : sb.ToString();
        }
        private static void AppendWord(StringBuilder sb, string word, string s, int next)
        {
            if (Spaced && word.Length > 0 && sb.Length > 0 && char.IsLetterOrDigit(sb[sb.Length - 1]) && char.IsLetterOrDigit(word[0])) sb.Append(' ');
            sb.Append(word);
            if (Spaced && word.Length > 0 && next < s.Length && char.IsLetterOrDigit(word[word.Length - 1]) && (char.IsLetterOrDigit(s[next]) || IsHan(s[next]))) sb.Append(' ');
        }
        private static string Punct(char c)
        {
            switch (c)
            {
                case '，': case '、': return ", ";
                case '。': return ". ";
                case '：': return ": ";
                case '；': return "; ";
                case '！': return "! ";
                case '？': return "? ";
                case '（': return " (";
                case '）': return ") ";
                case '「': case '」': case '“': case '”': return "\"";
                case '·': return " · ";
                case '…': return "...";
                case '—': return " - ";
                case '～': return "~";
                case '　': return " ";
                default: return null;
            }
        }

        /// 开发用：把这次运行里没翻到的句子写进 Logs/Loc/missing_xx.txt（追加、去重）。
        public static void DumpMissing()
        {
            if (missing.Count == 0 || Current == Lang.Zh) return;
            try
            {
                string dir = Path.Combine(Application.dataPath, "..", "Logs", "Loc"); Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "missing_" + Codes[(int)Current] + ".txt");
                var all = new SortedSet<string>(missing);
                if (File.Exists(file)) foreach (string line in File.ReadAllLines(file, Encoding.UTF8)) if (line.Length > 0) all.Add(line);
                var lines = new List<string>(); foreach (string m in all) lines.Add(m.Replace("\n", "\\n"));
                File.WriteAllLines(file, lines.ToArray(), new UTF8Encoding(false));
            }
            catch (System.Exception e) { Debug.LogWarning("THREAD ZERO: 写没翻到的句子失败：" + e.Message); }
        }
        public static int MissingCount { get { return missing.Count; } }
    }
}
