using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// v0.15 战斗血条（美术素材：Resources/Art/V15/Hud）。
    /// 框是三段式（左端 / 可拉伸中段 / 右端），填充条按比例裁 UV（斜纹不会被挤扁）。
    /// 掉血时掉的那段先留白，约 0.35 秒后再缩回。
    public static class HudBars
    {
        private const string Folder = "Art/V15/Hud/";
        private sealed class Frame
        {
            public Texture2D tex; public float capL, capR;       // 源图像素
            public Rect hp, sp;                                   // 槽位（源图像素，左上为原点）
        }
        private static Frame hero, enemy, elite, boss;
        private static Texture2D fillHp, fillGhost, fillSp, fillReady, icon0, icon1, icon2;
        private static bool loaded, ok;
        private static readonly Dictionary<int, Vector3> ghosts = new Dictionary<int, Vector3>();   // x=残影值 y=上次真实值 z=上次掉血时间

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { loaded = false; ok = false; ghosts.Clear(); }

        private static Texture2D T(string n) { return Resources.Load<Texture2D>(Folder + n); }
        private static bool Ready()
        {
            if (loaded) return ok;
            loaded = true;
            hero = new Frame { tex = T("hero_frame"), capL = 175, capR = 88, hp = Rect.MinMaxRect(159, 44, 892, 86), sp = Rect.MinMaxRect(159, 103, 892, 116) };
            enemy = new Frame { tex = T("enemy_frame"), capL = 60, capR = 57, hp = Rect.MinMaxRect(42, 20, 434, 46) };
            elite = new Frame { tex = T("elite_frame"), capL = 100, capR = 32, hp = Rect.MinMaxRect(84, 19, 456, 47) };
            boss = new Frame { tex = T("boss_frame"), capL = 230, capR = 113, hp = Rect.MinMaxRect(189, 45, 859, 90) };
            fillHp = T("fill_hp"); fillGhost = T("fill_ghost"); fillSp = T("fill_sp"); fillReady = T("fill_ready");
            icon0 = T("icon_0"); icon1 = T("icon_1"); icon2 = T("icon_2");
            ok = hero.tex != null && enemy.tex != null && elite.tex != null && boss.tex != null && fillHp != null && fillGhost != null && fillSp != null && fillReady != null && icon0 != null;
            if (!ok) Debug.LogWarning("THREAD ZERO: 血条素材缺失（Resources/" + Folder + "），先用纯色条。");
            return ok;
        }

        /// 掉血残影：返回该单位当前应显示的“白色残影”比例。
        public static float Ghost(int key, float value, float now)
        {
            Vector3 g;
            if (!ghosts.TryGetValue(key, out g)) g = new Vector3(value, value, -9);
            if (value < g.y - .0001f) g.z = now;                 // 刚掉血
            if (value > g.x) g.x = value;                         // 回血：残影跟上
            if (now - g.z > .35f) g.x = Mathf.MoveTowards(g.x, value, Time.unscaledDeltaTime * 1.4f);
            g.y = value; ghosts[key] = g;
            return Mathf.Max(g.x, value);
        }

        // ---------- 主角：头顶，血条 + 技能条 + 左端菱形技能图标 ----------
        /// cx/top：条框中心 x、上沿 y（逻辑坐标）。charge/chargeMax：技能充能格；ready=技能就绪。
        public static void Hero(float cx, float top, float hp01, float ghost01, int charge, int chargeMax, float readiness, float hpNow, float hpMax, float now, float shield01 = 0)
        {
            const float W = 196, s = .22f;
            float x = cx - W / 2;
            bool art = Ready();
            if (art)
            {
                DrawFrame(hero, x, top, W, s);
                Rect hpR = Slot(hero, hero.hp, x, top, W, s), spR = Slot(hero, hero.sp, x, top, W, s);
                Fill(fillGhost, hpR, ghost01, Color.white);
                Fill(fillHp, hpR, hp01, Color.white);
                if (shield01 > 0)   // v0.21 改造板「护盾回路」：血条正上方一条亮蓝细条，长短 = 护盾占最大生命的比例
                { float w = hpR.width * Mathf.Clamp01(shield01); GUI.DrawTexture(new Rect(hpR.x, hpR.y - 4 * Ui.K, w, 3 * Ui.K), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.6f, .9f, 1, .95f), 0, 0); }
                bool ready = readiness >= .999f;
                if (ready)
                {
                    float pulse = .75f + .25f * Mathf.Sin(now * 9);
                    Fill(fillReady, spR, 1, new Color(1, 1, 1, pulse));
                }
                else if (chargeMax > 0)
                {
                    Fill(fillSp, spR, Mathf.Clamp01((float)charge / chargeMax), Color.white);
                    for (int i = 1; i < chargeMax; i++) Ui.Fill(spR.x + spR.width * i / chargeMax - .5f, spR.y, 1, spR.height, Ui.Alpha(Ui.Ink, .9f));
                }
                else Fill(fillSp, spR, readiness, Color.white);
                // 菱形图标压在左端插座上
                Texture2D icon = ready ? icon2 : (chargeMax > 0 && charge > 0) || readiness > 0 ? icon1 : icon0;
                float isz = 24, icx = x + 88 * s, icy = top + 78 * s;
                if (ready) QualityVisual.Halo(Ui.P(icx, icy), 22 * Ui.K, new Color(1, .82f, .29f), .55f + .2f * Mathf.Sin(now * 9));
                GUI.DrawTexture(Ui.R(icx - isz / 2, icy - isz / 2, isz, isz), icon != null ? icon : icon0);
            }
            else
            {
                Ui.Fill(x, top + 6, W, 8, Ui.Alpha(Ui.Ink, .8f)); Ui.Fill(x, top + 6, W * ghost01, 8, Color.white); Ui.Fill(x, top + 6, W * hp01, 8, Ui.Hex(0x3FC4AD));
                Ui.Fill(x, top + 17, W * readiness, 3, readiness >= .999f ? Ui.Hex(0xFFD24A) : Ui.Orange);
            }
            // 血量数字：框右上
            string a = Mathf.Max(0, hpNow).ToString("F0"), b = " / " + hpMax.ToString("F0");
            float wb = Ui.Width(b, 11, false, true), wa = Ui.Width(a, 15, true, true);
            Color shadow = Ui.Alpha(Color.black, .6f);
            float tx = x + W - 6 - wa - wb, ty = top - 17;
            Ui.Text(tx + 1, ty + 1, wa + 4, 18, a, 15, shadow, TextAnchor.LowerLeft, true, true);
            Ui.Text(tx, ty, wa + 4, 18, a, 15, hp01 < .3f ? Ui.Hex(0xFF8A6A) : Ui.Panel, TextAnchor.LowerLeft, true, true);
            Ui.Text(tx + wa, ty, wb + 4, 17, b, 11, Ui.Alpha(Ui.Panel, .75f), TextAnchor.LowerLeft, false, true);
        }

        // ---------- 敌人：头顶细条；精英加粗带警示徽章 ----------
        public static void Enemy(float cx, float top, float hp01, float ghost01, bool isElite)
        {
            Frame f = isElite ? elite : enemy;
            float W = isElite ? 104 : 74, s = isElite ? .2f : .16f;
            float x = cx - W / 2;
            Color red = isElite ? new Color(1, .62f, .16f) : new Color(.93f, .3f, .2f);
            if (Ready())
            {
                DrawFrame(f, x, top, W, s);
                Rect r = Slot(f, f.hp, x, top, W, s);
                Fill(fillGhost, r, ghost01, Color.white);
                Fill(fillGhost, r, hp01, red);
            }
            else
            {
                Ui.Fill(x, top, W, 6, Ui.Alpha(Ui.Ink, .8f)); Ui.Fill(x, top, W * ghost01, 6, Color.white); Ui.Fill(x, top, W * hp01, 6, red);
            }
        }

        // ---------- 首领：屏幕上方一条长框 ----------
        public static void Boss(float cx, float top, float hp01, float ghost01, string name, float hpNow, float hpMax)
        {
            const float W = 620, s = .42f;
            float x = cx - W / 2;
            Color red = new Color(.93f, .28f, .2f);
            if (Ready())
            {
                DrawFrame(boss, x, top, W, s);
                Rect r = Slot(boss, boss.hp, x, top, W, s);
                Fill(fillGhost, r, ghost01, Color.white);
                Fill(fillGhost, r, hp01, red);
                Ui.Text(x + 104 * s, top + 38 * s, 60 * s, 58 * s, "首", 16, Ui.Hex(0xFFB02E), TextAnchor.MiddleCenter, true);
            }
            else { Ui.Fill(x, top + 20, W, 12, Ui.Alpha(Ui.Ink, .8f)); Ui.Fill(x, top + 20, W * hp01, 12, red); }
            Ui.ShadowText(x + 120, top - 22, 300, 22, name, 16, Ui.Panel, TextAnchor.LowerLeft, true);   // v0.20 加投影：第四章亮背景上原来看不见
            Ui.ShadowText(x + W - 420, top - 22, 300, 22, Mathf.Max(0, hpNow).ToString("F0") + " / " + hpMax.ToString("F0"), 14, Ui.Alpha(Ui.Panel, .85f), TextAnchor.LowerRight, false, true);
        }

        // ---------- helpers ----------
        private static void DrawFrame(Frame f, float x, float top, float W, float s)
        {
            float tw = f.tex.width, th = f.tex.height, h = th * s;
            float l = f.capL * s, r = f.capR * s, mid = Mathf.Max(0, W - l - r);
            Part(f.tex, x, top, l, h, 0, f.capL / tw);
            Part(f.tex, x + l, top, mid, h, f.capL / tw, (tw - f.capR) / tw);
            Part(f.tex, x + l + mid, top, r, h, (tw - f.capR) / tw, 1);
        }
        private static void Part(Texture2D t, float x, float y, float w, float h, float u0, float u1)
        {
            if (w <= 0) return;
            GUI.DrawTextureWithTexCoords(Ui.R(x, y, w, h), t, new Rect(u0, 0, u1 - u0, 1));
        }
        /// 源图里的槽位换算到屏幕：左右端按比例 s，中段按拉伸后的宽度线性映射。
        private static Rect Slot(Frame f, Rect src, float x, float top, float W, float s)
        {
            float tw = f.tex.width;
            float x0 = MapX(f, src.xMin, tw, x, W, s), x1 = MapX(f, src.xMax, tw, x, W, s);
            return new Rect(x0 + 1, top + src.yMin * s + .5f, Mathf.Max(1, x1 - x0 - 2), Mathf.Max(1, src.height * s - 1));
        }
        private static float MapX(Frame f, float sx, float tw, float x, float W, float s)
        {
            float l = f.capL * s, r = f.capR * s, mid = Mathf.Max(0, W - l - r);
            if (sx <= f.capL) return x + sx * s;
            if (sx >= tw - f.capR) return x + W - (tw - sx) * s;
            return x + l + (sx - f.capL) / (tw - f.capL - f.capR) * mid;
        }
        private static void Fill(Texture2D t, Rect r, float frac, Color tint)
        {
            frac = Mathf.Clamp01(frac);
            if (frac <= 0 || t == null) return;
            Color old = GUI.color; GUI.color = tint;
            GUI.DrawTextureWithTexCoords(Ui.R(r.x, r.y, r.width * frac, r.height), t, new Rect(0, 0, frac, 1));
            GUI.color = old;
        }
    }
}
