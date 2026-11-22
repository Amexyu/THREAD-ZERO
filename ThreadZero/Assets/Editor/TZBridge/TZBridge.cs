using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace ThreadZero
{
    // TZ Bridge：让 Claude 只靠读写文件就能遥控编辑器（编译、跑测试剧本、截图），Win / Mac 通用。
    // 通信目录 <项目>/Logs/TZBridge（被 git 忽略）：
    //   inbox/<单号>.txt   指令。第一行是命令：ping / compile / refresh / play / menu <菜单路径>
    //   inbox/STOP         立刻停止正在跑的剧本
    //   out/<单号>/        结果：result.txt，play 还有 log.txt、截图、dump
    //   status.txt         心跳，约 2 秒刷新一次
    //   console.txt        编辑器最近 300 条日志
    //   compile_errors.txt 最近一次编译的错误（没有错误时为空）
    // 剧本写法和用法见 Docs/TZBridge.md。运行时部分在 Assets/Scripts/Dev/TZAutoTest.cs。
    [InitializeOnLoad]
    public static class TZBridge
    {
        private const string KeyCompile = "TZBridge.compile", KeyCompileWarnings = "TZBridge.compileWarnings";
        private const string KeyMuted = "TZBridge.muted";   // v0.20 自动测试期间把编辑器的声音整个静音，收尾时还原
        private const string KeyPlay = "TZBridge.play", KeyPlayDeadline = "TZBridge.playDeadline", KeyPlayStartedAt = "TZBridge.playStartedAt", KeyPlayEntered = "TZBridge.playEntered";
        private static readonly string Root, Inbox, Archive, OutRoot, StatusFile, ConsoleFile, ErrorsFile, RequestFile, StopFile;
        private static readonly List<string> compileErrors = new List<string>(), compileWarningList = new List<string>();   // v0.20 警告原文也记下来（compile_warnings.txt），原来只有个数
        private static readonly List<string> console = new List<string>();
        private static int compileWarnings, lastErrorCount;
        private static double nextPoll, nextStatus, compileRequestedAt = -1, compileFinishedAt = -1;
        private static bool consoleDirty;
        private static string lastCommand = "-", lastStatus = "-";

        static TZBridge()
        {
            Root = Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(Application.dataPath, ".."), "Logs"), "TZBridge"));
            Inbox = Path.Combine(Root, "inbox");
            Archive = Path.Combine(Inbox, "done");
            OutRoot = Path.Combine(Root, "out");
            StatusFile = Path.Combine(Root, "status.txt");
            ConsoleFile = Path.Combine(Root, "console.txt");
            ErrorsFile = Path.Combine(Root, "compile_errors.txt");
            RequestFile = Path.Combine(Root, "play_request.txt");
            StopFile = Path.Combine(Inbox, "STOP");
            try
            {
                Directory.CreateDirectory(Archive);
                Directory.CreateDirectory(OutRoot);
            }
            catch (Exception e) { Debug.LogWarning("[TZBridge] 建不了通信目录：" + e.Message); }
            Prune();
            EditorApplication.update += Tick;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Application.logMessageReceived += OnLog;
            ResumeCompile();
            WriteStatus();
        }

        [MenuItem("Thread Zero/TZ Bridge/打开通信文件夹")]
        private static void OpenFolder() { EditorUtility.RevealInFinder(StatusFile); }

        // ---------- 主循环 ----------

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextPoll) return;
            nextPoll = now + .5;
            try
            {
                MonitorPlay(now);
                MonitorCompile(now);
                if (now >= nextStatus)
                {
                    nextStatus = now + 2;
                    WriteStatus();
                }
                if (BusyReason().Length == 0)
                {
                    if (File.Exists(StopFile)) TryDelete(StopFile);   // 没有在跑的剧本，STOP 作废
                    string next = NextCommand();
                    if (next != null) Run(next);
                }
            }
            catch (Exception e) { Debug.LogWarning("[TZBridge] " + e); }
        }

        private static string BusyReason()
        {
            if (SessionState.GetString(KeyCompile, "").Length > 0) return "compile";
            if (SessionState.GetString(KeyPlay, "").Length > 0) return "play";
            if (EditorApplication.isCompiling) return "compiling";
            if (EditorApplication.isUpdating) return "importing";
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "user_play_mode";
            return "";
        }

        private static string NextCommand()
        {
            if (!Directory.Exists(Inbox)) return null;
            string[] files = Directory.GetFiles(Inbox, "*.txt");
            if (files.Length == 0) return null;
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string f in files)
                if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(f)).TotalSeconds > .5) return f;   // 刚写入的文件等半秒再读
            return null;
        }

        private static void Run(string file)
        {
            string id = Path.GetFileNameWithoutExtension(file);
            string text;
            try { text = File.ReadAllText(file, Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("[TZBridge] 读不了指令 " + file + "：" + e.Message); return; }
            string archived = Path.Combine(Archive, Path.GetFileName(file));
            TryDelete(archived);
            try { File.Move(file, archived); }
            catch (Exception) { TryDelete(file); }

            string first = "";
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                string s = raw.Trim();
                if (s.Length > 0 && !s.StartsWith("#")) { first = s; break; }
            }
            int space = first.IndexOf(' ');
            string cmd = (space < 0 ? first : first.Substring(0, space)).ToLowerInvariant();
            string rest = space < 0 ? "" : first.Substring(space + 1).Trim();
            string outDir = Path.Combine(OutRoot, id);
            try
            {
                if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
                Directory.CreateDirectory(outDir);
            }
            catch (Exception) { }
            lastCommand = id + " " + cmd;
            lastStatus = "running";

            switch (cmd)
            {
                case "ping":
                    WriteResult(id, "ok", StatusText());
                    break;
                case "refresh":
                    AssetDatabase.Refresh();
                    WriteResult(id, "ok", "已刷新资源\n");
                    break;
                case "compile":
                    StartCompile(id);
                    break;
                case "play":
                    StartPlay(id, text, outDir);
                    break;
                case "menu":
                    {
                        bool ok = EditorApplication.ExecuteMenuItem(rest);
                        WriteResult(id, ok ? "ok" : "error", ok ? "已执行菜单：" + rest + "\n" : "找不到菜单：" + rest + "\n");
                        break;
                    }
                default:
                    WriteResult(id, "error", "不认识的命令：" + cmd + "\n");
                    break;
            }
        }

        // ---------- 编译 ----------

        private static void StartCompile(string id)
        {
            compileErrors.Clear(); compileWarningList.Clear();
            compileWarnings = 0;
            SessionState.SetString(KeyCompile, id);
            compileRequestedAt = EditorApplication.timeSinceStartup;
            compileFinishedAt = -1;
            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
        }

        private static void OnCompilationStarted(object context)
        {
            compileErrors.Clear(); compileWarningList.Clear();
            compileWarnings = 0;
        }

        private static void OnAssemblyCompiled(string assembly, CompilerMessage[] messages)
        {
            foreach (CompilerMessage m in messages)
            {
                if (m.type == CompilerMessageType.Error) compileErrors.Add(m.message);
                else if (m.type == CompilerMessageType.Warning) { compileWarnings++; compileWarningList.Add(m.message); }
            }
        }

        private static void OnCompilationFinished(object context)
        {
            compileFinishedAt = EditorApplication.timeSinceStartup;
            lastErrorCount = compileErrors.Count;
            try { File.WriteAllText(ErrorsFile, compileErrors.Count == 0 ? "" : string.Join("\n", compileErrors.ToArray()) + "\n", new UTF8Encoding(false)); }
            catch (Exception) { }
            try { File.WriteAllText(Path.Combine(Root, "compile_warnings.txt"), compileWarningList.Count == 0 ? "" : string.Join("\n", compileWarningList.ToArray()) + "\n", new UTF8Encoding(false)); }
            catch (Exception) { }
            string id = SessionState.GetString(KeyCompile, "");
            if (id.Length == 0) return;
            if (compileErrors.Count > 0)
            {
                SessionState.EraseString(KeyCompile);
                WriteResult(id, "error", "编译失败，" + compileErrors.Count + " 个错误：\n" + string.Join("\n", compileErrors.ToArray()) + "\n");
            }
            else SessionState.SetInt(KeyCompileWarnings, compileWarnings);   // 没有错误：马上会重新加载脚本，结果在 ResumeCompile 里写
        }

        private static void ResumeCompile()
        {
            string id = SessionState.GetString(KeyCompile, "");
            if (id.Length == 0) return;
            SessionState.EraseString(KeyCompile);
            int warnings = SessionState.GetInt(KeyCompileWarnings, 0);
            SessionState.EraseInt(KeyCompileWarnings);
            WriteResult(id, "ok", "编译通过，脚本已重新加载。warnings " + warnings + "\n");
        }

        private static void MonitorCompile(double now)
        {
            string id = SessionState.GetString(KeyCompile, "");
            if (id.Length == 0 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            double since = Math.Max(compileFinishedAt, compileRequestedAt);
            if (since < 0 || now - since < 20) return;
            // 20 秒内既没报错也没重新加载：多半是没有需要编译的改动。
            SessionState.EraseString(KeyCompile);
            if (compileErrors.Count > 0) WriteResult(id, "error", "编译失败，" + compileErrors.Count + " 个错误：\n" + string.Join("\n", compileErrors.ToArray()) + "\n");
            else WriteResult(id, EditorUtility.scriptCompilationFailed ? "error" : "ok",
                "没有触发重新加载（可能没有改动）。scriptCompilationFailed=" + (EditorUtility.scriptCompilationFailed ? "yes" : "no") + "\n");
        }

        // ---------- 跑剧本 ----------

        private static void StartPlay(string id, string text, string outDir)
        {
            if (EditorUtility.scriptCompilationFailed)
            {
                WriteResult(id, "error", "脚本有编译错误，进不了 Play。先发 compile 看错误。\n");
                return;
            }
            float timeout = 120;
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                string s = raw.Trim();
                float v;
                if (s.StartsWith("timeout ") && float.TryParse(s.Substring(8).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) timeout = v;
            }
            try { File.WriteAllText(RequestFile, "id " + id + "\nout " + outDir + "\n" + text, new UTF8Encoding(false)); }
            catch (Exception e)
            {
                WriteResult(id, "error", "写不了剧本文件：" + e.Message + "\n");
                return;
            }
            double now = EditorApplication.timeSinceStartup;
            SessionState.SetString(KeyPlay, id);
            SessionState.SetFloat(KeyPlayDeadline, (float)(now + timeout + 30));
            SessionState.SetFloat(KeyPlayStartedAt, (float)now);
            SessionState.SetBool(KeyPlayEntered, false);
            // v0.20 剧本跑在用户自己的电脑上：运行时 TZAutoTest 会把音量压到 0，这里再把编辑器的游戏声音整个关掉兜一层，收尾时还原
            if (!EditorUtility.audioMasterMute) { EditorUtility.audioMasterMute = true; SessionState.SetBool(KeyMuted, true); }
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(KeyPlay, "").Length > 0)
                SessionState.SetBool(KeyPlayEntered, true);
        }

        private static void MonitorPlay(double now)
        {
            string id = SessionState.GetString(KeyPlay, "");
            if (id.Length == 0) return;
            string outDir = Path.Combine(OutRoot, id);
            if (File.Exists(StopFile))
            {
                TryDelete(StopFile);
                Note(outDir, "editor: 收到 STOP，停止 Play");
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                return;
            }
            if (EditorApplication.isPlaying)
            {
                SessionState.SetBool(KeyPlayEntered, true);
                if (File.Exists(Path.Combine(outDir, "done.txt"))) { EditorApplication.ExitPlaymode(); return; }
                if (now > SessionState.GetFloat(KeyPlayDeadline, float.MaxValue))
                {
                    Note(outDir, "editor: 超时，强制停止 Play");
                    EditorApplication.ExitPlaymode();
                }
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;   // 正在进入或退出 Play
            bool entered = SessionState.GetBool(KeyPlayEntered, false);
            if (!entered && now - SessionState.GetFloat(KeyPlayStartedAt, 0) < 15) return;   // 还没进去，再等等
            // 已经回到编辑模式：收尾
            if (SessionState.GetBool(KeyMuted, false)) { EditorUtility.audioMasterMute = false; SessionState.EraseBool(KeyMuted); }
            SessionState.EraseString(KeyPlay);
            SessionState.EraseFloat(KeyPlayDeadline);
            SessionState.EraseFloat(KeyPlayStartedAt);
            SessionState.EraseBool(KeyPlayEntered);
            TryDelete(RequestFile);
            string result = Path.Combine(outDir, "result.txt");
            if (File.Exists(result)) lastStatus = ReadStatus(result);
            else WriteResult(id, "error", entered ? "Play 结束了，但运行时没有写结果（TZAutoTest 没启动？）\n" : "没能进入 Play（编译错误或被取消）\n");
            WriteStatus();
        }

        // ---------- 状态和日志 ----------

        private static void OnLog(string message, string stack, LogType type)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + " [" + type + "] " + message;
            if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert) && !string.IsNullOrEmpty(stack)) line += "\n" + stack.TrimEnd();
            console.Add(line);
            if (console.Count > 300) console.RemoveRange(0, console.Count - 300);
            consoleDirty = true;
        }

        private static void WriteStatus()
        {
            try
            {
                File.WriteAllText(StatusFile, StatusText(), new UTF8Encoding(false));
                if (consoleDirty)
                {
                    consoleDirty = false;
                    File.WriteAllText(ConsoleFile, string.Join("\n", console.ToArray()) + "\n", new UTF8Encoding(false));
                }
            }
            catch (Exception) { }
        }

        private static string StatusText()
        {
            string busy = BusyReason();
            var sb = new StringBuilder();
            sb.Append("time ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
            sb.Append("unity ").Append(Application.unityVersion).Append('\n');
            sb.Append("project ").Append(Path.GetFullPath(Path.Combine(Application.dataPath, ".."))).Append('\n');
            sb.Append("playing ").Append(EditorApplication.isPlaying ? "yes" : "no").Append('\n');
            sb.Append("compiling ").Append(EditorApplication.isCompiling ? "yes" : "no").Append('\n');
            sb.Append("compile_failed ").Append(EditorUtility.scriptCompilationFailed ? "yes" : "no").Append('\n');
            sb.Append("last_compile_errors ").Append(lastErrorCount).Append('\n');
            sb.Append("busy ").Append(busy.Length > 0 ? busy : "no").Append('\n');
            sb.Append("last ").Append(lastCommand).Append(" -> ").Append(lastStatus).Append('\n');
            return sb.ToString();
        }

        private static void WriteResult(string id, string status, string body)
        {
            string dir = Path.Combine(OutRoot, id);
            try
            {
                Directory.CreateDirectory(dir);
                string head = "id " + id + "\nstatus " + status + "\ntime " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n---\n";
                File.WriteAllText(Path.Combine(dir, "result.txt"), head + body, new UTF8Encoding(false));
            }
            catch (Exception e) { Debug.LogWarning("[TZBridge] 写不了结果：" + e.Message); }
            lastStatus = status;
        }

        private static void Note(string dir, string text)
        {
            try
            {
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "editor.txt"), DateTime.Now.ToString("HH:mm:ss") + " " + text + "\n", new UTF8Encoding(false));
            }
            catch (Exception) { }
        }

        private static string ReadStatus(string resultFile)
        {
            try
            {
                foreach (string line in File.ReadAllLines(resultFile))
                    if (line.StartsWith("status ")) return line.Substring(7).Trim();
            }
            catch (Exception) { }
            return "?";
        }

        // 只留最近 40 次的结果文件夹和最近 200 条已执行的指令，免得截图越积越多。
        private static void Prune()
        {
            try
            {
                string[] runs = Directory.GetDirectories(OutRoot);
                if (runs.Length > 40)
                {
                    Array.Sort(runs, (a, b) => Directory.GetLastWriteTimeUtc(a).CompareTo(Directory.GetLastWriteTimeUtc(b)));
                    for (int i = 0; i < runs.Length - 40; i++) Directory.Delete(runs[i], true);
                }
                string[] old = Directory.GetFiles(Archive, "*.txt");
                if (old.Length > 200)
                {
                    Array.Sort(old, StringComparer.Ordinal);
                    for (int i = 0; i < old.Length - 200; i++) File.Delete(old[i]);
                }
            }
            catch (Exception) { }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception) { }
        }
    }
}
