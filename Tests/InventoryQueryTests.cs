using System;
using System.Linq;
using ThreadZero;

public static class InventoryQueryTests
{
    static int passed;
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); passed++; }
    public static void Main()
    {
        var m = new GameModel(null, 42); m.state.items.Clear();
        var a = m.Create("w0", Quality.Common, 10);
        var b = m.Create("w1", Quality.Legendary, 2);
        var c = m.Create("c0", Quality.Rare, 20);
        var d = m.Create("w0", Quality.Common, 10);
        a.affixes.Clear(); b.affixes.Clear(); c.affixes.Clear(); d.affixes.Clear();
        a.affixes.Add(new Affix { stat = 4, value = .05f });
        b.locked = true; m.state.items.AddRange(new[] { a, b, c, d });
        m.state.equipped = new[] { a.uid, null, null, null };
        var original = m.state.items.Select(i => i.uid).ToArray();
        var q = new InventoryQuery();
        Check(q.Apply(m.state).SequenceEqual(new[] { d,c,b,a }), "newest first");
        q.descending = false; Check(q.Apply(m.state).First() == a, "oldest first");
        q.sort = InventorySort.Quality; q.descending = true;
        Check(q.Apply(m.state).SequenceEqual(new[] { b,c,d,a }), "quality and stable tie");
        q.sort = InventorySort.Level; Check(q.Apply(m.state).First() == c, "level descending");
        q.descending = false; Check(q.Apply(m.state).First() == b, "level ascending");
        q.slot = 0; q.qualityMask = 1; q.minLevel = q.maxLevel = 10;
        Check(q.Apply(m.state).Count == 2, "combined slot quality level inclusive");
        q.qualityMask = 0; Check(q.Apply(m.state).Count == 0, "no qualities selected");
        q = new InventoryQuery { status = InventoryStatus.Equipped }; Check(q.Apply(m.state).Single() == a, "equipped");
        q.status = InventoryStatus.Unequipped; Check(q.Apply(m.state).Count == 3, "unequipped");
        q.status = InventoryStatus.Locked; Check(q.Apply(m.state).Single() == b, "locked");
        q.status = InventoryStatus.Unlocked; Check(q.Apply(m.state).Count == 3, "unlocked");
        q = new InventoryQuery { affix = 4 }; Check(q.Apply(m.state).Single() == a, "actual random affix, not base stat");
        q = new InventoryQuery { effect = (int)Catalog.Find("w1").effect }; Check(q.Apply(m.state).Single() == b, "fixed effect");
        q = new InventoryQuery { search = " W0 " }; Check(q.Apply(m.state).Count == 2, "trim and case insensitive search");
        q.search = Catalog.Find("c0").name; Check(q.Apply(m.state).Single() == c, "Chinese name search");
        q = new InventoryQuery { minLevel = 20, maxLevel = 10 }; Check(q.Apply(m.state).Count == 0, "inverted range is empty");
        q = new InventoryQuery { sort = InventorySort.Crit }; Check(q.Apply(m.state).First() == a, "rolled attribute sort");
        foreach (InventorySort sort in Enum.GetValues(typeof(InventorySort)))
        {
            q = new InventoryQuery { sort = sort }; Check(q.Apply(m.state).Count == 4, "sort keeps all items " + sort);
        }
        Check(original.SequenceEqual(m.state.items.Select(i => i.uid)), "source order unchanged");
        Check(m.state.equipped[0] == a.uid && b.locked, "equipped and lock unchanged");
        Check(!m.Salvage(a.uid) && !m.Salvage(b.uid), "salvage protections unchanged");
        Check(new InventoryQuery().ActiveCount == 0, "reset all filters");
        Console.WriteLine("Inventory query: " + passed + " checks PASS");
    }
}
