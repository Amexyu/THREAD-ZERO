using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    // 素材包 ArtPack_v07 的整帧绘制：角色和敌人都是完整的一张图，不再拆肢体、不再旋转袖片。
    // 画布 512x512，脚底基准为左上原点像素 (256,470)。坐标使用 1600x900 逻辑画布，由 Ui 换算到屏幕。
    // 贴图缺失时返回 false，调用方退回旧的绘制，保证游戏仍能运行。
    public static class ActorView
    {
        public const string Version = "ArtPack_v08_equipment_pose_fix";
        private const float CanvasSize = 512, PivotX = 256, PivotY = 470;
        // ranged_fire 帧里枪口闪光的中心（画布像素，实测）。
        private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> cleanCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> equipmentHeadCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> weaponCache = new Dictionary<string, Texture2D>();
        private static readonly int[] gripX = { 214, 347, 347, 282, 185, 350 };
        private static readonly int[] gripY = { 343, 195, 195, 275, 120, 285 };
        private static readonly int[] weaponSize = { 220, 310, 310, 390, 380, 450 };
        private static readonly int[] weaponAngle = { 88, 0, 0, -15, -35, 15 };
        private static readonly string[] enemyIds = { "patrol", "salvager", "jammer", "patrol_commander", "furnace_foreman", "blackout_broadcaster" };

        private static Texture2D Load(string name)
        {
            Texture2D texture;
            if (!cache.TryGetValue(name, out texture))
            {
                texture = Resources.Load<Texture2D>("Art/V07/" + name);
                cache[name] = texture;
                if (texture == null) Debug.LogWarning("THREAD ZERO: 缺少贴图 Resources/Art/V07/" + name + "，已退回旧绘制");
            }
            return texture;
        }
        private static bool Draw(Texture2D texture, Rect uv, float feetX, float feetY, float canvas, Color tint)
        {
            if (texture == null) return false;
            float scale = canvas / CanvasSize;
            Color old = GUI.color; GUI.color = tint;
            GUI.DrawTextureWithTexCoords(Ui.R(feetX - PivotX * scale, feetY - PivotY * scale, canvas, canvas), texture, uv, true);
            GUI.color = old;
            return true;
        }

        // look：外套外观 0-4（对应 c0-c4）。frame：0 待机 1 举枪 2 开火 3 近战准备 4 举刃 5 突刺。
        public static bool Player(float feetX, float feetY, float canvas, int look, int frame, Color tint, string headId = null, string weaponId = null)
        {
            look = Mathf.Clamp(look, 0, 4); frame = Mathf.Clamp(frame, 0, 5);
            Rect uv = new Rect((frame % 3) / 3f, 1 - (frame / 3 + 1) / 2f, 1 / 3f, .5f);
            bool replacementWeapon = frame <= 2
                ? weaponId == "w0" || weaponId == "w2" || weaponId == "w3"
                : weaponId == "w1" || weaponId == "w4";
            Texture2D body = LoadFaceAtlas(look, replacementWeapon);
            if (body == null) body = Load("Atlases/Player_c" + look);
            if (!Draw(body, uv, feetX, feetY, canvas, tint)) return false;
            DrawHeadEquipment(uv, feetX, feetY, canvas, tint, headId);
            if (replacementWeapon)
            {
                DrawReplacementWeapon(feetX, feetY, canvas, frame, tint, weaponId);
                if (frame == 2) DrawMuzzleFlash(feetX, feetY, canvas, weaponId);
            }
            return true;
        }
        private static void DrawHeadEquipment(Rect uv, float feetX, float feetY, float canvas, Color tint, string headId)
        {
            if (string.IsNullOrEmpty(headId) || headId == "_default") return;
            Texture2D head;
            if (!equipmentHeadCache.TryGetValue(headId, out head))
            {
                head = Resources.Load<Texture2D>("Art/V08/HeadEquipment/" + headId);
                equipmentHeadCache[headId] = head;
            }
            Draw(head, uv, feetX, feetY, canvas, tint);
        }
        private static Texture2D LoadFaceAtlas(int look, bool weaponRemoved)
        {
            string key = (weaponRemoved ? "AtlasesFaceWeaponClean/Player_c" : "AtlasesFace/Player_c") + look;
            Texture2D texture;
            if (!cleanCache.TryGetValue(key, out texture))
            {
                texture = Resources.Load<Texture2D>("Art/V08/" + key);
                cleanCache[key] = texture;
            }
            return texture;
        }
        private static void DrawReplacementWeapon(float feetX, float feetY, float canvas, int frame, Color tint, string weaponId)
        {
            Texture2D weapon;
            if (!weaponCache.TryGetValue(weaponId, out weapon))
            {
                weapon = Resources.Load<Texture2D>("Art/V08/Weapon/" + weaponId);
                weaponCache[weaponId] = weapon;
            }
            if (weapon == null) return;
            float scale = canvas / CanvasSize;
            float x = feetX + (gripX[frame] - PivotX) * scale;
            float y = feetY + (gripY[frame] - PivotY) * scale;
            float variantScale = weaponId == "w2" ? .90f : weaponId == "w3" ? .96f : weaponId == "w4" ? .94f : 1f;
            float size = weaponSize[frame] * variantScale * scale;
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color; GUI.color = tint;
            GUIUtility.RotateAroundPivot(weaponAngle[frame], Ui.P(x, y));
            GUI.DrawTexture(Ui.R(x - size / 2, y - size / 2, size, size), weapon, ScaleMode.StretchToFill, true);
            GUI.matrix = oldMatrix; GUI.color = oldColor;
        }
        public static void Muzzle(string weaponId, out float x, out float y)
        {
            float variantScale = weaponId == "w2" ? .90f : weaponId == "w3" ? .96f : 1f;
            float size = weaponSize[2] * variantScale;
            int tipX = weaponId == "w2" ? 446 : weaponId == "w3" ? 424 : 416;
            x = gripX[2] + (tipX - 256) * size / 512f;
            y = gripY[2] + (215 - 256) * size / 512f;
        }
        private static void DrawMuzzleFlash(float feetX, float feetY, float canvas, string weaponId)
        {
            float mx, my;
            Muzzle(weaponId, out mx, out my);
            float scale = canvas / CanvasSize;
            mx = feetX + (mx - PivotX) * scale;
            my = feetY + (my - PivotY) * scale;
            Ui.Stroke(mx, my, mx + 18 * scale, my, 3 * scale, Ui.Orange);
            Ui.Stroke(mx + 3 * scale, my, mx + 11 * scale, my - 8 * scale, 2 * scale, Ui.Orange);
            Ui.Stroke(mx + 3 * scale, my, mx + 11 * scale, my + 8 * scale, 2 * scale, Ui.Orange);
        }
        public static string EnemyId(int region, bool boss) { return enemyIds[Mathf.Clamp(region, 0, 2) + (boss ? 3 : 0)]; }
        public static bool Enemy(float feetX, float feetY, float canvas, int region, bool boss, Color tint)
        { return Draw(Load("Enemies/" + EnemyId(region, boss)), new Rect(0, 0, 1, 1), feetX, feetY, canvas, tint); }

        public static int IdleFrame(bool melee) { return melee ? 3 : 0; }
        // progress 为 DollAnimation 的攻击进度（0 表示没有在攻击）。分段与伤害结算时机对齐：
        // 远程在约 0.29 处开火，近战在约 0.41 处突刺命中。
        public static int CombatFrame(float progress, bool melee)
        {
            if (progress <= 0) return IdleFrame(melee);
            if (melee) return progress < .16f ? 3 : progress < .41f ? 4 : progress < .80f ? 5 : 3;
            return progress < .286f ? 1 : progress < .43f ? 2 : progress < .80f ? 1 : 0;
        }
    }
}
