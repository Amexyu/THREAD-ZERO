using UnityEngine;

namespace ThreadZero
{
    public sealed partial class GameApp
    {
        // ---------- 11-25 v0.31 卡 32：三个回收潮牌的测试外观（不进装备表、不存档，只给桥截图 / 看效果） ----------
        // 部件在 Resources/Art/V13/Parts/<id>/：h / c / b / l / w 加 81 路锥俱乐部、82 信号鬼、83 投币帮；84 起是备选（投币帮的另一版头等）。
        private static readonly string[][] BrandLooks =
        {
            null,
            new[] { "w81", "h81", "c81", "b81", "l81" },   // 路锥俱乐部 CONE CLUB
            new[] { "w82", "h82", "c82", "b82", "l82" },   // 信号鬼 GHOSTWAVE
            new[] { "w83", "h83", "c83", "b83", "l83" },   // 投币帮 COIN-OP（头部备选：h86 毛线帽、h87 头盔帽、h84 扭蛋机直接当头）
        };
        // 测试武器借的旧武器：动作和招式跟着它走，外观的 weapon_class 也是照它切的（霰弹枪借 w0、音波炮借 w3 手炮、汽水枪借 w6 钉枪）。
        private static readonly string[] BrandMech = { null, "w0", "w3", "w6" };

        /// 测试：整套换成第 n 个潮牌的外观（0 = 恢复正常），顺便装上同招式的旧武器，动作和外观对得上。
        public void TestBrandLook(int brand)
        {
            brand = Mathf.Clamp(brand, 0, BrandLooks.Length - 1);
            for (int s = 0; s < 5; s++) LayeredRigView.TestLook[s] = brand == 0 ? null : BrandLooks[brand][s];
            if (brand > 0) TestEquip(BrandMech[brand]);
        }

        /// 测试：单独换一个部位的外观（slot：0 武器、1 头、2 外套、3 背包、4 下装；id 写 - 表示不覆盖）。看混搭和备选用。
        public void TestPartLook(int slot, string id)
        {
            LayeredRigView.TestLook[Mathf.Clamp(slot, 0, 4)] = string.IsNullOrEmpty(id) || id == "-" ? null : id;
        }
    }
}
