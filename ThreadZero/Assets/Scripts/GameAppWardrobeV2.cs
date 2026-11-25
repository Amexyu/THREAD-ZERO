using System.Linq;
using UnityEngine;

namespace ThreadZero
{
    /// 11-24 v0.29 衣柜新皮肤（例图「化妆镜 + 衣架」）：左边化妆镜里看穿搭、下面外观收集和恢复按钮；
    /// 右边部位按键、章节按键、衣架上挂 5 张收藏卡，下面「外观详情」和「定向回收订单」。功能和旧版一样，F4 切换。
    public sealed partial class GameApp
    {
        private void WardrobePageV2()
        {
            Skin.PageBackground();
            MirrorPanelV2();
            WardrobeTabsV2();
            if (wardrobeSets) { WardrobeSetsV2(); return; }
            WardrobeRackV2();
        }

        // ================= 左：化妆镜 =================
        private void MirrorPanelV2()
        {
            const float lx = 128, ly = 16, lw = 440, lh = 700;
            Skin.Plastic(lx, ly, lw, lh);
            Skin.Screw(lx + 14, ly + 14); Skin.Screw(lx + lw - 14, ly + 14);
            Ui.FitText(lx + 22, ly + 10, 160, 42, "衣柜", 30, Skin.TextDark, TextAnchor.MiddleLeft, true);
            Skin.Spaced(lx + 24, ly + 50, 100, 14, "WARDROBE", 8.5f, Skin.TextMid);
            Ui.FitText(lx + 126, ly + 48, lw - 150, 18, "穿搭预览 · 只改变样子", 10, Skin.TextMid, TextAnchor.MiddleLeft, true);

            // 镜子：竖放的胶囊形（上下半圆），奶油色镜框 + 一圈灯泡 + 浅蓝镜面
            float mx = lx + 34, my = ly + 84, mw = lw - 68, mh = lh - 104;
            Skin.Glow(mx + mw / 2, my + mh / 2, mw * .62f, Ui.Hex(0xFFC46B), .16f);
            Skin.Stadium(mx - 2, my + 4, mw + 4, mh, new Color(0, 0, 0, .22f));
            Skin.Stadium(mx, my, mw, mh, Ui.Hex(0xE9DCC0));
            Skin.Stadium(mx + 12, my + 12, mw - 24, mh - 24, Ui.Hex(0xCDBB97));
            Skin.Stadium(mx + 17, my + 17, mw - 34, mh - 34, Ui.Hex(0xB9CDD6));
            Skin.Stadium(mx + 17, my + 17, mw - 34, (mh - 34) * .55f, new Color(1, 1, 1, .16f));   // 上半部分亮一点
            Skin.Glow(mx + mw * .36f, my + mh * .24f, mw * .3f, Color.white, .22f);
            // 灯泡：沿镜框中线均匀排一圈
            float r = mw / 2 - 6, cx = mx + mw / 2, top = my + mw / 2, bottom = my + mh - mw / 2, straight = bottom - top;
            float perimeter = 2 * Mathf.PI * r + 2 * straight; const int bulbs = 22;
            for (int i = 0; i < bulbs; i++)
            {
                float d = (i + .5f) / bulbs * perimeter, bx, by;
                if (d < Mathf.PI * r) { float a = Mathf.PI + d / r; bx = cx + r * Mathf.Cos(a); by = top + r * Mathf.Sin(a); }   // 上半圆：从左往右
                else if ((d -= Mathf.PI * r) < straight) { bx = cx + r; by = top + d; }                                           // 右边往下
                else if ((d -= straight) < Mathf.PI * r) { float a = d / r; bx = cx + r * Mathf.Cos(a); by = bottom + r * Mathf.Sin(a); }   // 下半圆：从右往左
                else { d -= Mathf.PI * r; bx = cx - r; by = bottom - d; }                                                          // 左边往上
                Skin.Glow(bx, by, 13, Ui.Hex(0xFFC870), .6f);
                Skin.Disk(bx, by + .8f, 6.6f, new Color(0, 0, 0, .18f));
                Skin.Disk(bx, by, 6.4f, Ui.Hex(0xF5E6C4));
                Skin.Disk(bx, by - .5f, 4.6f, Ui.Hex(0xFFFDF5));
            }
            // 镜子里的人
            float feet = my + mh - 70;
            Skin.Glow(cx, feet, 90, Ui.Orange, .3f);
            Skin.Sprite("platform", cx - 100, feet - 20, 200, 44, Color.white);
            PreviewHero(cx + 4, feet + 6, 520, cx - 180, my + 60, 360, feet - my - 60);

            // 外观收集（深色金属条）
            int owned = model.state.collection.Count, total = Catalog.All.Length;
            const float cy0 = 732;
            Skin.Metal(lx, cy0, lw, 66);
            Ui.Text(lx + 18, cy0 + 6, 200, 26, "外观收集", 13, Ui.Hex(0xE4E6E8), TextAnchor.MiddleLeft, true);
            string totalText = " / " + total; float totalW = Ui.Width(totalText, 13, true, true);
            Ui.Text(lx + lw - 18 - totalW, cy0 + 4, totalW, 30, totalText, 13, Ui.Hex(0x9EA3A8), TextAnchor.LowerLeft, true, true);
            Skin.OutlineText(lx + 180, cy0 + 1, lw - 200 - totalW, 34, owned.ToString(), 26, Color.white, Ui.Hex(0x0B0C0D), TextAnchor.LowerRight, true, true, 1);
            Skin.Pill(lx + 18, cy0 + 44, lw - 36, 10, Ui.Hex(0x101113));
            if (owned > 0) Skin.Pill(lx + 19, cy0 + 45, Mathf.Max(8, (lw - 38) * owned / Mathf.Max(1, total)), 8, Ui.Orange);
            for (int k = 1; k < 5; k++) Ui.Fill(lx + 18 + (lw - 36) * k / 5f, cy0 + 44, 1.2f, 10, new Color(0, 0, 0, .55f));   // 每章一段
            // 恢复实际装备外观
            bool custom = model.state.appearance.Any(id => !string.IsNullOrEmpty(id));
            if (Skin.Keycap(lx, 818, lw, 52, "恢复实际装备外观", Skin.Key.White, custom, 16))
            { model.state.appearance = new string[Catalog.SlotCount]; Save(); Notify("已恢复实际装备外观"); }
        }

