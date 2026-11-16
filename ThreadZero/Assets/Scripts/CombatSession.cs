using System;
using System.Collections.Generic;

namespace ThreadZero
{
    public enum Ability { Drone, Mark, Arc, Surge, Dash, Assault, GuardCharge, GuardRelease }
    public sealed class AbilityEvent
    {
        public Ability ability;
        public float damage;
        public AbilityEvent(Ability kind, float value = 0) { ability = kind; damage = value; }
    }

    // v0.14 群怪战斗：一关 = 若干波，每波一群怪从右侧分批涌上来，各自走位、各自攻击、可以重叠。
    // 角色打最近的一只；电弧/推进/技能穿透会波及身后的怪。打完最后一波 = 本关通过（StageCleared），由 GameApp 播转场。
    public sealed partial class CombatSession
    {
        public readonly GameModel model;
        public float health, attackTimer, recovery;
        public int wave = 1, attackNumber;
        public float lastPlayerDamage, lastEnemyDamage, hitFlash, hurtFlash;
        public float droneFlash, arcFlash, dashFlash, guardFlash, markTime, guardCharge;
        public float lastDroneDamage, lastArcDamage, lastDashDamage;
        public int droneHits, arcHits, dashHits, combos, packShifts;
        public bool lastCritical;
        public float lastHitX, lastKillX, lastHurtX;      // 世界坐标：伤害数字、掉落位置
        public bool StageCleared { get; private set; }
        public bool RetreatRequested { get; set; }          // 被打倒并修好后，GameApp 退回上一关
        // 转场后角色从地面检修舱口爬上来（EntryTime 秒），期间不战斗、不出怪。
        public const float EntryTime = .9f;
        public float EntryLeft { get; private set; }
        private float droneTimer = 1, markTimer = .7f, dashTimer = 2;
        private string loadoutSignature;
        private bool pendingStrike, testDummy;
        private float strikeDelay, spawnTimer, waveDelay, summonTimer, elapsed;
        private int nextId;
        private readonly Random rng = new Random(1234);
        public Action<float> OnAdvance;
        public Action<string> OnMessage;
        public Action<Item, bool> OnDrop;
        public Action OnCheckpoint;
        public Action<AbilityEvent> OnAbility;
        public Action OnBasicAttack, OnHit;
        /// 每一次对怪造成伤害（含范围、多段）：伤害值、是否暴击、怪的世界坐标 x。界面用它逐个弹伤害数字。
        public Action<float, bool, float> OnDamage;

        // 活着的怪 + 刚倒下的残骸（deadAt >= 0）。queue：本波还没出场的怪。
        public readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Enemy> queue = new List<Enemy>();

        public StageDef Stage { get { return Chapters.Stage(model.state.region); } }
        public int WaveCount { get { return Stage.waves.Length; } }
        public bool Boss { get { return Stage.kind == StageKind.Boss; } }
        public bool Elite { get { return Stage.kind == StageKind.Elite && wave == WaveCount; } }
        public float Elapsed { get { return elapsed; } }
        public int Alive { get { int n = queue.Count; foreach (Enemy e in enemies) if (e.Alive) n++; return n; } }
        public Enemy Target
        {
            get
            {
                Enemy best = null;
                foreach (Enemy e in enemies) if (e.Alive && (best == null || e.x < best.x)) best = e;
                return best;
            }
        }
        public Enemy BossEnemy { get { foreach (Enemy e in enemies) if (e.Alive && e.boss) return e; foreach (Enemy e in queue) if (e.boss) return e; return null; } }
        public float enemyHealth { get { Enemy t = Target; return t == null ? 0 : t.health; } }
        public float enemyMaxHealth { get { Enemy t = Target; return t == null ? 1 : t.maxHealth; } }
        public string EnemyName { get { Enemy t = Target; return t == null ? "" : NameOf(t); } }
        public string NameOf(Enemy e)
        {
            if (e.boss) return Catalog.BossNames[Math.Min(Catalog.BossNames.Length - 1, model.state.region)];
            string[] names = { "失控巡逻机", "装甲运输机", "信号干扰机" };
            return (e.elite ? "精英·" : "") + names[(int)e.role];
        }

