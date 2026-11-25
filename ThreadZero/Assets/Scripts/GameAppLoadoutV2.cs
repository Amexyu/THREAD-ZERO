using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ThreadZero
{
    /// 11-24 v0.29 整备页新皮肤（例图「回收柜」）：左边储物柜（义体、穿着的五件挂在洞洞板上、属性、改造板），
    /// 中间回收柜（部位按键、排序筛选、三层挂杆上的收藏卡、底部回收口），右边夹板上的「装备鉴定单」。
    /// 功能和旧版一样，F4 在新旧之间切换（UiV2）。
    public sealed partial class GameApp
    {
        private void LoadoutPageV2()
        {
            // 义体升级卡片盖在储物柜上面，最后画；鼠标在卡片上时先把下面的按钮都屏蔽掉（IMGUI 先画的先吃到点击）
            bool before = Ui.Blocked;
            bool card = implantPick >= 0 && model.ImplantLevel(implantPick) > 0;
            if (card && Ui.Hover(ImplantCardX, ImplantCardY, 340, 232)) Ui.Blocked = true;
            implantTipTitle = null;
            Skin.PageBackground();
            LockerV2();
            CabinetV2();
            InspectV2(FindItem(selectedItem));
            Ui.Blocked = before;
            if (card) ImplantCard(implantPick, ImplantCardX, ImplantCardY);
            else if (implantTipTitle != null)
            {
                Ui.Fill(implantTipX, ImplantCardY, 280, 70, Ui.Ink); Ui.Fill(implantTipX, ImplantCardY, 5, 70, Ui.Orange);
                Ui.FitText(implantTipX + 14, ImplantCardY + 6, 256, 24, implantTipTitle, 15, Ui.Panel, TextAnchor.MiddleLeft, true);
                Ui.WrapText(implantTipX + 14, ImplantCardY + 30, 256, 36, implantTipBody, 12, Ui.MuteOnInk);
            }
        }
        private const float ImplantCardX = 144, ImplantCardY = 132;
        private string implantTipTitle, implantTipBody; private float implantTipX;

        // ================= 左：储物柜 =================
        private void LockerV2()
        {
            const float lx = 128, ly = 16, lw = 400, lh = 588;
            Skin.Plastic(lx, ly, lw, lh);
            Skin.Screw(lx + 14, ly + 14); Skin.Screw(lx + lw - 14, ly + 14);
            Ui.FitText(lx + 22, ly + 10, 150, 42, "整备", 30, Skin.TextDark, TextAnchor.MiddleLeft, true);
            Skin.Spaced(lx + 24, ly + 50, 90, 14, "LOADOUT", 8.5f, Skin.TextMid);
            Ui.FitText(lx + 112, ly + 48, lw - 168 - 112 - 6, 18, model.LoadoutSummary, 10, Skin.TextMid, TextAnchor.MiddleLeft, true);   // 打法概要（旧版右上角那块）
            Skin.Lcd(lx + lw - 168, ly + 18, 150, 38, Thousands(PowerShown), "战力", Ui.Orange, 20);
            ImplantRowV2(lx + 20, ly + 70);

            float rx = lx + 16, ry = ly + 112, rw = lw - 32, rh = lh - 128;
            Skin.Recess(rx, ry, rw, rh);
            Skin.Tile("tile_pegboard", rx + 4, ry + 14, rw - 8, rh - 18, 15, 15, Color.white);
            Ui.Text(rx, ry + 120, rw, 80, "LOADOUT", 54, new Color(1, 1, 1, .28f), TextAnchor.MiddleCenter, true, true);
            Skin.Led(rx + 22, ry + 12, rw - 44, Ui.Orange);
            Skin.Glow(rx + rw / 2, ry + 26, 170, Ui.Orange, .14f);
            // 平台 + 主角
            float cx = lx + lw / 2, py = ry + rh - 46;
            Skin.Glow(cx, py, 96, Ui.Orange, .42f);
            Skin.Sprite("platform", cx - 100, py - 20, 200, 44, Color.white);
            PreviewHero(cx + 4, py + 6, 330, cx - 150, py - 300, 300, 300);
            string title = model.TitleName;
            if (title != null)
            {
                string t = "「" + title + "」"; float tw = Ui.Width(t, 10.5f, true) + 26;
                Skin.Tag(cx - tw / 2, py + 26, tw, 22, Skin.Rubber, t, 0, 10.5f, Ui.Orange);
            }
            // 穿着的五件：左三右二，挂在洞洞板的挂钉上
            float[] px = { rx + 12, rx + 12, rx + 12, rx + rw - 98, rx + rw - 98 }, pyy = { ry + 40, ry + 180, ry + 320, ry + 40, ry + 180 };
            for (int i = 0; i < Catalog.SlotCount; i++)
            {
                float x = px[i], y = pyy[i]; Item item = model.Equipped((Slot)i);
                Skin.Peg(x + 43, y - 30);
                if (item == null)
                {   // 空位：虚线框 + 部位名
                    DashedBox(x, y, 86, 116, Ui.Hex(0x5A564F));
                    Ui.FitText(x + 4, y + 36, 78, 22, Catalog.SlotNames[i], 13, Skin.TextMid, TextAnchor.MiddleCenter, true);
                    Ui.FitText(x + 4, y + 58, 78, 18, "空位", 10, Skin.TextLight, TextAnchor.MiddleCenter);
                    continue;
                }
                EquipmentBase b = Catalog.Find(item.baseId);
                bool hover = Ui.Hover(x, y, 86, 116);
                var look = Look(); look.small = true; look.level = item.level; look.max = item.level >= model.License; look.selected = selectedItem == item.uid; look.hover = hover;
                SkinCard(x, y, 86, 116, b, item.quality, b.name, look);
                if (Ui.Click(x, y, 86, 116)) { ResetInventoryFilters(); SelectItem(item); slotFilter = i; inventoryScroll = 0; }
            }
            // 第六格：芯片 / 改装（去工坊装备台）
            float sx = rx + rw - 98, sy = ry + 320; bool over = Ui.Hover(sx, sy, 86, 116);
            DashedBox(sx, sy, 86, 116, over ? Ui.Orange : Ui.Hex(0x5A564F));
            Ui.Text(sx, sy + 30, 86, 24, "芯片 · 改装", 11.5f, over ? Ui.Orange : Skin.TextMid, TextAnchor.MiddleCenter, true);
            Ui.Text(sx, sy + 54, 86, 36, "去工坊\n装备台", 9.5f, Skin.TextLight, TextAnchor.MiddleCenter);
            Skin.Tape(sx + 20, sy - 8, 46, 14, -4);
            if (Ui.Click(sx, sy, 86, 116)) { workshopTab = 0; Go(GameScreen.Workshop); }

            // 属性（深色拉丝金属）
            const float sy0 = 620;
            Skin.Metal(lx, sy0, lw, 152);
            float[] totals = Values(model.Total());
            for (int i = 0; i < 6; i++)
            {
                float x = lx + 22 + (i % 3) * 128, y = sy0 + 14 + (i / 3) * 70;
                string value = i == 3 ? (1 + totals[i]).ToString("F2") : i == 4 ? (totals[i] * 100).ToString("F0") + "%" : totals[i].ToString("F0");
                Skin.Gem(x + 4, y + 9, 3.6f, i < 3 ? Ui.Orange : Ui.Hex(0xB9BEC0), true);
                Ui.Text(x + 14, y, 110, 18, i == 3 ? "每秒攻击" : Catalog.StatNames[i], 11, Ui.Hex(0xB9BEC0), TextAnchor.MiddleLeft, true);
                Skin.OutlineText(x, y + 18, 120, 40, value, Skin.Fit(value, 30, 116, true, true), Color.white, Ui.Hex(0x0B0C0D), TextAnchor.MiddleLeft, true, true, 1);
                if (i % 3 < 2) Ui.Fill(x + 120, y + 4, 1.2f, 50, new Color(1, 1, 1, .1f));
            }
            Ui.Fill(lx + 16, sy0 + 82, lw - 32, 1.2f, new Color(1, 1, 1, .1f));
            // 改造板
            const float by0 = 788;
            Skin.Plastic(lx, by0, lw, 62);
            Ui.Text(lx + 18, by0 + 6, 100, 24, "改造板", 13, Skin.TextDark, TextAnchor.MiddleLeft, true);
            for (int bi = 0; bi < 3; bi++)
            {   // 分支名量了宽度再接等级（英文 Firepower 比两个汉字宽得多）
                float bx = lx + 18 + bi * 86, bw = Mathf.Min(48, Ui.Width(Catalog.BoardBranches[bi], 10, true));
                Ui.FitText(bx, by0 + 32, 48, 20, Catalog.BoardBranches[bi], 10, Skin.TextMid, TextAnchor.MiddleLeft, true);
                Ui.Text(bx + bw + 5, by0 + 30, 40, 24, "Lv." + model.state.board[bi], 14, Skin.TextDark, TextAnchor.MiddleLeft, true, true);
            }
            if (Skin.Keycap(lx + lw - 122, by0 + 11, 106, 40, "改造板 ›", Skin.Key.Orange, true, 14)) { workshopTab = 1; Go(GameScreen.Workshop); }
        }

        private static void DashedBox(float x, float y, float w, float h, Color color)
        {
            for (float k = 0; k < h - 2; k += 9) { Ui.Fill(x, y + k, 2, Mathf.Min(5, h - k), color); Ui.Fill(x + w - 2, y + k, 2, Mathf.Min(5, h - k), color); }
            for (float k = 0; k < w - 2; k += 9) { Ui.Fill(x + k, y, Mathf.Min(5, w - k), 2, color); Ui.Fill(x + k, y + h - 2, Mathf.Min(5, w - k), 2, color); }
        }

        /// 义体：五个圆形接口一排（装上的是橙色，带等级；能升级亮小点），点开升级卡片（卡片和悬停说明在 LoadoutPageV2 最后画）。
        private void ImplantRowV2(float x, float y)
        {
            int have = model.state.implants.Count;
            for (int i = 0; i < Catalog.AscensionTotal; i++)
            {
                ImplantDef def = i < Catalog.Implants.Length ? Catalog.Implants[i] : null;
                float cx = x + 16 + i * 38, cy = y + 18; bool on = def != null && model.state.implants.Contains(def.id), hover = Ui.Hover(cx - 16, cy - 16, 32, 32);
                Skin.Disk(cx, cy + 1, 16, new Color(0, 0, 0, .25f));
                Skin.Disk(cx, cy, 16, on ? Skin.Ink2 : Ui.Hex(0xBDB8AE));
                Skin.Disk(cx, cy, 13.5f, on ? Ui.Orange : Ui.Hex(0xD9D5CC));
                Ui.Text(cx - 16, cy - 16, 32, 32, Loc.Get() == Loc.Lang.Zh ? ImplantParts[i].Substring(0, 1) : "AESHP".Substring(i % 5, 1), 13, on ? Color.white : Ui.Hex(0x8E8A82), TextAnchor.MiddleCenter, true);   // 其它语言用字母：手臂 Arm、义眼 Eye、脊柱 Spine、心脏 Heart、接口 Port
                if (on)
                {
                    int lv = model.ImplantLevel(i);
                    Skin.Pill(cx + 4, cy + 6, 18, 13, Skin.Ink2); Ui.Text(cx + 4, cy + 6, 18, 13, lv.ToString(), 8.5f, Ui.Orange, TextAnchor.MiddleCenter, true, true);
                    if (model.CanUpgradeImplant(i)) { Skin.Disk(cx + 12, cy - 12, 4.5f, Color.white); Skin.Disk(cx + 12, cy - 12, 3.2f, Ui.Orange); }
                    if (Ui.Click(cx - 16, cy - 16, 32, 32)) implantPick = implantPick == i ? -1 : i;
                    if (def.id == implantNew) Skin.Sticker(cx + 10, cy - 14, 9, Ui.Orange, "NEW", -12, 6, Color.white);
                }
                if (hover)
                {
                    implantTipTitle = on ? def.name : "未解锁 · " + ImplantParts[i];
                    implantTipBody = on ? def.desc + "（点击升级）" : "击败第 " + (i + 1) + " 章的首领后装上。";
                    implantTipX = Mathf.Min(cx - 16, 528 - 280);
                }
            }
            // 右边：飞升度 / 阶段，下一行织网碎片（升级义体用）
            float lx = x + Catalog.AscensionTotal * 38 + 4, lw = 528 - 20 - lx;
            Ui.FitText(lx, y, lw, 18, "机械飞升 " + (model.Ascension * 100).ToString("0") + "%", 12, have > 0 ? Ui.Orange : Skin.TextMid, TextAnchor.MiddleLeft, true);
            if (model.AscensionStage >= Catalog.AscensionStageAt.Length)
            {   // 飞升完成以后可以重看尾声
                bool hv = Ui.Hover(lx, y + 19, lw, 16);
                Ui.FitText(lx, y + 19, lw, 16, "重看尾声 ›", 10, hv ? Skin.TextDark : Ui.Orange, TextAnchor.MiddleLeft, true);
                if (Ui.Click(lx, y + 19, lw, 16)) OpenEpilogue();
            }
            else if (model.AscensionStage > 0) Ui.FitText(lx, y + 19, lw, 16, Catalog.AscensionStageNames[model.AscensionStage - 1], 10, Skin.TextMid, TextAnchor.MiddleLeft);
            else if (have > 0) Ui.FitText(lx, y + 19, lw, 16, "织网碎片 " + model.state.shards, 10, Skin.TextMid, TextAnchor.MiddleLeft, true);
        }

        // ================= 中：回收柜 =================
        private void CabinetV2()
        {
            const float cx0 = 540, cy0 = 16, cw = 600, ch = 860;
            Skin.Plastic(cx0, cy0, cw, ch);
            Skin.Screw(cx0 + 14, cy0 + 14); Skin.Screw(cx0 + cw - 14, cy0 + 14); Skin.Screw(cx0 + 14, cy0 + ch - 14); Skin.Screw(cx0 + cw - 14, cy0 + ch - 14);
            // 部位按键（数量放在键帽上的小胶囊里）。先量一遍自然宽度，译文长放不下就按比例收窄（字用 FitText 缩）
            var counts = new string[Catalog.SlotCount + 1]; var widths = new float[Catalog.SlotCount + 1]; float natural = 0;
            for (int f = -1; f < Catalog.SlotCount; f++)
            {
                int slot = f;
                counts[f + 1] = model.state.items.Count(it => slot < 0 || (int)Catalog.Find(it.baseId).slot == slot).ToString();
                widths[f + 1] = Mathf.Max(78, Ui.Width(f < 0 ? "全部" : Catalog.SlotNames[f], 14, true) + Ui.Width(counts[f + 1], 10, true, true) + 42);
                natural += widths[f + 1];
            }
            float squeeze = Mathf.Min(1, (cw - 44 - 6 * Catalog.SlotCount) / natural), x = cx0 + 22;
            for (int f = -1; f < Catalog.SlotCount; f++)
            {
                bool on = slotFilter == f;
                string label = f < 0 ? "全部" : Catalog.SlotNames[f], count = counts[f + 1];
                float cwid = Ui.Width(count, 10, true, true) + 12, kw = widths[f + 1] * squeeze;
                if (Skin.Keycap(x, cy0 + 18, kw, 44, "", on ? Skin.Key.Orange : Skin.Key.White, true, 14, on)) { slotFilter = f; inventoryScroll = 0; }
                float oy = on ? 3 : 0;
                Ui.FitText(x + 12, cy0 + 18 + oy, kw - cwid - 26, 39.5f - oy, label, 14, on ? Color.white : Skin.TextDark, TextAnchor.MiddleLeft, true);
                Skin.Pill(x + kw - cwid - 10, cy0 + 18 + oy + 12, cwid, 16, on ? new Color(0, 0, 0, .28f) : Skin.Rubber);
                Ui.Text(x + kw - cwid - 10, cy0 + 18 + oy + 12, cwid, 16, count, 10, Color.white, TextAnchor.MiddleCenter, true, true);
                x += kw + 6;
            }
            // 工具条：排序、顺序、筛选、清空；右边零件液晶
            float ty = cy0 + 70;
            string sortLabel = "排序：" + InventorySortNames[(int)inventoryQuery.sort];
            if (Skin.Keycap(cx0 + 22, ty, 140, 34, sortLabel, Skin.Key.White, true, 12, inventorySortOpen)) { inventorySortOpen = !inventorySortOpen; inventoryFiltersOpen = false; inventoryScroll = 0; }
            string order = inventoryQuery.sort == InventorySort.Acquired ? (inventoryQuery.descending ? "最新优先 ↓" : "最早优先 ↑") : (inventoryQuery.descending ? "从高到低 ↓" : "从低到高 ↑");
            if (Skin.Keycap(cx0 + 168, ty, 104, 34, order, Skin.Key.White, true, 12)) { inventoryQuery.descending = !inventoryQuery.descending; inventoryScroll = 0; }
            string filter = "筛选" + (inventoryQuery.ActiveCount > 0 ? "（" + inventoryQuery.ActiveCount + "）" : "") + (inventoryFiltersOpen ? " −" : " +");
            if (Skin.Keycap(cx0 + 278, ty, 92, 34, filter, inventoryQuery.ActiveCount > 0 ? Skin.Key.Dark : Skin.Key.White, true, 12, inventoryFiltersOpen)) { inventoryFiltersOpen = !inventoryFiltersOpen; inventorySortOpen = false; inventoryScroll = 0; }
            if (Skin.Keycap(cx0 + 376, ty, 60, 34, "清空", Skin.Key.White, true, 12)) { ResetInventoryFilters(); slotFilter = -1; }
            Skin.Lcd(cx0 + cw - 156, ty - 1, 134, 36, Thousands(model.state.scrap), "零件", Ui.Orange, 17);

            // 内腔
            float ix = cx0 + 14, iy = cy0 + 114, iw = cw - 28, ih = ch - 114 - 60;
            Skin.Recess(ix, iy, iw, ih);
            Skin.Sprite("fx_halftone_big", ix, iy + ih - 300, 400, 300, new Color(0, 0, 0, .1f));
            Skin.Led(ix + 24, iy + 11, iw - 48, Ui.Orange);
            Skin.Glow(ix + iw / 2, iy + 20, 280, Ui.Orange, .12f);
            float gridTop = iy + 22;
            if (inventorySortOpen || inventoryFiltersOpen) gridTop = CabinetDrawerV2(ix + 10, iy + 24, iw - 20) + 8;

            inventoryQuery.slot = slotFilter;
            List<Item> items = inventoryQuery.Apply(model.state);
            const float cardW = 100, cardH = 188, gap = 8, rowStep = 212;
            float viewX = ix + 4, viewW = iw - 24, viewH = iy + ih - 6 - gridTop;
            int rows = (items.Count + 4) / 5;
            float content = rows * rowStep + 20, limit = Mathf.Max(0, content - viewH);
            bool inside = Ui.Hover(viewX, gridTop, viewW, viewH);
            if (inside && Event.current.type == EventType.ScrollWheel) { inventoryScroll += Event.current.delta.y * 36; Event.current.Use(); }
            inventoryScroll = Mathf.Clamp(inventoryScroll, 0, limit);
            if (items.Count == 0) Ui.Text(ix, gridTop + 20, iw, 140, "没有符合条件的装备。\n可修改条件或点击「清空筛选」。", 15, Skin.TextMid, TextAnchor.MiddleCenter, true);

            Ui.BeginClip(viewX, gridTop, viewW, viewH, inventoryScroll);
            for (int r = 0; r < rows; r++)
            {
                float railY = gridTop + 20 + r * rowStep;
                if (railY + rowStep < gridTop + inventoryScroll - 30 || railY - 40 > gridTop + inventoryScroll + viewH) continue;
                Skin.Rail(ix + 8, railY, iw - 36);
                for (int c = 0; c < 5; c++)
                {
                    int i = r * 5 + c; if (i >= items.Count) break;
                    Item item = items[i]; EquipmentBase b = Catalog.Find(item.baseId);
                    float tx = ix + 12 + c * (cardW + gap), cy = railY + 22;
                    Skin.Hook(tx + cardW / 2, railY + 6);
                    bool hover = inside && Ui.Hover(tx, cy, cardW, cardH);
                    var look = Look();
                    look.level = item.level; look.max = item.level >= model.License; look.power = Thousands(ItemScore(item));
                    look.model = "TZ-" + item.baseId.ToUpperInvariant();
                    look.equipped = model.state.equipped.Contains(item.uid); look.isNew = !look.equipped && unseen.Contains(item.uid);
                    look.selected = selectedItem == item.uid; look.locked = item.locked; look.hover = hover;
                    SkinCard(tx, cy, cardW, cardH, b, item.quality, b.name, look);
                    if (inside && Ui.Click(tx, cy - 8, cardW, cardH + 8)) SelectItem(item);
                }
            }
            Ui.EndClip();
            // 滚动槽
            float sx = ix + iw - 17;
            Skin.Pill(sx, gridTop + 8, 9, viewH - 16, Ui.Hex(0x2A2B2E));
            if (limit > 0)
            {
                float track = viewH - 20, thumb = Mathf.Max(48, track * viewH / content), ty2 = gridTop + 10 + (track - thumb) * inventoryScroll / limit;
                Skin.Pill(sx - 2, ty2, 13, thumb, Color.white); Skin.Pill(sx - .5f, ty2 + 1.5f, 10, thumb - 3, Ui.Orange);
            }
            // 底部回收口：一键分解
            float sy4 = cy0 + ch - 52;
            Skin.Hazard(ix + 4, sy4 + 4, iw - 196, 34);
            Skin.Pill(ix + 14, sy4 + 12, iw - 216, 18, Ui.Hex(0x121315));
            Ui.FitText(ix + 24, sy4 + 12, iw - 236, 18, "显示 " + items.Count + " / " + model.state.items.Count + " 件 · 同值优先等级、品质、最近获得", 9.5f, Ui.Hex(0xB9BEC0), TextAnchor.MiddleLeft, true);
            if (Skin.Keycap(ix + iw - 180, sy4, 176, 42, "一键分解", Skin.Key.Orange, true, 15)) navigation.Open(GameModal.Salvage);
        }

        /// 排序 / 筛选抽屉（压在柜子里最上面），返回下沿 y。
        private float CabinetDrawerV2(float x, float y, float w)
        {
            float h = inventorySortOpen ? 128 : 214;
            Skin.Paper(x, y, w, h);
            if (inventorySortOpen)
            {
                float bw = (w - 24 - 16) / 3;
                for (int i = 0; i < InventorySortNames.Length; i++)
                    if (Skin.Keycap(x + 12 + i % 3 * (bw + 8), y + 12 + i / 3 * 38, bw, 30, InventorySortNames[i], (int)inventoryQuery.sort == i ? Skin.Key.Dark : Skin.Key.White, true, 12, (int)inventoryQuery.sort == i))
                    { inventoryQuery.sort = (InventorySort)i; inventorySortOpen = false; inventoryScroll = 0; }
                return y + h;
            }
            Ui.Text(x + 12, y + 10, 50, 30, "名称", 12, Skin.TextMid, TextAnchor.MiddleLeft, true);
            string search = InventoryInput(x + 60, y + 10, w - 72, inventoryQuery.search, false);
            if (search != inventoryQuery.search) { inventoryQuery.search = search; inventoryScroll = 0; }
            Ui.Text(x + 12, y + 46, 50, 30, "品质", 12, Skin.TextMid, TextAnchor.MiddleLeft, true);
            float qw = (w - 72 - 6 * 4) / 7;
            for (int i = 0; i < Catalog.QualityNames.Length; i++)
            {
                bool on = (inventoryQuery.qualityMask & 1 << i) != 0;
                if (Skin.Keycap(x + 60 + i * (qw + 4), y + 46, qw, 28, Catalog.QualityNames[i], on ? Skin.Key.Dark : Skin.Key.White, true, 11, on)) { inventoryQuery.qualityMask ^= 1 << i; inventoryScroll = 0; }
                Ui.Fill(x + 60 + i * (qw + 4) + 6, y + 46 + (on ? 3 : 0) + 4, qw - 12, 2, QualityVisual.Colors[i]);
            }
            Ui.Text(x + 12, y + 82, 50, 30, "等级", 12, Skin.TextMid, TextAnchor.MiddleLeft, true);
            string low = InventoryInput(x + 60, y + 82, 64, minimumLevel, true), high = InventoryInput(x + 150, y + 82, 64, maximumLevel, true);
            Ui.Text(x + 128, y + 82, 20, 30, "—", 12, Skin.TextMid, TextAnchor.MiddleCenter);
            if (low != minimumLevel || high != maximumLevel) { minimumLevel = low; maximumLevel = high; inventoryScroll = 0; }
            int parsed;
            inventoryQuery.minLevel = int.TryParse(minimumLevel, out parsed) ? parsed : 1;
            inventoryQuery.maxLevel = int.TryParse(maximumLevel, out parsed) ? parsed : Balance.MaxItemLevel;
            Ui.FitText(x + 226, y + 82, w - 238, 30, inventoryQuery.minLevel > inventoryQuery.maxLevel ? "最低等级不能大于最高等级" : "留空不限 · 1–" + Balance.MaxItemLevel, 11, inventoryQuery.minLevel > inventoryQuery.maxLevel ? Ui.OrangeInk : Skin.TextMid, TextAnchor.MiddleLeft);
            float sw = (w - 24 - 4 * 6) / 5;
            for (int i = 0; i < 5; i++)
            {
                bool on = (int)inventoryQuery.status == i;
                if (Skin.Keycap(x + 12 + i * (sw + 6), y + 120, sw, 28, InventoryStatusNames[i], on ? Skin.Key.Dark : Skin.Key.White, true, 11, on)) { inventoryQuery.status = (InventoryStatus)i; inventoryScroll = 0; }
            }
            string affix = inventoryQuery.affix < 0 ? "全部随机词条" : Catalog.StatNames[inventoryQuery.affix] + "随机词条";
            if (Skin.Keycap(x + 12, y + 156, (w - 32) / 2, 28, affix + " ›", Skin.Key.White, true, 11)) { inventoryQuery.affix = (inventoryQuery.affix + 2) % 7 - 1; inventoryScroll = 0; }
            string effect = inventoryQuery.effect < 0 ? "全部固定效果" : Catalog.EffectName((Effect)inventoryQuery.effect).Split('：')[0];
            if (Skin.Keycap(x + 20 + (w - 32) / 2, y + 156, (w - 32) / 2, 28, effect + " ›", Skin.Key.White, true, 11)) { inventoryQuery.effect = (inventoryQuery.effect + 2) % (Enum.GetValues(typeof(Effect)).Length + 1) - 1; inventoryScroll = 0; }
            Ui.FitText(x + 12, y + 190, w - 24, 18, "品质可多选；不同条件同时满足。词条／效果按钮点击切换。", 10, Skin.TextLight, TextAnchor.MiddleLeft);
            return y + h;
        }

        // ================= 右：装备鉴定单 =================
        private void InspectV2(Item item)
        {
            const float rx0 = 1152, ry0 = 16, rw0 = 416, rh0 = 860;
            Skin.Plastic(rx0, ry0, rw0, rh0, new Color(.985f, .975f, .955f, 1));
            const float px0 = rx0 + 16, py0 = ry0 + 46, pw0 = rw0 - 32, ph0 = 620;
            Skin.Paper(px0, py0, pw0, ph0);
            for (float k = 0; k < pw0 - 12; k += 12) Skin.Disk(px0 + 10 + k, py0 + 8, 2.2f, Ui.Hex(0xD3CFC6));
            Skin.Spaced(px0 + 14, py0 + 16, 150, 14, "INSPECTION SHEET", 6.8f, Skin.TextLight);
            string sheet = item != null ? "No." + (Mathf.Abs(item.uid.GetHashCode()) % 9000 + 1000) : "";
            if (Loc.Get() == Loc.Lang.Zh) sheet = "装备鉴定单" + (sheet.Length > 0 ? " · " + sheet : "");   // 其它语言左边的 INSPECTION SHEET 已经够了
            Ui.FitText(px0 + 150, py0 + 16, 140, 14, sheet, 9, Skin.TextLight, TextAnchor.MiddleLeft, true);
            Skin.Barcode(px0 + pw0 - 92, py0 + 15, 78, 12, Skin.TextDark);
            // 夹板的金属夹
            float clx = rx0 + rw0 / 2;
            Skin.Nine("key_white", clx - 78, ry0 + 12, 156, 52, 26, new Color(.86f, .88f, .9f, 1));
            Skin.Screw(clx - 52, ry0 + 40); Skin.Screw(clx + 52, ry0 + 40);
            Skin.Spaced(clx - 26, ry0 + 32, 60, 14, "TZ-04", 7, Ui.Hex(0x5A5F66));

            if (item == null)
            {
                Ui.Text(px0, py0 + 40, pw0, ph0 - 80, "选择一件装备\n查看属性与替换效果", 17, Skin.TextMid, TextAnchor.MiddleCenter, true);
                return;
            }
            EquipmentBase b = Catalog.Find(item.baseId);
            Color qc = QualityVisual.Colors[(int)item.quality];
            bool equippedNow = model.state.equipped.Contains(item.uid);
            // 大卡（胶带贴着，略歪）
            var look = Look();
            look.level = item.level; look.max = item.level >= model.License; look.power = Thousands(ItemScore(item));
            look.model = "TZ-" + item.baseId.ToUpperInvariant(); look.equipped = equippedNow; look.nameSize = 14;
            SkinCard(px0 + 14, py0 + 48, 160, 244, b, item.quality, b.name, look, 2);
            Skin.Tape(px0 + 2, py0 + 42, 54, 15, 28); Skin.Tape(px0 + 128, py0 + 280, 54, 15, 24);
            // 右边：标签、名字、技能、固定效果、专属
            float tx = px0 + 190, tw = pw0 - 190 - 12, yy = py0 + 50;
            float chipX = tx;
            string qn = Catalog.QualityNames[(int)item.quality]; float qw = Ui.Width(qn, 10.5f, true) + 18;
            Skin.Pill(chipX, yy, qw, 20, qc); Ui.Text(chipX, yy, qw, 20, qn, 10.5f, Skin.Ink2, TextAnchor.MiddleCenter, true); chipX += qw + 6;
            string sn = Catalog.SlotNames[(int)b.slot]; float sw2 = Ui.Width(sn, 10, true) + 18;
            Skin.Pill(chipX, yy, sw2, 20, Skin.Rubber); Ui.Text(chipX, yy, sw2, 20, sn, 10, Color.white, TextAnchor.MiddleCenter, true); chipX += sw2 + 6;
            if (b.slot == Slot.Weapon)
            {
                string reach = Catalog.IsGroupWeapon(b.id) ? "群伤" : "单体"; float rw2 = Ui.Width(reach, 10, true) + 18;
                if (chipX + rw2 <= tx + tw) { Skin.Pill(chipX, yy, rw2, 20, Skin.Rubber); Ui.Text(chipX, yy, rw2, 20, reach, 10, Color.white, TextAnchor.MiddleCenter, true); }
            }
            yy += 26;
            Ui.FitText(tx, yy, tw, 32, b.name, 22, Skin.TextDark, TextAnchor.MiddleLeft, true);
            yy += 32;
            Ui.FitText(tx, yy, tw, 14, "TZ-" + item.baseId.ToUpperInvariant() + " · Lv." + item.level, 8.5f, Skin.TextLight, TextAnchor.MiddleLeft, true, true);
            yy += 22;
            SkillDef skill = b.slot == Slot.Weapon ? SkillBook.ForWeapon(item) : null;
            if (skill != null)
            {   // 第一行：「技能」+ 发动条件（右对齐），第二行技能名，下面要点做成一排小标签（放不下换行）
                Ui.Text(tx + 10, yy, 60, 14, "技能", 9.5f, Skin.TextLight, TextAnchor.MiddleLeft, true);
                float lab = Ui.Width("技能", 9.5f, true) + 16;
                Ui.FitText(tx + 10 + lab, yy, tw - 10 - lab, 14, SkillWhen(skill), 9.5f, Skin.TextMid, TextAnchor.MiddleRight);
                Ui.FitText(tx + 10, yy + 14, tw - 10, 22, skill.displayName, 15, Ui.Orange, TextAnchor.MiddleLeft, true);
                float chx = tx + 10, chy = yy + 38;
                foreach (string part in SkillParts(skill))
                {
                    float pw = Mathf.Min(tw - 10, Ui.Width(part, 9, true) + 12);
                    if (chx + pw > tx + tw && chx > tx + 10) { chx = tx + 10; chy += 17; }
                    Skin.Pill(chx, chy, pw, 15, new Color(0, 0, 0, .07f));
                    Ui.FitText(chx + 4, chy, pw - 8, 15, part, 9, Skin.TextMid, TextAnchor.MiddleCenter, true);
                    chx += pw + 4;
                }
                Ui.Fill(tx, yy + 2, 4, chy + 15 - yy - 2, Ui.Orange);
                yy = chy + 15 + 10;
            }
            // 固定效果：一行「固定效果 · 名字」，下面两行说明
            string effectName, effectText; SplitEffect(b.effect, out effectName, out effectText);
            Ui.Fill(tx, yy + 2, 4, 40, Skin.TextDark);
            Ui.Text(tx + 10, yy, 60, 16, "固定效果", 9.5f, Skin.TextLight, TextAnchor.MiddleLeft, true);
            float lab2 = Ui.Width("固定效果", 9.5f, true) + 16;
            Ui.FitText(tx + 10 + lab2, yy, tw - 10 - lab2, 16, effectName, 12, Skin.TextDark, TextAnchor.MiddleLeft, true);
            Ui.WrapText(tx + 10, yy + 17, tw - 10, 26, effectText, 9.5f, Skin.TextMid);
            yy += 50;
            for (int ti = 0; ti < 2; ti++)
            {   // 专属词条（机密、原型各一条，零号两条）
                int tal = ti == 0 ? item.talent : item.talent2; if (tal <= 0) continue;
                Ui.Fill(tx, yy + 2, 4, 34, qc);
                Ui.FitText(tx + 10, yy, tw - 10, 14, "专属 · " + Catalog.TalentNames[tal], 10, QualityVisual.Ink(item.quality), TextAnchor.MiddleLeft, true);
                Ui.WrapText(tx + 10, yy + 14, tw - 10, 24, Catalog.TalentTexts[tal], 9.5f, Skin.TextDark);
                yy += 40;
            }

            // 属性表（和旧版同样的数据：对比当前穿戴，◆ 是随机词条，短条是成色）
            Item worn = model.Equipped(b.slot);
            float[] mine = Values(GameModel.ItemStats(item)), old = worn == null ? new float[Balance.AffixStats] : Values(GameModel.ItemStats(worn));
            float sy5 = Mathf.Max(py0 + 312, yy + 6);   // 右边一栏写得多（技能 + 两条专属）时属性表往下让
            Ui.Text(px0 + 16, sy5, 100, 18, "属性", 11, Skin.TextLight, TextAnchor.MiddleLeft, true);
            Ui.Text(px0 + pw0 - 216, sy5, 200, 18, equippedNow ? "当前穿戴" : "对比当前穿戴", 11, Skin.TextLight, TextAnchor.MiddleRight, true);
            int rows = 0; for (int i = 0; i < Balance.AffixStats; i++) if (Mathf.Abs(mine[i]) >= .0001f || Mathf.Abs(old[i]) >= .0001f) rows++;
            float rowH = Mathf.Clamp(Mathf.Floor((py0 + ph0 - 34 - (sy5 + 22)) / Mathf.Max(1, rows)), 20, 40);
            float ry = sy5 + 22;
            for (int i = 0; i < Balance.AffixStats; i++)
            {
                if (Mathf.Abs(mine[i]) < .0001f && Mathf.Abs(old[i]) < .0001f) continue;
                int stat = i; bool rolled = item.affixes.Any(affix => affix.stat == stat), special = i >= Balance.BasicStats;
                float delta = mine[i] - old[i]; bool flat = equippedNow || Mathf.Abs(delta) < .0001f;
                float labelSize = rowH < 30 ? 12 : 14, labelW = Mathf.Min(112, Ui.Width(Catalog.StatNames[i], labelSize, true));
                Ui.FitText(px0 + 16, ry, 112, rowH - 6, Catalog.StatNames[i], labelSize, special ? Ui.OrangeInk : Skin.TextDark, TextAnchor.MiddleLeft, true);
                if (rolled)
                {
                    float roll = Balance.AffixRoll(item.affixes.First(affix => affix.stat == stat), item.level); bool great = roll >= Balance.GreatRoll;
                    Ui.Text(px0 + 19 + labelW, ry, 20, rowH - 6, great ? "★" : "◆", great ? 11 : 9, great ? Ui.Orange : QualityVisual.Ink(item.quality), TextAnchor.MiddleLeft);
                    Skin.Pill(px0 + 16, ry + rowH - 9, 64, 4, new Color(0, 0, 0, .12f)); Skin.Pill(px0 + 16, ry + rowH - 9, Mathf.Max(4, 64 * roll), 4, great ? Ui.Orange : qc);
                }
                for (float dx = px0 + 34 + labelW + (rolled ? 12 : 0); dx < px0 + pw0 - 170; dx += 5) Ui.Fill(dx, ry + rowH / 2 - 1, 1.6f, 1.6f, Ui.Hex(0xB4B0A8));
                Ui.Text(px0 + pw0 - 236, ry, 120, rowH - 4, Value(i, mine[i]), rowH < 30 ? 17 : 20, Skin.TextDark, TextAnchor.MiddleRight, true, true);
                if (flat) Ui.Text(px0 + pw0 - 108, ry, 92, rowH - 4, "—", 15, Skin.TextLight, TextAnchor.MiddleRight, true, true);
                else
                {
                    float ph = Mathf.Min(24, rowH - 8);
                    Skin.Pill(px0 + pw0 - 108, ry + (rowH - 4 - ph) / 2, 92, ph, delta > 0 ? Ui.Good : Ui.OrangeInk);
                    Ui.Text(px0 + pw0 - 108, ry + (rowH - 4 - ph) / 2, 86, ph, (delta > 0 ? "▲ +" : "▼ ") + Value(i, delta), 12.5f, Color.white, TextAnchor.MiddleRight, true, true);
                }
                Ui.Fill(px0 + 12, ry + rowH - 2, pw0 - 24, 1, new Color(0, 0, 0, .08f));
                ry += rowH;
            }
            // 底部一行：芯片 / 改装，或者词条说明
            var extras = new List<string>();
            if (item.mod > 0) extras.Add("改装：" + Catalog.EffectShort((Effect)item.mod));
            if (item.chips != null) foreach (Chip c in item.chips) extras.Add(Catalog.ChipLabel(c) + " " + Catalog.ChipEffectText(c, item.level));
            if (extras.Count > 0) Ui.FitText(px0 + 16, py0 + ph0 - 28, pw0 - 32, 18, string.Join(" · ", extras.ToArray()), 10.5f, Ui.OrangeInk, TextAnchor.MiddleLeft, true);
            else if (item.affixes.Count > 0) Ui.FitText(px0 + 16, py0 + ph0 - 28, pw0 - 32, 18, "◆ 标记的属性含随机词条，共 " + item.affixes.Count + " 条；短条是成色，★ 是极品", 10, Skin.TextLight, TextAnchor.MiddleLeft);
            // 印章盖在鉴定单右下角（属性多时会压到最后一行，像真的盖章一样，半透明）
            if (equippedNow) Skin.Stamp(px0 + pw0 - 74, py0 + ph0 - 62, "穿戴中", Ui.Orange, -12, 14);
            else if (item.locked) Skin.Stamp(px0 + pw0 - 74, py0 + ph0 - 62, "已锁定", Ui.Hex(0x3B3D42), -12, 14);

            // 按键
            float by = py0 + ph0 + 14;
            if (Skin.Keycap(rx0 + 16, by, rw0 - 32, 52, equippedNow ? "已穿戴" : "穿戴装备", Skin.Key.Orange, !equippedNow, 19))
            { model.Equip(item.uid); combat.SyncLoadout(); animationState.Clear(); Save(); Play(equipSound); Notify("已穿戴 " + b.name); }
            bool lookFits = model.LookFits(b);
            if (Skin.Keycap(rx0 + 16, by + 62, 240, 44, lookFits ? "仅使用外观" : "外观类别不同", Skin.Key.White, lookFits, 15)) { model.SetAppearance(b.id); Save(); Play(equipSound); Notify("已切换外观：" + b.name); }
            if (Skin.Keycap(rx0 + 264, by + 62, rw0 - 280, 44, item.locked ? "解锁" : "锁定", Skin.Key.Dark, true, 15)) { item.locked = !item.locked; Save(); }
            bool canSalvage = !item.locked && !equippedNow, over = canSalvage && Ui.Hover(rx0 + 16, by + 116, rw0 - 32, 32);
            Skin.Hazard(rx0 + 16, by + 116, rw0 - 32, 32, !over, over ? Time.unscaledTime * 10 : 0);
            string salvage = equippedNow ? "穿戴中的装备不可分解" : item.locked ? "已锁定的装备不可分解" : "分解 · +" + model.SalvageGain(item) + " 零件（外观保留）";
            float lw2 = Mathf.Min(rw0 - 80, Ui.Width(salvage, 11, true) + 30);
            Skin.Pill(rx0 + rw0 / 2 - lw2 / 2, by + 122, lw2, 20, over ? Skin.Ink2 : new Color(1, 1, 1, .9f));
            Ui.FitText(rx0 + rw0 / 2 - lw2 / 2 + 8, by + 122, lw2 - 16, 20, salvage, 11, over ? Ui.Orange : canSalvage ? Ui.OrangeInk : Skin.TextMid, TextAnchor.MiddleCenter, true);
            if (canSalvage && Ui.Click(rx0 + 16, by + 116, rw0 - 32, 32) && model.Salvage(item.uid))
            { recentDrops.Remove(item.uid); unseen.Remove(item.uid); selectedItem = null; Save(); Play(salvageSound); Notify("已分解，收藏的外观保留"); }
        }
    }
}
