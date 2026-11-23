using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ThreadZero
{
    // v0.10 工坊：装备台（升级、重校词条、突破）· v0.21 改造板 · 机库（GameAppHangar.cs）。
    public sealed partial class GameApp
    {
        private float workshopScroll;
        private int workshopTab;   // v0.21 0 装备台 · 1 改造板 · 2 机库 · 3 回收终端（v0.22）

        // ---------- v0.21 改造板（取代训练）：三条分支 + 每条 4 个改变打法的节点 ----------
        private void BoardPage()
        {
            for (int bi = 0; bi < 3; bi++)
            {
                float x = 128 + bi * 484, w = 468;
                int lv = model.state.board[bi], cost = model.BoardCost(bi);
                Ui.Fill(x, 156, w, 148, Ui.Ink); Ui.Fill(x, 156, w, 6, Ui.Orange);
                Ui.Text(x + 20, 168, 200, 34, Catalog.BoardBranches[bi], 24, Ui.Panel, TextAnchor.MiddleLeft, true);
                Ui.Text(x + w - 220, 164, 200, 42, "Lv." + lv, 32, Ui.Orange, TextAnchor.MiddleRight, true, true);
                string total = bi == 0 ? "攻击 +" + (lv * Balance.TrainingAttack).ToString("0.#") : bi == 1 ? "生命 +" + (lv * Balance.TrainingHealth).ToString("0")
                             : "零件回收 +" + (lv * Balance.BoardScrap * 100).ToString("0.#") + "%、幸运 +" + (lv * Balance.BoardLuck).ToString("0.##");
                Ui.FitText(x + 20, 204, w - 40, 22, Catalog.BoardPerLevel[bi] + " · 现在 " + total, 13, Ui.MuteOnInk, TextAnchor.MiddleLeft);
                int ten = 0; for (int k = 0; k < 10; k++) ten += Math.Max(10, (20 + 15 * (lv + k)) / 2);
                if (Ui.Button(x + 20, 238, 232, 52, "升级 · 零件 " + cost, 1, model.CanUpgradeBoard(bi), 15) && model.UpgradeBoard(bi))
                { combat.ClampHealth(); Save(); Play(equipSound); }
                if (Ui.Button(x + 262, 238, w - 282, 52, "连升 10 级 · " + ten, 3, model.state.scrap >= ten, 13))
                { for (int k = 0; k < 10; k++) model.UpgradeBoard(bi); combat.ClampHealth(); Save(); Play(equipSound); Notify(Catalog.BoardBranches[bi] + " 升到 Lv." + model.state.board[bi]); }
                for (int t = 0; t < 4; t++)
                {
                    float y = 316 + t * 140;
                    bool has = model.HasPerk(bi, t), open = model.PerkOpen(bi, t), can = model.CanBuyPerk(bi, t);
                    Ui.Fill(x, y, w, 130, has ? Ui.Ink : Ui.Panel); Ui.Frame(x, y, w, 130, has ? 0 : 1, has ? Ui.Ink : Ui.Rule);
                    if (has) Ui.Fill(x, y, 6, 130, Ui.Orange);
                    Ui.Text(x + 20, y + 10, w - 40, 18, "第" + Catalog.ChapterNumber(Catalog.PerkChapter[t]) + "章开放 · 核心 " + Catalog.PerkCores[t], 12, has ? Ui.MuteOnInk : Ui.Mute, TextAnchor.MiddleLeft);
                    Ui.Text(x + 20, y + 30, w - 40, 30, Catalog.PerkNames[bi, t], 19, has ? Ui.Orange : open ? Ui.Ink : Ui.Grey, TextAnchor.MiddleLeft, true);
                    Ui.Text(x + 20, y + 60, w - 40, 22, Catalog.PerkTexts[bi, t], 13, has ? Ui.Panel : open ? Ui.Body : Ui.Grey, TextAnchor.MiddleLeft);
                    string state = has ? "已激活" : !open ? (state0(bi, t)) : can ? "激活 · 核心 " + Catalog.PerkCores[t] : "核心不足（持有 " + model.state.cores + "）";
                    if (has) Ui.Text(x + 20, y + 90, w - 40, 30, "✓ 已激活", 14, Ui.Orange, TextAnchor.MiddleLeft, true);
                    else if (Ui.Button(x + 20, y + 88, w - 40, 34, state, can ? 1 : 0, can, 14) && model.BuyPerk(bi, t))
                    { combat.ClampHealth(); Save(); Play(rareSound); Notify("改造板节点激活：" + Catalog.PerkNames[bi, t] + " · " + Catalog.PerkTexts[bi, t], 5); }
                }
            }
        }
        private string state0(int bi, int t)
        {
            if (model.state.unlockedRegion < Chapters.FirstRegion(Catalog.PerkChapter[t])) return "第" + Catalog.ChapterNumber(Catalog.PerkChapter[t]) + "章开放";
            return "先激活「" + Catalog.PerkNames[bi, t - 1] + "」";
        }
        public void TestWorkshopTab(int tab) { workshopTab = tab; Go(GameScreen.Workshop); }
        public void TestBoard(int branch, int levels) { for (int k = 0; k < levels; k++) { model.state.scrap += model.BoardCost(branch); model.UpgradeBoard(branch); } }
        public void TestPerk(int branch, int tier) { model.state.cores += Catalog.PerkCores[tier]; model.BuyPerk(branch, tier); }

        private void WorkshopPage()
        {
            float titleW = Ui.Width("工坊", 30, true);   // v0.21 四语：标题变长时页签跟着往右挪
            Ui.Text(128, 16, titleW + 12, 52, "工坊", 30, Ui.Ink, TextAnchor.MiddleLeft, true);
            // v0.21 三个页签都能用：装备台、改造板（取代训练）、机库（机器人）
            string[] tabs = { "装备台", "改造板", model.HangarUnlocked ? "机库" : "机库 · 第二章开放", model.TerminalUnlocked ? "回收终端" : "回收终端 · 第三章开放" };
            float tabX = Mathf.Max(250, 128 + titleW + 28);
            for (int i = 0; i < 4; i++)
            {
                bool open = i < 2 || (i == 2 ? model.HangarUnlocked : model.TerminalUnlocked); float width = Math.Max(120, Ui.Width(tabs[i], 14) + 40);
                if (Ui.Button(tabX, 22, width, 40, tabs[i], workshopTab == i ? 2 : 0, open, 14) && open) workshopTab = i;
                if ((i == 0 && model.AnyChipToSocket) || (i == 1 && model.AnyPerkBuyable) || (i == 2 && model.AnyRobotAssemblable)) Ui.Fill(tabX + width - 13, 27, 8, 8, Ui.Orange);   // 11-24 这一页有能买的东西
                tabX += width + 8;
            }

            // 资源栏（v0.16 统一货币）：等级上限、零件、核心。
            string[] names = { "等级上限", "零件", "核心" };
            int[] values = { model.License, model.state.scrap, model.state.cores };
            const float chipW = (1440f - 2 * 8) / 3;
            for (int i = 0; i < 3; i++)
            {
                float x = 128 + i * (chipW + 8);
                Ui.Fill(x, 78, chipW, 62, Ui.Ink);
                if (i == 0) Ui.Fill(x, 78, 6, 62, Ui.Orange);
                Ui.Text(x + 16, 82, chipW - 24, 22, names[i], 12, Ui.MuteOnInk, TextAnchor.MiddleLeft);
                Ui.Text(x + 16, 102, chipW - 24, 34, (i == 0 ? "Lv." : "") + values[i], 24, i == 0 ? Ui.Orange : Ui.Panel, TextAnchor.MiddleLeft, true, true);
            }

            if (workshopTab == 2 && !model.HangarUnlocked) workshopTab = 0;
            if (workshopTab == 1) { BoardPage(); return; }
            if (workshopTab == 3 && !model.TerminalUnlocked) workshopTab = 0;
            if (workshopTab == 2) { HangarPage(); return; }
            if (workshopTab == 3) { TerminalPage(); return; }
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
                Ui.QualityCard(x, y, tileW, tileH, item.quality);
                LevelTag(item, x, y);
                if (model.state.equipped.Contains(item.uid)) { Ui.Fill(x + tileW - 50, y, 50, 20, Ui.Panel); Ui.Text(x + tileW - 50, y, 50, 20, "穿戴中", 11, Ui.Ink, TextAnchor.MiddleCenter, true); }
                Ui.FitText(x + 2, y + 96, tileW - 4, 22, b.name, 12, quality, TextAnchor.MiddleCenter, true);
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

        // ---------- v0.20 装备光效：升级 / 升到上限 / 突破的那一下 ----------
        // 只在装备台的大图标上演，不全屏、不闪白：品质色淡出、方框往外扩、数字往上飘。动画按不受暂停影响的时间走。
        private string benchFxUid; private float benchFxAt = -10; private int benchFxKind;   // 1 升级 2 升到当前上限 3 突破成原型
        private void UpgradeClicked(Item item)
        {
            if (!model.Upgrade(item.uid)) return;
            combat.ClampHealth(); Save(); Play(equipSound);
            benchFxUid = item.uid; benchFxAt = Time.unscaledTime; benchFxKind = item.level >= model.License ? 2 : 1;
        }
        private void AscendClicked(Item item, EquipmentBase b)
        {
            if (!model.Ascend(item.uid)) return;
            combat.ClampHealth(); Save(); Play(rareSound); Notify("突破完成：" + b.name + " 现在是「" + Catalog.QualityNames[(int)item.quality] + "」", 5);
            benchFxUid = item.uid; benchFxAt = Time.unscaledTime; benchFxKind = 3;
        }
        public void TestBenchUpgrade(int slot) { Item it = model.Equipped((Slot)slot); if (it == null) return; selectedItem = it.uid; model.state.scrap = Mathf.Max(model.state.scrap, 999999999); UpgradeClicked(it); }
        public void TestBenchAscend(int slot) { Item it = model.Equipped((Slot)slot); if (it == null) return; selectedItem = it.uid; model.state.cores = Mathf.Max(model.state.cores, 99); AscendClicked(it, Catalog.Find(it.baseId)); }

        /// 格子左上角：到当前等级上限的装备显示橙色「MAX」小标签（上限 = 工坊顶上的「等级上限」，打过下一个首领会提高），没到就照旧显示等级。
        private void LevelTag(Item item, float x, float y)
        {
            if (item.level >= model.License) MaxBadge(x + 5, y + 4);
            else Ui.Text(x + 7, y + 3, 60, 18, "Lv." + item.level, 13, Ui.MuteOnInk, TextAnchor.MiddleLeft, true, true);
        }
        private static void MaxBadge(float x, float y)
        {
            Ui.Fill(x, y, 38, 17, Ui.Orange);
            Ui.Text(x, y, 38, 17, "MAX", 12, Ui.OnAccent, TextAnchor.MiddleCenter, true, true);
        }
        private void BenchFxDraw(Item item, Color quality)
        {
            float age = Time.unscaledTime - benchFxAt;
            const float tx = 764, ty = 180, ts = 92; Vector2 c = new Vector2(tx + ts / 2, ty + ts / 2);
            if (age < .55f)
            {   // 升级（三种都有）：图标格子上一层品质色淡出，方框往外扩，新等级从升级按钮往上飘
                float u = age / .55f, e = 1 - (1 - u) * (1 - u);
                Ui.Fill(tx, ty, ts, ts, Ui.Alpha(quality, .38f * (1 - u)));
                float g = 4 + 22 * e; Ui.Frame(tx - g, ty - g, ts + 2 * g, ts + 2 * g, 3 * (1 - u) + 1, Ui.Alpha(quality, .85f * (1 - u)));
                if (benchFxKind != 3) Ui.Text(1348, 300 - 26 * e, 196, 34, "Lv." + item.level, 24, Ui.Alpha(Ui.Orange, 1 - u * u), TextAnchor.MiddleCenter, true, true);
            }
            if (benchFxKind == 2 && age < 1.2f)
            {   // 升到上限：金色的两层方框再扩一次，「MAX」从大缩到图标角上的小标签那么大，金光慢慢淡掉
                Color gold = QualityVisual.Colors[4];
                for (int i = 0; i < 2; i++)
                {
                    float u = Mathf.Clamp01((age - .08f - i * .14f) / .7f); if (u <= 0 || u >= 1) continue;
                    float g = 6 + 40 * (1 - (1 - u) * (1 - u)); Ui.Frame(tx - g, ty - g, ts + 2 * g, ts + 2 * g, 3 * (1 - u) + .5f, Ui.Alpha(gold, .9f * (1 - u)));
                }
                QualityVisual.Halo(Ui.P(c.x, c.y), 90 * Ui.K, gold, .45f * (1 - age / 1.2f));
                float k = Mathf.Clamp01(age / .22f), fade = age < .8f ? 1 : 1 - (age - .8f) / .4f;
                Ui.Text(c.x - 80, c.y - 30, 160, 60, "MAX", Mathf.Lerp(52, 34, k), Ui.Alpha(gold, k * fade), TextAnchor.MiddleCenter, true, true);
            }
            if (benchFxKind == 3 && age < 1.6f)
            {   // 突破：一道品质色的光柱从图标往上冲出标题栏，两圈方框扩散，光点往上飘，最后一团光慢慢散
                Color red = QualityVisual.Colors[5];
                float rise = Mathf.Clamp01(age / .25f), fade = age < .5f ? 1 : 1 - (age - .5f) / 1.1f, w = 34 * rise * fade;
                Ui.Fill(c.x - w / 2, 156, w, ty + ts - 156, Ui.Alpha(red, .45f * fade));
                Ui.Fill(c.x - w / 6, 156, w / 3, ty + ts - 156, Ui.Alpha(Color.Lerp(red, Color.white, .5f), .6f * fade));
                QualityVisual.Halo(Ui.P(c.x, c.y), 120 * Ui.K, red, .5f * fade);
                for (int i = 0; i < 2; i++)
                {
                    float u = Mathf.Clamp01((age - .1f - i * .18f) / .8f); if (u <= 0 || u >= 1) continue;
                    float g = 6 + 54 * (1 - (1 - u) * (1 - u)); Ui.Frame(tx - g, ty - g, ts + 2 * g, ts + 2 * g, 3 * (1 - u) + .5f, Ui.Alpha(red, .9f * (1 - u)));
                }
                for (int i = 0; i < 12; i++)
                {
                    float u = Mathf.Clamp01((age - i * .05f) / 1f); if (u <= 0 || u >= 1) continue;
                    float px = c.x + (Mathf.Repeat(i * .618f, 1) - .5f) * 110, py = ty + ts - 100 * u - 10;
                    Ui.Fill(px - 2, py - 2, 4, 4, Ui.Alpha(i % 3 == 0 ? Color.white : red, (1 - u) * .9f));
                }
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
            Ui.FitText(876, 206, 460, 40, b.name, 24, Ui.Panel, TextAnchor.MiddleLeft, true);
            Ui.Text(876, 248, 500, 22, "Lv." + item.level + "  /  许可上限 Lv." + license, 14, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            if (item.level >= license) MaxBadge(766, 182);
            if (benchFxUid == item.uid) BenchFxDraw(item, quality);

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
            else Ui.Text(772, line + 2, 540, 26, "消耗 零件 " + cost + "（持有 " + model.state.scrap + "）· 随机词条的数值会同步提高", 13, Ui.Mute, TextAnchor.MiddleLeft);
            if (Ui.Button(1348, 340, 196, 60, capped ? "已到上限" : "升级到 Lv." + (item.level + 1), 1, model.CanUpgrade(item), 17)) UpgradeClicked(item);

            // 重校 / 洗炼
            Ui.Text(772, 470, 400, 20, "RECALIBRATE / 重校 · 洗炼", 12, Ui.Mute, TextAnchor.MiddleLeft, true, true);
            if (item.affixes.Count > 0)
            {   // v0.18 洗炼：锁住的词条不动，其余全部重抽（换属性 + 重抽数值）
                int locked = model.LockedCount(item);
                if (!model.ReforgeUnlocked) Ui.Button(1196, 462, 348, 28, "洗炼 · 进入第三章后开放", 0, false, 13);
                else if (Ui.Button(1196, 462, 348, 28, "洗炼" + (locked > 0 ? "（锁 " + locked + " 条）" : "全部") + " · 零件 " + model.ReforgeCost(item), 1, model.CanReforge(item), 13))
                { if (model.Reforge(item.uid)) { combat.ClampHealth(); Save(); Play(equipSound); } }
            }
            Ui.Fill(772, 492, 772, 2, Ui.Ink);
            if (item.affixes.Count == 0) Ui.Text(772, 500, 700, 40, "这件装备没有随机词条。" + Catalog.QualityNames[1] + "及以上的装备才有。", 14, Ui.Mute, TextAnchor.MiddleLeft);
            int rerollCost = model.RerollCost(item);
            for (int i = 0; i < item.affixes.Count; i++)
            {
                Affix affix = item.affixes[i]; float y = 500 + i * 42;
                Ui.Text(772, y, 30, 40, "◆", 12, QualityVisual.Ink(item.quality), TextAnchor.MiddleLeft);
                Ui.Text(796, y, 120, 40, Catalog.StatNames[affix.stat], 16, Balance.SpecialStat(affix.stat) ? Ui.OrangeInk : Ui.Body, TextAnchor.MiddleLeft, Balance.SpecialStat(affix.stat));
                bool isLocked = (item.affixLocks & (1 << i)) != 0;
                if (Ui.Button(1270, y + 4, 70, 32, isLocked ? "已锁" : "锁定", isLocked ? 2 : 0, true, 13)) { model.ToggleLock(item, i); Save(); }
                Ui.Text(920, y, 200, 40, "+" + Value(affix.stat, affix.value), 22, Ui.Ink, TextAnchor.MiddleLeft, true, true);
                {   // 11-23 成色：这条抽到了随机范围的哪儿
                    float roll = Balance.AffixRoll(affix, item.level); bool great = roll >= Balance.GreatRoll;
                    Ui.Fill(1090, y + 18, 110, 4, Ui.Alpha(Ui.Ink, .55f)); Ui.Fill(1090, y + 18, 110 * Mathf.Max(.05f, roll), 4, great ? Ui.Orange : QualityVisual.Colors[(int)item.quality]);
                    Ui.Text(1206, y, 60, 40, Mathf.RoundToInt(roll * 100) + "%", 13, great ? Ui.OrangeInk : Ui.Mute, TextAnchor.MiddleLeft, true, true);
                }
                Ui.Fill(772, y + 40, 772, 1, Ui.Hair);
                if (Ui.Button(1348, y + 4, 196, 32, model.RecalibrateUnlocked ? "重校 · 零件 " + rerollCost : "重校 · 第二章开放", 0, model.CanReroll(item, i), 13))
                { if (model.Reroll(item.uid, i)) { combat.ClampHealth(); Save(); Play(equipSound); } }
            }
            if (item.affixes.Count > 0)
                Ui.Text(772, 500 + item.affixes.Count * 42 + 8, 772, 40, "重校：只换这一条。洗炼：没锁的全部重抽，每锁一条费用多一倍。特装及以上可能出橙色的特殊词条（暴击伤害、特效伤害、首领伤害、零件回收）。", 12, Ui.Mute, TextAnchor.UpperLeft, false, false, true);   // 一行放不下（会顶出面板边），自动换行

            // 突破（仅「机密」）
            if (item.quality == Quality.Legendary || item.quality == Quality.Prototype)
            {   // v0.22 原型还能再突破一次：打完主线后变成「零号」，多一条专属词条
                bool second = item.quality == Quality.Prototype, open = model.AscendOpen(item); int to = second ? 6 : 5;
                Ui.Fill(772, 756, 772, 60, Ui.Ink); Ui.Fill(772, 756, 8, 60, QualityVisual.Colors[to]);
                string need = !open ? (second ? "打完主线后开放：用核心把「" : "进入第四章后开放：用核心把「") + Catalog.QualityNames[to - 1] + "」突破为「" + Catalog.QualityNames[to] + "」"
                    : item.level < model.AscendLevel ? "先把等级升到 Lv." + model.AscendLevel + "，再用核心突破"
                    : "突破为「" + Catalog.QualityNames[to] + (second ? "」· 多一条专属词条，属性更高" : "」· 多一条词条，属性更高");
                Ui.FitText(796, 756, 540, 60, need, 15, !open || item.level < model.AscendLevel ? Ui.MuteOnInk : Ui.Panel, TextAnchor.MiddleLeft, true);
                if (Ui.Button(1348, 762, 188, 48, "核心 " + model.AscendCost(item) + "（持有 " + model.state.cores + "）", 1, model.CanAscend(item), 14)) AscendClicked(item, b);
            }
            ChipRow(item, 826);
        }

        // ---------- v0.21 芯片与改装件（企划 9A 第二步，第五章开放） ----------
        private string socketUid; private int socketSlot;   // 正在操作的装备；socketSlot = 芯片槽序号，-1 = 改装位
        private void ChipRow(Item item, float y)
        {
            int slots = model.ChipSlotsOf(item);
            if (!model.ChipsUnlocked) { Ui.Text(772, y + 8, 772, 24, "芯片槽 " + slots + " 个 · 改装位 1 个 · 进入第五章后开放", 13, Ui.Mute, TextAnchor.MiddleLeft); return; }
            Ui.Text(772, y, 44, 42, "芯片", 13, Ui.Mute, TextAnchor.MiddleLeft, true);
            if (slots == 0) Ui.Text(820, y, 500, 42, "没有芯片槽（特装及以上才有）", 13, Ui.Mute, TextAnchor.MiddleLeft);
            for (int i = 0; i < slots; i++)
            {
                float x = 820 + i * 172; Chip c = item.chips != null && i < item.chips.Count ? item.chips[i] : null;
                Ui.Fill(x, y, 164, 42, Ui.Hover(x, y, 164, 42) ? Ui.Tile2 : Ui.Ink);
                if (c != null)
                {
                    Ui.Text(x + 10, y + 2, 150, 20, Catalog.ChipLabel(c), 13, Ui.Panel, TextAnchor.MiddleLeft, true);
                    Ui.Text(x + 10, y + 21, 150, 18, Catalog.ChipEffectText(c, item.level), 12, Ui.Orange, TextAnchor.MiddleLeft);
                }
                else Ui.Text(x, y, 164, 42, model.state.chipBag.Count > 0 ? "空槽 · 植入" : "空槽", 13, Ui.MuteOnInk, TextAnchor.MiddleCenter, true);
                if (Ui.Click(x, y, 164, 42)) OpenSocket(item, i);
            }
            Ui.Fill(1348, y, 196, 42, Ui.Hover(1348, y, 196, 42) ? Ui.Tile2 : Ui.Ink);
            Ui.Text(1358, y + 2, 180, 18, "改装位", 11, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            Ui.Text(1358, y + 19, 180, 22, item.mod > 0 ? Catalog.EffectShort((Effect)item.mod) : model.ModCount > 0 ? "空 · 装上" : "空 · 制作", 14, item.mod > 0 ? Ui.Orange : Ui.Panel, TextAnchor.MiddleLeft, true);
            if (Ui.Click(1348, y, 196, 42)) OpenSocket(item, -1);
        }
        private void OpenSocket(Item item, int slot) { socketUid = item.uid; socketSlot = slot; navigation.Open(GameModal.Socket); }
        public void TestOpenSocket(int slot, int which) { Item it = model.Equipped((Slot)slot); if (it != null) { selectedItem = it.uid; OpenSocket(it, which); } }
        /// 测试用：给第 slot 个部位的装备植入库存里第一块芯片（放进第 chipSlot 个槽）；给某部位装上 effect 号改装件（没有就先用核心做一件）。
        public void TestInsertChip(int slot, int chipSlot) { Item it = model.Equipped((Slot)slot); if (it != null && model.state.chipBag.Count > 0) { model.InsertChip(it, chipSlot, 0); combat.ClampHealth(); } }
        public void TestMod(int slot, int effect) { Item it = model.Equipped((Slot)slot); if (it == null) return; if (model.state.modBag[effect] <= 0) { model.state.cores += Balance.ModCraftCores; model.CraftMod((Effect)effect); } model.InstallMod(it, (Effect)effect); combat.SyncLoadout(); }
        public void TestChips(int n) { for (int i = 0; i < n; i++) model.NewChip(1 + i % 3); }

        private void SocketModal(float x, float y, float w)
        {
            Item item = FindItem(socketUid); if (item == null) { navigation.Close(); return; }
            EquipmentBase b = Catalog.Find(item.baseId);
            float row = y + 90;
            Ui.Text(x + 32, row, w - 64, 26, b.name + " · " + Catalog.QualityNames[(int)item.quality] + " · Lv." + item.level, 15, Ui.Ink, TextAnchor.MiddleLeft, true);
            row += 34;
            if (socketSlot >= 0)
            {
                Chip old = item.chips != null && socketSlot < item.chips.Count ? item.chips[socketSlot] : null;
                string note = old != null ? "这个槽里已有「" + Catalog.ChipLabel(old) + "」（" + Catalog.ChipEffectText(old, item.level) + "），植入新的会把它销毁。"
                                          : "芯片植入后不能拆。以后往这个槽里植入新的，旧的会被销毁。数值随这件装备的等级上涨。";
                Ui.Text(x + 32, row, w - 64, 44, note, 13, old != null ? Ui.OrangeInk : Ui.Mute, TextAnchor.UpperLeft, false, false, true);
                row += 50;
                var groups = model.state.chipBag.Select((c, i) => new { c, i }).GroupBy(g => g.c.kind * 10 + g.c.tier)
                    .OrderByDescending(g => g.Key % 10).ThenBy(g => g.Key / 10).ToList();
                if (groups.Count == 0) Ui.Text(x + 32, row, w - 64, 40, "还没有芯片。精英、首领有机会掉落，深网首领层更容易。", 14, Ui.Mute, TextAnchor.MiddleLeft);
                foreach (var g in groups)
                {
                    if (row > y + 580) break;
                    Chip c = g.First().c; int bagIndex = g.First().i;
                    Ui.Fill(x + 32, row, w - 64, 44, Ui.Ink);
                    Ui.Text(x + 46, row, 210, 44, Catalog.ChipLabel(c) + (g.Count() > 1 ? "  ×" + g.Count() : ""), 15, Ui.Panel, TextAnchor.MiddleLeft, true);
                    Ui.Text(x + 260, row, 260, 44, Catalog.ChipEffectText(c, item.level), 14, Ui.Orange, TextAnchor.MiddleLeft, true);
                    if (Ui.Button(x + w - 150, row + 6, 104, 32, "植入", 1, true, 13) && model.InsertChip(item, socketSlot, bagIndex))
                    { combat.ClampHealth(); Save(); Play(rareSound); Notify("已植入：" + Catalog.ChipLabel(c) + " · " + Catalog.ChipEffectText(c, item.level)); navigation.Close(); return; }
                    row += 50;
                }
            }
            else
            {
                Ui.Text(x + 32, row, w - 64, 44, "改装件给这件装备加一个模块效果，和装备自带的效果算在一起（同一种最多两份）。可以随时换，换下来的回到库存。", 13, Ui.Mute, TextAnchor.UpperLeft, false, false, true);
                row += 50;
                if (item.mod > 0)
                {
                    Ui.Text(x + 32, row, 300, 34, "当前：" + Catalog.EffectShort((Effect)item.mod), 15, Ui.Ink, TextAnchor.MiddleLeft, true);
                    if (Ui.Button(x + w - 150, row + 1, 104, 32, "卸下", 0, true, 13)) { model.InstallMod(item, Effect.None); combat.SyncLoadout(); Save(); Play(equipSound); }
                    row += 40;
                }
                for (int k = 1; k <= 7; k++)
                {
                    Effect e = (Effect)k; int have = model.state.modBag[k];
                    string full = Catalog.EffectName(e); int cut = full.IndexOf('：');
                    Ui.Fill(x + 32, row, w - 64, 50, Ui.Ink);
                    Ui.Text(x + 46, row + 3, 300, 24, Catalog.EffectShort(e) + "   持有 " + have, 15, item.mod == k ? Ui.Orange : Ui.Panel, TextAnchor.MiddleLeft, true);
                    Ui.Text(x + 46, row + 26, 380, 20, cut > 0 ? full.Substring(cut + 1) : full, 12, Ui.MuteOnInk, TextAnchor.MiddleLeft);
                    if (Ui.Button(x + w - 276, row + 9, 100, 32, item.mod == k ? "已装上" : "装上", 1, have > 0 && item.mod != k, 13) && model.InstallMod(item, e))
                    { combat.SyncLoadout(); Save(); Play(equipSound); Notify("已装上改装件：" + Catalog.EffectShort(e)); }
                    if (Ui.Button(x + w - 168, row + 9, 122, 32, "制作 · 核心 " + Balance.ModCraftCores, 0, model.CanCraftMod, 12) && model.CraftMod(e))
                    { Save(); Play(equipSound); Notify("制作了改装件：" + Catalog.EffectShort(e) + "（持有核心 " + model.state.cores + "）"); }
                    row += 56;
                }
                Ui.Text(x + 32, row + 2, w - 64, 22, "持有核心 " + model.state.cores + "（核心也用于突破和组装机器人）", 12, Ui.Mute, TextAnchor.MiddleLeft);
            }
        }
    }
}
