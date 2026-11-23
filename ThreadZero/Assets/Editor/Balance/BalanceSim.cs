using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ThreadZero.EditorTools
{
    /// v0.18 自动数值测试：用游戏里真实的 GameModel + CombatSession，模拟一个“开着自动推进挂机、会换装备会升级”的玩家，
    /// 从新存档一路打主线（打不过就照游戏规则撤回上一关刷，变强了再自动挑战），记录每关首通时间、死亡次数、单次通关用时。
    /// 结果写到 Docs/数值测试报告.md，历史记录追加到 ThreadZero/Logs/BalanceSim/history.csv。
    /// 自动：数值相关的脚本（关卡、平衡表、模型、战斗、技能）一改，编译完就自动重跑一次。
    /// 手动：菜单 Thread Zero/数值测试/立即运行；或桥接命令 `menu Thread Zero/数值测试/立即运行`。
    [InitializeOnLoad]
    public static partial class BalanceSim
    {
        // ---------- 设置 ----------
        private static int Seeds = 6;                 // 几个随机种子（掉落运气不同）；v0.20 4 → 6，后几章种子之间差得多，4 个看不准
        private const float MaxHours = 18;           // 每个种子最多模拟多少小时（1× 速度的游戏时间）
        private const float StallHours = 3;          // 这么久没有打过新的一关 = 卡关
        private const float TickSeconds = .25f;      // 每次推进的时间（CombatSession 内部再切成 0.05 秒一小步）
        /// v0.19 节奏（Docs/节奏与长线目标_提案.md，用户 11-21 拍板）：每章用时目标（小时，模拟玩家 = 最优，1× 速度），主线合计约 7 小时。
        private static readonly float[] ChapterTargetHours = { .67f, 1f, 1.33f, 1.75f, 2.25f };
        /// 中位数落在目标的 0.75–1.3 倍算达标；最慢的种子不超过中位数的 2 倍（运气差也别卡太久）。
        private static readonly float[] TargetBand = { .75f, 1.3f };
        private const float SlowestRatio = 2f;
        /// 调数值时可以只跑前几章（快）；正式报告用 5。
        private const int UntilChapter = 5;
        /// 打完主线后在回声层再挂几小时（报告“打完主线以后”一节）。调主线数值时设 0 省时间。
        private static float EchoHours = 8;
        private static int SimDraws;
        /// v0.20 卡关时去回声层刷一会（真人会这么做，回声层就是为这个准备的）：最前面那一关连着倒 6 次、回声层已开放 → 刷这么多分钟再回来。
        private const float EchoDetourMinutes = 20;
        private const int EchoDetourAfter = 6;
        /// 一章里单独一关（从上一关首通到这一关首通）最多占这一章用时的多少：超过就是“墙”。
        private const float WallShare = .5f;
        private static readonly string[] Watched = { "Chapters.cs", "Balance.cs", "GameModel.cs", "CombatSession.cs", "CombatEnemyAttacks.cs",
                                                     "DaggerSkill.cs", "SkillSystem.cs", "EncounterMovement.cs", "DollAnimation.cs" };

        private static string ProjectRoot { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); } }
        private static string RepoRoot { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../..")); } }
        private static string LogDir { get { return Path.Combine(ProjectRoot, "Logs/BalanceSim"); } }
        public static string ReportPath { get { return Path.Combine(RepoRoot, "Docs/数值测试报告.md"); } }

        static BalanceSim()
        {
            // 编译 / 打开工程后：数值脚本和上次跑的时候不一样就自动重跑。等编辑器稳定 3 秒、不在播放中、没在编译再跑。
            if (Application.isBatchMode || !AutoEnabled) return;
            autoAt = EditorApplication.timeSinceStartup + 3;
            EditorApplication.update += AutoCheck;
        }
        private static double autoAt;
        private static void AutoCheck()
        {
            if (EditorApplication.timeSinceStartup < autoAt) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) { autoAt = EditorApplication.timeSinceStartup + 3; return; }
            EditorApplication.update -= AutoCheck;
            string hash = SourceHash();
            string last = File.Exists(HashFile) ? File.ReadAllText(HashFile).Trim() : "";
            if (hash == last) return;
            Debug.Log("[数值测试] 数值相关脚本有改动（" + last + " → " + hash + "），自动跑快速版（3 个种子、不挂深网）……");
            AutoQuick(hash);
        }
        /// 11-24：完整版（6 个种子 + 主线后挂 8 小时深网）一次要卡编辑器 8–10 分钟，编译后自动跑太碍事。
        /// 自动只跑快速版，报告写到 Logs/BalanceSim/快速.md；完整版只在菜单「立即运行」时跑（写 Docs/数值测试报告.md）。
        private static void AutoQuick(string hash)
        {
            int seeds = Seeds; float echo = EchoHours; Seeds = 3; EchoHours = 0;
            try { RunAndReport("自动快速（脚本有改动）", Path.Combine(LogDir, "快速.md")); }
            finally { Seeds = seeds; EchoHours = echo; }
            Directory.CreateDirectory(LogDir); File.WriteAllText(HashFile, hash);
        }
        [MenuItem("Thread Zero/数值测试/自动重跑：开")] public static void AutoOn() { AutoEnabled = true; Debug.Log("[数值测试] 自动重跑：开"); }
        [MenuItem("Thread Zero/数值测试/自动重跑：关")] public static void AutoOff() { AutoEnabled = false; Debug.Log("[数值测试] 自动重跑：关"); }
        private static string HashFile { get { return Path.Combine(LogDir, "last_hash.txt"); } }
        private static string SourceHash()
        {
            using (var md5 = MD5.Create())
            {
                var all = new List<byte>();
                foreach (string f in Watched)
                {
                    string p = Path.Combine(Application.dataPath, "Scripts", f);
                    if (File.Exists(p)) all.AddRange(File.ReadAllBytes(p));
                }
                string self = Path.Combine(Application.dataPath, "Editor/Balance/BalanceSim.cs");
                if (File.Exists(self)) all.AddRange(File.ReadAllBytes(self));
                return BitConverter.ToString(md5.ComputeHash(all.ToArray())).Replace("-", "").Substring(0, 12);
            }
        }

        /// 自动重跑开关（默认开）。跑一次要卡编辑器十几秒到一分钟，嫌烦可以在菜单里关掉。
        private const string AutoKey = "ThreadZero.BalanceSim.Auto";
        public static bool AutoEnabled { get { return EditorPrefs.GetBool(AutoKey, true); } set { EditorPrefs.SetBool(AutoKey, value); } }
        [MenuItem("Thread Zero/数值测试/改了数值脚本自动重跑")]
        public static void ToggleAuto() { AutoEnabled = !AutoEnabled; Debug.Log("[数值测试] 自动重跑：" + (AutoEnabled ? "开" : "关")); }
        [MenuItem("Thread Zero/数值测试/改了数值脚本自动重跑", true)]
        private static bool ToggleAutoCheck() { Menu.SetChecked("Thread Zero/数值测试/改了数值脚本自动重跑", AutoEnabled); return true; }

        [MenuItem("Thread Zero/数值测试/立即运行")]
        public static void RunMenu() { RunAndReport("手动"); }
        [MenuItem("Thread Zero/数值测试/打开报告")]
        public static void OpenReport() { if (File.Exists(ReportPath)) EditorUtility.OpenWithDefaultApp(ReportPath); }

        // ---------- 模拟 ----------
        private sealed class StageLog
        {
            public int attempts, clears, defeats, timeouts; public float first = -1, clearSum;
            public float atk, hp, def, haste, crit, gearLevel; public int training; public string weapon = "";
        }
        private sealed class RunLog
        {
            public int seed; public StageLog[] stages; public float time; public bool stalled; public int stallAt = -1; public bool finished;
            public int stuckAt = -1, stuckCount, reforges, reforgeScrap;   // 卡关计数（按最前面那一关算）和洗炼统计
            public int echoDetours; public float echoDetourTime;           // v0.20 卡关时去回声层刷了几次、一共多久
            public int echoStart, echoZero, echoDraws; public readonly List<int> echoMarks = new List<int>(); public readonly List<int> echoAsc = new List<int>();   // v0.20 每小时的飞升度（%）
            public StageLog stallSnap; public int stallScrap;   // 卡关时的身板和手里的零件   // 打完主线后：从第几层开始、每小时的最高层
            public float[] bossAt = new float[Chapters.ChapterCount];
            public readonly List<string> events = new List<string>();   // 最近的换关记录（卡关时写进报告）
            public void Note(float t, string what) { events.Add(H(t) + " " + what); if (events.Count > 24) events.RemoveAt(0); }
        }

        // v0.21 展览模式的预设存档：同一套模拟跑到第一次进第 stopAtRegion 关（第三章开头）就停，存下这时的状态。
        private static int stopAtRegion = -1;
        private static GameState stopState;
        [MenuItem("Thread Zero/展览/生成预设展览存档（第三章开头）")]
        public static void BuildExhibitPreset()
        {
            stopAtRegion = Chapters.FirstRegion(3); stopState = null;
            try { Simulate(1); } finally { stopAtRegion = -1; }
            if (stopState == null) { Debug.LogWarning("[展览] 模拟没打到第三章，没生成预设"); return; }
            stopState.region = Chapters.FirstRegion(3); stopState.savedAt = 0; stopState.retryRegion = -1; stopState.retryStreak = 0; stopState.retryFarms = 0;
            // 模拟不管机库：给两台第二章的机器人（装卸机前排、炮车后排，5 级）上场，来看的人一进来就能看到；回收无人机照常白送
            stopState.hangarGift = true; stopState.robotLevels[0] = 1; stopState.robotLevels[1] = 5; stopState.robotLevels[2] = 5; stopState.deployed = new[] { 1, 2 };
            string dir = Path.Combine(Application.dataPath, "Resources/Exhibit"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "preset.json"), JsonUtility.ToJson(stopState, true));
            AssetDatabase.Refresh();
            Debug.Log("[展览] 预设展览存档已生成：Resources/Exhibit/preset.json（" + Catalog.StageCode(stopState.region) + "，装备 " + stopState.items.Count + " 件，核心 " + stopState.cores + "）");
        }

        private static RunLog Simulate(int seed)
        {
            bool savedPower = GameModel.TestPower; GameModel.TestPower = false;
            try
            {
                var model = new GameModel(new GameState(), seed);
                var combat = new CombatSession(model);
                var run = new RunLog { seed = seed, stages = new StageLog[Chapters.LastRegion + 1] };
                for (int i = 0; i < run.stages.Length; i++) run.stages[i] = new StageLog();
                for (int c = 0; c < run.bossAt.Length; c++) run.bossAt[c] = -1;
                Policy(model); combat.ResetRegion(); run.stages[0].attempts++;
                float t = 0, lastProgress = 0, attemptStart = 0, limit = MaxHours * 3600; int timeoutsSeen = 0;
                while (t < limit)
                {
                    combat.Tick(TickSeconds); t += TickSeconds;
                    int region = model.state.region;
                    if (combat.RetreatRequested)
                    {   // 被打倒（已经修了 3 秒）：和游戏一样撤回上一关，不跨章
                        combat.RetreatRequested = false;
                        bool timeout = combat.timeouts > timeoutsSeen;
                        if (timeout) { timeoutsSeen = combat.timeouts; run.stages[region].timeouts++; } else run.stages[region].defeats++;
                        if (region >= model.state.unlockedRegion - 1)
                        {   // 卡在最前面（或回刷的前一关）：连续倒 3 次以上就洗炼，和真人一样
                            if (run.stuckAt != model.state.unlockedRegion) { run.stuckAt = model.state.unlockedRegion; run.stuckCount = 0; }
                            if (++run.stuckCount >= 3 && UseReforge) ReforgeWhenStuck(model, run);
                        }
                        run.Note(t, Catalog.StageCode(region) + (timeout ? " 超时" : " 被打倒") + "（第 " + combat.wave + " 波，用了 " + (t - attemptStart).ToString("F0") + " 秒）");
                        Policy(model);
                        t += Go(model, combat, run, model.RetreatTarget(), region);
                        if (run.stuckAt == model.state.unlockedRegion && run.stuckCount >= EchoDetourAfter && model.EchoUnlocked)
                        {   // v0.20 洗炼也不够：去回声层刷一会再回来
                            float d = EchoDetour(model, combat, run, t); t += d; run.echoDetours++; run.echoDetourTime += d;
                            run.stuckCount = EchoDetourAfter / 2;
                        }
                        attemptStart = t;
                    }
                    else if (combat.StageCleared)
                    {
                        t += .5f;   // 过关后往前走一小段再转场
                        StageLog st = run.stages[region]; st.clears++; st.clearSum += t - attemptStart;
                        run.Note(t, Catalog.StageCode(region) + " 通过（" + (t - attemptStart).ToString("F0") + " 秒）");
                        if (st.first < 0)
                        {
                            st.first = t; lastProgress = t; Snapshot(model, st);
                            if (captureAt != null && Array.IndexOf(captureAt, region) >= 0) captured[region] = JsonUtility.ToJson(model.state);   // 装备对照：首通这一关时存一份档
                            if (Chapters.Stage(region).kind == StageKind.Boss) run.bossAt[Chapters.ChapterOf(region) - 1] = t;
                            if (region == Chapters.BossRegion(UntilChapter)) { run.finished = true; break; }
                        }
                        if (stopAtRegion >= 0 && model.state.unlockedRegion >= stopAtRegion) { stopState = model.state; break; }   // 生成展览预设时到这里就停
                        Policy(model);
                        t += Go(model, combat, run, region < model.state.unlockedRegion && model.CanAdvanceAfterClear() ? region + 1 : region, region);   // 和游戏一样：墙前回刷，变强了才往前
                        attemptStart = t;
                    }
                    if (t - lastProgress > StallHours * 3600) { run.stalled = true; run.stallAt = model.state.unlockedRegion; run.stallSnap = new StageLog(); Snapshot(model, run.stallSnap); run.stallScrap = model.state.scrap; break; }
                }
                if (run.finished && EchoHours > 0 && captureAt == null)
                {   // 打完主线：和游戏一样进回声层，过一层往上一层，倒下退 1 层
                    int start = model.EchoStartFloor(); model.CatchUpEcho(start); run.echoStart = start; int draws0 = SimDraws;
                    model.state.echoFloor = start; Policy(model); combat.ResetRegion();
                    float end = t + EchoHours * 3600, mark = t + 3600, tidy = t;
                    while (t < end)
                    {
                        combat.Tick(TickSeconds); t += TickSeconds;
                        if (combat.RetreatRequested) { combat.RetreatRequested = false; Policy(model); tidy = t; model.state.echoFloor = Math.Max(1, model.state.echoFloor - 1); combat.ResetRegion(); t += .6f; }
                        // v0.22 过层快的时候不用每层都整理一遍装备（真人也不会），一分钟一次；不然 8 小时深网要算半个多小时
                        else if (combat.StageCleared) { t += .5f; if (t - tidy >= 60) { Policy(model); tidy = t; } model.state.echoFloor++; combat.ResetRegion(); t += .45f; }
                        if (t >= mark) { run.echoMarks.Add(model.state.echoBest); run.echoAsc.Add((int)Math.Round(model.Ascension * 100)); mark += 3600; }
                    }
                    run.echoZero = model.state.equipped.Count(u => { Item it = model.state.items.FirstOrDefault(x => x.uid == u); return it != null && it.quality == Quality.Zero; });
                    run.echoDraws = SimDraws - draws0;
                    model.state.echoFloor = 0;
                }
                run.time = t; return run;
            }
            finally { GameModel.TestPower = savedPower; }
        }

        /// 换关：和 GameApp 的转场一样计时（精英 / 首领 1 秒，进新章 0.6 秒，其余 0.45 秒）。
        private static float EchoDetour(GameModel model, CombatSession combat, RunLog run, float t0)
        {
            int start = model.EchoStartFloor(); model.CatchUpEcho(start);
            model.state.echoFloor = start; Policy(model); combat.ResetRegion();
            float t = 0, end = EchoDetourMinutes * 60;
            while (t < end)
            {
                combat.Tick(TickSeconds); t += TickSeconds;
                if (combat.RetreatRequested) { combat.RetreatRequested = false; Policy(model); model.state.echoFloor = Math.Max(1, model.state.echoFloor - 1); combat.ResetRegion(); t += .6f; }
                else if (combat.StageCleared) { t += .5f; Policy(model); model.state.echoFloor++; combat.ResetRegion(); t += .45f; }
            }
            model.state.echoFloor = 0; Policy(model); combat.ResetRegion();
            run.Note(t0 + t, "卡关，去零号深网刷了 " + EchoDetourMinutes + " 分钟（最高 " + model.state.echoBest + " 层）");
            return t;
        }
        private static float Go(GameModel model, CombatSession combat, RunLog run, int to, int from)
        {
            to = Math.Max(0, Math.Min(model.state.unlockedRegion, to));
            StageKind kind = Chapters.Stage(to).kind;
            float len = kind != StageKind.Normal && to != from ? 1f : to % Chapters.StagesPerChapter == 0 && to != from ? .6f : .45f;
            model.ChangeRegion(to); combat.ResetRegion(); run.stages[to].attempts++;
            return len;
        }

        private static void Snapshot(GameModel m, StageLog st)
        {
            Stats s = m.Total(); st.atk = s.attack; st.hp = s.health; st.def = s.defense; st.haste = s.haste; st.crit = s.crit; st.training = m.state.board[0] + m.state.board[1];
            float lv = 0; int n = 0;
            for (int i = 0; i < Catalog.SlotCount; i++) { Item it = m.Equipped((Slot)i); if (it != null) { lv += it.level; n++; } }
            st.gearLevel = n > 0 ? lv / n : 0;
            Item w = m.Equipped(Slot.Weapon); st.weapon = w == null ? "" : Catalog.Find(w.baseId).name;
        }

        /// 卡关时的洗炼（像真人那样一条一条换）：每件有词条的装备，找出“去掉以后战力掉得最少”的那一条，其余全锁，只洗这一条；
        /// 每次最多花当前零件的三成，剩下的照常买强化 / 训练。洗出来不好也不回退（游戏里本来就不能回退）。
        private static void ReforgeWhenStuck(GameModel m, RunLog run)
        {
            int budget = m.state.scrap * 3 / 10;
            for (int s = 0; s < Catalog.SlotCount; s++)
            {
                Item it = m.Equipped((Slot)s);
                if (it == null || it.affixes.Count < 2) continue;
                double full = Score(m); int weakest = -1; double least = double.MaxValue;
                for (int i = 0; i < it.affixes.Count; i++)
                {
                    float keep = it.affixes[i].value; it.affixes[i].value = 0;
                    double loss = full - Score(m); it.affixes[i].value = keep;
                    if (loss < least) { least = loss; weakest = i; }
                }
                it.affixLocks = 0;
                for (int i = 0; i < it.affixes.Count; i++) if (i != weakest) it.affixLocks |= 1 << i;
                int cost = m.ReforgeCost(it);
                if (cost > budget || !m.CanReforge(it)) { it.affixLocks = 0; continue; }
                budget -= cost; run.reforgeScrap += cost; run.reforges++;
                m.Reforge(it.uid); it.affixLocks = 0;
            }
        }

        /// 战力评分：输出 × 有效生命 × 效果折算。输出 = 攻击 × (1 + 攻速) × 暴击期望；有效生命 = 生命 × (100 + 防御) / 100。
        private static double Score(GameModel m)
        {
            Stats s = m.Total();
            double power = s.attack * (1 + s.haste) * (1 + s.crit * (Balance.CritMultiplier - 1 + s.critDamage)) * (1 + .3 * s.effectDamage) * (1 + .3 * s.bossDamage);
            double ehp = s.health * (100 + Math.Max(0, s.defense)) / 100;
            // 词条之外的效果也算进去（真人看得懂“吸血 / 连锁电弧”这些效果，不会只比面板数字）：粗略折成输出 / 生存的倍率
            double effect = 1 + .25 * m.EffectCount(Effect.Arc) + .08 * m.EffectCount(Effect.Execute) + .15 * m.EffectCount(Effect.Drone) + .1 * m.EffectCount(Effect.Mark) + .1 * m.EffectCount(Effect.Thruster) + .08 * m.TalentCount(2) + .06 * m.TalentCount(3);
            double survive = 1 + .12 * m.EffectCount(Effect.Leech) + .12 * m.EffectCount(Effect.Guard) + .06 * m.TalentCount(1) + .05 * m.TalentCount(4);   // 11-23 汲取有回复上限以后不再值 30%
            return power * effect * ehp * survive * Splash(m);
        }
        /// v0.20 武器打一群的能力（真人看得出清怪快慢，卡在成群的波次时会换群伤武器）：手炮 / 重锤溅射、撞杆矛穿透、霰弹散射、线圈跳电。
        /// 原来只比面板，模拟里拿单体武器（信号弩、钩索）的种子常年卡在精英关的第三波，面板比别的种子还高。
        private static double Splash(GameModel m)
        {
            Item w = m.Equipped(Slot.Weapon); if (w == null) return 1;
            switch (Catalog.MechOf(w.baseId)) { case "w3": case "w4": return 1.35; case "w5": return 1.3; case "w0": case "w2": return 1.2; default: return 1; }
        }

        /// 玩家的做法（每次过关 / 被打倒后）：每个部位换上评分最高的装备；没穿的全部分解（收藏不会丢）；
        /// 零件每次买“每个零件涨分最多”的一项（升级某件已穿装备 / 训练），买到买不起为止。卡关时会洗炼（见 ReforgeWhenStuck）。不刷回声层、不突破。
        private static void Policy(GameModel m)
        {
            // v0.22 打完主线：零件的四成拿去深层回收（抽到的和掉落一样参加下面的比较，没用的分解）
            // 零件平时一到手就被升级花光，所以单独记一笔「回收基金」：每次把新到手零件的四成划进去，攒够一次就抽。
            int drawSpent = 0;
            bool deep = UseTerminal && m.DeepTerminalUnlocked, part = UseTerminal && UseSlotRecycle && !m.DeepTerminalUnlocked && m.TerminalUnlocked;
            if (!deep && !part) drawFund = 0;
            else for (int n = 0; n < 40 && drawFund >= m.TerminalCost(deep) && m.state.scrap >= m.TerminalCost(deep); n++)
                { int c = m.TerminalCost(deep); if (m.TerminalDraw(deep, deep ? 0 : WeakestSlot(m)) == null) break; drawFund -= c; drawSpent += c; if (deep) SimDraws++; else SimSlotDraws++; }
            for (int s = 0; s < Catalog.SlotCount; s++)
            {
                string keep = m.state.equipped[s], best = keep; double current = Score(m), bestScore = current;
                // 换武器要明显更好（评分高 15%）才换：真人不会为了面板高一点点就把用顺手的武器换成另一种打法（近战 / 远程、吸血 / 电弧差别很大，评分看不出来）
                double need = keep == null ? 0 : s == (int)Slot.Weapon ? 1.15 : 1.0;
                foreach (Item it in m.state.items)
                {
                    if ((int)Catalog.Find(it.baseId).slot != s || it.uid == keep) continue;
                    m.state.equipped[s] = it.uid;
                    double sc = Score(m);
                    if (sc > bestScore && sc >= current * need) { bestScore = sc; best = it.uid; }
                }
                m.state.equipped[s] = best;
            }
            foreach (Item it in m.state.items.ToList()) if (!m.state.equipped.Contains(it.uid)) m.Salvage(it.uid);
            if (deep || part) drawFund = Math.Min(m.state.scrap, drawFund + (int)(Math.Max(0, m.state.scrap + drawSpent - scrapSeen) * (deep ? .4f : SlotShare)));
            m.state.scrap -= drawFund;   // 基金里的零件先拿开，下面的升级花不到
            try { PolicySpend(m); } finally { m.state.scrap += drawFund; scrapSeen = m.state.scrap; }
        }
        private static int drawFund, scrapSeen, SimSlotDraws;
        private const float SlotShare = .25f;
        /// 部位回收补哪个部位：身上最弱的那件（品质倍率 × 等级系数最低）。
        private static int WeakestSlot(GameModel m)
        {
            int pick = 0; float low = float.MaxValue;
            for (int s = 0; s < Catalog.SlotCount; s++)
            {
                Item it = m.Equipped((Slot)s); float v = it == null ? 0 : Balance.Tier(it.quality).multiplier * Balance.LevelFactor(it.level);
                if (v < low) { low = v; pick = s; }
            }
            return pick;
        }
        private static void PolicySpend(GameModel m)
        {
            V021Systems(m);
            // v0.22 打完主线：穿着的机密 / 原型，核心够了就突破（主线期间不突破，和以前一样）
            if (m.MainCleared && UseAscend) for (int s = 0; s < Catalog.SlotCount; s++) { Item it = m.Equipped((Slot)s); while (it != null && m.CanAscend(it) && m.Ascend(it.uid)) { } }
            // v0.20 机械飞升：织网碎片只有义体能用，够了就升（先升等级最低的那件；也要花零件，但比装备升级便宜得多）
            for (bool any = UseImplants; any;)
            {
                any = false; int low = -1;
                for (int i = 0; i < Catalog.Implants.Length; i++) if (m.CanUpgradeImplant(i) && (low < 0 || m.ImplantLevel(i) < m.ImplantLevel(low))) low = i;
                if (low >= 0) { m.UpgradeImplant(low); any = true; }
            }
            for (int n = 0; n < 600; n++)
            {
                double now = Score(m), bestGain = 0; Item pick = null; bool train = false;
                for (int s = 0; s < Catalog.SlotCount; s++)
                {
                    Item it = m.Equipped((Slot)s);
                    if (!m.CanUpgrade(it)) continue;
                    int cost = m.UpgradeCost(it); it.level++; double g = (Score(m) - now) / cost; it.level--;
                    if (g > bestGain) { bestGain = g; pick = it; train = false; }
                }
                int branch = -1;   // v0.21 改造板取代训练：火力（攻击）、机体（生命）各算一次收益，选划算的
                for (int bi = 0; bi < 2; bi++)
                {
                    if (!m.CanUpgradeBoard(bi)) continue;
                    int cost = m.BoardCost(bi); m.state.board[bi]++; double g = (Score(m) - now) / cost; m.state.board[bi]--;
                    if (g > bestGain) { bestGain = g; pick = null; train = true; branch = bi; }
                }
                if (train) m.UpgradeBoard(branch); else if (pick != null) m.Upgrade(pick.uid); else break;
            }
        }

        /// v0.21 新系统，按「认真玩的真人」的做法：
        ///   图鉴：集齐的套装领核心和碎片（游戏里每帧自动领）；
        ///   改造板节点：核心够就按顺序买（机体 → 火力 → 回收，每条从第一个开始；模拟不突破，核心没别的用处）；
        ///   机库：开放就领回收无人机；碎片集满就组装；上场前排 + 后排里最强的（编号越大越强）；上场的两台升到「章节 × 6」级；
        ///   芯片（第五章）：库存里的芯片按「过载 > 装甲 > 聚焦 > 缓冲 > 汲取 > 调度」塞进身上装备的空槽。改装件不做（要核心，模拟拿不准选哪种）。
        private static readonly int[] ChipPreference = { 0, 1, 5, 4, 3, 2 };
        // 对照实验用：关掉某个新系统，看它让主线快了多少（菜单「数值测试 / 对照」）
        private static bool UseSets = true, UsePerks = true, UseRobots = true, UseChips = true;
        // 11-24 补齐：义体升级、洗炼、突破、回收终端；主线期间用部位回收（默认关，见 Policy）
        private static bool UseImplants = true, UseReforge = true, UseAscend = true, UseTerminal = true, UseSlotRecycle = false;
        private static void ResetToggles()
        {
            UseSets = UsePerks = UseRobots = UseChips = UseImplants = UseReforge = UseAscend = UseTerminal = true;
            GameModel.TestNoTalents = GameModel.TestFlatQuality = false;
        }
        private static void V021Systems(GameModel m)
        {
            if (UseSets) m.ClaimSets();
            if (UsePerks)
                foreach (int bi in new[] { 1, 0, 2 })
                    for (int t = 0; t < 4; t++) if (m.CanBuyPerk(bi, t)) m.BuyPerk(bi, t);
            if (!UseRobots) { m.state.deployed[0] = m.state.deployed[1] = -1; m.state.hangarGift = true; }
            else V021Robots(m);
            if (UseChips) V021Chips(m);
        }
        [MenuItem("Thread Zero/数值测试/对照：不用机库")] public static void NoRobots() { Variant("不用机库", () => UseRobots = false); }
        [MenuItem("Thread Zero/数值测试/对照：不买改造板节点")] public static void NoPerks() { Variant("不买改造板节点", () => UsePerks = false); }
        [MenuItem("Thread Zero/数值测试/对照：不领套装奖励")] public static void NoSets() { Variant("不领套装奖励", () => UseSets = false); }
        [MenuItem("Thread Zero/数值测试/对照：全部新系统都不用")] public static void NoNew() { Variant("全部新系统都不用", () => { UseRobots = UsePerks = UseSets = UseChips = false; }); }
        /// 调数值时用：3 个种子、不挂深网，报告另存到 Logs/BalanceSim/快速.md（约为正式报告三分之一的时间）。
        [MenuItem("Thread Zero/数值测试/快速（3 个种子，不挂深网）")]
        public static void Quick()
        {
            int seeds = Seeds; float echo = EchoHours; Seeds = 3; EchoHours = 0;
            try { RunAndReport("快速", Path.Combine(LogDir, "快速.md")); } finally { Seeds = seeds; EchoHours = echo; }
        }
        // ---------- 11-24 提升来源对照：每样东西单独关掉跑一遍，和全开比 ----------
        private static readonly string[] ToggleNames = { "不用机库", "不买改造板节点", "不领套装奖励", "不升级义体", "卡关时不洗炼", "打完主线不突破", "不用芯片", "不用回收终端", "没有专属词条", "品质倍率全按 1", "主线也用部位回收" };
        private static readonly Action[] ToggleOffs = {
            () => UseRobots = false, () => UsePerks = false, () => UseSets = false, () => UseImplants = false, () => UseReforge = false,
            () => UseAscend = false, () => UseChips = false, () => UseTerminal = false, () => GameModel.TestNoTalents = true, () => GameModel.TestFlatQuality = true,
            () => UseSlotRecycle = true };
        public static string ContribPath { get { return Path.Combine(RepoRoot, "Docs/提升来源对照.md"); } }
        [MenuItem("Thread Zero/数值测试/提升来源对照（逐项关闭，约 1.5 小时）")] public static void Contributions() { ContributionRun(null); }
        [MenuItem("Thread Zero/数值测试/提升来源对照：只跑部位回收")] public static void ContribSlot() { ContributionRun(new[] { 10 }); }
        private static void ContributionRun(int[] only)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var rows = new List<string>(); var notes = new List<string>(); List<RunLog> baseRuns = null;
            for (int k = -1; k < ToggleNames.Length; k++)
            {
                if (only != null && k >= 0 && Array.IndexOf(only, k) < 0) continue;
                string name = k < 0 ? "基线（全部都用）" : ToggleNames[k];
                ResetToggles(); UseSlotRecycle = false; if (k >= 0) ToggleOffs[k]();
                var runs = new List<RunLog>();
                try
                {
                    for (int i = 0; i < Seeds; i++)
                    {
                        EditorUtility.DisplayProgressBar("提升来源对照", name + "：种子 " + (i + 1) + " / " + Seeds, (float)i / Seeds);
                        runs.Add(Simulate(101 + i * 17));
                    }
                }
                finally { EditorUtility.ClearProgressBar(); ResetToggles(); UseSlotRecycle = false; }
                if (k < 0) baseRuns = runs;
                string note; rows.Add(ContribRow(name, runs, baseRuns, k < 0, out note));
                if (note != null) notes.Add("- **" + name + "**：" + note);
                var sb = new StringBuilder();
                sb.AppendLine("# 提升来源对照（自动生成）"); sb.AppendLine();
                sb.AppendLine("> 菜单 `Thread Zero/数值测试/提升来源对照`。每一行只关掉一样东西，其余和基线一样；6 个种子的中位数，1× 速度的游戏时间。");
                sb.AppendLine("> 主线 = 打通 5-7 的总用时；倒下 = 各章第 6 关 / 首领被打倒次数（首通前后合计）；深网和飞升 = 打完主线后挂 3 / 8 小时。");
                sb.AppendLine("> 生成：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "，已跑 " + (watch.Elapsed.TotalMinutes).ToString("F0") + " 分钟，脚本指纹 " + SourceHash());
                sb.AppendLine();
                sb.AppendLine("| 关掉的东西 | 打通 | 主线 | 比基线 | 一 | 二 | 三 | 四 | 五 | 倒下 6关/首领（四章 · 五章） | 深网 3h | 深网 8h | 飞升 8h |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (string r in rows) sb.AppendLine(r);
                sb.AppendLine();
                if (notes.Count > 0) { sb.AppendLine("## 标出来的"); sb.AppendLine(); foreach (string n in notes) sb.AppendLine(n); sb.AppendLine(); }
                Directory.CreateDirectory(Path.GetDirectoryName(ContribPath));
                File.WriteAllText(ContribPath, sb.ToString(), new UTF8Encoding(false));
            }
            Debug.Log("[数值测试] 提升来源对照完成（" + watch.Elapsed.TotalMinutes.ToString("F0") + " 分钟）：" + ContribPath);
        }
        private static string ContribRow(string name, List<RunLog> runs, List<RunLog> baseRuns, bool isBase, out string note)
        {
            note = null;
            Func<List<RunLog>, float> total = rs => Median(rs.Select(r => r.bossAt[Chapters.ChapterCount - 1]));
            Func<List<RunLog>, int, float> chap = (rs, c) => Median(rs.Select(r => r.bossAt[c - 1] < 0 || (c > 1 && r.bossAt[c - 2] < 0) ? -1 : r.bossAt[c - 1] - (c == 1 ? 0 : r.bossAt[c - 2])));
            Func<List<RunLog>, int, float> deep = (rs, h) => Median(rs.Select(r => r.echoMarks.Count >= h ? (float)r.echoMarks[h - 1] : -1));
            Func<List<RunLog>, int, float> asc = (rs, h) => Median(rs.Select(r => r.echoAsc.Count >= h ? (float)r.echoAsc[h - 1] : -1));
            Func<List<RunLog>, int, int, float> deaths = (rs, c, st) => Median(rs.Select(r => (float)r.stages[Chapters.FirstRegion(c) + st].defeats));
            int done = runs.Count(r => r.finished);
            float t = total(runs), bt = baseRuns == null ? -1 : total(baseRuns), ratio = t > 0 && bt > 0 ? t / bt : -1;
            Func<float, string> hm = v => v < 0 ? "—" : H(v);
            Func<float, string> n0 = v => v < 0 ? "—" : v.ToString("F0");
            string row = "| " + name + " | " + done + "/" + runs.Count + " | " + hm(t) + " | " + (isBase ? "—" : ratio < 0 ? "—" : (ratio >= 1 ? "+" : "") + ((ratio - 1) * 100).ToString("F0") + "%")
                + " | " + string.Join(" | ", Enumerable.Range(1, Chapters.ChapterCount).Select(c => hm(chap(runs, c))))
                + " | " + n0(deaths(runs, 4, 5)) + "/" + n0(deaths(runs, 4, 6)) + " · " + n0(deaths(runs, 5, 5)) + "/" + n0(deaths(runs, 5, 6))
                + " | " + n0(deep(runs, 3)) + " | " + n0(deep(runs, 8)) + " | " + (asc(runs, 8) < 0 ? "—" : asc(runs, 8).ToString("F0") + "%") + " |";
            if (!isBase && baseRuns != null)
            {
                float d8 = deep(runs, 8), b8 = deep(baseRuns, 8), a8 = asc(runs, 8), ba8 = asc(baseRuns, 8);
                if (done < runs.Count) note = "有 " + (runs.Count - done) + " 个种子 18 小时内没打通主线——关掉它就卡死了，太关键";
                else if (ratio >= 2) note = "关掉后主线慢了 " + ((ratio - 1) * 100).ToString("F0") + "%（超过一倍），太强";
                else if (ratio >= 0 && ratio < 1.03f && Math.Abs(d8 - b8) <= 4 && Math.Abs(a8 - ba8) <= 2) note = "关掉后主线只差 " + ((ratio - 1) * 100).ToString("F0") + "%，深网 8 小时差 " + (d8 - b8).ToString("F0") + " 层——几乎没用";
                else if (ratio >= 0 && ratio < .97f) note = "关掉后主线反而快了 " + ((1 - ratio) * 100).ToString("F0") + "%（模拟玩家用它反而吃亏，或者噪声）";
            }
            return row;
        }
        private static void Variant(string name, Action off)
        {
            off();
            try { RunAndReport("对照：" + name, Path.Combine(LogDir, "对照_" + name + ".md")); }
            finally { ResetToggles(); }
        }
        private static void V021Robots(GameModel m)
        {
            m.GiftHangar();
            for (int i = 0; i < Catalog.RobotCount; i++) if (m.CanAssemble(i)) m.Assemble(i);
            for (int role = 0; role < 2; role++)
            {
                int best = -1;
                for (int i = 0; i < Catalog.RobotCount; i++) if (m.RobotBuilt(i) && Catalog.Robots[i].role == role) best = i;
                if (best >= 0 && !m.IsDeployed(best)) m.Deploy(best, role);
            }
            if (m.DeployedOfRole(0) < 0 && m.DeployedOfRole(1) < 0 && m.RobotBuilt(0) && !m.IsDeployed(0)) m.Deploy(0);
            int cap = Math.Min(Catalog.RobotMaxLevel, 6 * Chapters.ChapterOf(m.state.unlockedRegion));
            for (int k = 0; k < 2; k++)
            {
                int r = m.state.deployed[k]; if (r < 0) continue;
                while (m.state.robotLevels[r] < cap && m.CanUpgradeRobot(r) && m.state.scrap > Catalog.RobotUpgradeScrap(m.state.robotLevels[r]) * 3) m.UpgradeRobot(r);
            }
        }
        private static void V021Chips(GameModel m)
        {
            if (m.ChipsUnlocked && m.state.chipBag.Count > 0)
                for (int s = 0; s < Catalog.SlotCount; s++)
                {
                    Item it = m.Equipped((Slot)s); if (it == null) continue;
                    int have = it.chips == null ? 0 : it.chips.Count;
                    for (int slot = have; slot < m.ChipSlotsOf(it) && m.state.chipBag.Count > 0; slot++)
                    {
                        int pick = 0;
                        for (int b = 1; b < m.state.chipBag.Count; b++)
                            if (Array.IndexOf(ChipPreference, m.state.chipBag[b].kind) < Array.IndexOf(ChipPreference, m.state.chipBag[pick].kind)
                                || (m.state.chipBag[b].kind == m.state.chipBag[pick].kind && m.state.chipBag[b].tier > m.state.chipBag[pick].tier)) pick = b;
                        m.InsertChip(it, slot, pick);
                    }
                }
        }

        // ---------- 报告 ----------
        public static void RunAndReport(string why) { RunAndReport(why, null); }
        public static void RunAndReport(string why, string variantPath)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var runs = new List<RunLog>();
            try
            {
                for (int i = 0; i < Seeds; i++)
                {
                    EditorUtility.DisplayProgressBar("数值测试", "主线模拟：种子 " + (i + 1) + " / " + Seeds, (float)i / Seeds);
                    runs.Add(Simulate(101 + i * 17));
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            string hash = SourceHash();
            string report = Report(runs, why, hash, watch.Elapsed.TotalSeconds);
            if (variantPath != null)
            {   // 对照实验：报告另存，不动正式报告和历史
                Directory.CreateDirectory(Path.GetDirectoryName(variantPath));
                File.WriteAllText(variantPath, report, new UTF8Encoding(false));
                Debug.Log("[数值测试] 对照完成：" + variantPath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report, new UTF8Encoding(false));
            Directory.CreateDirectory(LogDir);
            File.WriteAllText(HashFile, hash);
            File.WriteAllText(Path.Combine(LogDir, "last_report.md"), report, new UTF8Encoding(false));
            AppendHistory(runs, hash);
            Debug.Log("[数值测试] 完成（" + watch.Elapsed.TotalSeconds.ToString("F0") + " 秒），报告：" + ReportPath + "\n" + Verdict);
        }
        private static string Verdict = "";

        private static float Median(IEnumerable<float> v) { var a = v.Where(x => x >= 0).OrderBy(x => x).ToList(); return a.Count == 0 ? -1 : a.Count % 2 == 1 ? a[a.Count / 2] : (a[a.Count / 2 - 1] + a[a.Count / 2]) / 2; }
        private static string H(float seconds) { if (seconds < 0) return "—"; int m = (int)Math.Round(seconds / 60); return (m / 60) + ":" + (m % 60).ToString("00"); }

        private static string Report(List<RunLog> runs, string why, string hash, double took)
        {
            var sb = new StringBuilder();
            var checks = new List<string>(); int fails = 0, warns = 0;
            Action<bool, bool, string> check = (ok, hard, text) => { if (ok) checks.Add("- ✅ " + text); else if (hard) { fails++; checks.Add("- ❌ " + text); } else { warns++; checks.Add("- ⚠️ " + text); } };

            int finished = runs.Count(r => r.finished);
            check(finished == runs.Count, true, "全部 " + runs.Count + " 个种子在 " + MaxHours + " 小时内打通主线（" + finished + " / " + runs.Count + "）"
                + (runs.Any(r => r.stalled) ? "；卡关：" + string.Join("、", runs.Where(r => r.stalled).Select(r => "种子" + r.seed + " 卡在 " + Catalog.StageCode(r.stallAt))) : ""));

            // 每章用时
            var chapterRows = new StringBuilder(); float prevChapter = 0;
            for (int c = 1; c <= UntilChapter; c++)
            {
                var spans = runs.Select(r =>
                {
                    float end = r.bossAt[c - 1]; if (end < 0) return -1f;
                    float start = c == 1 ? 0 : r.bossAt[c - 2];
                    return start < 0 ? -1f : end - start;
                }).ToList();
                float med = Median(spans), lo, hi;
                lo = ChapterTargetHours[c - 1] * TargetBand[0] * 3600; hi = ChapterTargetHours[c - 1] * TargetBand[1] * 3600;
                prevChapter = med;
                int deaths = (int)Median(runs.Select(r => (float)r.stages.Skip(Chapters.FirstRegion(c)).Take(Chapters.StagesPerChapter).Sum(s => s.defeats)));
                chapterRows.AppendLine("| 第" + Catalog.ChapterNumber(c) + "章 · " + Catalog.ChapterNames[c - 1] + " | " + H(med) + " | " + string.Join(" / ", spans.Select(H)) + " | " + H(lo) + "–" + H(hi) + " | " + deaths + " |");
                if (med >= 0)
                {
                    check(med >= lo && med <= hi, false, "第" + Catalog.ChapterNumber(c) + "章用时 " + H(med) + "（目标 " + H(ChapterTargetHours[c - 1] * 3600) + "，" + H(lo) + "–" + H(hi) + " 算达标）");
                    float slowest = spans.Max();
                    if (spans.All(x => x >= 0)) check(slowest <= med * SlowestRatio, false, "第" + Catalog.ChapterNumber(c) + "章最慢的种子 " + H(slowest) + "（不超过中位数的 " + SlowestRatio + " 倍）");
                }
                else check(false, true, "第" + Catalog.ChapterNumber(c) + "章没有打通");
                // 墙：一章里某一关单独吃掉了大半的时间
                if (med > 1200)
                {
                    int wall = -1; float worst = 0;
                    for (int r = Chapters.FirstRegion(c); r <= Chapters.BossRegion(c); r++)
                    {
                        int rr = r;
                        float gap = Median(runs.Select(x => x.stages[rr].first < 0 ? -1 : x.stages[rr].first - (rr == 0 ? 0 : Math.Max(0, x.stages[rr - 1].first))));
                        if (gap > worst) { worst = gap; wall = r; }
                    }
                    if (wall >= 0) check(worst <= med * WallShare, false, "第" + Catalog.ChapterNumber(c) + "章最慢的一关是 " + Catalog.StageCode(wall) + "，单独用了 " + H(worst) + "（不超过本章的 " + (int)(WallShare * 100) + "%）");
                }
            }

            // 每关
            var stageRows = new StringBuilder();
            for (int r = 0; r <= Chapters.LastRegion; r++)
            {
                StageDef def = Chapters.Stage(r);
                float first = Median(runs.Select(x => x.stages[r].first));
                float attempts = Median(runs.Select(x => (float)x.stages[r].attempts)), deaths = Median(runs.Select(x => (float)x.stages[r].defeats));
                float clear = Median(runs.Where(x => x.stages[r].clears > 0).Select(x => x.stages[r].clearSum / x.stages[r].clears));
                var got = runs.Where(x => x.stages[r].first >= 0).Select(x => x.stages[r]).ToList();
                string kind = def.kind == StageKind.Boss ? "首领" : def.kind == StageKind.Elite ? "精英" : "";
                stageRows.AppendLine("| " + Catalog.StageCode(r) + " " + kind + " | " + Catalog.RegionNames[r] + " | " + H(first) + " | " + attempts.ToString("F0") + " | " + deaths.ToString("F0")
                    + " | " + (clear < 0 ? "—" : clear.ToString("F0") + " 秒")
                    + " | " + (got.Count == 0 ? "— | — | — | —" : Median(got.Select(s => s.atk)).ToString("F0") + " | " + Median(got.Select(s => s.hp)).ToString("F0") + " | "
                        + Median(got.Select(s => s.gearLevel)).ToString("F1") + " | " + Median(got.Select(s => (float)s.training)).ToString("F0")) + " |");
                // 单关检查：一关的通关用时太长（挂机太慢）或者被打倒太多次（太卡）
                if (clear > 0 && def.kind == StageKind.Normal) check(clear <= 150, false, Catalog.StageCode(r) + " 平均一次通关 " + clear.ToString("F0") + " 秒（上限 150 秒）");
                if (got.Count > 0) check(deaths <= 25, false, Catalog.StageCode(r) + " 首通前后共被打倒 " + deaths.ToString("F0") + " 次（上限 25）");
            }
            // 章节入口：新一章第一关不应该是“必须回上一章刷很久”的断崖
            for (int c = 2; c <= Chapters.ChapterCount; c++)
            {
                int r = Chapters.FirstRegion(c);
                float wait = Median(runs.Select(x => x.stages[r].first >= 0 && x.bossAt[c - 2] >= 0 ? x.stages[r].first - x.bossAt[c - 2] : -1));
                if (wait >= 0) check(wait <= 1800, false, "进入第" + Catalog.ChapterNumber(c) + "章后打过 " + Catalog.StageCode(r) + " 用了 " + H(wait) + "（上限 0:30）");
            }

            Verdict = fails > 0 ? "❌ 有 " + fails + " 项不通过、" + warns + " 项提醒" : warns > 0 ? "⚠️ 通过，" + warns + " 项提醒" : "✅ 全部通过";
            sb.AppendLine("# 数值测试报告（自动生成）");
            sb.AppendLine();
            sb.AppendLine("> 每次改了关卡 / 平衡 / 战斗相关的脚本，Unity 编译完会自动重跑并覆盖这份报告。手动：菜单 `Thread Zero/数值测试/立即运行`。代码：`ThreadZero/Assets/Editor/Balance/BalanceSim.cs`。");
            sb.AppendLine();
            sb.AppendLine("- 结论：**" + Verdict + "**");
            sb.AppendLine("- 生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "（" + why + "，耗时 " + took.ToString("F0") + " 秒，脚本指纹 " + hash + "）");
            sb.AppendLine("- 方法：用游戏里真实的模型和战斗代码，从新存档开始，" + Seeds + " 个随机种子各模拟最多 " + MaxHours + " 小时（1× 速度）。开着自动推进；被打倒就按游戏规则撤回上一关，变强后自动再挑战。");
            sb.AppendLine("- 模拟玩家的做法：每次过关 / 被打倒后，立刻给每个部位换上评分最高的装备（评分 = 攻击×攻速×暴击期望 × 生命×防御减伤），没穿的全部分解，零件每次买“每个零件涨分最多”的一项（升级已穿装备或训练）。**不重校、不突破、不看词条特效；只在卡关（最前面那一关连着倒 6 次）时去零号深网刷 20 分钟再回来**。v0.21 起也会：领套装奖励、按章买改造板节点、机库组装并上场前排 + 后排（升到章节 × 6 级）、第五章塞芯片（不做改装件）。换装和花零件比真人勤快，零号深网和特效又没用上，两头抵消，大致相当于“认真管装备、只打主线”的玩家。");
            sb.AppendLine("- 时间都是 1× 速度的游戏时间；2× / 4× 按比例缩短。");
            sb.AppendLine();
            sb.AppendLine("## 检查");
            sb.AppendLine();
            foreach (string c in checks.Where(c => !c.StartsWith("- ✅"))) sb.AppendLine(c);
            foreach (string c in checks.Where(c => c.StartsWith("- ✅"))) sb.AppendLine(c);
            sb.AppendLine();
            sb.AppendLine("## 每章用时（从进入本章到第一次打过本章首领）");
            sb.AppendLine();
            sb.AppendLine("| 章 | 中位数 | 各种子 | 期望 | 被打倒次数（中位数） |");
            sb.AppendLine("|---|---|---|---|---|");
            sb.Append(chapterRows);
            sb.AppendLine();
            sb.AppendLine("## 每关（各种子的中位数）");
            sb.AppendLine();
            sb.AppendLine("首通时间 = 从新存档开始累计的游戏时间（时:分）。进关次数 / 被打倒次数 含回刷。通关用时 = 这一关所有成功通关的平均用时。右边四列是第一次打过这一关时的角色面板。");
            sb.AppendLine();
            sb.AppendLine("| 关 | 名字 | 首通时间 | 进关次数 | 被打倒 | 通关用时 | 攻击 | 生命 | 装备平均等级 | 训练 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            sb.Append(stageRows);
            sb.AppendLine();
            if (EchoHours > 0)
            {
                sb.AppendLine("## 打完主线以后：零号深网");
                sb.AppendLine();
                sb.AppendLine("打完 5-7 接着挂 " + EchoHours + " 小时：从第几层开始，之后每小时的最高层（第 48 层 ≈ 5-7 的强度，之后每层敌人生命约 +2.7%、攻击约 +1.8%、掉落约 +1.2 级）。");
                sb.AppendLine();
                foreach (RunLog x in runs.Where(x => x.finished)) sb.AppendLine("- 种子 " + x.seed + "：从第 " + x.echoStart + " 层 → " + string.Join(" → ", x.echoMarks.Select((f, i) => (i + 1) + " 小时 " + f + " 层" + (i < x.echoAsc.Count ? "（飞升 " + x.echoAsc[i] + "%）" : ""))) + "；最后身上零号 " + x.echoZero + " 件，深层回收 " + x.echoDraws + " 次");
                sb.AppendLine();
            }
            sb.AppendLine("## 各种子明细");
            sb.AppendLine();
            sb.AppendLine("每格：首通时间（时:分）/ 被打倒次数（超时 = 打太久按打不过撤回的次数）/ 进关次数。— = 没打过。");
            sb.AppendLine();
            sb.AppendLine("| 关 | " + string.Join(" | ", runs.Select(r => "种子 " + r.seed + (r.stalled ? "（卡在 " + Catalog.StageCode(r.stallAt) + "）" : r.finished ? "" : "（没打完）"))) + " |");
            sb.AppendLine("|---|" + string.Join("", runs.Select(r => "---|")));
            for (int r = 0; r <= Chapters.LastRegion; r++)
            {
                int rr = r;
                sb.AppendLine("| " + Catalog.StageCode(r) + " | " + string.Join(" | ", runs.Select(x => H(x.stages[rr].first) + " / " + x.stages[rr].defeats + (x.stages[rr].timeouts > 0 ? "（超时 " + x.stages[rr].timeouts + "）" : "") + " / " + x.stages[rr].attempts)) + " |");
            }
            sb.AppendLine();
            sb.AppendLine("各种子在每章打到的最后一关（首通那一刻）的身板：武器 / 攻击 / 生命 / 防御 / 攻速 / 暴击 / 装备平均等级 / 训练。卡关多半是武器类型或者词条运气差，对照着看。");
            sb.AppendLine();
            sb.AppendLine("| 章 | " + string.Join(" | ", runs.Select(r => "种子 " + r.seed)) + " |");
            sb.AppendLine("|---|" + string.Join("", runs.Select(r => "---|")));
            for (int c = 0; c < Chapters.ChapterCount; c++)
            {
                int cc = c;
                sb.AppendLine("| " + (c + 1) + " | " + string.Join(" | ", runs.Select(x =>
                {
                    for (int r = Math.Min(Chapters.LastRegion, cc * Chapters.StagesPerChapter + Chapters.StagesPerChapter - 1); r >= cc * Chapters.StagesPerChapter; r--)
                    {
                        StageLog st = x.stages[r]; if (st.first < 0) continue;
                        return Catalog.StageCode(r) + " " + st.weapon + " " + st.atk.ToString("F0") + "/" + st.hp.ToString("F0") + "/" + st.def.ToString("F0") + " 速" + (st.haste * 100).ToString("F0") + "% 暴" + (st.crit * 100).ToString("F0") + "% Lv" + st.gearLevel.ToString("F0") + " 训" + st.training;
                    }
                    return "—";
                })) + " |");
            }
            foreach (RunLog x in runs.Where(x => x.stallSnap != null))
            {
                StageLog st = x.stallSnap;
                sb.AppendLine("| 卡关时（种子 " + x.seed + "） | " + st.weapon + " " + st.atk.ToString("F0") + "/" + st.hp.ToString("F0") + "/" + st.def.ToString("F0") + " 速" + (st.haste * 100).ToString("F0") + "% 暴" + (st.crit * 100).ToString("F0") + "% Lv" + st.gearLevel.ToString("F0") + " 训" + st.training + " · 手里零件 " + x.stallScrap + " |");
            }
            sb.AppendLine();
            sb.AppendLine("卡关时洗炼（同一关连续倒 3 次起，每次最多花三成零件）：" + string.Join("；", runs.Select(x => "种子 " + x.seed + " 洗了 " + x.reforges + " 次、花 " + x.reforgeScrap + " 零件")));
            sb.AppendLine();
            sb.AppendLine("卡关时去零号深网（最前面那一关连着倒 " + EchoDetourAfter + " 次、洗炼也不够时，刷 " + EchoDetourMinutes + " 分钟再回来）：" + string.Join("；", runs.Select(x => "种子 " + x.seed + " 去了 " + x.echoDetours + " 次（" + (x.echoDetourTime / 60).ToString("F0") + " 分钟）")));
            foreach (RunLog x in runs.Where(x => x.stalled || x == runs.OrderByDescending(y => y.time).First()))
            {
                sb.AppendLine();
                sb.AppendLine("种子 " + x.seed + (x.stalled ? " 卡关前最后的记录：" : "（用时最长）最后的记录："));
                sb.AppendLine();
                foreach (string e in x.events) sb.AppendLine("- " + e);
            }
            sb.AppendLine();
            sb.AppendLine("## 当前数值（摘要）");
            sb.AppendLine();
            sb.AppendLine("- 敌人血量倍率 HealthScale = " + Chapters.HealthScale + "；每章倍率 血量 " + string.Join(" / ", Chapters.ChapterHealth) + "（章内每关再加 " + string.Join(" / ", Chapters.ChapterHealthRamp) + "），攻击 "
                + string.Join(" / ", Chapters.ChapterAttack) + "（章内每关再加 " + string.Join(" / ", Chapters.ChapterAttackRamp) + "）");
            sb.AppendLine("- 掉落等级段：" + string.Join("，", Enumerable.Range(0, Chapters.LastRegion + 1).Select(r => Catalog.StageCode(r) + " " + Chapters.DropLevels[r, 0] + "–" + Chapters.DropLevels[r, 1])));
            sb.AppendLine("- 义体：" + string.Join("；", Catalog.Implants.Select(d => d.name + "（" + d.desc + "）")));
            return sb.ToString();
        }

        private static void AppendHistory(List<RunLog> runs, string hash)
        {
            string path = Path.Combine(LogDir, "history.csv");
            bool head = !File.Exists(path);
            var sb = new StringBuilder();
            if (head) sb.AppendLine("time,hash,finished," + string.Join(",", Enumerable.Range(1, Chapters.ChapterCount).Select(c => "ch" + c + "_hours")));
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append(',').Append(hash).Append(',').Append(runs.Count(r => r.finished) + "/" + runs.Count);
            for (int c = 1; c <= Chapters.ChapterCount; c++)
            {
                float med = Median(runs.Select(r => r.bossAt[c - 1] < 0 || (c > 1 && r.bossAt[c - 2] < 0) ? -1 : r.bossAt[c - 1] - (c == 1 ? 0 : r.bossAt[c - 2])));
                sb.Append(',').Append(med < 0 ? "" : (med / 3600).ToString("F2"));
            }
            sb.AppendLine();
            File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