        public CombatSession(GameModel m)
        { model = m; health = m.Total().health; loadoutSignature = Signature(); StartWave(); }
        private string Signature() { return string.Join("/", model.state.equipped); }
        private void ResetModules()
        {
            droneTimer = Balance.DroneInterval; markTimer = .7f; dashTimer = Balance.ThrusterInterval;
            markTime = guardCharge = droneFlash = arcFlash = dashFlash = guardFlash = 0;
            attackNumber = 0; pendingStrike = false;
            ResetDagger();
        }
        public void SyncLoadout()
        {
            string current = Signature();
            if (current == loadoutSignature) return;
            loadoutSignature = current; ResetModules(); ClampHealth();
        }
        /// 重新开始当前关（换关、转场、维修后）。
        public void ResetRegion()
        {
            wave = 1; recovery = 0; StageCleared = false; RetreatRequested = false;
            health = model.Total().health; ResetModules();
            enemies.Clear(); queue.Clear();
            Movement.ResetAtCamera(Movement.CameraX);
            StartWave();
            EntryLeft = EntryTime; spawnTimer = .6f;   // 爬出来 0.6 秒后第一波开始涌入
        }
        public void ClampHealth() { health = Math.Min(health, model.Total().health); }
        /// 素材审核用：怪不会死也不会打人。
        public void MakeTestDummy() { testDummy = true; foreach (Enemy e in enemies) { e.health = e.maxHealth = 100000; e.attack = 0; } foreach (Enemy e in queue) { e.health = e.maxHealth = 100000; e.attack = 0; } }
        public void ClearPack() { queue.Clear(); }

        public void Tick(float dt)
        {
            for (float remaining = Math.Min(Math.Max(dt, 0), 2); remaining > .00001f;)
            { float step = Math.Min(.05f, remaining); Step(step); remaining -= step; }
        }
        private void Emit(Ability ability, float damage = 0) { OnAbility?.Invoke(new AbilityEvent(ability, damage)); }
        private float Mitigate(float damage)
        { Enemy t = Target; return GameModel.Damage(damage, Chapters.Defense(model.state.region) + (t != null && t.boss ? 6 : 0)); }

        // ---------- 波次与出怪 ----------
        private Enemy Make(EnemyRole role, bool elite)
        {
            RoleStats r = Chapters.Roles[(int)role];
            int region = model.state.region;
            var e = new Enemy
            {
                id = ++nextId, role = role, elite = elite,
                health = Chapters.Health(region, wave) * r.health * (elite ? Chapters.EliteHealth : 1),
                attack = Chapters.Attack(region, wave) * r.attack * (elite ? Chapters.EliteAttack : 1),
                interval = r.interval, speed = r.speed * (elite ? .85f : 1),
                stop = r.stopMin + (float)rng.NextDouble() * (r.stopMax - r.stopMin),
                canvas = r.canvas * (elite ? Chapters.EliteCanvas : 1),
                depth = (float)rng.NextDouble(),
            };
            e.maxHealth = e.health; e.timer = e.interval * (.4f + .6f * (float)rng.NextDouble());
            if (testDummy) { e.health = e.maxHealth = 100000; e.attack = 0; }
            return e;
        }
        private Enemy MakeBoss()
        {
            int region = model.state.region;
            var e = new Enemy
            {
                id = ++nextId, role = EnemyRole.Brute, boss = true,
                health = Chapters.Health(region, 5) * Chapters.BossHealth, attack = Chapters.Attack(region, 5) * Chapters.BossAttack,
                interval = Chapters.BossInterval, speed = Chapters.BossSpeed, stop = Chapters.BossStop, canvas = Chapters.BossCanvas, depth = .5f,
            };
            e.maxHealth = e.health; e.timer = 1;
            if (testDummy) { e.health = e.maxHealth = 100000; e.attack = 0; }
            return e;
        }
        private void StartWave()
        {
            queue.Clear(); spawnTimer = .2f; waveDelay = 0;
            WaveDef w = Stage.waves[Math.Max(0, Math.Min(WaveCount - 1, wave - 1))];
            if (Boss) { queue.Add(MakeBoss()); summonTimer = Chapters.BossSummonInterval; }
            for (int i = 0; i < w.elites; i++) queue.Add(Make(i % 2 == 0 ? EnemyRole.Brute : EnemyRole.Ranged, true));
            var mob = new List<Enemy>();
            for (int i = 0; i < w.swarmers; i++) mob.Add(Make(EnemyRole.Swarmer, false));
            for (int i = 0; i < w.brutes; i++) mob.Add(Make(EnemyRole.Brute, false));
            for (int i = 0; i < w.ranged; i++) mob.Add(Make(EnemyRole.Ranged, false));
            while (mob.Count > 0) { int k = rng.Next(mob.Count); queue.Add(mob[k]); mob.RemoveAt(k); }
        }
        private int AliveOnStage() { int n = 0; foreach (Enemy e in enemies) if (e.Alive) n++; return n; }
        private void SpawnFromQueue(float dt)
        {
            spawnTimer -= dt;
            while (queue.Count > 0 && spawnTimer <= 0 && AliveOnStage() < Chapters.MaxOnScreen)
            {
                Enemy e = queue[0]; queue.RemoveAt(0);
                // 从画面右侧外面冲进来（镜头右缘约在角色前方 930 像素）。
                e.x = Math.Max(Movement.HeroX + 980 + (float)rng.NextDouble() * 140, Movement.CameraX + 1650);
                enemies.Add(e);
                spawnTimer += .25f + (float)rng.NextDouble() * .35f;
            }
        }

