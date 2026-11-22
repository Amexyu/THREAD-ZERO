using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ThreadZero
{
    // TZ Bridge 的运行时部分。平时什么都不做，只有找到测试剧本时才启动：
    // · 编辑器里：Assets/Editor/TZBridge 把剧本写到 <项目>/Logs/TZBridge/play_request.txt，再进入 Play。
    // · 打包版本：命令行参数 -tzAutotest <剧本文件>。
    // 剧本逐行执行（等待、按键、点击、读写字段、调用方法、截图、断言），写法见 Docs/TZBridge.md。
    // 默认切到测试存档（tz_test_save = 1），并先备份测试存档；跑完把存档槽和测试存档都还原，不碰正式存档。
    public sealed class TZAutoTest : MonoBehaviour
    {
        private const string SaveSlotKey = "tz_test_save";
        private const string TestSaveFile = "thread-zero-save-test.json";
        private const string SlotBackupKey = "tz_autotest_slot";   // 跑剧本前的存档槽，跑完（或下次启动时）还原
        private const string KeepSuffix = ".tzkeep", RunMarker = ".tzrun";
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static string pendingText;
        private static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();

        private readonly List<string> steps = new List<string>();
        private readonly StringBuilder report = new StringBuilder();
        private readonly StringBuilder logText = new StringBuilder();
        private string id = "manual", outDir, status = "ok";
        private float timeout = 120;
        private int done, failures, logErrors, logWarnings;
        private bool finished;
        private Component app;
        private MethodInfo appOnGui;
        private readonly Queue<Event> injectQueue = new Queue<Event>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            pendingText = null; typeCache.Clear();
            RestoreSaves();   // 上次剧本没正常结束时留下的备份，先还原
            string path = RequestPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try { pendingText = File.ReadAllText(path, Encoding.UTF8); }
            catch (Exception e) { Debug.LogWarning("[TZAutoTest] 读不了剧本：" + e.Message); return; }
#if UNITY_EDITOR
            try { File.Delete(path); } catch (Exception) { }
#endif
            bool mainSave = false, fresh = false;
            foreach (string line in Lines(pendingText))
            {
                string[] t = Split(line);
                if (t.Length == 0) continue;
                if (t[0] == "save" && t.Length > 1 && t[1] == "main") mainSave = true;
                else if (t[0] == "fresh") fresh = true;
            }
            if (!mainSave)
            {
                PlayerPrefs.SetInt(SlotBackupKey, PlayerPrefs.GetInt(SaveSlotKey, 0));
                PlayerPrefs.SetInt(SaveSlotKey, 1);
                PlayerPrefs.Save();
                BackupTestSave(fresh);
            }
            var go = new GameObject("TZAutoTest");
            DontDestroyOnLoad(go);
            go.AddComponent<TZAutoTest>();
        }

        private static string RequestPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-tzAutotest");
            if (at >= 0 && at + 1 < args.Length) return args[at + 1];
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(Path.Combine(Path.Combine(Application.dataPath, ".."), "Logs"), Path.Combine("TZBridge", "play_request.txt")));
#else
            return null;
