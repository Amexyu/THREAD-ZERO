using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// v0.16 第一章正式美术：分层视差背景（Resources/Art/V16/Stage）与三姿势敌人（Resources/Art/V16/Enemies）。
    /// 素材缺失时返回 false，调用方继续用旧的程序背景 / V07 敌人。
    public static class StageArt
    {
        private const string EnemyFolder = "Art/V16/Enemies/";
        private static readonly string[] StageFolders = { "Art/V16/Stage/", "Art/V16/Stage2/", "Art/V16/Stage3/", "Art/V16/Stage4/", "Art/V16/Stage5/" };   // 每章一个文件夹
        /// v0.33 亮色方向 B「晴天涂鸦」：V17 里有这个场景的图（_far）就整套用 V17（三层 + 动效配置 + 道具），没有就用 V16。
        /// 图是照 V16 每一层的位置重画的，所以 _fx.json 里动效的坐标照旧能用。Bright = false 全部回到 V16 的暗色版。
        private static readonly string[] BrightFolders = { "Art/V17/Stage/", "Art/V17/Stage2/", "Art/V17/Stage3/", "Art/V17/Stage4/", "Art/V17/Stage5/" };
        public static bool Bright = true;
        /// 这个场景实际用的文件夹（亮色有图就用亮色）。
        public static string FolderOf(int scene)
        {
            int ch = Mathf.Clamp(Mathf.Max(0, scene) / 4, 0, StageFolders.Length - 1);
            string key = SceneKeys[Mathf.Max(0, scene) % 4];
            if (Bright && Tex(BrightFolders[ch] + key + "_far") != null) return BrightFolders[ch];
            return StageFolders[ch];
        }
        public const float FarScroll = .25f, MidScroll = .6f;
        /// 测试用：设了就优先用这个文件夹里的第一章背景（比如 "Art/V16/StageProto/" 里的样板），不影响正式素材。
        /// v0.20 样板已挪到 Assets/ArtArchive/V16/StageProto（不进包），要用先挪回 Resources/Art/V16/。
        public static string ProtoFolder;
        private static readonly string[] SceneKeys = { "A", "B", "C", "D" };
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() { cache.Clear(); }
        private static Texture2D Tex(string path)
        {
            Texture2D t;
            if (!cache.TryGetValue(path, out t)) { t = Resources.Load<Texture2D>(path); if (t != null) t.wrapMode = TextureWrapMode.Repeat; cache[path] = t; }
            return t;
        }

        // ---------- 背景 ----------
        /// 画一个场景的三层背景。scene：0 A … 3 D（首领场地，远景不卷动）；4..7 是第二章的 A..D，8..11 第三章。groundY：角色脚下的地面线。
        public static bool Background(int scene, float worldX, float stageW, float stageH, float groundY)
        {
            scene = Mathf.Max(0, scene);
            string folder = FolderOf(scene);
            string key = SceneKeys[scene % 4];
            if (scene < 4 && !string.IsNullOrEmpty(ProtoFolder) && Tex(ProtoFolder + key + "_far") != null) folder = ProtoFolder;   // 第一章样板素材
            Texture2D far = Tex(folder + key + "_far"), mid = Tex(folder + key + "_mid"), ground = Tex(folder + key + "_ground");
            if (far == null) return false;
            StageFxConfig fx = StageFx.Config(folder + key);   // v0.17 背景动效（没有配置就不画）
            StageFx.Daylight = folder.StartsWith("Art/V17/");   // v0.33 亮色背景：动效换白天的配色
            float time = Time.unscaledTime;
            bool arena = key == "D";
            Fade.WorldX = worldX;   // 11-25「褪色之城」试验：颜色圈跟着每一层一起卷（Fade.On 关着时不起作用）
            if (arena) { Fade.LayerScroll = 0; StageFx.DrawCover(fx, folder, 0, Cover(far, stageW, stageH), time); }
            else
            {
                Fade.LayerScroll = FarScroll;
                Layer(far, worldX * FarScroll, stageH, 0);
                StageFx.Draw(fx, folder, 0, far, worldX * FarScroll, stageH, 0, time);
            }
            if (mid != null)
            {
                Fade.LayerScroll = MidScroll;
                Layer(mid, worldX * MidScroll, stageH, 0);
                StageFx.Draw(fx, folder, 1, mid, worldX * MidScroll, stageH, 0, time);
            }
            if (ground != null)
            {   // 地面带：图高的 20% 处是地面上沿，对齐到 groundY；下沿铺到舞台底部
                float h = (stageH - groundY) / .8f;
                Fade.LayerScroll = 1;
                Layer(ground, worldX, h, groundY - h * .2f);
                StageFx.Draw(fx, folder, 2, ground, worldX, h, groundY - h * .2f, time);
            }
            Fade.LayerScroll = 1;
            return true;
        }
        // 横向平铺的一层：按高度等比缩放，scroll 为已经乘过系数的卷动距离（逻辑像素）。
        private static void Layer(Texture2D t, float scroll, float h, float y)
        {
            float w = h * t.width / t.height;
            Rect uv = new Rect(scroll / w, 0, 1600 / w, 1);
            if (!Fade.Draw(new Rect(0, y, 1600, h), t, uv, GUI.color)) GUI.DrawTextureWithTexCoords(Ui.R(0, y, 1600, h), t, uv, true);
        }
        /// 铺满舞台的一张图（不卷动），返回它实际占的矩形（逻辑像素）。
        private static Rect Cover(Texture2D t, float w, float h)
        {
            float k = Mathf.Max(w / t.width, h / t.height), tw = t.width * k, th = t.height * k;
            if (!Fade.Draw(new Rect((w - tw) / 2, (h - th) / 2, tw, th), t, new Rect(0, 0, 1, 1), GUI.color))
                GUI.DrawTextureWithTexCoords(Ui.R((w - tw) / 2, (h - th) / 2, tw, th), t, new Rect(0, 0, 1, 1), true);
            return new Rect((w - tw) / 2, (h - th) / 2, tw, th);
        }

        // ---------- 敌人 ----------
        public enum Pose { Idle, Windup, Strike }
        /// 移动方式：用脚走（和角色同一套步伐）、轮子 / 履带（不颠，只有发动机震动）、飞行（悬停起伏）。
        public enum Gait { Legged, Wheeled, Flying }
        public static Gait GaitOf(Enemy e)
        {
            switch (ArtKey(e))   // 按实际画出来的图判断（第三章借第二章的图时，走法跟着图）
            {
                case "ranged": case "swarm2": case "elite_carrier": case "swarm3": case "boss_watcher":
                case "swarm4": case "elite_protocol": case "boss_weaver": return Gait.Flying;   // v0.40 回收机器人：电饭锅无人机、外卖箱无人机、吊扇运输机、小风扇蜻蜓、监视摄像头、粉饼盒飞弹、霓虹灯泡、缝纫机首领（线轴吊在下面）
                case "ranged2": case "boss_gatekeeper": case "boss_crane": case "ranged3": case "ranged4": case "ranged5": case "swarm5":
                case "elite_beacon": case "elite_relay": case "elite_prism": case "brute2": case "brute5": return Gait.Wheeled;   // 轮子 / 履带 / 平底滑行：旧电视（轮座）、路由器、香水瓶、购物车叉车、火锅推车、灯笼毛毛虫
                default: return Gait.Legged;   // 扫地机器狗、冰箱、洗衣机、快递柜、空调外机、双盾热水器、玻璃展柜、穿衣镜衣柜、收银机首领、毛衣机甲
            }
        }
        /// v0.40 倒下「关机打瞌睡」时 z 和小星星冒出来的位置：每张待机图最上面那一截的中心（占 512 画布的比例，x 往中间收 40%）。
        /// 数值由 Gemini/卡35 的切图结果量出来（idle 图 alpha > 60 的最上沿、上面 1/4 的横向中心）。
        private static readonly Dictionary<string, Vector2> HeadOf = new Dictionary<string, Vector2> { { "boss_auditor", new Vector2(0.552f, 0.051f) }, { "boss_crane", new Vector2(0.349f, 0.098f) }, { "boss_gatekeeper", new Vector2(0.543f, 0.182f) }, { "boss_watcher", new Vector2(0.509f, 0.053f) }, { "boss_weaver", new Vector2(0.492f, 0.047f) }, { "brute2", new Vector2(0.605f, 0.314f) }, { "brute3", new Vector2(0.573f, 0.242f) }, { "brute4", new Vector2(0.546f, 0.326f) }, { "brute5", new Vector2(0.537f, 0.242f) }, { "brute", new Vector2(0.524f, 0.172f) }, { "elite_beacon", new Vector2(0.490f, 0.121f) }, { "elite_carrier", new Vector2(0.469f, 0.406f) }, { "elite_crusher", new Vector2(0.616f, 0.186f) }, { "elite_hauler", new Vector2(0.508f, 0.072f) }, { "elite_mirror", new Vector2(0.502f, 0.207f) }, { "elite_prism", new Vector2(0.539f, 0.232f) }, { "elite_protocol", new Vector2(0.488f, 0.283f) }, { "elite_relay", new Vector2(0.526f, 0.229f) }, { "elite_shield", new Vector2(0.500f, 0.207f) }, { "elite_weave", new Vector2(0.457f, 0.246f) }, { "ranged2", new Vector2(0.518f, 0.232f) }, { "ranged3", new Vector2(0.424f, 0.232f) }, { "ranged4", new Vector2(0.458f, 0.232f) }, { "ranged5", new Vector2(0.569f, 0.232f) }, { "ranged", new Vector2(0.535f, 0.125f) }, { "swarm2", new Vector2(0.503f, 0.293f) }, { "swarm3", new Vector2(0.521f, 0.295f) }, { "swarm4", new Vector2(0.508f, 0.291f) }, { "swarm5", new Vector2(0.518f, 0.541f) }, { "swarm", new Vector2(0.663f, 0.109f) } };
        public static Vector2 HeadFrac(Enemy e) { Vector2 v; return HeadOf.TryGetValue(ArtKey(e), out v) ? new Vector2(Mathf.Lerp(v.x, .5f, .4f), v.y) : new Vector2(.5f, .3f); }
        /// 走路帧（可选）：有 _walk1 / _walk2 图就按步伐交替，没有就用待机图。
        public static bool HasWalk(Enemy e) { string k = ArtKey(e); return Tex(EnemyFolder + k + "_walk1") != null && Tex(EnemyFolder + k + "_walk2") != null; }
        public static string EnemyKey(Enemy e) { return KeyFor(e, e.skin); }
        // 每章一套怪图（skin = 章节 - 1）：首领、两种精英、重型、远程、冲锋。
        private static readonly string[][] Keys = {
            new[] { "boss_gatekeeper", "elite_crusher", "elite_beacon", "brute", "ranged", "swarm" },
            new[] { "boss_crane", "elite_hauler", "elite_carrier", "brute2", "ranged2", "swarm2" },
            new[] { "boss_watcher", "elite_shield", "elite_relay", "brute3", "ranged3", "swarm3" },   // 第三章：信号禁区（美术未到时用第二章的图调冷色）
            new[] { "boss_auditor", "elite_mirror", "elite_prism", "brute4", "ranged4", "swarm4" },   // 第四章：镜面上城（未到时借第一章的图调暖金）
            new[] { "boss_weaver", "elite_weave", "elite_protocol", "brute5", "ranged5", "swarm5" },  // 第五章：零号织网（未到时借第二章的图调青绿）
        };
        /// 这一章的图还没画时按什么顺序去借别章的图（借来的叠 BorrowedTint 区分章节）。
        private static readonly int[][] BorrowOrder = { new[] { 0 }, new[] { 1, 0 }, new[] { 2, 1, 0 }, new[] { 3, 0, 1, 2 }, new[] { 4, 2, 1, 0 } };
        private static string KeyFor(Enemy e, int skin)
        {
            string[] k = Keys[Mathf.Clamp(skin, 0, Keys.Length - 1)];
            if (e.boss) return k[0];
            switch (e.move)
            {
                case EnemyMove.Crusher: return k[1];
                case EnemyMove.Beacon: return k[2];
                case EnemyMove.Slam: return k[3];
                case EnemyMove.Bolt: return k[4];
                default: return k[5];
            }
        }
        /// 实际用哪张图：这一章的图还没画就往前一章找（第三章暂用第二章的图）。
        private static string ArtKey(Enemy e)
        {
            foreach (int skin in BorrowOrder[Mathf.Clamp(e.skin, 0, BorrowOrder.Length - 1)]) { string k = KeyFor(e, skin); if (Tex(EnemyFolder + k + "_idle") != null) return k; }
            return KeyFor(e, 0);
        }
        /// 借别章怪图时叠的颜色：第三章偏冷青紫，第四章暖金，第五章青绿。
        private static readonly Color[] BorrowedTints = { Color.white, Color.white, new Color(.74f, .82f, 1f, 1f), new Color(1f, .9f, .74f, 1f), new Color(.7f, 1f, .86f, 1f) };
        private static Color BorrowedTint(Enemy e) { return BorrowedTints[Mathf.Clamp(e.skin, 0, BorrowedTints.Length - 1)]; }
        public static Pose PoseOf(Enemy e)
        {
            if (e.windLeft > 0) return Pose.Windup;
            if (e.sinceStrike < .28f) return Pose.Strike;
            return Pose.Idle;
        }
        /// v0.21 机库：按素材名画一只（被改造过来的）怪，flip = 左右翻过来（怪原本朝左，机器人朝右）。
        /// outline：描一圈纯色边（青色 = 自己人，和同款敌人分开）。翻转用贴图坐标做，不动 GUI.matrix。
        private static Material flatMat;
        public static bool DrawKey(string key, Pose pose, float feetX, float feetY, float canvas, Color tint, bool flip, Color outline = default(Color))
        {
            Texture2D t = Tex(EnemyFolder + key + "_" + (pose == Pose.Windup ? "windup" : pose == Pose.Strike ? "strike" : "idle"));
            if (t == null) t = Tex(EnemyFolder + key + "_idle");
            if (t == null) return false;
            Rect r = Ui.R(feetX - canvas / 2, feetY - canvas * .875f, canvas, canvas), uv = flip ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1);
            if (outline.a > 0 && Event.current.type == EventType.Repaint)
            {
                if (flatMat == null) { Shader fs = Shader.Find("GUI/Text Shader"); if (fs != null) flatMat = new Material(fs) { hideFlags = HideFlags.HideAndDontSave }; }
                if (flatMat != null)
                {
                    float o = 2.2f * Ui.K;
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4;
                        Graphics.DrawTexture(new Rect(r.x + Mathf.Cos(a) * o, r.y + Mathf.Sin(a) * o, r.width, r.height), t, uv, 0, 0, 0, 0, outline, flatMat);
                    }
                }
            }
            Color old = GUI.color; GUI.color = tint;
            GUI.DrawTextureWithTexCoords(r, t, uv, true);
            GUI.color = old; return true;
        }
        /// 正方形素材，脚底在图高 7/8 处。canvas = 画出来的边长（逻辑像素）。
        public static bool DrawEnemy(Enemy e, Pose pose, float feetX, float feetY, float canvas, Color tint, int walkFrame = 0)
        {
            string key = ArtKey(e);
            Texture2D t = walkFrame > 0 && pose == Pose.Idle ? Tex(EnemyFolder + key + "_walk" + walkFrame) : null;
            if (t == null) t = Tex(EnemyFolder + key + "_" + (pose == Pose.Windup ? "windup" : pose == Pose.Strike ? "strike" : "idle"));
            if (t == null) t = Tex(EnemyFolder + key + "_idle");
            if (t == null) return false;
            if (key != EnemyKey(e)) tint *= BorrowedTint(e);
            if (Fade.On)
            {   // 11-25「褪色之城」试验：机器人是灰的，挨刷越多颜色回来越多；倒下（想起来了）就是全彩
                float hp01 = Mathf.Clamp01(e.health / Mathf.Max(1, e.maxHealth));
                float sat = e.Alive ? .06f + .84f * Mathf.Pow(1 - hp01, .8f) : 1;
                Fade.NoBlooms = true;
                bool done = Fade.Draw(new Rect(feetX - canvas / 2, feetY - canvas * .875f, canvas, canvas), t, new Rect(0, 0, 1, 1), tint, sat);
                Fade.NoBlooms = false;
                if (done) return true;
            }
            Color old = GUI.color; GUI.color = tint;
            GUI.DrawTexture(Ui.R(feetX - canvas / 2, feetY - canvas * .875f, canvas, canvas), t, ScaleMode.StretchToFill, true);
            GUI.color = old; return true;
        }
    }
}