        // ---------- 每一小步 ----------
        private void Step(float dt)
        {
            elapsed += dt;
            OnAdvance?.Invoke(dt);
            hitFlash = Math.Max(0, hitFlash - dt); hurtFlash = Math.Max(0, hurtFlash - dt);
            droneFlash = Math.Max(0, droneFlash - dt); arcFlash = Math.Max(0, arcFlash - dt);
            dashFlash = Math.Max(0, dashFlash - dt); guardFlash = Math.Max(0, guardFlash - dt);
            foreach (Enemy e in enemies) { e.hitFlash = Math.Max(0, e.hitFlash - dt); e.strikeFlash = Math.Max(0, e.strikeFlash - dt); }
            enemies.RemoveAll(e => !e.Alive && (elapsed - e.deadAt > 8 || e.x < Movement.CameraX - 300));
            SyncLoadout();
            if (recovery > 0)
            {
                Movement.Stop();
                recovery -= dt;
                if (recovery <= 0) RetreatRequested = true;
                return;
            }
            if (StageCleared) { Movement.Tick(dt, model.IsMelee, true); return; }
            if (EntryLeft > 0) { EntryLeft = Math.Max(0, EntryLeft - dt); Movement.Tick(dt, model.IsMelee, false); return; }
            Stats s = model.Total(); health = Math.Min(health, s.health);

            SpawnFromQueue(dt);
            if (Boss && BossEnemy != null)
            {
                summonTimer -= dt;
                if (summonTimer <= 0)
                {
                    summonTimer += Chapters.BossSummonInterval;
                    int n = 2 + rng.Next(2);
                    for (int i = 0; i < n; i++) queue.Add(Make(EnemyRole.Swarmer, false));
                    OnMessage?.Invoke("首领呼叫了支援");
                }
            }

            // 移动：角色在攻击对象进入射程前一直往前走；怪各自走到自己的停步距离。
            Enemy target = Target;
            bool busy = pendingStrike || DaggerActive;
            bool walk = !busy && (target == null || target.x - Movement.HeroX > Movement.HeroRange);
            Movement.Tick(dt, model.IsMelee, walk);
            foreach (Enemy e in enemies)
            {
                if (!e.Alive) { e.moving = false; continue; }
                float want = Movement.HeroX + e.stop;
                float before = e.x;
                if (e.x > want) e.x = Math.Max(want, e.x - e.speed * dt);
                e.moving = e.x < before - .01f; e.travel += before - e.x;
            }

            bool engaged = HeroInRange;
            markTime = Math.Max(0, markTime - dt);
            if (engaged && model.Has(Effect.Mark))
            {
                markTimer -= dt;
                if (markTimer <= 0) { markTime = Balance.MarkDuration; markTimer += Balance.MarkInterval; Emit(Ability.Mark); }
            }
            attackTimer -= dt;
            if (!engaged) { attackTimer = Math.Max(0, attackTimer); pendingStrike = false; }
            AdvanceDagger(dt, s);
            if (pendingStrike) strikeDelay -= dt;
            if (pendingStrike && strikeDelay <= .00001f)
            {
                pendingStrike = false; attackNumber++;
                target = Target;
                if (target != null && HeroInRange) BasicHit(target, s);
            }
            if (attackTimer <= 0 && !pendingStrike && !DaggerActive && HeroInRange)
            {
                if (!BeginDagger(s))
                {
                    attackTimer += 1 / (1 + s.haste); pendingStrike = true;
                    strikeDelay = DollAnimation.Windup(model.IsMelee) + (model.IsMelee ? .06f : .12f);
                    OnBasicAttack?.Invoke();
                }
            }
            if (HeroInRange && model.Has(Effect.Drone))
            {
                droneTimer -= dt;
                if (droneTimer <= 0)
                {
                    droneTimer += Balance.DroneInterval; droneFlash = .35f; droneHits++;
                    lastDroneDamage = Mitigate(s.attack * Balance.DroneDamage) * (markTime > 0 ? Balance.MarkedDroneMultiplier : 1);
                    Emit(Ability.Drone, lastDroneDamage); lastCritical = false;
                    Enemy t = Target; if (t != null) DealDamage(t, lastDroneDamage, s);
                }
            }
            if (HeroInRange && model.Has(Effect.Thruster))
            {
                dashTimer -= dt;
                if (dashTimer <= 0)
                {
                    dashTimer += Balance.ThrusterInterval; dashFlash = .4f; dashHits++;
                    lastDashDamage = Mitigate(s.attack * Balance.ThrusterDamage) * (model.IsMelee ? Balance.AssaultMultiplier : 1);
                    if (model.IsMelee) combos++;
                    Emit(model.IsMelee ? Ability.Assault : Ability.Dash, lastDashDamage); lastCritical = false;
                    Enemy t = Target;
                    if (t != null) { DealDamage(t, lastDashDamage, s); Area(t, Movement.HeroX + 320, lastDashDamage * Balance.ThrusterPierce, 99, s); }
                }
            }
            EnemiesAttack(dt, s);
            if (recovery > 0) return;

            // 本波清空：下一波；最后一波清空 = 过关。
            if (queue.Count == 0 && AliveOnStage() == 0)
            {
                waveDelay += dt;
                if (waveDelay >= .6f)
                {
                    if (wave < WaveCount)
                    {
                        health = Math.Min(s.health, health + s.health * (Elite ? Balance.EliteRecovery : Balance.WaveRecovery));
                        wave++; StartWave();
                    }
                    else ClearStage(s);
                }
            }
        }