        // ================= 右上：部位、章节、套装 =================
        private void WardrobeTabsV2()
        {
            // 部位：5 个大键帽，右边是收藏数
            for (int i = 0; i < Catalog.SlotCount; i++)
            {
                int slot = i; float x = 588 + i * 197, w = 189; bool on = wardrobeSlot == i && !wardrobeSets;
                string count = Catalog.All.Count(b => (int)b.slot == slot && model.state.collection.Contains(b.id)) + " / " + Catalog.All.Count(b => (int)b.slot == slot);
                if (Skin.Keycap(x, 16, w, 48, "", on ? Skin.Key.Orange : Skin.Key.White, true, 16, on))
                { wardrobeSlot = i; selectedLook = Catalog.All.First(b => (int)b.slot == slot).id; wardrobeSets = false; }
                float oy = on ? 3 : 0, cw = Ui.Width(count, 12, true, true);
                Skin.Disk(x + 18, 16 + oy + 21, 5, on ? Color.white : Skin.TextDark);
                Ui.FitText(x + 30, 16 + oy, w - cw - 50, 43, Catalog.SlotNames[i], 16, on ? Color.white : Skin.TextDark, TextAnchor.MiddleLeft, true);
                Ui.Text(x + w - cw - 16, 16 + oy, cw + 4, 43, count, 12, on ? Color.white : Skin.TextMid, TextAnchor.MiddleLeft, true, true);
            }
            // 章节（第 6 页是零号深网限定）+ 套装视图
            for (int c = 1; c <= Catalog.DeepNetPage; c++)
            {
                int ch = c; bool on = wardrobeChapter == c;
                string label = c == Catalog.DeepNetPage ? "零号深网" : "第" + "一二三四五"[Mathf.Min(4, c - 1)] + "章";
                if (Skin.Keycap(588 + (c - 1) * 126, 74, 118, 34, label, on ? Skin.Key.Dark : Skin.Key.White, true, 13, on))
                { wardrobeChapter = ch; selectedLook = null; if (ch == Catalog.DeepNetPage) wardrobeSets = false; }
            }
            if (Skin.Keycap(1356, 74, 212, 34, "套装 · " + model.SetsComplete + " / " + Catalog.SetCount, wardrobeSets ? Skin.Key.Dark : Skin.Key.Orange, true, 13, wardrobeSets))
            { wardrobeSets = !wardrobeSets; if (wardrobeChapter == Catalog.DeepNetPage) wardrobeChapter = 1; }
        }

