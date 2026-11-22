using System;

namespace ThreadZero
{
    /// 怪的攻击模组：冲锋怪扑咬 / 重型怪砸地 / 远程怪蓄能弹；精英「拆解者」重砸击退、「信号塔」给周围加攻速；首领「闸门看守」。
    public enum EnemyMove { Lunge, Slam, Bolt, Crusher, Beacon, Gate }

    // 一只怪。位置是世界坐标（逻辑像素），渲染与战斗共用。
    public sealed class Enemy
    {
        public int id;
        public EnemyRole role;
        public bool elite, boss;
        public float health, maxHealth, attack, interval, timer, speed, stop, canvas;
        public float x, depth;                 // depth 0..1：站位前后错开（画面上 0~20 像素），越大越靠前
        public float hitFlash, travel, deadAt = -1;
        public EnemyMove move;
        public float windLeft, windDur, sinceStrike = 9, hasteLeft, aux;   // 蓄力剩余 / 总时长、距上次出手、加速剩余、模组自用计时
        public int phase;          // 首领阶段：0 正常，1 已放水，2 过载
        public bool special;       // 下一次出手是特殊招式（首领放水 / 吊箱砸落）
        public int skin;           // 外观与招式变体：0 第一章，1 第二章（物流高架），2 第三章（信号禁区）
        public bool summoned;      // 首领 / 运输母机放出来的小怪：不掉装备
        public int aux2;           // 运输母机放的无人机：记住母机 id
        public float pull;         // 还要被吊钩拽过来的距离
        public float vel;          // 当前移动速度（和角色一样有加速 / 减速，不再瞬间起步、瞬间停）
        public float popAt = -10, popAmp, zapAt = -10;   // v0.20 只管画面：被震起的时刻 / 高度、被电到的时刻
        public float Wind01 { get { return windLeft > 0 && windDur > 0 ? 1 - windLeft / windDur : 0; } }
        public bool Heavy { get { return move == EnemyMove.Slam || move == EnemyMove.Crusher || move == EnemyMove.Gate; } }
        public bool moving;
        public bool Alive { get { return health > 0; } }
        public bool Ranged { get { return role == EnemyRole.Ranged && !boss; } }
        public int Art { get { return boss ? Chapters.BossArt : Chapters.Roles[(int)role].art; } }
    }

    // 角色的位置、速度与镜头。怪的移动在 CombatSession 里，每只各走各的。
    public sealed class EncounterMovement
    {
        public const float InitialHero = 428, HeroSpeed = 220, MeleeRange = 200, GunRange = 380, ChainRange = 300;
        /// 武器自带的射程（例如锁链 300）；0 = 按近战 / 远程默认。
        public float RangeOverride;
        public float HeroX { get; private set; } = InitialHero;
        public float CameraX { get; private set; }
        public float HeroTravel { get; private set; }
        public float HeroVelocity { get; private set; }
        public float HeroRange { get; private set; } = MeleeRange;
        public float HeroScreenX { get { return HeroX - CameraX; } }
        public bool HeroMoving { get { return HeroVelocity > .01f; } }

        public void ResetAtCamera(float camera = 0)
        { CameraX = Math.Max(0, camera); HeroX = CameraX + InitialHero; HeroVelocity = HeroTravel = 0; }
        public void Stop() { HeroVelocity = 0; pushV = 0; }
        // 被击退：总距离约 dist，先快后慢（指数衰减）。
        private float pushV;
        public bool Pushed { get { return pushV > 40; } }
        public void Push(float dist) { pushV += dist * 6; }
        public void Tick(float dt, bool melee, bool walk)
        {
            if (dt <= 0 || float.IsNaN(dt)) return;
            HeroRange = RangeOverride > 0 ? RangeOverride : melee ? MeleeRange : GunRange;
            if (pushV > 0)
            {
                HeroX = Math.Max(CameraX + 160, HeroX - pushV * dt);
                pushV *= (float)Math.Exp(-6 * dt); if (pushV < 5) pushV = 0;
                if (Pushed) walk = false;
            }
            float target = walk ? HeroSpeed : 0, max = 900 * dt;
            HeroVelocity = HeroVelocity < target ? Math.Min(target, HeroVelocity + max) : Math.Max(target, HeroVelocity - max);
            float d = HeroVelocity * dt; HeroX += d; HeroTravel += d;
            float cameraTarget = Math.Max(CameraX, HeroX - 640);
            CameraX += (cameraTarget - CameraX) * (1 - (float)Math.Exp(-6 * dt));
        }
    }

    public sealed partial class CombatSession
    {
        public readonly EncounterMovement Movement = new EncounterMovement();
        public bool MovementEnabled { get; private set; } = true;
        public void EnableMovement() { MovementEnabled = true; Movement.ResetAtCamera(); }
        /// 角色的攻击对象（最近的一只活怪）在射程内。
        public bool HeroInRange { get { Enemy t = Target; return t != null && t.x - Movement.HeroX <= Movement.HeroRange + .1f; } }
        /// 至少有一只怪已经走到位、正在打角色。
        public bool EnemyInRange { get { foreach (Enemy e in enemies) if (e.Alive && Arrived(e)) return true; return false; } }
        private bool Arrived(Enemy e) { return e.x - Movement.HeroX <= e.stop + 2; }
    }
}
