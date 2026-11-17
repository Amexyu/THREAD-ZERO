using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    public sealed partial class GameApp
    {
        private const float StageX = 0, StageY = 0, StageW = 1600, StageH = 900, GroundY = 690;   // v0.15：战斗画面铺满全屏；界面平时隐藏（纯净模式），鼠标移到边缘才滑出
        // v0.8：镜头拉远。角色与敌人缩小，舞台里能看到更长的一段路。
        private const float HeroRestX = 428, HeroCanvas = 260, EnemyX = 1060, EnemyCanvas = 220, EnemyEliteCanvas = 260, EnemyBossCanvas = 300, PackSpacing = 136;
        private LayeredRigView.Anim lastRigAnim; private float lastRigT;
        private float liveEnemyX=EnemyX;
        private float hudShow, hudWantAt = -10;   // 0 = 纯净模式，1 = 终端界面完全滑出

        private static float EaseOutBack(float x) { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(x - 1, 3) + c1 * Mathf.Pow(x - 1, 2); }
        private static float Wrap(float value, float period) { value %= period; return value < 0 ? value + period : value; }
        private static float Noise(int n) { n = (n << 13) ^ n; return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f; }

        private void ActionPage()
        {
            Stats stats = model.Total();
            // 鼠标停在上 / 下边缘或左侧导航栏 → 呼出终端；离开 1.5 秒后收回。暂停、维修、弹窗时常驻。
            bool wantHud = Ui.Hover(0, 0, 1600, 104) || Ui.Hover(0, 724, 1600, 176) || Ui.Hover(0, 0, 104, 900)
                || paused || combat.recovery > 0 || navigation.HasModal;
            if (wantHud) hudWantAt = clock;
            if (Event.current.type == EventType.Repaint)
                hudShow = Mathf.MoveTowards(hudShow, clock - hudWantAt < 1.5f ? 1 : 0, Time.unscaledDeltaTime * 6);
            int region = model.state.region, district = Catalog.District(region);
            bool deep = region >= Catalog.Districts, moving = travelLeft > 0;
            bool boss = combat.Boss, melee = model.IsMelee, down = combat.recovery > 0;
            bool nearCombat = combat.HeroInRange || combat.EnemyInRange;
            float hit = moving || !nearCombat ? 0 : combat.hitFlash, dash = moving || !nearCombat ? 0 : combat.dashFlash;

            Ui.Fill(StageX, StageY, StageW, StageH, deep ? Ui.Hex(0xE9EAE5) : Ui.Stage);
            Ui.BeginClip(StageX, StageY, StageW, StageH, 0);
            Backdrop(region, district, deep);

            // v0.14 群怪：每只怪各自的世界坐标 → 屏幕坐标。depth 让怪群前后错开、互相重叠。
            Enemy target = combat.Target;
            float targetCanvas = target != null ? target.canvas : EnemyCanvas;
            float enemyMid = GroundY - targetCanvas * .42f;
            liveEnemyX = target != null ? target.x - worldX : StageX + StageW + 100;
            var back = new List<Enemy>(); var front = new List<Enemy>();
            foreach (Enemy e in combat.enemies)
            {
                if (!e.Alive) { DrawEnemyWreck(e); continue; }
                if (e.depth < .5f) back.Add(e); else front.Add(e);
            }
            back.Sort((a, b) => a.depth.CompareTo(b.depth)); front.Sort((a, b) => a.depth.CompareTo(b.depth));
            foreach (Enemy e in back) DrawEnemyUnit(e, e == target, deep);

            bool heroWalking = !down && combat.Movement.HeroMoving;
            float heroWalkPhase = combat.Movement.HeroTravel / 24f;
            DollPose pose = animationState.Pose(down);
            float stride = heroWalking ? Mathf.Abs(Mathf.Sin(heroWalkPhase)) * 7 : 0;
            float heroFeetX = combat.Movement.HeroScreenX;
            // 登场：转场后从地面检修舱口爬上来（先落到地面以下，再弹起落地）。
            float entry = combat.EntryLeft > 0 ? 1 - combat.EntryLeft / CombatSession.EntryTime : 1;
            float rise = entry < 1 ? (1 - EaseOutBack(Mathf.Clamp01(entry / .75f))) * 210 : 0;
            float heroFeetY = GroundY + 2 - stride + rise;
            if (entry < 1) Ui.Fill(heroFeetX - 60, GroundY - 2, 120, 8, Ui.Ink);   // 打开的舱口
            else Ui.Fill(heroFeetX - 56, GroundY - 3, 112, 5, Ui.Alpha(Ui.Ink, .16f));
            if (heroWalking)
                for (int k = 0; k < 3; k++)
                {
                    float phase = Wrap(worldX / 70f + k * .33f, 1), size = 3 + 7 * (1 - phase);
                    Ui.Fill(heroFeetX - 34 - phase * 46, GroundY - 4 - phase * 12 - size, size, size, Ui.Alpha(Ui.Grey, .55f * (1 - phase)));
                }
            if (dash > 0) Ui.Stroke(heroFeetX - 190, GroundY - 44, heroFeetX - 36, GroundY - 44, 4, Ui.Orange);
            Color heroTint = down ? new Color(.6f, .65f, .68f) : pose.damageTint > 0 ? new Color(1, 1 - pose.damageTint, 1 - pose.damageTint) : Color.white;
            int heroFrame = down || heroWalking ? ActorView.IdleFrame(melee) : ActorView.CombatFrame(pose.attackProgress, melee);
            float shake = down || heroWalking || pose.attackProgress > 0 ? 0 : pose.rootX;
            string headId = model.Equipped(Slot.Head) == null && model.state.appearance[(int)Slot.Head] == null
                ? "_default" : model.Appearance(Slot.Head).id;
            // Hold F9 on the action page to inspect the walk rig without waiting for travel.
            bool walkReview = Input.GetKey(KeyCode.F9);
            int rigCoat = walkReview ? 0 : model.Appearance(Slot.Coat).style;
            string rigHead = walkReview ? "h0" : headId;
            string rigWeapon = walkReview ? "w0" : model.Appearance(Slot.Weapon).id;
            string rigBack = walkReview ? "b0" : model.Appearance(Slot.Back).id;
            int daggerFrame=!heroWalking && !down && combat.DaggerActive?DaggerSkill.Frame(combat.DaggerAge,combat.DaggerCombo):0;
            // V13 layered starter rig (parts + rig.py motion). Hold F7 to compare with the old V12 frames.
            bool layered = LayeredRigView.CanDraw(model);
            if (layered)
            {
                // 动作：走路 / 初始匕首（普攻+迅切连击）/ 其他武器按类别（射击、重砸）/ 待机。
                LayeredRigView.Anim rigAnim = LayeredRigView.Anim.Idle; float rigT = clock / 1.2f;
                if (heroWalking && !down) { rigAnim = LayeredRigView.Anim.Walk; rigT = heroWalkPhase / (2 * Mathf.PI); }
                else if (!down && combat.DaggerActive) { rigAnim = LayeredRigView.DaggerAnim(combat.DaggerCombo); rigT = LayeredRigView.DaggerT(combat.DaggerAge, combat.DaggerCombo); }
                else if (!down && pose.attackProgress > 0) { rigAnim = LayeredRigView.ClassAttack(model); rigT = pose.attackProgress; }
                LayeredRigView.Draw(model, heroFeetX, GroundY + 2 + rise, 1, rigAnim, rigT, heroTint);
                if (Event.current.type == EventType.Repaint)
                {
                    // 动作走到开火 / 落锤那一帧时放特效（弹道、光束、炮弹、冲击波），起点是枪口 / 锤头的实际位置。
                    float prevT = rigAnim == lastRigAnim ? lastRigT : 0;
                    if (rigAnim == LayeredRigView.Anim.Shoot && prevT < LayeredRigView.FireT && rigT >= LayeredRigView.FireT && target != null)
                        WeaponFx.Fire(LayeredRigView.Kind, clock, LayeredRigView.Muzzle, LayeredRigView.MuzzleDir,
                            new Vector2(liveEnemyX - targetCanvas * .2f, enemyMid + UnityEngine.Random.Range(-14f, 14f)));
                    if (rigAnim == LayeredRigView.Anim.Smash && prevT < .42f && rigT >= .42f)
                        WeaponFx.Slam(clock, new Vector2(LayeredRigView.Muzzle.x, GroundY));
                    lastRigAnim = rigAnim; lastRigT = rigT;
                }
            }
            Matrix4x4 heroMatrix=GUI.matrix;
            if(!layered && heroWalking && !down)
            {
                Vector2 pivot=Ui.P(heroFeetX,heroFeetY);
                float squash=Mathf.Cos(heroWalkPhase*2)*.018f;
                GUIUtility.RotateAroundPivot(2.5f+Mathf.Sin(heroWalkPhase)*.6f,pivot);
                GUIUtility.ScaleAroundPivot(new Vector2(1+squash,1-squash),pivot);
            }
            bool newDagger = layered || DaggerActorView.Draw(model,heroFeetX+shake,heroFeetY,HeroCanvas,daggerFrame,heroTint);
            bool skeletal = !newDagger && SkeletalActorView.Draw(heroFeetX + shake, heroFeetY, HeroCanvas,
                rigCoat, rigHead, rigWeapon, rigBack,
                heroFrame, heroWalking || walkReview, walkReview ? clock * 6f : heroWalkPhase,
                pose.attackProgress, heroTint);
            bool wholeFrame = newDagger || skeletal || ActorView.Player(heroFeetX + shake, heroFeetY, HeroCanvas,
                model.Appearance(Slot.Coat).style, heroFrame, heroTint, headId, model.Appearance(Slot.Weapon).id);
            const float oldW = 300, oldH = 240;
            if (!wholeFrame) CharacterView.Draw(model, Ui.R(heroFeetX - oldW / 2, heroFeetY - oldH + 6, oldW, oldH), clock, Mathf.Abs(pose.weaponAngle) > .1f, moving ? 0 : combat.hurtFlash, down, dash, pose);
            GUI.matrix=heroMatrix;
            if (entry < 1)
            {
                // 地面以下的部分被地面挡住；舱盖竖在一旁，最后 0.25 秒合上。
                Ui.Fill(heroFeetX - 110, GroundY + 4, 220, StageY + StageH - GroundY - 4, Ui.Floor);
                Ui.Fill(heroFeetX - 110, GroundY, 220, 4, Ui.Ink);
                float close = Mathf.Clamp01((entry - .75f) / .25f);
                if (close <= 0) Ui.Fill(heroFeetX + 62, GroundY - 56, 8, 56, Ui.Steel);
                else Ui.Fill(heroFeetX - 60, GroundY - 4, 120 * close, 6, Ui.Steel);
            }

            foreach (Enemy e in front) DrawEnemyUnit(e, e == target, deep);
            // 主角头顶：血条 + 技能条（明日方舟式），掉血有白色残影。
            {
                float hp01 = Mathf.Clamp01(combat.health / Mathf.Max(1, stats.health));
                SkillDef skill = combat.EquippedSkill;
                int chargeMax = skill != null && skill.trigger == SkillTrigger.EveryNAttacks ? skill.attacks : 0;
                HudBars.Hero(heroFeetX, GroundY + rise - 224, hp01, HudBars.Ghost(-1, hp01, clock), combat.SkillCharge, chargeMax,
                    skill != null ? combat.SkillReadiness : 0, combat.health, stats.health, clock);
            }

            if (!layered && pose.projectile > 0 && target != null)
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
                Vector2 end = new Vector2(liveEnemyX - targetCanvas * .2f, enemyMid), point = Vector2.Lerp(start, end, pose.projectile), direction = (end - start).normalized;
                Ui.Stroke(point.x, point.y, point.x + direction.x * 30, point.y + direction.y * 30, 4, Ui.Orange);
            }
            WeaponFx.Draw(clock);
            ModuleEffects(heroFeetX, enemyMid, moving);
            DropEffect(heroFeetX);
            DrawFloaters(heroFeetX);
            Ui.EndClip();

            // 首领：屏幕上方长血条。其余信息在纯净模式只留四角细字，终端滑出时看完整界面。
            Enemy bossUnit = combat.BossEnemy;
            if (bossUnit != null)
            {
                float b01 = Mathf.Clamp01(bossUnit.health / Mathf.Max(1, bossUnit.maxHealth));
                HudBars.Boss(800, 132, b01, HudBars.Ghost(bossUnit.id, b01, clock), combat.NameOf(bossUnit), bossUnit.health, bossUnit.maxHealth);
            }
            PureOverlay(1 - hudShow);

            if (down || paused)
            {
                string line = down ? "维修中 · " + combat.recovery.ToString("F1") + " 秒" : "已暂停";
                string note = down ? (region > 0 ? "修好后撤回上一关补给，变强后自动再挑战" : "装备与收藏不会丢失，修好后重新开始本关") : "按空格或右下角按钮继续";
                Ui.Fill(StageX + StageW / 2 - 210, StageY + 170, 420, 76, Ui.Ink); Ui.Fill(StageX + StageW / 2 - 210, StageY + 170, 8, 76, Ui.Orange);
                Ui.Text(StageX + StageW / 2 - 180, StageY + 178, 380, 36, line, 22, Ui.Panel, TextAnchor.MiddleLeft, true);
                Ui.Text(StageX + StageW / 2 - 180, StageY + 212, 380, 24, note, 13, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            }
            StageTransitionOverlay();
            // 终端界面：上下两条实心带滑入，盖在画面上（画面本身不动）。
            if (hudShow > .001f)
            {
                Matrix4x4 m0 = GUI.matrix; float e = 1 - Mathf.Pow(1 - hudShow, 3);
                GUI.matrix = Matrix4x4.Translate(new Vector3(0, -(1 - e) * 112 * Ui.K, 0)) * m0;
                Ui.Fill(0, 0, 1600, 104, Ui.Ground); Ui.Fill(0, 104, 1600, 2, Ui.Ink);
                ActionHeader();
                GUI.matrix = Matrix4x4.Translate(new Vector3(0, (1 - e) * 186 * Ui.K, 0)) * m0;
                Ui.Fill(0, 726, 1600, 174, Ui.Ground); Ui.Fill(0, 724, 1600, 2, Ui.Ink);
                ActionFooter();
                GUI.matrix = m0;
            }
        }

        // 一只活怪：影子、本体（走路/出手姿态）、头顶小血条。
        private void DrawEnemyUnit(Enemy e, bool isTarget, bool deep)
        {
            float x = e.x - worldX;
            if (x < StageX - e.canvas || x > StageX + StageW + e.canvas) return;
            float feetY = GroundY + 2 + (e.depth - .5f) * 18;
            bool floating = e.Art == 2 && !e.boss;
            if (floating) feetY += Mathf.Sin(clock * 2 + e.id) * 5 - 8;
            Ui.Fill(x - e.canvas * .26f, GroundY - 3 + (e.depth - .5f) * 18, e.canvas * .52f, 5, Ui.Alpha(Ui.Ink, .16f));
            float strike = e.strikeFlash > 0 ? Mathf.Sin(Mathf.Clamp01(e.strikeFlash / .25f) * Mathf.PI) : 0;
            float recoil = e.hitFlash > 0 ? e.hitFlash / .3f * 6 : 0;
            Color tint = e.hitFlash > 0 ? new Color(1, .74f, .68f) : e.elite ? new Color(1, .86f, .8f) : deep ? new Color(.84f, .88f, 1) : Color.white;
            if (!DrawMovingEnemy(x + recoil, feetY, e.canvas, e.Art, e.boss, tint, e.moving, e.travel / 21f, strike))
            {
                float boxW = e.canvas * .5f, boxH = e.canvas * .4f;
                DrawEnemy(x, GroundY - 50 - boxH, boxW, boxH, e.boss, e.hitFlash, false);
            }
            float top = feetY - e.canvas * .82f;
            if (isTarget && combat.markTime > 0) MarkFrame(x - e.canvas * .42f, top - 8, x + e.canvas * .42f, GroundY + 14);
            if (e.boss) return;   // 首领血量在右上铭牌
            float hp01 = Mathf.Clamp01(e.health / Mathf.Max(1, e.maxHealth));
            HudBars.Enemy(x, top - 18, hp01, HudBars.Ghost(e.id, hp01, clock), e.elite);
        }

        // 倒下的怪：压扁、变灰，几秒后淡出（随角色前进被甩到画面左侧）。
        private void DrawEnemyWreck(Enemy e)
        {
            float x = e.x - worldX;
            if (x < StageX - e.canvas || x > StageX + StageW + e.canvas) return;
            float fade = Mathf.Clamp01(1 - (combat.Elapsed - e.deadAt - 4) / 2);
            if (fade <= 0) return;
            float feetY = GroundY + 8 + (e.depth - .5f) * 18;
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(-9, Ui.P(x, feetY));
            GUIUtility.ScaleAroundPivot(new Vector2(1.05f, .28f), Ui.P(x, feetY));
            bool drawn = ActorView.Enemy(x, feetY, e.canvas, e.Art, e.boss, new Color(.42f, .44f, .47f, .75f * fade));
            GUI.matrix = old;
            if (!drawn) Ui.Fill(x - e.canvas * .25f, feetY - 34, e.canvas * .5f, 34, Ui.Alpha(Ui.Grey, fade));
        }

        // 关卡转场（约 2 秒）：变暗 → 纵向管道上标记往上爬一格、亮出下一关编号 → 变亮。
        private void StageTransitionOverlay()
        {
            if (transitionLeft <= 0) return;
            float p = 1 - transitionLeft / Chapters.StageTransition;
            float alpha = p < .25f ? p / .25f : p > .75f ? (1 - p) / .25f : 1;
            Ui.Fill(StageX, StageY, StageW, StageH, Ui.Alpha(Ui.Ink, .94f * alpha));
            if (alpha < .4f) return;
            float a = Mathf.Clamp01((alpha - .4f) / .6f);
            float cx = StageX + StageW / 2 - 260, top = StageY + 70, bottom = StageY + StageH - 70;
            int stages = Chapters.StagesPerChapter;
            Ui.Fill(cx - 3, top, 6, bottom - top, Ui.Alpha(Ui.Steel, a));
            for (int r = 0; r < stages; r++)
            {
                float y = Mathf.Lerp(bottom, top, r / (float)(stages - 1));
                bool passed = r < transitionTo, here = r == transitionTo;
                StageKind kind = Chapters.Stage(r).kind;
                float size = kind == StageKind.Boss ? 22 : kind == StageKind.Elite ? 18 : 14;
                Color c = here ? Ui.Orange : passed ? QualityVisual.Colors[1] : Ui.Steel;
                Ui.Fill(cx - size / 2, y - size / 2, size, size, Ui.Alpha(c, a));
            }
            float climb = Mathf.SmoothStep(0, 1, Mathf.Clamp01((p - .25f) / .4f));
            float fromY = Mathf.Lerp(bottom, top, transitionFrom / (float)(stages - 1)), toY = Mathf.Lerp(bottom, top, transitionTo / (float)(stages - 1));
            float my = Mathf.Lerp(fromY, toY, climb);
            Ui.Fill(cx + 18, my - 6, 22, 12, Ui.Alpha(QualityVisual.Colors[1], a));
            StageDef stage = Chapters.Stage(transitionTo);
            string kindText = stage.kind == StageKind.Boss ? "首领" : stage.kind == StageKind.Elite ? "精英" : "";
            Ui.Text(cx + 90, StageY + StageH / 2 - 90, 600, 110, Catalog.StageCode(transitionTo), 96, Ui.Alpha(Ui.Panel, a), TextAnchor.MiddleLeft, true, true);
            Ui.Text(cx + 96, StageY + StageH / 2 + 20, 600, 40, Catalog.RegionNames[transitionTo] + (kindText.Length > 0 ? " · " + kindText : ""), 26, Ui.Alpha(Ui.MuteOnInk, a), TextAnchor.MiddleLeft, true);
            string note = transitionTo > transitionFrom ? "向上一层" : transitionTo < transitionFrom ? "撤回补给" : "重新开始";
            Ui.Text(cx + 96, StageY + StageH / 2 + 64, 600, 30, note, 16, Ui.Alpha(Ui.Orange, a), TextAnchor.MiddleLeft, true);
        }

        private bool DrawMovingEnemy(float x,float y,float canvas,int district,bool boss,Color tint,bool walking,float phase,float strike)
        {
            Matrix4x4 previous=GUI.matrix;
            float hop=walking?Mathf.Abs(Mathf.Sin(phase))*4:0;
            Vector2 pivot=Ui.P(x,y-hop);
            if(walking)GUIUtility.RotateAroundPivot(-2.5f+Mathf.Sin(phase)*.6f,pivot);
            else if(strike>0)GUIUtility.RotateAroundPivot(-strike*3,pivot);
            bool drawn=ActorView.Enemy(x-strike*5,y-hop,canvas,district,boss,tint);
            GUI.matrix=previous;return drawn;
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
            GUIUtility.ScaleAroundPivot(new Vector2(1.05f,.28f),Ui.P(x,GroundY));
            bool drawn = ActorView.Enemy(x, GroundY + 8, canvas, wreckDistrict, wreckBoss, new Color(.42f, .44f, .47f, .75f));
            GUI.matrix = old;
            if (!drawn) Ui.Fill(x - 55, GroundY - 34, 110, 34, Ui.Grey);
        }

        // 纯净模式：四角细框 + 左上关卡号与路线点 + 上方波次；边缘一道淡线提示可以呼出终端。
        private void PureOverlay(float a)
        {
            if (a <= .01f) return;
            Color ink = Ui.Alpha(Ui.Ink, .85f * a), mute = Ui.Alpha(Ui.Mute, .9f * a);
            const float m = 18, L = 26, t = 2;
            Ui.Fill(m, m, L, t, ink); Ui.Fill(m, m, t, L, ink);
            Ui.Fill(1600 - m - L, m, L, t, ink); Ui.Fill(1600 - m - t, m, t, L, ink);
            Ui.Fill(m, 900 - m - t, L, t, ink); Ui.Fill(m, 900 - m - L, t, L, ink);
            Ui.Fill(1600 - m - L, 900 - m - t, L, t, ink); Ui.Fill(1600 - m - t, 900 - m - L, t, L, ink);
            int region = model.state.region;
            Ui.Text(40, 30, 200, 56, Catalog.StageCode(region), 48, ink, TextAnchor.MiddleLeft, true, true);
            StageKind kind = Chapters.Stage(region).kind;
            string kindText = kind == StageKind.Boss ? " · 首领" : kind == StageKind.Elite ? " · 精英" : "";
            Ui.Text(42, 84, 400, 22, Catalog.RegionNames[region] + kindText, 15, ink, TextAnchor.MiddleLeft, true);
            for (int r = 0; r < Chapters.StagesPerChapter; r++)
            {
                bool now = r == region, done = r < model.state.unlockedRegion;
                float size = now ? 10 : 8, x = 44 + r * 16 + (now ? -1 : 0);
                Ui.Fill(x, 116 - size / 2, size, size, now ? Ui.Alpha(Ui.Orange, a) : done ? ink : Ui.Alpha(Ui.Rule, a));
            }
            if (combat.BossEnemy == null)
                Ui.Text(500, 30, 600, 24, "—  第 " + combat.wave + " / " + combat.WaveCount + " 波 · 剩余 " + combat.Alive + "  —", 14, mute, TextAnchor.MiddleCenter, true);
            Ui.Text(1200, 900 - 52, 360, 20, "鼠标移到边缘 · 呼出终端", 12, Ui.Alpha(Ui.Mute, .6f * a), TextAnchor.MiddleRight);
            Ui.Fill(0, 0, 1600, 2, Ui.Alpha(Ui.Ink, .18f * a)); Ui.Fill(0, 898, 1600, 2, Ui.Alpha(Ui.Ink, .18f * a));
        }

        private void ActionHeader()
        {
            int region = model.state.region;
            string name = Catalog.RegionNames[region];
            float nameWidth = Ui.Width(name, 30, true);
            bool hover = Ui.Hover(128, 16, 190 + nameWidth, 76);
            Ui.Text(128, 12, 120, 80, Catalog.StageCode(region), 52, Ui.Ink, TextAnchor.MiddleLeft, true, true);
            Ui.Text(250, 22, 240, 18, "CHAPTER 1 / 第一章", 13, Ui.Mute, TextAnchor.MiddleLeft, true, true);
            Ui.Text(250, 42, nameWidth + 10, 40, name, 30, Ui.Ink, TextAnchor.MiddleLeft, true);
            float boxX = 250 + nameWidth + 16;
            Ui.Fill(boxX, 44, 36, 36, hover ? Ui.Ink : Ui.Ground); Ui.Frame(boxX, 44, 36, 36, 2, Ui.Ink);
            Ui.IconChevron(boxX + 18, 62, hover ? Ui.Panel : Ui.Ink);
            if (Ui.Click(128, 16, 190 + nameWidth, 76)) navigation.Open(GameModal.Map);

            // 路线：一章 7 关。当前关下方的小格 = 本关波次进度。
            float x = 640, middle = 50;
            Ui.Text(x - 66, middle - 12, 56, 24, "ROUTE", 14, Ui.Mute, TextAnchor.MiddleRight, true, true);
            for (int r = 0; r < Chapters.StagesPerChapter; r++)
            {
                StageKind kind = Chapters.Stage(r).kind;
                bool now = r == region, done = r < model.state.unlockedRegion || (kind == StageKind.Boss && model.state.bosses > 0), locked = r > model.state.unlockedRegion;
                float size = now ? 48 : 38;
                string label = kind == StageKind.Boss ? "首" : kind == StageKind.Elite ? "精" : (r + 1).ToString();
                bool digit = kind == StageKind.Normal;
                if (now) { Ui.Fill(x, middle - size / 2, size, size, Ui.Orange); Ui.Text(x, middle - size / 2, size, size, label, digit ? 26 : 20, Ui.Ink, TextAnchor.MiddleCenter, true, digit); }
                else if (done) { Ui.Fill(x, middle - size / 2, size, size, Ui.Ink); Ui.Text(x, middle - size / 2, size, size, label, digit ? 18 : 15, Ui.Panel, TextAnchor.MiddleCenter, true, digit); }
                else { Ui.Frame(x, middle - size / 2, size, size, 2, locked ? Ui.Rule : Ui.Grey); Ui.Text(x, middle - size / 2, size, size, label, digit ? 18 : 15, locked ? Ui.Rule : Ui.Mute, TextAnchor.MiddleCenter, true, digit); }
                if (now)
                {
                    int waves = combat.WaveCount; float w = 10, gap = 4, total = waves * w + (waves - 1) * gap;
                    for (int k = 0; k < waves; k++)
                        Ui.Fill(x + size / 2 - total / 2 + k * (w + gap), middle + size / 2 + 6, w, 4, k < combat.wave - 1 ? Ui.Ink : k == combat.wave - 1 ? Ui.Orange : Ui.Rule);
                }
                x += size;
                if (r < Chapters.StagesPerChapter - 1) { Ui.Fill(x, middle - 2, 30, 4, done ? Ui.Ink : Ui.Rule); x += 30; }
            }

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
                if (!moving && (combat.HeroInRange || combat.EnemyInRange) && combat.droneFlash > 0) Ui.Stroke(dx + 24, dy + 5, liveEnemyX, enemyMid, 3, Ui.Orange);
            }
            if (moving || (!combat.HeroInRange && !combat.EnemyInRange)) return;
            if (combat.arcFlash > 0)
            {
                Color arc = QualityVisual.Colors[3]; float x0 = feetX + 84, span = liveEnemyX - x0;
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

        // ---------- 掉落：从残骸弹出 → 落地闪一下 → 被吸向角色（不再钉在原地） ----------
        private sealed class DropFx { public Item item; public EquipmentBase b; public float from, wx, side; }
        private readonly List<DropFx> dropFx = new List<DropFx>();
        private const float DropPop = .5f, DropFly = .36f, DropFlash = .28f;
        private static float DropRest(Quality q) { return .22f + (int)q * .12f; }
        private void SpawnDrop(Item item)
        {
            EquipmentBase b = Catalog.Find(item.baseId); if (b == null) return;
            if (dropFx.Count >= 8) dropFx.RemoveAt(0);
            dropFx.Add(new DropFx { item = item, b = b, from = clock, wx = combat.lastKillX, side = (dropFx.Count % 2 == 0 ? 1 : -1) * (20 + dropFx.Count * 6) });
        }
        private float DropScreenX(float wx) { return combat.MovementEnabled ? wx - worldX : EnemyX - (worldX - wx); }
        private void DropEffect(float heroX)
        {
            const float W = 44, H = 48;
            Vector2 hero = new Vector2(heroX, GroundY - 118);
            for (int i = dropFx.Count - 1; i >= 0; i--)
            {
                DropFx d = dropFx[i]; float t = clock - d.from, rest = DropRest(d.item.quality);
                Color qc = QualityVisual.Colors[(int)d.item.quality];
                float gx = DropScreenX(d.wx) + d.side, groundTop = GroundY - H - 4;
                if (t < DropPop)
                {   // 抛物线弹出，落地时轻微回弹
                    float u = t / DropPop, x = Mathf.Lerp(DropScreenX(d.wx), gx, u);
                    float y = groundTop - 4 * u * (1 - u) * 110 - 70 * (1 - u) * (1 - u);
                    Ui.Fill(gx - 16, GroundY - 3, 32, 3, Ui.Alpha(Ui.Ink, .18f * u));
                    Ui.ItemTile(x - W / 2, y, W, H, d.b, d.item.quality, true, false, 3, false);
                }
                else if (t < DropPop + rest)
                {   // 落地停一下：轻微上下浮动 + 品质色底光
                    float u = (t - DropPop) / rest, bob = Mathf.Sin(u * Mathf.PI) * 5;
                    Ui.Fill(gx - 18, GroundY - 3, 36, 3, Ui.Alpha(qc, .55f));
                    if (model.state.glowMode > 0 && (int)d.item.quality >= 2)
                        QualityVisual.Halo(Ui.P(gx, groundTop + H / 2 - bob), 46 * Ui.K, qc, model.state.glowMode == 1 ? .25f : .5f);
                    Ui.ItemTile(gx - W / 2, groundTop - bob, W, H, d.b, d.item.quality, true, false, 3, false);
                }
                else if (t < DropPop + rest + DropFly)
                {   // 吸向角色：先慢后快、边飞边缩小，拖一道品质色尾迹
                    float u = (t - DropPop - rest) / DropFly, e = u * u * u, s = Mathf.Lerp(1, .32f, e);
                    Vector2 a = new Vector2(gx, groundTop + H / 2), c = a + new Vector2(0, -90);
                    Vector2 p = Bez(a, c, hero, e), q = Bez(a, c, hero, Mathf.Max(0, e - .18f));
                    Ui.Stroke(q.x, q.y, p.x, p.y, 5 * s + 1, Ui.Alpha(qc, .5f));
                    Ui.ItemTile(p.x - W * s / 2, p.y - H * s / 2, W * s, H * s, d.b, d.item.quality, true, false, 3 * s, false);
                }
                else if (t < DropPop + rest + DropFly + DropFlash)
                {   // 收进身上：一圈小闪光
                    float u = (t - DropPop - rest - DropFly) / DropFlash;
                    Ui.Disk(hero.x, hero.y, 10 + u * 30, Ui.Alpha(qc, .35f * (1 - u)));
                    Ui.Disk(hero.x, hero.y, 5 + u * 8, Ui.Alpha(Color.white, .7f * (1 - u)));
                }
                else dropFx.RemoveAt(i);
            }
        }
        private static Vector2 Bez(Vector2 a, Vector2 c, Vector2 b, float t) { float m = 1 - t; return m * m * a + 2 * m * t * c + t * t * b; }

        private void DrawFloaters(float feetX)
        {
            for (int i = 0; i < floaters.Count; i++)
            {
                Floater f = floaters[i]; float age = (clock - f.born) / .9f, rise = 54 * age, alpha = age < .7f ? 1 : Mathf.Clamp01(1 - (age - .7f) / .3f);
                if (f.onEnemy)
                {
                    float size = f.critical ? 42 : 28, x = f.x - worldX - 30 + f.drift, y = GroundY - 230 - rise - f.lift;
                    string value = f.value.ToString("F0");
                    if (f.critical) { Ui.Fill(x - 56, y + 20, 48, 24, Ui.Alpha(Ui.Orange, alpha)); Ui.Text(x - 56, y + 20, 48, 24, "暴击", 14, Ui.Alpha(Ui.Ink, alpha), TextAnchor.MiddleCenter, true); }
                    Ui.Text(x, y, 220, 64, value, size, Ui.Alpha(Ui.Ink, alpha), TextAnchor.MiddleLeft, true, true);
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
