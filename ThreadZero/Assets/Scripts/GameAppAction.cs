using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    public sealed partial class GameApp
    {
        private const float StageX = 128, StageY = 104, StageW = 1440, StageH = 624, GroundY = 572;
        // v0.8：镜头拉远。角色与敌人缩小，舞台里能看到更长的一段路。
        private const float HeroRestX = 428, HeroCanvas = 260, EnemyX = 1060, EnemyCanvas = 220, EnemyEliteCanvas = 260, EnemyBossCanvas = 300, PackSpacing = 136;

        private static float Wrap(float value, float period) { value %= period; return value < 0 ? value + period : value; }
        private static float Noise(int n) { n = (n << 13) ^ n; return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f; }

        private void ActionPage()
        {
            Stats stats = model.Total();
            ActionHeader();
            int region = model.state.region, district = Catalog.District(region);
            bool deep = region >= Catalog.Districts, moving = travelLeft > 0;
            bool boss = combat.Boss, melee = model.IsMelee, down = combat.recovery > 0;
            // 行进途中战斗逻辑暂停，上一场战斗残留的闪光不参与显示。
            float hit = moving ? 0 : combat.hitFlash, dash = moving ? 0 : combat.dashFlash;

            Ui.Fill(StageX, StageY, StageW, StageH, deep ? Ui.Hex(0xE9EAE5) : Ui.Stage);
            Ui.BeginClip(StageX, StageY, StageW, StageH, 0);
            Backdrop(region, district, deep);

            float enemyCanvas = boss ? EnemyBossCanvas : combat.Elite ? EnemyEliteCanvas : EnemyCanvas;
            float enemyTop = GroundY - enemyCanvas * .82f, enemyMid = GroundY - enemyCanvas * .42f;
            // 敌人固定在世界里：行进时它随着地面从右侧进入画面。
            float enemyX = EnemyX + (moving ? travelFrom + travelDistance - worldX : 0);

            if (wreckShown)
            {
                float wreckX = EnemyX - (worldX - wreckWorldX);
                if (wreckX < StageX - 360) wreckShown = false; else DrawWreck(wreckX);
            }

            float lunge = dash > 0 ? 90 * Mathf.Sin(dash / .4f * Mathf.PI) : 0;
            DollPose pose = animationState.Pose(down);
            bool daggerSample = DaggerActorView.CanDraw(model);
            float approach = !moving && melee && pose.attackProgress > 0 ? Mathf.Sin(pose.attackProgress * Mathf.PI) * (EnemyX - enemyCanvas * .30f - HeroRestX - 125) : 0;
            if(daggerSample)
            {
                float age=combat.DaggerAge, contact=combat.DaggerCombo?.23f:DaggerSkill.BasicHit;
                float recover=combat.DaggerCombo?.56f:.18f, duration=combat.DaggerCombo?DaggerSkill.Duration:DaggerSkill.BasicDuration;
                float reach=age<contact?Mathf.SmoothStep(0,1,age/contact):age<recover?1:Mathf.SmoothStep(1,0,(age-recover)/(duration-recover));
                approach=!moving && !down && combat.DaggerActive?reach*(EnemyX-enemyCanvas*.30f-HeroRestX-HeroCanvas*.42f):0;
            }
            float stride = moving ? Mathf.Abs(Mathf.Sin(worldX / 24f)) * 3 : 0;
            float heroFeetX = HeroRestX + lunge + approach, heroFeetY = GroundY + 2 - stride;
            Ui.Fill(heroFeetX - 56, GroundY - 3, 112, 5, Ui.Alpha(Ui.Ink, .16f));
            if (moving)
                for (int k = 0; k < 3; k++)
                {
                    float phase = Wrap(worldX / 70f + k * .33f, 1), size = 3 + 7 * (1 - phase);
                    Ui.Fill(heroFeetX - 34 - phase * 46, GroundY - 4 - phase * 12 - size, size, size, Ui.Alpha(Ui.Grey, .55f * (1 - phase)));
                }
            if (dash > 0) Ui.Stroke(HeroRestX - 150, GroundY - 44, heroFeetX - 36, GroundY - 44, 4, Ui.Orange);
            Color heroTint = down ? new Color(.6f, .65f, .68f) : pose.damageTint > 0 ? new Color(1, 1 - pose.damageTint, 1 - pose.damageTint) : Color.white;
            int heroFrame = down || moving ? ActorView.IdleFrame(melee) : ActorView.CombatFrame(pose.attackProgress, melee);
            float shake = down || moving || pose.attackProgress > 0 ? 0 : pose.rootX;
            string headId = model.Equipped(Slot.Head) == null && model.state.appearance[(int)Slot.Head] == null
                ? "_default" : model.Appearance(Slot.Head).id;
            // Hold F9 on the action page to inspect the walk rig without waiting for travel.
            bool walkReview = Input.GetKey(KeyCode.F9);
            int rigCoat = walkReview ? 0 : model.Appearance(Slot.Coat).style;
            string rigHead = walkReview ? "h0" : headId;
            string rigWeapon = walkReview ? "w0" : model.Appearance(Slot.Weapon).id;
            string rigBack = walkReview ? "b0" : model.Appearance(Slot.Back).id;
            int daggerFrame=!moving && !down && combat.DaggerActive?DaggerSkill.Frame(combat.DaggerAge,combat.DaggerCombo):0;
            float hop=daggerSample && moving?Mathf.Abs(Mathf.Sin(worldX/24f))*6:0;
            bool newDagger = DaggerActorView.Draw(model,heroFeetX+shake,heroFeetY-hop,HeroCanvas,daggerFrame,heroTint);
            bool skeletal = !newDagger && SkeletalActorView.Draw(heroFeetX + shake, heroFeetY, HeroCanvas,
                rigCoat, rigHead, rigWeapon, rigBack,
                heroFrame, moving || walkReview, walkReview ? clock * 6f : worldX / 24f,
                pose.attackProgress, heroTint);
            bool wholeFrame = newDagger || skeletal || ActorView.Player(heroFeetX + shake, heroFeetY, HeroCanvas,
                model.Appearance(Slot.Coat).style, heroFrame, heroTint, headId, model.Appearance(Slot.Weapon).id);
            const float oldW = 300, oldH = 240;
            if (!wholeFrame) CharacterView.Draw(model, Ui.R(heroFeetX - oldW / 2, heroFeetY - oldH + 6, oldW, oldH), clock, Mathf.Abs(pose.weaponAngle) > .1f, moving ? 0 : combat.hurtFlash, down, dash, pose);

            // 敌人：信号禁区的两种是悬浮单位，会上下浮动。深层敌人带冷色调。
            // 怪群：最前面一只在 enemyX，同伴依次排在它身后。前面的倒下后，后面的整队向前顶一格。
            float packSlide = moving ? 0 : PackSpacing * (1 - Mathf.Clamp01((clock - packShiftAt) / .28f));
            enemyX += packSlide;
            for (int i = combat.reserve.Count - 1; i >= 0; i--)
            {
                float memberX = enemyX + (i + 1) * PackSpacing, memberFeetY = GroundY + 2 + (district == 2 ? Mathf.Sin(clock * 2 + i + 1) * 5 - 8 : 0);
                if (memberX > StageX + StageW + enemyCanvas) continue;
                Ui.Fill(memberX - enemyCanvas * .26f, GroundY - 3, enemyCanvas * .52f, 5, Ui.Alpha(Ui.Ink, .16f));
                if (!ActorView.Enemy(memberX, memberFeetY, enemyCanvas, district, false, deep ? new Color(.84f, .88f, 1) : Color.white))
                    DrawEnemy(memberX, GroundY - 138, 110, 88, false, 0, false);
                Ui.Fill(memberX - 32, enemyTop - 16, 64, 5, Ui.Steel);
                Ui.Fill(memberX - 32, enemyTop - 16, 64 * Mathf.Clamp01(combat.reserve[i] / Mathf.Max(1, combat.reserveMax)), 5, Ui.Orange);
            }
            if (enemyX < StageX + StageW + enemyCanvas)
            {
                bool floating = district == 2;
                float enemyFeetY = GroundY + 2 + (floating ? Mathf.Sin(clock * 2) * 5 - 8 : 0), recoil = hit > 0 ? hit / .3f * 6 : 0;
                Color enemyTint = hit > 0 ? new Color(1, .74f, .68f) : deep ? new Color(.84f, .88f, 1) : Color.white;
                Ui.Fill(enemyX - enemyCanvas * .26f, GroundY - 3, enemyCanvas * .52f, 5, Ui.Alpha(Ui.Ink, .16f));
                if (ActorView.Enemy(enemyX + recoil, enemyFeetY, enemyCanvas, district, boss, enemyTint))
                { if (!moving && combat.markTime > 0) MarkFrame(enemyX - enemyCanvas * .42f, enemyTop - 8, enemyX + enemyCanvas * .42f, GroundY + 14); }
                else
                {
                    float boxW = boss ? 150 : 110, boxH = boss ? 120 : 88;
                    DrawEnemy(enemyX, GroundY - 50 - boxH + Mathf.Sin(clock * 2) * 4, boxW, boxH, boss, hit, !moving && combat.markTime > 0);
                }
            }
            if (!moving && pose.projectile > 0)
            {
                float scale = HeroCanvas / 512;
                float weaponMuzzleX, weaponMuzzleY;
                ActorView.Muzzle(model.Appearance(Slot.Weapon).id, out weaponMuzzleX, out weaponMuzzleY);
                Vector2 start = new Vector2(heroFeetX + (weaponMuzzleX - 256) * scale, heroFeetY + (weaponMuzzleY - 470) * scale);
                if (!wholeFrame)
                {
                    float mx, my; PuppetGeometry.Muzzle(pose, model.Appearance(Slot.Weapon).style, out mx, out my);
                    float old = Mathf.Min(oldW / 330, oldH / 320);
                    start = new Vector2(heroFeetX - 128 * old + mx * old, heroFeetY + 6 - 320 * old + my * old);
                }
                Vector2 end = new Vector2(EnemyX - enemyCanvas * .2f, enemyMid), point = Vector2.Lerp(start, end, pose.projectile), direction = (end - start).normalized;
                Ui.Stroke(point.x, point.y, point.x + direction.x * 30, point.y + direction.y * 30, 4, Ui.Orange);
            }
            ModuleEffects(heroFeetX, enemyMid, moving);
            DropEffect(enemyCanvas);
            DrawFloaters(heroFeetX, EnemyX - enemyCanvas * .36f - 120, enemyMid - 50);
            Ui.EndClip();

            // 敌我铭牌。
            float plateH = 120;
            Ui.Fill(StageX + 24, StageY + 24, 400, plateH, Ui.Ink);
            Ui.Text(StageX + 42, StageY + 34, 200, 28, "回收员", 18, Ui.Panel, TextAnchor.MiddleLeft, true);
            Numbers(StageX + 42, StageY + 30, 364, combat.health, stats.health, true);
            Bar(StageX + 42, StageY + 72, 364, healthShown, Ui.Panel, false);
            ModuleChips(StageX + 42, StageY + 98);

            Color enemyColor = boss ? QualityVisual.Colors[4] : Ui.Orange;
            Ui.Fill(StageX + StageW - 424, StageY + 24, 400, 84, Ui.Ink);
            Ui.Text(StageX + StageW - 406, StageY + 34, 364, 28, combat.EnemyName + (combat.Alive > 1 ? "  ×" + combat.Alive : ""), 18, boss ? enemyColor : Ui.Panel, TextAnchor.MiddleRight, true);
            Numbers(StageX + StageW - 406, StageY + 30, 364, Mathf.Max(0, combat.enemyHealth), combat.enemyMaxHealth, false);
            Bar(StageX + StageW - 406, StageY + 72, 364, enemyShown, enemyColor, true);

            bool transit = moving && travelKind >= 2;
            if (down || paused || transit)
            {
                string line = down ? "维修中 · " + combat.recovery.ToString("F1") + " 秒" : paused ? "已暂停"
                    : travelKind == 3 ? "前往 0" + (region + 1) + " · " + Catalog.RegionNames[region] : "首领已回收";
                string note = down ? "装备与收藏不会丢失，修好后从第 1 波重试" : paused ? "按空格或右下角按钮继续"
                    : travelKind == 3 ? "转移中，抵达后自动开始回收" : "返回街区入口继续循环，可在左上角切换街区";
                Ui.Fill(StageX + StageW / 2 - 210, StageY + 170, 420, 76, Ui.Ink); Ui.Fill(StageX + StageW / 2 - 210, StageY + 170, 8, 76, Ui.Orange);
                Ui.Text(StageX + StageW / 2 - 180, StageY + 178, 380, 36, line, 22, Ui.Panel, TextAnchor.MiddleLeft, true);
                Ui.Text(StageX + StageW / 2 - 180, StageY + 212, 380, 24, note, 13, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            }
            Ui.Frame(StageX, StageY, StageW, StageH, 2, Ui.Ink);
            ActionFooter();
        }

        // 程序画的行进背景：远景楼块（慢）、中景立柱与街区特征（中）、地面标线（快），随 worldX 滚动。
        // 正式背景图到位后替换这里，滚动系数保持不变。
        private void Backdrop(int region, int district, bool deep)
        {
            float far = worldX * .25f, mid = worldX * .6f;
            int firstBlock = Mathf.FloorToInt(far / 190);
            for (int i = 0; i < 10; i++)
            {
                float height = 70 + Noise(firstBlock + i + district * 101) * 170;
                Ui.Fill(StageX + i * 190 - Wrap(far, 190), GroundY - height, 124, height, Ui.Hex(deep ? 0xDFE1DB : 0xE9EAE5));
            }
            for (int k = 0; k < 2; k++)
                Ui.Text(StageX + 560 + k * 1700 - Wrap(far, 1700), StageY + 6, 700, 400, "0" + (region + 1), 300, Ui.Ghost, TextAnchor.UpperLeft, true, true);
            Ui.Fill(StageX, StageY + 150, StageW, 1, Ui.Hair);

            int firstPillar = Mathf.FloorToInt(mid / 520);
            for (int j = 0; j < 4; j++)
            {
                float x = StageX + j * 520 - Wrap(mid, 520); int id = firstPillar + j;
                Ui.Fill(x, StageY, 26, 468, Ui.Hex(0xDDDFD9)); Ui.Fill(x, StageY + 118, 26, 8, Ui.Ink);
                Ui.Text(x + 36, StageY + 158, 80, 18, "S-" + (Mathf.Abs(id) % 90 + 10), 12, Ui.Mute, TextAnchor.MiddleLeft, true, true);
                if (district == 1) { Ui.Fill(x + 250, StageY, 6, 96, Ui.Rule); }
                if (district == 2)
                {
                    Ui.Fill(x + 250, StageY + 60, 6, 408, Ui.Rule); Ui.Fill(x + 236, StageY + 96, 34, 4, Ui.Rule);
                    if (Mathf.Sin(clock * 3 + id) > 0) Ui.Fill(x + 247, StageY + 46, 12, 12, Ui.Orange);
                }
            }
            if (district == 0)
                for (int i = 0; i < 16; i++)
                {
                    float x = StageX + Wrap(i * 103.7f - worldX * .8f, StageW), y = StageY + Wrap(i * 71.3f + clock * 520, 460);
                    Ui.Stroke(x, y, x - 8, y + 26, 2, Ui.Alpha(Ui.Mute, .22f));
                }
            if (district == 1)
            {
                Ui.Fill(StageX, StageY + 96, StageW, 14, Ui.Hex(0xD9DBD5));
                Ui.Fill(StageX, GroundY - 40, StageW, 4, Ui.Rule);
                for (int i = 0; i < 12; i++) Ui.Fill(StageX + i * 130 - Wrap(worldX, 130), GroundY - 40, 4, 40, Ui.Rule);
            }

            Ui.Fill(StageX, GroundY + 4, StageW, StageH - 472, Ui.Floor);
            Ui.Fill(StageX, GroundY, StageW, 4, Ui.Ink);
            Ui.Hazard(StageX, GroundY + 4, StageW, 14, worldX);
            for (int i = 0; i < 10; i++)
            {
                Ui.Fill(StageX + i * 160 - Wrap(worldX, 160), GroundY + 46, 64, 4, Ui.Rule);
                Ui.Fill(StageX + 80 + i * 160 - Wrap(worldX * 1.15f, 160), GroundY + 96, 64, 4, Ui.Rule);
            }
            if (deep) Ui.Hazard(StageX, StageY, StageW, 8, mid);
        }

        // 被击败的敌人留在原地，随着角色前进被甩到画面左侧。
        private void DrawWreck(float x)
        {
            float canvas = wreckBoss ? EnemyBossCanvas : EnemyCanvas;
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(-9, Ui.P(x, GroundY));
            bool drawn = ActorView.Enemy(x, GroundY + 8, canvas, wreckDistrict, wreckBoss, new Color(.42f, .44f, .47f, .75f));
            GUI.matrix = old;
            if (!drawn) Ui.Fill(x - 55, GroundY - 34, 110, 34, Ui.Grey);
        }

        private void ActionHeader()
        {
            string region = Catalog.RegionNames[model.state.region];
            float nameWidth = Ui.Width(region, 30, true);
            bool hover = Ui.Hover(128, 16, 150 + nameWidth, 76);
            Ui.Text(128, 12, 84, 80, "0" + (model.state.region + 1), 60, Ui.Ink, TextAnchor.MiddleLeft, true, true);
            Ui.Text(214, 22, 240, 18, "SECTOR / 街区", 13, Ui.Mute, TextAnchor.MiddleLeft, true, true);
            Ui.Text(214, 42, nameWidth + 10, 40, region, 30, Ui.Ink, TextAnchor.MiddleLeft, true);
            float boxX = 214 + nameWidth + 16;
            Ui.Fill(boxX, 44, 36, 36, hover ? Ui.Ink : Ui.Ground); Ui.Frame(boxX, 44, 36, 36, 2, Ui.Ink);
            Ui.IconChevron(boxX + 18, 62, hover ? Ui.Panel : Ui.Ink);
            if (Ui.Click(128, 16, 150 + nameWidth, 76)) navigation.Open(GameModal.Map);

            float x = 640, middle = 54; float[] centers = new float[6];
            Ui.Text(x - 66, middle - 12, 56, 24, "ROUTE", 14, Ui.Mute, TextAnchor.MiddleRight, true, true);
            for (int wave = 1; wave <= 4; wave++)
            {
                bool done = wave < combat.wave, now = wave == combat.wave;
                float size = now ? 52 : 40;
                bool digit = wave != Balance.EliteWave; string label = digit ? wave.ToString() : "精";
                if (done) { Ui.Fill(x, middle - 20, 40, 40, Ui.Ink); Ui.IconCheck(x + 20, middle, Ui.Panel); }
                else if (now) { Ui.Fill(x, middle - 26, 52, 52, Ui.Orange); Ui.Text(x, middle - 26, 52, 52, label, digit ? 30 : 24, Ui.Ink, TextAnchor.MiddleCenter, true, digit); }
                else { Ui.Frame(x, middle - 20, 40, 40, 2, Ui.Grey); Ui.Text(x, middle - 20, 40, 40, label, digit ? 20 : 17, Ui.Mute, TextAnchor.MiddleCenter, true, digit); }
                centers[wave] = x + size / 2; x += size;
                Ui.Fill(x, middle - 2, 44, 4, done ? Ui.Ink : Ui.Rule); x += 44;
            }
            if (combat.Boss) { Ui.Fill(x, middle - 26, 100, 52, Ui.Orange); Ui.Text(x, middle - 26, 100, 52, "BOSS", 26, Ui.Ink, TextAnchor.MiddleCenter, true, true); }
            else { Ui.Frame(x, middle - 20, 84, 40, 2, Ui.Ink); Ui.Text(x, middle - 20, 84, 40, "BOSS", 19, Ui.Ink, TextAnchor.MiddleCenter, true, true); }

            centers[5] = x + (combat.Boss ? 50 : 42);
            // 路线上的位置标记：赶往下一波时在两个节点之间滑动。
            int at = Mathf.Clamp(combat.wave, 1, 5); float marker = centers[at];
            if (travelLeft > 0 && travelKind == 1 && at > 1) marker = Mathf.Lerp(centers[at - 1], centers[at], TravelProgress);
            Ui.Fill(marker - 6, middle + 32, 12, 4, Ui.Ink); Ui.Fill(marker - 2, middle + 28, 4, 4, Ui.Ink);

            if (Ui.Button(1196, 30, 176, 48, autoAdvance ? "自动推进 · 开" : "自动推进 · 关", autoAdvance ? 2 : 0, true, 15)) autoAdvance = !autoAdvance;
            ScrapChip(1568);
        }

        private void ScrapChip(float right)
        {
            string amount = model.state.scrap.ToString();
            float width = Ui.Width(amount, 26, true, true) + 84;
            Ui.Fill(right - width, 30, width, 48, Ui.Panel); Ui.Frame(right - width, 30, width, 48, 1, Ui.Rule);
            Ui.Text(right - width + 16, 30, 50, 48, "零件", 14, Ui.Mute, TextAnchor.MiddleLeft);
            Ui.Text(right - width, 30, width - 16, 48, amount, 26, Ui.Ink, TextAnchor.MiddleRight, true, true);
        }

        private void Numbers(float x, float y, float width, float now, float max, bool alignRight)
        {
            string a = now.ToString("F0"), b = " / " + max.ToString("F0");
            float wa = Ui.Width(a, 32, true, true), wb = Ui.Width(b, 18, false, true);
            float left = alignRight ? x + width - wa - wb : x;
            Ui.Text(left, y, wa + 4, 36, a, 32, Ui.Panel, TextAnchor.LowerLeft, true, true);
            Ui.Text(left + wa, y, wb + 4, 33, b, 18, Ui.MuteOnInk, TextAnchor.LowerLeft, false, true);
        }

        private void Bar(float x, float y, float width, float value, Color color, bool fromRight)
        {
            Ui.Fill(x, y, width, 14, Ui.Steel);
            float filled = width * Mathf.Clamp01(value);
            Ui.Fill(fromRight ? x + width - filled : x, y, filled, 14, color);
            for (float tick = 34; tick < width - 4; tick += 36) Ui.Fill(x + tick, y, 2, 14, Ui.Ink);
        }

        private void ModuleChips(float x, float y)
        {
            var names = new List<string>(); var live = new List<bool>();
            if (model.Has(Effect.Drone)) { names.Add("无人机"); live.Add(combat.droneFlash > 0); }
            if (model.Has(Effect.Mark)) { names.Add("标记"); live.Add(combat.markTime > 0); }
            if (model.Has(Effect.Arc)) { names.Add("电弧"); live.Add(combat.arcFlash > 0); }
            if (model.Has(Effect.Thruster)) { names.Add("推进"); live.Add(combat.dashFlash > 0); }
            if (model.Has(Effect.Guard)) { names.Add("护盾"); live.Add(combat.guardFlash > 0 || combat.guardCharge > 0); }
            if (model.Has(Effect.Leech)) { names.Add("汲取"); live.Add(false); }
            if (model.Has(Effect.Execute)) { names.Add("处决"); live.Add(combat.enemyHealth < combat.enemyMaxHealth * Balance.ExecuteThreshold); }
            if (names.Count == 0) { names.Add("常规攻击"); live.Add(false); }
            for (int i = 0; i < names.Count; i++)
            {
                float width = Ui.Width(names[i], 13, true) + 20;
                if (live[i]) Ui.Fill(x, y, width, 26, Ui.Orange); else Ui.Frame(x, y, width, 26, 1, Ui.Hex(0x6B7175));
                Ui.Text(x, y, width, 26, names[i], 13, live[i] ? Ui.Ink : Ui.Panel, TextAnchor.MiddleCenter, true);
                x += width + 8;
            }
        }

        // 旧版程序化机器人，仅在缺少敌人贴图时使用。
        private void DrawEnemy(float x, float top, float width, float height, bool boss, float hit, bool marked)
        {
            Color accent = boss ? QualityVisual.Colors[4] : Ui.Orange;
            Color shell = hit > 0 ? Ui.Hex(0x6B7175) : Ui.Ink;
            float bottom = top + height;
            Ui.Fill(x - 60, GroundY - 3, 120, 5, Ui.Alpha(Ui.Ink, .16f));
            Ui.Stroke(x - 30, bottom - 4, x - 58, GroundY - 4, 14, Ui.Ink); Ui.Stroke(x + 30, bottom - 4, x + 58, GroundY - 4, 14, Ui.Ink);
            Ui.Fill(x - 82, GroundY - 10, 44, 10, Ui.Ink); Ui.Fill(x + 38, GroundY - 10, 44, 10, Ui.Ink);
            Ui.Fill(x + width / 2 - 30, top - 30, 4, 30, Ui.Ink); Ui.Fill(x + width / 2 - 33, top - 38, 10, 10, accent);
            Ui.Fill(x - width / 2, top, width, height, shell);
            Ui.Fill(x - width / 2 + 8, top + 8, width - 16, height - 16, hit > 0 ? Ui.Grey : Ui.Tile2);
            Ui.Fill(x - width / 2 + 20, top + 26, width - 40, 18, accent);
            Ui.Fill(x - width / 2 + 20, top + 26, (width - 40) * .28f, 18, Ui.Ink);
            Ui.Disk(x, bottom - 40, boss ? 26 : 20, Ui.Ink); Ui.Disk(x, bottom - 40, boss ? 12 : 9, accent);
            if (boss) { Ui.Fill(x - width / 2 - 16, top + 30, 16, 60, Ui.Ink); Ui.Fill(x + width / 2, top + 30, 16, 60, Ui.Ink); }
            if (marked) MarkFrame(x - width / 2 - 22, top - 22, x + width / 2 + 22, bottom + 22);
        }

        private void MarkFrame(float l, float t, float r, float b)
        {
            Ui.Fill(l, t, 26, 4, Ui.Orange); Ui.Fill(l, t, 4, 26, Ui.Orange); Ui.Fill(r - 26, t, 26, 4, Ui.Orange); Ui.Fill(r - 4, t, 4, 26, Ui.Orange);
            Ui.Fill(l, b - 4, 26, 4, Ui.Orange); Ui.Fill(l, b - 26, 4, 26, Ui.Orange); Ui.Fill(r - 26, b - 4, 26, 4, Ui.Orange); Ui.Fill(r - 4, b - 26, 4, 26, Ui.Orange);
        }

        private void ModuleEffects(float feetX, float enemyMid, bool moving)
        {
            if (model.Has(Effect.Drone))
            {
                float dx = feetX + 96, dy = GroundY - 262 + Mathf.Sin(clock * 3) * 5;
                if (!PaperDollView.DrawDrone(Ui.R(dx - 38, dy - 15, 76, 30))) { Ui.Fill(dx - 18, dy - 7, 36, 14, Ui.Ink); Ui.Disk(dx, dy, 4, Ui.Orange); }
                if (!moving && combat.droneFlash > 0) Ui.Stroke(dx + 24, dy + 5, EnemyX, enemyMid, 3, Ui.Orange);
            }
            if (moving) return;
            if (combat.arcFlash > 0)
            {
                Color arc = QualityVisual.Colors[3]; float x0 = feetX + 84, span = EnemyX - x0;
                float[] offsets = { 0, -26, 18, -20, 0 };
                for (int i = 1; i < 5; i++)
                    Ui.Stroke(x0 + span * (i - 1) / 4, enemyMid + offsets[i - 1], x0 + span * i / 4, enemyMid + offsets[i], 4, arc);
            }
            if (combat.guardFlash > 0)
            {
                float gx = feetX + 92;
                Ui.Stroke(gx, GroundY - 150, gx + 20, GroundY - 95, 4, Ui.Ink); Ui.Stroke(gx + 20, GroundY - 95, gx, GroundY - 40, 4, Ui.Ink);
            }
        }

        private void DropEffect(float enemyCanvas)
        {
            if (lastDrop == null || clock >= dropEffectUntil) return;
            EquipmentBase b = Catalog.Find(lastDrop.baseId); if (b == null) return;
            float rise = Mathf.Clamp01((clock - dropEffectFrom) / .22f) * 34, x = EnemyX + enemyCanvas * .42f + 24 - (worldX - dropWorldX), y = GroundY - 64 - rise;
            if (model.state.glowMode > 0 && (int)lastDrop.quality >= 2)
                QualityVisual.Halo(Ui.P(x + 26, y + 28), 56 * Ui.K, QualityVisual.Colors[(int)lastDrop.quality], model.state.glowMode == 1 ? .25f : .5f);
            Ui.ItemTile(x, y, 52, 56, b, lastDrop.quality, true, false, 4, false);
        }

        private void DrawFloaters(float feetX, float enemySideX, float enemySideY)
        {
            for (int i = 0; i < floaters.Count; i++)
            {
                Floater f = floaters[i]; float age = (clock - f.born) / .9f, rise = 54 * age, alpha = age < .7f ? 1 : Mathf.Clamp01(1 - (age - .7f) / .3f);
                if (f.onEnemy)
                {
                    float size = f.critical ? 54 : 40, x = enemySideX + f.drift, y = enemySideY - rise;
                    string value = f.value.ToString("F0");
                    if (f.critical) { Ui.Fill(x - 56, y + 24, 48, 24, Ui.Alpha(Ui.Orange, alpha)); Ui.Text(x - 56, y + 24, 48, 24, "暴击", 14, Ui.Alpha(Ui.Ink, alpha), TextAnchor.MiddleCenter, true); }
                    Ui.Text(x, y, 220, 72, value, size, Ui.Alpha(Ui.Ink, alpha), TextAnchor.MiddleLeft, true, true);
                }
                else Ui.Text(feetX + 56 + f.drift, GroundY - 250 - rise, 160, 44, "-" + f.value.ToString("F0"), 32, Ui.Alpha(Ui.OrangeInk, alpha), TextAnchor.MiddleLeft, true, true);
            }
        }

        private void ActionFooter()
        {
            const float y = 744, h = 132;
            // 当前配装
            bool over = Ui.Hover(128, y, 404, h);
            Ui.Fill(128, y, 404, h, Ui.Panel); Ui.Frame(128, y, 404, h, over ? 2 : 1, over ? Ui.Ink : Ui.Rule);
            Ui.Text(144, y + 12, 120, 22, "当前配装", 15, Ui.Ink, TextAnchor.MiddleLeft, true);
            Ui.Text(250, y + 12, 266, 22, model.LoadoutSummary, 13, Ui.Mute, TextAnchor.MiddleRight);
            for (int i = 0; i < 4; i++)
            {
                Item item = model.Equipped((Slot)i); float x = 144 + i * 82;
                if (item != null) Ui.ItemTile(x, y + 46, 72, 72, Catalog.Find(item.baseId), item.quality, true, false, 5, false);
                else { Ui.Fill(x, y + 46, 72, 72, Ui.Rule); Ui.Text(x, y + 46, 72, 72, Catalog.SlotNames[i], 13, Ui.Mute, TextAnchor.MiddleCenter); }
            }
            if (Ui.Click(128, y, 404, h)) Go(GameScreen.Loadout);

            // 最近回收
            Ui.Fill(548, y, 400, h, Ui.Panel); Ui.Frame(548, y, 400, h, 1, Ui.Rule);
            Ui.Text(564, y + 12, 120, 22, "最近回收", 15, Ui.Ink, TextAnchor.MiddleLeft, true);
            if (unseen.Count > 0) Ui.Text(690, y + 12, 242, 22, unseen.Count + " 件未查看", 13, Ui.Mute, TextAnchor.MiddleRight);
            else if (autoSalvagedCount > 0) Ui.Text(690, y + 12, 242, 22, "已自动分解 " + autoSalvagedCount + " 件 · +" + autoSalvagedScrap, 13, Ui.Mute, TextAnchor.MiddleRight);
            int shown = 0;
            for (int i = 0; i < recentDrops.Count && shown < 5; i++)
            {
                Item item = FindItem(recentDrops[i]); if (item == null) continue;
                float x = 564 + shown * 74; bool hot = Ui.Hover(x, y + 46, 62, 72);
                Ui.ItemTile(x, y + 46, 62, 72, Catalog.Find(item.baseId), item.quality, true, false, 5, hot);
                if (unseen.Contains(item.uid)) Ui.Fill(x + 52, y + 46, 10, 10, Ui.Orange);
                if (Ui.Click(x, y + 46, 62, 72)) OpenItem(item);
                shown++;
            }
            if (shown == 0) Ui.Text(564, y + 46, 368, 72, "击败敌人后，回收到的装备会出现在这里", 14, Ui.Mute, TextAnchor.MiddleLeft);

            // 回收目标
            EquipmentBase target = Catalog.Find(model.state.recoveryTarget);
            bool hotTarget = Ui.Hover(964, y, 360, h);
            Ui.Fill(964, y, 360, h, hotTarget ? Ui.Tile : Ui.Ink);
            Ui.Text(target == null ? 984 : 1078, y + 14, 240, 18, "TARGET / 回收目标", 12, Ui.Orange, TextAnchor.MiddleLeft, true, true);
            if (target != null)
            {
                Ui.Fill(980, y + 24, 84, 84, Ui.Tile2); PaperDollView.DrawEquipmentIcon(target, Ui.R(987, y + 31, 70, 70));
                Ui.Text(1078, y + 36, 232, 28, target.name, 18, Ui.Panel, TextAnchor.MiddleLeft, true);
                int misses = model.TargetActive ? model.state.targetBossMisses : 0;
                for (int i = 0; i < 8; i++) Ui.Fill(1078 + i * 29, y + 74, 25, 8, i < misses ? Ui.Orange : Ui.Steel);
                string note = model.TargetActive ? "本区首领保底 " + misses + " / " + Balance.TargetBossGuarantee : "产地 · " + Catalog.RegionNames[Catalog.SourceRegion(target.id)];
                Ui.Text(1078, y + 90, 232, 22, note, 13, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            }
            else
            {
                Ui.Text(984, y + 40, 320, 30, "还没有设定回收目标", 18, Ui.Panel, TextAnchor.MiddleLeft, true);
                Ui.Text(984, y + 76, 320, 24, "去衣柜挑一件想要的装备", 13, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            }
            if (Ui.Click(964, y, 360, h))
            {
                if (target != null) { selectedLook = target.id; wardrobeSlot = (int)target.slot; }
                Go(GameScreen.Wardrobe);
            }

            // 暂停与倍速
            bool hotPause = Ui.Hover(1340, y, 228, 60);
            Color pauseFill = paused ? Ui.Orange : hotPause ? Ui.Ink : Ui.Panel, pauseText = paused ? Ui.Ink : hotPause ? Ui.Panel : Ui.Ink;
            Ui.Fill(1340, y, 228, 60, pauseFill); Ui.Frame(1340, y, 228, 60, 2, Ui.Ink);
            if (!paused) Ui.IconPause(1340 + 78, y + 30, pauseText);
            Ui.Text(paused ? 1340 : 1340 + 18, y, 228, 60, paused ? "继续行动" : "暂停", 18, pauseText, TextAnchor.MiddleCenter, true);
            if (Ui.Click(1340, y, 228, 60)) paused = !paused;
            int[] speeds = { 1, 2, 4 };
            for (int i = 0; i < 3; i++)
            {
                float x = 1340 + i * 76; bool on = speed == speeds[i], hot = Ui.Hover(x, y + 72, 76, 60);
                Ui.Fill(x, y + 72, 76, 60, on ? Ui.Ink : hot ? Ui.Rule : Ui.Panel);
                Ui.Text(x, y + 72, 76, 60, speeds[i] + "×", 24, on ? Ui.Panel : Ui.Ink, TextAnchor.MiddleCenter, true, true);
                if (Ui.Click(x, y + 72, 76, 60)) speed = speeds[i];
            }
            Ui.Frame(1340, y + 72, 228, 60, 2, Ui.Ink);
        }
    }
}
