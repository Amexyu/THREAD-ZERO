using System;

namespace ThreadZero
{
    // v0.14 章节制：一章 7 关，每关若干波，怪物成群涌上来。
    // state.region 现在表示“第一章的第几关”（0 = 1-1 … 6 = 1-7）；存档字段不变。
    public enum EnemyRole { Swarmer, Brute, Ranged }
    public enum StageKind { Normal, Elite, Boss }

    public sealed class WaveDef
    {
        public readonly int swarmers, brutes, ranged, elites;
        public WaveDef(int swarm, int brute, int range, int elite = 0) { swarmers = swarm; brutes = brute; ranged = range; elites = elite; }
        public int Count { get { return swarmers + brutes + ranged + elites; } }
    }

    public sealed class StageDef
    {
        public readonly string code; public readonly StageKind kind; public readonly int backdrop; public readonly WaveDef[] waves;
        public StageDef(string id, StageKind type, int scene, params WaveDef[] list) { code = id; kind = type; backdrop = scene; waves = list; }
    }

    public sealed class RoleStats
    {
        public readonly float health, attack, interval, speed, stopMin, stopMax, canvas, dropChance;
        public readonly int art;   // ActorView.Enemy 的“街区”参数：0 巡逻机 / 1 运输机 / 2 干扰机
        public RoleStats(float hp, float atk, float every, float move, float near, float far, float size, float drop, int look)
        { health = hp; attack = atk; interval = every; speed = move; stopMin = near; stopMax = far; canvas = size; dropChance = drop; art = look; }
    }

    public static class Chapters
    {
        public const int StagesPerChapter = 7;
        public const int MaxOnScreen = 8;          // 同屏最多 8 只，其余在画面外排队
        public const int MeleeAttackers = 3;       // 同时贴身攻击的近战怪最多 3 只（其余挤在后面等位置）
        public const float BossSummonInterval = 15, StageTransition = 2.0f;

        // 第一章：1-3、1-6 精英，1-7 首领。场景套 0=A 废液沟渠 1=B 叠层棚户 2=C 泵站管廊 3=D 抽水总闸。
        public static readonly StageDef[] Chapter1 = {
            new StageDef("1-1", StageKind.Normal, 0, new WaveDef(2,0,0), new WaveDef(3,0,0), new WaveDef(4,0,0)),
            new StageDef("1-2", StageKind.Normal, 0, new WaveDef(3,0,0), new WaveDef(3,1,0), new WaveDef(3,2,0)),
            new StageDef("1-3", StageKind.Elite,  1, new WaveDef(3,1,0), new WaveDef(4,1,0), new WaveDef(2,0,0,1)),
            new StageDef("1-4", StageKind.Normal, 1, new WaveDef(3,1,0), new WaveDef(3,1,1), new WaveDef(3,2,1)),
            new StageDef("1-5", StageKind.Normal, 2, new WaveDef(3,1,1), new WaveDef(3,2,1), new WaveDef(8,0,0)),
            new StageDef("1-6", StageKind.Elite,  2, new WaveDef(3,1,1), new WaveDef(3,2,1), new WaveDef(3,0,0,2)),
            new StageDef("1-7", StageKind.Boss,   3, new WaveDef(0,0,0)),
        };
        // v0.16 第二章「物流高架」：2-3、2-6 精英，2-7 首领。场景套 4=A 高架货轨 5=B 货柜堆场 6=C 分拣仓内 7=D 总调度塔。
        public static readonly StageDef[] Chapter2 = {
            new StageDef("2-1", StageKind.Normal, 4, new WaveDef(4,1,0), new WaveDef(4,1,1), new WaveDef(5,2,1)),
            new StageDef("2-2", StageKind.Normal, 4, new WaveDef(4,2,1), new WaveDef(5,2,1), new WaveDef(6,2,2)),
            new StageDef("2-3", StageKind.Elite,  5, new WaveDef(4,2,1), new WaveDef(5,2,1), new WaveDef(3,1,0,1)),
            new StageDef("2-4", StageKind.Normal, 5, new WaveDef(5,2,2), new WaveDef(6,2,2), new WaveDef(6,3,2)),
            new StageDef("2-5", StageKind.Normal, 6, new WaveDef(5,2,2), new WaveDef(6,3,2), new WaveDef(10,0,1)),
            new StageDef("2-6", StageKind.Elite,  6, new WaveDef(5,2,2), new WaveDef(6,3,2), new WaveDef(3,1,1,2)),
            new StageDef("2-7", StageKind.Boss,   7, new WaveDef(0,0,0)),
        };
        public const int ChapterCount = 2;
        /// region：全局关卡序号。0..6 = 1-1..1-7，7..13 = 2-1..2-7。
        public static int ChapterOf(int region) { return Math.Max(0, region) / StagesPerChapter + 1; }
        public static int FirstRegion(int chapter) { return (chapter - 1) * StagesPerChapter; }
        public static int BossRegion(int chapter) { return chapter * StagesPerChapter - 1; }
        public static StageDef Stage(int region)
        {
            region = Math.Max(0, Math.Min(ChapterCount * StagesPerChapter - 1, region));
            return region < StagesPerChapter ? Chapter1[region] : Chapter2[region - StagesPerChapter];
        }
        /// 场景编号 0..3 第一章，4..7 第二章；%4 == 3 是首领场地。
        public static int SceneChapter(int scene) { return scene / 4 + 1; }

