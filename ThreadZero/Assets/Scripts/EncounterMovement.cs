using System;

namespace ThreadZero
{
    // 一只怪。位置是世界坐标（逻辑像素），渲染与战斗共用。
    public sealed class Enemy
    {
        public int id;
        public EnemyRole role;
        public bool elite, boss;
        public float health, maxHealth, attack, interval, timer, speed, stop, canvas;
        public float x, depth;                 // depth 0..1：站位前后错开（画面上 0~20 像素），越大越靠前
        public float hitFlash, strikeFlash, travel, deadAt = -1;
        public bool moving;
        public bool Alive { get { return health > 0; } }
        public bool Ranged { get { return role == EnemyRole.Ranged && !boss; } }
        public int Art { get { return boss ? Chapters.BossArt : Chapters.Roles[(int)role].art; } }
    }

    // 角色的位置、速度与镜头。怪的移动在 CombatSession 里，每只各走各的。
    public sealed class EncounterMovement
    {
        public const float InitialHero = 428, HeroSpeed = 220, MeleeRange = 200, GunRange = 380;
        public float HeroX { get; private set; } = InitialHero;
        public float CameraX { get; private set; }
        public float HeroTravel { get; private set; }
        public float HeroVelocity { get; private set; }
        public float HeroRange { get; private set; } = MeleeRange;
        public float HeroScreenX { get { return HeroX - CameraX; } }
        public bool HeroMoving { get { return HeroVelocity > .01f; } }

        public void ResetAtCamera(float camera = 0)
        { CameraX = Math.Max(0, camera); HeroX = CameraX + InitialHero; HeroVelocity = HeroTravel = 0; }
        public void Stop() { HeroVelocity = 0; }
        public void Tick(float dt, bool melee, bool walk)
        {
            if (dt <= 0 || float.IsNaN(dt)) return;
            HeroRange = melee ? MeleeRange : GunRange;
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
