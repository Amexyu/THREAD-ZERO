using System;
using System.Linq;

namespace ThreadZero
{
    /// v0.21 机库的机器人在战斗里做的事（企划 9A 第四步）：上场两台。
    ///   前排：替主角挡下 35% 的伤害，自己有血（主角最大生命 × 系数 × 等级），被打坏 10 秒后重新上场；
    ///   后排：隔几秒打一发（主角攻击 × 系数 × 等级，吃「特效伤害」）；
    ///   支援：回收无人机定时回血，镜面无人机定时给护盾（和改造板「护盾回路」用同一个护盾）。
    public sealed partial class CombatSession
    {
        public readonly float[] robotTimer = { 1, 1.6f };
        public readonly float[] robotFlash = new float[2];   // 出手 / 回血的那一下，界面画火光用
        public float tankHp, tankDown, tankFlash;            // 前排机器人的血、被打坏后还要多久回来、挨打时闪一下
        public Action<int, Enemy> OnRobotShot;                // 上场的第几台、打到的怪
        public Action<int> OnRobotSupport;

        private void ResetRobots(Stats s) { robotTimer[0] = 1; robotTimer[1] = 1.6f; robotFlash[0] = robotFlash[1] = 0; tankDown = 0; tankHp = TankMax(s); }
        private int TankSlot { get { for (int i = 0; i < 2; i++) { int r = model.state.deployed[i]; if (r >= 0 && Catalog.Robots[r].role == 0) return i; } return -1; } }
        public bool HasTank { get { return TankSlot >= 0; } }
        /// 机库里换了上场的机器人或升了级：前排的血按新的上限重算。
        public void RobotsChanged() { tankDown = 0; tankHp = TankMax(model.Total()); }
        public float TankMax(Stats s)
        {
            int slot = TankSlot; if (slot < 0) return 0;
            int r = model.state.deployed[slot]; return s.health * Catalog.Robots[r].power * model.RobotFactor(r);
        }
        private void RobotsTick(float dt, Stats s)
        {
            tankFlash = Math.Max(0, tankFlash - dt);
            if (tankDown > 0) { tankDown -= dt; if (tankDown <= 0) { tankDown = 0; tankHp = TankMax(s); } }
            for (int slot = 0; slot < 2; slot++)
            {
                robotFlash[slot] = Math.Max(0, robotFlash[slot] - dt);
                int r = model.state.deployed[slot]; if (r < 0) continue;
                RobotDef d = Catalog.Robots[r]; if (d.role == 0) continue;   // 前排是被动的，见 TankAbsorb
                float f = model.RobotFactor(r);
                if (d.role == 1)
                {
                    if (!HeroInRange) { robotTimer[slot] = Math.Min(robotTimer[slot], .6f); continue; }
                    robotTimer[slot] -= dt; if (robotTimer[slot] > 0) continue;
                    robotTimer[slot] += d.interval;
                    Enemy t = Target; if (t == null) continue;
                    float dmg = Mitigate(s.attack * d.power * f) * (1 + s.effectDamage);
                    DealDamage(t, dmg, s); robotFlash[slot] = .25f; OnRobotShot?.Invoke(slot, t);
                    if (d.pierce)
                    {
                        Enemy behind = enemies.Where(e => e != t && e.Alive && e.x > t.x).OrderBy(e => e.x).FirstOrDefault();
                        if (behind != null) DealDamage(behind, dmg * .7f, s);
                    }
                }
                else
                {
                    robotTimer[slot] -= dt; if (robotTimer[slot] > 0) continue;
                    robotTimer[slot] += d.interval;
                    if (d.id == "r0") { if (health >= s.health) continue; health = Math.Min(s.health, health + s.health * Math.Min(.06f, d.power * f)); }
                    else shield = Math.Min(s.health * .3f, shield + s.health * Math.Min(.12f, d.power * f));
                    robotFlash[slot] = .5f; OnRobotSupport?.Invoke(slot);
                }
            }
        }
        /// 前排机器人替主角挡一部分伤害；被打坏了 10 秒后重新上场。返回挡下以后主角还要吃的伤害。
        private float TankAbsorb(float damage, Stats s)
        {
            if (TankSlot < 0 || tankDown > 0 || damage <= 0) return damage;
            if (tankHp <= 0) tankHp = TankMax(s);
            float share = damage * Balance.TankShare; tankHp -= share; tankFlash = .2f;
            if (tankHp <= 0) { tankHp = 0; tankDown = Balance.TankRespawn; OnMessage?.Invoke("前排机器人被打坏了，10 秒后重新上场"); }
            return damage - share;
        }
    }
}
