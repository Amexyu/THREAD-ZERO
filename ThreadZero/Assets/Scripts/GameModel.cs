using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreadZero
{
    public enum Slot { Weapon, Head, Coat, Back, Legs }   // v0.16 加下装（Legs）
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
        public string[] equipped = new string[Catalog.SlotCount];
        public string[] appearance = new string[Catalog.SlotCount];
        public int region, unlockedRegion, scrap, training, kills, bosses, bossMask;   // bossMask：每章首领首通一位
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
        // v0.16 义体（机械飞升）：每章首领给一件，永久生效，角色身上可见。
        public List<string> implants = new List<string>();
        // v0.16 回声层：当前层（0 = 不在回声层）、历史最高层、生效的协议异常。
        public int echoFloor, echoBest;
        public List<string> anomalies = new List<string>();
    }

    public sealed class ImplantDef
    {
        public readonly string id, name, part, desc; public readonly Stats stats;
        public ImplantDef(string key, string title, string body, string text, Stats value) { id = key; name = title; part = body; desc = text; stats = value; }
    }

    public static class Catalog
    {
        // v0.16 机械飞升：5 章各一件义体（后面 4 件先占位，做到那一章再定数值和外观）。
        public const int AscensionTotal = 5;
        public static readonly ImplantDef[] Implants = {
            new ImplantDef("arm_l", "绞盘机械臂", "手臂", "闸门看守的绞盘核心改成的机械臂。攻速 +15%。", new Stats(0, 0, 0, .15f)),
            new ImplantDef("eye_r", "调度义眼", "义眼", "总调度吊臂的调度镜头改成的义眼。暴击 +6%，攻击 +4。", new Stats(0, 4, 0, 0, .06f)),
        };
        public static ImplantDef FindImplant(string id) { foreach (ImplantDef d in Implants) if (d.id == id) return d; return null; }
        public const int SlotCount = 5;
        public static readonly string[] SlotNames = { "武器", "头部", "外套", "背部", "下装" };
        public static readonly string[] PartNames = { "枪机件", "传感件", "装甲片", "动力件", "护腿件" };
        public static readonly string[] QualityNames = Balance.Qualities.Select(q=>q.name).ToArray();
        // v0.8：六个关卡 = 三个街区 × 两层。后三关是同一街区的深层，敌人更强，掉落等级更高。
        public const int Districts = 3;
        // v0.14：region = 第一章的第几关（0..6 = 1-1..1-7）。名字是场景占位名，正式关卡名待定。
        public static readonly string[] RegionNames = { "排污口", "积水暗渠", "棚户下层", "晒衣走道", "泵站入口", "压力管廊", "抽水总闸",
                                                         "货运坡道", "高架轨线", "货柜堆场", "吊装通道", "分拣仓", "传送干线", "总调度塔" };
        public static readonly string[] SceneNames = { "废液沟渠", "叠层棚户", "泵站管廊", "抽水总闸", "高架货轨", "货柜堆场", "分拣仓内", "总调度塔" };
        public static readonly string[] ChapterNames = { "沉降区", "物流高架" };
        /// 每关一句叙事（企划书 · 第一章逐关设计），转场的关卡条和选关页用。
        public static readonly string[] StageLines = {
            "回收员从沉降区最底部的排污口爬了出来", "巡逻机开始成群出现，封锁比传言中更严", "棚屋里的人都不见了，只剩下衣服",
            "悬空走道上挂满无主的旧衣", "广播里第一次出现「零号协议」", "管廊尽头就是整个沉降区的闸门", "闸门看守：这一层的管理者",
            "沉降区之上，是彻夜不停的雨中货运线", "无人货机沿着高架轨道来回巡逻", "货柜垒到看不见顶，里面有东西在走动",
            "吊装通道的探照灯一直跟着回收员", "分拣仓把一切都当作货物处理，包括人", "传送干线的尽头亮着总调度塔的灯", "总调度吊臂：整条货运线的管理者" };
        public static string StageCode(int region) { return Chapters.ChapterOf(region) + "-" + (region % Chapters.StagesPerChapter + 1); }
        /// 某件装备在第一章哪几关掉落（按 District 轮换），例如 "1-1 / 1-4 / 1-7"。
        public static string SourceStages(string id)
        {
            if (IsStarter(id)) return "初始装备";
            int ch = ChapterOf(id); string name = ch == 2 ? "第二章全关" : "第一章全关";
            return (id == BossExclusiveOf(ch) ? name + " · " + ch + "-7 首领必掉" : name) + " · 回声层";
        }
        public static int LastRegion { get { return RegionNames.Length - 1; } }
        public static int District(int region) { return ((region % Districts) + Districts) % Districts; }
        public static readonly string[] BossNamesByChapter = { "闸门看守", "总调度吊臂" };
        public static string BossName(int chapter) { return BossNamesByChapter[Math.Max(0, Math.Min(BossNamesByChapter.Length - 1, chapter - 1))]; }
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
            new EquipmentBase("b4", "轻量推进背架", Slot.Back, 4, new Stats(8, 0, 0, .08f), Effect.Thruster),
            // ---- v0.16 第二章「物流高架」20 件（雨夜货运）。先在回声层掉落，第二章关卡做好后也在第二章掉。----
            new EquipmentBase("w5", "撞杆矛", Slot.Weapon, 0, new Stats(0, 9, 0, -.02f), Effect.None, AttackStyle.Melee),
            new EquipmentBase("w6", "气动钉枪", Slot.Weapon, 1, new Stats(0, 6, 0, .06f), Effect.Mark),
            new EquipmentBase("w7", "吊臂钩索", Slot.Weapon, 2, new Stats(8, 9, 1), Effect.Leech, AttackStyle.Melee),
            new EquipmentBase("w8", "切割锯", Slot.Weapon, 3, new Stats(0, 6, 0, .1f, .02f), Effect.Execute, AttackStyle.Melee),
            new EquipmentBase("w9", "信号弩", Slot.Weapon, 4, new Stats(0, 8, 0, 0, .04f), Effect.Arc),
            new EquipmentBase("h5", "反光雨帽", Slot.Head, 0, new Stats(12, 0, 2, 0, 0, 2)),
            new EquipmentBase("h6", "调度耳麦", Slot.Head, 1, new Stats(6, 1, 1, .05f), Effect.Mark),
            new EquipmentBase("h7", "吊车安全盔", Slot.Head, 2, new Stats(20, 0, 4), Effect.Guard),
            new EquipmentBase("h8", "货柜焊接面罩", Slot.Head, 3, new Stats(16, 0, 5)),
            new EquipmentBase("h9", "雨幕全息面罩", Slot.Head, 4, new Stats(8, 1, 0, 0, .04f, 2), Effect.Mark),
            new EquipmentBase("c5", "反光雨披", Slot.Coat, 0, new Stats(22, 0, 2, 0, 0, 3)),
            new EquipmentBase("c6", "装卸工负重马甲", Slot.Coat, 1, new Stats(26, 1, 3), Effect.Guard),
            new EquipmentBase("c7", "防水潜工夹克", Slot.Coat, 2, new Stats(32, 0, 4)),
            new EquipmentBase("c8", "轨道维修夹克", Slot.Coat, 3, new Stats(20, 2, 2, .03f), Effect.Arc),
            new EquipmentBase("c9", "信号员长衣", Slot.Coat, 4, new Stats(20, 1, 2, .04f, .02f), Effect.Mark),
            new EquipmentBase("b5", "迷你集装箱背架", Slot.Back, 0, new Stats(20, 0, 4), Effect.Guard),
            new EquipmentBase("b6", "绞盘线轴", Slot.Back, 1, new Stats(12, 1, 1, .04f), Effect.Thruster),
            new EquipmentBase("b7", "伞架无人机", Slot.Back, 2, new Stats(10, 1, 0, 0, 0, 2), Effect.Drone),
            new EquipmentBase("b8", "电池组信号塔", Slot.Back, 3, new Stats(10, 1, 0, .03f, 0, 2), Effect.Arc),
            new EquipmentBase("b9", "折叠货梯", Slot.Back, 4, new Stats(16, 0, 2, 0, 0, 3)),
            // ---- v0.16 下装：l0 初始，l1–l4 第一章，l5–l9 第二章。下装偏生命 / 防御，少量攻速和幸运。----
            new EquipmentBase("l0", "回收员工装裤", Slot.Legs, 0, new Stats(10, 0, 1)),
            new EquipmentBase("l1", "排污防水裤", Slot.Legs, 1, new Stats(14, 0, 2)),
            new EquipmentBase("l2", "棚户补丁裤", Slot.Legs, 2, new Stats(10, 0, 1, 0, 0, 2)),
            new EquipmentBase("l3", "护膝战术裤", Slot.Legs, 3, new Stats(16, 0, 3)),
            new EquipmentBase("l4", "管廊液压腿甲", Slot.Legs, 4, new Stats(12, 1, 2, .03f)),
            new EquipmentBase("l5", "荧光雨裤", Slot.Legs, 0, new Stats(20, 0, 3, 0, 0, 2)),
            new EquipmentBase("l6", "装卸工护腿", Slot.Legs, 1, new Stats(26, 0, 4)),
            new EquipmentBase("l7", "防水潜工长靴裤", Slot.Legs, 2, new Stats(22, 0, 3, .02f)),
            new EquipmentBase("l8", "轨道检修工装裤", Slot.Legs, 3, new Stats(18, 1, 2, .03f)),
            new EquipmentBase("l9", "信号员马裤", Slot.Legs, 4, new Stats(16, 1, 2, 0, .02f, 2))
        };
        public static EquipmentBase Find(string id) { return All.FirstOrDefault(x => x.id == id); }
        // v0.16 掉落池：整章通用——第一章任何一关都能掉全章 16 件（初始的 4 件不掉）。
        // 首领关额外必掉专属件；以后每章一个池子，按章节切换。
        public static readonly string[] StarterIds = { "w1", "h0", "c0", "b0", "l0" };
        // 首领必掉的专属件（平时也能掉）：闸门看守 → 逆流手炮，总调度吊臂 → 吊臂钩索。
        public static string BossExclusiveOf(int chapter) { return chapter == 2 ? "w7" : "w3"; }
        public static bool IsStarter(string id) { return System.Array.IndexOf(StarterIds, id) >= 0; }
        /// 装备属于第几章：编号 0–4 第一章，5–9 第二章……
        public static int ChapterOf(string id) { int n; return id != null && id.Length > 1 && int.TryParse(id.Substring(1), out n) ? n / 5 + 1 : 1; }
        /// 会不会掉：主线每章只掉本章的装备（整章通用）；回声层掉所有章节。初始的 4 件都不掉。
        public static bool DropsIn(string id, bool echo, int chapter) { return Find(id) != null && !IsStarter(id) && (echo || ChapterOf(id) == chapter); }
        private static readonly Dictionary<int, string[]> pools = new Dictionary<int, string[]>();
        public static string[] DropPool(bool echo, int chapter)
        {
            int key = echo ? 0 : chapter; string[] p;
            if (!pools.TryGetValue(key, out p)) pools[key] = p = All.Where(b => DropsIn(b.id, echo, chapter)).Select(b => b.id).ToArray();
            return p;
        }
        /// 产地显示用：整章通用，统一返回第一关。
        public static int SourceRegion(string id) { return 0; }
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
                foreach (string id in new[] { "w1", "h0", "c0", "b0", "l0" })   // v0.14：初始武器是 w1 匕首（带迅切连击）
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
                || state.equipped.Length < 4 || state.appearance.Length < 4) throw new InvalidOperationException("存档结构不完整");
            // v0.16 老存档只有 4 个部位：补上下装一格
            if (state.equipped.Length < Catalog.SlotCount) Array.Resize(ref state.equipped, Catalog.SlotCount);
            if (state.appearance.Length < Catalog.SlotCount) Array.Resize(ref state.appearance, Catalog.SlotCount);
            var ids = new HashSet<string>();
            foreach (Item item in state.items)
            {
                if (item == null || Catalog.Find(item.baseId) == null || string.IsNullOrEmpty(item.uid) || !ids.Add(item.uid)
                    || item.level < 1 || item.level > Balance.MaxItemLevel || (int)item.quality < 0 || (int)item.quality > 5 || item.affixes == null)
                    throw new InvalidOperationException("装备数据无效");
                var affixIds = new HashSet<int>();
                foreach (Affix a in item.affixes)
                    if (a == null || a.stat < 0 || a.stat > 5 || float.IsNaN(a.value) || float.IsInfinity(a.value)
                        || a.value < 0 || !affixIds.Add(a.stat)) throw new InvalidOperationException("装备词条无效");
            }
            for (int s = 0; s < Catalog.SlotCount; s++)
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
            // v0.16 统一货币：四种部位零件并入「零件」（scrap）。老存档里攒的部位零件全部折进来。
            for (int i = 0; i < 4; i++) { state.scrap += Math.Max(0, state.parts[i]); state.parts[i] = 0; }
            state.cores = Math.Max(0, state.cores);
            if (state.implants == null) state.implants = new List<string>();
            if (state.anomalies == null) state.anomalies = new List<string>();
            state.anomalies = state.anomalies.Where(id => Chapters.FindAnomaly(id) != null).Distinct().Take(Chapters.MaxAnomalies).ToList();
            state.echoFloor = Math.Max(0, state.echoFloor); state.echoBest = Math.Max(state.echoBest, state.echoFloor);
            state.implants = state.implants.Where(id => Catalog.FindImplant(id) != null).Distinct().ToList();
            // 老存档：已经打过第一章首领（目前唯一的首领）但还没有义体，补上绞盘左臂。
            // 老存档（还没有 bossMask）：打过首领 = 打过第一章首领，补上绞盘机械臂并开放 2-1。
            // 有了 bossMask 以后按章判断，不再因为打过别的首领误补手臂（之前测试存档只打 2-7 也会被补上手臂）。
            if (state.bossMask == 0 && state.bosses > 0) state.bossMask = 1;
            if ((state.bossMask & 1) != 0)
            {
                if (!state.implants.Contains("arm_l")) state.implants.Add("arm_l");
                state.unlockedRegion = Math.Max(state.unlockedRegion, Chapters.FirstRegion(2));
            }
            if ((state.bossMask & 2) != 0 && !state.implants.Contains("eye_r")) state.implants.Add("eye_r");
            if (state.schema < 2)
            {
                // 旧存档的装备等级是 1–6（关卡序号），换算到新等级段的中间值。
                foreach (Item item in state.items) item.level = Math.Min(Balance.MaxLicense, (item.level - 1) * Balance.LicenseStep + 3);
                state.schema = 2;
            }
            if (state.schema < 3)
            {
                // v0.14：初始武器从 w0 短铳改成 w1 匕首。老存档补一把普通 w1；如果还拿着初始 w0 就顺手换上。
                if (state.items.Count > 0 && !state.items.Any(x => x.baseId == "w1"))
                {
                    Item dagger = Create("w1", Quality.Common, 1);
                    state.items.Add(dagger); Unlock("w1");
                    Item held = state.items.FirstOrDefault(x => x.uid == state.equipped[(int)Slot.Weapon]);
                    if (held == null || (held.baseId == "w0" && held.quality == Quality.Common)) state.equipped[(int)Slot.Weapon] = dagger.uid;
                }
                state.schema = 3;
            }
            if (state.schema < 4)
            {
                // v0.16 加下装：老存档补一条初始工装裤并穿上。
                if (state.items.Count > 0 && !state.items.Any(x => x.baseId == "l0"))
                {
                    Item pants = Create("l0", Quality.Common, 1);
                    state.items.Add(pants); Unlock("l0");
                    if (Equipped(Slot.Legs) == null) state.equipped[(int)Slot.Legs] = pants.uid;
                }
                state.schema = 4;
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
            Item item = new Item { uid = Guid.NewGuid().ToString("N"), baseId = id, quality = quality, level = Math.Max(1, Math.Min(Balance.MaxItemLevel, level)) };
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
        /// 测试用（F6）：攻击、生命 ×10，并且不会被打倒。不写进存档。
        public static bool TestPower;
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetTestPower() { TestPower = false; }
        public Stats Total()
        {
            Stats s = new Stats(Balance.BaseHealth + state.training * Balance.TrainingHealth, Balance.BaseAttack + state.training * Balance.TrainingAttack, Balance.BaseDefense, 0, Balance.BaseCrit);
            for(int i = 0; i < Catalog.SlotCount; i++) { Item item = Equipped((Slot)i); if (item != null) s.Add(ItemStats(item)); }
            foreach (string id in state.implants) { ImplantDef d = Catalog.FindImplant(id); if (d != null) s.Add(d.stats); }
            if (TestPower) { s.attack *= 10; s.health *= 10; }
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
        { get { return !string.IsNullOrEmpty(state.recoveryTarget) && Catalog.DropsIn(state.recoveryTarget, InEcho, Chapter); } }
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
            state.scrap += SalvageValue(item);
            if ((int)item.quality >= (int)Quality.Legendary) state.cores++;
            state.items.Remove(item); return true;
        }
        public static int SalvageValue(Item item) { return (2 + (int)item.quality * 3) * item.level; }
        // 许可等级：装备升级的上限。击败一关首领解锁下一关时提高。
        // 许可等级（装备升级上限）：主线按关卡，最高 30；打通第一章后放开到装备等级上限，靠零件慢慢升。
        public int License { get { return EchoUnlocked ? Balance.MaxItemLevel : Math.Min(Balance.MaxLicense, (state.unlockedRegion + 1) * Balance.LicenseStep); } }
        // ---------- 回声层 ----------
        public bool InEcho { get { return state.echoFloor > 0; } }
        /// 当前主线所在章节（回声层里也按进入前的关卡算）。
        public int Chapter { get { return Chapters.ChapterOf(state.region); } }
        /// 某一章的首领打过没有（v0.16 起按章记录；老存档 bosses > 0 视为打过第一章）。
        public bool BossCleared(int chapter) { return (state.bossMask & (1 << (chapter - 1))) != 0 || (chapter == 1 && state.bossMask == 0 && state.bosses > 0); }
        public bool EchoUnlocked { get { return state.bosses > 0; } }
        public bool Anomaly(string id) { return InEcho && state.anomalies.Contains(id); }
        public int UpgradeCost(Item item) { return Balance.UpgradeCost(item.level, item.quality); }
        public bool CanUpgrade(Item item)
        { return item != null && item.level < License && state.scrap >= UpgradeCost(item); }
        public bool Upgrade(string uid)
        {
            Item item = state.items.FirstOrDefault(x => x.uid == uid);
            if (!CanUpgrade(item)) return false;
            state.scrap -= UpgradeCost(item);
            float ratio = Balance.AffixLevelFactor(item.level + 1) / Balance.AffixLevelFactor(item.level);
            item.level++;
            foreach (Affix a in item.affixes) a.value *= ratio;
            return true;
        }
        // 重校：把一条随机词条换成这件装备上没有的另一种属性，并重新抽取数值。消耗零件，次数越多越贵。
        public int RerollCost(Item item) { return Balance.RerollCost(item.quality, item.rerolls); }
        public bool CanReroll(Item item, int index)
        { return item != null && index >= 0 && index < item.affixes.Count && item.affixes.Count < 6 && state.scrap >= RerollCost(item); }
        public bool Reroll(string uid, int index)
        {
            Item item = state.items.FirstOrDefault(x => x.uid == uid);
            if (!CanReroll(item, index)) return false;
            List<int> pool = new List<int>();
            for (int stat = 0; stat < 6; stat++) if (!item.affixes.Any(a => a.stat == stat)) pool.Add(stat);
            if (pool.Count == 0) return false;
            state.scrap -= RerollCost(item);
            int chosen = pool[rng.Next(pool.Count)];
            item.affixes[index].stat = chosen;
            item.affixes[index].value = Balance.AffixValue(chosen, item.quality, item.level, (float)rng.NextDouble());
            item.rerolls++;
            return true;
        }
        // 突破：用核心把「机密」装备提升为「原型」，并补上第五条词条。
        // v0.16 门槛：8 个核心 + 装备先升到当前等级上限（许可等级）。
        public bool CanAscend(Item item) { return item != null && item.quality == Quality.Legendary && state.cores >= Balance.AscendCores && item.level >= License; }
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
        /// 掉一件装备。minQuality：保底品质（精英、首领用）；forceId：指定模板（首领专属）。
        public Item Drop(bool boss, bool certain = false, bool elite = false, Quality minQuality = Quality.Common, string forceId = null)
        {
            if (!boss && !certain && !Chance(Balance.NormalDropChance)) return null;
            Quality q = RollQuality(minQuality);
            if (boss && forceId != null && Chance(Balance.PrototypeBossChance)) q = Quality.Prototype;
            string[] pool = Catalog.DropPool(InEcho, Chapter);
            string baseId = forceId ?? pool[rng.Next(pool.Length)];
            // 定向回收：精英和首领的掉落有机会（并有保底）换成回收目标；只换模板，不影响数量、等级、品质保底。
            bool targeted = (boss || elite) && forceId == null && TargetActive;
            if (targeted && (state.targetBossMisses >= Balance.TargetBossGuarantee-1 || Chance(Balance.TargetBossChance))) baseId = state.recoveryTarget;
            if (baseId == state.recoveryTarget) state.targetBossMisses = 0;
            else if (targeted) state.targetBossMisses++;
            // 掉落等级落在本关的等级段里：首领偏高，其余随击败数小幅浮动（不额外消耗随机数）。
            // v0.14：掉落等级由关卡决定（每关一个等级段），精英/首领掉段内偏高的等级。等级比品质更重要。
            int level = InEcho ? Chapters.EchoDropLevel(state.echoFloor, boss, elite, rng.NextDouble()) : Chapters.DropLevel(state.region, boss, elite, rng.NextDouble());
            Item item = Create(baseId, q, level);
            state.items.Add(item); Unlock(item.baseId); return item;
        }
        public Quality RollQuality(Quality minQuality)
        {
            float luck = Total().luck + (Anomaly("overload") ? 15 : 0);
            double[] weights = Balance.QualityWeights(luck);
            double roll = rng.NextDouble() * weights.Sum();
            int selected = 4;
            for (int i = 0; i < 5; i++) { roll -= weights[i]; if (roll < 0) { selected = i; break; } }
            selected = Math.Max(selected, (int)minQuality);
            if (state.sinceRare >= Balance.RarePity) selected = Math.Max(selected, 2);
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
