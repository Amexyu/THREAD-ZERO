using System;
using System.Collections.Generic;
using System.Linq;

namespace ThreadZero
{
    public enum InventorySort { Acquired, Quality, Level, Health, Attack, Defense, Haste, Crit, Luck }
    public enum InventoryStatus { All, Equipped, Unequipped, Locked, Unlocked }

    // Display-only query: never reorder the save's acquisition list or change equipment.
    public sealed class InventoryQuery
    {
        // v0.20 等级上限默认改成 Balance.MaxItemLevel（300）：原来写死 100，回声层 60 层以后掉的 100 级以上装备、工坊升到 100 级以上的装备
        //       在整备页的背包里直接被筛掉了（没开筛选也一样），看不到也穿不上。
        public int slot = -1, qualityMask = 63, minLevel = 1, maxLevel = Balance.MaxItemLevel, affix = -1, effect = -1;
        public string search = "";
        public InventoryStatus status;
        public InventorySort sort;
        public bool descending = true;

        public int ActiveCount { get { return (qualityMask != 63 ? 1 : 0) + (minLevel != 1 || maxLevel != Balance.MaxItemLevel ? 1 : 0)
            + (affix >= 0 ? 1 : 0) + (effect >= 0 ? 1 : 0) + (status != InventoryStatus.All ? 1 : 0) + (!string.IsNullOrWhiteSpace(search) ? 1 : 0); } }

        public List<Item> Apply(GameState state)
        {
            string needle = (search ?? "").Trim();
            var equipped = new HashSet<string>(state.equipped);
            var rows = state.items.Select((item, index) => new { item, index, basis = Catalog.Find(item.baseId) })
                .Where(r => r.basis != null && (slot < 0 || (int)r.basis.slot == slot)
                    && (qualityMask & (1 << (int)r.item.quality)) != 0
                    && r.item.level >= minLevel && r.item.level <= maxLevel
                    && (affix < 0 || r.item.affixes.Any(a => a.stat == affix))
                    && (effect < 0 || (int)r.basis.effect == effect)
                    && (needle.Length == 0 || r.basis.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 || r.basis.id.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    && (status == InventoryStatus.All || status == InventoryStatus.Equipped && equipped.Contains(r.item.uid)
                        || status == InventoryStatus.Unequipped && !equipped.Contains(r.item.uid)
                        || status == InventoryStatus.Locked && r.item.locked || status == InventoryStatus.Unlocked && !r.item.locked));
            Func<Item, float> key = item => SortValue(item, sort);
            // Equal values always use newest acquisition first; UID is never a timestamp.
            if (sort == InventorySort.Acquired)
                return (descending ? rows.OrderByDescending(r => r.index) : rows.OrderBy(r => r.index)).Select(r => r.item).ToList();
            return (descending ? rows.OrderByDescending(r => key(r.item)) : rows.OrderBy(r => key(r.item)))
                .ThenByDescending(r => r.item.level).ThenByDescending(r => r.item.quality).ThenByDescending(r => r.index).Select(r => r.item).ToList();
        }

        private static float SortValue(Item item, InventorySort mode)
        {
            if (mode == InventorySort.Quality) return (int)item.quality;
            if (mode == InventorySort.Level) return item.level;
            Stats s = GameModel.ItemStats(item);
            switch (mode) {
                case InventorySort.Health: return s.health; case InventorySort.Attack: return s.attack;
                case InventorySort.Defense: return s.defense; case InventorySort.Haste: return s.haste;
                case InventorySort.Crit: return s.crit; case InventorySort.Luck: return s.luck; default: return 0;
            }
        }
    }
}
