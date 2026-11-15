using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreadZero
{
    public enum Slot { Weapon, Head, Coat, Back }
    public enum Quality { Common, Uncommon, Rare, Epic, Legendary, Prototype }
    // Append values to preserve v1 serialized enum numbers.
    public enum Effect { None, Arc, Leech, Guard, Execute, Drone, Mark, Thruster }
    public enum AttackStyle { Ranged, Melee }

    [Serializable]
    public sealed class Stats
    {
        public float health, attack, defense, haste, crit, luck;
        public Stats() { }
        public Stats(float hp, float atk, float def, float speed = 0, float critical = 0, float fortune = 0)
        { health = hp; attack = atk; defense = def; haste = speed; crit = critical; luck = fortune; }
        public void Add(Stats s)
        { health += s.health; attack += s.attack; defense += s.defense; haste += s.haste; crit += s.crit; luck += s.luck; }
        public Stats Copy() { return new Stats(health, attack, defense, haste, crit, luck); }
    }

    public sealed class EquipmentBase
    {
        public readonly string id, name;
        public readonly Slot slot;
        public readonly int style;
        public readonly Stats stats;
        public readonly Effect effect;
        public readonly AttackStyle attackStyle;
        public EquipmentBase(string key, string title, Slot part, int look, Stats value, Effect proc = Effect.None, AttackStyle attack = AttackStyle.Ranged)
        { id = key; name = title; slot = part; style = look; stats = value; effect = proc; attackStyle = attack; }
    }

    [Serializable]
    public sealed class Affix { public int stat; public float value; }

    [Serializable]
    public sealed class Item
    {
        public string uid, baseId;
        public Quality quality;
        public int level;
        public bool locked;
        public int rerolls;
        public List<Affix> affixes = new List<Affix>();
    }

    [Serializable]
    public sealed class GameState
    {
        public int version = 1;
        public List<Item> items = new List<Item>();
        public List<string> collection = new List<string>();
        public string[] equipped = new string[4];
        public string[] appearance = new string[4];
        public int region, unlockedRegion, scrap, training, kills, bosses;
        public int sinceRare, sinceEpic, sinceLegendary;
        public string recoveryTarget;
        public int targetBossMisses;
        public int glowMode = 2;
        // 自动分解档位：0 关闭，1 仅普通，2 精良及以下。旧存档没有这个字段时为 0。
        public int autoSalvage;
        // v0.10：schema 低于 2 的存档在读取时把装备等级换算到 30 级制。
        // parts 按部位分四种（枪机件、传感件、装甲片、动力件），来自分解；scrap 仍是击败敌人得到的通用零件。
        public int schema;
        public int[] parts = new int[4];
        public int cores;
    }

    public static class Catalog
    {
        public static readonly string[] SlotNames = { "武器", "头部", "外套", "背部" };
        public static readonly string[] PartNames = { "枪机件", "传感件", "装甲片", "动力件" };
        public static readonly string[] QualityNames = Balance.Qualities.Select(q=>q.name).ToArray();
        // v0.8：六个关卡 = 三个街区 × 两层。后三关是同一街区的深层，敌人更强，掉落等级更高。
        public const int Districts = 3;
        public static readonly string[] RegionNames = { "雨巷边界", "物流高架", "信号禁区", "雨巷深层", "高架深层", "禁区核心" };
        public static int LastRegion { get { return RegionNames.Length - 1; } }
        public static int District(int region) { return ((region % Districts) + Districts) % Districts; }
        public static readonly string[] BossNames = { "巡检核心", "运输壁垒", "断讯监视者", "巡检核心·改", "运输壁垒·改", "断讯监视者·改" };
        public static readonly string[] StatNames = { "生命", "攻击", "防御", "攻速", "暴击", "幸运" };
        public static readonly float[] Multipliers = Balance.Qualities.Select(q=>q.multiplier).ToArray();
        public static readonly EquipmentBase[] All = {
            new EquipmentBase("w0", "勤务短铳", Slot.Weapon, 0, new Stats(0, 7, 0)),
            new EquipmentBase("w1", "霓虹切刃", Slot.Weapon, 1, new Stats(0, 6, 0, .08f), Effect.Execute, AttackStyle.Melee),
            new EquipmentBase("w2", "线圈发射器", Slot.Weapon, 2, new Stats(0, 5, 0), Effect.Arc),
            new EquipmentBase("w3", "逆流手炮", Slot.Weapon, 3, new Stats(6, 6, 0), Effect.Leech),
            new EquipmentBase("w4", "重型破拆器", Slot.Weapon, 4, new Stats(0, 10, 0, -.05f), Effect.None, AttackStyle.Melee),
            new EquipmentBase("h0", "回收员兜帽", Slot.Head, 0, new Stats(10, 0, 2)),
            new EquipmentBase("h1", "夜行面罩", Slot.Head, 1, new Stats(8, 0, 1, 0, .025f)),
            new EquipmentBase("h2", "信号护目镜", Slot.Head, 2, new Stats(6, 1, 1, 0, 0, 2), Effect.Mark),
            new EquipmentBase("h3", "封闭式头盔", Slot.Head, 3, new Stats(18, 0, 4)),
            new EquipmentBase("h4", "故障猫耳终端", Slot.Head, 4, new Stats(8, 1, 0, .04f)),
            new EquipmentBase("c0", "防雨勤务夹克", Slot.Coat, 0, new Stats(24, 0, 3)),
            new EquipmentBase("c1", "反光机能风衣", Slot.Coat, 1, new Stats(18, 0, 2, .04f)),
            new EquipmentBase("c2", "轻型装甲外套", Slot.Coat, 2, new Stats(30, 0, 5), Effect.Guard),
            new EquipmentBase("c3", "逆流束带上衣", Slot.Coat, 3, new Stats(18, 2, 2), Effect.Leech),
            new EquipmentBase("c4", "白噪研究长衣", Slot.Coat, 4, new Stats(20, 0, 2, 0, 0, 3)),
            new EquipmentBase("b0", "折叠回收背包", Slot.Back, 0, new Stats(12, 0, 1, 0, 0, 1), Effect.Drone),
            new EquipmentBase("b1", "侦察天线模块", Slot.Back, 1, new Stats(8, 1, 0, .03f, 0, 2), Effect.Mark),
            new EquipmentBase("b2", "蓄电线圈模块", Slot.Back, 2, new Stats(8, 1, 0), Effect.Arc),
            new EquipmentBase("b3", "折叠防护翼", Slot.Back, 3, new Stats(20, 0, 4), Effect.Guard),
            new EquipmentBase("b4", "轻量推进背架", Slot.Back, 4, new Stats(8, 0, 0, .08f), Effect.Thruster)
        };
        public static EquipmentBase Find(string id) { return All.FirstOrDefault(x => x.id == id); }
        public static int SourceRegion(string id)
        {
            switch (id)
            {
                case "w2": case "h2": case "h4": case "c4": case "b1": case "b2": return 2;
                case "w3": case "w4": case "h3": case "c2": case "c3": case "b3": case "b4": return 1;
                default: return 0;
            }
        }
        public static string EffectName(Effect e)
        {
            switch(e) { case Effect.Arc: return "电弧：每三次攻击追加伤害";
                case Effect.Leech: return "汲取：造成伤害恢复生命";
                case Effect.Guard: return "护盾：减伤蓄能，强化下一次攻击";
                case Effect.Execute: return "处决：敌人低血时提高伤害";
                case Effect.Drone: return "无人机：每 2.4 秒自动射击，标记增伤";
                case Effect.Mark: return "标记：定时锁定目标，联动无人机与电弧";
                case Effect.Thruster: return "推进：每 4 秒突袭，近战武器强化冲击";
                default: return "无特殊效果"; }
        }
    }

    public sealed class GameModel
    {
        public readonly GameState state;
        private readonly Random rng;
        public GameModel(GameState data = null, int? seed = null)
        {
            state = data ?? new GameState();
            rng = seed.HasValue ? new Random(seed.Value) : new Random();
            Validate();
            if (state.items.Count == 0)
            {
                foreach (string id in new[] { "w0", "h0", "c0", "b0" })
                {
                    Item item = Create(id, Quality.Common, 1);
                    state.items.Add(item);
                    Unlock(id);
                    // Keep the starter hood in the inventory. An empty head slot shows the
                    // masked-hair default; equipping h0 visibly adds the hood.
                    if (id != "h0") state.equipped[(int)Catalog.Find(id).slot] = item.uid;
                }
            }
        }
        private void Validate()
        {
            if (state.version != 1) throw new InvalidOperationException("不支持此版本存档：" + state.version);
            if (state.items == null || state.collection == null || state.equipped == null || state.appearance == null
                || state.equipped.Length != 4 || state.appearance.Length != 4) throw new InvalidOperationException("存档结构不完整");
            var ids = new HashSet<string>();
            foreach (Item item in state.items)
            {
                if (item == null || Catalog.Find(item.baseId) == null || string.IsNullOrEmpty(item.uid) || !ids.Add(item.uid)
                    || item.level < 1 || item.level > 100 || (int)item.quality < 0 || (int)item.quality > 5 || item.affixes == null)
                    throw new InvalidOperationException("装备数据无效");
                var affixIds = new HashSet<int>();
                foreach (Affix a in item.affixes)
                    if (a == null || a.stat < 0 || a.stat > 5 || float.IsNaN(a.value) || float.IsInfinity(a.value)
                        || a.value < 0 || !affixIds.Add(a.stat)) throw new InvalidOperationException("装备词条无效");
            }
            for (int s = 0; s < 4; s++)
            {
                Item item = state.items.FirstOrDefault(x => x.uid == state.equipped[s]);
                if (item == null || (int)Catalog.Find(item.baseId).slot != s) state.equipped[s] = null;
                EquipmentBase look = Catalog.Find(state.appearance[s]);
                if (look == null || (int)look.slot != s || !state.collection.Contains(look.id)) state.appearance[s] = null;
            }
            state.region = Math.Max(0, Math.Min(Catalog.LastRegion, state.region));
            state.unlockedRegion = Math.Max(state.region, Math.Max(0, Math.Min(Catalog.LastRegion, state.unlockedRegion)));
            state.scrap = Math.Max(0, state.scrap); state.training = Math.Max(0, Math.Min(20, state.training));
            state.sinceRare = Math.Max(0, Math.Min(12, state.sinceRare));
            state.sinceEpic = Math.Max(0, Math.Min(45, state.sinceEpic));
            state.sinceLegendary = Math.Max(0, Math.Min(120, state.sinceLegendary));
            if (Catalog.Find(state.recoveryTarget) == null) { state.recoveryTarget = null; state.targetBossMisses = 0; }
            state.targetBossMisses = Math.Max(0, Math.Min(7, state.targetBossMisses));
            state.glowMode = Math.Max(0, Math.Min(2, state.glowMode));
            state.autoSalvage = Math.Max(0, Math.Min(2, state.autoSalvage));
            if (state.parts == null || state.parts.Length != 4) state.parts = new int[4];
            for (int i = 0; i < 4; i++) state.parts[i] = Math.Max(0, state.parts[i]);
            state.cores = Math.Max(0, state.cores);
            if (state.schema < 2)
            {
                // 旧存档的装备等级是 1–6（关卡序号），换算到新等级段的中间值。
                foreach (Item item in state.items) item.level = Math.Min(Balance.MaxLicense, (item.level - 1) * Balance.LicenseStep + 3);
                state.schema = 2;
            }
            state.collection = state.collection.Where(id => Catalog.Find(id) != null).Distinct().ToList();
            foreach (Item item in state.items) Unlock(item.baseId);
        }
        public Item Equipped(Slot slot) { return state.items.FirstOrDefault(x => x.uid == state.equipped[(int)slot]); }
        public EquipmentBase Appearance(Slot slot)
        {
            EquipmentBase look = Catalog.Find(state.appearance[(int)slot]);
            Item item = Equipped(slot);
            return look ?? (item == null ? Catalog.All.First(x => x.slot == slot) : Catalog.Find(item.baseId));
        }
        public bool Unlock(string id)
        {
            if (state.collection.Contains(id)) return false;
            state.collection.Add(id); return true;
        }
        public Item Create(string id, Quality quality, int level)
        {
            if (Catalog.Find(id) == null) throw new ArgumentException("未知装备模板");
            QualityTier tier = Balance.Tier(quality);
            Item item = new Item { uid = Guid.NewGuid().ToString("N"), baseId = id, quality = quality, level = Math.Max(1, Math.Min(100, level)) };
            List<int> pool = new List<int> { 0, 1, 2, 3, 4, 5 };
            for (int i = 0; i < tier.affixCount; i++)
            {
                int p = rng.Next(pool.Count), stat = pool[p]; pool.RemoveAt(p);
                float value = Balance.AffixValue(stat, quality, item.level, (float)rng.NextDouble());
                item.affixes.Add(new Affix { stat = stat, value = value });
            }
            return item;
        }
        public static Stats ItemStats(Item item)
        {
            Stats s = Catalog.Find(item.baseId).stats.Copy();
            float factor = Balance.Tier(item.quality).multiplier * Balance.LevelFactor(item.level);
            s.health *= factor; s.attack *= factor; s.defense *= factor;
            foreach (Affix a in item.affixes)
            {
                switch(a.stat) { case 0: s.health += a.value; break; case 1: s.attack += a.value; break;
                    case 2: s.defense += a.value; break; case 3: s.haste += a.value; break;
                    case 4: s.crit += a.value; break; case 5: s.luck += a.value; break; }
            }
            return s;
        }
        public Stats Total()
        {
            Stats s = new Stats(Balance.BaseHealth + state.training * Balance.TrainingHealth, Balance.BaseAttack + state.training * Balance.TrainingAttack, Balance.BaseDefense, 0, Balance.BaseCrit);
            for(int i = 0; i < 4; i++) { Item item = Equipped((Slot)i); if (item != null) s.Add(ItemStats(item)); }
            s.haste = Math.Max(-.5f, Math.Min(Balance.HasteCap, s.haste)); s.crit = Math.Max(0, Math.Min(Balance.CritCap, s.crit)); s.luck = Math.Max(0, Math.Min(Balance.LuckCap, s.luck));
            return s;
        }
        public int EffectCount(Effect effect)
        { return Math.Min(2, state.items.Count(i => state.equipped.Contains(i.uid) && Catalog.Find(i.baseId).effect == effect)); }
        public bool Has(Effect effect) { return EffectCount(effect) > 0; }
        public bool IsMelee
        { get { Item weapon = Equipped(Slot.Weapon); return weapon != null && Catalog.Find(weapon.baseId).attackStyle == AttackStyle.Melee; } }
        public string LoadoutSummary
        {
            get
            {
                var parts = new List<string>();
                if (Has(Effect.Drone)) parts.Add("无人机");
                if (Has(Effect.Mark)) parts.Add("目标标记");
                if (Has(Effect.Arc)) parts.Add("电弧" + (Has(Effect.Mark) ? "引爆" : ""));
                if (Has(Effect.Thruster)) parts.Add(IsMelee ? "近战突袭" : "推进射击");
                if (Has(Effect.Guard)) parts.Add("护盾反击");
                if (Has(Effect.Leech)) parts.Add("汲取");
                return parts.Count == 0 ? "常规攻击" : string.Join(" + ", parts);
            }
        }
        public bool SetRecoveryTarget(string id)
        {
            if (!string.IsNullOrEmpty(id) && Catalog.Find(id) == null) return false;
            if (state.recoveryTarget != id) state.targetBossMisses = 0;
            state.recoveryTarget = string.IsNullOrEmpty(id) ? null : id;
            return true;
        }
        public bool TargetActive
        { get { return !string.IsNullOrEmpty(state.recoveryTarget) && Catalog.SourceRegion(state.recoveryTarget) == Catalog.District(state.region); } }
        public bool Equip(string uid)
        {
            Item item = state.items.FirstOrDefault(x => x.uid == uid);
            if (item == null) return false;
            state.equipped[(int)Catalog.Find(item.baseId).slot] = uid;
            return true;
        }
        public bool SetAppearance(string baseId)
        {
            EquipmentBase b = Catalog.Find(baseId);
            if (b == null || !state.collection.Contains(baseId)) return false;
            state.appearance[(int)b.slot] = baseId; return true;
        }
        public bool Salvage(string uid)
        {
            Item item = state.items.FirstOrDefault(x => x.uid == uid);
            if (item == null || item.locked || state.equipped.Contains(uid)) return false;
            state.parts[(int)Catalog.Find(item.baseId).slot] += SalvageValue(item);
            if ((int)item.quality >= (int)Quality.Legendary) state.cores++;
            state.items.Remove(item); return true;
        }
        public static int SalvageValue(Item item) { return (2 + (int)item.quality * 3) * item.level; }
        // 许可等级：装备升级的上限。击败一关首领解锁下一关时提高。
        public int License { get { return Math.Min(Balance.MaxLicense, (state.unlockedRegion + 1) * Balance.LicenseStep); } }
        public int UpgradeCost(Item item) { return Balance.UpgradeCost(item.level, item.quality); }
        public bool CanUpgrade(Item item)
        { return item != null && item.level < License && state.parts[(int)Catalog.Find(item.baseId).slot] >= UpgradeCost(item); }
        public bool Upgrade(string uid)
        {
            Item item = state.items.FirstOrDefault(x => x.uid == uid);
            if (!CanUpgrade(item)) return false;
            state.parts[(int)Catalog.Find(item.baseId).slot] -= UpgradeCost(item);
            float ratio = Balance.AffixLevelFactor(item.level + 1) / Balance.AffixLevelFactor(item.level);
            item.level++;
            foreach (Affix a in item.affixes) a.value *= ratio;
            return true;
        }
        // 重校：把一条随机词条换成这件装备上没有的另一种属性，并重新抽取数值。消耗传感件，次数越多越贵。
        public int RerollCost(Item item) { return Balance.RerollCost(item.quality, item.rerolls); }
        public bool CanReroll(Item item, int index)
        { return item != null && index >= 0 && index < item.affixes.Count && item.affixes.Count < 6 && state.parts[1] >= RerollCost(item); }
        public bool Reroll(string uid, int index)
        {
            Item item = state.items.FirstOrDefault(x => x.uid == uid);
            if (!CanReroll(item, index)) return false;
            List<int> pool = new List<int>();
            for (int stat = 0; stat < 6; stat++) if (!item.affixes.Any(a => a.stat == stat)) pool.Add(stat);
            if (pool.Count == 0) return false;
            state.parts[1] -= RerollCost(item);
            int chosen = pool[rng.Next(pool.Count)];
            item.affixes[index].stat = chosen;
            item.affixes[index].value = Balance.AffixValue(chosen, item.quality, item.level, (float)rng.NextDouble());
            item.rerolls++;
            return true;
        }
        // 突破：用核心把「机密」装备提升为「原型」，并补上第五条词条。
        public bool CanAscend(Item item) { return item != null && item.quality == Quality.Legendary && state.cores >= Balance.AscendCores; }
        public bool Ascend(string uid)
        {
            Item item = state.items.FirstOrDefault(x => x.uid == uid);
            if (!CanAscend(item)) return false;
            state.cores -= Balance.AscendCores; item.quality = Quality.Prototype;
            List<int> pool = new List<int>();
            for (int stat = 0; stat < 6; stat++) if (!item.affixes.Any(a => a.stat == stat)) pool.Add(stat);
            if (pool.Count > 0 && item.affixes.Count < Balance.Tier(Quality.Prototype).affixCount)
            {
                int chosen = pool[rng.Next(pool.Count)];
                item.affixes.Add(new Affix { stat = chosen, value = Balance.AffixValue(chosen, item.quality, item.level, (float)rng.NextDouble()) });
            }
            return true;
        }
        public int TrainingCost { get { return Balance.TrainingCost(state.training); } }
        public bool Train()
        {
            if (state.training >= Balance.TrainingCap || state.scrap < TrainingCost) return false;
            state.scrap -= TrainingCost; state.training++; return true;
        }
        public bool ChangeRegion(int region)
        {
            if (region < 0 || region > state.unlockedRegion) return false;
            state.region = region; return true;
        }
        public bool Chance(double chance) { return rng.NextDouble() < chance; }
        // certain：精英必掉一件，但品质仍按普通规则抽取。
        public Item Drop(bool boss, bool certain = false)
        {
            if (!boss && !certain && !Chance(Balance.NormalDropChance)) return null;
            Quality q = RollQuality(boss);
            if (boss && state.region >= Catalog.Districts && Chance(Balance.PrototypeBossChance)) q = Quality.Prototype;
            string[] exclusive = { "c1", "c2", "w2" };
            EquipmentBase[] pool = Catalog.All.Where(b => Catalog.SourceRegion(b.id) == Catalog.District(state.region)).ToArray();
            string baseId = boss && Chance(Balance.FeaturedBossChance) ? exclusive[Catalog.District(state.region)] : pool[rng.Next(pool.Length)].id;
            // Targeting changes only the template, never quantity, item level, or quality pity.
            if (boss && TargetActive && (state.targetBossMisses >= Balance.TargetBossGuarantee-1 || Chance(Balance.TargetBossChance))) baseId = state.recoveryTarget;
            if (baseId == state.recoveryTarget) state.targetBossMisses = 0;
            else if (boss && TargetActive) state.targetBossMisses++;
            // 掉落等级落在本关的等级段里：首领偏高，其余随击败数小幅浮动（不额外消耗随机数）。
            Item item = Create(baseId, q, state.region * Balance.LicenseStep + 1 + (boss ? 2 : 0) + state.kills % 3);
            state.items.Add(item); Unlock(item.baseId); return item;
        }
        public Quality RollQuality(bool boss)
        {
            float luck = Total().luck;
            double[] weights = Balance.QualityWeights(luck);
            double roll = rng.NextDouble() * weights.Sum();
            int selected = 4;
            for (int i = 0; i < 5; i++) { roll -= weights[i]; if (roll < 0) { selected = i; break; } }
            if (boss || state.sinceRare >= Balance.RarePity) selected = Math.Max(selected, 2);
            if (state.sinceEpic >= Balance.EpicPity) selected = Math.Max(selected, 3);
            if (state.sinceLegendary >= Balance.LegendaryPity) selected = 4;
            state.sinceRare = selected >= 2 ? 0 : state.sinceRare + 1;
            state.sinceEpic = selected >= 3 ? 0 : state.sinceEpic + 1;
            state.sinceLegendary = selected >= 4 ? 0 : state.sinceLegendary + 1;
            return (Quality)selected;
        }
        public static float Damage(float attack, float defense) { return Math.Max(1, attack * 100 / (100 + Math.Max(0, defense))); }
    }
}