#endif
        }

        // v0.20 自动测试静音：剧本跑在用户自己的电脑上，游戏的提示音（升级、掉落、首领出场）会从扬声器响出来。
        // 跑剧本期间把总音量压到 0，退出 Play（或打包版退出）时还原，平时自己玩不受影响。
        private float savedVolume = -1;

        private void Awake()
        {
            savedVolume = AudioListener.volume; AudioListener.volume = 0;
            GameApp.SuppressTips = true;   // v0.20 新手提示默认不弹，免得盖住截图（要测提示的剧本 set GameApp.SuppressTips false）
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
            string text = pendingText ?? "";
            pendingText = null;
            foreach (string line in Lines(text))
            {
                string[] t = Split(line);
                if (t.Length == 0) continue;
                string key = t[0].ToLowerInvariant();
                if (key == "play" || key == "save" || key == "fresh") continue;
                if (key == "id") { if (t.Length > 1) id = t[1]; continue; }
                if (key == "out") { outDir = line.Substring(3).Trim().Trim('"'); continue; }
                if (key == "timeout") { timeout = Number(Arg(t, 1, "120"), 120); continue; }
                steps.Add(line);
            }
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.Combine(Application.persistentDataPath, "tzautotest"), Safe(id));
            try { Directory.CreateDirectory(outDir); }
            catch (Exception e) { Debug.LogWarning("[TZAutoTest] 建不了输出目录：" + e.Message); }
        }

        // 收尾时先把所有声源停掉并静音，再还原音量：Unity 不保证销毁顺序，还原音量那一刻要是还有提示音没放完，
        // 就会漏出半截（正弦短音从中间突然出声，听起来是“很怪的一下”，见 Docs/提示音排查_2026-11-22.md）。
        private static void SilenceAll()
        {
            foreach (AudioSource s in FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
                if (s != null) { s.Stop(); s.mute = true; }
        }

        private void OnDestroy()
        {
            SilenceAll();
            if (savedVolume >= 0) { AudioListener.volume = savedVolume; savedVolume = -1; }   // 还原自动测试前的音量
            GameApp.SuppressTips = false;
            Application.logMessageReceived -= OnLog;
            if (!finished)
            {
                finished = true;
                status = "aborted";
                Note("被中断（Play 停止或对象被销毁）");
                WriteReport();
            }
            RestoreSaves();   // 放在 OnDestroy：GameApp 退出时最后一次存档（OnApplicationQuit）已经写完了
        }

        private void OnLog(string message, string stack, LogType type)
        {
            bool bad = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            if (bad) logErrors++;
            else if (type == LogType.Warning) logWarnings++;
            logText.Append(DateTime.Now.ToString("HH:mm:ss")).Append(" [").Append(type.ToString()).Append("] ").Append(message).Append('\n');
            if (bad && !string.IsNullOrEmpty(stack)) logText.Append(stack).Append('\n');
            if (logText.Length > 400000) logText.Remove(0, 100000);
        }

        private IEnumerator Start()
        {
            float begin = Time.realtimeSinceStartup;
            Note("start " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  screen " + Screen.width + "x" + Screen.height + "  steps " + steps.Count);
            for (int i = 0; i < steps.Count; i++)
            {
                if (Time.realtimeSinceStartup - begin > timeout)
                {
                    status = "timeout";
                    Note("超时：停在第 " + (i + 1) + " 步 " + steps[i]);
                    break;
                }
                if (app == null)
                {
                    float until = Time.realtimeSinceStartup + 15;
                    app = FindApp();
                    while (app == null && Time.realtimeSinceStartup < until)
                    {
                        yield return new WaitForSecondsRealtime(.25f);
                        app = FindApp();
                    }
                    if (app == null) { status = "error"; Note("找不到 GameApp"); break; }
                }
                string line = steps[i];
                string[] t = Split(line);
                if (t.Length == 0) continue;
                string cmd = t[0].ToLowerInvariant();
                if (cmd == "end" || cmd == "quit") break;

                if (cmd == "wait")
                {
                    yield return new WaitForSecondsRealtime(Number(Arg(t, 1, "1"), 1));
                    done++;
                    continue;
                }
                if (cmd == "frames")
                {
                    int n = (int)Number(Arg(t, 1, "1"), 1);
                    for (int f = 0; f < n; f++) yield return null;
                    done++;
                    continue;
                }
                if (cmd == "key" || cmd == "click")
                {
                    Event down = null, up = null;
                    bool built = false;
                    try
                    {
                        if (cmd == "key")
                        {
                            down = KeyEvent(EventType.KeyDown, t);
                            up = KeyEvent(EventType.KeyUp, t);
                        }
                        else
                        {
                            var pos = new Vector2(Number(Arg(t, 1, "0"), 0), Number(Arg(t, 2, "0"), 0));
                            down = MouseEvent(EventType.MouseDown, pos);
                            up = MouseEvent(EventType.MouseUp, pos);
                        }
                        built = true;
                    }
                    catch (Exception e) { failures++; Note("FAIL " + line + " -> " + Describe(e)); }
                    if (!built) continue;
                    bool sent = Inject(down);
                    int guard = 0;
                    while (sent && injectQueue.Count > 0 && guard++ < 60) yield return null;
                    if (sent) Inject(up);
                    guard = 0;
                    while (sent && injectQueue.Count > 0 && guard++ < 60) yield return null;
                    yield return null;
                    if (sent) { done++; Note("ok   " + line); }
                    else failures++;
                    continue;
                }
                if (cmd == "shot")
                {
                    string name = Safe(Arg(t, 1, "shot" + (i + 1)));
                    int size = Mathf.Max(1, (int)Number(Arg(t, 2, "1"), 1));
                    string file = Path.Combine(outDir, name + ".png");
                    bool started = false;
                    try
                    {
                        if (File.Exists(file)) File.Delete(file);
                        ScreenCapture.CaptureScreenshot(file, size);
                        started = true;
                    }
                    catch (Exception e) { failures++; Note("FAIL " + line + " -> " + Describe(e)); }
                    if (!started) continue;
                    float until = Time.realtimeSinceStartup + 5;
                    while (!File.Exists(file) && Time.realtimeSinceStartup < until) yield return null;
                    yield return null;
                    yield return null;
                    if (File.Exists(file)) { done++; Note("shot " + name + ".png  " + (Screen.width * size) + "x" + (Screen.height * size)); }
                    else { failures++; Note("FAIL shot " + name + "：5 秒内没有生成（Game 视图可能没在显示）"); }
                    continue;
                }
                if (cmd == "burst")
                {
                    // burst <名字> <张数> <间隔秒> [宽度]：连拍缩小的 jpg，用来拼 GIF 看动效
                    string name = Safe(Arg(t, 1, "burst"));
                    int count = Mathf.Clamp((int)Number(Arg(t, 2, "30"), 30), 1, 300);
                    float every = Mathf.Max(0, Number(Arg(t, 3, ".1"), .1f));
                    int width = Mathf.Clamp((int)Number(Arg(t, 4, "800"), 800), 64, 4096);
                    int saved = 0;
                    for (int f = 0; f < count; f++)
                    {
                        yield return new WaitForEndOfFrame();
                        bool ok = false;
                        try { SaveFrame(Path.Combine(outDir, name + "_" + f.ToString("000") + ".jpg"), width); ok = true; }
                        catch (Exception e) { Note("FAIL burst 第 " + f + " 张：" + Describe(e)); }
                        if (!ok) break;
                        saved++;
                        if (every > 0) yield return new WaitForSecondsRealtime(every);
                    }
                    if (saved == count) { done++; Note("burst " + name + " " + saved + " 张"); }
                    else failures++;
                    continue;
                }
                if (cmd == "waitfor")
                {
                    float until = Time.realtimeSinceStartup + Number(Arg(t, 4, "20"), 20);
                    bool ok = false;
                    string actual = "";
                    while (true)
                    {
                        try { ok = Check(t, out actual); }
                        catch (Exception e) { actual = Describe(e); }
                        if (ok || Time.realtimeSinceStartup >= until) break;
                        yield return new WaitForSecondsRealtime(.2f);
                    }
                    if (ok) { done++; Note("ok   " + line + "  (= " + actual + ")"); }
                    else { failures++; Note("FAIL " + line + "  (当前 " + actual + ")"); }
                    continue;
                }
                try { Execute(cmd, t, line); done++; }
                catch (Exception e) { failures++; Note("FAIL " + line + " -> " + Describe(e)); }
                yield return null;
            }
            Finish();
        }

        private void Execute(string cmd, string[] t, string line)
        {
            switch (cmd)
            {
                case "get":
                    Note("get  " + Need(t, 1) + " = " + Format(Get(t[1])));
                    break;
                case "set":
                    Set(Need(t, 1), Need(t, 2));
                    Note("set  " + t[1] + " = " + Format(Get(t[1])));
                    break;
                case "call":
                    {
                        string path = Need(t, 1);
                        string[] args = new string[t.Length - 2];
                        Array.Copy(t, 2, args, 0, args.Length);
                        object r = Call(path, args);
                        Note("call " + string.Join(" ", t, 1, t.Length - 1) + (r != null ? " -> " + Format(r) : ""));
                        break;
                    }
                case "expect":
                    {
                        string actual;
                        if (Check(t, out actual)) Note("ok   " + line + "  (= " + actual + ")");
                        else { failures++; Note("FAIL " + line + "  (当前 " + actual + ")"); }
                        break;
                    }
                case "dump":
                    {
                        object v = Get(Need(t, 1));
                        string name = Safe(Arg(t, 2, t[1]));
                        string json = v == null ? "null" : JsonUtility.ToJson(v, true);
                        File.WriteAllText(Path.Combine(outDir, name + ".json"), json, Encoding.UTF8);
                        Note("dump " + t[1] + " -> " + name + ".json（" + json.Length + " 字）");
                        break;
                    }
                case "log":
                    Note("log  " + (line.Length > 3 ? line.Substring(3).Trim() : ""));
                    break;
                default:
                    throw new Exception("不认识的指令：" + cmd);
            }
        }

        private void Finish()
        {
            if (finished) return;
            SilenceAll();   // 打包版跑剧本没有编辑器的总静音兜底，这里先把声源都关掉
            if (status == "ok" && failures > 0) status = "fail";
            Note("end " + DateTime.Now.ToString("HH:mm:ss") + "  steps " + done + "/" + steps.Count + "  failures " + failures + "  log_errors " + logErrors);
            finished = true;
            WriteReport();
            try { File.WriteAllText(Path.Combine(outDir, "done.txt"), status, Encoding.UTF8); }
            catch (Exception) { }
#if !UNITY_EDITOR
            Application.Quit(status == "ok" ? 0 : 1);
#endif
        }

        private void WriteReport()
        {
            try
            {
                var head = new StringBuilder();
                head.Append("id ").Append(id).Append('\n');
                head.Append("status ").Append(status).Append('\n');
                head.Append("steps ").Append(done).Append('/').Append(steps.Count).Append('\n');
                head.Append("failures ").Append(failures).Append('\n');
                head.Append("log_errors ").Append(logErrors).Append('\n');
                head.Append("log_warnings ").Append(logWarnings).Append('\n');
                head.Append("---\n").Append(report.ToString());
                File.WriteAllText(Path.Combine(outDir, "result.txt"), head.ToString(), Encoding.UTF8);
                File.WriteAllText(Path.Combine(outDir, "log.txt"), logText.ToString(), Encoding.UTF8);
            }
            catch (Exception e) { Debug.LogWarning("[TZAutoTest] 写结果失败：" + e.Message); }
        }

        private static string[] TestSaveFiles()
        {
            string save = Path.Combine(Application.persistentDataPath, TestSaveFile);
            return new[] { save, save + ".bak", save + ".tmp" };
        }

        // 备份测试存档（fresh 时备份后清空），并留一个标记文件，说明“跑完要还原”。
        private static void BackupTestSave(bool fresh)
        {
            try
            {
                foreach (string f in TestSaveFiles())
                {
                    string keep = f + KeepSuffix;
                    if (File.Exists(keep)) File.Delete(keep);
                    if (File.Exists(f)) File.Copy(f, keep);
                    if (fresh && File.Exists(f)) File.Delete(f);
                }
                File.WriteAllText(TestSaveFiles()[0] + RunMarker, DateTime.Now.ToString("o"));
            }
            catch (Exception e) { Debug.LogWarning("[TZAutoTest] 备份测试存档失败：" + e.Message); }
        }

        // 还原存档槽和测试存档。没有备份时什么都不做，可以重复调用。
        private static void RestoreSaves()
        {
            if (PlayerPrefs.HasKey(SlotBackupKey))
            {
                PlayerPrefs.SetInt(SaveSlotKey, PlayerPrefs.GetInt(SlotBackupKey, 0));
                PlayerPrefs.DeleteKey(SlotBackupKey);
                PlayerPrefs.Save();
            }
            string marker = TestSaveFiles()[0] + RunMarker;
            if (!File.Exists(marker)) return;
            try
            {
                foreach (string f in TestSaveFiles())
                {
                    string keep = f + KeepSuffix;
                    if (File.Exists(f)) File.Delete(f);
                    if (File.Exists(keep)) File.Move(keep, f);
                }
                File.Delete(marker);
            }
            catch (Exception e) { Debug.LogWarning("[TZAutoTest] 还原测试存档失败：" + e.Message); }
        }

        private void Note(string text) { report.Append(text).Append('\n'); }

        // 抓当前画面，缩到 width 宽存成 jpg（要在 WaitForEndOfFrame 之后调用）。
        private static void SaveFrame(string path, int width)
        {
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                int w = Mathf.Min(width, shot.width), h = Mathf.Max(1, shot.height * w / shot.width);
                RenderTexture rt = RenderTexture.GetTemporary(w, h, 0);
                RenderTexture prev = RenderTexture.active;
                Graphics.Blit(shot, rt);
                RenderTexture.active = rt;
                var small = new Texture2D(w, h, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                small.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                File.WriteAllBytes(path, small.EncodeToJPG(85));
                Destroy(small);
            }
            finally { Destroy(shot); }
        }

        // ---------- 输入模拟 ----------

        private static Event KeyEvent(EventType type, string[] t)
        {
            var e = new Event();
            e.type = type;
            e.keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), Need(t, 1), true);
            EventModifiers mods = EventModifiers.None;
            for (int k = 2; k < t.Length; k++)
            {
                string m = t[k].ToLowerInvariant();
                if (m == "shift") mods |= EventModifiers.Shift;
                else if (m == "ctrl" || m == "control") mods |= EventModifiers.Control;
                else if (m == "alt") mods |= EventModifiers.Alt;
            }
            e.modifiers = mods;
            return e;
        }

        private static Event MouseEvent(EventType type, Vector2 position)
        {
            var e = new Event();
            e.type = type;
            e.mousePosition = position;
            e.button = 0;
            e.clickCount = 1;
            return e;
        }

        // 不走系统输入（Unity 在后台时游戏收不到按键），而是在 Layout 阶段把事件直接交给 GameApp.OnGUI。
        // 编辑器和打包版本都能用；GameApp 没用 GUILayout，单独分发一个事件是安全的。
        private bool Inject(Event e)
        {
            if (app == null) { Note("FAIL 没有 GameApp，无法模拟输入"); return false; }
            injectQueue.Enqueue(e);
            return true;
        }

        private void OnGUI()
        {
            if (injectQueue.Count == 0 || app == null || Event.current.type != EventType.Layout) return;
            if (appOnGui == null) appOnGui = app.GetType().GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Event e = injectQueue.Dequeue();
            if (appOnGui == null) { Note("FAIL GameApp 没有 OnGUI"); return; }
            Event saved = Event.current;
            try
            {
                Event.current = e;
                appOnGui.Invoke(app, null);
            }
            catch (Exception ex) { Note("FAIL 分发输入出错：" + Describe(ex)); }
            finally { Event.current = saved; }
        }

        // ---------- 反射：从 GameApp 出发读写字段、调方法 ----------
        // 路径从 GameApp 开始：model.state.region、combat.health、navigation.Screen、speed。
        // 也可以从类型开始访问静态成员：GameModel.TestPower、Catalog.LastRegion、Time.timeScale。
        // 列表/数组用 [序号]：model.state.items[0].id。

        private static Component FindApp()
        {
            Type type = FindType("GameApp");
            if (type == null) return null;
            foreach (UnityEngine.Object o in Resources.FindObjectsOfTypeAll(type))
            {
                var c = o as Component;
                if (c != null && c.gameObject.scene.IsValid()) return c;
            }
            return null;
        }

        private static Type FindType(string name)
        {
            Type found;
            if (typeCache.TryGetValue(name, out found)) return found;
            found = null;
#pragma warning disable UAC0005   // 剧本按名字找类型（游戏、UnityEngine 各模块都要找），打包版里也要能用，这里用 AppDomain 没问题
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
#pragma warning restore UAC0005
            {
                Type t = asm.GetType("ThreadZero." + name, false);
                if (t == null) t = asm.GetType(name, false);
                if (t == null) t = asm.GetType("UnityEngine." + name, false);
                if (t != null) { found = t; break; }
            }
            typeCache[name] = found;
            return found;
        }

        private static MemberInfo FindMember(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, Any | BindingFlags.DeclaredOnly);
                if (f != null) return f;
                PropertyInfo p = null;
                try { p = t.GetProperty(name, Any | BindingFlags.DeclaredOnly); }
                catch (AmbiguousMatchException) { }
                if (p != null) return p;
                foreach (MethodInfo m in t.GetMethods(Any | BindingFlags.DeclaredOnly))
                    if (m.Name == name) return m;
            }
            return null;
        }

        private void Resolve(string path, out object owner, out Type ownerType, out string member, out int index)
        {
            string[] parts = path.Split('.');
            object current = app;
            Type type = app.GetType();
            int start = 0;
            if (parts[0] == "app") start = 1;
            else if (FindMember(type, Strip(parts[0])) == null)
            {
                Type staticType = FindType(parts[0]);
                if (staticType == null) throw new Exception("找不到成员或类型：" + parts[0]);
                current = null;
                type = staticType;
                start = 1;
            }
            if (start >= parts.Length) throw new Exception("路径不完整：" + path);
            for (int p = start; p < parts.Length - 1; p++)
            {
                current = ReadSegment(current, type, parts[p]);
                if (current == null) throw new Exception(path + "：" + parts[p] + " 是 null");
                type = current.GetType();
            }
            member = Strip(parts[parts.Length - 1], out index);
            owner = current;
            ownerType = type;
        }

        private static string Strip(string segment, out int index)
        {
            index = -1;
            int open = segment.IndexOf('[');
            if (open < 0 || !segment.EndsWith("]")) return segment;
            index = int.Parse(segment.Substring(open + 1, segment.Length - open - 2), CultureInfo.InvariantCulture);
            return segment.Substring(0, open);
        }

        private static string Strip(string segment)
        {
            int ignored;
            return Strip(segment, out ignored);
        }

        private static object ReadSegment(object obj, Type type, string segment)
        {
            int index;
            string name = Strip(segment, out index);
            object value = ReadMember(obj, type, name);
            return index >= 0 ? ItemAt(value, index) : value;
        }

        private static object ReadMember(object obj, Type type, string name)
        {
            MemberInfo m = FindMember(type, name);
            var f = m as FieldInfo;
            if (f != null) return f.GetValue(f.IsStatic ? null : obj);
            var p = m as PropertyInfo;
            if (p != null) return p.GetValue(obj, null);
            var method = m as MethodInfo;
            if (method != null && method.GetParameters().Length == 0) return method.Invoke(method.IsStatic ? null : obj, null);
            throw new Exception("找不到字段或属性：" + type.Name + "." + name);
        }

        private static object ItemAt(object value, int index)
        {
            var list = value as IList;
            if (list == null) throw new Exception("不是数组/列表，不能用 [" + index + "]");
            return list[index];
        }

        private object Get(string path)
        {
            object owner; Type type; string member; int index;
            Resolve(path, out owner, out type, out member, out index);
            object value = ReadMember(owner, type, member);
            return index >= 0 ? ItemAt(value, index) : value;
        }

        private void Set(string path, string raw)
        {
            object owner; Type type; string member; int index;
            Resolve(path, out owner, out type, out member, out index);
            if (index >= 0)
            {
                var list = ReadMember(owner, type, member) as IList;
                if (list == null) throw new Exception(member + " 不是数组/列表");
                list[index] = ParseValue(raw, ElementType(list.GetType()));
                return;
            }
            MemberInfo m = FindMember(type, member);
            var f = m as FieldInfo;
            if (f != null) { f.SetValue(f.IsStatic ? null : owner, ParseValue(raw, f.FieldType)); return; }
            var p = m as PropertyInfo;
            if (p != null)
            {
                MethodInfo setter = p.GetSetMethod(true);
                if (setter != null) { setter.Invoke(setter.IsStatic ? null : owner, new object[] { ParseValue(raw, p.PropertyType) }); return; }
            }
            throw new Exception("不能写入：" + type.Name + "." + member);
        }

        private object Call(string path, string[] raw)
        {
            object owner; Type type; string member; int index;
            Resolve(path, out owner, out type, out member, out index);
            Exception last = null;
            for (Type t = type; t != null; t = t.BaseType)
            {
                foreach (MethodInfo m in t.GetMethods(Any | BindingFlags.DeclaredOnly))
                {
                    if (m.Name != member) continue;
                    ParameterInfo[] ps = m.GetParameters();
                    int required = 0;
                    foreach (ParameterInfo p in ps) if (!p.IsOptional) required++;
                    if (raw.Length < required || raw.Length > ps.Length) continue;
                    var values = new object[ps.Length];
                    bool ok = true;
                    for (int k = 0; k < ps.Length; k++)
                    {
                        try { values[k] = k < raw.Length ? ParseValue(raw[k], ps[k].ParameterType) : ps[k].DefaultValue; }
                        catch (Exception e) { last = e; ok = false; break; }
                    }
                    if (!ok) continue;
                    return m.Invoke(m.IsStatic ? null : owner, values);
                }
            }
            throw new Exception("找不到能接受 " + raw.Length + " 个参数的方法：" + type.Name + "." + member + (last != null ? "（" + last.Message + "）" : ""));
        }

        private static Type ElementType(Type t)
        {
            if (t.IsArray) return t.GetElementType();
            if (t.IsGenericType)
            {
                Type[] g = t.GetGenericArguments();
                if (g.Length == 1) return g[0];
            }
            return typeof(object);
        }

        private static object ParseValue(string raw, Type type)
        {
            if (type == typeof(string) || type == typeof(object)) return raw;
            if (raw == "null" && !type.IsValueType) return null;
            Type under = Nullable.GetUnderlyingType(type);
            if (under != null)
            {
                if (raw == "null") return null;
                type = under;
            }
            if (type.IsEnum) return Enum.Parse(type, raw, true);
            if (type == typeof(bool)) return raw == "1" || raw == "on" || raw.Equals("true", StringComparison.OrdinalIgnoreCase);
            if (type == typeof(int)) return int.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(float)) return float.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (type == typeof(double)) return double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (type == typeof(long)) return long.Parse(raw, CultureInfo.InvariantCulture);
            return System.Convert.ChangeType(raw, type, CultureInfo.InvariantCulture);
        }

        private bool Check(string[] t, out string actual)
        {
            actual = "";
            if (t.Length < 4) throw new Exception("写法：" + t[0] + " <路径> <==|!=|>|<|>=|<=|has> <值>");
            object value = Get(t[1]);
            actual = Format(value);
            string op = t[2], expect = t[3];
            if (op == "has")
            {
                var list = value as IList;
                if (list != null)
                {
                    foreach (object o in list)
                        if (string.Equals(Plain(o), expect, StringComparison.OrdinalIgnoreCase) || string.Equals(Short(o), expect, StringComparison.OrdinalIgnoreCase)) return true;
                    return false;
                }
                return actual.IndexOf(expect, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            double a = 0, b = 0;
            bool numeric = double.TryParse(Plain(value), NumberStyles.Float, CultureInfo.InvariantCulture, out a)
                && double.TryParse(expect, NumberStyles.Float, CultureInfo.InvariantCulture, out b);
            switch (op)
            {
                case "==": return numeric ? Math.Abs(a - b) < 1e-6 : string.Equals(Plain(value), expect, StringComparison.OrdinalIgnoreCase);
                case "!=": return numeric ? Math.Abs(a - b) >= 1e-6 : !string.Equals(Plain(value), expect, StringComparison.OrdinalIgnoreCase);
                case ">": return numeric && a > b;
                case "<": return numeric && a < b;
                case ">=": return numeric && a >= b - 1e-6;
                case "<=": return numeric && a <= b + 1e-6;
                default: throw new Exception("不认识的比较：" + op);
            }
        }

        // ---------- 小工具 ----------

        private static string Format(object v)
        {
            if (v == null) return "null";
            if (v is string || v is bool || v is Enum || v.GetType().IsPrimitive || v is decimal) return Plain(v);
            var list = v as IList;
            if (list != null)
            {
                var sb = new StringBuilder("[" + list.Count + "]");
                for (int i = 0; i < list.Count && i < 12; i++) sb.Append(i == 0 ? " " : ", ").Append(Short(list[i]));
                if (list.Count > 12) sb.Append(", …");
                return sb.ToString();
            }
            string json = Json(v);
            if (json != null) return json.Length > 600 ? json.Substring(0, 600) + "…" : json;
            return v.ToString();
        }

        private static string Short(object v)
        {
            if (v == null) return "null";
            if (v is string || v is bool || v is Enum || v.GetType().IsPrimitive || v is decimal) return Plain(v);
            string json = Json(v);
            if (json != null) return json.Length > 160 ? json.Substring(0, 160) + "…" : json;
            return v.ToString();
        }

        private static string Json(object v)
        {
            if (v is Vector2 || v is Vector3 || v is Color || v is Rect) return v.ToString();
            try
            {
                string json = JsonUtility.ToJson(v);
                return string.IsNullOrEmpty(json) || json == "{}" ? null : json;
            }
            catch (Exception) { return null; }
        }

        private static string Plain(object v)
        {
            if (v == null) return "null";
            if (v is bool) return (bool)v ? "true" : "false";
            var f = v as IFormattable;
            return f != null ? f.ToString(null, CultureInfo.InvariantCulture) : v.ToString();
        }

        private static IEnumerable<string> Lines(string text)
        {
            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                string s = raw.Trim();
                if (s.Length == 0 || s.StartsWith("#")) continue;
                yield return s;
            }
        }

        // 按空白切分，"带 空格" 的参数用双引号括起来。
        private static string[] Split(string line)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            foreach (char c in line)
            {
                if (c == '"') { quoted = !quoted; continue; }
                if (!quoted && char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0) { list.Add(sb.ToString()); sb.Length = 0; }
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0) list.Add(sb.ToString());
            return list.ToArray();
        }

        private static string Arg(string[] t, int i, string fallback) { return i < t.Length ? t[i] : fallback; }

        private static string Need(string[] t, int i)
        {
            if (i >= t.Length) throw new Exception("缺少第 " + i + " 个参数");
            return t[i];
        }

        private static float Number(string s, float fallback)
        {
            float v;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        private static string Safe(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        private static string Describe(Exception e)
        {
            while (e is TargetInvocationException && e.InnerException != null) e = e.InnerException;
            return e.GetType().Name + ": " + e.Message;
        }
    }
}
