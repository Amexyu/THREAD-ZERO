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
        public const float EntryTime = 0f;   // v0.16 快速转场：不再从检修口爬出来，直接接着走
        public float EntryLeft { get; private set; }
        private float droneTimer = 1, markTimer = .7f, dashTimer = 2;
        private string loadoutSignature;
        private bool pendingStrike, testDummy;
        private float strikeDelay, spawnTimer, waveDelay, summonTimer, elapsed;
        private int firstBossClear;
        // ---- 锁链模组 ----
        public const int ChainPullEvery = 3; public const float ChainPull = 170;
        private int chainHits;
        public Action<Enemy> OnChainPull;
        public bool IsChain { get { Item w = model.Equipped(Slot.Weapon); return w != null && w.baseId == "w7"; } }
        public const float EnemyAccel = 900;   // 和角色 EncounterMovement 的加速度一致   // 本关首领是第几章的首通（ClearStage 里提示一次）
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
        /// 第一次击败本章首领：装上本章的义体（机械飞升 +1）。
        public Action<ImplantDef> OnImplant;
        private void GrantImplant()
        {
            if (model.InEcho) return;
            int ch = model.Chapter;
            if (model.state.region != Chapters.BossRegion(ch) || ch > Catalog.Implants.Length) return;
            ImplantDef d = Catalog.Implants[ch - 1];   // 第一章绞盘机械臂，第二章调度义眼
            if (model.state.implants.Contains(d.id)) return;
            model.state.implants.Add(d.id); OnImplant?.Invoke(d);
        }

        // 活着的怪 + 刚倒下的残骸（deadAt >= 0）。queue：本波还没出场的怪。
        public readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<Enemy> queue = new List<Enemy>();

        public StageDef Stage
        {
            get
            {
                if (!model.InEcho) return Chapters.Stage(model.state.region);
                int key = model.state.echoFloor * 2 + (model.Anomaly("swarm") ? 1 : 0);
                if (key != echoKey) { echoKey = key; echoStage = Chapters.EchoStage(model.state.echoFloor, model.Anomaly("swarm")); }
                return echoStage;
            }
        }
        private StageDef echoStage; private int echoKey = -1;
        // 敌人强度：主线按关卡；回声层以 1-7 为起点按层增长（再叠协议异常）。
        private float EnemyHealth(int w) { return model.InEcho ? Chapters.Health(Chapters.EchoAnchor, w) * Chapters.EchoScale(model.state.echoFloor, false) * (model.Anomaly("armor") ? 1.5f : 1) : Chapters.Health(model.state.region, w); }
        private float EnemyAttack(int w) { return model.InEcho ? Chapters.Attack(Chapters.EchoAnchor, w) * Chapters.EchoScale(model.state.echoFloor, true) : Chapters.Attack(model.state.region, w); }
        /// 当前关卡用哪一章的怪（回声层按场景轮换）。
        private int Skin { get { return Chapters.SceneChapter(Stage.backdrop) - 1; } }
        private float EnemyDefense() { return model.InEcho ? Chapters.Defense(Chapters.EchoAnchor) + model.state.echoFloor * .5f : Chapters.Defense(model.state.region); }
        public Action<int, bool> OnFloorCleared;   // 回声层过层：层数、是否刷新最高层
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
            if (e.boss) return (model.InEcho ? "回声 · " : "") + Catalog.BossName(e.skin + 1);
            if (e.move == EnemyMove.Crusher) return e.skin == 1 ? "精英 · 起重机甲" : "精英 · 拆解者";
            if (e.move == EnemyMove.Beacon) return e.skin == 1 ? "精英 · 运输母机" : "精英 · 信号塔";
            string[] names = e.skin == 1 ? new[] { "货运无人机", "叉臂装卸机", "轨道炮车" } : new[] { "失控巡逻机", "装甲运输机", "信号干扰机" };
            return names[(int)e.role];
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
            enemies.Clear(); queue.Clear(); shots.Clear();
            Movement.ResetAtCamera(Movement.CameraX);
            StartWave();
            EntryLeft = EntryTime; spawnTimer = .6f;   // 0.6 秒后第一波开始涌入
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
        { Enemy t = Target; return GameModel.Damage(damage, EnemyDefense() + (t != null && t.boss ? 6 : 0)); }

        // ---------- 波次与出怪 ----------
        private Enemy Make(EnemyRole role, bool elite)
        {
            if (!elite && model.Anomaly("elite") && rng.NextDouble() < .12) elite = true;   // 协议异常 · 精英化
            RoleStats r = Chapters.Roles[(int)role];
            int region = model.state.region;
            var e = new Enemy
            {
                id = ++nextId, role = role, elite = elite,
                health = EnemyHealth(wave) * r.health * (elite ? Chapters.EliteHealth : 1),
                attack = EnemyAttack(wave) * r.attack * (elite ? Chapters.EliteAttack : 1),
                interval = r.interval, speed = r.speed * (elite ? .85f : 1),
                stop = r.stopMin + (float)rng.NextDouble() * (r.stopMax - r.stopMin),
                canvas = r.canvas * (elite ? Chapters.EliteCanvas : 1),
                depth = (float)rng.NextDouble(),
            };
            e.maxHealth = e.health; e.timer = e.interval * (.4f + .6f * (float)rng.NextDouble());
            if (model.Anomaly("overload")) e.interval *= .8f;   // 协议异常 · 过载
            e.move = role == EnemyRole.Swarmer ? EnemyMove.Lunge : role == EnemyRole.Brute ? (elite ? EnemyMove.Crusher : EnemyMove.Slam) : (elite ? EnemyMove.Beacon : EnemyMove.Bolt);
            e.aux = 1.5f; e.skin = Skin;
            if (testDummy) { e.health = e.maxHealth = 100000; e.attack = 0; }
            return e;
        }
        private Enemy MakeBoss()
        {
            int region = model.state.region;
            var e = new Enemy
            {
                id = ++nextId, role = EnemyRole.Brute, boss = true,
                health = EnemyHealth(5) * Chapters.BossHealth, attack = EnemyAttack(5) * Chapters.BossAttack,
                interval = Chapters.BossInterval, speed = Chapters.BossSpeed, stop = Chapters.BossStop, canvas = Chapters.BossCanvas, depth = .5f,
            };
            e.maxHealth = e.health; e.timer = 1; e.move = EnemyMove.Gate; e.skin = Skin;
            if (model.Anomaly("overload")) e.interval *= .8f;
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
            foreach (Enemy e in enemies) { e.hitFlash = Math.Max(0, e.hitFlash - dt); e.sinceStrike += dt; e.hasteLeft = Math.Max(0, e.hasteLeft - dt); }
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
                    for (int i = 0; i < n; i++) { Enemy m = Make(EnemyRole.Swarmer, false); m.summoned = true; queue.Add(m); }
                }
            }

            // 移动：角色在攻击对象进入射程前一直往前走；怪各自走到自己的停步距离。
            Enemy target = Target;
            bool busy = pendingStrike || DaggerActive;
            bool walk = !busy && (target == null || target.x - Movement.HeroX > Movement.HeroRange);
            Movement.RangeOverride = IsChain ? EncounterMovement.ChainRange : 0;
            Movement.Tick(dt, model.IsMelee, walk);
            foreach (Enemy e in enemies)
            {
                if (!e.Alive) { e.moving = false; continue; }
                if (e.pull > 0)
                {   // 被吊钩拽：900 像素/秒往角色身前拉，最近拉到身前 140
                    float step = Math.Min(e.pull, 900 * dt); e.pull -= step;
                    e.x = Math.Max(Movement.HeroX + 140, e.x - step); if (e.x <= Movement.HeroX + 140) e.pull = 0;
                    e.moving = false; continue;
                }
                // v0.16 和角色同一套移动：900/s² 加速到自己的速度，快到停步点时按同样的减速度刹住（不再匀速滑行、瞬停）
                float want = Movement.HeroX + e.stop, before = e.x, dist = Math.Max(0, e.x - want);
                float top = Math.Min(e.speed, (float)Math.Sqrt(2 * EnemyAccel * dist));
                e.vel = e.vel < top ? Math.Min(top, e.vel + EnemyAccel * dt) : Math.Max(top, e.vel - EnemyAccel * dt);
                // 只往角色方向走；角色走近了（e.x 已经在停步点以内）就原地不动，绝不把怪往后推
                if (dist > 0) e.x = Math.Max(want, e.x - e.vel * dt); else e.vel = 0;
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
            else if (kind == "w5")
            {
                // 撞杆矛：长柄穿透，目标身后 160 内的怪排成一线都挨 70%。
                DealDamage(target, damage, s);
                foreach (Enemy e in enemies.ToArray()) if (e != target && e.Alive && e.x >= target.x && e.x <= target.x + 160) DealDamage(e, damage * .7f, s);
            }
            else if (kind == "w7")
            {
                // 吊臂钩索（锁链模组）：甩钩打单体，射程 300；每 3 钩把目标拽到身前（首领拽不动，精英拽一半）。
                DealDamage(target, damage, s);
                chainHits++;
                if (chainHits % ChainPullEvery == 0 && target.Alive && !target.boss)
                {
                    float pull = target.elite ? ChainPull * .5f : ChainPull;
                    target.pull = pull; target.vel = 0;   // 0.2 秒内被链子拽过来（下面移动里结算）
                    OnChainPull?.Invoke(target);
                }
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

        // 怪的攻击模组见 CombatEnemyAttacks.cs

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
            if (model.InEcho)
            {   // 回声层：记最高层；每 25 层里程碑给核心。不走主线的解锁。
                int floor = model.state.echoFloor; bool best = floor > model.state.echoBest;
                if (best)
                {
                    model.state.echoBest = floor;
                    if (floor % Chapters.MilestoneEvery == 0)
                    { model.state.cores += Chapters.MilestoneCores; OnMessage?.Invoke("回声层第 " + floor + " 层里程碑：核心 +" + Chapters.MilestoneCores); }
                }
                if (Boss) health = Math.Min(s.health, health + s.health * Balance.BossRecovery);
                OnFloorCleared?.Invoke(floor, best);
                OnCheckpoint?.Invoke();
                return;
            }
            if (Boss)
            {
                health = Math.Min(s.health, health + s.health * Balance.BossRecovery);
                if (firstBossClear == 1) OnMessage?.Invoke("第一章首领回收完成 · 回声层与第二章已开放");   // 只在第一次通关时提示
                else if (firstBossClear == 2) OnMessage?.Invoke("第二章首领回收完成 · 调度义眼已装上");
                firstBossClear = 0;
            }
            int next = Math.Min(Chapters.ChapterCount * Chapters.StagesPerChapter - 1, region + 1);
            if (next > model.state.unlockedRegion)
            { model.state.unlockedRegion = next; OnMessage?.Invoke("开放 " + Catalog.StageCode(next) + " · " + Catalog.RegionNames[next]); }
            OnCheckpoint?.Invoke();
        }

        private void Reward(Enemy e)
        {
            model.state.kills++; model.state.scrap += (int)Math.Ceiling((e.boss ? 8 : e.elite ? 3 : 1) * (model.Anomaly("swarm") ? 1.5f : 1));
            if (e.boss)
            {   // 首通必掉核心，之后重刷 25%（回声层每 10 层的首领：刷新最高层时算首通）
                int ch = model.Chapter;
                bool first = model.InEcho ? model.state.echoFloor > model.state.echoBest : !model.BossCleared(ch);
                if (first || model.Chance(Balance.RepeatCoreChance)) { model.state.cores++; OnMessage?.Invoke("回收首领核心 +1（持有 " + model.state.cores + "）"); }
                if (!model.InEcho && first) firstBossClear = ch;
                model.state.bosses++; if (!model.InEcho) model.state.bossMask |= 1 << (ch - 1);
                GrantImplant();
            }
            int before = model.state.collection.Count;
            // v0.16 掉落：普通怪按分工的概率掉 1 件；精英掉 2 件（至少 1 件特装）；首领掉 3 件（专属件 + 至少 1 件特装）。
            var drops = new List<Item>();
            if (e.boss)
            {
                drops.Add(model.Drop(true, true, false, Quality.Rare, Catalog.BossExclusiveOf(e.skin + 1)));
                for (int i = 1; i < Chapters.BossDrops; i++) drops.Add(model.Drop(true, true));
            }
            else if (e.elite)
            {
                drops.Add(model.Drop(false, true, true, Quality.Rare));
                for (int i = 1; i < Chapters.EliteDrops; i++) drops.Add(model.Drop(false, true, true));
            }
            else if (!e.summoned && rng.NextDouble() < Chapters.Roles[(int)e.role].dropChance * (model.Anomaly("armor") ? 1.4f : 1)) drops.Add(model.Drop(false, true));
            foreach (Item item in drops)
            {
                if (item == null) continue;
                bool fresh = model.state.collection.Count > before; before = model.state.collection.Count;
                OnDrop?.Invoke(item, fresh);
            }
        }
    }
}
