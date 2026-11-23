using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// v0.22 回收终端：工坊第四个页签。花零件抽装备，给「卡住时只能等掉落」多一个出口。
    ///   部位回收（第三章开放）：指定部位，品质和平时掉落一样，优先出图鉴里没有的。
    ///   深层回收（打完主线开放）：部位随机，至少特装，有保底。
    /// 规则和价格在 GameModel.TerminalDraw / Balance.Terminal*。
    public sealed partial class GameApp
    {
        private int terminalSlot;                                  // 部位回收选中的部位
        private readonly List<string> terminalGot = new List<string>();   // 这一次抽到的装备（uid），右边展示
        private int terminalNew; private float terminalAt = -10;

        public void TestTerminal(int scrap) { model.state.scrap += scrap; workshopTab = 3; Go(GameScreen.Workshop); }
        public void TestTerminalDraw(int deep) { TerminalRun(deep > 0, 10); }

        private void TerminalRun(bool deep, int times)
        {
            terminalGot.Clear(); terminalNew = 0; int best = 0;
            for (int i = 0; i < times; i++)
            {
                bool had = false; Item item = null;
                int before = model.state.collection.Count;
                item = model.TerminalDraw(deep, terminalSlot); if (item == null) break;
                had = model.state.collection.Count == before;
                if (!had) terminalNew++;
                terminalGot.Add(item.uid); unseen.Add(item.uid); best = Mathf.Max(best, (int)item.quality);
            }
            if (terminalGot.Count == 0) return;
            terminalAt = Time.unscaledTime; Save(); Play(best >= 2 ? rareSound : dropSound);
            if (best >= 4) { Impact.Shake(clock, best >= 5 ? 6 : 3, .24f); Sfx("unlock", .6f, .3f); }
        }

        private void TerminalPage()
        {
            int level = model.TerminalLevel;
            // ---------- 左：两种回收 ----------
            for (int k = 0; k < 2; k++)
            {
                bool deep = k == 1, open = deep ? model.DeepTerminalUnlocked : model.TerminalUnlocked;
                float x = 128, y = 156 + k * 366, w = 604, h = 354;
                Ui.Fill(x, y, w, h, Ui.Panel); Ui.Frame(x, y, w, h, 2, Ui.Ink); Ui.Fill(x, y, 8, h, deep ? QualityVisual.Colors[4] : Ui.Orange);
                Ui.Text(x + 28, y + 14, 300, 34, deep ? "深层回收" : "部位回收", 24, Ui.Ink, TextAnchor.MiddleLeft, true);
                Ui.Text(x + w - 228, y + 18, 208, 26, "装备等级 Lv." + level, 14, Ui.Mute, TextAnchor.MiddleRight, true);
                Ui.Text(x + 28, y + 54, w - 56, 60, deep
                    ? "从织网深处捞一件装备：部位随机，至少是特装。出不出货都算一次保底。"
                    : "指定一个部位，回收一件这个部位的装备。品质和平时掉落一样，优先给图鉴里还没有的。", 14, Ui.Body, TextAnchor.UpperLeft, false, false, true);
                if (!deep)
                {
                    for (int s = 0; s < Catalog.SlotCount; s++)
                        if (Ui.Button(x + 28 + s * 111, y + 122, 105, 44, Catalog.SlotNames[s], terminalSlot == s ? 2 : 0, open, 14)) terminalSlot = s;
                    int have = 0, all = 0;
                    foreach (EquipmentBase b in Catalog.All) if ((int)b.slot == terminalSlot && !Catalog.IsStarter(b.id) && Catalog.ChapterOf(b.id) <= 5) { all++; if (model.state.collection.Contains(b.id)) have++; }
                    Ui.Text(x + 28, y + 180, w - 56, 24, "这个部位的图鉴 " + have + " / " + all, 14, Ui.Mute, TextAnchor.MiddleLeft);
                }
                else
                {
                    double sum = 0; foreach (double v in Balance.TerminalDeepWeights) sum += v;
                    float cx = x + 28;
                    for (int q = 2; q < Balance.TerminalDeepWeights.Length; q++)
                    {
                        Ui.Fill(cx, y + 124, 130, 40, Ui.Ink); Ui.Fill(cx, y + 124, 5, 40, QualityVisual.Colors[q]);
                        Ui.FitText(cx + 12, y + 124, 70, 40, Catalog.QualityNames[q], 14, QualityVisual.Colors[q], TextAnchor.MiddleLeft, true);
                        Ui.Text(cx + 78, y + 124, 46, 40, (Balance.TerminalDeepWeights[q] / sum * 100).ToString("0") + "%", 15, Ui.Panel, TextAnchor.MiddleRight, true, true);
                        cx += 138;
                    }
                    Ui.Text(x + 28, y + 180, w - 56, 24, open ? "保底：再 " + (Balance.TerminalDeepPity - model.state.terminalPity) + " 次内必出机密以上" : "打完主线后开放", 14, open ? Ui.Mute : Ui.OrangeInk, TextAnchor.MiddleLeft);
                }
                int cost = model.TerminalCost(deep);
                Ui.Fill(x + 28, y + 218, w - 56, 1, Ui.Rule);
                for (int j = 0; j < 2; j++)
                {
                    int times = j == 0 ? 1 : 10; float bx = x + 28 + j * 280;
                    if (Ui.Button(bx, y + 238, 268, 60, "回收 " + times + " 次 · 零件 " + cost * times, j == 1 ? 1 : 0, open && model.CanTerminal(deep, times), 16)) TerminalRun(deep, times);
                }
                Ui.Text(x + 28, y + 310, w - 56, 26, "持有零件 " + model.state.scrap, 13, Ui.Mute, TextAnchor.MiddleLeft, false, true);
            }

            // ---------- 右：这一次抽到的 ----------
            Ui.Fill(748, 156, 820, 720, Ui.Panel); Ui.Frame(748, 156, 820, 720, 2, Ui.Ink);
            Ui.Text(776, 170, 500, 34, "回收结果", 22, Ui.Ink, TextAnchor.MiddleLeft, true);
            if (terminalGot.Count == 0)
            {
                Ui.Text(776, 420, 764, 60, "用不上的装备分解成零件，零件可以在这里换成新装备。抽到的装备直接进仓库。", 15, Ui.Mute, TextAnchor.MiddleCenter, false, false, true);
                return;
            }
            if (terminalNew > 0) Ui.Text(1180, 172, 360, 30, "新外观 " + terminalNew + " 件", 15, Ui.OrangeInk, TextAnchor.MiddleRight, true);
            const float tw = 144, th = 176;
            float since = Time.unscaledTime - terminalAt;
            for (int i = 0; i < terminalGot.Count; i++)
            {
                Item item = FindItem(terminalGot[i]); if (item == null) continue;
                if (since < i * .06f) continue;   // 一件一件亮出来
                EquipmentBase b = Catalog.Find(item.baseId); Color qc = QualityVisual.Colors[(int)item.quality];
                float x = 776 + (i % 5) * (tw + 11), y = 222 + (i / 5) * (th + 14);
                bool hover = Ui.Hover(x, y, tw, th);
                Ui.Fill(x, y, tw, th, hover ? Ui.Tile2 : Ui.Tile);
                PaperDollView.DrawEquipmentIcon(b, Ui.R(x + (tw - 92) / 2, y + 20, 92, 92));
                Ui.QualityCard(x, y, tw, th, item.quality);
                Ui.FitText(x + 4, y + 118, tw - 8, 22, b.name, 13, qc, TextAnchor.MiddleCenter, true);
                Ui.FitText(x + 4, y + 142, tw - 8, 20, Catalog.QualityNames[(int)item.quality] + " · Lv." + item.level, 12, Ui.MuteOnInk, TextAnchor.MiddleCenter);
                Ui.Fill(x, y + th - 5, tw, 5, qc);
                if (Ui.Click(x, y, tw, th)) { workshopTab = 0; SelectItem(item); }
            }
            Ui.Text(776, 222 + 2 * (th + 14) + 6, 764, 26, "点一件装备，回到装备台查看。", 13, Ui.Mute, TextAnchor.MiddleLeft);
        }
    }
}