        private void BasicHit(Enemy target, Stats s)
        {
            lastCritical = model.Chance(s.crit);
            float damage = Mitigate(s.attack);
            if (lastCritical) damage *= Balance.CritMultiplier;
            if (target.health < target.maxHealth * Balance.ExecuteThreshold && model.Has(Effect.Execute)) damage *= Balance.ExecuteMultiplier;
            if (guardCharge > 0 && model.Has(Effect.Guard))
            { damage += guardCharge; Emit(Ability.GuardRelease, guardCharge); guardCharge = 0; guardFlash = .45f; }
            bool arc = attackNumber % 3 == 0 && model.Has(Effect.Arc);
            if (arc)
            {
                bool surge = markTime > 0;
                lastArcDamage = Mitigate(s.attack * Balance.ArcDamage * model.EffectCount(Effect.Arc)) * (surge ? Balance.MarkedArcMultiplier : 1);
                damage += lastArcDamage; arcFlash = .45f; arcHits++;
                if (surge) { markTime = 0; combos++; }
                Emit(surge ? Ability.Surge : Ability.Arc, lastArcDamage);
            }
            // 普攻小击退：把贴身的怪推开一点，免得一群怪全糊在角色身上。
            if (!target.boss) target.x += target.elite ? 8 : 16;
            Item weapon = model.Equipped(Slot.Weapon);
            string kind = weapon == null ? "" : weapon.baseId;
            if (kind == "w0")
            {
                // 霰弹：6 颗弹丸各算一次伤害。一半必中目标，其余落在目标身后 150 内的怪（没有就也打目标）。
                var near = new List<Enemy>();
                foreach (Enemy e in enemies) if (e != target && e.Alive && e.x >= target.x - 20 && e.x <= target.x + Balance.ShotgunSpread) near.Add(e);
                for (int i = 0; i < Balance.ShotgunPellets; i++)
                {
                    Enemy hitE = i % 2 == 0 || near.Count == 0 ? target : near[(i / 2) % near.Count];
                    if (!hitE.Alive) hitE = target;
                    DealDamage(hitE, damage * Balance.ShotgunPellet, s);
                }
            }
            else if (kind == "w3")
            {
                // 手炮：命中点爆炸，周围 120 内的怪吃 60%。
                DealDamage(target, damage, s);
                foreach (Enemy e in enemies.ToArray()) if (e != target && e.Alive && Math.Abs(e.x - target.x) <= Balance.CannonRadius) DealDamage(e, damage * Balance.CannonSplash, s);
            }
            else if (kind == "w4")
            {
                // 重锤：砸地横扫，目标及其身后 140 内的怪吃 70%。
                DealDamage(target, damage, s);
                foreach (Enemy e in enemies.ToArray()) if (e != target && e.Alive && e.x >= target.x - 30 && e.x <= target.x + Balance.HeavyReach) DealDamage(e, damage * Balance.HeavySplash, s);
            }
            else DealDamage(target, damage, s);
            if (arc) Area(target, target.x + 260, lastArcDamage * Balance.ArcChain, 2, s);
        }

