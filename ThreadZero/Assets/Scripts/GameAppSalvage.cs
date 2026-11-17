using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    // 一键分解与自动分解。穿戴中、已锁定的装备永远不会被处理；收藏的外观不受分解影响。
    public sealed partial class GameApp
    {
        private static readonly string[] AutoSalvageNames = { "关闭", "仅量产", "改良及以下" };
        private int autoSalvagedCount, autoSalvagedScrap;

        private bool CanSalvage(Item item)
        { return item != null && !item.locked && Array.IndexOf(model.state.equipped, item.uid) < 0; }

        // 统计（execute 为 false）或执行（true）：分解整个仓库里品质不高于 maxQuality 的装备。
        private void SalvageTier(int maxQuality, bool execute, out int count, out int gain)
        {
            count = 0; gain = 0;
            foreach (Item item in new List<Item>(model.state.items))
            {
                if ((int)item.quality > maxQuality || !CanSalvage(item)) continue;
                int value = GameModel.SalvageValue(item);
                if (execute)
                {
                    if (!model.Salvage(item.uid)) continue;
                    recentDrops.Remove(item.uid); unseen.Remove(item.uid);
                    if (selectedItem == item.uid) selectedItem = null;
                }
                count++; gain += value;
            }
        }

        // 一键分解弹窗：三个档位各一个大按钮，点哪个就分解到哪个品质为止。
        private void SalvageModal(float x, float y, float w)
        {
            Ui.Text(x + 32, y + 92, w - 64, 48, "选一个档位，仓库里这个品质及以下的装备会全部换成零件。穿戴中和已锁定的不会动，收藏的外观保留。", 14, Ui.Mute, TextAnchor.UpperLeft, false, false, true);
            for (int tier = 0; tier < 3; tier++)
            {
                int count, gain; SalvageTier(tier, false, out count, out gain);
                float top = y + 156 + tier * 124;
                Ui.Fill(x + 32, top, w - 64, 108, Ui.Stage); Ui.Frame(x + 32, top, w - 64, 108, 1, Ui.Rule);
                Ui.Fill(x + 32, top, 8, 108, QualityVisual.Colors[tier]);
                Ui.Text(x + 60, top + 18, 300, 34, Catalog.QualityNames[tier] + (tier > 0 ? "及以下" : ""), 22, Ui.Ink, TextAnchor.MiddleLeft, true);
                Ui.Text(x + 60, top + 56, 340, 28, count == 0 ? "没有可分解的装备" : count + " 件  →  +" + gain + " 零件", 15, Ui.Mute, TextAnchor.MiddleLeft);
                if (Ui.Button(x + w - 212, top + 28, 160, 52, "分解", 1, count > 0, 17))
                {
                    SalvageTier(tier, true, out count, out gain);
                    inventoryScroll = 0; Save(); navigation.Close();
                    Notify("已分解 " + count + " 件，获得 " + gain + " 个零件。", 4);
                    return;
                }
            }
            Ui.Text(x + 32, y + 540, w - 64, 44, "想留下的装备，先在右侧详情里点「锁定」。不想每次手动点，可以在设置里打开自动分解。", 13, Ui.Mute, TextAnchor.UpperLeft, false, false, true);
        }

        // 掉落时调用。品质低于设定档位的装备直接换成零件，不进仓库。
        // 例外：第一次获得的外观、当前的回收目标，照常进仓库，让玩家看得到。
        private bool TryAutoSalvage(Item item, bool fresh)
        {
            if (item == null || fresh || (int)item.quality >= model.state.autoSalvage || item.baseId == model.state.recoveryTarget) return false;
            int value = GameModel.SalvageValue(item);
            if (!model.Salvage(item.uid)) return false;
            autoSalvagedCount++; autoSalvagedScrap += value;
            return true;
        }
    }
}
