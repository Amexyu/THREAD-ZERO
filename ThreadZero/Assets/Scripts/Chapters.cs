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
        public static StageDef Stage(int region) { return Chapter1[Math.Max(0, Math.Min(Chapter1.Length - 1, region))]; }

        public static readonly RoleStats[] Roles = {
            //            血     攻    间隔  速度  停步近 停步远 画布  掉落  外形
            new RoleStats(.45f, .5f,  1.3f, 200, 150, 210, 160, .10f, 0),   // 冲锋：快、血少、成群
            new RoleStats(1.4f, 1f,   2f,   110, 165, 195, 220, .30f, 1),   // 重型：慢、血厚
            new RoleStats(.7f,  .6f,  1.9f, 140, 360, 430, 200, .20f, 2),   // 远程：站在后排
        };
        public const float EliteHealth = 2.6f, EliteAttack = 1.4f, EliteCanvas = 1.2f;
        public const float BossHealth = 6, BossAttack = 1.4f, BossInterval = 1.6f, BossSpeed = 120, BossStop = 200, BossCanvas = 300;
        public const int BossArt = 1;

        // 第一章数值（首版，模拟见 Docs/Chapter1_Swarm_v14.md）：关卡越往后越强，但比旧的“区域”曲线平缓。
        public static float Health(int region, int wave) { return (50 + 10 * wave) * (1 + .16f * region); }
        public static float Attack(int region, int wave) { return (4 + 1.2f * wave) * (1 + .14f * region); }
        public static float Defense(int region) { return region * 3; }

        // 掉落等级段：每关 [min, max]。普通怪在段内随机，精英偏上半段，首领取上限附近。
        public static readonly int[,] DropLevels = { { 1, 3 }, { 2, 4 }, { 4, 6 }, { 5, 7 }, { 7, 9 }, { 8, 11 }, { 12, 15 } };
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
