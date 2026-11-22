using System;
using System.Collections.Generic;

namespace ThreadZero
{
    /// 远程怪打出的蓄能弹（世界坐标）。飞到角色身上才结算伤害。
    public sealed class EnemyShot { public Enemy from; public float x0, born, dur, damage; }
    public enum EnemyActKind { Windup, Strike, ShotHit, Flood, Pulse, Enrage, Deploy }
    public struct EnemyAct { public Enemy e; public EnemyActKind kind; public float x; }

    // 怪的攻击模组：计时到 → 蓄力（有前摇、重击有预警）→ 出手（近战此刻结算，远程发弹、命中时结算）。
    public sealed partial class CombatSession
    {
        public readonly List<EnemyShot> shots = new List<EnemyShot>();
        public Action<EnemyAct> OnEnemyAct;
        public const float ShotSpeed = 950, HasteRate = 1.4f, HasteTime = 3, PulseEvery = 4, PulseRange = 460;
        public const float CrusherPush = 90, FloodPush = 240, FloodHit = 1.5f, EnragedInterval = .65f;
        public const float CratePush = 110, CrateHit = 1.8f, DeployEvery = 6;   // 第二章：吊箱砸落、运输母机放无人机
        public const int DeployCap = 3;
        // v0.18 第三章首领「断讯监视者」：断讯脉冲——重击（×1.3）、推开一小段，并且 4 秒内角色出手变慢（攻速 ×0.45）。
        public const float JamTime = 4, JamRate = .45f, JamHit = 1.3f, JamPush = 60;
        public float JamLeft { get; private set; }
        // v0.18 第四章首领「镜面审计官」：镜面反射——5 秒内角色打它的伤害有 20% 反弹给角色（受防御减免）。
        public const float MirrorTime = 5, MirrorRatio = .2f;
        public float MirrorLeft { get; private set; }
        // v0.18 第五章首领「织网母体」：织网收束——重击 ×1.6、3 秒断讯，并立刻放出 3 只织网线虫。
        public const float WeaveHit = 1.6f, WeaveJam = 3; public const int WeaveSummon = 3;

        private static float WindTime(Enemy e)
        {
            if (e.special) return 1.2f;   // 首领放水：前摇最长，给玩家看清
            switch (e.move)
            {
                case EnemyMove.Lunge: return .28f;
                case EnemyMove.Slam: return .62f;
                case EnemyMove.Crusher: return .8f;
                case EnemyMove.Bolt: return .5f;
                case EnemyMove.Beacon: return .55f;
                default: return e.phase >= 2 ? .6f : .9f;   // 闸门看守
            }
        }
        private void Act(Enemy e, EnemyActKind kind, float x) { OnEnemyAct?.Invoke(new EnemyAct { e = e, kind = kind, x = x }); }

        private void EnemiesAttack(float dt, Stats s)
        {
            if (UpdateShots(s)) return;
            BossPhases();
            // 近战：最靠前的 3 只能打，其余挤在后面等位置（计时不走）；远程走到位就打。
            var melee = new List<Enemy>();
            foreach (Enemy e in enemies) if (e.Alive && !e.Ranged && Arrived(e)) melee.Add(e);
            melee.Sort((a, b) => a.x.CompareTo(b.x));
            if (melee.Count > Chapters.MeleeAttackers) melee.RemoveRange(Chapters.MeleeAttackers, melee.Count - Chapters.MeleeAttackers);
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy e = enemies[i];
                if (!e.Alive) continue;
                if (e.windLeft > 0)
                {   // 已经起手的招式一定打完（除非它先死了）
                    e.windLeft -= dt;
                    if (e.windLeft <= 0) { e.windLeft = 0; if (Strike(e, s)) return; }
                    continue;
                }
                if (!Arrived(e)) continue;
                if (e.move == EnemyMove.Beacon) Pulse(e, dt);
                if (!e.Ranged && !melee.Contains(e)) continue;
                e.timer -= dt * (e.hasteLeft > 0 ? HasteRate : 1);
                if (e.timer > 0) continue;
                e.timer += e.interval;
                if (e.attack <= 0) continue;
                e.windDur = e.windLeft = WindTime(e);
                Act(e, EnemyActKind.Windup, e.x);
            }
        }

