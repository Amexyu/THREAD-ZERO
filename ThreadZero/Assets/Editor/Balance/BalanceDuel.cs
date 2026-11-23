using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ThreadZero.EditorTools
{
    /// 装备对照（11-23）：同一个角色、同样的等级 / 品质 / 词条，只把某个部位换成本章的另一件，去打本章第 6 关（精英）和第 7 关（首领），
    /// 比谁打得快、谁打得过。主线模拟只会挑面板分最高的那件，看不出“换别的会怎样”；这个报告专门看同章装备之间平不平衡。
    /// 角色取自主线模拟里第一次打过本章首领（第 7 关）的那一刻：原装备两关都能过，换了装备快了慢了、过不过得去一目了然。
    public static partial class BalanceSim
    {
        private static int[] captureAt;
        private static Dictionary<int, string> captured;
        private static readonly int[] DuelSeeds = { 101, 135, 169 };   // 11-24 两个种子 × 3 次太吵（取样点在首领首通边缘，输赢一翻指数差几十），改 3 个种子 × 4 次
        private const int DuelTries = 4;
        private static readonly string[] SlotNames = { "武器", "头部", "外套", "背部", "下装" };
        private static readonly string[] SlotPrefix = { "w", "h", "c", "b", "l" };
        public static string DuelPath { get { return Path.Combine(RepoRoot, "Docs/装备对照报告.md"); } }

        [MenuItem("Thread Zero/数值测试/装备对照（同章装备互换）")]
        public static void DuelMenu() { DuelReuse = false; DuelSlots = null; RunDuel(); }
        [MenuItem("Thread Zero/数值测试/装备对照：沿用上次取样")]
        public static void DuelReuseMenu() { DuelReuse = true; DuelSlots = null; try { RunDuel(); } finally { DuelReuse = false; } }
        [MenuItem("Thread Zero/数值测试/装备对照：只看武器（沿用取样）")]
        public static void DuelWeaponsMenu() { DuelReuse = true; DuelSlots = new[] { 0 }; try { RunDuel(); } finally { DuelReuse = false; DuelSlots = null; } }
        private static bool DuelReuse; private static int[] DuelSlots;

        private sealed class DuelResult { public string id; public int wins6, tries6, wins7, tries7; public float time6, time7; public double value6, value7, index; }

        public static void RunDuel()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var states = new List<KeyValuePair<int, string>>();   // (章, 存档)
            try
            {
                string cache = Path.Combine(LogDir, "duel_states.txt");
                if (DuelReuse && File.Exists(cache))
                    foreach (string line in File.ReadAllLines(cache)) { int tab = line.IndexOf('\t'); states.Add(new KeyValuePair<int, string>(int.Parse(line.Substring(0, tab)), line.Substring(tab + 1))); }
                bool cached = states.Count > 0;
                for (int si = 0; si < DuelSeeds.Length && !cached; si++)
                {
                    EditorUtility.DisplayProgressBar("装备对照", "主线模拟取样：种子 " + DuelSeeds[si], (float)si / (DuelSeeds.Length + 1));
                    captureAt = Enumerable.Range(1, 5).Select(Chapters.BossRegion).ToArray(); captured = new Dictionary<int, string>();
                    stopAtRegion = -1;
                    try { Simulate(DuelSeeds[si]); } finally { stopAtRegion = -1; captureAt = null; }
                    for (int c = 1; c <= 5; c++) { string js; if (captured.TryGetValue(Chapters.BossRegion(c), out js)) states.Add(new KeyValuePair<int, string>(c, js)); }
                }
                Directory.CreateDirectory(LogDir); File.WriteAllLines(cache, states.Select(kv => kv.Key + "\t" + kv.Value).ToArray());
                var results = new Dictionary<string, DuelResult>();
                int done = 0, total = states.Count * 25;
                foreach (var kv in states)
                {
                    int c = kv.Key;
                    for (int slot = 0; slot < 5; slot++)
                        if (DuelSlots == null || Array.IndexOf(DuelSlots, slot) >= 0)
                        for (int k = 0; k < 5; k++)
                        {
                            string id = SlotPrefix[slot] + ((c - 1) * 5 + k);
                            if (done++ % 25 == 0) EditorUtility.DisplayProgressBar("装备对照", "第 " + c + " 章 · 种子 " + (states.FindIndex(x => x.Value == kv.Value) / 5 + 1), .3f + .7f * done / total);   // 11-24 每份存档只刷一次：屏幕关着时进度条一次要几秒，每件都刷会让对照从 6 分钟拖到 30 多分钟
                            DuelResult r; if (!results.TryGetValue(id, out r)) results[id] = r = new DuelResult { id = id };
                            Duel(kv.Value, slot, id, Chapters.BossRegion(c) - 1, ref r.wins6, ref r.tries6, ref r.time6, ref r.value6);
                            Duel(kv.Value, slot, id, Chapters.BossRegion(c), ref r.wins7, ref r.tries7, ref r.time7, ref r.value7);
                        }
                }
                string report = DuelReport(results, watch.Elapsed.TotalSeconds);
                File.WriteAllText(DuelPath, report, new UTF8Encoding(false));
                Debug.Log("[装备对照] 完成（" + watch.Elapsed.TotalSeconds.ToString("F0") + " 秒）：" + DuelPath);
            }
            finally { EditorUtility.ClearProgressBar(); captureAt = null; }
        }

        /// 读档 → 把 slot 换成 id（等级 / 品质 / 词条 / 芯片 / 改装件照抄原来那件）→ 打 region 若干次。time 累加的是打赢那几次的用时。
        private static void Duel(string json, int slot, string id, int region, ref int wins, ref int tries, ref float time, ref double value)
        {
            bool savedPower = GameModel.TestPower; GameModel.TestPower = false;
            try
            {
                for (int n = 0; n < DuelTries; n++)
                {
                    var state = JsonUtility.FromJson<GameState>(json);
                    var model = new GameModel(state, 7919 * (n + 1) + region);
                    Item old = model.Equipped((Slot)slot);
                    if (old != null && old.baseId != id)
                    {
                        Item it = model.Create(id, old.quality, old.level);
                        it.affixes = old.affixes.Select(a => new Affix { stat = a.stat, value = a.value }).ToList();
                        it.chips = old.chips; it.mod = old.mod; it.talent = old.talent;
                        state.items.Add(it); state.equipped[slot] = it.uid;
                    }
                    model.ChangeRegion(region);
                    var combat = new CombatSession(model); combat.ResetRegion();
                    float t = 0; tries++;
                    while (t < 400)
                    {
                        combat.Tick(TickSeconds); t += TickSeconds;
                        if (combat.StageCleared) { wins++; time += t; value += 60.0 / t; break; }
                        if (combat.RetreatRequested || t >= 400) { value += 60.0 / 400 * Math.Max(0, combat.wave - 1) / Balance.BossWave; break; }   // 输了：按打到第几段给一点分（打得越远越接近赢）
                    }
                }
            }
            finally { GameModel.TestPower = savedPower; }
        }

        private static string DuelReport(Dictionary<string, DuelResult> res, double took)
        {
            // 强度指数 = 第 6 关和第 7 关各算「每分钟打赢几次」（输了算 0）再相加；同章同部位里除以中位数 → 100 = 平均
            foreach (var r in res.Values)
                r.index = r.value6 / Math.Max(1, r.tries6) + r.value7 / Math.Max(1, r.tries7);
            var sb = new StringBuilder();
            sb.AppendLine("# 装备对照报告（自动生成）");
            sb.AppendLine();
            sb.AppendLine("> 菜单 `Thread Zero/数值测试/装备对照（同章装备互换）`。代码：`ThreadZero/Assets/Editor/Balance/BalanceDuel.cs`。");
            sb.AppendLine();
            sb.AppendLine("- 生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "（耗时 " + took.ToString("F0") + " 秒）");
            sb.AppendLine("- 方法：主线模拟（种子 " + string.Join(" / ", DuelSeeds) + "）每章第一次打过首领时存一份档。读档后只把一个部位换成本章另一件（等级、品质、词条、芯片、改装件照抄），去打第 6 关和第 7 关各 " + DuelTries + " 次。其余全部不变。");
            sb.AppendLine("- 强度指数：两关各算「赢的比例 × 每分钟能打完几次」，输了按打到第几段折一点分；相加后除以同章同部位的平均。100 = 本组平均，**80–120 算平衡**。");
            sb.AppendLine();
            int flagged = 0; var notes = new List<string>();
            for (int c = 1; c <= 5; c++)
            {
                sb.AppendLine("## 第" + "一二三四五"[c - 1] + "章");
                sb.AppendLine();
                sb.AppendLine("| 部位 | 装备 | 效果 | 第 6 关（赢 / 次，平均用时） | 第 7 关 | 强度指数 |");
                sb.AppendLine("|---|---|---|---|---|---|");
                for (int slot = 0; slot < 5; slot++)
                {
                    var group = Enumerable.Range(0, 5).Select(k => SlotPrefix[slot] + ((c - 1) * 5 + k)).Where(res.ContainsKey).Select(i => res[i]).ToList();
                    if (group.Count == 0) continue;
                    double med = group.Average(g => g.index); if (med <= 0) med = 1e-9;
                    foreach (var r in group)
                    {
                        EquipmentBase b = Catalog.Find(r.id); int idx = (int)Math.Round(r.index / med * 100);
                        bool bad = idx < 80 || idx > 120; if (bad) { flagged++; notes.Add("第" + "一二三四五"[c - 1] + "章 " + SlotNames[slot] + "「" + b.name + "」" + idx); }
                        sb.AppendLine("| " + SlotNames[slot] + " | " + b.name + "（" + r.id + "） | " + EffectName(b.effect) + " | " + Cell(r.wins6, r.tries6, r.time6) + " | " + Cell(r.wins7, r.tries7, r.time7) + " | " + (bad ? "**" + idx + "**" : idx.ToString()) + " |");
                    }
                }
                sb.AppendLine();
            }
            sb.Insert(sb.ToString().IndexOf("## 第一章"), "- 结论：" + (flagged == 0 ? "**✅ 全部在 80–120 之内**" : "**⚠️ " + flagged + " 件超出 80–120**：" + string.Join("、", notes)) + "\n\n");
            return sb.ToString();
        }
        /// 赢的比例 × 每分钟能打完几次（输了不算）
        private static double Rate(int w, int n, float t) { return w == 0 || n == 0 ? 0 : (double)w / n * 60.0 / (t / w); }
        private static string Cell(int w, int n, float t) { return w + " / " + n + (w > 0 ? "，" + (t / w).ToString("F0") + " 秒" : ""); }
        private static string EffectName(Effect e)
        {
            switch (e) { case Effect.None: return "—"; case Effect.Arc: return "电弧"; case Effect.Leech: return "吸血"; case Effect.Mark: return "标记"; case Effect.Execute: return "处决";
                case Effect.Guard: return "护盾"; case Effect.Drone: return "无人机"; case Effect.Thruster: return "推进"; default: return e.ToString(); }
        }
    }
}
