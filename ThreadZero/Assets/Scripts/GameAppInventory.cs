using System;
using UnityEngine;

namespace ThreadZero
{
    public sealed partial class GameApp
    {
        private InventoryQuery inventoryQuery = new InventoryQuery();
        private bool inventoryFiltersOpen, inventorySortOpen;
        private string minimumLevel = "", maximumLevel = "";
        private static readonly string[] InventorySortNames = { "获得顺序", "稀有度", "等级", "生命", "攻击", "防御", "攻速", "暴击", "幸运" };
        private static readonly string[] InventoryStatusNames = { "全部状态", "已穿戴", "未穿戴", "已锁定", "未锁定" };

        private void ResetInventoryFilters()
        {
            var sort = inventoryQuery.sort; bool descending = inventoryQuery.descending;
            inventoryQuery = new InventoryQuery { sort = sort, descending = descending };
            minimumLevel = maximumLevel = ""; inventoryScroll = 0;
        }

        private float InventoryToolbar()
        {
            if (Ui.Button(548, 72, 154, 34, "排序：" + InventorySortNames[(int)inventoryQuery.sort], inventorySortOpen ? 2 : 0, true, 14))
            { inventorySortOpen = !inventorySortOpen; inventoryFiltersOpen = false; inventoryScroll = 0; }
            string order = inventoryQuery.sort == InventorySort.Acquired ? (inventoryQuery.descending ? "最新优先 ↓" : "最早优先 ↑") : (inventoryQuery.descending ? "从高到低 ↓" : "从低到高 ↑");
            if (Ui.Button(710, 72, 132, 34, order, 0, true, 14)) { inventoryQuery.descending = !inventoryQuery.descending; inventoryScroll = 0; }
            if (Ui.Button(850, 72, 164, 34, "筛选" + (inventoryQuery.ActiveCount > 0 ? "（" + inventoryQuery.ActiveCount + "）" : "") + (inventoryFiltersOpen ? " −" : " +"), inventoryFiltersOpen ? 2 : 0, true, 14))
            { inventoryFiltersOpen = !inventoryFiltersOpen; inventorySortOpen = false; inventoryScroll = 0; }
            if (Ui.Button(1022, 72, 118, 34, "清空筛选", 0, true, 14)) { ResetInventoryFilters(); slotFilter = -1; }
            float bottom = 112;
            if (inventorySortOpen)
            {
                for (int i = 0; i < InventorySortNames.Length; i++)
                    if (Ui.Button(548 + i % 3 * 200, 114 + i / 3 * 36, 192, 30, InventorySortNames[i], (int)inventoryQuery.sort == i ? 2 : 0, true, 14))
                    { inventoryQuery.sort = (InventorySort)i; inventorySortOpen = false; inventoryScroll = 0; }
                bottom = 224;
            }
            if (inventoryFiltersOpen)
            {
                Ui.Text(548, 118, 62, 30, "名称", 13, Ui.Mute);
                string search = InventoryInput(610, 114, 530, inventoryQuery.search, false);
                if (search != inventoryQuery.search) { inventoryQuery.search = search; inventoryScroll = 0; }
                Ui.Text(548, 154, 62, 30, "品质", 13, Ui.Mute);
                for (int i = 0; i < Catalog.QualityNames.Length; i++)
                    if (Ui.Button(610 + i * 89, 150, 85, 30, Catalog.QualityNames[i], (inventoryQuery.qualityMask & 1 << i) != 0 ? 2 : 0, true, 13))
                    { inventoryQuery.qualityMask ^= 1 << i; inventoryScroll = 0; }
                Ui.Text(548, 190, 62, 30, "等级", 13, Ui.Mute);
                string low = InventoryInput(610, 186, 70, minimumLevel, true), high = InventoryInput(710, 186, 70, maximumLevel, true);
                Ui.Text(688, 190, 20, 30, "—", 13, Ui.Mute);
                if (low != minimumLevel || high != maximumLevel) { minimumLevel = low; maximumLevel = high; inventoryScroll = 0; }
                int parsed;
                inventoryQuery.minLevel = int.TryParse(minimumLevel, out parsed) ? parsed : 1;
                inventoryQuery.maxLevel = int.TryParse(maximumLevel, out parsed) ? parsed : 100;
                Ui.Text(794, 190, 346, 30, inventoryQuery.minLevel > inventoryQuery.maxLevel ? "最低等级不能大于最高等级" : "留空不限 · 1–100", 13, inventoryQuery.minLevel > inventoryQuery.maxLevel ? Ui.OrangeInk : Ui.Mute);
                for (int i = 0; i < 5; i++)
                    if (Ui.Button(548 + i * 120, 222, 112, 30, InventoryStatusNames[i], (int)inventoryQuery.status == i ? 2 : 0, true, 13))
                    { inventoryQuery.status = (InventoryStatus)i; inventoryScroll = 0; }
                string affix = inventoryQuery.affix < 0 ? "全部随机词条" : Catalog.StatNames[inventoryQuery.affix] + "随机词条";
                if (Ui.Button(548, 258, 292, 30, affix + " ›", 0, true, 13)) { inventoryQuery.affix = (inventoryQuery.affix + 2) % 7 - 1; inventoryScroll = 0; }
                string effect = inventoryQuery.effect < 0 ? "全部固定效果" : Catalog.EffectName((Effect)inventoryQuery.effect).Split('：')[0];
                if (Ui.Button(848, 258, 292, 30, effect + " ›", 0, true, 13)) { inventoryQuery.effect = (inventoryQuery.effect + 2) % (Enum.GetValues(typeof(Effect)).Length + 1) - 1; inventoryScroll = 0; }
                Ui.Text(548, 296, 592, 24, "品质可多选；不同条件同时满足。词条／效果按钮点击切换。", 12, Ui.Mute);
                bottom = 324;
            }
            return bottom + 30;
        }

        private string InventoryInput(float x, float y, float width, string value, bool numeric)
        {
            Ui.Fill(x, y, width, 30, Ui.Panel); Ui.Frame(x, y, width, 30, 1, Ui.Rule);
            var style = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.Max(10, Mathf.RoundToInt(14 * Ui.K)), alignment = TextAnchor.MiddleLeft };
            bool enabled = GUI.enabled; GUI.enabled = enabled && !Ui.Blocked;
            string result = GUI.TextField(Ui.R(x + 4, y + 2, width - 8, 26), value, numeric ? 3 : 64, style);
            GUI.enabled = enabled;
            if (numeric) foreach (char c in result) if (c < '0' || c > '9') return value;
            return result;
        }
    }
}
