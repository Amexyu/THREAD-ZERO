using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ThreadZero
{
    // v0.10 工坊。这一步只有「装备台」：升级、重校词条、突破。改造板与机库在后续步骤加入。
    public sealed partial class GameApp
    {
        private float workshopScroll;

        private void WorkshopPage()
        {
            Ui.Text(128, 16, 120, 52, "工坊", 30, Ui.Ink, TextAnchor.MiddleLeft, true);
            string[] tabs = { "装备台", "改造板 · 制作中", "机库 · 制作中" };
            float tabX = 250;
            for (int i = 0; i < 3; i++)
            {
                float width = i == 0 ? 120 : 190;
                Ui.Button(tabX, 22, width, 40, tabs[i], i == 0 ? 2 : 0, i == 0, 14);
                tabX += width + 8;
            }

            // 资源栏：许可等级、通用零件、四种部位零件、核心。
            string[] names = { "许可等级", "通用零件", Catalog.PartNames[0], Catalog.PartNames[1], Catalog.PartNames[2], Catalog.PartNames[3], "核心" };
            int[] values = { model.License, model.state.scrap, model.state.parts[0], model.state.parts[1], model.state.parts[2], model.state.parts[3], model.state.cores };
            const float chipW = (1440f - 6 * 8) / 7;
            for (int i = 0; i < 7; i++)
            {
                float x = 128 + i * (chipW + 8);
                Ui.Fill(x, 78, chipW, 62, Ui.Ink);
                if (i == 0) Ui.Fill(x, 78, 6, 62, Ui.Orange);
                Ui.Text(x + 16, 82, chipW - 24, 22, names[i], 12, Ui.MuteOnInk, TextAnchor.MiddleLeft);
                Ui.Text(x + 16, 102, chipW - 24, 34, (i == 0 ? "Lv." : "") + values[i], 24, i == 0 ? Ui.Orange : Ui.Panel, TextAnchor.MiddleLeft, true, true);
            }

            WorkshopList();
            Item chosen = FindItem(selectedItem);
            Ui.Fill(748, 156, 820, 720, Ui.Panel);
            if (chosen != null) Bench(chosen);
            else Ui.Text(748, 156, 820, 720, "从左边选一件装备\n在这里升级、重校词条或突破", 18, Ui.Mute, TextAnchor.MiddleCenter);
            Ui.Frame(748, 156, 820, 720, 2, Ui.Ink);
        }

        private void WorkshopList()
        {
            List<Item> items = model.state.items
                .OrderByDescending(item => model.state.equipped.Contains(item.uid) ? 1 : 0)
                .ThenByDescending(item => (int)item.quality).ThenByDescending(item => item.level).ToList();
            const float viewX = 124, viewY = 152, viewW = 608, viewH = 728, tileW = 112, tileH = 132, step = 142;
            int rows = (items.Count + 4) / 5;
            float content = rows * step - 10 + 8, limit = Mathf.Max(0, content - viewH);
            bool inside = Ui.Hover(viewX, viewY, viewW, viewH);
            if (inside && Event.current.type == EventType.ScrollWheel) { workshopScroll += Event.current.delta.y * 36; Event.current.Use(); }
            workshopScroll = Mathf.Clamp(workshopScroll, 0, limit);

            Ui.BeginClip(viewX, viewY, viewW, viewH, workshopScroll);
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i]; EquipmentBase b = Catalog.Find(item.baseId);
                float x = 128 + (i % 5) * (tileW + 10), y = 156 + (i / 5) * step;
                if (y + tileH < viewY + workshopScroll || y > viewY + workshopScroll + viewH) continue;
                bool selected = selectedItem == item.uid, hover = inside && Ui.Hover(x, y, tileW, tileH);
                Color quality = QualityVisual.Colors[(int)item.quality];
                if (selected) Ui.Frame(x - 4, y - 4, tileW + 8, tileH + 8, 3, Ui.Orange);
                Ui.Fill(x, y, tileW, tileH, hover ? Ui.Tile2 : Ui.Tile);
                PaperDollView.DrawEquipmentIcon(b, Ui.R(x + (tileW - 74) / 2, y + 18, 74, 74));
                Ui.Text(x + 7, y + 3, 60, 18, "Lv." + item.level, 13, Ui.MuteOnInk, TextAnchor.MiddleLeft, true, true);
                if (model.state.equipped.Contains(item.uid)) { Ui.Fill(x + tileW - 50, y, 50, 20, Ui.Panel); Ui.Text(x + tileW - 50, y, 50, 20, "穿戴中", 11, Ui.Ink, TextAnchor.MiddleCenter, true); }
                Ui.Text(x, y + 96, tileW, 22, b.name, 12, quality, TextAnchor.MiddleCenter, true);
                Ui.Fill(x, y + tileH - 5, tileW, 5, quality);
                if (inside && Ui.Click(x, y, tileW, tileH)) SelectItem(item);
            }
            Ui.EndClip();
            if (limit > 0)
            {
                float track = viewH - 8, thumb = Mathf.Max(40, track * viewH / content);
                Ui.Fill(736, viewY + 4, 4, track, Ui.Rule);
                Ui.Fill(736, viewY + 4 + (track - thumb) * workshopScroll / limit, 4, thumb, Ui.Ink);
            }
        }

        private void Bench(Item item)
        {
            EquipmentBase b = Catalog.Find(item.baseId); int slot = (int)b.slot;
            Color quality = QualityVisual.Colors[(int)item.quality];
            int license = model.License;

            Ui.Fill(748, 156, 820, 132, Ui.Ink); Ui.Fill(748, 156, 820, 8, quality);
            Ui.Fill(764, 180, 92, 92, Ui.Tile2); PaperDollView.DrawEquipmentIcon(b, Ui.R(770, 186, 80, 80));
            Ui.Text(876, 182, 500, 22, Catalog.QualityNames[(int)item.quality] + " · " + Catalog.SlotNames[slot], 14, quality, TextAnchor.MiddleLeft, true);
            Ui.Text(876, 206, 500, 40, b.name, 24, Ui.Panel, TextAnchor.MiddleLeft, true);
            Ui.Text(876, 248, 500, 22, "Lv." + item.level + "  /  许可上限 Lv." + license, 14, Ui.MuteOnInk, TextAnchor.MiddleLeft);

            // 升级
            Ui.Text(772, 304, 400, 20, "UPGRADE / 升级", 12, Ui.Mute, TextAnchor.MiddleLeft, true, true);
            Ui.Fill(772, 326, 772, 2, Ui.Ink);
            float tier = Balance.Tier(item.quality).multiplier, now = Balance.LevelFactor(item.level), next = Balance.LevelFactor(item.level + 1);
            float[] basis = { b.stats.health, b.stats.attack, b.stats.defense };
            bool capped = item.level >= license;
            float line = 338;
            for (int i = 0; i < 3; i++)
            {
                if (basis[i] <= 0) continue;
                string text = Catalog.StatNames[i] + "  " + (basis[i] * tier * now).ToString("F1") + (capped ? "" : "  →  " + (basis[i] * tier * next).ToString("F1"));
                Ui.Text(772, line, 520, 28, text, 17, Ui.Ink, TextAnchor.MiddleLeft, true);
                line += 30;
            }
            int cost = model.UpgradeCost(item);
            if (capped) Ui.Text(772, line + 2, 540, 26, "已到许可上限。击败下一关的首领后可以继续升级。", 13, Ui.OrangeInk, TextAnchor.MiddleLeft, true);
            else Ui.Text(772, line + 2, 540, 26, "消耗 " + Catalog.PartNames[slot] + " " + cost + "（持有 " + model.state.parts[slot] + "）· 随机词条的数值会同步提高", 13, Ui.Mute, TextAnchor.MiddleLeft);
            if (Ui.Button(1348, 340, 196, 60, capped ? "已到上限" : "升级到 Lv." + (item.level + 1), 1, model.CanUpgrade(item), 17))
            { if (model.Upgrade(item.uid)) { combat.ClampHealth(); Save(); Play(equipSound); } }

            // 重校
            Ui.Text(772, 470, 400, 20, "RECALIBRATE / 重校词条", 12, Ui.Mute, TextAnchor.MiddleLeft, true, true);
            Ui.Fill(772, 492, 772, 2, Ui.Ink);
            if (item.affixes.Count == 0) Ui.Text(772, 500, 700, 40, "这件装备没有随机词条。" + Catalog.QualityNames[1] + "及以上的装备才有。", 14, Ui.Mute, TextAnchor.MiddleLeft);
            int rerollCost = model.RerollCost(item);
            for (int i = 0; i < item.affixes.Count; i++)
            {
                Affix affix = item.affixes[i]; float y = 500 + i * 42;
                Ui.Text(772, y, 30, 40, "◆", 12, QualityVisual.Ink(item.quality), TextAnchor.MiddleLeft);
                Ui.Text(796, y, 120, 40, Catalog.StatNames[affix.stat], 16, Ui.Body, TextAnchor.MiddleLeft);
                Ui.Text(920, y, 200, 40, "+" + Value(affix.stat, affix.value), 22, Ui.Ink, TextAnchor.MiddleLeft, true, true);
                Ui.Fill(772, y + 40, 772, 1, Ui.Hair);
                if (Ui.Button(1348, y + 4, 196, 32, "重校 · " + Catalog.PartNames[1] + " " + rerollCost, 0, model.CanReroll(item, i), 13))
                { if (model.Reroll(item.uid, i)) { combat.ClampHealth(); Save(); Play(equipSound); } }
            }
            if (item.affixes.Count > 0)
                Ui.Text(772, 500 + item.affixes.Count * 42 + 4, 772, 22, "重校会把这一条换成这件装备上没有的另一种属性，并重新抽取数值。每重校一次，这件装备的下一次更贵。", 12, Ui.Mute, TextAnchor.MiddleLeft);

            // 突破（仅「机密」）
            if (item.quality == Quality.Legendary)
            {
                Ui.Fill(772, 756, 772, 60, Ui.Ink); Ui.Fill(772, 756, 8, 60, QualityVisual.Colors[5]);
                Ui.Text(796, 756, 520, 60, "突破为「" + Catalog.QualityNames[5] + "」· 多一条词条，属性倍率更高", 15, Ui.Panel, TextAnchor.MiddleLeft, true);
                if (Ui.Button(1348, 762, 188, 48, "核心 " + Balance.AscendCores + "（持有 " + model.state.cores + "）", 1, model.CanAscend(item), 14))
                { if (model.Ascend(item.uid)) { combat.ClampHealth(); Save(); Play(rareSound); Notify("突破完成：" + b.name + " 现在是「" + Catalog.QualityNames[5] + "」", 5); } }
            }
            int chipSlots = Balance.ChipSlots[(int)item.quality];
            Ui.Text(772, 832, 772, 24, "芯片槽 " + chipSlots + " 个 · 改装位 1 个（芯片与改装件在下一步开放）", 13, Ui.Mute, TextAnchor.MiddleLeft);
        }
    }
}