        /// 这件外观属于哪一套（初始和各章都是「部位字母 + 套装号」，深网限定不算套装），-1 = 不属于。
        private static int SetOfLook(string id)
        {
            int n;
            if (string.IsNullOrEmpty(id) || id.Length < 2 || Catalog.IsDeepNet(id) || !int.TryParse(id.Substring(1), out n)) return -1;
            return n >= 0 && n < Catalog.SetCount ? n : -1;
        }
        /// 衣柜卡片的颜色：仓库里这件最好的品质（没有就按量产画）。
        private Quality LookQuality(string id)
        {
            int best = -1;
            foreach (Item item in model.state.items) if (item.baseId == id && (int)item.quality > best) best = (int)item.quality;
            return (Quality)Mathf.Max(0, best);
        }

        // ================= 右中：衣架 + 右下：详情、定向回收 =================
        private void WardrobeRackV2()
        {
            const float rx = 588, ry = 118, rw = 980, rh = 322;
            Skin.Plastic(rx, ry, rw, rh);
            float ix = rx + 14, iy = ry + 12, iw = rw - 28, ih = rh - 24;
            Skin.Recess(ix, iy, iw, ih);
            Skin.Tile("tile_pegboard", ix + 4, iy + 14, iw - 8, ih - 18, 15, 15, new Color(1, 1, 1, .75f));
            Skin.Led(ix + 30, iy + 11, iw - 60, Ui.Orange);
            Skin.Glow(ix + iw / 2, iy + 20, 360, Ui.Orange, .1f);

            EquipmentBase[] looks = Catalog.All.Where(b => (int)b.slot == wardrobeSlot && Catalog.LookPage(b.id) == wardrobeChapter).ToArray();
            if (looks.Length == 0)
            {
                if (wardrobeChapter != Catalog.DeepNetPage) { wardrobeChapter = 1; return; }
                Ui.Text(ix, iy, iw, ih, "这个部位没有深网限定外观\n零号深网第 " + string.Join(" / ", Catalog.DeepNetFloors.Select(f => f.ToString()).ToArray()) + " 层各给一件：头部、外套、背包", 16, Skin.TextMid, TextAnchor.MiddleCenter, true);
                LookDetailV2(null); return;
            }
            if (!looks.Any(b => b.id == selectedLook)) selectedLook = looks[0].id;
            float railY = iy + 30;
            Skin.Rail(ix + 18, railY, iw - 36);
            const float cardW = 172, cardH = 236, gap = 16;
            float startX = ix + (iw - (looks.Length * cardW + (looks.Length - 1) * gap)) / 2;
            for (int i = 0; i < looks.Length; i++)
            {
                EquipmentBase b = looks[i]; float x = startX + i * (cardW + gap), y = railY + 22;
                bool has = model.state.collection.Contains(b.id), selected = selectedLook == b.id, hover = Ui.Hover(x, y, cardW, cardH);
                Skin.Hook(x + cardW / 2, railY + 6);
                var look = Look();
                look.owned = has; look.selected = selected; look.hover = hover; look.current = model.Appearance(b.slot).id == b.id;
                look.sub = has ? "已收藏" : Catalog.SourceShort(b.id);   // 没收藏的卡是剪影 + 锁，这里只写产地
                int set = SetOfLook(b.id); if (set >= 0) look.foot = "套装 · " + Catalog.SetNames[set];
                float top = SkinCard(x, y, cardW, cardH, b, LookQuality(b.id), b.name, look);
                if (model.state.recoveryTarget == b.id) { float tw = Mathf.Min(cardW - 30, Ui.Width("回收目标", 9.5f, true) + 14); Skin.Tag(x + 8, top + (look.current ? 36 : 12), tw, 19, Ui.Hex(0x2E3034), "回收目标", -5, 9.5f, Ui.Orange); }
                if (Ui.Click(x, y - 8, cardW, cardH + 8)) selectedLook = b.id;
            }
            LookDetailV2(Catalog.Find(selectedLook));
        }

