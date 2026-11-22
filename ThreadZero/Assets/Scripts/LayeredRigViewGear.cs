using UnityEngine;

namespace ThreadZero
{
    /// v0.20 装备光效：在角色身上看得出装备的品质（按正在穿的那件算，幻化只换外观、不换光）。
    ///   特装起：武器后面一圈品质色的光边，慢慢一明一暗（只呼吸、不闪，最暗也不会全灭）；
    ///   军规起：绕着武器转的光点（军规 1 / 机密 3 / 原型 4）；
    ///   机密起：攻击时武器拖品质色残影，每隔几秒一道光从握把扫到尖端；
    ///           头 / 外套 / 背包 / 下装是机密、原型的，每隔几秒一道扫光从头扫到脚（只扫到这几件，其余部位不亮）。
    ///   刀光、起手闪光、连击火花、冲刺残影的颜色按武器品质走（这部分不算光效，设置里关了也照样换色）。
    ///   设置里的「光效」：关 = 上面的光都不画；低 = 只有不动的光边；全 = 全部。
    public static partial class LayeredRigView
    {
        private static int fxMode, qWeapon = -1, qHead = -1, qCoat = -1, qBack = -1, qLegs = -1;
        private static readonly float[] EdgeAlpha = { 0, 0, .34f, .48f, .6f, .7f };   // 按品质：量产 改良 特装 军规 机密 原型
        private const float ScanPeriod = 5.2f, ScanTime = 1.3f, SweepPeriod = 3.4f, SweepTime = .5f;
        /// 测试用：≥ 0 时光效的时钟停在这个值（截图对准扫光）；平时 -1。
        public static float GearFreeze = -1;
        private static float GearNow { get { return GearFreeze >= 0 ? GearFreeze : Time.unscaledTime; } }

        private static int QualityOf(GameModel model, Slot slot)
        {
            Item item = model != null ? model.Equipped(slot) : null;
            return item != null ? (int)item.quality : -1;
        }
        private static void ReadGear(GameModel model)
        {
            fxMode = model != null ? Mathf.Clamp(model.state.glowMode, 0, 2) : 0;
            qWeapon = QualityOf(model, Slot.Weapon); qHead = QualityOf(model, Slot.Head); qCoat = QualityOf(model, Slot.Coat);
            qBack = QualityOf(model, Slot.Back); qLegs = QualityOf(model, Slot.Legs);
        }
        private static Color QC(int q, float a) { Color c = QualityVisual.Colors[Mathf.Clamp(q, 0, 5)]; c.a = Mathf.Clamp01(a); return c; }

        /// 刀光、起手闪光、连击火花用的颜色：量产 / 改良还是原来的颜色，特装起换成武器的品质色。
        private static Color GearColor(Color orig) { return qWeapon >= 2 ? QualityVisual.Colors[qWeapon] : orig; }
        private static Matrix4x4 hookM; private static Vector2 hookTip;   // 锁链类：吊钩的位置（光点绕着吊钩转）

        /// 光边的明暗：全 = 按品质的节奏慢慢呼吸（.82–1，不会闪也不会全灭）；低 = 固定偏暗。
        private static float GearPulse(int q)
        {
            if (fxMode == 1) return .6f;
            float sec = Balance.Tier((Quality)q).pulseSeconds; if (sec <= 0) sec = 3;
            return .82f + .18f * Mathf.Sin(GearNow * 2 * Mathf.PI / sec);
        }

        /// 武器（锁链类是吊钩）后面一圈品质色的光边：剪影往八个方向各挪一点叠起来，外圈淡、内圈浓。
        private static void GearEdge(Matrix4x4 bone, Part part, Color tint)
        {
            if (fxMode == 0 || qWeapon < 2 || part == null) return;
            float a = EdgeAlpha[qWeapon] * GearPulse(qWeapon) * tint.a;
            if (qWeapon >= 4 && fxMode == 2)   // 机密起武器周围再有一层很淡的光晕
            {
                Vector2 tipLocal = part == weapon ? tipPt : new Vector2(0, part.size.y - part.pivot.y);
                for (int i = 0; i < 3; i++) Glow(P(bone, tipLocal * (.25f + .3f * i)), 13, QC(qWeapon, a * .16f));
            }
            for (int ring = 0; ring < 2; ring++)
            {
                float r = ring == 0 ? 2.3f : 1.05f; Color c = QC(qWeapon, ring == 0 ? a * .2f : a * .42f);
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * Mathf.PI / 4;
                    Blit(bone * T(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r), part, false, c, flat);
                }
            }
        }

        /// 攻击时武器拖两道品质色残影（机密起，光效「全」）。
        private static void GearTrail(Anim anim, float t, Color tint)
        {
            if (fxMode < 2 || qWeapon < 4 || anim == Anim.Idle || anim == Anim.Walk) return;
            for (int k = 2; k >= 1; k--)
            {
                float back = t - k * .035f; if (back <= 0) continue;
                Blit(Build(Compute(anim, back)).wpn, weapon, false, QC(qWeapon, (k == 1 ? .3f : .16f) * tint.a), flat);
            }
        }

