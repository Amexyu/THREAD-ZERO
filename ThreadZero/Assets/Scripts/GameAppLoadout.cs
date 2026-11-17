using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ThreadZero
{
    public sealed partial class GameApp
    {
        private string implantNew;   // 刚装上、还没在整备页看过的义体（看过一次就清掉）
        private float implantNewSeenAt = -1;
        private void LoadoutPage()
        {
            if (implantNew != null) { if (implantNewSeenAt < 0) implantNewSeenAt = clock; else if (clock - implantNewSeenAt > 6) { implantNew = null; implantNewSeenAt = -1; } }
            LoadoutCharacter();
            LoadoutInventory();
            Item chosen = FindItem(selectedItem);
            Ui.Fill(1160, 20, 408, 856, Ui.Panel);
            if (chosen != null) ItemDetail(chosen);
            else Ui.Text(1160, 20, 408, 856, "选择一件装备\n查看属性与替换效果", 18, Ui.Mute, TextAnchor.MiddleCenter);
            Ui.Frame(1160, 20, 408, 856, 2, Ui.Ink);
        }

        private void LoadoutCharacter()
        {
            Ui.Fill(128, 20, 400, 404, Ui.Stage);
            Ui.Fill(128, 380, 400, 44, Ui.Floor); Ui.Fill(128, 378, 400, 3, Ui.Ink);
            Ui.Text(146, 30, 200, 40, "整备", 30, Ui.Ink, TextAnchor.MiddleLeft, true);
            Ui.Text(147, 70, 200, 18, "LOADOUT", 13, Ui.Mute, TextAnchor.MiddleLeft, true, true);
            string summary = model.LoadoutSummary; float chip = Ui.Width(summary, 13, true) + 20;
            Ui.Fill(516 - chip, 32, chip, 28, Ui.Ink); Ui.Text(516 - chip, 32, chip, 28, summary, 13, Ui.Panel, TextAnchor.MiddleCenter, true);
            PreviewHero(328, 384, 350, 158, 86, 340, 306);
            ImplantColumn(146, 104);
            Ui.Frame(128, 20, 400, 404, 2, Ui.Ink);

            // v0.16 五个部位：两列三行（第三行是下装），格子比原来矮一点
            for (int i = 0; i < Catalog.SlotCount; i++)
            {
                const float tw = 196, th = 52;
                Item item = model.Equipped((Slot)i); float x = 128 + (i % 2) * 204, y = 436 + (i / 2) * 56;
                bool hover = Ui.Hover(x, y, tw, th);
                Ui.Fill(x, y, tw, th, Ui.Panel); Ui.Frame(x, y, tw, th, hover ? 2 : 1, hover ? Ui.Ink : Ui.Rule);
                if (item == null)
                {
                    Ui.Fill(x + 5, y + 5, 42, 42, Ui.Rule);
                    Ui.Text(x + 56, y, 136, th, Catalog.SlotNames[i] + " · 空", 14, Ui.Mute, TextAnchor.MiddleLeft);
                    continue;
                }
                EquipmentBase b = Catalog.Find(item.baseId);
                Ui.ItemTile(x + 5, y + 5, 42, 42, b, item.quality, true, false, 3, false);
                Ui.Text(x + 56, y + 5, 136, 16, Catalog.SlotNames[i] + " · " + Catalog.QualityNames[(int)item.quality] + " Lv." + item.level, 11, Ui.Mute, TextAnchor.MiddleLeft);
                Ui.Text(x + 56, y + 22, 136, 24, b.name, 14, Ui.Ink, TextAnchor.MiddleLeft, true);
                if (Ui.Click(x, y, tw, th)) { ResetInventoryFilters(); SelectItem(item); slotFilter = i; inventoryScroll = 0; }
            }

            float[] totals = Values(model.Total());
            Ui.Fill(128, 604, 400, 164, Ui.Ink);
            Ui.Fill(144, 686, 368, 1, Ui.Steel);
            for (int i = 0; i < 6; i++)
            {
                float x = 128 + (i % 3) * 133.3f, y = 612 + (i / 3) * 80;
                string value = i == 3 ? (1 + totals[i]).ToString("F2") : i == 4 ? (totals[i] * 100).ToString("F0") + "%" : totals[i].ToString("F0");
                Ui.Text(x, y, 133.3f, 20, i == 3 ? "每秒攻击" : Catalog.StatNames[i], 13, Ui.MuteOnInk, TextAnchor.MiddleCenter);
                Ui.Text(x, y + 20, 133.3f, 46, value, i < 3 ? 38 : 30, Ui.Panel, TextAnchor.MiddleCenter, true, true);
            }

            bool capped = model.state.training >= Balance.TrainingCap, affordable = !capped && model.state.scrap >= model.TrainingCost;
            string label = capped ? "训练已满级 · Lv." + model.state.training : "训练 Lv." + model.state.training + " → " + (model.state.training + 1) + " · " + model.TrainingCost + " 零件";
            if (Ui.Button(128, 780, 400, 52, label, 0, affordable, 16))
            { if (model.Train()) { Save(); Play(equipSound); Notify("训练完成：攻击 +" + Balance.TrainingAttack.ToString("F1") + " / 生命 +" + Balance.TrainingHealth.ToString("F0")); } }
            Ui.Text(128, 840, 400, 30, capped ? "基础训练已经到达上限。" : affordable ? "每级训练：攻击 +1.5，生命 +8" : "零件不足。击败敌人和分解装备可以获得零件。", 13, Ui.Mute, TextAnchor.MiddleLeft);
        }

        // v0.16 机械飞升：5 个义体槽竖排在角色左边。已装 = 橙色块；悬停显示名字和效果。
        private static readonly string[] ImplantParts = { "手臂", "义眼", "脊柱", "心脏", "接口" };
        private void ImplantColumn(float x, float y)
        {
            int have = model.state.implants.Count;
            Ui.Text(x, y, 120, 18, "机械飞升 " + have + "/" + Catalog.AscensionTotal, 13, have > 0 ? Ui.Orange : Ui.Mute, TextAnchor.MiddleLeft, true);
            string tipTitle = null, tipBody = null; float tipY = 0;
            for (int i = 0; i < Catalog.AscensionTotal; i++)
            {
                // 按部位对格子：第 i 格 = 第 i 章首领给的义体（手臂、义眼……），装没装看有没有这件，和获得顺序无关
                ImplantDef slotDef = i < Catalog.Implants.Length ? Catalog.Implants[i] : null;
                float by = y + 26 + i * 50; bool on = slotDef != null && model.state.implants.Contains(slotDef.id); bool hover = Ui.Hover(x, by, 44, 44);
                Ui.Fill(x, by, 44, 44, on ? Ui.Orange : Ui.Panel); Ui.Frame(x, by, 44, 44, 2, on ? Ui.Ink : Ui.Rule);
                Ui.Text(x, by, 44, 44, ImplantParts[i].Substring(0, 1), 18, on ? Ui.OnAccent : Ui.Rule, TextAnchor.MiddleCenter, true);
                if (on && slotDef.id == implantNew) { Ui.Fill(x + 30, by - 6, 30, 16, Ui.Ink); Ui.Text(x + 30, by - 6, 30, 16, "NEW", 10, Ui.Orange, TextAnchor.MiddleCenter, true, true); }
                if (hover)
                {
                    ImplantDef d = on ? slotDef : null;
                    tipTitle = d != null ? d.name : "未解锁 · " + ImplantParts[i];
                    tipBody = d != null ? d.desc : "击败第 " + (i + 1) + " 章的首领后装上。";
                    tipY = by;
                }
            }
            if (tipTitle != null)
            {
                Ui.Fill(x + 52, tipY - 4, 260, 64, Ui.Ink);
                Ui.Text(x + 64, tipY, 240, 24, tipTitle, 15, Ui.Panel, TextAnchor.MiddleLeft, true);
                Ui.Text(x + 64, tipY + 24, 240, 34, tipBody, 12, Ui.MuteOnInk, TextAnchor.UpperLeft, false, false, true);
            }
        }

        private void LoadoutInventory()
        {
            float x = 548;
            for (int f = -1; f < Catalog.SlotCount; f++)
            {
                int slot = f;
                string label = f < 0 ? "全部" : Catalog.SlotNames[f];
                string count = model.state.items.Count(item => slot < 0 || (int)Catalog.Find(item.baseId).slot == slot).ToString();
                float labelWidth = Ui.Width(label, 15, true), width = labelWidth + Ui.Width(count, 17, true, true) + 30;   // 五个部位 + 全部，收紧一点给零件数留位置
                bool on = slotFilter == f, hover = Ui.Hover(x, 20, width, 44);
                Color fill = on ? Ui.Ink : hover ? Ui.Rule : Ui.Panel, text = on ? Ui.Panel : Ui.Ink;
                Ui.Fill(x, 20, width, 44, fill); Ui.Frame(x, 20, width, 44, 2, Ui.Ink);
                Ui.Text(x + 10, 20, labelWidth + 4, 44, label, 15, text, TextAnchor.MiddleLeft, true);
                Ui.Text(x + 10 + labelWidth + 8, 20, 60, 44, count, 17, on ? Ui.MuteOnInk : Ui.Mute, TextAnchor.MiddleLeft, true, true);
                if (Ui.Click(x, 20, width, 44)) { slotFilter = f; inventoryScroll = 0; }
                x += width + 4;
            }
            string scrap = model.state.scrap.ToString();
            Ui.Text(960, 20, 180, 44, scrap, 26, Ui.Ink, TextAnchor.MiddleRight, true, true);
            Ui.Text(960, 20, 180 - Ui.Width(scrap, 26, true, true) - 8, 44, "零件", 14, Ui.Mute, TextAnchor.MiddleRight);

            float viewY = InventoryToolbar();
            inventoryQuery.slot = slotFilter;
            List<Item> items = inventoryQuery.Apply(model.state);
            Ui.Text(548, viewY - 28, 592, 22, "显示 " + items.Count + " / " + model.state.items.Count + " 件 · 同值优先等级、品质、最近获得", 12, Ui.Mute);
            if (Ui.Button(1010, viewY - 32, 130, 28, "一键分解", 0, true, 13)) navigation.Open(GameModal.Salvage);
            const float viewX = 544, viewW = 600, tileW = 110.4f, tileH = 132, step = 142;
            float viewH = 876 - viewY;
            int rows = (items.Count + 4) / 5;
            float content = rows * step - 10 + 8, limit = Mathf.Max(0, content - viewH);
            bool inside = Ui.Hover(viewX, viewY, viewW, viewH);
            if (inside && Event.current.type == EventType.ScrollWheel) { inventoryScroll += Event.current.delta.y * 36; Event.current.Use(); }
            inventoryScroll = Mathf.Clamp(inventoryScroll, 0, limit);
            if (items.Count == 0) Ui.Text(548, viewY + 4, 592, 180, "没有符合条件的装备。\n可修改条件或点击「清空筛选」。", 16, Ui.Mute, TextAnchor.MiddleCenter);

            Ui.BeginClip(viewX, viewY, viewW, viewH, inventoryScroll);
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i]; EquipmentBase b = Catalog.Find(item.baseId);
                float tx = 548 + (i % 5) * (tileW + 10), ty = viewY + 4 + (i / 5) * step;
                if (ty + tileH < viewY + inventoryScroll || ty > viewY + inventoryScroll + viewH) continue;
                bool selected = selectedItem == item.uid, equipped = model.state.equipped.Contains(item.uid), hover = inside && Ui.Hover(tx, ty, tileW, tileH);
                Color quality = QualityVisual.Colors[(int)item.quality];
                if (selected) Ui.Frame(tx - 4, ty - 4, tileW + 8, tileH + 8, 3, Ui.Orange);
                Ui.Fill(tx, ty, tileW, tileH, hover ? Ui.Tile2 : Ui.Tile);
                PaperDollView.DrawEquipmentIcon(b, Ui.R(tx + (tileW - 74) / 2, ty + 18, 74, 74));
                Ui.Text(tx + 7, ty + 3, 60, 18, "Lv." + item.level, 13, Ui.MuteOnInk, TextAnchor.MiddleLeft, true, true);
                if (equipped) { Ui.Fill(tx + tileW - 50, ty, 50, 20, Ui.Panel); Ui.Text(tx + tileW - 50, ty, 50, 20, "穿戴中", 11, Ui.Ink, TextAnchor.MiddleCenter, true); }
                else if (unseen.Contains(item.uid)) { Ui.Fill(tx + tileW - 26, ty, 26, 20, Ui.Orange); Ui.Text(tx + tileW - 26, ty, 26, 20, "新", 11, Ui.OnAccent, TextAnchor.MiddleCenter, true); }
                if (item.locked) Ui.IconLock(tx + 8, ty + 26, Ui.MuteOnInk);
                Ui.Text(tx, ty + 96, tileW, 22, b.name, 12, quality, TextAnchor.MiddleCenter, true);
                Ui.Fill(tx, ty + tileH - 5, tileW, 5, quality);
                if (inside && Ui.Click(tx, ty, tileW, tileH)) SelectItem(item);
            }
            Ui.EndClip();
            if (limit > 0)
            {
                float track = viewH - 8, thumb = Mathf.Max(40, track * viewH / content);
                Ui.Fill(1146, viewY + 4, 4, track, Ui.Rule);
                Ui.Fill(1146, viewY + 4 + (track - thumb) * inventoryScroll / limit, 4, thumb, Ui.Ink);
            }
        }

        private void ItemDetail(Item item)
        {
            EquipmentBase b = Catalog.Find(item.baseId);
            Color quality = QualityVisual.Colors[(int)item.quality], qualityInk = QualityVisual.Ink(item.quality);
            bool equippedNow = model.state.equipped.Contains(item.uid);
            Ui.Fill(1160, 20, 408, 148, Ui.Ink); Ui.Fill(1160, 20, 408, 8, quality);
            Ui.Fill(1176, 44, 108, 108, Ui.Tile2); PaperDollView.DrawEquipmentIcon(b, Ui.R(1183, 51, 94, 94));
            Ui.Text(1300, 48, 256, 22, Catalog.QualityNames[(int)item.quality] + " · " + Catalog.SlotNames[(int)b.slot] + " · Lv." + item.level, 14, quality, TextAnchor.MiddleLeft, true);
            Ui.Text(1300, 74, 256, 40, b.name, 24, Ui.Panel, TextAnchor.MiddleLeft, true);
            Ui.Text(1300, 118, 256, 22, equippedNow ? "穿戴中" : item.locked ? "已锁定 · 不会被分解" : "背包中", 13, Ui.MuteOnInk, TextAnchor.MiddleLeft);

            Item worn = model.Equipped(b.slot);
            float[] mine = Values(GameModel.ItemStats(item)), old = worn == null ? new float[6] : Values(GameModel.ItemStats(worn));
            Ui.Text(1176, 180, 200, 20, "属性", 12, Ui.Mute, TextAnchor.MiddleLeft);
            Ui.Text(1352, 180, 200, 20, equippedNow ? "当前穿戴" : "对比当前穿戴", 12, Ui.Mute, TextAnchor.MiddleRight);
            Ui.Fill(1176, 202, 376, 2, Ui.Ink);
            float y = 204;
            for (int i = 0; i < 6; i++)
            {
                if (Mathf.Abs(mine[i]) < .0001f && Mathf.Abs(old[i]) < .0001f) continue;
                int stat = i; bool rolled = item.affixes.Any(affix => affix.stat == stat);
                float delta = mine[i] - old[i]; bool flat = equippedNow || Mathf.Abs(delta) < .0001f;
                Ui.Text(1176, y, 70, 42, Catalog.StatNames[i], 15, Ui.Body, TextAnchor.MiddleLeft);
                if (rolled) Ui.Text(1214, y, 30, 42, "◆", 11, qualityInk, TextAnchor.MiddleLeft);
                Ui.Text(1272, y, 180, 42, Value(i, mine[i]), 26, Ui.Ink, TextAnchor.MiddleLeft, true, true);
                if (flat) Ui.Text(1452, y, 100, 42, "—", 18, Ui.Mute, TextAnchor.MiddleRight, true, true);
                else
                {
                    Ui.Fill(1452, y + 7, 100, 28, delta > 0 ? Ui.Good : Ui.OrangeInk);
                    Ui.Text(1452, y + 7, 92, 28, (delta > 0 ? "▲ +" : "▼ ") + Value(i, delta), 17, Color.white, TextAnchor.MiddleRight, true, true);
                }
                Ui.Fill(1176, y + 41, 376, 1, Ui.Hair);
                y += 42;
            }

            string effectName, effectText; SplitEffect(b.effect, out effectName, out effectText);
            y += 14;
            Ui.Frame(1176, y, 376, 88, 2, Ui.Ink);
            Ui.Text(1190, y + 8, 348, 18, "MODULE / 固定效果", 12, Ui.Mute, TextAnchor.MiddleLeft, true, true);
            Ui.Text(1190, y + 28, 348, 24, effectName, 16, Ui.Ink, TextAnchor.MiddleLeft, true);
            Ui.Text(1190, y + 54, 348, 24, effectText, 13, Ui.Body, TextAnchor.MiddleLeft);
            if (item.affixes.Count > 0) Ui.Text(1176, y + 96, 376, 22, "◆ 标记的属性含随机词条，共 " + item.affixes.Count + " 条", 12, Ui.Mute, TextAnchor.MiddleLeft);

            if (Ui.Button(1176, 706, 376, 60, equippedNow ? "已穿戴" : "穿戴装备", 1, !equippedNow, 20))
            { model.Equip(item.uid); combat.SyncLoadout(); animationState.Clear(); Save(); Play(equipSound); Notify("已穿戴 " + b.name); }
            if (Ui.Button(1176, 774, 244, 46, "仅使用外观", 0, true, 15)) { model.SetAppearance(b.id); Save(); Play(equipSound); Notify("已切换外观：" + b.name); }
            if (Ui.Button(1428, 774, 124, 46, item.locked ? "解锁" : "锁定", 0, true, 15)) { item.locked = !item.locked; Save(); }
            bool canSalvage = !item.locked && !equippedNow, over = canSalvage && Ui.Hover(1176, 828, 376, 40);
            Ui.Fill(1176, 828, 376, 40, over ? Ui.OrangeInk : Ui.Panel); Ui.Frame(1176, 828, 376, 40, 1, canSalvage ? Ui.OrangeInk : Ui.Grey);
            string salvage = equippedNow ? "穿戴中的装备不可分解" : item.locked ? "已锁定的装备不可分解" : "分解 · +" + GameModel.SalvageValue(item) + " 零件（外观保留）";
            Ui.Text(1176, 828, 376, 40, salvage, 14, over ? Color.white : canSalvage ? Ui.OrangeInk : Ui.Mute, TextAnchor.MiddleCenter, true);
            if (canSalvage && Ui.Click(1176, 828, 376, 40) && model.Salvage(item.uid))
            { recentDrops.Remove(item.uid); unseen.Remove(item.uid); selectedItem = null; Save(); Notify("已分解，收藏的外观保留"); }
        }
    }
}