        private void LookDetailV2(EquipmentBase chosen)
        {
            // 外观详情（白纸）
            const float dx = 600, dy = 468, dw = 576, dh = 396;
            Skin.Paper(dx, dy, dw, dh);
            Ui.Fill(dx + 14, dy + 16, 4, 20, Ui.Orange);
            Skin.Spaced(dx + 26, dy + 18, 70, 16, "LOOK", 8, Skin.TextLight);
            Skin.Barcode(dx + dw - 150, dy + dh - 30, 136, 16, Skin.TextDark);
            // 定向回收订单（橙色抬头的单子）
            const float tx = 1204, ty = 456, tw = 364, th = 420;
            Skin.Plastic(tx, ty, tw, th, new Color(.985f, .975f, .955f, 1));
            Skin.Round(tx, ty, tw, 46, Ui.Orange); Ui.Fill(tx, ty + 34, tw, 12, Ui.Orange);
            Skin.Spaced(tx + 18, ty + 6, 90, 34, "TARGET", 8, Color.white);
            Ui.FitText(tx + 110, ty + 6, tw - 126, 34, "定向回收订单", 12, Color.white, TextAnchor.MiddleLeft, true);
            if (chosen == null) return;

            bool unlocked = model.state.collection.Contains(chosen.id), tracked = model.state.recoveryTarget == chosen.id, wearing = model.Appearance(chosen.slot).id == chosen.id;
            bool deep = Catalog.IsDeepNet(chosen.id);   // 零号深网限定：不掉落，不能定向回收
            string effectName, effectText; SplitEffect(chosen.effect, out effectName, out effectText);
            Ui.FitText(dx + 26 + Ui.Width("L O O K", 8, true, true) + 10, dy + 16, 300, 20, Catalog.SlotNames[(int)chosen.slot] + "外观", 11, Skin.TextMid, TextAnchor.MiddleLeft, true);
            Ui.FitText(dx + 20, dy + 40, dw - 220, 50, chosen.name, 32, Skin.TextDark, TextAnchor.MiddleLeft, true);
            // 标签：产地（深色）、固定效果、群伤 / 单体
            float cx = dx + 20, cy = dy + 98;
            string region = "产地 · " + Catalog.SourceStages(chosen.id); float rw = Mathf.Min(dw - 40, Ui.Width(region, 11, true) + 26);
            Skin.Pill(cx, cy, rw, 26, Skin.Rubber); Ui.FitText(cx + 12, cy, rw - 22, 26, region, 11, Color.white, TextAnchor.MiddleLeft, true); cx += rw + 8;
            float ew = Ui.Width(effectName, 11, true) + 26;
            if (cx + ew <= dx + dw - 20) { Skin.Pill(cx, cy, ew, 26, Ui.Hex(0xFFD27A)); Ui.Text(cx, cy, ew, 26, effectName, 11, Skin.TextDark, TextAnchor.MiddleCenter, true); cx += ew + 8; }
            if (chosen.slot == Slot.Weapon)
            {
                string reach = Catalog.IsGroupWeapon(chosen.id) ? "群伤" : "单体"; float aw = Ui.Width(reach, 11, true) + 26;
                if (cx + aw <= dx + dw - 20) { Skin.PillLine(cx, cy, aw, 26, Skin.TextDark); Ui.Text(cx, cy, aw, 26, reach, 11, Skin.TextDark, TextAnchor.MiddleCenter, true); }
            }
            Ui.WrapText(dx + 20, dy + 136, dw - 40, 40, chosen.effect == Effect.None ? "这款外观对应的装备只提供属性，没有固定模块。" : Loc.Tr("对应装备 ·") + " " + Loc.Tr(Catalog.EffectName(chosen.effect)), 13, Skin.TextDark);
            Ui.WrapText(dx + 20, dy + 178, dw - 40, 36, "外观只改变样子。属性和模块来自「整备」里实际穿戴的装备，分解装备不会丢失已收藏的外观。武器外观只能套在同类武器上（近战 / 远程）。", 10.5f, Skin.TextMid);
            // 所属套装：5 件小图（有的亮、没有的灰）+ 集齐奖励；点小图跳到那件
            int set = SetOfLook(chosen.id);
            if (set >= 0)
            {
                int have = model.SetOwned(set); bool done = have == 5;
                Ui.FitText(dx + 20, dy + 220, dw - 40, 20, "套装 · " + Catalog.SetNames[set] + "（" + have + " / 5）", 12, done ? Ui.Orange : Ui.OrangeInk, TextAnchor.MiddleLeft, true);
                string[] ids = Catalog.SetPieces(set);
                for (int j = 0; j < ids.Length; j++)
                {
                    EquipmentBase pb = Catalog.Find(ids[j]); if (pb == null) continue;
                    bool own = model.state.collection.Contains(pb.id), me = pb.id == chosen.id; float px = dx + 20 + j * 58, py = dy + 246;
                    bool hv = Ui.Hover(px, py, 50, 50);
                    Skin.Round(px, py, 50, 50, own ? Skin.Ink2 : Ui.Hex(0xDAD6CD));
                    if (me || hv) Skin.RoundLine(px - 2, py - 2, 54, 54, me ? Ui.Orange : Skin.TextMid);
                    PaperDollView.DrawEquipmentIcon(pb, Ui.R(px + 5, py + 5, 40, 40), !own);
                    if (Ui.Click(px, py, 50, 50) && !me) { wardrobeSlot = (int)pb.slot; selectedLook = pb.id; wardrobeChapter = Catalog.LookPage(pb.id); }
                }
                Ui.WrapText(dx + 316, dy + 246, dw - 336, 50, done ? "已集齐 · 核心 +" + Balance.SetCores + "、织网碎片 +" + Balance.SetShards + " 已领取"
                    : "已收藏 " + have + " / 5 · 集齐给核心 +" + Balance.SetCores + "、织网碎片 +" + Balance.SetShards, 11, done ? Ui.OrangeInk : Skin.TextDark, TextAnchor.MiddleLeft, true);
            }
            // 按键：应用外观、查看产出
            bool fits = model.LookFits(chosen);   // 武器外观只能套在同类武器上
            string apply = !unlocked ? "尚未收藏" : wearing ? "当前外观" : !fits ? (chosen.attackStyle == AttackStyle.Melee ? "需装备近战武器" : "需装备远程武器") : "应用外观";
            if (Skin.Keycap(dx + 16, dy + dh - 64, 236, 50, apply, Skin.Key.Orange, unlocked && !wearing && fits, 17))
            { model.SetAppearance(chosen.id); Save(); Play(equipSound); Notify("已应用外观：" + chosen.name); }
            if (Skin.Keycap(dx + 262, dy + dh - 64, 150, 50, deep ? "去零号深网" : "查看产出街区", Skin.Key.White, !deep || model.EchoUnlocked, 13))
            { mapChapter = deep ? model.Chapter : Catalog.ChapterOf(chosen.id); navigation.Open(GameModal.Map); }
            if (wearing) Skin.Stamp(dx + dw - 78, dy + 52, "当前外观", Ui.Orange, -10, 13);

            // 定向回收订单内容
            float bx = tx + 18, bw = tw - 36, by = ty + 62;
            Ui.WrapText(bx, by, bw, 52, tracked ? "正在定向回收这款外观" : deep ? "深网限定 · 打到第 " + Catalog.DeepNetFloorOf(chosen.id) + " 层就给" : "想要这一款？设为回收目标", 17, Skin.TextDark, TextAnchor.UpperLeft, true);
            int misses = tracked && model.TargetActive ? model.state.targetBossMisses : 0, guarantee = Balance.TargetBossGuarantee;
            Ui.Text(bx, by + 58, bw, 16, "保底进度", 10, Skin.TextMid, TextAnchor.MiddleLeft, true);
            float step = bw / guarantee;
            for (int i = 0; i < guarantee; i++)
            {
                float pcx = bx + step * (i + .5f), pcy = by + 92;
                Skin.Disk(pcx, pcy + 1, 12, new Color(0, 0, 0, .12f));
                Skin.Disk(pcx, pcy, 12, i < misses ? Ui.Orange : Ui.Hex(0xE4E0D7));
                Skin.Disk(pcx, pcy, 9, i < misses ? Ui.Hex(0xFF7A4A) : Ui.Hex(0xF3F0EA));
                Ui.Text(pcx - 12, pcy + 12, 24, 14, (i + 1).ToString(), 8.5f, Skin.TextLight, TextAnchor.MiddleCenter, true, true);
            }
            string note = deep ? "深网限定外观不会掉落：第一次打过零号深网第 " + Catalog.DeepNetFloorOf(chosen.id) + " 层时直接给（原型品质）。现在最高 " + model.state.echoBest + " 层。"
                : !tracked ? "设为目标后，击败精英或首领时更容易掉落它，连续 " + (guarantee - 1) + " 次未掉落则第 " + guarantee + " 次必得。"
                : model.TargetActive ? "击败精英或首领时更容易掉落。已 " + misses + " 次未掉落，最多再 " + (guarantee - misses) + " 次必得。"
                : "初始装备不会掉落，换一件作为目标吧。";
            Ui.WrapText(bx, by + 124, bw, 120, note, 12, Skin.TextMid);
            if (tracked) Skin.Stamp(tx + tw - 74, ty + th - 104, "PRIORITY", Ui.Orange, -10, 12);
            if (Skin.Keycap(bx, ty + th - 66, bw, 52, tracked ? "取消回收目标" : deep ? "深网限定 · 不能定向" : "设为回收目标", tracked ? Skin.Key.Dark : Skin.Key.Orange, !deep || tracked, 16))
            { model.SetRecoveryTarget(tracked ? null : chosen.id); Save(); }
        }

