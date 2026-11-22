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
        public float critDamage, effectDamage, bossDamage, scrapBonus;   // v0.18 特殊词条（只来自装备词条）
        // v0.21 芯片专属属性（普通词条池里没有）：攻击% 生命% 技能冷却缩短 伤害回血 受到伤害减免 单体伤害；改造板的技能伤害
        public float attackPct, healthPct, cooldown, leech, reduction, focus, skillDamage;
        public Stats() { }
        public Stats(float hp, float atk, float def, float speed = 0, float critical = 0, float fortune = 0)
        { health = hp; attack = atk; defense = def; haste = speed; crit = critical; luck = fortune; }
        public void Add(Stats s)
        { health += s.health; attack += s.attack; defense += s.defense; haste += s.haste; crit += s.crit; luck += s.luck;
          critDamage += s.critDamage; effectDamage += s.effectDamage; bossDamage += s.bossDamage; scrapBonus += s.scrapBonus;
          attackPct += s.attackPct; healthPct += s.healthPct; cooldown += s.cooldown; leech += s.leech; reduction += s.reduction; focus += s.focus; skillDamage += s.skillDamage; }
        public Stats Copy() { var c = new Stats(health, attack, defense, haste, crit, luck); c.critDamage = critDamage; c.effectDamage = effectDamage; c.bossDamage = bossDamage; c.scrapBonus = scrapBonus;
          c.attackPct = attackPct; c.healthPct = healthPct; c.cooldown = cooldown; c.leech = leech; c.reduction = reduction; c.focus = focus; c.skillDamage = skillDamage; return c; }
    }

    public sealed class EquipmentBase
    {
        public readonly string id, name;
        public readonly Slot slot;
        public readonly int style;
        public readonly Stats stats;
        public readonly Effect effect;
        public readonly AttackStyle attackStyle;
        /// v0.18：美术未到的装备借用哪件旧装备的外观（art）、武器借用哪把旧武器的招式和技能（mech）。null = 用自己的。
        public readonly string art, mech;
        public EquipmentBase(string key, string title, Slot part, int look, Stats value, Effect proc = Effect.None, AttackStyle attack = AttackStyle.Ranged, string artFrom = null, string mechFrom = null)
        { id = key; name = title; slot = part; style = look; stats = value; effect = proc; attackStyle = attack; art = artFrom; mech = mechFrom; }
    }

    [Serializable]
    public sealed class Affix { public int stat; public float value; }

    /// v0.21 芯片：kind 见 Catalog.ChipNames（过载 装甲 调度 汲取 缓冲 聚焦），tier 1–3（Mk1–Mk3）。植入装备后不能拆。
    [Serializable]
    public sealed class Chip { public int kind, tier; }

    [Serializable]
    public sealed class Item
    {
        public string uid, baseId;
        public Quality quality;
        public int level;
        public bool locked;
        public int rerolls;
        public int affixLocks;   // v0.18 洗炼时锁住的词条（按位，第 i 条 = 1 << i）
        public List<Affix> affixes = new List<Affix>();
        public List<Chip> chips = new List<Chip>();   // v0.21 植入的芯片（最多 Balance.ChipSlots[品质] 块）
        public int mod;                               // v0.21 改装件：0 = 没装，其余 = (int)Effect
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
        public int retryRegion = -1, retryFarms, retryStreak; public float retryPower;   // v0.19 变强后再挑战：倒在哪一关、倒下时的战力、回刷了几次、在这一关连着倒了几次
        public int sinceRare, sinceEpic, sinceLegendary;
        public string recoveryTarget;
        public int targetBossMisses;
        public int glowMode = 2;
        public int shakeMode;   // v0.20 震屏：0 完整 / 1 减弱 / 2 关闭（默认 0，老存档也是完整）
        // 自动分解档位：0 关闭，1 仅普通，2 精良及以下。旧存档没有这个字段时为 0。
        public int autoSalvage;
        // v0.10：schema 低于 2 的存档在读取时把装备等级换算到 30 级制。
        // parts 按部位分四种（枪机件、传感件、装甲片、动力件），来自分解；scrap 仍是击败敌人得到的通用零件。
        public int schema;
        public int[] parts = new int[4];
        public int cores;
        // v0.16 义体（机械飞升）：每章首领给一件，永久生效，角色身上可见。
        public List<string> implants = new List<string>();
        // v0.20 机械飞升：每件义体的等级（按 Catalog.Implants 的顺序，0 = 还没装上，装上时 1 级，最高 10 级）和织网碎片（升级材料）。
        public int[] implantLevels = new int[Catalog.AscensionTotal];
        public int shards;
        // v0.16 回声层：当前层（0 = 不在回声层）、历史最高层、生效的协议异常。
        public int echoFloor, echoBest;
        public long savedAt;      // v0.20 上次存档的时间（UTC ticks），算离线收益
        public float scrapRate;   // v0.20 挂机时每秒（游戏时间）的零件收入：击杀给的 + 掉落装备的分解价，慢慢平均
        public List<string> anomalies = new List<string>();
        public int tips, tipsVersion;   // v0.20 新手提示：看过的按位记（GameApp.Tip）；tipsVersion 0 = 还没做过老存档迁移
        public bool epilogueSeen;       // v0.20 机械飞升 100% 的尾声看过了（横幅放完后只自动弹一次）
        public List<Chip> chipBag = new List<Chip>();   // v0.21 还没植入的芯片
        public int[] modBag = new int[8];               // v0.21 改装件库存，按 (int)Effect 记数量（0 号不用）
        public bool chipGift;                           // v0.21 芯片 / 改装件开放时送的那一块和那一件领过了
    }

    public sealed class ImplantDef
    {
        public readonly string id, name, part, desc; public readonly Stats stats;
        public Stats trait5, trait10; public string trait5Text, trait10Text;   // v0.20 5 级 / 10 级各解锁一个特性（先做成固定加成）
        public ImplantDef(string key, string title, string body, string text, Stats value) { id = key; name = title; part = body; desc = text; stats = value; }
        public ImplantDef Traits(string t5, Stats s5, string t10, Stats s10) { trait5Text = t5; trait5 = s5; trait10Text = t10; trait10 = s10; return this; }
    }

    public static class Catalog
    {
        // v0.16 机械飞升：5 章各一件义体（后面 4 件先占位，做到那一章再定数值和外观）。
        public const int AscensionTotal = 5;
        public static readonly ImplantDef[] Implants = {
            new ImplantDef("arm_l", "绞盘机械臂", "手臂", "闸门看守的绞盘核心改成的机械臂。攻速 +15%。", new Stats(0, 0, 0, .15f))
                .Traits("绞盘加压：攻速再 +5%", new Stats(0, 0, 0, .05f), "全功率绞盘：攻击 +12", new Stats(0, 12, 0)),
            new ImplantDef("eye_r", "调度义眼", "义眼", "总调度吊臂的调度镜头改成的义眼。暴击 +6%，攻击 +4。", new Stats(0, 4, 0, 0, .06f))
                .Traits("弱点标定：暴击伤害 +20%", new Stats { critDamage = .2f }, "全域调度：暴击再 +6%", new Stats(0, 0, 0, 0, .06f)),
            new ImplantDef("spine", "中继脊柱", "脊柱", "断讯监视者的中继骨架改成的脊柱支架。生命 +60，防御 +6。", new Stats(60, 0, 6))
                .Traits("屏蔽椎节：防御 +10", new Stats(0, 0, 10), "全段中继：生命 +300", new Stats(300, 0, 0)),
            new ImplantDef("heart", "冷核心脏", "心脏", "镜面审计官的冷却核心改成的人工心脏。生命 +120，攻速 +5%。", new Stats(120, 0, 0, .05f))
                .Traits("冷却循环：生命 +200", new Stats(200, 0, 0), "零度泵：攻速 +8%", new Stats(0, 0, 0, .08f)),
            new ImplantDef("port", "织网接口", "接口", "织网母体的协议接口，接在后脑。攻击 +10，暴击 +5%，幸运 +5。", new Stats(0, 10, 0, 0, .05f, 5))
                .Traits("协议握手：幸运 +10", new Stats(0, 0, 0, 0, 0, 10), "零号协议：攻击 +20", new Stats(0, 20, 0)),
        };
        /// v0.20 机械飞升：义体最高等级、每升一级基础效果 +30%（10 级 = 3.7 倍）。
        public const int ImplantMaxLevel = 10;
        public const float ImplantLevelGain = .3f;
        public static int ImplantIndex(string id) { for (int i = 0; i < Implants.Length; i++) if (Implants[i].id == id) return i; return -1; }
        /// v0.20 机械飞升的阶段（机械飞升_设计.md）：飞升度到 30 / 60 / 90 / 100% 各一段。文字是草稿，外观等主角的分阶段部件画好再换。
        public static readonly int[] AscensionStageAt = { 30, 60, 90, 100 };
        public static readonly string[] AscensionStageNames = { "第一阶段 · 机械呼吸", "第二阶段 · 替换关节", "第三阶段 · 织网接口", "飞升完成 · 零号" };
        public static readonly string[] AscensionStageLines = {
            "肺换成了机械的，双手也是。回收员第一次可以不戴维生面罩睡觉。",
            "关节换成了织网的标准件，背上多了一对散热鳍。全力运转的时候，它们烧得通红。",
            "后脑接上了织网接口。每一根线缆里的声音，回收员都听得见。",
            "最后一块零件装好的时候，零织协议认出了回收员：第零号。从今天起，这座城由你来回收。" };
        /// 从 level 升到 level + 1 要多少织网碎片 / 零件（1→2：8 片，9→10：约 100 片；5 件全满约 1700 片 ≈ 回声层 150～200 层）。
        public static int ImplantShardCost(int level) { return (int)Math.Round(8 * Math.Pow(1.37, Math.Max(0, level - 1))); }
        public static int ImplantScrapCost(int level) { return 60 * level * (level + 1); }
        public static ImplantDef FindImplant(string id) { foreach (ImplantDef d in Implants) if (d.id == id) return d; return null; }
        public const int SlotCount = 5;
        public static readonly string[] SlotNames = { "武器", "头部", "外套", "背部", "下装" };
        public static readonly string[] PartNames = { "枪机件", "传感件", "装甲片", "动力件", "护腿件" };
        public static readonly string[] QualityNames = Balance.Qualities.Select(q=>q.name).ToArray();
        // v0.8：六个关卡 = 三个街区 × 两层。后三关是同一街区的深层，敌人更强，掉落等级更高。
        public const int Districts = 3;
        // v0.14：region = 第一章的第几关（0..6 = 1-1..1-7）。名字是场景占位名，正式关卡名待定。
        public static readonly string[] RegionNames = { "排污口", "积水暗渠", "棚户下层", "晒衣走道", "泵站入口", "压力管廊", "抽水总闸",
                                                         "货运坡道", "高架轨线", "货柜堆场", "吊装通道", "分拣仓", "传送干线", "总调度塔",
                                                         "中继屋顶", "天线森林", "屏蔽走廊", "断讯网桥", "干扰站", "冷却井", "监视穹顶",
                                                         "镜面街口", "空中花园", "审计长廊", "玻璃天桥", "冷却中枢", "镜像大厅", "审计之庭",
                                                         "织网边缘", "线缆深渊", "协议回廊", "零号机房", "母线井", "织网心室", "零号核心" };
        public static readonly string[] SceneNames = { "废液沟渠", "叠层棚户", "泵站管廊", "抽水总闸", "高架货轨", "货柜堆场", "分拣仓内", "总调度塔",
                                                        "天线屋顶", "中继塔廊", "干扰站内", "监视穹顶",
                                                        "镜面街区", "空中花园", "冷却中枢", "审计之庭", "织网边缘", "协议回廊", "母线机房", "零号核心" };
        public static readonly string[] ChapterNames = { "沉降区", "物流高架", "信号禁区", "镜面上城", "零号织网" };
        /// 每关一句叙事（企划书 · 第一章逐关设计），转场的关卡条和选关页用。
        public static readonly string[] StageLines = {
            "回收员从沉降区最底部的排污口爬了出来", "巡逻机开始成群出现，封锁比传言中更严", "棚屋里的人都不见了，只剩下衣服",
            "悬空走道上挂满无主的旧衣", "广播里第一次出现「零号协议」", "管廊尽头就是整个沉降区的闸门", "闸门看守：这一层的管理者",
            "沉降区之上，是彻夜不停的雨中货运线", "无人货机沿着高架轨道来回巡逻", "货柜垒到看不见顶，里面有东西在走动",
            "吊装通道的探照灯一直跟着回收员", "分拣仓把一切都当作货物处理，包括人", "传送干线的尽头亮着总调度塔的灯", "总调度吊臂：整条货运线的管理者",
            "货运线之上是信号禁区，屋顶上长满了天线", "每一根天线都在低声重复同一段广播", "中继塔之间的走廊被屏蔽甲兵封死了",
            "雨停了，回收员第一次看见了天色", "干扰站把所有信号都切成了噪音", "冷却井深处，干扰中继还在嗡嗡作响", "断讯监视者：切断整座城市声音的东西",
            "信号禁区之上，是从来不下雨的镜面上城", "这里的人从来不往下看", "审计长廊的镜面卫兵只认通行证",
            "玻璃天桥下面，是回收员一路爬上来的整座城", "上城的一切都靠冷却中枢维持体温", "镜像大厅里，回收员有一千个倒影", "镜面审计官：决定谁有资格留在上城的东西",
            "上城的地板下面，是把整座城织在一起的网", "每一根线缆都连着一个被回收的人", "协议回廊里巡逻的是第一代织网守卫",
            "零号机房：一切协议开始的地方", "母线井里流动的不是电，是记忆", "织网心室在模仿一颗跳动的心脏", "织网母体：零织协议本身" };
        public static string StageCode(int region) { return Chapters.ChapterOf(region) + "-" + (region % Chapters.StagesPerChapter + 1); }
        /// 某件装备在第一章哪几关掉落（按 District 轮换），例如 "1-1 / 1-4 / 1-7"。
        public static string SourceStages(string id)
        {
            if (IsStarter(id)) return "初始装备";
            int ch = ChapterOf(id); string name = "第" + ChapterNumber(ch) + "章全关";
            string where = id == BossExclusiveOf(ch) ? name + " · " + ch + "-7 首领必掉" : name;
            // v0.18 还没有自己装备的章节（第三章起）掉前面所有章节的装备
            for (int c = GearChapters + 1; c <= Chapters.ChapterCount; c++) where += " · 第" + ChapterNumber(c) + "章" + (id == BossExclusiveOf(c) ? "（" + c + "-7 首领必掉）" : "");
            return where + " · 回声层";
        }
        /// v0.20 能打一群的武器（霰弹、线圈跳电、手炮、重锤、撞杆矛这几类招式，后面章节借用同一套招式的也算）。
        /// 整备页武器详情标「群伤 / 单体」，卡关提示也按这个判断。
        public static bool IsGroupWeapon(string id) { string m = MechOf(id); return m == "w0" || m == "w2" || m == "w3" || m == "w4" || m == "w5"; }
        /// 衣柜卡片上放得下的短版产地（卡片约 190 宽，全称「第一章全关 · 1-7 首领必掉 · 回声层」会溢出到隔壁卡片；详情里仍用全称）。
        public static string SourceShort(string id)
        {
            if (IsStarter(id)) return "初始装备";
            int ch = ChapterOf(id);
            return id == BossExclusiveOf(ch) ? ch + "-7 首领必掉" : "第" + ChapterNumber(ch) + "章全关";
        }
        public static int LastRegion { get { return RegionNames.Length - 1; } }
        public static int District(int region) { return ((region % Districts) + Districts) % Districts; }
        public static string ChapterNumber(int chapter) { return "一二三四五六七八九"[Math.Max(0, Math.Min(8, chapter - 1))].ToString(); }
        public static readonly string[] BossNamesByChapter = { "闸门看守", "总调度吊臂", "断讯监视者", "镜面审计官", "织网母体" };
        public static string BossName(int chapter) { return BossNamesByChapter[Math.Max(0, Math.Min(BossNamesByChapter.Length - 1, chapter - 1))]; }
        public static readonly string[] StatNames = { "生命", "攻击", "防御", "攻速", "暴击", "幸运", "暴击伤害", "特效伤害", "首领伤害", "零件回收" };
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
            new EquipmentBase("l9", "信号员马裤", Slot.Legs, 4, new Stats(16, 1, 2, 0, .02f, 2)),
            // ---- v0.18 第三章 25 件（美术未到：外观借 art 指定的旧装备，武器招式借 mech 指定的旧武器）----
            // v0.20 卡27：第三～五章 15 把武器（w10–w24）都有了自己的图（art = null），招式仍借同类旧武器（mech）。
            new EquipmentBase("w10", "天线警棍", Slot.Weapon, 0, new Stats(0f, 10.3f, 0f, -0.02f), Effect.Arc, AttackStyle.Melee, null, "w8"),
            new EquipmentBase("w11", "定向干扰枪", Slot.Weapon, 1, new Stats(0f, 6.9f, 0f, 0.06f), Effect.Mark, AttackStyle.Ranged, null, "w6"),
            new EquipmentBase("w12", "雷达碟炮", Slot.Weapon, 2, new Stats(9.2f, 10.3f, 1.1f), Effect.None, AttackStyle.Ranged, null, "w3"),
            new EquipmentBase("w13", "屏蔽网刃", Slot.Weapon, 3, new Stats(0f, 6.9f, 0f, 0.1f, 0.02f), Effect.Guard, AttackStyle.Melee, null, "w5"),
            new EquipmentBase("w14", "中继弩枪", Slot.Weapon, 4, new Stats(0f, 9.2f, 0f, 0f, 0.04f), Effect.Arc, AttackStyle.Ranged, null, "w9"),
            new EquipmentBase("h10", "天线耳罩", Slot.Head, 0, new Stats(13.8f, 0f, 2.3f, 0f, 0f, 2), Effect.Mark, AttackStyle.Ranged, "h5", null),
            new EquipmentBase("h11", "降噪头盔", Slot.Head, 1, new Stats(6.9f, 1.1f, 1.1f, 0.05f), Effect.Guard, AttackStyle.Ranged, "h6", null),
            new EquipmentBase("h12", "信号目镜", Slot.Head, 2, new Stats(23, 0f, 4.6f), Effect.Mark, AttackStyle.Ranged, "h7", null),
            new EquipmentBase("h13", "屏蔽面罩", Slot.Head, 3, new Stats(18.4f, 0f, 5.8f), Effect.None, AttackStyle.Ranged, "h8", null),
            new EquipmentBase("h14", "中继头环", Slot.Head, 4, new Stats(9.2f, 1.1f, 0f, 0f, 0.04f, 2), Effect.None, AttackStyle.Ranged, "h9", null),
            new EquipmentBase("c10", "信号员风衣", Slot.Coat, 0, new Stats(25.3f, 0f, 2.3f, 0f, 0f, 3), Effect.Mark, AttackStyle.Ranged, "c5", null),
            new EquipmentBase("c11", "屏蔽网背心", Slot.Coat, 1, new Stats(29.9f, 1.1f, 3.4f), Effect.Guard, AttackStyle.Ranged, "c6", null),
            new EquipmentBase("c12", "机房防静电服", Slot.Coat, 2, new Stats(36.8f, 0f, 4.6f), Effect.None, AttackStyle.Ranged, "c7", null),
            new EquipmentBase("c13", "天线维修夹克", Slot.Coat, 3, new Stats(23, 2.3f, 2.3f, 0.03f), Effect.Arc, AttackStyle.Ranged, "c8", null),
            new EquipmentBase("c14", "断讯斗篷", Slot.Coat, 4, new Stats(23, 1.1f, 2.3f, 0.04f, 0.02f), Effect.None, AttackStyle.Ranged, "c9", null),
            new EquipmentBase("b10", "折叠雷达碟", Slot.Back, 0, new Stats(23, 0f, 4.6f), Effect.Mark, AttackStyle.Ranged, "b5", null),
            new EquipmentBase("b11", "中继电池箱", Slot.Back, 1, new Stats(13.8f, 1.1f, 1.1f, 0.04f), Effect.Arc, AttackStyle.Ranged, "b6", null),
            new EquipmentBase("b12", "天线束", Slot.Back, 2, new Stats(11.5f, 1.1f, 0f, 0f, 0f, 2), Effect.Drone, AttackStyle.Ranged, "b7", null),
            new EquipmentBase("b13", "干扰无人机舱", Slot.Back, 3, new Stats(11.5f, 1.1f, 0f, 0.03f, 0f, 2), Effect.Drone, AttackStyle.Ranged, "b8", null),
            new EquipmentBase("b14", "屏蔽伞", Slot.Back, 4, new Stats(18.4f, 0f, 2.3f, 0f, 0f, 3), Effect.Guard, AttackStyle.Ranged, "b9", null),
            new EquipmentBase("l10", "屋顶维修裤", Slot.Legs, 0, new Stats(23, 0f, 3.4f, 0f, 0f, 2), Effect.None, AttackStyle.Ranged, "l5", null),
            new EquipmentBase("l11", "防静电工装裤", Slot.Legs, 1, new Stats(29.9f, 0f, 4.6f), Effect.None, AttackStyle.Ranged, "l6", null),
            new EquipmentBase("l12", "绝缘护腿", Slot.Legs, 2, new Stats(25.3f, 0f, 3.4f, 0.02f), Effect.None, AttackStyle.Ranged, "l7", null),
            new EquipmentBase("l13", "天线兵马裤", Slot.Legs, 3, new Stats(20.7f, 1.1f, 2.3f, 0.03f), Effect.None, AttackStyle.Ranged, "l8", null),
            new EquipmentBase("l14", "中继护膝", Slot.Legs, 4, new Stats(18.4f, 1.1f, 2.3f, 0f, 0.02f, 2), Effect.None, AttackStyle.Ranged, "l9", null),
            // ---- v0.18 第四章 25 件（美术未到：外观借 art 指定的旧装备，武器招式借 mech 指定的旧武器）----
            new EquipmentBase("w15", "镜刃", Slot.Weapon, 0, new Stats(0f, 11.7f, 0f, -0.02f), Effect.Execute, AttackStyle.Melee, null, "w1"),   // v0.20 镜刃招式用匕首的迅切连击（卡27 起用自己的图）
            new EquipmentBase("w16", "折光步枪", Slot.Weapon, 1, new Stats(0f, 7.8f, 0f, 0.06f), Effect.Mark, AttackStyle.Ranged, null, "w0"),   // v0.20 折光步枪招式用贴脸轰击（卡27 起用自己的图）
            new EquipmentBase("w17", "审计手炮", Slot.Weapon, 2, new Stats(10.4f, 11.7f, 1.3f), Effect.Leech, AttackStyle.Ranged, null, "w3"),
            new EquipmentBase("w18", "冷却重锤", Slot.Weapon, 3, new Stats(0f, 7.8f, 0f, 0.1f, 0.02f), Effect.None, AttackStyle.Melee, null, "w4"),
            new EquipmentBase("w19", "光纤钩索", Slot.Weapon, 4, new Stats(0f, 10.4f, 0f, 0f, 0.04f), Effect.Leech, AttackStyle.Melee, null, "w7"),
            new EquipmentBase("h15", "镜面面罩", Slot.Head, 0, new Stats(15.6f, 0f, 2.6f, 0f, 0f, 2), Effect.None, AttackStyle.Ranged, "h0", null),
            new EquipmentBase("h16", "审计耳机", Slot.Head, 1, new Stats(7.8f, 1.3f, 1.3f, 0.05f), Effect.Mark, AttackStyle.Ranged, "h1", null),
            new EquipmentBase("h17", "冷光头盔", Slot.Head, 2, new Stats(26, 0f, 5.2f), Effect.Guard, AttackStyle.Ranged, "h2", null),
            new EquipmentBase("h18", "晶体护目镜", Slot.Head, 3, new Stats(20.8f, 0f, 6.5f), Effect.Mark, AttackStyle.Ranged, "h3", null),
            new EquipmentBase("h19", "上城礼帽", Slot.Head, 4, new Stats(10.4f, 1.3f, 0f, 0f, 0.04f, 2), Effect.None, AttackStyle.Ranged, "h4", null),
            new EquipmentBase("c15", "镜面风衣", Slot.Coat, 0, new Stats(28.6f, 0f, 2.6f, 0f, 0f, 3), Effect.None, AttackStyle.Ranged, "c0", null),
            new EquipmentBase("c16", "审计制服", Slot.Coat, 1, new Stats(33.8f, 1.3f, 3.9f), Effect.Guard, AttackStyle.Ranged, "c1", null),
            new EquipmentBase("c17", "冷却服", Slot.Coat, 2, new Stats(41.6f, 0f, 5.2f), Effect.Leech, AttackStyle.Ranged, "c2", null),
            new EquipmentBase("c18", "白金防弹衣", Slot.Coat, 3, new Stats(26, 2.6f, 2.6f, 0.03f), Effect.Guard, AttackStyle.Ranged, "c3", null),
            new EquipmentBase("c19", "玻璃斗篷", Slot.Coat, 4, new Stats(26, 1.3f, 2.6f, 0.04f, 0.02f), Effect.Mark, AttackStyle.Ranged, "c4", null),
            new EquipmentBase("b15", "镜面无人机舱", Slot.Back, 0, new Stats(26, 0f, 5.2f), Effect.Drone, AttackStyle.Ranged, "b0", null),
            new EquipmentBase("b16", "冷却背罐", Slot.Back, 1, new Stats(15.6f, 1.3f, 1.3f, 0.04f), Effect.Guard, AttackStyle.Ranged, "b1", null),
            new EquipmentBase("b17", "折光翼", Slot.Back, 2, new Stats(13, 1.3f, 0f, 0f, 0f, 2), Effect.Thruster, AttackStyle.Ranged, "b2", null),
            new EquipmentBase("b18", "审计数据箱", Slot.Back, 3, new Stats(13, 1.3f, 0f, 0.03f, 0f, 2), Effect.Arc, AttackStyle.Ranged, "b3", null),
            new EquipmentBase("b19", "晶格推进器", Slot.Back, 4, new Stats(20.8f, 0f, 2.6f, 0f, 0f, 3), Effect.Thruster, AttackStyle.Ranged, "b4", null),
            new EquipmentBase("l15", "镜面护腿", Slot.Legs, 0, new Stats(26, 0f, 3.9f, 0f, 0f, 2), Effect.None, AttackStyle.Ranged, "l0", null),
            new EquipmentBase("l16", "制服长裤", Slot.Legs, 1, new Stats(33.8f, 0f, 5.2f), Effect.None, AttackStyle.Ranged, "l1", null),
            new EquipmentBase("l17", "冷却腿甲", Slot.Legs, 2, new Stats(28.6f, 0f, 3.9f, 0.02f), Effect.None, AttackStyle.Ranged, "l2", null),
            new EquipmentBase("l18", "白金战术裤", Slot.Legs, 3, new Stats(23.4f, 1.3f, 2.6f, 0.03f), Effect.None, AttackStyle.Ranged, "l3", null),
            new EquipmentBase("l19", "玻璃纤维靴裤", Slot.Legs, 4, new Stats(20.8f, 1.3f, 2.6f, 0f, 0.02f, 2), Effect.None, AttackStyle.Ranged, "l4", null),
            // ---- v0.18 第五章 25 件（美术未到：外观借 art 指定的旧装备，武器招式借 mech 指定的旧武器）----
            new EquipmentBase("w20", "织针刺剑", Slot.Weapon, 0, new Stats(0f, 13, 0f, -0.02f), Effect.Execute, AttackStyle.Melee, null, "w8"),
            new EquipmentBase("w21", "线缆鞭", Slot.Weapon, 1, new Stats(0f, 8.7f, 0f, 0.06f), Effect.Leech, AttackStyle.Melee, null, "w7"),
            new EquipmentBase("w22", "协议发射器", Slot.Weapon, 2, new Stats(11.6f, 13, 1.4f), Effect.Arc, AttackStyle.Ranged, null, "w2"),
            new EquipmentBase("w23", "母线重锤", Slot.Weapon, 3, new Stats(0f, 8.7f, 0f, 0.1f, 0.02f), Effect.None, AttackStyle.Melee, null, "w4"),
            new EquipmentBase("w24", "零号霰枪", Slot.Weapon, 4, new Stats(0f, 11.6f, 0f, 0f, 0.04f), Effect.None, AttackStyle.Ranged, null, "w0"),
            new EquipmentBase("h20", "织网面纱", Slot.Head, 0, new Stats(17.4f, 0f, 2.9f, 0f, 0f, 2), Effect.Mark, AttackStyle.Ranged, "h5", null),
            new EquipmentBase("h21", "协议头环", Slot.Head, 1, new Stats(8.7f, 1.4f, 1.4f, 0.05f), Effect.None, AttackStyle.Ranged, "h6", null),
            new EquipmentBase("h22", "零号目镜", Slot.Head, 2, new Stats(29, 0f, 5.8f), Effect.Mark, AttackStyle.Ranged, "h7", null),
            new EquipmentBase("h23", "线缆头盔", Slot.Head, 3, new Stats(23.2f, 0f, 7.2f), Effect.Guard, AttackStyle.Ranged, "h8", null),
            new EquipmentBase("h24", "母体冠", Slot.Head, 4, new Stats(11.6f, 1.4f, 0f, 0f, 0.04f, 2), Effect.None, AttackStyle.Ranged, "h9", null),
            new EquipmentBase("c20", "织网长袍", Slot.Coat, 0, new Stats(31.9f, 0f, 2.9f, 0f, 0f, 3), Effect.Arc, AttackStyle.Ranged, "c5", null),
            new EquipmentBase("c21", "协议外骨骼", Slot.Coat, 1, new Stats(37.7f, 1.4f, 4.3f), Effect.Guard, AttackStyle.Ranged, "c6", null),
            new EquipmentBase("c22", "线缆编织甲", Slot.Coat, 2, new Stats(46.4f, 0f, 5.8f), Effect.None, AttackStyle.Ranged, "c7", null),
            new EquipmentBase("c23", "零号战衣", Slot.Coat, 3, new Stats(29, 2.9f, 2.9f, 0.03f), Effect.Leech, AttackStyle.Ranged, "c8", null),
            new EquipmentBase("c24", "回收员终装", Slot.Coat, 4, new Stats(29, 1.4f, 2.9f, 0.04f, 0.02f), Effect.None, AttackStyle.Ranged, "c9", null),
            new EquipmentBase("b20", "织网纺锤", Slot.Back, 0, new Stats(29, 0f, 5.8f), Effect.Drone, AttackStyle.Ranged, "b5", null),
            new EquipmentBase("b21", "母线线轴", Slot.Back, 1, new Stats(17.4f, 1.4f, 1.4f, 0.04f), Effect.Thruster, AttackStyle.Ranged, "b6", null),
            new EquipmentBase("b22", "协议无人机", Slot.Back, 2, new Stats(14.5f, 1.4f, 0f, 0f, 0f, 2), Effect.Drone, AttackStyle.Ranged, "b7", null),
            new EquipmentBase("b23", "零号核心包", Slot.Back, 3, new Stats(14.5f, 1.4f, 0f, 0.03f, 0f, 2), Effect.Arc, AttackStyle.Ranged, "b8", null),
            new EquipmentBase("b24", "织网之翼", Slot.Back, 4, new Stats(23.2f, 0f, 2.9f, 0f, 0f, 3), Effect.Thruster, AttackStyle.Ranged, "b9", null),
            new EquipmentBase("l20", "织网绑腿", Slot.Legs, 0, new Stats(29, 0f, 4.3f, 0f, 0f, 2), Effect.None, AttackStyle.Ranged, "l5", null),
            new EquipmentBase("l21", "协议护胫", Slot.Legs, 1, new Stats(37.7f, 0f, 5.8f), Effect.None, AttackStyle.Ranged, "l6", null),
            new EquipmentBase("l22", "线缆护腿", Slot.Legs, 2, new Stats(31.9f, 0f, 4.3f, 0.02f), Effect.None, AttackStyle.Ranged, "l7", null),
            new EquipmentBase("l23", "零号战裤", Slot.Legs, 3, new Stats(26.1f, 1.4f, 2.9f, 0.03f), Effect.None, AttackStyle.Ranged, "l8", null),
            new EquipmentBase("l24", "终装长靴", Slot.Legs, 4, new Stats(23.2f, 1.4f, 2.9f, 0f, 0.02f, 2), Effect.None, AttackStyle.Ranged, "l9", null)
        };
        public static EquipmentBase Find(string id) { return All.FirstOrDefault(x => x.id == id); }
        // v0.16 掉落池：整章通用——第一章任何一关都能掉全章 16 件（初始的 4 件不掉）。
        // 首领关额外必掉专属件；以后每章一个池子，按章节切换。
        public static readonly string[] StarterIds = { "w1", "h0", "c0", "b0", "l0" };
        // 首领必掉的专属件（平时也能掉）：闸门看守 → 逆流手炮，总调度吊臂 → 吊臂钩索。
        // 首领专属件：闸门看守 → 逆流手炮，总调度吊臂 → 吊臂钩索，断讯监视者 → 雷达碟炮，镜面审计官 → 审计手炮，织网母体 → 协议发射器。
        private static readonly string[] BossExclusive = { "w3", "w7", "w12", "w17", "w22" };
        public static string BossExclusiveOf(int chapter) { return BossExclusive[Math.Max(0, Math.Min(BossExclusive.Length - 1, chapter - 1))]; }
        /// 有自己一套装备的章节数（v0.18 起五章都有，第三～五章的外观先借旧装备）。之后的章节掉前面所有章节的装备。
        public const int GearChapters = 5;
        /// 画这件装备用哪件的图（美术未到的借旧装备）；武器招式和技能用哪把旧武器的。
        public static string ArtOf(string id) { EquipmentBase b = Find(id); return b != null && b.art != null ? b.art : id; }
        public static string MechOf(string id) { EquipmentBase b = Find(id); return b != null && b.mech != null ? b.mech : id; }
        public static bool IsStarter(string id) { return System.Array.IndexOf(StarterIds, id) >= 0; }
        /// 装备属于第几章：编号 0–4 第一章，5–9 第二章……
        public static int ChapterOf(string id) { int n; return id != null && id.Length > 1 && int.TryParse(id.Substring(1), out n) ? n / 5 + 1 : 1; }
        /// 会不会掉：主线每章只掉本章的装备（整章通用）；回声层掉已经到过的章节（chapter 传最前面那一章）。初始的 4 件都不掉。
        /// v0.20 原来回声层掉所有章节——打完 1-7 就能进回声层，第二章去刷一会就能刷到第五章首领的专属武器，后面几章的掉落全剧透了。
        public static bool DropsIn(string id, bool echo, int chapter) { return Find(id) != null && !IsStarter(id) && (echo ? ChapterOf(id) <= chapter : ChapterOf(id) == chapter || chapter > GearChapters); }
        private static readonly Dictionary<int, string[]> pools = new Dictionary<int, string[]>();
        public static string[] DropPool(bool echo, int chapter)
        {
            int key = echo ? -chapter : chapter; string[] p;
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
        /// 效果的短名（改装件、面板用）：电弧、汲取……
        public static string EffectShort(Effect e) { string full = EffectName(e); int cut = full.IndexOf('：'); return cut > 0 ? full.Substring(0, cut) : full; }

        // ---------- v0.21 芯片 ----------
        public const int ChipKinds = 6;
        public static readonly string[] ChipNames = { "过载", "装甲", "调度", "汲取", "缓冲", "聚焦" };
        public static readonly string[] ChipStatNames = { "攻击", "生命", "技能冷却", "伤害回血", "受到伤害", "单体伤害" };
        private static readonly float[,] ChipValues = { { .04f, .07f, .10f }, { .06f, .10f, .14f }, { .05f, .08f, .12f }, { .006f, .010f, .015f }, { .03f, .05f, .07f }, { .06f, .10f, .15f } };
        public static readonly float[] ChipCaps = { .6f, .8f, .4f, .06f, .35f, 1f };
        /// 芯片在这件装备上的数值：按 Mk 等级取基础值，再随装备等级涨（和特殊词条同一条成长线：每级 +1%）。
        public static float ChipValue(Chip c, int level) { return ChipValues[Math.Max(0, Math.Min(ChipKinds - 1, c.kind)), Math.Max(1, Math.Min(3, c.tier)) - 1] * Balance.SpecialLevelFactor(level); }
        public static string ChipLabel(Chip c) { return ChipNames[Math.Max(0, Math.Min(ChipKinds - 1, c.kind))] + " Mk" + Math.Max(1, Math.Min(3, c.tier)); }
        /// 「攻击 +7.4%」「技能冷却 −8.0%」「受到伤害 −5.0%」
        public static string ChipEffectText(Chip c, int level)
        {
            float v = ChipValue(c, level) * 100; bool minus = c.kind == 2 || c.kind == 4;
            return ChipStatNames[Math.Max(0, Math.Min(ChipKinds - 1, c.kind))] + (minus ? " −" : " +") + v.ToString(v < 10 ? "0.0" : "0") + "%";
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
                    if (a == null || a.stat < 0 || a.stat >= Balance.AffixStats || float.IsNaN(a.value) || float.IsInfinity(a.value)
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
            // v0.20 修：训练等级读档时原来夹在 20（早期的训练上限），现在上限是 Balance.TrainingCap（9999），
            //       练过 20 级的存档每次打开游戏都会被打回 20 级（数值模拟打到 5-7 时训练 110～150 级）。保底计数也改成跟常量走。
            state.scrap = Math.Max(0, state.scrap); state.training = Math.Max(0, Math.Min(Balance.TrainingCap, state.training));
            state.sinceRare = Math.Max(0, Math.Min(Balance.RarePity, state.sinceRare));
            state.sinceEpic = Math.Max(0, Math.Min(Balance.EpicPity, state.sinceEpic));
            state.sinceLegendary = Math.Max(0, Math.Min(Balance.LegendaryPity, state.sinceLegendary));
            if (Catalog.Find(state.recoveryTarget) == null) { state.recoveryTarget = null; state.targetBossMisses = 0; }
            state.targetBossMisses = Math.Max(0, Math.Min(7, state.targetBossMisses));
            if (state.tipsVersion < 1)
            {   // v0.20 新手提示上线前的存档：已经走过的阶段对应的提示直接记成看过，老玩家不会突然弹一串入门提示
                if (state.unlockedRegion >= 1 || state.kills >= 30) state.tips |= 1 << 0 | 1 << 1;   // 刚进游戏、第一次掉装备
                if (state.unlockedRegion >= 3) state.tips |= 1 << 2 | 1 << 3;                         // 第一次被打倒、第一次进精英关
                if (state.bosses > 0) state.tips |= 1 << 4;                                           // 回声层开放
                state.tipsVersion = 1;
            }
            state.glowMode = Math.Max(0, Math.Min(2, state.glowMode));
            state.shakeMode = Math.Max(0, Math.Min(2, state.shakeMode));
            state.autoSalvage = Math.Max(0, Math.Min(2, state.autoSalvage));
            if (state.parts == null || state.parts.Length != 4) state.parts = new int[4];
            // v0.16 统一货币：四种部位零件并入「零件」（scrap）。老存档里攒的部位零件全部折进来。
            for (int i = 0; i < 4; i++) { state.scrap += Math.Max(0, state.parts[i]); state.parts[i] = 0; }
            state.cores = Math.Max(0, state.cores);
            // v0.21 芯片与改装件
            if (state.chipBag == null) state.chipBag = new List<Chip>();
            state.chipBag = state.chipBag.Where(c => c != null && c.kind >= 0 && c.kind < Catalog.ChipKinds && c.tier >= 1 && c.tier <= 3).ToList();
            if (state.modBag == null || state.modBag.Length != 8) { var mb = new int[8]; if (state.modBag != null) Array.Copy(state.modBag, mb, Math.Min(8, state.modBag.Length)); state.modBag = mb; }
            for (int i = 0; i < 8; i++) state.modBag[i] = Math.Max(0, state.modBag[i]);
            foreach (Item it in state.items)
            {
                if (it.chips == null) it.chips = new List<Chip>();
                it.chips = it.chips.Where(c => c != null && c.kind >= 0 && c.kind < Catalog.ChipKinds && c.tier >= 1 && c.tier <= 3).Take(Balance.ChipSlots[(int)it.quality]).ToList();
                if (it.mod < 0 || it.mod > 7) it.mod = 0;
            }
            if (state.implants == null) state.implants = new List<string>();
            if (state.anomalies == null) state.anomalies = new List<string>();
            state.anomalies = state.anomalies.Where(id => Chapters.FindAnomaly(id) != null).Distinct().Take(Chapters.MaxAnomalies).ToList();
            state.echoFloor = Math.Max(0, state.echoFloor); state.echoBest = Math.Max(state.echoBest, state.echoFloor);
            state.implants = state.implants.Where(id => Catalog.FindImplant(id) != null).Distinct().ToList();
            if (state.implantLevels == null || state.implantLevels.Length != Catalog.AscensionTotal)
            { var lv = new int[Catalog.AscensionTotal]; if (state.implantLevels != null) Array.Copy(state.implantLevels, lv, Math.Min(lv.Length, state.implantLevels.Length)); state.implantLevels = lv; }
            state.shards = Math.Max(0, state.shards);
            // 老存档：已经打过第一章首领（目前唯一的首领）但还没有义体，补上绞盘左臂。
            // 老存档（还没有 bossMask）：打过首领 = 打过第一章首领，补上绞盘机械臂并开放 2-1。
            // 有了 bossMask 以后按章判断，不再因为打过别的首领误补手臂（之前测试存档只打 2-7 也会被补上手臂）。
            if (state.bossMask == 0 && state.bosses > 0) state.bossMask = 1;
            if ((state.bossMask & 1) != 0)
            {
                if (!state.implants.Contains("arm_l")) state.implants.Add("arm_l");
                state.unlockedRegion = Math.Max(state.unlockedRegion, Chapters.FirstRegion(2));
            }
            if ((state.bossMask & 2) != 0)
            {
                if (!state.implants.Contains("eye_r")) state.implants.Add("eye_r");
                state.unlockedRegion = Math.Max(state.unlockedRegion, Chapters.FirstRegion(3));   // v0.18：打过 2-7 的老存档补开 3-1
            }
            if ((state.bossMask & 4) != 0)
            {
                if (!state.implants.Contains("spine")) state.implants.Add("spine");
                state.unlockedRegion = Math.Max(state.unlockedRegion, Chapters.FirstRegion(4));
            }
            if ((state.bossMask & 8) != 0)
            {
                if (!state.implants.Contains("heart")) state.implants.Add("heart");
                state.unlockedRegion = Math.Max(state.unlockedRegion, Chapters.FirstRegion(5));
            }
            if ((state.bossMask & 16) != 0 && !state.implants.Contains("port")) state.implants.Add("port");
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
            EquipmentBase worn = item == null ? null : Catalog.Find(item.baseId);
            // v0.20 武器外观只套在同类武器上（近战 / 远程），不然动作对不上（拿着枪做砸地）。存着的外观换回同类武器时自动生效。
            if (look != null && slot == Slot.Weapon && worn != null && look.attackStyle != worn.attackStyle) look = null;
            return look ?? worn ?? Catalog.All.First(x => x.slot == slot);
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
            List<int> pool = AffixPool(quality, null);
            for (int i = 0; i < tier.affixCount && pool.Count > 0; i++)
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
                    case 4: s.crit += a.value; break; case 5: s.luck += a.value; break;
                    case 6: s.critDamage += a.value; break; case 7: s.effectDamage += a.value; break;
                    case 8: s.bossDamage += a.value; break; case 9: s.scrapBonus += a.value; break; }
            }
            return s;
        }
        /// 测试用（F6）：攻击、生命 ×10，并且不会被打倒。不写进存档。
        public static bool TestPower;
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetTestPower() { TestPower = false; }
        private static Stats Scaled(Stats a, float k)
        {
            var c = new Stats(a.health * k, a.attack * k, a.defense * k, a.haste * k, a.crit * k, a.luck * k);
            c.critDamage = a.critDamage * k; c.effectDamage = a.effectDamage * k; c.bossDamage = a.bossDamage * k; c.scrapBonus = a.scrapBonus * k; return c;
        }
        // ---------- v0.20 机械飞升：义体升级 ----------
        /// 第 i 件义体的等级（没装上 = 0，装上至少 1 级）。
        public int ImplantLevel(int i)
        {
            if (i < 0 || i >= Catalog.Implants.Length || !state.implants.Contains(Catalog.Implants[i].id)) return 0;
            return Math.Max(1, Math.Min(Catalog.ImplantMaxLevel, state.implantLevels != null && i < state.implantLevels.Length ? state.implantLevels[i] : 1));
        }
        public bool CanUpgradeImplant(int i)
        {
            int lv = ImplantLevel(i);
            return lv > 0 && lv < Catalog.ImplantMaxLevel && state.shards >= Catalog.ImplantShardCost(lv) && state.scrap >= Catalog.ImplantScrapCost(lv);
        }
        public bool UpgradeImplant(int i)
        {
            if (!CanUpgradeImplant(i)) return false;
            int lv = ImplantLevel(i);
            state.shards -= Catalog.ImplantShardCost(lv); state.scrap -= Catalog.ImplantScrapCost(lv);
            state.implantLevels[i] = lv + 1; return true;
        }
        /// 飞升度 0..1 = 5 件义体等级之和 ÷ 50。主线打完时约 10%。
        /// 现在到了第几个飞升阶段（0 = 还没到 30%）。
        public int AscensionStage { get { int pct = (int)Math.Round(Ascension * 100), n = 0; foreach (int at in Catalog.AscensionStageAt) if (pct >= at) n++; return n; } }
        public float Ascension { get { int sum = 0; for (int i = 0; i < Catalog.Implants.Length; i++) sum += ImplantLevel(i); return sum / (float)(Catalog.Implants.Length * Catalog.ImplantMaxLevel); } }
        public Stats Total()
        {
            Stats s = new Stats(Balance.BaseHealth + state.training * Balance.TrainingHealth, Balance.BaseAttack + state.training * Balance.TrainingAttack, Balance.BaseDefense, 0, Balance.BaseCrit);
            for(int i = 0; i < Catalog.SlotCount; i++) { Item item = Equipped((Slot)i); if (item != null) { s.Add(ItemStats(item)); AddChips(s, item); } }
            foreach (string id in state.implants)
            {   // v0.20 义体按等级放大；5 / 10 级的特性另加
                ImplantDef d = Catalog.FindImplant(id); if (d == null) continue;
                int lv = ImplantLevel(Catalog.ImplantIndex(id));
                s.Add(Scaled(d.stats, 1 + Catalog.ImplantLevelGain * (lv - 1)));
                if (lv >= 5 && d.trait5 != null) s.Add(d.trait5);
                if (lv >= 10 && d.trait10 != null) s.Add(d.trait10);
            }
            s.attackPct = Math.Min(Catalog.ChipCaps[0], s.attackPct); s.healthPct = Math.Min(Catalog.ChipCaps[1], s.healthPct);
            s.cooldown = Math.Min(Catalog.ChipCaps[2], s.cooldown); s.leech = Math.Min(Catalog.ChipCaps[3], s.leech);
            s.reduction = Math.Min(Balance.ReductionCap, s.reduction); s.focus = Math.Min(Catalog.ChipCaps[5], s.focus);
            s.attack *= 1 + s.attackPct; s.health *= 1 + s.healthPct;   // v0.21 芯片的攻击% / 生命% 乘在所有加成之后
            if (TestPower) { s.attack *= 10; s.health *= 10; }
            s.haste = Math.Max(-.5f, Math.Min(Balance.HasteCap, s.haste)); s.crit = Math.Max(0, Math.Min(Balance.CritCap, s.crit)); s.luck = Math.Max(0, Math.Min(Balance.LuckCap, s.luck));
            s.critDamage = Math.Min(Balance.CritDamageCap, s.critDamage); s.effectDamage = Math.Min(Balance.EffectDamageCap, s.effectDamage);
            s.bossDamage = Math.Min(Balance.BossDamageCap, s.bossDamage); s.scrapBonus = Math.Min(Balance.ScrapBonusCap, s.scrapBonus);
            return s;
        }
        /// 效果份数 = 穿着的装备自带的 + 改装件装上的，同一种最多算 2 份（v0.21 改装件让外观和效果脱钩）。
        public int EffectCount(Effect effect)
        {
            int n = 0;
            for (int i = 0; i < Catalog.SlotCount; i++)
            {
                Item it = Equipped((Slot)i); if (it == null) continue;
                if (Catalog.Find(it.baseId).effect == effect) n++;
                if (it.mod == (int)effect && effect != Effect.None) n++;
            }
            return Math.Min(2, n);
        }
        private static void AddChips(Stats s, Item item)
        {
            if (item.chips == null) return;
            foreach (Chip c in item.chips)
            {
                float v = Catalog.ChipValue(c, item.level);
                switch (c.kind) { case 0: s.attackPct += v; break; case 1: s.healthPct += v; break; case 2: s.cooldown += v; break;
                    case 3: s.leech += v; break; case 4: s.reduction += v; break; default: s.focus += v; break; }
            }
        }

        // ---------- v0.21 芯片与改装件（第五章开放） ----------
        public bool ChipsUnlocked { get { return state.unlockedRegion >= Chapters.FirstRegion(5); } }
        public int ChipSlotsOf(Item item) { return item == null ? 0 : Balance.ChipSlots[(int)item.quality]; }
        /// 掉一块芯片进库存：深网 100 层起 Mk3，其余 Mk2。
        public Chip NewChip(int tier = 0)
        {
            var c = new Chip { kind = rng.Next(Catalog.ChipKinds), tier = tier > 0 ? tier : InEcho && state.echoFloor >= 100 ? 3 : 2 };
            state.chipBag.Add(c); return c;
        }
        /// 植入：往第 slot 个槽里放库存里第 bag 块芯片。槽里原来有芯片的会被销毁（芯片不能拆）。
        public bool InsertChip(Item item, int slot, int bag)
        {
            if (!ChipsUnlocked || item == null || slot < 0 || slot >= ChipSlotsOf(item) || bag < 0 || bag >= state.chipBag.Count) return false;
            if (item.chips == null) item.chips = new List<Chip>();
            Chip c = state.chipBag[bag]; state.chipBag.RemoveAt(bag);
            if (slot < item.chips.Count) item.chips[slot] = c; else item.chips.Add(c);
            return true;
        }
        /// 芯片掉率倍率（改造板「芯片嗅探」×1.5，做改造板时接上）。
        public float ChipFindBonus { get { return 1; } }
        public bool CanCraftMod { get { return ChipsUnlocked && state.cores >= Balance.ModCraftCores; } }
        public bool CraftMod(Effect kind)
        {
            if (!CanCraftMod || kind == Effect.None) return false;
            state.cores -= Balance.ModCraftCores; state.modBag[(int)kind]++; return true;
        }
        /// 装改装件：换下来的回到库存（改装件可以随时换）。kind = None 是卸下。
        public bool InstallMod(Item item, Effect kind)
        {
            if (!ChipsUnlocked || item == null) return false;
            if (kind != Effect.None && state.modBag[(int)kind] <= 0) return false;
            if (item.mod != 0) state.modBag[item.mod]++;
            item.mod = (int)kind; if (kind != Effect.None) state.modBag[(int)kind]--;
            return true;
        }
        public int ModCount { get { int n = 0; for (int i = 1; i < state.modBag.Length; i++) n += state.modBag[i]; return n; } }
        /// 第一次到第五章：送一块过载 Mk1 和一件标记改装件，让玩家知道有这个系统。
        public bool GiftChips()
        {
            if (!ChipsUnlocked || state.chipGift) return false;
            state.chipGift = true; state.chipBag.Add(new Chip { kind = 0, tier = 1 }); state.modBag[(int)Effect.Mark]++; return true;
        }
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
        { get { return !string.IsNullOrEmpty(state.recoveryTarget) && Catalog.DropsIn(state.recoveryTarget, InEcho, DropChapter); } }
        public bool Equip(string uid)
        {
            Item item = state.items.FirstOrDefault(x => x.uid == uid);
            if (item == null) return false;
            state.equipped[(int)Catalog.Find(item.baseId).slot] = uid;
            return true;
        }
        /// 这款外观现在能不能用：武器外观要和身上的武器同类（近战 / 远程）。
        public bool LookFits(EquipmentBase look)
        {
            if (look == null || look.slot != Slot.Weapon) return true;
            Item item = Equipped(Slot.Weapon); EquipmentBase worn = item == null ? null : Catalog.Find(item.baseId);
            return worn == null || worn.attackStyle == look.attackStyle;
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
            if (item.mod > 0 && item.mod < state.modBag.Length) state.modBag[item.mod]++;   // v0.21 改装件退回库存（芯片随装备销毁）
            state.items.Remove(item); return true;
        }
        public static int SalvageValue(Item item) { return Math.Max(1, (int)Math.Round((2 + (int)item.quality * 3) * item.level * Balance.SalvageScale)); }
        // 许可等级：装备升级的上限。击败一关首领解锁下一关时提高。
        // 许可等级（装备升级上限）：主线按关卡，最高 30；打通第一章后放开到装备等级上限，靠零件慢慢升。
        public int License { get { return EchoUnlocked ? Balance.MaxItemLevel : Math.Min(Balance.MaxLicense, (state.unlockedRegion + 1) * Balance.LicenseStep); } }
        // ---------- 回声层 ----------
        public bool InEcho { get { return state.echoFloor > 0; } }

        // ---------- v0.19 系统按章开放：每章都有新东西，开局不一下子摆满 ----------
        public bool RecalibrateUnlocked { get { return state.unlockedRegion >= Chapters.FirstRegion(2); } }   // 重校：进第二章
        public bool ReforgeUnlocked { get { return state.unlockedRegion >= Chapters.FirstRegion(3); } }       // 洗炼：进第三章
        public bool SpecialAffixesUnlocked { get { return state.unlockedRegion >= Chapters.FirstRegion(3); } } // 特殊词条：进第三章后才开始掉
        public bool AscendUnlocked { get { return state.unlockedRegion >= Chapters.FirstRegion(4); } }        // 突破：进第四章

        /// 战力：输出 × 生存的几何平均（“变强后再挑战”用，之后也可以显示在界面上）。
        public float Power()
        {
            Stats s = Total();
            float offense = s.attack * (1 + s.haste) * (1 + s.crit * (Balance.CritMultiplier - 1 + s.critDamage));
            float survive = s.health * (100 + Math.Max(0, s.defense)) / 100;
            return (float)Math.Sqrt(Math.Max(1, offense) * Math.Max(1, survive));
        }
        /// 被打倒 / 超时：记下倒在哪一关、这时的战力。之后退一关刷，变强了再回去。
        public void NoteDefeat()
        {
            if (InEcho) return;
            state.retryStreak = state.retryRegion == state.region ? state.retryStreak + 1 : 1;
            state.retryRegion = state.region; state.retryPower = Power(); state.retryFarms = 0;
        }
        /// 倒下以后去哪一关（不跨章）：在最前面那一关倒下退一关；在回刷的关偶尔倒一次就重打这一关，连着倒 3 次才再往下退
        /// （一倒就往下掉的话，会一路掉回章首，掉落等级跟着变低，越刷越慢——数值测试里有种子在第三章这样耗了 3 小时）。
        public int RetreatTarget()
        {
            int r = state.region;
            if (r < state.unlockedRegion && state.retryStreak < 3) return r;
            return FarmStage(r);
        }
        /// v0.20 回刷哪一关：从倒下那一关往前找最近的普通关（跳过 x-3 / x-6 精英和 x-7 首领），不跨章。
        /// 原来是“退一关”，倒在 x-4 就去刷 x-3 精英、倒在首领就去刷 x-6 精英——精英一次要打 3～4 分钟还容易倒，数值测试里有种子这样耗了好几个小时。
        public static int FarmStage(int fell)
        {
            int first = Chapters.FirstRegion(Chapters.ChapterOf(fell)), t = fell - 1;
            while (t > first && Chapters.IsWallStage(t)) t--;
            return Math.Max(first, t);
        }
        /// 正在倒下那一关的前面回刷（界面提示用）。
        public bool Farming { get { return !InEcho && state.retryRegion >= 0 && state.region < state.retryRegion; } }
        /// 过关后要不要往前走（只在开着自动推进、这一关不是最前面那一关时问）：在回刷的那一关（FarmStage）时，战力涨够 RetryGain 或刷满 RetryFarms 次才往前。
        /// 注意：每问一次算刷了一次，过一次关只能问一次（GameApp 和数值测试都是这样用的）。
        public bool CanAdvanceAfterClear()
        {
            if (InEcho || state.retryRegion < 0) return true;
            if (state.region >= state.retryRegion) { ClearRetry(); return true; }   // 打过了倒下的那一关
            if (state.region != FarmStage(state.retryRegion)) return true;          // 不在回刷的那一关（还没到 / 已经过了）就往前走
            state.retryFarms++;
            return Power() >= state.retryPower * (1 + Balance.RetryGain) || state.retryFarms >= Balance.RetryFarms;
        }
        public void ClearRetry() { state.retryRegion = -1; state.retryFarms = 0; state.retryPower = 0; state.retryStreak = 0; }
        /// 进回声层从第几层开始：最高层往回 2 层，但不低于主线进度对应的层数往回 6 层（打完第五章约从 41 层开始）。
        public int EchoStartFloor() { return Math.Max(1, Math.Max(state.echoBest - 2, Chapters.FloorForRegion(state.unlockedRegion) - 6)); }
        /// 从后面的层开始时，跳过的里程碑核心照样补上，最高层记到开始层的前一层。返回补了几个核心。
        public int CatchUpEcho(int start)
        {
            int gained = 0;
            for (int f = state.echoBest + 1; f < start; f++) if (f % Chapters.MilestoneEvery == 0) gained += Chapters.MilestoneCores;
            state.cores += gained; state.echoBest = Math.Max(state.echoBest, start - 1); return gained;
        }
        /// 当前主线所在章节（回声层里也按进入前的关卡算）。
        public int Chapter { get { return Chapters.ChapterOf(state.region); } }
        /// 掉落池按哪一章：主线 = 所在章；回声层 = 打到的最前面那一章（只掉到过的章节的装备）。
        public int DropChapter { get { return InEcho ? Chapters.ChapterOf(state.unlockedRegion) : Chapter; } }
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
            int from = item.level;
            item.level++;
            foreach (Affix a in item.affixes) a.value *= Balance.AffixFactor(a.stat, item.level) / Balance.AffixFactor(a.stat, from);
            return true;
        }
        // 重校：把一条随机词条换成这件装备上没有的另一种属性，并重新抽取数值。消耗零件，次数越多越贵。
        public int RerollCost(Item item) { return Balance.RerollCost(item.quality, item.rerolls); }
        public bool CanReroll(Item item, int index)
        { return RecalibrateUnlocked && item != null && index >= 0 && index < item.affixes.Count && item.affixes.Count < 6 && state.scrap >= RerollCost(item); }
        public bool Reroll(string uid, int index)
        {
            Item item = state.items.FirstOrDefault(x => x.uid == uid);
            if (!CanReroll(item, index)) return false;
            List<int> pool = AffixPool(item.quality, item);
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
        public bool CanAscend(Item item) { return AscendUnlocked && item != null && item.quality == Quality.Legendary && state.cores >= Balance.AscendCores && item.level >= AscendLevel; }
        /// 突破前要先把装备升到的等级。v0.19：许可打完第一个首领就放开到 300，原来“升到许可上限”变成升不到，改成 30 级。
        public int AscendLevel { get { return Math.Min(License, Balance.MaxLicense); } }
        public bool Ascend(string uid)
        {
            Item item = state.items.FirstOrDefault(x => x.uid == uid);
            if (!CanAscend(item)) return false;
            state.cores -= Balance.AscendCores; item.quality = Quality.Prototype;
            List<int> pool = AffixPool(item.quality, item);
            if (pool.Count > 0 && item.affixes.Count < Balance.Tier(Quality.Prototype).affixCount)
            {
                int chosen = pool[rng.Next(pool.Count)];
                item.affixes.Add(new Affix { stat = chosen, value = Balance.AffixValue(chosen, item.quality, item.level, (float)rng.NextDouble()) });
            }
            return true;
        }
        /// 能抽的词条：基础六种；「特装」及以上再加四种特殊词条。去掉这件装备上已经有的。
        public List<int> AffixPool(Quality quality, Item item)
        {
            var pool = new List<int>();
            int top = (int)quality >= (int)Quality.Rare && SpecialAffixesUnlocked ? Balance.AffixStats : Balance.BasicStats;
            for (int stat = 0; stat < top; stat++) if (item == null || !item.affixes.Any(a => a.stat == stat)) pool.Add(stat);
            return pool;
        }
        // ---------- v0.18 洗炼：锁住想留的词条，其余全部重抽 ----------
        public int LockedCount(Item item) { int n = 0; for (int i = 0; i < item.affixes.Count; i++) if ((item.affixLocks & (1 << i)) != 0) n++; return n; }
        public int ReforgeCost(Item item) { return Balance.ReforgeCost(item.quality, item.rerolls, LockedCount(item)); }
        public bool CanReforge(Item item) { return ReforgeUnlocked && item != null && item.affixes.Count > 0 && LockedCount(item) < item.affixes.Count && state.scrap >= ReforgeCost(item); }
        public void ToggleLock(Item item, int index) { if (item != null && index >= 0 && index < item.affixes.Count) item.affixLocks ^= 1 << index; }
        public bool Reforge(string uid)
        {
            Item item = state.items.FirstOrDefault(x => x.uid == uid);
            if (!CanReforge(item)) return false;
            state.scrap -= ReforgeCost(item);
            var keep = new List<int>();
            for (int i = 0; i < item.affixes.Count; i++) if ((item.affixLocks & (1 << i)) != 0) keep.Add(item.affixes[i].stat);
            var pool = AffixPool(item.quality, null).Where(st => !keep.Contains(st)).ToList();
            for (int i = 0; i < item.affixes.Count; i++)
            {
                if ((item.affixLocks & (1 << i)) != 0 || pool.Count == 0) continue;
                int p = rng.Next(pool.Count), stat = pool[p]; pool.RemoveAt(p);
                item.affixes[i].stat = stat;
                item.affixes[i].value = Balance.AffixValue(stat, item.quality, item.level, (float)rng.NextDouble());
            }
            item.rerolls++;
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
            string[] pool = Catalog.DropPool(InEcho, DropChapter);
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