        // 波及 target 身后（x 不超过 reach）的其他活怪，最多 limit 只，近的先挨。
        private void Area(Enemy target, float reach, float damage, int limit, Stats s)
        {
            var hit = new List<Enemy>();
            foreach (Enemy e in enemies) if (e != target && e.Alive && e.x <= reach) hit.Add(e);
            hit.Sort((a, b) => a.x.CompareTo(b.x));
            for (int i = 0; i < hit.Count && i < limit; i++) DealDamage(hit[i], damage, s);
        }

        private void EnemiesAttack(float dt, Stats s)
        {
            // 近战：最靠前的 3 只能打，其余挤在后面等位置（计时不走）；远程走到位就打。
            var melee = new List<Enemy>();
            foreach (Enemy e in enemies) if (e.Alive && !e.Ranged && Arrived(e)) melee.Add(e);
            melee.Sort((a, b) => a.x.CompareTo(b.x));
            if (melee.Count > Chapters.MeleeAttackers) melee.RemoveRange(Chapters.MeleeAttackers, melee.Count - Chapters.MeleeAttackers);
            foreach (Enemy e in enemies)
            {
                if (!e.Alive || !Arrived(e)) continue;
                if (!e.Ranged && !melee.Contains(e)) continue;
                e.timer -= dt;
                if (e.timer > 0) continue;
                e.timer += e.interval; e.strikeFlash = .25f;
                if (e.attack <= 0) continue;
                float raw = GameModel.Damage(e.attack, s.defense);
                float blocked = raw * Balance.GuardReduction * model.EffectCount(Effect.Guard);
                float damage = raw - blocked;
                if (blocked > 0)
                { guardCharge = Math.Min(s.attack, guardCharge + blocked * Balance.GuardChargeRatio); guardFlash = .35f; Emit(Ability.GuardCharge, blocked); }
                health = Math.Max(0, health - damage); lastEnemyDamage = damage; hurtFlash = .25f; lastHurtX = e.x;
                OnHit?.Invoke();
                if (health <= 0)
                {
                    recovery = 3; ResetModules(); Movement.Stop();
                    OnMessage?.Invoke(model.state.region > 0 ? "失去战斗能力，修好后撤回上一关补给；变强后自动再挑战。" : "失去战斗能力，3 秒后维修重试；无装备损失。");
                    OnCheckpoint?.Invoke();
                    return;
                }
            }
        }

        /// 对一只怪造成伤害；打死返回 true。
        private bool DealDamage(Enemy e, float damage, Stats s)
        {
            if (!e.Alive) return false;
            float actual = Math.Min(e.health, damage);
            health = Math.Min(s.health, health + actual * Balance.LeechRatio * model.EffectCount(Effect.Leech));
            e.health -= damage; e.hitFlash = .3f;
            lastPlayerDamage = damage; hitFlash = .3f; lastHitX = e.x;
            OnDamage?.Invoke(damage, lastCritical, e.x);
            if (e.health > 0) return false;
            e.health = 0; e.deadAt = elapsed; lastKillX = e.x; packShifts++;
            Reward(e);
            return true;
        }

        private void ClearStage(Stats s)
        {
            StageCleared = true; pendingStrike = false;
            int region = model.state.region;
            if (Boss)
            {
                health = Math.Min(s.health, health + s.health * Balance.BossRecovery);
                OnMessage?.Invoke("第一章首领回收完成。");
            }
            int next = Math.Min(Catalog.LastRegion, region + 1);
            if (next > model.state.unlockedRegion)
            { model.state.unlockedRegion = next; OnMessage?.Invoke("开放 " + Catalog.StageCode(next) + " · " + Catalog.RegionNames[next]); }
            OnCheckpoint?.Invoke();
        }

        private void Reward(Enemy e)
        {
            model.state.kills++; model.state.scrap += e.boss ? 8 : e.elite ? 3 : 1;
            if (e.boss) { model.state.cores++; model.state.bosses++; }
            int before = model.state.collection.Count;
            Item item = null;
            if (e.boss) item = model.Drop(true);
            else if (e.elite) item = model.Drop(false, true, true);
            else if (rng.NextDouble() < Chapters.Roles[(int)e.role].dropChance) item = model.Drop(false, true);
            if (item != null) OnDrop?.Invoke(item, model.state.collection.Count > before);
        }
    }
}
