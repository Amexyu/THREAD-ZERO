using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ThreadZero
{
    public sealed partial class GameApp
    {
        // ---------- 11-24 v0.29 新界面：开关 ----------
        private static int uiV2 = -1;
        /// 新版界面皮肤（整备 / 衣柜）。F4 在整备、衣柜页切换新旧，记在 PlayerPrefs。
        internal static bool UiV2
        {
            get { if (uiV2 < 0) uiV2 = PlayerPrefs.GetInt("tz_ui_v2", 1); return uiV2 == 1; }
            set { uiV2 = value ? 1 : 0; PlayerPrefs.SetInt("tz_ui_v2", uiV2); }
        }
        public void TestUiV2(int on) { UiV2 = on != 0; }
        /// 测试用：选中仓库当前筛选结果里的第 n 件（截图看没穿着的装备）。
        public void TestSelectBag(int n) { inventoryQuery.slot = slotFilter; List<Item> list = inventoryQuery.Apply(model.state); if (list.Count > 0) SelectItem(list[Mathf.Clamp(n, 0, list.Count - 1)]); }
        /// 测试用：装上前 n 个义体、都设成 level 级（不要求测试存档，截图用）。
        public void TestImplants(int n, int level)
        {
            for (int i = 0; i < n && i < Catalog.Implants.Length; i++)
            {
                if (!model.state.implants.Contains(Catalog.Implants[i].id)) model.state.implants.Add(Catalog.Implants[i].id);
                if (i < model.state.implantLevels.Length) model.state.implantLevels[i] = Mathf.Clamp(level, 1, Catalog.ImplantMaxLevel);
            }
        }

        // ---------- 战力 / 评分 ----------
        private static float PowerOf(Stats s)
        {
            float offense = s.attack * (1 + s.haste) * (1 + s.crit * (Balance.CritMultiplier - 1 + s.critDamage));
            float survive = s.health * (100 + Mathf.Max(0, s.defense)) / 100;
            return Mathf.Sqrt(Mathf.Max(1, offense) * Mathf.Max(1, survive));
        }
        /// 卡片左下角的大数字「评分」：这件单独穿在裸装角色身上能加多少战力（和角色战力同一个算法、同样 ×10 显示，能直接比大小）。
        private static int ItemScore(Item item)
        {
            if (item == null) return 0;
            var bare = new Stats(Balance.BaseHealth, Balance.BaseAttack, Balance.BaseDefense, 0, Balance.BaseCrit);
            var with = new Stats(Balance.BaseHealth, Balance.BaseAttack, Balance.BaseDefense, 0, Balance.BaseCrit);
            with.Add(GameModel.ItemStats(item));
            return Mathf.Max(1, Mathf.RoundToInt((PowerOf(with) - PowerOf(bare)) * 10));
        }
        private static string Thousands(int value) { return value.ToString("N0", CultureInfo.InvariantCulture); }
        private int PowerShown { get { return Mathf.RoundToInt(model.Power() * 10); } }

        // ---------- 技能说明（整备详情） ----------
        // 返回中文原句，画字时 Ui.Text 按 Tools/loc/master/11_skin.txt 的模板翻译（不要先翻好再交给 Ui.Text：日文里的汉字会被再翻一遍）。
        private static string SkillWhen(SkillDef s)
        {
            if (s.trigger == SkillTrigger.EveryNAttacks) return "每 " + s.attacks + " 次普攻发动";
            if (s.trigger == SkillTrigger.Chance) return "每次出手 " + Mathf.RoundToInt(s.chance * 100) + "% 概率发动";
            return "冷却 " + s.cooldown.ToString("0.#", CultureInfo.InvariantCulture) + " 秒";
        }
        /// 技能的几个要点，一条一个小标签：段数、弹跳 / 穿透、范围、击退、合计倍率。
        private static List<string> SkillParts(SkillDef s)
        {
            int hits = s.hitDamage == null ? 0 : s.hitDamage.Length;
            var parts = new List<string>();
            if (hits > 0) parts.Add(hits + " 段");
            if (s.chain > 0) parts.Add("弹跳 " + s.chain + " 次");
            else if (s.pierceAll) parts.Add("每段穿透");
            else if (s.pierceLastHit) parts.Add("末段穿透");
            if (s.splash > 0) parts.Add("范围伤害");
            if (s.push > 0) parts.Add("击退");
            float total = 0; if (s.hitDamage != null) foreach (float d in s.hitDamage) total += d;
            if (total > 0) parts.Add("合计 " + Mathf.RoundToInt(total * 100) + "% 攻击");
            return parts;
        }

        // ---------- 收藏卡 ----------
        // 角标的单字：中文用汉字，其它语言用字母（单个汉字没有译文，日文里也不是同一个字）
        private static readonly string[] SlotGlyph = { "武", "头", "衣", "背", "腿" }, SlotGlyphLatin = { "W", "H", "C", "B", "L" };
        private static readonly Dictionary<Effect, string> EffectGlyph = new Dictionary<Effect, string>
        {
            { Effect.Execute, "决" }, { Effect.Arc, "弧" }, { Effect.Leech, "汲" }, { Effect.Mark, "标" },
            { Effect.Guard, "盾" }, { Effect.Drone, "机" }, { Effect.Thruster, "推" },
        };
        private static readonly Dictionary<Effect, string> EffectGlyphLatin = new Dictionary<Effect, string>
        {
            { Effect.Execute, "X" }, { Effect.Arc, "A" }, { Effect.Leech, "L" }, { Effect.Mark, "M" },
            { Effect.Guard, "G" }, { Effect.Drone, "D" }, { Effect.Thruster, "T" },
        };
        private struct CardLook
        {
            public bool equipped, isNew, selected, small, owned, current, locked, hover, max;
            public string sub, foot, power, model;
            public int level;          // < 0 不显示等级
            public float artRatio, nameSize;
        }
        private static CardLook Look() { return new CardLook { owned = true, level = -1 }; }

        /// 深色收藏卡（例图 v0.29）：外黑边 + 品质色内边 + 右侧品质竖条 + 画框（品质光、放射光、网点、全息斜光）+ 名字 / 型号 / 品质宝石 / 评分 / 等级。
        /// 返回卡片最后画在哪个 y（选中、悬停会抬起）。
        private float SkinCard(float x, float y, float w, float h, EquipmentBase b, Quality quality, string name, CardLook o, float angle = 0)
        {
            int q = (int)quality; Color qc = QualityVisual.Colors[q];
            if (o.selected) y -= 8; else if (o.hover) y -= 3;
            Matrix4x4 m = Skin.Rotate(angle, x + w / 2, y + h / 2);
            if (o.selected) Skin.Glow(x + w / 2, y + h / 2, Mathf.Max(w, h) * .62f, Ui.Orange, .5f);
            Skin.Nine("card_shadow", x - 14, y - 14 + (o.small ? 4 : 6), w + 28, h + 28, 28, new Color(1, 1, 1, .9f));
            Skin.Nine("card_outer", x, y, w, h, 14, Color.white);
            Skin.Nine("card_rim", x + 1.8f, y + 1.8f, w - 3.6f, h - 3.6f, 14, Color.Lerp(qc, Color.white, .12f));
            Skin.Nine("card_body", x + 3.8f, y + 3.8f, w - 7.6f, h - 7.6f, 12, Color.white);
            float sw = o.small ? 7 : 10, sx = x + w - 3.8f - sw;
            Skin.Nine("card_stripe", sx, y + 3.8f, sw, h - 7.6f, sw / 2, 6, sw / 2, 6, qc);
            if (!o.small)
            {   // 竖条里竖着印品质英文
                Matrix4x4 m2 = Skin.Rotate(90, sx + sw / 2, y + 18 + (h - 36) / 2);
                float len = h - 36;
                Skin.Spaced(sx + sw / 2 - len / 2, y + 18 + (h - 36) / 2 - sw / 2, len, sw, Skin.QualityEnglish[q], 6.5f, new Color(.07f, .07f, .08f, .62f), TextAnchor.MiddleLeft);
                Skin.End(m2);
            }
            // 画框
            float ratio = o.artRatio > 0 ? o.artRatio : o.small ? .63f : .54f;
            float ax = x + 7, ay = y + 8, aw = w - sw - 14.5f, ah = h * ratio;
            Skin.Nine("card_art", ax, ay, aw, ah, 10, Color.white);
            bool clip = Mathf.Abs(angle) < .01f;   // GUI 裁剪组遇到旋转会错位，歪着画的卡不裁
            if (clip) Ui.BeginClip(ax + 1, ay + 1, aw - 2, ah - 2, 0);
            if (o.owned)
            {
                Skin.Glow(ax + aw / 2, ay + ah * .62f, aw * .55f, qc, .42f);
                Skin.Sprite("fx_rays", ax, ay, aw, ah, Ui.Alpha(qc, .45f));
                float hs = Mathf.Min(aw, ah) * .95f;
                Skin.Sprite("fx_halftone", ax + aw - hs, ay + ah - hs, hs, hs, Ui.Alpha(qc, .24f));
                if (q >= 4)
                {   // 全息斜光：慢慢往右滑
                    float slide = Mathf.Repeat(Time.unscaledTime * 18 + x * .37f, aw * 1.6f) - aw * .3f;
                    Skin.Sprite("fx_sheen", ax + slide - aw * .5f, ay, aw, ah, new Color(1, 1, 1, .32f));
                }
                if (q >= 6)
                {
                    int seed = b == null ? 7 : b.id.GetHashCode();
                    for (int k = 0; k < 6; k++)
                    {
                        float px = ax + 6 + Mathf.Abs((seed >> k) % 97) / 97f * (aw - 12), py = ay + 6 + Mathf.Abs((seed >> (k + 7)) % 89) / 89f * (ah - 12);
                        float s = 7 + 3 * Mathf.Sin(Time.unscaledTime * 2.4f + k * 1.7f);
                        Skin.Sprite("fx_sparkle", px - s / 2, py - s / 2, s, s, new Color(1, 1, 1, .85f));
                    }
                }
            }
            if (b != null)
            {
                float icon = Mathf.Min(aw, ah) * .86f; Rect ir = Ui.R(ax + (aw - icon) / 2, ay + (ah - icon) / 2, icon, icon);
                Color old = GUI.color;
                if (o.owned)
                {   // 投影：先用黑色画一遍、往右下挪一点
                    GUI.color = new Color(0, 0, 0, .45f);
                    PaperDollView.DrawEquipmentIcon(b, Ui.R(ax + (aw - icon) / 2 + 2, ay + (ah - icon) / 2 + 3, icon, icon));
                    GUI.color = old;
                    PaperDollView.DrawEquipmentIcon(b, ir);
                }
                else
                {   // 没收藏：剪影 + 锁
                    GUI.color = new Color(.27f, .28f, .31f, 1);
                    PaperDollView.DrawEquipmentIcon(b, ir);
                    GUI.color = old;
                    float cx = ax + aw / 2, cy = ay + ah / 2;
                    Skin.Round(cx - 12, cy - 3, 24, 19, Ui.Hex(0x7C7F86));
                    if (clip) { Ui.BeginClip(cx - 10, cy - 17, 20, 15, 0); Skin.Sprite("ring", cx - 9, cy - 17, 18, 24, Ui.Hex(0x7C7F86)); Ui.EndClip(); }
                }
            }
            if (clip) Ui.EndClip();
            // 右上角标：固定效果、部位
            float br = o.small ? 6.6f : 8.5f, bx = ax + aw - br - 3.5f, by = ay + br + 3.5f;
            string glyph; bool han = Loc.Get() == Loc.Lang.Zh;
            if (b != null && (han ? EffectGlyph : EffectGlyphLatin).TryGetValue(b.effect, out glyph)) { Skin.Badge(bx, by, br, Color.Lerp(qc, Color.white, .15f), glyph); by += br * 2 + 4; }
            if (b != null) Skin.Badge(bx, by, br, Ui.Hex(0xE9E9E4), (han ? SlotGlyph : SlotGlyphLatin)[(int)b.slot]);
            // 等级（画框左下）
            if (o.level >= 0)
            {
                string lv = o.max ? "MAX" : o.level.ToString(); float lfs = o.small ? 9 : 10.5f;
                float lw = Ui.Width(lv, lfs, true, true) + (o.max ? 12 : Ui.Width("LV.", 6.5f, true, true) + 14), lh = o.small ? 14 : 16;
                float lx = ax + 4, ly = ay + ah - lh - 4;
                Skin.Pill(lx, ly, lw, lh, o.max ? Ui.Orange : new Color(.03f, .035f, .04f, .82f));
                if (!o.max) { Skin.PillLine(lx, ly, lw, lh, new Color(1, 1, 1, .18f)); Ui.Text(lx + 6, ly, 20, lh, "LV.", 6.5f, Skin.OnCardMute, TextAnchor.MiddleLeft, true, true); }
                Ui.Text(lx, ly, lw - (o.max ? 0 : 6), lh, lv, lfs, o.max ? Skin.Ink2 : Color.white, o.max ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight, true, true);
            }
            // 左上「穿戴中」斜缎带
            if (o.equipped && !o.small)
            {
                Skin.Sprite("ribbon_corner", x + 3.8f, y + 3.8f, 44, 44, Color.white);
                Matrix4x4 m3 = Skin.Rotate(-45, x + 3.8f + 15.5f, y + 3.8f + 15.5f);
                Ui.Text(x + 3.8f + 15.5f - 24, y + 3.8f + 15.5f - 7, 48, 14, "穿戴中", 8.5f, Color.white, TextAnchor.MiddleCenter, true);
                Skin.End(m3);
            }
            // 名字（左边部位色小条）、型号 / 说明、品质宝石
            float ny = ay + ah + (o.small ? 4 : 6), nameSize = o.nameSize > 0 ? o.nameSize : o.small ? 10 : 12.5f, maxW = w - sw - 22;
            float fs = Skin.Fit(name, nameSize, maxW); bool two = false;
            if (fs < nameSize * .82f)
            {   // 译文长（英文名常有三个词）：先试两行（大卡字最小 7.5、小卡 8）；两行也放不下就单行，超出的部分裁掉
                float s2 = nameSize * .9f, min2 = o.small ? 8 : 7.5f;
                while (s2 > min2 && Ui.Height(name, s2, maxW, true) > s2 * 2.8f) s2 -= .5f;
                if (Ui.Height(name, s2, maxW, true) <= s2 * 2.8f) { two = true; fs = s2; }
            }
            bool cut = !two && clip && Ui.Width(name, fs, true) > maxW + 3;
            float nh = two ? Ui.Height(name, fs, maxW, true) : fs + 6;
            if (b != null) Ui.Fill(x + 6.5f, ny + 2, 3, two ? nh - 3 : fs + 2, Skin.SlotColors[(int)b.slot]);
            if (cut) Ui.BeginClip(x + 9, ny - 3, maxW + 7, nh + 6, 0);
            Skin.OutlineText(x + 13, ny, cut ? 400 : maxW + 4, nh, name, fs, o.owned ? Color.white : Ui.Hex(0x9A9DA3), Skin.Ink2, two ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, true, false, .9f, two);
            if (cut) Ui.EndClip();
            float gy = two ? ny + nh + (o.small ? 2 : 4) : ny + fs + (o.small ? 8 : 10);
            if (!string.IsNullOrEmpty(o.sub)) { Ui.FitText(x + 13, gy - 6, maxW, 16, o.sub, 9.5f, o.owned ? Ui.Hex(0xA4A8AE) : Ui.Hex(0x7B7F86)); gy += 13; }
            else if (!string.IsNullOrEmpty(o.model) && !o.small && !two) { Ui.FitText(x + 13, gy - 6, maxW, 12, o.model, 7.5f, Ui.Hex(0x8C9096), TextAnchor.MiddleLeft, true, true); gy += 9; }
            float step = o.small ? 8.4f : 9.6f, gr = o.small ? 2.9f : 3.4f;
            if (gy + 4 + gr <= y + h - 5)   // 小卡名字两行时宝石放不下就不画
                for (int i = 0; i < 7; i++) Skin.Gem(x + 14 + i * step, gy + 4, gr, qc, o.owned && i <= q);
            // 底部小吊牌（衣柜：所属套装）
            if (!string.IsNullOrEmpty(o.foot))
            {
                float fw = Mathf.Min(w - sw - 22, Ui.Width(o.foot, 9, true) + 16);   // 译文长时吊牌拉长、不画条码
                Skin.Round(x + 9, y + h - 30, fw, 18, new Color(1, 1, 1, .07f)); Skin.RoundLine(x + 9, y + h - 30, fw, 18, new Color(1, 1, 1, .16f));
                Ui.FitText(x + 17, y + h - 30, fw - 12, 18, o.foot, 9, o.owned ? Ui.Hex(0xB9BEC0) : Ui.Hex(0x7B7F86), TextAnchor.MiddleLeft, true);
                if (fw <= w - sw - 60) Skin.Barcode(x + w - sw - 46, y + h - 28, 30, 13, o.owned ? Ui.Hex(0x5A5E65) : Ui.Hex(0x3E4146));
            }
            // 评分
            if (!string.IsNullOrEmpty(o.power) && !o.small)
            {
                Ui.Text(x + 9, y + h - 37, 60, 10, "P O W E R", 5.5f, Ui.Hex(0x8C9096), TextAnchor.MiddleLeft, true, true);
                Skin.OutlineText(x + 8, y + h - 30, w - sw - 16, 22, o.power, Skin.Fit(o.power, 16, w - sw - 20, true, true), Color.white, Skin.Ink2, TextAnchor.MiddleLeft, true, true, 1);
            }
            if (o.selected)
            {
                Skin.Nine("card_select", x - 7, y - 7, w + 14, h + 14, 18, Color.white);
                Skin.Sprite("gem", x + w / 2 - 7, y + h + 5, 14, 14, Ui.Orange);
            }
            Skin.End(m);
            // 夹子、贴纸不跟着卡转
            Skin.Clip(x + w / 2, y + 2);
            if (o.current) { float cw = Mathf.Min(w - 30, Ui.Width("当前外观", 9.5f, true) + 14); Skin.Tag(x + 8, y + 12, cw, 19, Ui.Orange, "当前外观", -5, 9.5f, Color.white); }
            if (o.isNew) Skin.Sticker(x + w - 4, y + 2, 11, Ui.Orange, "NEW", -14, 7.5f, Color.white);
            if (o.locked) { Skin.Disk(x + 10, y + h * .55f, 9, Color.white); Skin.Disk(x + 10, y + h * .55f, 7.5f, Ui.Hex(0x3B3D42)); Ui.IconLock(x + 4, y + h * .55f - 7, Color.white); }
            return y;
        }
    }
}