        /// 出手。返回 true = 角色被打倒。
        private bool Strike(Enemy e, Stats s)
        {
            e.sinceStrike = 0;
            switch (e.move)
            {
                case EnemyMove.Bolt:
                case EnemyMove.Beacon:
                {
                    float x0 = e.x - e.canvas * .32f;
                    shots.Add(new EnemyShot { from = e, x0 = x0, born = elapsed, dur = Math.Max(.18f, (x0 - Movement.HeroX) / ShotSpeed), damage = e.attack });
                    Act(e, EnemyActKind.Strike, x0);
                    return false;
                }
                case EnemyMove.Crusher:
                    Act(e, EnemyActKind.Strike, Movement.HeroX);
                    Movement.Push(CrusherPush);
                    return Hurt(e.attack * 1.1f, e.x, s);
                case EnemyMove.Gate:
                    if (e.special)
                    {   // 闸门看守：开闸放水，把角色冲后一大段；总调度吊臂：吊箱砸在角色头上，冲击小但更痛
                        e.special = false;
                        if (e.skin == 3)
                        {   // 镜面反射：举起镜面，这一下不痛，但接下来 5 秒打它会反伤
                            Act(e, EnemyActKind.Flood, Movement.HeroX);
                            MirrorLeft = MirrorTime;
                            return Hurt(e.attack * .8f, e.x, s);
                        }
                        if (e.skin >= 4)
                        {   // 织网收束
                            Act(e, EnemyActKind.Flood, Movement.HeroX);
                            JamLeft = WeaveJam; Movement.Push(JamPush);
                            for (int i = 0; i < WeaveSummon; i++) { Enemy m = Make(EnemyRole.Swarmer, false); m.summoned = true; m.health = m.maxHealth = m.health * Chapters.SummonHealth; queue.Add(m); }
                            return Hurt(e.attack * WeaveHit, e.x, s);
                        }
                        if (e.skin >= 2)
                        {
                            Act(e, EnemyActKind.Flood, Movement.HeroX);
                            JamLeft = JamTime; Movement.Push(JamPush);
                            return Hurt(e.attack * JamHit, e.x, s);
                        }
                        Act(e, EnemyActKind.Flood, Movement.HeroX);
                        Movement.Push(e.skin == 1 ? CratePush : FloodPush);
                        return Hurt(e.attack * (e.skin == 1 ? CrateHit : FloodHit), e.x, s);
                    }
                    Act(e, EnemyActKind.Strike, Movement.HeroX);
                    return Hurt(e.attack, e.x, s);
                default:   // 扑咬、砸地
                    Act(e, EnemyActKind.Strike, Movement.HeroX);
                    return Hurt(e.attack, e.x, s);
            }
        }

        private bool UpdateShots(Stats s)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                EnemyShot shot = shots[i];
                if (elapsed - shot.born < shot.dur) continue;
                shots.RemoveAt(i);
                Act(shot.from, EnemyActKind.ShotHit, Movement.HeroX);
                if (Hurt(shot.damage, shot.from.x, s)) return true;
            }
            return false;
        }

        // 精英「信号塔」：每 4 秒发一圈信号，附近的怪 3 秒内攻速 ×1.4。
        // 第二章「运输母机」：每 6 秒从货舱放下一架货运无人机（同屏它放的最多 3 架；放出来的不掉装备）。
        private void Pulse(Enemy e, float dt)
        {
            e.aux -= dt;
            if (e.aux > 0) return;
            if (e.skin == 1)
            {
                e.aux += DeployEvery;
                int mine = 0; foreach (Enemy o in enemies) if (o.Alive && o.summoned && o.aux2 == e.id) mine++;
                if (mine >= DeployCap || AliveOnStage() >= Chapters.MaxOnScreen) return;
                Enemy d = Make(EnemyRole.Swarmer, false); d.summoned = true; d.aux2 = e.id; d.x = e.x - 40; d.health = d.maxHealth = d.health * .6f;
                enemies.Add(d);
                Act(e, EnemyActKind.Deploy, e.x);
                return;
            }
            e.aux += PulseEvery;
            foreach (Enemy o in enemies) if (o != e && o.Alive && Math.Abs(o.x - e.x) <= PulseRange) o.hasteLeft = HasteTime;
            Act(e, EnemyActKind.Pulse, e.x);
        }

        // 首领「闸门看守」：50% 血开闸放水（重击 + 把角色冲后一段），25% 血过载（出手变快）。
        private void BossPhases()
        {
            foreach (Enemy e in enemies)
            {
                if (!e.boss || !e.Alive) continue;
                float hp = e.health / Math.Max(1, e.maxHealth);
                if (e.phase == 0 && hp <= .5f)
                {
                    e.phase = 1; e.special = true; e.windDur = e.windLeft = WindTime(e);
                    Act(e, EnemyActKind.Windup, e.x);
                }
                else if (e.phase == 1 && hp <= .25f && !e.special)
                {
                    e.phase = 2; e.interval *= EnragedInterval; e.timer = Math.Min(e.timer, e.interval);
                    Act(e, EnemyActKind.Enrage, e.x);
                }
            }
        }

        /// 角色挨一下（护盾模块减伤并充能）。返回 true = 被打倒。
        private bool Hurt(float attack, float fromX, Stats s)
        {
            float raw = GameModel.Damage(attack, s.defense);
            float blocked = raw * Balance.GuardReduction * model.EffectCount(Effect.Guard);
            float damage = raw - blocked;
            if (GameModel.TestPower) damage = 0;
            if (blocked > 0)
            { guardCharge = Math.Min(s.attack, guardCharge + blocked * Balance.GuardChargeRatio); guardFlash = .35f; Emit(Ability.GuardCharge, blocked); }
            health = Math.Max(0, health - damage); lastEnemyDamage = damage; hurtFlash = .25f; lastHurtX = fromX;
            OnHit?.Invoke();
            if (health > 0) return false;
            recovery = 3; ResetModules(); Movement.Stop(); shots.Clear();
            OnMessage?.Invoke(model.state.region % Chapters.StagesPerChapter > 0 ? "失去战斗能力，修好后撤回上一关补给；变强后自动再挑战。" : "失去战斗能力，3 秒后维修重试；无装备损失。");
            OnCheckpoint?.Invoke();
            return true;
        }
    }
}
