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
        private int implantPick = -1;   // v0.20 点开的义体（升级卡片），-1 = 没点开
        private static string ImplantStatsText(Stats s)
        {
            var parts = new List<string>();
            if (s.attack > 0) parts.Add("攻击 +" + s.attack.ToString("0.#")); if (s.health > 0) parts.Add("生命 +" + s.health.ToString("0"));
            if (s.defense > 0) parts.Add("防御 +" + s.defense.ToString("0.#")); if (s.haste > 0) parts.Add("攻速 +" + (s.haste * 100).ToString("0.#") + "%");
            if (s.crit > 0) parts.Add("暴击 +" + (s.crit * 100).ToString("0.#") + "%"); if (s.luck > 0) parts.Add("幸运 +" + s.luck.ToString("0.#"));
            if (s.critDamage > 0) parts.Add("暴击伤害 +" + (s.critDamage * 100).ToString("0") + "%");
            return string.Join("，", parts);
        }
        private static Stats ImplantScaled(Stats a, int lv)
        {
            float k = 1 + Catalog.ImplantLevelGain * (lv - 1);
            var c = new Stats(a.health * k, a.attack * k, a.defense * k, a.haste * k, a.crit * k, a.luck * k); c.critDamage = a.critDamage * k; return c;
        }
        /// v0.20 义体升级卡片：当前效果、下一级、5 / 10 级特性、花费、升级按钮。
        private void ImplantCard(int i, float x, float y)
        {
            ImplantDef d = Catalog.Implants[i]; int lv = model.ImplantLevel(i); bool max = lv >= Catalog.ImplantMaxLevel;
            float w = 340, h = 232;
            Ui.Fill(x, y, w, h, Ui.Ink); Ui.Fill(x, y, 6, h, Ui.Orange);
            Ui.Text(x + 18, y + 10, w - 30, 26, d.name, 18, Ui.Panel, TextAnchor.MiddleLeft, true);
            Ui.Text(x + 18, y + 10, w - 34, 26, "Lv " + lv + " / " + Catalog.ImplantMaxLevel, 15, Ui.Orange, TextAnchor.MiddleRight, true, true);
            Ui.Text(x + 18, y + 38, w - 30, 20, "现在：" + ImplantStatsText(ImplantScaled(d.stats, lv)), 12, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            Ui.Text(x + 18, y + 58, w - 30, 20, max ? "已经满级" : "下一级：" + ImplantStatsText(ImplantScaled(d.stats, lv + 1)), 12, max ? Ui.Orange : Ui.Panel, TextAnchor.MiddleLeft);
            Ui.Text(x + 18, y + 86, w - 30, 20, (lv >= 5 ? "✓ " : "5 级 · ") + d.trait5Text, 12, lv >= 5 ? Ui.Orange : Ui.MuteOnInk, TextAnchor.MiddleLeft, lv >= 5);
            Ui.Text(x + 18, y + 106, w - 30, 20, (lv >= 10 ? "✓ " : "10 级 · ") + d.trait10Text, 12, lv >= 10 ? Ui.Orange : Ui.MuteOnInk, TextAnchor.MiddleLeft, lv >= 10);
            if (!max)
            {
                int sc = Catalog.ImplantShardCost(lv), pc = Catalog.ImplantScrapCost(lv);
                bool okS = model.state.shards >= sc, okP = model.state.scrap >= pc;
                Ui.Text(x + 18, y + 134, w - 30, 20, "织网碎片 " + sc + "（持有 " + model.state.shards + "）", 13, okS ? Ui.Panel : Ui.Orange, TextAnchor.MiddleLeft, true);
                Ui.Text(x + 18, y + 154, w - 30, 20, "零件 " + pc + "（持有 " + model.state.scrap + "）", 13, okP ? Ui.Panel : Ui.Orange, TextAnchor.MiddleLeft, true);
                if (Ui.Button(x + 18, y + 182, w - 36, 40, okS && okP ? "升级到 Lv " + (lv + 1) : !okS ? "织网碎片不足 · 去回声层刷" : "零件不足", 1, okS && okP, 15))
                    UpgradeImplantClicked(i);
            }
            else Ui.Text(x + 18, y + 140, w - 30, 60, "这件义体已经满级。其余义体升满后机械飞升完成。", 13, Ui.Panel, TextAnchor.UpperLeft, false, false, true);
        }
        private void UpgradeImplantClicked(int i)
        {
            ImplantDef d = Catalog.Implants[i]; int stage = model.AscensionStage;
            if (!model.UpgradeImplant(i)) return;
            Save(); Play(equipSound); Notify(d.name + " 升到 Lv " + model.ImplantLevel(i) + " · 飞升度 " + (model.Ascension * 100).ToString("0") + "%");
            if (model.AscensionStage > stage)   // v0.20 跨过飞升阶段：横幅只画在行动页上，等玩家回去再放（不然在整备页升级时看不到）
            {
                stageShown = model.AscensionStage; stagePending = true; Play(rareSound);
                Notify("机械飞升进入「" + Catalog.AscensionStageNames[stageShown - 1] + "」· 回到行动页看看", 4);
            }
        }
        /// 测试用（桥接 call TestUpgradeImplant 0）：等同在整备页点义体的「升级」按钮。
        public void TestUpgradeImplant(int i) { UpgradeImplantClicked(i); }
        private void ImplantColumn(float x, float y)
        {
            int have = model.state.implants.Count;
            Ui.Text(x, y, 160, 18, "机械飞升 " + (model.Ascension * 100).ToString("0") + "%", 13, have > 0 ? Ui.Orange : Ui.Mute, TextAnchor.MiddleLeft, true);
            if (model.AscensionStage > 0) Ui.Text(x + 100, y, 220, 18, Catalog.AscensionStageNames[model.AscensionStage - 1], 11, Ui.Alpha(Ui.Orange, .8f), TextAnchor.MiddleLeft, false);   // 和百分比同一行（放下一行会被第一个义体格子压住）
            if (model.AscensionStage >= Catalog.AscensionStageAt.Length)
            {   // v0.20 飞升完成以后可以重看尾声
                float lx = x + 100 + Ui.Width(Catalog.AscensionStageNames[model.AscensionStage - 1], 11) + 12; bool hv = Ui.Hover(lx, y, 70, 18);
                Ui.Text(lx, y, 70, 18, "重看尾声 ›", 11, hv ? Ui.Ink : Ui.Orange, TextAnchor.MiddleLeft, true);
                if (Ui.Click(lx, y, 70, 18)) OpenEpilogue();
            }
            if (have > 0) Ui.Text(x, y + 280, 160, 18, "织网碎片 " + model.state.shards, 12, Ui.Mute, TextAnchor.MiddleLeft, true);
            string tipTitle = null, tipBody = null; float tipY = 0;
            for (int i = 0; i < Catalog.AscensionTotal; i++)
            {
                // 按部位对格子：第 i 格 = 第 i 章首领给的义体（手臂、义眼……），装没装看有没有这件，和获得顺序无关
                ImplantDef slotDef = i < Catalog.Implants.Length ? Catalog.Implants[i] : null;
                float by = y + 26 + i * 50; bool on = slotDef != null && model.state.implants.Contains(slotDef.id); bool hover = Ui.Hover(x, by, 44, 44);
                Ui.Fill(x, by, 44, 44, on ? Ui.Orange : Ui.Panel); Ui.Frame(x, by, 44, 44, 2, on ? Ui.Ink : Ui.Rule);
                Ui.Text(x, by, 44, 44, ImplantParts[i].Substring(0, 1), 18, on ? Ui.OnAccent : Ui.Rule, TextAnchor.MiddleCenter, true);
                if (on)
                {   // v0.20 等级角标 + 点开升级卡片
                    int lv = model.ImplantLevel(i);
                    Ui.Fill(x + 26, by + 28, 18, 16, Ui.Ink); Ui.Text(x + 26, by + 28, 18, 16, lv.ToString(), 11, Ui.Orange, TextAnchor.MiddleCenter, true, true);
                    if (model.CanUpgradeImplant(i)) Ui.Fill(x + 38, by - 2, 8, 8, Ui.Orange);
                    if (Ui.Click(x, by, 44, 44)) implantPick = implantPick == i ? -1 : i;
                }
                if (on && slotDef.id == implantNew) { Ui.Fill(x + 30, by - 6, 30, 16, Ui.Ink); Ui.Text(x + 30, by - 6, 30, 16, "NEW", 10, Ui.Orange, TextAnchor.MiddleCenter, true, true); }
                if (hover)
                {
                    ImplantDef d = on ? slotDef : null;
                    tipTitle = d != null ? d.name : "未解锁 · " + ImplantParts[i];
                    tipBody = d != null ? d.desc + "（点击升级）" : "击败第 " + (i + 1) + " 章的首领后装上。";
                    tipY = by;
                }
            }
            if (implantPick >= 0 && model.ImplantLevel(implantPick) > 0) { ImplantCard(implantPick, x + 52, Mathf.Min(y + 22 + implantPick * 50, 860 - 232)); return; }
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
            Ui.Text(1300, 48, 256, 22, Catalog.QualityNames[(int)item.quality] + " · " + Catalog.SlotNames[(int)b.slot] + " · Lv." + item.level
                + (b.slot == Slot.Weapon ? (Catalog.IsGroupWeapon(b.id) ? " · 群伤" : " · 单体") : ""), 14, quality, TextAnchor.MiddleLeft, true);   // v0.20 武器标群伤 / 单体，卡关提示里说的「换群伤武器」有地方看
            Ui.Text(1300, 74, 256, 40, b.name, 24, Ui.Panel, TextAnchor.MiddleLeft, true);
            Ui.Text(1300, 118, 256, 22, equippedNow ? "穿戴中" : item.locked ? "已锁定 · 不会被分解" : "背包中", 13, Ui.MuteOnInk, TextAnchor.MiddleLeft);

            Item worn = model.Equipped(b.slot);
            float[] mine = Values(GameModel.ItemStats(item)), old = worn == null ? new float[Balance.AffixStats] : Values(GameModel.ItemStats(worn));
            Ui.Text(1176, 180, 200, 20, "属性", 12, Ui.Mute, TextAnchor.MiddleLeft);
            Ui.Text(1352, 180, 200, 20, equippedNow ? "当前穿戴" : "对比当前穿戴", 12, Ui.Mute, TextAnchor.MiddleRight);
            Ui.Fill(1176, 202, 376, 2, Ui.Ink);
            float y = 204;
            // v0.20 特殊词条（暴击伤害等 4 项，橙色）也列出来：原来只列 6 项基础属性，洗出来的特殊词条在整备页看不到、也比不了。
            // 行数多了（最多 10 行）就把行距从 42 压到 36，免得顶到下面的固定效果框和按钮。
            int rows = 0; for (int i = 0; i < Balance.AffixStats; i++) if (Mathf.Abs(mine[i]) >= .0001f || Mathf.Abs(old[i]) >= .0001f) rows++;
            float rowH = rows > 8 ? 36 : 42;   // 9 行 × 42 时下面那句「◆ 标记的属性…」会压到「穿戴装备」按钮
            for (int i = 0; i < Balance.AffixStats; i++)
            {
                if (Mathf.Abs(mine[i]) < .0001f && Mathf.Abs(old[i]) < .0001f) continue;
                int stat = i; bool rolled = item.affixes.Any(affix => affix.stat == stat), special = i >= Balance.BasicStats;
                float delta = mine[i] - old[i]; bool flat = equippedNow || Mathf.Abs(delta) < .0001f;
                Ui.Text(1176, y, 70, rowH, Catalog.StatNames[i], 15, special ? Ui.OrangeInk : Ui.Body, TextAnchor.MiddleLeft, special);
                if (rolled) Ui.Text(1176 + Catalog.StatNames[i].Length * 16 + 6, y, 30, rowH, "◆", 11, qualityInk, TextAnchor.MiddleLeft);
                Ui.Text(1272, y, 180, rowH, Value(i, mine[i]), rowH < 42 ? 22 : 26, Ui.Ink, TextAnchor.MiddleLeft, true, true);
                if (flat) Ui.Text(1452, y, 100, rowH, "—", 18, Ui.Mute, TextAnchor.MiddleRight, true, true);
                else
                {
                    Ui.Fill(1452, y + (rowH - 28) / 2, 100, 28, delta > 0 ? Ui.Good : Ui.OrangeInk);
                    Ui.Text(1452, y + (rowH - 28) / 2, 92, 28, (delta > 0 ? "▲ +" : "▼ ") + Value(i, delta), 17, Color.white, TextAnchor.MiddleRight, true, true);
                }
                Ui.Fill(1176, y + rowH - 1, 376, 1, Ui.Hair);
                y += rowH;
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
            bool lookFits = model.LookFits(b);   // v0.20 近战 / 远程外观不能互换
            if (Ui.Button(1176, 774, 244, 46, lookFits ? "仅使用外观" : "外观类别不同", 0, lookFits, 15)) { model.SetAppearance(b.id); Save(); Play(equipSound); Notify("已切换外观：" + b.name); }
            if (Ui.Button(1428, 774, 124, 46, item.locked ? "解锁" : "锁定", 0, true, 15)) { item.locked = !item.locked; Save(); }
            bool canSalvage = !item.locked && !equippedNow, over = canSalvage && Ui.Hover(1176, 828, 376, 40);
            Ui.Fill(1176, 828, 376, 40, over ? Ui.OrangeInk : Ui.Panel); Ui.Frame(1176, 828, 376, 40, 1, canSalvage ? Ui.OrangeInk : Ui.Grey);
            string salvage = equippedNow ? "穿戴中的装备不可分解" : item.locked ? "已锁定的装备不可分解" : "分解 · +" + GameModel.SalvageValue(item) + " 零件（外观保留）";
            Ui.Text(1176, 828, 376, 40, salvage, 14, over ? Color.white : canSalvage ? Ui.OrangeInk : Ui.Mute, TextAnchor.MiddleCenter, true);
            if (canSalvage && Ui.Click(1176, 828, 376, 40) && model.Salvage(item.uid))
            { recentDrops.Remove(item.uid); unseen.Remove(item.uid); selectedItem = null; Save(); Play(salvageSound); Notify("已分解，收藏的外观保留"); }
        }
    }
}