        // ================= 套装视图 =================
        private void WardrobeSetsV2()
        {
            int ch = Mathf.Clamp(wardrobeChapter, 1, 5);
            const float sx = 588, sw = 980;
            for (int k = 0; k < 5; k++)
            {
                int set = (ch - 1) * 5 + k; float y = 120 + k * 100, h = 90;
                int owned = model.SetOwned(set); bool done = owned == 5;
                Skin.Plastic(sx, y, sw, h, done ? Color.white : new Color(.93f, .925f, .91f, 1));
                if (done) { Skin.Round(sx + 10, y + 12, 6, h - 24, Ui.Orange); }
                Ui.FitText(sx + 26, y + 8, 300, 30, Catalog.SetNames[set], 18, done ? Ui.OrangeInk : Skin.TextDark, TextAnchor.MiddleLeft, true);
                Ui.WrapText(sx + 26, y + 40, 300, 42, done ? "已集齐 · 核心 +" + Balance.SetCores + "、织网碎片 +" + Balance.SetShards + " 已领取"
                    : "已收藏 " + owned + " / 5 · 集齐给核心 +" + Balance.SetCores + "、织网碎片 +" + Balance.SetShards, 11, Skin.TextMid);
                if (done)
                {   // 集齐的套装可以一键换上整套外观（只换外观，不动实际穿的装备）
                    string[] pieces = Catalog.SetPieces(set); bool wearing = true;
                    foreach (string id in pieces) { EquipmentBase pb = Catalog.Find(id); if (pb == null || model.state.appearance[(int)pb.slot] != id) wearing = false; }
                    if (Skin.Keycap(sx + 336, y + 22, 112, 44, wearing ? "外观使用中" : "换上外观", wearing ? Skin.Key.Ghost : Skin.Key.Orange, !wearing, 13))
                    {
                        foreach (string id in pieces) model.SetAppearance(id);
                        EquipmentBase wl = Catalog.Find(pieces[0]); Item worn = model.Equipped(Slot.Weapon); EquipmentBase wb = worn == null ? null : Catalog.Find(worn.baseId);
                        bool mismatch = wl != null && wb != null && wl.attackStyle != wb.attackStyle;
                        Save(); Play(equipSound);
                        Notify(mismatch ? "已换上套装外观「" + Catalog.SetNames[set] + "」。武器外观要换成同类武器（近战 / 远程）才显示。" : "已换上套装外观「" + Catalog.SetNames[set] + "」", mismatch ? 5 : 3);
                    }
                }
                string[] ids = Catalog.SetPieces(set);
                for (int j = 0; j < 5; j++)
                {
                    EquipmentBase b = Catalog.Find(ids[j]); if (b == null) continue;
                    bool has = model.state.collection.Contains(b.id); float x = sx + 466 + j * 102, hy = y + 6;
                    bool hover = Ui.Hover(x, hy, 94, h - 12);
                    Skin.Round(x, hy, 94, h - 12, has ? Skin.Ink2 : Ui.Hex(0xD9D5CC));
                    if (hover) Skin.RoundLine(x - 2, hy - 2, 98, h - 8, Ui.Orange);
                    PaperDollView.DrawEquipmentIcon(b, Ui.R(x + 25, hy + 3, 44, 44), !has);
                    Ui.FitText(x + 4, hy + 50, 86, 24, b.name, 10, has ? Color.white : Skin.TextMid, TextAnchor.MiddleCenter, true);
                    if (Ui.Click(x, hy, 94, h - 12)) { wardrobeSlot = (int)b.slot; selectedLook = b.id; wardrobeSets = false; }   // 点一件：跳到这件的外观详情
                }
            }
            // 称号：集齐一章的 5 套解锁，显示在整备页角色脚下
            const float ty = 628;
            Skin.Paper(sx + 12, ty, sw - 24, 236);
            Ui.Fill(sx + 26, ty + 14, 4, 20, Ui.Orange);
            Skin.Spaced(sx + 38, ty + 16, 60, 16, "TITLE", 8, Skin.TextLight);
            Ui.FitText(sx + 104, ty + 14, 120, 20, "称号", 12, Skin.TextDark, TextAnchor.MiddleLeft, true);
            Ui.FitText(sx + 26, ty + 38, sw - 64, 20, "集齐一章的全部 5 套就解锁那一章的称号，显示在整备页的角色脚下。", 11, Skin.TextMid, TextAnchor.MiddleLeft);
            for (int c = 1; c <= 5; c++)
            {
                float x = sx + 26 + (c - 1) * 186, y = ty + 68, w = 174, h = 156;
                bool open = model.TitleUnlocked(c), using_ = model.state.title == c && open;
                int sets = 0; for (int k = 0; k < 5; k++) if (model.SetComplete((c - 1) * 5 + k)) sets++;
                Skin.Round(x, y, w, h, using_ ? Skin.Ink2 : open ? Ui.Hex(0xF1EEE7) : Ui.Hex(0xDAD6CD));
                Ui.FitText(x + 12, y + 8, w - 24, 18, "第" + Catalog.ChapterNumber(c) + "章 · " + sets + " / 5 套", 10.5f, using_ ? Ui.Hex(0xB9BEC0) : Skin.TextMid, TextAnchor.MiddleLeft, true);
                Ui.WrapText(x + 12, y + 30, w - 24, 56, open ? Catalog.TitleNames[c - 1] : "？？？", 16, using_ ? Ui.Orange : open ? Skin.TextDark : Skin.TextLight, TextAnchor.UpperLeft, true);
                if (using_) { if (Skin.Keycap(x + 12, y + h - 50, w - 24, 38, "取下", Skin.Key.Dark, true, 13)) { model.state.title = 0; Save(); } }
                else if (Skin.Keycap(x + 12, y + h - 50, w - 24, 38, open ? "使用" : "未解锁", Skin.Key.Orange, open, 13)) { model.state.title = c; Save(); Play(equipSound); }
            }
        }
    }
}
