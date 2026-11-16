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

    public sealed partial class CombatSession
    {
        public readonly GameModel model;
        public float health, enemyHealth, enemyMaxHealth;
        public float attackTimer, enemyTimer, recovery;
        public int wave = 1, attackNumber;
        public float lastPlayerDamage, lastEnemyDamage, hitFlash, hurtFlash;
        public float droneFlash, arcFlash, dashFlash, guardFlash, markTime, guardCharge;
        public float lastDroneDamage, lastArcDamage, lastDashDamage;
        public int droneHits, arcHits, dashHits, combos;
        public bool lastCritical;
        private float droneTimer = 1, markTimer = .7f, dashTimer = 2;
        private string loadoutSignature;
        private bool pendingStrike;
        private float strikeDelay;
        public Action<float> OnAdvance;
        public Action<string> OnMessage;
        public Action<Item, bool> OnDrop;
        public Action OnCheckpoint;
        public Action<AbilityEvent> OnAbility;
        public Action OnBasicAttack, OnHit;
        // v0.9 怪群：enemyHealth / enemyMaxHealth 始终是最前面那只；reserve 是它身后还活着的同伴的生命。
        public readonly List<float> reserve = new List<float>();
        public float reserveMax;
        public int packSize = 1, packShifts;
        public bool Boss { get { return wave == Balance.BossWave; } }
        public bool Elite { get { return wave == Balance.EliteWave; } }
        public int Alive { get { return 1 + reserve.Count; } }
        public void ClearPack() { reserve.Clear(); }
        public string EnemyName { get { return (Elite ? "精英·" : "") + BaseEnemyName; } }
        private string BaseEnemyName { get { return Boss ? Catalog.BossNames[model.state.region] : new[] { "失控巡逻机", "装甲运输机", "信号干扰机" }[Catalog.District(model.state.region)] + (model.state.region >= Catalog.Districts ? "·改" : ""); } }

        public CombatSession(GameModel m)
        { model = m; health = m.Total().health; loadoutSignature = Signature(); Spawn(); }
        private string Signature() { return string.Join("/", model.state.equipped); }
        private void Spawn()
        {
            int r = model.state.region;
            packSize = Balance.PackSize(r, wave);
            enemyMaxHealth = Balance.EnemyHealth(r, wave) * (packSize > 1 ? Balance.PackHealthShare : Elite ? Balance.EliteHealth : 1);
            reserve.Clear(); reserveMax = enemyMaxHealth;
            for (int i = 1; i < packSize; i++) reserve.Add(enemyMaxHealth);
            enemyHealth = enemyMaxHealth; attackTimer = .3f; enemyTimer = 1.25f; markTime = 0; pendingStrike=false;
            if(MovementEnabled)Movement.BeginEncounter();
        }
        private void ResetModules()
        {
            droneTimer = Balance.DroneInterval; markTimer = .7f; dashTimer = Balance.ThrusterInterval;
            markTime = guardCharge = droneFlash = arcFlash = dashFlash = guardFlash = 0;
            // Loadout swaps cannot bank a near-ready proc or carry an old weapon's third-hit counter.
            attackNumber = 0;
            pendingStrike=false;
            ResetDagger();
        }
        public void SyncLoadout()
        {
            string current = Signature();
            if (current == loadoutSignature) return;
            loadoutSignature = current; ResetModules(); ClampHealth();
        }
        public void ResetRegion()
        { wave = 1; recovery = 0; health = model.Total().health; ResetModules(); Spawn(); }
        public void ClampHealth() { health = Math.Min(health, model.Total().health); }
        public void Tick(float dt)
        {
            for (float remaining = Math.Min(Math.Max(dt, 0), 2); remaining > .00001f;)
            { float step = Math.Min(.05f, remaining); Step(step); remaining -= step; }
        }
        private void Emit(Ability ability, float damage = 0) { OnAbility?.Invoke(new AbilityEvent(ability, damage)); }
        private float Mitigate(float damage)
        { return GameModel.Damage(damage, Balance.EnemyDefense(model.state.region, Boss)); }

        private void Step(float dt)
        {
            OnAdvance?.Invoke(dt);
            hitFlash = Math.Max(0, hitFlash - dt); hurtFlash = Math.Max(0, hurtFlash - dt);
            droneFlash = Math.Max(0, droneFlash - dt); arcFlash = Math.Max(0, arcFlash - dt);
            dashFlash = Math.Max(0, dashFlash - dt); guardFlash = Math.Max(0, guardFlash - dt);
            SyncLoadout();
            if (recovery > 0)
            {
                if(MovementEnabled)Movement.Stop();
                recovery -= dt;
                if (recovery <= 0) { ResetRegion(); OnMessage?.Invoke("维修完成，装备与收藏保留，设备重新部署。"); }
                return;
            }
            Stats s = model.Total(); health = Math.Min(health, s.health);
            if(MovementEnabled)Movement.Tick(dt,model.IsMelee,Boss,Elite,pendingStrike || DaggerActive);
            bool engaged=HeroInRange || EnemyInRange;
            markTime = Math.Max(0, markTime - dt);
            if (engaged && model.Has(Effect.Mark))
            {
                markTimer -= dt;
                if (markTimer <= 0) { markTime = Balance.MarkDuration; markTimer += Balance.MarkInterval; Emit(Ability.Mark); }
            }
            attackTimer -= dt; enemyTimer -= dt;
            if(!HeroInRange){attackTimer=Math.Max(0,attackTimer);pendingStrike=false;}
            if(!EnemyInRange)enemyTimer=Math.Max(0,enemyTimer);
            if (AdvanceDagger(dt,s)) return;
            if(pendingStrike)strikeDelay-=dt;
            if (pendingStrike && strikeDelay <= .00001f)
            {
                pendingStrike=false;attackNumber++;
                lastCritical = model.Chance(s.crit);
                float damage = Mitigate(s.attack);
                if (lastCritical) damage *= Balance.CritMultiplier;
                if (enemyHealth < enemyMaxHealth * Balance.ExecuteThreshold && model.Has(Effect.Execute)) damage *= Balance.ExecuteMultiplier;
                if (guardCharge > 0 && model.Has(Effect.Guard))
                { damage += guardCharge; Emit(Ability.GuardRelease, guardCharge); guardCharge = 0; guardFlash = .45f; }
                if (attackNumber % 3 == 0 && model.Has(Effect.Arc))
                {
                    bool surge = markTime > 0;
                    lastArcDamage = Mitigate(s.attack * Balance.ArcDamage * model.EffectCount(Effect.Arc)) * (surge ? Balance.MarkedArcMultiplier : 1);
                    damage += lastArcDamage; arcFlash = .45f; arcHits++;
                    Splash(lastArcDamage * Balance.ArcChain, 2);
                    if (surge) { markTime = 0; combos++; }
                    Emit(surge ? Ability.Surge : Ability.Arc, lastArcDamage);
                }
                if (DealDamage(damage, s)) return;
            }
            if(attackTimer<=0 && !pendingStrike && !DaggerActive && HeroInRange)
            {
                if (!BeginDagger(s)) {
                attackTimer+=1/(1+s.haste);pendingStrike=true;
                strikeDelay=DollAnimation.Windup(model.IsMelee)+(model.IsMelee?.06f:.12f);
                OnBasicAttack?.Invoke();
                }
            }
            if (engaged && model.Has(Effect.Drone))
            {
                droneTimer -= dt;
                if (droneTimer <= 0)
                {
                    droneTimer += Balance.DroneInterval; droneFlash = .35f; droneHits++;
                    lastDroneDamage = Mitigate(s.attack * Balance.DroneDamage) * (markTime > 0 ? Balance.MarkedDroneMultiplier : 1);
                    Emit(Ability.Drone, lastDroneDamage); lastCritical = false;
                    if (DealDamage(lastDroneDamage, s)) return;
                }
            }
            if (engaged && model.Has(Effect.Thruster))
            {
                dashTimer -= dt;
                if (dashTimer <= 0)
                {
                    dashTimer += Balance.ThrusterInterval; dashFlash = .4f; dashHits++;
                    lastDashDamage = Mitigate(s.attack * Balance.ThrusterDamage) * (model.IsMelee ? Balance.AssaultMultiplier : 1);
                    if (model.IsMelee) combos++;
                    Emit(model.IsMelee ? Ability.Assault : Ability.Dash, lastDashDamage); lastCritical = false;
                    Splash(lastDashDamage * Balance.ThrusterPierce, 99);
                    if (DealDamage(lastDashDamage, s)) return;
                }
            }
            if (enemyTimer <= 0 && EnemyInRange)
            {
                // 怪群里每多一只活着的同伴，攻击就更密一些；单只伤害不变。
                enemyTimer += (Boss ? 1.1f : 1.65f) / (1 + Balance.PackPressure * reserve.Count);
                float raw = GameModel.Damage(Balance.EnemyAttack(model.state.region, wave) * (Elite ? Balance.EliteAttack : 1), s.defense);
                float blocked = raw * Balance.GuardReduction * model.EffectCount(Effect.Guard);
                float damage = raw - blocked;
                if (blocked > 0)
                { guardCharge = Math.Min(s.attack, guardCharge + blocked * Balance.GuardChargeRatio); guardFlash = .35f; Emit(Ability.GuardCharge, blocked); }
                health = Math.Max(0, health - damage); lastEnemyDamage = damage; hurtFlash = .25f;
                OnHit?.Invoke();
                if (health <= 0)
                {
                    recovery = 3; ResetModules();
                    OnMessage?.Invoke("失去战斗能力，3 秒后维修重试；无装备损失。"); OnCheckpoint?.Invoke();
                }
            }
        }
        private bool DealDamage(float damage, Stats s)
        {
            float actual = Math.Min(enemyHealth, damage);
            health = Math.Min(s.health, health + actual * Balance.LeechRatio * model.EffectCount(Effect.Leech));
            enemyHealth -= damage; lastPlayerDamage = damage; hitFlash = .3f;
            if (enemyHealth > 0) return false;
            CancelDagger();
            pendingStrike=false;
            bool wasBoss = Boss, wasElite = Elite; Reward(wasBoss, wasElite);
            if (reserve.Count > 0 && !wasBoss)
            {
                // 怪群还没清完：下一只顶到最前面，路段不推进。
                enemyMaxHealth = reserveMax; enemyHealth = reserve[0]; reserve.RemoveAt(0);
                if(MovementEnabled)Movement.AdvanceTarget();
                markTime = 0; packShifts++; return true;
            }
            if (wasBoss)
            {
                model.state.bosses++;
                int unlocked = Math.Min(Catalog.LastRegion, model.state.region + 1);
                if (unlocked > model.state.unlockedRegion)
                { model.state.unlockedRegion = unlocked; OnMessage?.Invoke("新街区解锁：" + Catalog.RegionNames[unlocked]); }
                OnMessage?.Invoke("Boss 回收完成，可继续本区或前往新街区。");
                health = Math.Min(s.health, health + s.health * Balance.BossRecovery);
                wave = 1; OnCheckpoint?.Invoke();
            }
            else { health = Math.Min(s.health, health + s.health * (wasElite ? Balance.EliteRecovery : Balance.WaveRecovery)); wave++; }
            Spawn(); return true;
        }
        private void Reward(bool boss, bool certain)
        {
            model.state.kills++; model.state.scrap += boss ? 8 : 1;
            if (boss) model.state.cores++;
            int before = model.state.collection.Count;
            Item item = model.Drop(boss, certain);
            if (item != null) OnDrop?.Invoke(item, model.state.collection.Count > before);
        }
        // 溅射到身后的同伴：电弧连到最多两只，推进贯穿整队。被打死的同伴照常结算掉落。
        private void Splash(float damage, int limit)
        {
            int index = 0;
            for (int hit = 0; hit < limit && index < reserve.Count; hit++)
            {
                reserve[index] -= damage;
                if (reserve[index] <= 0) { reserve.RemoveAt(index); packShifts++; Reward(false, false); }
                else index++;
            }
        }
    }
}