        /// 一道光从握把扫到尖端（机密起，光效「全」，每 3.4 秒一次）。部件图都是握把在上、尖端在下。
        private static void GearSweep(Matrix4x4 bone, Part part, Color tint)
        {
            if (fxMode < 2 || qWeapon < 4 || part == null) return;
            float ph = Mathf.Repeat(GearNow + 1.3f, SweepPeriod) / SweepTime; if (ph >= 1) return;
            Color c = Color.Lerp(QualityVisual.Colors[qWeapon], Color.white, .45f);
            SheenStrips(bone, part, false, c, Mathf.Lerp(-8, part.size.y + 8, ph), 4, .8f * tint.a, false);
        }

        /// 机密、原型的头 / 外套 / 背包 / 下装：每 5.2 秒一道扫光从头顶扫到脚底（几件一起扫，看起来是同一道光）。
        private static void GearScan(Matrix4x4 bone, Part part, bool mirror, int q, Color tint)
        {
            if (fxMode < 2 || q < 4 || part == null) return;
            float ph = Mathf.Repeat(GearNow, ScanPeriod) / ScanTime; if (ph >= 1) return;
            // 一条细亮线（像扫描线）+ 两边一点点很淡的光；宽的光带叠在深色外套上会变成一团粉雾（第一版的问题）
            // 颜色往白偏一点、不太浓：太红太实会像敌人的瞄准激光
            Color c = Color.Lerp(QualityVisual.Colors[q], Color.white, .32f); float y = Mathf.Lerp(-172, 20, Sm(ph)), k = (q >= 5 ? 1f : .8f) * tint.a;
            SheenStrips(bone, part, mirror, c, y, 9, .08f * k, true);
            SheenStrips(bone, part, mirror, c, y, 2f, .55f * k, true);
        }

        /// 绕着武器转的光点（军规起，光效「全」）：沿武器方向拉长的椭圆，转到武器后面时淡一点。
        private static void GearMotes(Matrix4x4 bone, Vector2 tipLocal, Color tint)
        {
            if (fxMode < 2 || qWeapon < 3) return;
            int n = Balance.Tier((Quality)qWeapon).particles; if (n <= 0) return;
            float len = tipLocal.magnitude; if (len < 1) return;
            Vector2 u = tipLocal / len, v = new Vector2(-u.y, u.x), mid = tipLocal * .5f;
            for (int i = 0; i < n; i++)
            {
                float ph = GearNow * 1.15f + i * 2 * Mathf.PI / n;
                Vector2 q = P(bone, mid + u * (Mathf.Cos(ph) * len * .46f) + v * (Mathf.Sin(ph) * 8.5f));
                float depth = .62f + .38f * Mathf.Sin(ph);
                Glow(q, 5.5f, QC(qWeapon, .5f * depth * tint.a));
                Dot(q, 1.25f, new Color(1, 1, 1, .92f * depth * tint.a));
            }
        }

        /// 扫光：把部件剪影切成横条，每条上下两边的透明度按「离光带中心多远」算，叠在部件上面（只亮在部件本身上）。
        /// world = true：光带按角色身上的高度（部件单位，脚底 0、头顶约 -150）；false：按部件图自己的上下。
        private static void SheenStrips(Matrix4x4 bone, Part part, bool mirror, Color color, float center, float halfWidth, float strength, bool world)
        {
            const int N = 64;   // 细光线要切得细，不然光线变成一格一格的三角形
            float px = mirror ? part.size.x - part.pivot.x : part.pivot.x, w = part.size.x, h = part.size.y;
            Matrix4x4 local = bone * T(-px, -part.pivot.y), M = screenM * local;
            float u0 = mirror ? 1 : 0, u1 = mirror ? 0 : 1;
            flat.mainTexture = part.tex; flat.SetPass(0);
            GL.Begin(GL.QUADS);
            float prevY = 0, prevA = Band(local, w, 0, center, halfWidth, world);
            for (int i = 1; i <= N; i++)
            {
                float y = h * i / N, a = Band(local, w, y, center, halfWidth, world);
                if (prevA > .01f || a > .01f)
                {
                    Color c0 = color, c1 = color; c0.a = prevA * strength; c1.a = a * strength;
                    GL.Color(c0); GL.TexCoord2(u0, 1 - prevY / h); GL.Vertex(M.MultiplyPoint3x4(new Vector3(0, prevY, 0)));
                    GL.Color(c0); GL.TexCoord2(u1, 1 - prevY / h); GL.Vertex(M.MultiplyPoint3x4(new Vector3(w, prevY, 0)));
                    GL.Color(c1); GL.TexCoord2(u1, 1 - y / h); GL.Vertex(M.MultiplyPoint3x4(new Vector3(w, y, 0)));
                    GL.Color(c1); GL.TexCoord2(u0, 1 - y / h); GL.Vertex(M.MultiplyPoint3x4(new Vector3(0, y, 0)));
                }
                prevY = y; prevA = a;
            }
            GL.End();
        }
        private static float Band(Matrix4x4 local, float w, float y, float center, float halfWidth, bool world)
        {
            float at = world ? local.MultiplyPoint3x4(new Vector3(w * .5f, y, 0)).y : y;
            float d = (at - center) / halfWidth;
            return Mathf.Exp(-d * d);
        }
    }
}
