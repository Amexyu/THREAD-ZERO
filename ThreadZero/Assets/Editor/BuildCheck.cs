using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ThreadZero.Editor
{
    /// v0.20 打包前检查（给桥接 / 菜单用，结果写进 Logs/TZBridge/check_*.txt）：
    /// · 冒烟测试：BuildProject.Smoke（纯逻辑战斗 60 秒 + 存档往返 + 主存档损坏后从备份恢复）。
    /// · 打包检查：只打「当前平台」，不切换平台（切平台要整个工程重新导入，很慢，也会改动你的编辑器设置）。
    ///   输出到仓库根目录的 Builds/检查_<平台>/（Builds/ 在 .gitignore 里）。打包会用不带 UNITY_EDITOR 的方式重新编译脚本，
    ///   编辑器里能跑、打包却编不过的问题（运行时代码里用了 UnityEditor 之类）在这里就能发现。
    public static class BuildCheck
    {
        private static string LogDir { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/TZBridge")); } }

        [MenuItem("Thread Zero/检查/冒烟测试（战斗 + 存档往返）")]
        public static void SmokeMenu()
        {
            var sb = new StringBuilder();
            sb.AppendLine("time " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            try
            {
                BuildProject.Smoke(); SaveFieldCheck(sb);
                // v0.20 Windows 上 git 把 .cs 签出成 \r\n 换行，技能动作表解析时 FormatException（11-22 在 Windows 上踩到）
                string crlf = LayeredRigView.CheckMoveLineEndings();
                if (crlf.Length > 0) throw new Exception("技能动作表换成 Windows 换行（\\r\\n）后解析出错：" + crlf);
                sb.AppendLine("技能动作表：换成 Windows 换行（\\r\\n）解析结果和原样一致。");
                sb.AppendLine("status ok"); sb.AppendLine("冒烟测试通过：战斗推进、存档往返、主存档损坏后从备份恢复、技能动作表换行都正常。");
            }
            catch (Exception e) { sb.AppendLine("status fail"); sb.AppendLine(e.ToString()); }
            Write("check_smoke.txt", sb.ToString());
        }

        /// 存档字段往返：把各种数值写进存档再读回来（读档会过一遍 GameModel 的整理），看有没有被读档时的上下限夹掉。
        /// v0.20 加：训练等级原来读档时被夹到 20 级（上限早就改成 9999 了）。
        private static void SaveFieldCheck(StringBuilder sb)
        {
            var st = new GameModel(null, 11).state;
            st.training = 57; st.shards = 33; st.cores = 9; st.shakeMode = 1; st.glowMode = 1; st.scrapRate = 12.5f; st.savedAt = 638000000000000000L;
            st.bosses = 2; st.bossMask = 3; if (!st.implants.Contains("eye_r")) st.implants.Add("eye_r"); st.implantLevels[1] = 7; st.echoBest = 44; st.echoFloor = 40;
            st.sinceEpic = 30; st.sinceLegendary = 100;
            // v0.21：改造板（老存档训练 57 级要换算成火力 57 + 机体 57）、芯片与改装件
            st.boardVersion = 0; st.board = new int[3]; st.boardPerks = 5; st.chipBag.Add(new Chip { kind = 4, tier = 2 }); st.modBag[3] = 2;
            // v0.21 机库：碎片、等级、上场（第二个位故意放同分工的，读档时要被清掉）
            st.blueprints[3] = 6; st.robotLevels[1] = 9; st.robotLevels[3] = 4; st.deployed = new[] { 1, 3 }; st.hangarGift = true;
            var back = new GameModel(JsonUtility.FromJson<GameState>(JsonUtility.ToJson(st)), 11).state;
            var bad = new System.Collections.Generic.List<string>();
            if (back.training != 57) bad.Add("训练 " + back.training);
            if (back.shards != 33) bad.Add("织网碎片 " + back.shards);
            if (back.cores != 9) bad.Add("核心 " + back.cores);
            if (back.shakeMode != 1) bad.Add("震屏 " + back.shakeMode);
            if (back.glowMode != 1) bad.Add("辉光 " + back.glowMode);
            if (Math.Abs(back.scrapRate - 12.5f) > .001f) bad.Add("离线收入 " + back.scrapRate);
            if (back.savedAt != 638000000000000000L) bad.Add("存档时间 " + back.savedAt);
            if (back.implantLevels == null || back.implantLevels.Length < 2 || back.implantLevels[1] != 7) bad.Add("义体等级");
            if (back.echoBest != 44 || back.echoFloor != 40) bad.Add("零号深网 " + back.echoFloor + "/" + back.echoBest);
            if (back.sinceEpic != 30 || back.sinceLegendary != 100) bad.Add("保底计数 " + back.sinceEpic + "/" + back.sinceLegendary);
            if (back.board == null || back.board[0] != 57 || back.board[1] != 57 || back.boardVersion != 1 || back.boardPerks != 5) bad.Add("改造板 " + (back.board == null ? "null" : back.board[0] + "/" + back.board[1]) + " 节点 " + back.boardPerks);
            if (back.chipBag == null || back.chipBag.Count != 1 || back.chipBag[0].kind != 4 || back.chipBag[0].tier != 2) bad.Add("芯片库存");
            if (back.modBag == null || back.modBag.Length != 8 || back.modBag[3] != 2) bad.Add("改装件库存");
            if (back.blueprints == null || back.blueprints.Length != Catalog.RobotCount || back.blueprints[3] != 6) bad.Add("图纸碎片");
            if (back.robotLevels == null || back.robotLevels[1] != 9 || back.robotLevels[3] != 4 || !back.hangarGift) bad.Add("机器人等级");
            if (back.deployed == null || back.deployed[0] != 1 || back.deployed[1] != -1) bad.Add("上场 " + (back.deployed == null ? "null" : back.deployed[0] + "/" + back.deployed[1]));
            if (bad.Count > 0) throw new Exception("存档字段往返不对：" + string.Join("，", bad.ToArray()));
            sb.AppendLine("存档字段往返：训练 57 级（换算成改造板火力 57 + 机体 57）、改造板节点、芯片 / 改装件库存、机库（碎片、等级、上场，同分工两台读档时清掉一台）、织网碎片、核心、震屏、辉光、离线收入、存档时间、义体等级、零号深网、保底计数都对。");
        }

        [MenuItem("Thread Zero/检查/打包检查（当前平台，不切换）")]
        public static void BuildMenu()
        {
            var sb = new StringBuilder();
            BuildTarget active = EditorUserBuildSettings.activeBuildTarget;
            bool win = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            bool mac = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
            sb.AppendLine("time " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("active " + active);
            sb.AppendLine("support_windows64 " + win);
            sb.AppendLine("support_osx " + mac);
            bool standalone = active == BuildTarget.StandaloneWindows64 || active == BuildTarget.StandaloneOSX || active == BuildTarget.StandaloneLinux64;
            if (!standalone || !BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, active))
            {
                sb.AppendLine("status skipped");
                sb.AppendLine("当前平台不是电脑单机版，或者这台电脑没装这个平台的打包模块，没有打包（不会自动切换平台）。");
                Write("check_build.txt", sb.ToString());
                return;
            }
            string name = active == BuildTarget.StandaloneWindows64 ? "ThreadZero.exe" : active == BuildTarget.StandaloneOSX ? "ThreadZero.app" : "ThreadZero.x86_64";
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/检查_" + active));
            Directory.CreateDirectory(dir);
            var started = DateTime.Now;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                { scenes = new[] { "Assets/Scenes/Main.unity" }, locationPathName = Path.Combine(dir, name), target = active, options = BuildOptions.None });
            }
            catch (Exception e)
            {
                sb.AppendLine("status fail"); sb.AppendLine(e.ToString()); Write("check_build.txt", sb.ToString()); return;
            }
            var s = report.summary;
            sb.AppendLine("status " + (s.result == BuildResult.Succeeded ? "ok" : "fail"));
            sb.AppendLine("result " + s.result);
            sb.AppendLine("seconds " + (DateTime.Now - started).TotalSeconds.ToString("F0"));
            sb.AppendLine("size_mb " + (s.totalSize / 1048576.0).ToString("F1"));
            sb.AppendLine("errors " + s.totalErrors + " warnings " + s.totalWarnings);
            sb.AppendLine("output " + Path.Combine(dir, name));
            // 包里最大的东西：按文件夹汇总 + 前 25 个资源（看包为什么这么大）
            var assets = report.packedAssets.SelectMany(p => p.contents).Where(c => !string.IsNullOrEmpty(c.sourceAssetPath)).ToList();
            sb.AppendLine("--- 按文件夹（MB）");
            foreach (var g in assets.GroupBy(c => Folder(c.sourceAssetPath)).Select(g => new { g.Key, size = g.Sum(c => (double)c.packedSize), n = g.Count() }).OrderByDescending(g => g.size).Take(20))
                sb.AppendLine((g.size / 1048576.0).ToString("F1").PadLeft(8) + "  " + g.n.ToString().PadLeft(4) + " 个  " + g.Key);
            sb.AppendLine("--- 最大的 25 个（MB）");
            foreach (var c in assets.OrderByDescending(c => c.packedSize).Take(25))
                sb.AppendLine((c.packedSize / 1048576.0).ToString("F1").PadLeft(8) + "  " + c.sourceAssetPath);
            sb.AppendLine("---");
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Warning)
                        sb.AppendLine("[" + m.type + "] " + m.content.Split('\n')[0]);
            Write("check_build.txt", sb.ToString());
        }

        /// 贴图占用：Resources 下所有贴图按「当前平台导入后的格式」算大小（打包时 Resources 里的东西全部进包）。
        [MenuItem("Thread Zero/检查/贴图占用（Resources）")]
        public static void TextureMenu()
        {
            var sb = new StringBuilder();
            sb.AppendLine("time " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            var rows = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources" }).Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Select(path =>
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    long bytes = tex == null ? 0 : UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
                    return new { path, bytes, format = tex == null ? "?" : tex.format.ToString(), w = tex == null ? 0 : tex.width, h = tex == null ? 0 : tex.height, mips = tex == null ? 0 : tex.mipmapCount };
                }).ToList();
            sb.AppendLine("count " + rows.Count + "  total_mb " + (rows.Sum(r => (double)r.bytes) / 1048576.0).ToString("F1"));
            sb.AppendLine("--- 按格式（MB）");
            foreach (var g in rows.GroupBy(r => r.format).OrderByDescending(g => g.Sum(r => r.bytes)))
                sb.AppendLine((g.Sum(r => (double)r.bytes) / 1048576.0).ToString("F1").PadLeft(8) + "  " + g.Count().ToString().PadLeft(4) + " 张  " + g.Key);
            sb.AppendLine("--- 按文件夹（MB）");
            foreach (var g in rows.GroupBy(r => Folder(r.path)).OrderByDescending(g => g.Sum(r => r.bytes)).Take(25))
                sb.AppendLine((g.Sum(r => (double)r.bytes) / 1048576.0).ToString("F1").PadLeft(8) + "  " + g.Count().ToString().PadLeft(4) + " 张  " + g.Key);
            sb.AppendLine("--- 最大的 20 张（MB）");
            foreach (var r in rows.OrderByDescending(r => r.bytes).Take(20))
                sb.AppendLine((r.bytes / 1048576.0).ToString("F1").PadLeft(8) + "  " + r.w + "x" + r.h + " " + r.format + " mips " + r.mips + "  " + r.path);
            Write("check_textures.txt", sb.ToString());
        }

        /// v0.20 压缩画质（逐张）：压缩规则写在 ArtImport.cs（怪 + 远景 / 中景用 BC7）。这里把每张压过的图和它的原 PNG 比，
        /// 算 PSNR（越高越接近原样；40 dB 以上基本看不出，30 多 dB 放大才看得出），再列出该压却没压上的图。
        /// 每张背景、每只怪的待机图从中间取一块原尺寸，左原样右压缩，存到 <仓库>/Artifacts/压缩对比/（不进仓库）。只读，不改任何设置。
        [MenuItem("Thread Zero/检查/压缩画质（逐张和原 PNG 比）")]
        public static void CompressQualityMenu()
        {
            var sb = new StringBuilder();
            sb.AppendLine("time " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  ArtImport.Compress " + ArtImport.Compress);
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/压缩对比"));
            Directory.CreateDirectory(outDir);
            var rows = new List<KeyValuePair<double, string>>(); var missed = new List<string>();
            long before = 0, after = 0; int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!ArtImport.IsEnemy(path) && !ArtImport.IsBackdrop(path)) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path); var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (tex == null || imp == null) continue;
                if (tex.format != TextureFormat.BC7) { missed.Add(path + "  " + tex.width + "x" + tex.height + "  " + tex.format + " mips " + tex.mipmapCount); continue; }
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!src.LoadImage(File.ReadAllBytes(path)) || src.width != tex.width || src.height != tex.height) { missed.Add(path + "  读不了原 PNG / 尺寸不同"); UnityEngine.Object.DestroyImmediate(src); continue; }
                int w = src.width, h = src.height;
                Color32[] pa = src.GetPixels32(); UnityEngine.Object.DestroyImmediate(src);
                Color32[] pb = ReadBack(tex);
                double se = 0; long n = 0;
                for (int i = 0; i < pa.Length && i < pb.Length; i++)
                {
                    if (pa[i].a < 8 && pb[i].a < 8) continue;   // 两边都透明的像素不算
                    int dr = pa[i].r - pb[i].r, dg = pa[i].g - pb[i].g, db = pa[i].b - pb[i].b, da = pa[i].a - pb[i].a;
                    se += dr * dr + dg * dg + db * db + da * da; n += 4;
                }
                double mse = n == 0 ? 0 : se / n, psnr = mse <= 0 ? 99 : 10 * Math.Log10(255.0 * 255.0 / mse);
                long was = (long)(w * (long)h * (imp.DoesSourceTextureHaveAlpha() ? 4 : 3) * 4 / 3);   // 改之前：不压缩（RGBA32 / RGB24）+ mipmap
                long now = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
                before += was; after += now; count++;
                rows.Add(new KeyValuePair<double, string>(psnr, path.Replace("Assets/Resources/Art/V16/", "") + "  " + w + "x" + h + "  " + (was / 1048576.0).ToString("F1") + " → " + (now / 1048576.0).ToString("F1") + " MB"));
                if (ArtImport.IsBackdrop(path) || path.EndsWith("_idle.png"))
                {
                    int cw = Math.Min(w, 512), ch = Math.Min(h, 512), cx = (w - cw) / 2, cy = (h - ch) / 2;
                    var pair = new Texture2D(cw * 2 + 8, ch, TextureFormat.RGBA32, false);
                    var px = new Color32[pair.width * pair.height]; for (int i = 0; i < px.Length; i++) px[i] = new Color32(40, 42, 44, 255);
                    for (int y = 0; y < ch; y++)
                        for (int x = 0; x < cw; x++)
                        {
                            px[y * pair.width + x] = Over(pa[(cy + y) * w + cx + x]);
                            px[y * pair.width + cw + 8 + x] = Over(pb[(cy + y) * w + cx + x]);
                        }
                    pair.SetPixels32(px); pair.Apply();
                    File.WriteAllBytes(Path.Combine(outDir, path.Replace("Assets/Resources/Art/V16/", "").Replace('/', '_')), pair.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(pair);
                }
            }
            rows.Sort((x, y) => x.Key.CompareTo(y.Key));
            sb.AppendLine("count " + count + "  before_mb " + (before / 1048576.0).ToString("F1") + "  after_mb " + (after / 1048576.0).ToString("F1") + "  missed " + missed.Count);
            if (missed.Count > 0) { sb.AppendLine("--- 该压没压上（检查尺寸是不是 4 的倍数、mipmap 有没有关）"); foreach (string m in missed) sb.AppendLine("  " + m); }
            sb.AppendLine("--- PSNR 从低到高");
            foreach (var r in rows) sb.AppendLine(r.Key.ToString("F1").PadLeft(6) + " dB  " + r.Value);
            Write("check_compress_all.txt", sb.ToString());
        }
        private static Color32[] ReadBack(Texture2D tex)
        {
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt); RenderTexture.active = rt;
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0); copy.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            Color32[] px = copy.GetPixels32(); UnityEngine.Object.DestroyImmediate(copy); return px;
        }
        private static Color32 Over(Color32 c)   // 透明的地方垫深灰，看得出边缘
        { float a = c.a / 255f; return new Color32((byte)(c.r * a + 40 * (1 - a)), (byte)(c.g * a + 42 * (1 - a)), (byte)(c.b * a + 44 * (1 - a)), 255); }

        private static string Folder(string path)
        {
            string[] parts = path.Split('/');
            return string.Join("/", parts.Take(Math.Min(parts.Length - 1, 5)));
        }

        private static void Write(string file, string text)
        {
            try { Directory.CreateDirectory(LogDir); File.WriteAllText(Path.Combine(LogDir, file), text, new UTF8Encoding(false)); }
            catch (Exception e) { Debug.LogWarning("[检查] 写结果失败：" + e.Message); }
            Debug.Log("[检查] " + file + "\n" + text);
        }
    }
}
