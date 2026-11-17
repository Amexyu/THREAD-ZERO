using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// v0.16 第一章正式美术：分层视差背景（Resources/Art/V16/Stage）与三姿势敌人（Resources/Art/V16/Enemies）。
    /// 素材缺失时返回 false，调用方继续用旧的程序背景 / V07 敌人。
    public static class StageArt
    {
        private const string EnemyFolder = "Art/V16/Enemies/";
        private static readonly string[] StageFolders = { "Art/V16/Stage/", "Art/V16/Stage2/" };   // 第一章 / 第二章
        public const float FarScroll = .25f, MidScroll = .6f;
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
        /// 画一个场景的三层背景。scene：0 A … 3 D（首领场地，远景不卷动）；4..7 是第二章的 A..D。groundY：角色脚下的地面线。
        public static bool Background(int scene, float worldX, float stageW, float stageH, float groundY)
        {
            scene = Mathf.Max(0, scene);
            string folder = StageFolders[Mathf.Min(StageFolders.Length - 1, scene / 4)];
            string key = SceneKeys[scene % 4];
            Texture2D far = Tex(folder + key + "_far"), mid = Tex(folder + key + "_mid"), ground = Tex(folder + key + "_ground");
            if (far == null) return false;
            bool arena = key == "D";
            if (arena) Cover(far, stageW, stageH);
            else Layer(far, worldX * FarScroll, stageH, 0);
            if (mid != null) Layer(mid, worldX * MidScroll, stageH, 0);
            if (ground != null)
            {   // 地面带：图高的 20% 处是地面上沿，对齐到 groundY；下沿铺到舞台底部
                float h = (stageH - groundY) / .8f;
                Layer(ground, worldX, h, groundY - h * .2f);
            }
            return true;
        }
        // 横向平铺的一层：按高度等比缩放，scroll 为已经乘过系数的卷动距离（逻辑像素）。
        private static void Layer(Texture2D t, float scroll, float h, float y)
        {
            float w = h * t.width / t.height;
            GUI.DrawTextureWithTexCoords(Ui.R(0, y, 1600, h), t, new Rect(scroll / w, 0, 1600 / w, 1), true);
        }
        private static void Cover(Texture2D t, float w, float h)
        {
            float k = Mathf.Max(w / t.width, h / t.height), tw = t.width * k, th = t.height * k;
            GUI.DrawTextureWithTexCoords(Ui.R((w - tw) / 2, (h - th) / 2, tw, th), t, new Rect(0, 0, 1, 1), true);
        }

        // ---------- 敌人 ----------
        public enum Pose { Idle, Windup, Strike }
        /// 移动方式：用脚走（和角色同一套步伐）、轮子 / 履带（不颠，只有发动机震动）、飞行（悬停起伏）。
        public enum Gait { Legged, Wheeled, Flying }
        public static Gait GaitOf(Enemy e)
        {
            switch (EnemyKey(e))
            {
                case "ranged": case "elite_beacon": case "swarm2": case "elite_carrier": return Gait.Flying;
                case "ranged2": case "boss_gatekeeper": case "boss_crane": return Gait.Wheeled;
                default: return Gait.Legged;   // swarm（机械狗）、brute / brute2、elite_crusher / elite_hauler
            }
        }
        /// 走路帧（可选）：有 _walk1 / _walk2 图就按步伐交替，没有就用待机图。
        public static bool HasWalk(Enemy e) { return Tex(EnemyFolder + EnemyKey(e) + "_walk1") != null && Tex(EnemyFolder + EnemyKey(e) + "_walk2") != null; }
        public static string EnemyKey(Enemy e)
        {
            bool two = e.skin == 1;   // 第二章：物流高架
            if (e.boss) return two ? "boss_crane" : "boss_gatekeeper";
            switch (e.move)
            {
                case EnemyMove.Crusher: return two ? "elite_hauler" : "elite_crusher";
                case EnemyMove.Beacon: return two ? "elite_carrier" : "elite_beacon";
                case EnemyMove.Slam: return two ? "brute2" : "brute";
                case EnemyMove.Bolt: return two ? "ranged2" : "ranged";
                default: return two ? "swarm2" : "swarm";
            }
        }
        public static Pose PoseOf(Enemy e)
        {
            if (e.windLeft > 0) return Pose.Windup;
            if (e.sinceStrike < .28f) return Pose.Strike;
            return Pose.Idle;
        }
        /// 正方形素材，脚底在图高 7/8 处。canvas = 画出来的边长（逻辑像素）。
        public static bool DrawEnemy(Enemy e, Pose pose, float feetX, float feetY, float canvas, Color tint, int walkFrame = 0)
        {
            string key = EnemyKey(e);
            Texture2D t = walkFrame > 0 && pose == Pose.Idle ? Tex(EnemyFolder + key + "_walk" + walkFrame) : null;
            if (t == null) t = Tex(EnemyFolder + key + "_" + (pose == Pose.Windup ? "windup" : pose == Pose.Strike ? "strike" : "idle"));
            if (t == null) t = Tex(EnemyFolder + key + "_idle");
            if (t == null) return false;
            Color old = GUI.color; GUI.color = tint;
            GUI.DrawTexture(Ui.R(feetX - canvas / 2, feetY - canvas * .875f, canvas, canvas), t, ScaleMode.StretchToFill, true);
            GUI.color = old; return true;
        }
    }
}