        public static readonly RoleStats[] Roles = {
            //            血     攻    间隔  速度  停步近 停步远 画布  掉落  外形
            new RoleStats(.45f, .5f,  1.3f, 200, 150, 210, 160, .25f, 0),   // 冲锋：快、血少、成群
            new RoleStats(1.4f, 1f,   2f,   110, 165, 195, 220, .50f, 1),   // 重型：慢、血厚
            new RoleStats(.7f,  .6f,  1.9f, 140, 360, 430, 200, .40f, 2),   // 远程：站在后排
        };
        public const float EliteHealth = 2.6f, EliteAttack = 1.4f, EliteCanvas = 1.2f;
        public const float BossHealth = 6, BossAttack = 1.4f, BossInterval = 1.6f, BossSpeed = 120, BossStop = 200, BossCanvas = 380;
        public const int BossArt = 1;
        public const int EliteDrops = 2, BossDrops = 3;   // v0.16：精英 2 件（1 件保底特装），首领 3 件（专属 + 保底特装）

        // 第一章数值（首版，模拟见 Docs/Chapter1_Swarm_v14.md）：关卡越往后越强，但比旧的“区域”曲线平缓。
        // v0.16 敌人血量 ×2.4：1× 速度下第一章约 1.5–2 小时（含在卡点回刷），见 Tools/sim/pacing_sim.py。
        public const float HealthScale = 2.4f;
        // 第二章整体再乘一档（2-1 约等于回声层 8–10 层，2-7 约 25 层）。首版数值，实测后再调。
        public const float Chapter2Health = 2.2f, Chapter2Attack = 1.5f;
        public static float Health(int region, int wave) { return (50 + 10 * wave) * (1 + .16f * region) * HealthScale * (region >= StagesPerChapter ? Chapter2Health : 1); }
        public static float Attack(int region, int wave) { return (4 + 1.2f * wave) * (1 + .14f * region) * (region >= StagesPerChapter ? Chapter2Attack : 1); }
        public static float Defense(int region) { return region * 3; }

