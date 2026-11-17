using System.Linq;
using UnityEngine;

namespace ThreadZero
{
    public sealed partial class GameApp
    {
        private void WardrobePage()
        {
            int owned = model.state.collection.Count, total = Catalog.All.Length;

            // 左：穿搭预览与收集进度
            Ui.Fill(128, 20, 440, 696, Ui.Stage);
            Ui.Text(330, 6, 240, 250, owned.ToString(), 220, Ui.Ghost, TextAnchor.UpperRight, true, true);
            Ui.Fill(128, 648, 440, 68, Ui.Floor); Ui.Fill(128, 646, 440, 3, Ui.Ink);
            Ui.Text(146, 30, 200, 44, "衣柜", 34, Ui.Ink, TextAnchor.MiddleLeft, true);
            Ui.Text(147, 76, 300, 20, "穿搭预览 · 只改变样子", 13, Ui.Mute, TextAnchor.MiddleLeft);
            PreviewHero(348, 662, 600, 128, 110, 440, 560);
            Ui.Frame(128, 20, 440, 696, 2, Ui.Ink);

            Ui.Fill(128, 728, 440, 88, Ui.Ink);
            Ui.Text(144, 738, 200, 30, "外观收集", 15, Ui.Panel, TextAnchor.MiddleLeft, true);
            string totalText = " / " + total; float totalWidth = Ui.Width(totalText, 18, false, true);
            Ui.Text(344, 734, 208 - totalWidth, 36, owned.ToString(), 32, Ui.Panel, TextAnchor.LowerRight, true, true);
            Ui.Text(552 - totalWidth, 734, totalWidth + 4, 33, totalText, 18, Ui.MuteOnInk, TextAnchor.LowerLeft, false, true);
            float segment = (408f - (total - 1) * 3) / total;
            for (int i = 0; i < total; i++) Ui.Fill(144 + i * (segment + 3), 786, segment, 10, i < owned ? Ui.Panel : Ui.Steel);
            bool custom = model.state.appearance.Any(id => !string.IsNullOrEmpty(id));
            if (Ui.Button(128, 828, 440, 48, "恢复实际装备外观", 0, custom, 15))
            { model.state.appearance = new string[Catalog.SlotCount]; Save(); Notify("已恢复实际装备外观"); }

            // 右上：部位标签
            for (int i = 0; i < Catalog.SlotCount; i++)
            {
                const float tabW = 190; int slot = i; float x = 588 + i * 197.5f;
                string count = Catalog.All.Count(b => (int)b.slot == slot && model.state.collection.Contains(b.id)) + " / " + Catalog.All.Count(b => (int)b.slot == slot);
                bool on = wardrobeSlot == i, hover = Ui.Hover(x, 20, tabW, 52);
                Ui.Fill(x, 20, tabW, 52, on ? Ui.Ink : hover ? Ui.Rule : Ui.Panel); Ui.Frame(x, 20, tabW, 52, 2, Ui.Ink);
                float labelWidth = Ui.Width(Catalog.SlotNames[i], 17, true), countWidth = Ui.Width(count, 18, true, true), start = x + (tabW - labelWidth - countWidth - 12) / 2;
                Ui.Text(start, 20, labelWidth + 4, 52, Catalog.SlotNames[i], 17, on ? Ui.Panel : Ui.Ink, TextAnchor.MiddleLeft, true);
                Ui.Text(start + labelWidth + 12, 20, countWidth + 4, 52, count, 18, on ? Ui.MuteOnInk : Ui.Mute, TextAnchor.MiddleLeft, true, true);
                if (Ui.Click(x, 20, tabW, 52)) { wardrobeSlot = i; selectedLook = Catalog.All.First(b => (int)b.slot == slot).id; }
            }

            // 右中：五款外观
            // v0.16 按章节分页：每章每个部位 5 款
            int chapters = Catalog.All.Max(b => Catalog.ChapterOf(b.id));
            for (int c = 1; c <= chapters; c++)
            {
                int ch = c; string label = "第" + "一二三四五"[Mathf.Min(4, c - 1)] + "章";
                if (Ui.Button(588 + (c - 1) * 128, 80, 120, 30, label, wardrobeChapter == c ? 2 : 0, true, 13)) { wardrobeChapter = ch; selectedLook = null; }
            }
            EquipmentBase[] looks = Catalog.All.Where(b => (int)b.slot == wardrobeSlot && Catalog.ChapterOf(b.id) == wardrobeChapter).ToArray();
            if (looks.Length == 0) { wardrobeChapter = 1; return; }
            if (!looks.Any(b => b.id == selectedLook)) selectedLook = looks[0].id;
            const float cardW = 186.4f, cardH = 252;
            for (int i = 0; i < looks.Length; i++)
            {
                EquipmentBase b = looks[i]; float x = 588 + i * (cardW + 12), y = 118;
                bool has = model.state.collection.Contains(b.id), selected = selectedLook == b.id, hover = Ui.Hover(x, y, cardW, cardH);
                if (selected) Ui.Frame(x - 4, y - 4, cardW + 8, cardH + 8, 3, Ui.Orange);
                Ui.Fill(x, y, cardW, cardH, hover ? Ui.Tile2 : Ui.Tile);
                Ui.Text(x + 10, y + 6, 60, 22, "0" + (i + 1), 15, Ui.MuteOnInk, TextAnchor.MiddleLeft, true, true);
                PaperDollView.DrawEquipmentIcon(b, Ui.R(x + (cardW - 136) / 2, y + 28, 136, 136), !has);
                Ui.Text(x, y + 170, cardW, 26, b.name, 16, has ? Ui.Panel : Ui.MuteOnInk, TextAnchor.MiddleCenter, true);
                Ui.Text(x, y + 198, cardW, 22, has ? "已收藏" : "未收藏 · " + Catalog.SourceStages(b.id), 13, has ? Ui.MuteOnInk : Ui.Grey, TextAnchor.MiddleCenter);
                if (model.Appearance(b.slot).id == b.id) { Ui.Fill(x + cardW - 74, y, 74, 24, Ui.Panel); Ui.Text(x + cardW - 74, y, 74, 24, "当前外观", 12, Ui.Ink, TextAnchor.MiddleCenter, true); }
                if (model.state.recoveryTarget == b.id) { Ui.Fill(x, y + cardH - 24, cardW, 24, Ui.Orange); Ui.Text(x, y + cardH - 24, cardW, 24, "回收目标", 12, Ui.OnAccent, TextAnchor.MiddleCenter, true); }
                if (Ui.Click(x, y, cardW, cardH)) selectedLook = b.id;
            }

            // 右下：选中外观与定向回收
            EquipmentBase chosen = Catalog.Find(selectedLook); if (chosen == null) return;
            bool unlocked = model.state.collection.Contains(chosen.id), tracked = model.state.recoveryTarget == chosen.id, wearing = model.Appearance(chosen.slot).id == chosen.id;
            string regionName = Catalog.SourceStages(chosen.id);
            string effectName, effectText; SplitEffect(chosen.effect, out effectName, out effectText);
            Ui.Fill(588, 380, 980, 496, Ui.Panel);
            Ui.Text(612, 400, 400, 18, "LOOK / " + Catalog.SlotNames[(int)chosen.slot] + "外观", 13, Ui.Mute, TextAnchor.MiddleLeft, true, true);
            Ui.Text(612, 422, 560, 52, chosen.name, 36, Ui.Ink, TextAnchor.MiddleLeft, true);
            string regionLabel = "产地 · " + regionName; float regionWidth = Ui.Width(regionLabel, 14, true) + 58;
            Ui.Fill(612, 488, regionWidth, 32, Ui.Ink);
            Ui.Text(624, 488, 30, 32, "01", 17, Ui.Orange, TextAnchor.MiddleLeft, true, true);
            Ui.Text(654, 488, regionWidth - 46, 32, regionLabel, 14, Ui.Panel, TextAnchor.MiddleLeft, true);
            float effectWidth = Ui.Width(effectName, 14, true) + 28;
            Ui.Frame(612 + regionWidth + 10, 488, effectWidth, 32, 2, Ui.Ink);
            Ui.Text(612 + regionWidth + 10, 488, effectWidth, 32, effectName, 14, Ui.Ink, TextAnchor.MiddleCenter, true);
            Ui.Text(612, 536, 540, 56, chosen.effect == Effect.None ? "这款外观对应的装备只提供属性，没有固定模块。" : "对应装备 · " + effectText, 15, Ui.Body, TextAnchor.UpperLeft, false, false, true);
            Ui.Text(612, 726, 540, 46, "外观只改变样子。属性和模块来自「整备」里实际穿戴的装备，分解装备不会丢失已收藏的外观。", 13, Ui.Mute, TextAnchor.UpperLeft, false, false, true);
            if (Ui.Button(612, 792, 240, 60, !unlocked ? "尚未收藏" : wearing ? "当前外观" : "应用外观", 1, unlocked && !wearing, 19))
            { model.SetAppearance(chosen.id); Save(); Play(equipSound); Notify("已应用外观：" + chosen.name); }
            if (Ui.Button(862, 792, 180, 60, "查看产出街区", 0, true, 15)) { mapChapter = Catalog.ChapterOf(chosen.id); navigation.Open(GameModal.Map); }

            Ui.Fill(1188, 380, 380, 496, Ui.Ink);
            Ui.Text(1210, 400, 336, 18, "TARGET / 定向回收", 13, Ui.Orange, TextAnchor.MiddleLeft, true, true);
            Ui.Text(1210, 426, 336, 60, tracked ? "正在定向回收这款外观" : "想要这一款？设为回收目标", 20, Ui.Panel, TextAnchor.UpperLeft, true, false, true);
            int misses = tracked && model.TargetActive ? model.state.targetBossMisses : 0, guarantee = Balance.TargetBossGuarantee;
            float pip = (336f - (guarantee - 1) * 5) / guarantee;
            for (int i = 0; i < guarantee; i++) Ui.Fill(1210 + i * (pip + 5), 500, pip, 14, i < misses ? Ui.Orange : Ui.Steel);
            string note = !tracked ? "设为目标后，击败精英或首领时更容易掉落它，连续 " + (guarantee - 1) + " 次未掉落则第 " + guarantee + " 次必得。"
                : model.TargetActive ? "击败精英或首领时更容易掉落。已 " + misses + " 次未掉落，最多再 " + (guarantee - misses) + " 次必得。"
                : "初始装备不会掉落，换一件作为目标吧。";
            Ui.Text(1210, 530, 336, 110, note, 14, Ui.MuteOnInk, TextAnchor.UpperLeft, false, false, true);
            if (Ui.Button(1210, 796, 336, 56, tracked ? "取消回收目标" : "设为回收目标", tracked ? 3 : 1, true, 17))
            { model.SetRecoveryTarget(tracked ? null : chosen.id); Save(); }
            Ui.Frame(588, 380, 980, 496, 2, Ui.Ink);
        }
    }
}