        // ---------- v0.16 无尽回声层 ----------
        // 打通 1-7 后开放。层数无限：普通层 2 波；每 5 层精英层；每 10 层首领层（回声版闸门看守）。
        // 强度以 1-7 为起点按层成倍增长；掉落等级 = 15 + 层数 / 2（没有上限，装备最高 100 级）。
        // 数值来自 Tools/sim/pacing_sim.py（v0.16 首版）：1× 挂机约 1 天 30 层、1 周 90 层、1 月 110+ 层。
        public const float EchoHealthGrowth = 1.022f, EchoAttackGrowth = 1.012f, EchoBase = 2.2f;
        public static StageKind EchoKind(int floor) { return floor % 10 == 0 ? StageKind.Boss : floor % 5 == 0 ? StageKind.Elite : StageKind.Normal; }
        /// 场景：每 10 层换一个，第一章 A → B → C → 第二章 A → B → C 轮换；首领层用这一段所属章节的 D（闸门看守 / 总调度吊臂）。
        public static int EchoScene(int floor)
        {
            int block = (Math.Max(1, floor) - 1) / 10 % 6, ch = block / 3;
            return EchoKind(floor) == StageKind.Boss ? ch * 4 + 3 : ch * 4 + block % 3;
        }
        /// 回声层强度以 1-7 为起点（和玩家当前在哪一章无关）。
        public const int EchoAnchor = 6;
        public static StageDef EchoStage(int floor, bool swarmBoost)
        {
            StageKind kind = EchoKind(floor);
            if (kind == StageKind.Boss) return new StageDef("E" + floor, kind, EchoScene(floor), new WaveDef(0, 0, 0));
            int sw = Math.Min(9, 3 + floor / 10), br = Math.Min(3, 1 + floor / 20), ra = floor < 3 ? 0 : Math.Min(3, 1 + floor / 25);
            if (swarmBoost) sw = (int)Math.Ceiling(sw * 1.5f);
            int elites = kind == StageKind.Elite ? Math.Min(3, 1 + floor / 30) : 0;
            return new StageDef("E" + floor, kind, EchoScene(floor), new WaveDef(sw, br, ra), new WaveDef(sw, br, ra, elites));
        }
        public static float EchoScale(int floor, bool attack) { return EchoBase * (float)Math.Pow(attack ? EchoAttackGrowth : EchoHealthGrowth, Math.Max(0, floor - 1)); }
        public static int EchoDropLevel(int floor, bool boss, bool elite, double roll)
        {
            int lo = 15 + floor / 2, hi = lo + 2;
            if (boss) lo = hi; else if (elite) lo = lo + 1;
            return Math.Min(Balance.MaxItemLevel, lo + (int)Math.Floor(roll * (hi - lo + 1 - 1e-9)));
        }

        // 协议异常：每打过一个回声首领层（10 的倍数且刷新最高层），三选一叠加，最多 3 个。只在回声层生效。
        public sealed class Anomaly { public readonly string id, name, desc; public Anomaly(string a, string b, string c) { id = a; name = b; desc = c; } }
        public static readonly Anomaly[] Anomalies = {
            new Anomaly("armor", "厚甲", "怪物生命 +50%；普通怪掉落率 ×1.4"),
            new Anomaly("overload", "过载", "怪物出手快 25%；幸运 +15，高品质更常见"),
            new Anomaly("swarm", "狂潮", "每波冲锋怪 +50%；零件 ×1.5"),
            new Anomaly("elite", "精英化", "普通怪有 12% 变成精英（精英掉 2 件）"),
        };
        public static Anomaly FindAnomaly(string id) { foreach (Anomaly a in Anomalies) if (a.id == id) return a; return null; }
        public const int MaxAnomalies = 3, MilestoneEvery = 25, MilestoneCores = 2;

        // 掉落等级段：每关 [min, max]。普通怪在段内随机，精英偏上半段，首领取上限附近。
        public static readonly int[,] DropLevels = { { 1, 3 }, { 2, 4 }, { 4, 6 }, { 5, 7 }, { 7, 9 }, { 8, 11 }, { 12, 15 },
                                                     { 16, 19 }, { 18, 21 }, { 20, 23 }, { 22, 25 }, { 24, 27 }, { 26, 29 }, { 30, 33 } };
        public static int DropLevel(int region, bool boss, bool elite, double roll)
        {
            int r = Math.Max(0, Math.Min(DropLevels.GetLength(0) - 1, region));
            int lo = DropLevels[r, 0], hi = DropLevels[r, 1];
            if (boss) lo = Math.Max(lo, hi - 1);
            else if (elite) lo = (lo + hi + 1) / 2;
            return lo + (int)Math.Floor(roll * (hi - lo + 1 - 1e-9));
        }
    }
}
