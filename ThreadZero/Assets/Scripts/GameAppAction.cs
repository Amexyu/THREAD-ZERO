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
            WeaponFx.SetGear(model.Equipped(Slot.Weapon));   // v0.20 装备光效：开火、命中的光按武器品质 / 等级
            // 鼠标停在上 / 下边缘或左侧导航栏 → 呼出终端；离开 1.5 秒后收回。暂停、维修、弹窗时常驻。
            bool wantHud = Ui.Hover(0, 0, 1600, 104) || Ui.Hover(0, 724, 1600, 176) || Ui.Hover(0, 0, 104, 900)
                || paused || combat.recovery > 0 || navigation.HasModal;
            if (wantHud) hudWantAt = clock;
            if (Event.current.type == EventType.Repaint)
            {   // 滑出约 0.3 秒、收回约 0.45 秒；进度再过一道 ease-in-out，起停都不生硬
                bool show = clock - hudWantAt < 1.5f;
                hudShow = Mathf.MoveTowards(hudShow, show ? 1 : 0, Mathf.Min(Time.unscaledDeltaTime, 1 / 30f) * (show ? 3.4f : 2.2f));
            }
            int region = model.state.region, district = Catalog.District(region);
            bool deep = region >= Catalog.Districts, moving = travelLeft > 0;
            bool boss = combat.Boss, melee = model.IsMelee, down = combat.recovery > 0;
            bool nearCombat = combat.HeroInRange || combat.EnemyInRange;
            float hit = moving || !nearCombat ? 0 : combat.hitFlash, dash = moving || !nearCombat ? 0 : combat.dashFlash;

            Ui.Fill(StageX, StageY, StageW, StageH, deep ? Ui.Hex(0xE9EAE5) : Ui.Stage);
            Ui.BeginClip(StageX, StageY, StageW, StageH, 0);
            { Impact.Scale = model.state.shakeMode == 0 ? 1 : model.state.shakeMode == 1 ? .5f : 0; Vector2 q = Impact.Offset(clock); Ui.Nudge(q.x, q.y); }   // v0.20 震屏：舞台里的东西整体抖，界面不抖
            float camZoom = LayeredRigView.SkillZoom; bool zoomed = Mathf.Abs(camZoom - 1) > .002f;   // v0.20 技能镜头推近（用上一帧的值）
            if (zoomed) Ui.ZoomBegin(lastHeroFeetX, GroundY - 100, camZoom);
            Backdrop(region, district, deep);
            TransitionUnder();

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
            float heroFeetX = combat.Movement.HeroScreenX; lastHeroFeetX = heroFeetX;
            // 登场：转场后从地面检修舱口爬上来（先落到地面以下，再弹起落地）。
            float entry = combat.EntryLeft > 0 ? 1 - combat.EntryLeft / CombatSession.EntryTime : 1;
            float rise = entry < 1 ? (1 - EaseOutBack(Mathf.Clamp01(entry / .75f))) * 210 : 0;
            float heroFeetY = GroundY + 2 - stride + rise;
            DrawRobots(heroFeetX, entry < 1 ? Mathf.Clamp01((entry - .6f) / .4f) : 1, down, heroWalking, stats);   // v0.21 机库：上场的机器人站在主角身后（画在主角后面）
            if (entry < 1) Ui.Fill(heroFeetX - 60, GroundY - 2, 120, 8, Ui.Ink);   // 打开的舱口
            else Ui.Fill(heroFeetX - 56, GroundY - 3, 112, 5, Ui.Alpha(Ui.Ink, .16f));
            if (heroWalking)
                for (int k = 0; k < 3; k++)
                {
                    float phase = Wrap(worldX / 70f + k * .33f, 1), size = 4 + 8 * phase;   // 脚下扬起的尘土：软圆、越飘越淡
                    Ui.Disk(heroFeetX - 30 - phase * 50, GroundY - 6 - phase * 16, size, new Color(.62f, .6f, .56f, .35f * (1 - phase)));
                }

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
            // V13 layered starter rig (parts + rig.py motion).
            bool layered = LayeredRigView.CanDraw(model);
            if (layered)
            {
                // 动作：走路 / 初始匕首（普攻+迅切连击）/ 其他武器按类别（射击、重砸）/ 待机。
                LayeredRigView.Anim rigAnim = LayeredRigView.Anim.Idle; float rigT = clock / 1.2f;
                if (heroWalking && !down) { rigAnim = LayeredRigView.Anim.Walk; rigT = heroWalkPhase / (2 * Mathf.PI); }
                else if (!down && combat.DaggerActive && combat.ActiveSkill != null && combat.ActiveSkill != SkillBook.QuickCut)
                {   // 其他武器的技能：有专属动作的整段播（v0.19：震地 / 贯穿突刺 / 回旋切割）；还没有的按技能每一段把普攻动作对到命中时刻
                    LayeredRigView.Anim own = LayeredRigView.SkillAnim(combat.ActiveSkill.animation);
                    if (own != LayeredRigView.Anim.Idle) { rigAnim = own; rigT = Mathf.Clamp01(combat.DaggerAge / combat.ActiveSkill.duration); }
                    else
                    {
                        rigAnim = LayeredRigView.ClassAttack(model); rigT = SkillRigT(combat.ActiveSkill, combat.DaggerAge, rigAnim);
                        if (combat.ActiveSkill.animation == "sweep") rigAnim = LayeredRigView.Anim.Sweep;
                    }
                }
                else if (!down && combat.DaggerActive) { rigAnim = LayeredRigView.DaggerAnim(combat.DaggerCombo); rigT = LayeredRigView.DaggerT(combat.DaggerAge, combat.DaggerCombo); }
                else if (!down && pose.attackProgress > 0) { rigAnim = LayeredRigView.ClassAttack(model); rigT = pose.attackProgress; }
                if (target != null) LayeredRigView.ChainReachPx = Mathf.Clamp(liveEnemyX - targetCanvas * .15f - (heroFeetX + 30), 40, 320);
                LayeredRigView.Draw(model, heroFeetX, GroundY + 2 + rise, 1, rigAnim, rigT, heroTint);
                ImplantInstallFx();
                if (Event.current.type == EventType.Repaint)
                {
                    // 动作走到开火 / 落锤那一帧时放特效（弹道、光束、炮弹、冲击波），起点是枪口 / 锤头的实际位置。
                    float prevT = rigAnim == lastRigAnim ? lastRigT : 0;
                    if (rigAnim == LayeredRigView.Anim.Shoot && prevT < LayeredRigView.FireT && rigT >= LayeredRigView.FireT && target != null)
                        WeaponFx.Fire(LayeredRigView.Kind, clock, LayeredRigView.Muzzle, LayeredRigView.MuzzleDir,
                            new Vector2(liveEnemyX - targetCanvas * .2f, enemyMid + UnityEngine.Random.Range(-14f, 14f)));
                    if ((rigAnim == LayeredRigView.Anim.Throw || rigAnim == LayeredRigView.Anim.Sweep) && prevT < .42f && rigT >= .42f)
                        EnemyFx.Burst(clock, LayeredRigView.Muzzle.x + worldX, LayeredRigView.Muzzle.y, new Color(1, .62f, .25f), .9f);   // 吊钩打中：火星
                    if (rigAnim == LayeredRigView.Anim.Smash && prevT < .42f && rigT >= .42f)
                        WeaponFx.Slam(clock, new Vector2(LayeredRigView.Muzzle.x, GroundY));
                    SkillDef act = combat.DaggerActive ? combat.ActiveSkill : null;   // 技能专属动作：命中帧特效、顿帧、震屏，蓄能 / 排气这类按动作进度触发的效果
                    if (act != null && (LayeredRigView.SkillAnim(act.animation) == rigAnim || (act == SkillBook.QuickCut && rigAnim == LayeredRigView.Anim.Combo)))
                        SkillFrameFx(rigAnim, act, prevT, rigT, target, targetCanvas, enemyMid);
                    else if (act == null && LayeredRigView.SkillCharge > .01f) LayeredRigView.SkillCharge = 0;
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
                    skill != null ? combat.SkillReadiness : 0, combat.health, stats.health, clock, combat.shield / Mathf.Max(1, stats.health));
                SkillCallout(heroFeetX, GroundY + rise - 262);
                if (combat.JamLeft > 0)
                {   // 第三章首领的断讯脉冲：头顶挂一个闪烁的标签，提醒现在出手变慢了
                    float a = .65f + .35f * Mathf.Abs(Mathf.Sin(clock * 9));
                    Ui.Fill(heroFeetX - 80, GroundY + rise - 300, 160, 26, Ui.Alpha(Ui.Ink, .85f * a));
                    Ui.Text(heroFeetX - 80, GroundY + rise - 300, 160, 26, "断讯 · 出手变慢 " + combat.JamLeft.ToString("F1"), 13, Ui.Alpha(Ui.Orange, a), TextAnchor.MiddleCenter, true);
                }
                if (combat.MirrorLeft > 0)
                {   // 第四章首领的镜面反射：打它会反伤
                    float a = .65f + .35f * Mathf.Abs(Mathf.Sin(clock * 7));
                    Ui.Fill(heroFeetX - 80, GroundY + rise - 330, 160, 26, Ui.Alpha(Ui.Ink, .85f * a));
                    Ui.Text(heroFeetX - 80, GroundY + rise - 330, 160, 26, "镜面 · 打它会反伤 " + combat.MirrorLeft.ToString("F1"), 13, Ui.Alpha(new Color(.75f, .95f, 1f), a), TextAnchor.MiddleCenter, true);
                }
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
            DrawEnemyShots();
            EnemyFx.Draw(clock, worldX);
            ModuleEffects(heroFeetX, enemyMid, moving);
            DropEffect(heroFeetX);
            DrawFloaters(heroFeetX);
            if (zoomed) Ui.ZoomEnd();
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
                Matrix4x4 m0 = GUI.matrix; float e = HudEase;
                GUI.matrix = Matrix4x4.Translate(new Vector3(0, -(1 - e) * 112 * Ui.K, 0)) * m0;
                Ui.Fill(0, 0, 1600, 104, Ui.Ground); Ui.Fill(0, 104, 1600, 2, Ui.Ink);
                ActionHeader();
                GUI.matrix = Matrix4x4.Translate(new Vector3(0, (1 - e) * 186 * Ui.K, 0)) * m0;
                Ui.Fill(0, 726, 1600, 174, Ui.Ground); Ui.Fill(0, 724, 1600, 2, Ui.Ink);
                ActionFooter();
                GUI.matrix = m0;
            }
        }
        private float HudEase { get { float h = hudShow; return h < .5f ? 4 * h * h * h : 1 - Mathf.Pow(-2 * h + 2, 3) / 2; } }

        // 一只活怪：影子、本体（走路/出手姿态）、头顶小血条。
        private void DrawEnemyUnit(Enemy e, bool isTarget, bool deep)
        {
            float x = e.x - worldX;
            if (x < StageX - e.canvas || x > StageX + StageW + e.canvas) return;
            float feetY = GroundY + 2 + (e.depth - .5f) * 18;
            bool floating = StageArt.GaitOf(e) == StageArt.Gait.Flying;
            if (floating) feetY += Mathf.Sin(clock * 2 + e.id) * 5 - 8;
            feetY -= PopLift(e); x += ZapJitter(e);   // v0.20 被震起 / 被电到
            Ui.Fill(x - e.canvas * .26f, GroundY - 3 + (e.depth - .5f) * 18, e.canvas * .52f, 5, Ui.Alpha(Ui.Ink, .16f));
            float recoil = e.hitFlash > 0 ? e.hitFlash / .3f * 6 : 0;
            Color tint = e.hitFlash > 0 ? (e.boss ? new Color(1, .88f, .84f) : new Color(1, .74f, .68f)) : e.elite ? new Color(1, .86f, .8f) : deep ? new Color(.84f, .88f, 1) : Color.white;
            if (e.boss && e.phase >= 2) tint = Color.Lerp(tint, new Color(1, .55f, .5f), .45f + .15f * Mathf.Sin(clock * 8));
            if (e.hasteLeft > 0) tint = Color.Lerp(tint, new Color(.85f, .7f, 1), .35f);
            EnemyPoseT pose = EnemyPose(e);
            if (pose.flash > 0) tint = Color.Lerp(tint, EnemyFx.Warn, pose.flash);
            EnemyTelegraph(e, x, feetY);
            if (!DrawMovingEnemy(e, x + recoil, feetY, e.canvas, tint, e.moving && e.windLeft <= 0, 0, pose))
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
            Color wreck = new Color(.42f, .44f, .47f, .75f * fade);
            bool drawn = StageArt.DrawEnemy(e, StageArt.Pose.Idle, x, feetY, e.canvas, wreck) || ActorView.Enemy(x, feetY, e.canvas, e.Art, e.boss, wreck);
            GUI.matrix = old;
            if (!drawn) Ui.Fill(x - e.canvas * .25f, feetY - 34, e.canvas * .5f, 34, Ui.Alpha(Ui.Grey, fade));
        }

        // ---------- v0.16 快速转场（挂机不打断）----------
        // 0 无缝推进（日常过关 / 撤回）：旧场景带视差滑出淡掉，约 0.45 秒，转场一开始就换关。
        // 1 快闸（进精英关 / 首领关）：闸门合上→亮关卡号→打开，约 1 秒，闸门合拢时换关。
        // 2 织线一扫（进入一章）：织线带从左扫到右，扫过处是新场景，约 0.6 秒。
        // 三种结束后左上角弹出关卡条，停约 1.5 秒自动收回。
        public const int TransPush = 0, TransGate = 1, TransWeave = 2;
        public static float TransitionLength(int kind) { return kind == TransGate ? 1f : kind == TransWeave ? .6f : .45f; }
        public static float TransitionSwapAt(int kind) { return kind == TransGate ? .3f : 0; }
        private float TransitionP { get { return transitionLeft > 0 ? 1 - transitionLeft / transitionDur : 1; } }

        // 画在背景之上、角色之下：旧场景的残影。
        private void TransitionUnder()
        {
            if (transitionLeft <= 0 || !transitionSwapped) return;
            int oldScene = SceneOf(transitionFrom);
            float p = TransitionP;
            if (transitionKind == TransPush)
            {
                float e = Mathf.SmoothStep(0, 1, Mathf.Clamp01(p / .85f));
                Color old = GUI.color; GUI.color = new Color(1, 1, 1, 1 - e);
                StageArt.Background(oldScene, worldX + 260 * e, StageW, StageH, GroundY);
                GUI.color = old;
            }
            else if (transitionKind == TransWeave)
            {
                float bx = WeaveX(p);
                if (bx + 260 < StageW)
                {
                    Ui.BeginClip(bx + 260, 0, StageW - bx - 260, StageH, 0);
                    StageArt.Background(oldScene, worldX, StageW, StageH, GroundY);
                    Ui.EndClip();
                }
            }
        }
        private static float WeaveX(float p) { float e = p < .5f ? 2 * p * p : 1 - Mathf.Pow(-2 * p + 2, 2) / 2; return Mathf.Lerp(-260, 1600, e); }

        // 画在最上层：闸门、织线带、闪光、关卡条。
        private void StageTransitionOverlay()
        {
            StageTag();
            AscensionBanner(); AscensionStageBanner(); ShardTicker();
            AnomalyOffer();
            if (transitionLeft <= 0) return;
            float p = TransitionP;
            if (transitionKind == TransPush)
            {
                float f = p < .3f ? p / .3f : Mathf.Clamp01(1 - (p - .3f) / .5f);
                Ui.Fill(StageX, StageY, StageW, StageH, new Color(.9f, .88f, .82f, .3f * f));
            }
            else if (transitionKind == TransWeave)
            {
                float bx = WeaveX(p);
                Ui.Fill(bx, 0, 260, StageH, Ui.Hex(0x121817));
                for (int y = 22; y < StageH; y += 24) Ui.Fill(bx, y, 260, 2, new Color(.25f, .85f, .75f, .55f));
                for (float x = bx + 30; x < bx + 260; x += 34) Ui.Fill(x, 0, 4, StageH, new Color(.16f, .23f, .22f, .9f));
                Ui.Fill(bx + 260, 0, 6, StageH, new Color(.25f, .85f, .75f, 1));
            }
            else GateOverlay(p);
        }
        private void GateOverlay(float p)
        {
            float close = p < .3f ? EaseIn(p / .3f) : p < .72f ? 1 : 1 - EaseIn((p - .72f) / .28f);
            float bounce = p > .3f && p < .38f ? Mathf.Sin((p - .3f) / .08f * Mathf.PI) * 8 : 0;
            float h = 452 * close, top = h - 452 - bounce, bottom = StageH - h + bounce;
            Color steel = Ui.Hex(0x262C2D), seam = Ui.Hex(0x0A0C0D), rib = Ui.Hex(0x3A4244), rivet = Ui.Hex(0x4B5457);
            // 上门
            Ui.Fill(0, top, StageW, 452, steel);
            Ui.Fill(0, top + 40, StageW, 2, rib); Ui.Fill(0, top + 150, StageW, 2, rib); Ui.Fill(0, top + 260, StageW, 2, rib);
            for (int i = 0; i < 16; i++) Ui.Disk(40 + i * 101, top + 400, 5, rivet);
            Ui.Hazard(0, top + 418, StageW, 34, 0); Ui.Fill(0, top + 448, StageW, 4, seam);
            // 下门
            Ui.Fill(0, bottom, StageW, 452, steel);
            Ui.Hazard(0, bottom, StageW, 34, 0); Ui.Fill(0, bottom, StageW, 4, seam);
            Ui.Fill(0, bottom + 290, StageW, 2, rib); Ui.Fill(0, bottom + 390, StageW, 2, rib);
            if (close < .98f) return;
            // 合拢时：阀门轮转半圈 + 关卡号
            float spin = Mathf.Clamp01((p - .3f) / .35f) * 180, cx = StageW / 2, cy = StageH / 2;
            Ui.Disk(cx, cy, 86, seam); Ui.Disk(cx, cy, 78, Ui.Hex(0xC9541C)); Ui.Disk(cx, cy, 66, steel);
            for (int k = 0; k < 2; k++)
            {
                float ang = (spin + k * 90) * Mathf.Deg2Rad; Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 72;
                Ui.Stroke(cx - d.x, cy - d.y, cx + d.x, cy + d.y, 12, Ui.Hex(0xC9541C));
            }
            Ui.Disk(cx, cy, 20, Ui.Hex(0xC9541C));
            StageKind sk = KindOf(transitionTo);
            string kind = sk == StageKind.Boss ? "首领" : sk == StageKind.Elite ? "精英" : "";
            Ui.Text(cx - 520, cy - 60, 360, 120, CodeLabel(transitionTo), 110, Ui.Hex(0xE6E1D2), TextAnchor.MiddleRight, true, true);
            Ui.Text(cx + 160, cy - 46, 460, 50, NameLabel(transitionTo), 40, Ui.Hex(0xE6E1D2), TextAnchor.MiddleLeft, true);
            if (kind.Length > 0) Ui.Text(cx + 162, cy + 6, 420, 36, kind + "关", 24, Ui.Hex(0xF29A2E), TextAnchor.MiddleLeft, true);
        }
        private static float EaseIn(float x) { x = Mathf.Clamp01(x); return x * x * x; }

        // 左上角关卡条：滑入 0.15 秒，停 1.5 秒，滑出 0.25 秒。
        private void StageTag()
        {
            float t = clock - stageTagAt;
            if (t < 0 || t > 1.9f) return;
            float slide = t < .15f ? 1 - Mathf.SmoothStep(0, 1, t / .15f) : t > 1.65f ? Mathf.SmoothStep(0, 1, (t - 1.65f) / .25f) : 0;
            int r = CurrentCode; StageKind sk = KindOf(r);
            string sub = stageTagNote.Length > 0 ? stageTagNote : sk == StageKind.Boss ? "首领关" : sk == StageKind.Elite ? "精英关" : "";
            float w = 500, x = -w * slide, y = 120;
            Ui.Fill(x, y, w, 92, Ui.Alpha(Ui.Ink, .92f)); Ui.Fill(x, y, 6, 92, Ui.Orange);
            Ui.Text(x + 24, y + 6, 110, 56, CodeLabel(r), 34, Ui.Panel, TextAnchor.MiddleLeft, true, true);
            Ui.Text(x + 124, y + 6, 240, 56, NameLabel(r), 22, Ui.Panel, TextAnchor.MiddleLeft, true);
            if (sub.Length > 0) Ui.Text(x + 300, y + 6, 180, 56, sub, 15, Ui.Orange, TextAnchor.MiddleRight, true);
            Ui.Text(x + 26, y + 58, w - 40, 26, LineOf(r), 14, Ui.MuteOnInk, TextAnchor.MiddleLeft);
        }

        // v0.16 地面怪和角色用同一套步伐：
        //   · 速度有加速 / 减速（CombatSession 里和角色同为 900/s²）；
        //   · 步伐相位按走过的距离算，步长按体型等比放大（角色：每 24 像素 1 弧度）；
        //   · 用脚走的：落脚时身体往下沉、抬脚时上浮（同角色 |sin| × 7），身体随速度前倾，落脚瞬间轻微压扁；有走路帧就按步伐交替；
        //   · 轮子 / 履带：不颠，只有发动机细震，起步刹车时前后点一下；
        //   · 飞行：悬停起伏，往前飞时机头下压。
        private bool DrawMovingEnemy(Enemy e, float x, float y, float canvas, Color tint, bool walking, float unused, EnemyPoseT p)
        {
            Matrix4x4 previous = GUI.matrix;
            StageArt.Gait gait = StageArt.GaitOf(e);
            float size = canvas / 180f, speed01 = Mathf.Clamp01(e.vel / Mathf.Max(1, e.speed));
            float phase = e.travel / (24f * size), lift = 0, rot = 0, sx = 1, sy = 1; int frame = 0;
            if (gait == StageArt.Gait.Legged)
            {
                // 和角色走路动作同一组数（LayeredRigView Walk）：每步弹起 9 单位（×1.5 像素，按体型缩放）、左右摇 ±3.2°、落地压扁
                float sv = Mathf.Abs(Mathf.Sin(phase)), body = canvas / 210f;
                lift = sv * 9 * 1.5f * body * speed01;
                rot = (-3.2f * Mathf.Sin(phase) - 2) * speed01;              // 摇晃 + 朝前（左）微倾
                float k2 = Mathf.Clamp01((sv - .15f) / .45f); k2 = k2 * k2 * (3 - 2 * k2);
                sy = 1 + (-.035f + .06f * k2) * speed01; sx = 2 - sy;
                if (walking && StageArt.HasWalk(e)) frame = Mathf.Sin(phase) >= 0 ? 1 : 2;
            }
            else if (gait == StageArt.Gait.Wheeled)
            {   // 轮子 / 履带：也跟着节奏小幅颠，幅度只有用脚走的 1/3，外加发动机细震
                float sv = Mathf.Abs(Mathf.Sin(phase));
                lift = (sv * 3 * canvas / 210f + Mathf.Sin(clock * 38 + e.id) * .8f) * speed01;
                rot = (-1.1f * Mathf.Sin(phase) - 1.2f) * speed01;
            }
            else rot = -5 * speed01;                                        // 飞行：悬停起伏在外面算过了，这里只压机头
            float yy = y - lift + p.dy;
            Vector2 pivot = Ui.P(x + p.dx, yy);
            GUI.matrix = previous * Ui.RotateLocal(rot + p.rot, pivot) * Matrix4x4.Translate(pivot) * Matrix4x4.Scale(new Vector3(p.sx * sx, p.sy * sy, 1)) * Matrix4x4.Translate(-pivot);
            bool drawn = StageArt.DrawEnemy(e, StageArt.PoseOf(e), x + p.dx, yy, canvas, tint, frame)
                || ActorView.Enemy(x + p.dx, yy, canvas, e.Art, e.boss, tint);
            GUI.matrix = previous; return drawn;
        }

        // 程序画的行进背景：远景楼块（慢）、中景立柱与街区特征（中）、地面标线（快），随 worldX 滚动。
        // 正式背景图到位后替换这里，滚动系数保持不变。
        private void Backdrop(int region, int district, bool deep)
        {
            if (StageArt.Background(SceneOf(CurrentCode), worldX, StageW, StageH, GroundY)) return;   // v0.16 正式分层背景
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
            int region = model.state.region, code = CurrentCode;
            Ui.ShadowText(40, 30, 200, 56, CodeLabel(code), 48, ink, TextAnchor.MiddleLeft, true, true);
            StageKind kind = KindOf(code);
            string kindText = kind == StageKind.Boss ? " · 首领" : kind == StageKind.Elite ? " · 精英" : "";
            Ui.ShadowText(42, 84, 460, 22, NameLabel(code) + kindText + (model.InEcho ? " · 最高 " + model.state.echoBest : ""), 15, ink, TextAnchor.MiddleLeft, true);
            if (!model.InEcho) for (int i = 0; i < Chapters.StagesPerChapter; i++)
            {
                int r = Chapters.FirstRegion(model.Chapter) + i;
                bool now = r == region, done = r < model.state.unlockedRegion;
                float size = now ? 10 : 8, x = 44 + i * 16 + (now ? -1 : 0);
                Ui.Fill(x - 1, 116 - size / 2 - 1, size + 2, size + 2, new Color(.02f, .02f, .03f, .35f * a));   // 深色衬底：亮背景上也看得见
                Ui.Fill(x, 116 - size / 2, size, size, now ? Ui.Alpha(Ui.Orange, a) : done ? ink : Ui.Alpha(Ui.Rule, a));
            }
            if (model.Farming)
            {   // 墙前回刷：战力涨够了 / 刷够次数就自动回去
                int gain = Mathf.RoundToInt((model.Power() / Mathf.Max(1, model.state.retryPower) - 1) * 100), left = Mathf.Max(1, Balance.RetryFarms - model.state.retryFarms);
                Ui.ShadowText(44 + Chapters.StagesPerChapter * 16 + 8, 106, 420, 20, "回刷补给 · 战力 " + (gain >= 0 ? "+" : "") + gain + "%（+" + Mathf.RoundToInt(Balance.RetryGain * 100) + "% 或再刷 " + left + " 次后再挑战）", 13, Ui.Alpha(Ui.Orange, a), TextAnchor.MiddleLeft, true);
            }
            if (combat.BossEnemy == null)
                Ui.ShadowText(500, 30, 600, 24, "—  第 " + combat.wave + " / " + combat.WaveCount + " 波 · 剩余 " + combat.Alive + "  —", 14, mute, TextAnchor.MiddleCenter, true);
            Ui.ShadowText(1200, 900 - 52, 360, 20, "鼠标移到边缘 · 呼出终端", 12, Ui.Alpha(Ui.Mute, .6f * a), TextAnchor.MiddleRight);
            Ui.Fill(0, 0, 1600, 2, Ui.Alpha(Ui.Ink, .18f * a)); Ui.Fill(0, 898, 1600, 2, Ui.Alpha(Ui.Ink, .18f * a));
        }

        private void ActionHeader()
        {
            int region = model.state.region, code = CurrentCode;
            string name = NameLabel(code);
            float nameWidth = Ui.Width(name, 30, true);
            bool hover = Ui.Hover(128, 16, 190 + nameWidth, 76);
            Ui.Text(128, 12, 120, 80, CodeLabel(code), model.InEcho ? 40 : 52, Ui.Ink, TextAnchor.MiddleLeft, true, true);
            Ui.Text(250, 22, 240, 18, model.InEcho ? "DEEP NET / 零号深网" : "CHAPTER " + model.Chapter + " / 第" + "一二三四五"[model.Chapter - 1] + "章", 13, Ui.Mute, TextAnchor.MiddleLeft, true, true);
            // 机械飞升进度：5 格，一件义体一格
            Ui.FitText(416, 20, 36, 22, "飞升", 13, model.state.implants.Count > 0 ? Ui.Orange : Ui.Mute, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < Catalog.AscensionTotal; i++)
            {   // v0.20 一格 = 一件义体，按等级从下往上填（10 级填满）
                int lv = model.ImplantLevel(i); Ui.Fill(456 + i * 20, 24, 16, 14, Ui.Rule);
                if (lv > 0) { float hgt = 14 * Mathf.Max(.25f, lv / (float)Catalog.ImplantMaxLevel); Ui.Fill(456 + i * 20, 38 - hgt, 16, hgt, Ui.Orange); }
            }
            Ui.Text(250, 42, nameWidth + 10, 40, name, 30, Ui.Ink, TextAnchor.MiddleLeft, true);
            float boxX = 250 + nameWidth + 16;
            Ui.Fill(boxX, 44, 36, 36, hover ? Ui.Ink : Ui.Ground); Ui.Frame(boxX, 44, 36, 36, 2, Ui.Ink);
            Ui.IconChevron(boxX + 18, 62, hover ? Ui.Panel : Ui.Ink);
            if (Ui.Click(128, 16, 190 + nameWidth, 76)) { mapChapter = 0; navigation.Open(GameModal.Map); }

            // 回声层：路线换成「最高层 + 波次 + 协议异常」。
            if (model.InEcho) { EchoHeader(); if (Ui.Button(1196, 30, 176, 48, autoAdvance ? "自动推进 · 开" : "自动推进 · 关", autoAdvance ? 2 : 0, true, 15)) autoAdvance = !autoAdvance; ScrapChip(1568); return; }
            // 路线：一章 7 关。当前关下方的小格 = 本关波次进度。
            float x = 640, middle = 50;
            Ui.Text(x - 66, middle - 12, 56, 24, "ROUTE", 14, Ui.Mute, TextAnchor.MiddleRight, true, true);
            for (int i = 0; i < Chapters.StagesPerChapter; i++)
            {
                int r = Chapters.FirstRegion(model.Chapter) + i;
                StageKind kind = Chapters.Stage(r).kind;
                bool now = r == region, done = r < model.state.unlockedRegion || (kind == StageKind.Boss && model.BossCleared(model.Chapter)), locked = r > model.state.unlockedRegion;
                float size = now ? 48 : 38;
                string label = kind == StageKind.Boss ? "首" : kind == StageKind.Elite ? "精" : (i + 1).ToString();
                bool digit = kind == StageKind.Normal;
                if (now) { Ui.Fill(x, middle - size / 2, size, size, Ui.Orange); Ui.Text(x, middle - size / 2, size, size, label, digit ? 26 : 20, Ui.OnAccent, TextAnchor.MiddleCenter, true, digit); }
                else if (done) { Ui.Fill(x, middle - size / 2, size, size, Ui.Ink); Ui.Text(x, middle - size / 2, size, size, label, digit ? 18 : 15, Ui.Panel, TextAnchor.MiddleCenter, true, digit); }
                else { Ui.Frame(x, middle - size / 2, size, size, 2, locked ? Ui.Rule : Ui.Grey); Ui.Text(x, middle - size / 2, size, size, label, digit ? 18 : 15, locked ? Ui.Rule : Ui.Mute, TextAnchor.MiddleCenter, true, digit); }
                if (now)
                {
                    int waves = combat.WaveCount; float w = 10, gap = 4, total = waves * w + (waves - 1) * gap;
                    for (int k = 0; k < waves; k++)
                        Ui.Fill(x + size / 2 - total / 2 + k * (w + gap), middle + size / 2 + 6, w, 4, k < combat.wave - 1 ? Ui.Ink : k == combat.wave - 1 ? Ui.Orange : Ui.Rule);
                }
                x += size;
                if (i < Chapters.StagesPerChapter - 1) { Ui.Fill(x, middle - 2, 30, 4, done ? Ui.Ink : Ui.Rule); x += 30; }
            }

            if (Ui.Button(1196, 30, 176, 48, autoAdvance ? "自动推进 · 开" : "自动推进 · 关", autoAdvance ? 2 : 0, true, 15)) autoAdvance = !autoAdvance;
            ScrapChip(1568);
        }

        private void EchoHeader()
        {
            float x = 640;
            Ui.Text(x, 18, 200, 20, "最高 " + model.state.echoBest + " 层 · 下个里程碑 " + ((model.state.echoBest / Chapters.MilestoneEvery + 1) * Chapters.MilestoneEvery), 13, Ui.Mute, TextAnchor.MiddleLeft, true);
            int waves = combat.WaveCount;
            for (int k = 0; k < waves; k++) Ui.Fill(x + k * 34, 44, 28, 8, k < combat.wave - 1 ? Ui.Ink : k == combat.wave - 1 ? Ui.Orange : Ui.Rule);
            float ax = x + 90;
            if (model.state.anomalies.Count == 0) Ui.Text(ax, 36, 400, 24, "无协议异常", 13, Ui.Rule, TextAnchor.MiddleLeft);
            foreach (string id in model.state.anomalies)
            {
                Chapters.Anomaly a = Chapters.FindAnomaly(id); if (a == null) continue;
                float w = Ui.Width(a.name, 14, true) + 22;
                Ui.Fill(ax, 34, w, 28, Ui.Ink); Ui.Text(ax, 34, w, 28, a.name, 14, Ui.Orange, TextAnchor.MiddleCenter, true);
                if (Ui.Hover(ax, 34, w, 28)) { Ui.Fill(ax, 66, 300, 28, Ui.Ink); Ui.Text(ax + 10, 66, 290, 28, a.desc, 12, Ui.Panel, TextAnchor.MiddleLeft); }
                ax += w + 8;
            }
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
                Ui.Text(x, y, width, 26, names[i], 13, live[i] ? Ui.OnAccent : Ui.Panel, TextAnchor.MiddleCenter, true);
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
            }
            // v0.16 旧的直线特效（无人机激光线、折线电弧、护盾折线）已删除，改由 OnAbility 事件放正式特效（见 OnAbilityFx）。
        }
        private float gearKickAt = -10;
        /// v0.20 装备光效：每打中一只怪，特装起在怪身上多一下品质色的火花（暴击大一圈）；
        /// 原型武器暴击再加一点顿帧（0.03 秒）和震屏（0.5 秒内最多一次，群怪时不会一直抖，也不会明显拖慢战斗）。只在行动页、没有弹窗时。
        private void GearHit(bool critical, float x)
        {
            if (navigation.Screen != GameScreen.Action || navigation.HasModal) return;
            Enemy near = null; float best = float.MaxValue;
            foreach (Enemy e in combat.enemies) { float d = Mathf.Abs(e.x - x); if (d < best) { best = d; near = e; } }
            float y = near != null ? GroundY + 2 - near.canvas * .45f : GroundY - 70;
            WeaponFx.HitAccent(clock, new Vector2(x - worldX, y), critical);
            if (critical && WeaponFx.GearQuality >= 5 && clock - gearKickAt > .5f) { gearKickAt = clock; Impact.Stop(.03f); Impact.Shake(clock, 3, .14f); }
        }
        private void OnAbilityFx(AbilityEvent a)
        {
            Enemy t = combat.Target; float hx = combat.Movement.HeroScreenX;
            Vector2 aim = t != null ? new Vector2(t.x - worldX - t.canvas * .15f, GroundY + 2 - t.canvas * .45f) : new Vector2(hx + 300, GroundY - 120);
            switch (a.ability)
            {
                case Ability.Drone:   // 无人机：从机身打一发曳光弹
                {
                    Vector2 m = new Vector2(hx + 120, GroundY - 257);
                    WeaponFx.Plain = true; WeaponFx.Fire("gun", clock, m, (aim - m).normalized, aim); WeaponFx.Plain = false;   // 无人机不带武器的品质色
                    break;
                }
                case Ability.Arc:     // 电弧模块：从角色胸口拉一道电弧光束
                {
                    // 以前写死在「脚底往上 120」，Q 版角色矮，那个高度正好是脸。改成跟着骨骼：近侧肩膀往下、往前一点 = 胸口。
                    Vector2 c = LayeredRigView.ArmAnchor != Vector2.zero ? LayeredRigView.ArmAnchor + new Vector2(10, 14) : new Vector2(hx + 40, GroundY - 80);
                    WeaponFx.Plain = true; WeaponFx.Fire("coil", clock, c, (aim - c).normalized, aim); WeaponFx.Plain = false;
                    break;
                }
                case Ability.Dash:
                case Ability.Assault: // 推进器冲刺：目标处一团冲击
                    Sfx("thruster", .45f, .3f);
                    if (t != null) EnemyFx.Burst(clock, t.x, GroundY + 2 - t.canvas * .45f, new Color(1, .6f, .25f), 1.3f);
                    break;
                case Ability.GuardCharge:   // 护盾挡下伤害：角色前方一面弧形护盾闪一下
                    EnemyFx.Shield(clock, combat.Movement.HeroX + 70, GroundY - 110); Sfx("shield", .4f, .4f);
                    break;
                case Ability.GuardRelease:  // 护盾反击：目标处炸开
                    if (t != null) EnemyFx.Burst(clock, t.x, GroundY + 2 - t.canvas * .45f, new Color(.45f, .85f, 1f), 1.6f);
                    break;
            }
        }

        // ---------- 掉落 + 回收无人机（v0.16b）----------
        // 装备从残骸弹出、落地停一下，然后被吸进回收无人机（开局就有，独立于编队），存进仓库。
        // 只是表现：物品在掉落那一刻已经进了仓库。
        private sealed class DropFx { public Item item; public EquipmentBase b; public float from, wx, side; }
        private readonly List<DropFx> dropFx = new List<DropFx>();
        private const float DropPop = .5f, DropFly = .38f, DropFlash = .3f;
        private static float DropRest(Quality q) { return .2f + (int)q * .1f + ((int)q >= 4 ? .5f : (int)q == 3 ? .2f : 0); }   // 11-23 军规起多停一会，给光柱时间
        /// 11-23 军规起掉落时一道品质色光柱（机密、原型更高更亮，原型外面再一圈）。strength 0–1 控制淡入淡出。光效「关」不画。
        private void DropBeam(float gx, float top, Quality quality, float strength)
        {
            int q = (int)quality; if (q < 3 || model.state.glowMode == 0 || strength <= 0) return;
            Color qc = QualityVisual.Colors[q]; float hgt = q >= 5 ? 420 : q == 4 ? 340 : 220, k = strength * (model.state.glowMode == 1 ? .6f : 1);
            for (int i = 0; i < 6; i++)
            {   // 从下往上分 6 段淡出
                float y1 = top - hgt * i / 6f, seg = hgt / 6f, fade = 1 - i / 6f;
                Ui.Fill(gx - 18, y1 - seg, 36, seg, Ui.Alpha(qc, .10f * fade * k));
                Ui.Fill(gx - 7, y1 - seg, 14, seg, Ui.Alpha(qc, .28f * fade * k));
                Ui.Fill(gx - 2, y1 - seg, 4, seg, Ui.Alpha(Color.white, .6f * fade * k));
                if (q >= 5) Ui.Fill(gx - 34, y1 - seg, 68, seg, Ui.Alpha(qc, .06f * fade * k));
            }
        }
        private float droneGulp = -10;   // 最近一次吸进东西的时刻：无人机鼓一下、灯闪一下
        private void SpawnDrop(Item item)
        {
            EquipmentBase b = Catalog.Find(item.baseId); if (b == null) return;
            if (dropFx.Count >= 12) dropFx.RemoveAt(0);
            dropFx.Add(new DropFx { item = item, b = b, from = clock, wx = combat.lastKillX, side = (dropFx.Count % 2 == 0 ? 1 : -1) * (20 + (dropFx.Count % 4) * 14) });
        }
        private float DropScreenX(float wx) { return combat.MovementEnabled ? wx - worldX : EnemyX - (worldX - wx); }
        private void DropEffect(float heroX)
        {
            const float W = 44, H = 48;
            Vector2 drone = new Vector2(heroX - 92, GroundY - 222 + Mathf.Sin(clock * 2.4f) * 6);
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
                    DropBeam(gx, GroundY, d.item.quality, u * u);
                    Ui.ItemTile(x - W / 2, y, W, H, d.b, d.item.quality, true, false, 3, false);
                }
                else if (t < DropPop + rest)
                {   // 落地停一下：轻微上下浮动 + 品质色底光
                    float u = (t - DropPop) / rest, bob = Mathf.Sin(u * Mathf.PI) * 5;
                    Ui.Fill(gx - 18, GroundY - 3, 36, 3, Ui.Alpha(qc, .55f));
                    DropBeam(gx, GroundY, d.item.quality, Mathf.Clamp01((1 - u) * 3));
                    if (model.state.glowMode > 0 && (int)d.item.quality >= 2)
                        QualityVisual.Halo(Ui.P(gx, groundTop + H / 2 - bob), 46 * Ui.K, qc, model.state.glowMode == 1 ? .25f : .5f);
                    Ui.ItemTile(gx - W / 2, groundTop - bob, W, H, d.b, d.item.quality, true, false, 3, false);
                }
                else if (t < DropPop + rest + DropFly)
                {   // 吸进无人机：先慢后快、边飞边缩小，拖一道品质色尾迹
                    float u = (t - DropPop - rest) / DropFly, e = u * u * u, sc = Mathf.Lerp(1, .25f, e);
                    Vector2 a = new Vector2(gx, groundTop + H / 2), c = a + new Vector2(0, -120), target = drone + new Vector2(0, 30);
                    Vector2 p = Bez(a, c, target, e), q = Bez(a, c, target, Mathf.Max(0, e - .18f));
                    Ui.Stroke(q.x, q.y, p.x, p.y, 5 * sc + 1, Ui.Alpha(qc, .5f));
                    Ui.ItemTile(p.x - W * sc / 2, p.y - H * sc / 2, W * sc, H * sc, d.b, d.item.quality, true, false, 3 * sc, false);
                }
                else if (t < DropPop + rest + DropFly + DropFlash)
                {   // 存进仓库：爪子处一圈品质色闪光
                    if (clock - droneGulp > DropFlash) droneGulp = clock;
                    float u = (t - DropPop - rest - DropFly) / DropFlash;
                    Ui.Disk(drone.x, drone.y + 30, 10 + u * 28, Ui.Alpha(qc, .4f * (1 - u)));
                    Ui.Disk(drone.x, drone.y + 30, 4 + u * 6, Ui.Alpha(Color.white, .8f * (1 - u)));
                }
                else dropFx.RemoveAt(i);
            }
            RecyclerDrone(drone, clock - droneGulp < .25f);
        }

        // 回收无人机：有正式素材（Resources/Art/V16/Drone/recycler.png，朝右）就用素材，否则先用程序画的占位。
        private Texture2D recyclerTex; private bool recyclerTried;
        private void RecyclerDrone(Vector2 p, bool carrying)
        {
            if (!recyclerTried) { recyclerTried = true; recyclerTex = Resources.Load<Texture2D>("Art/V16/Drone/recycler"); }
            float tilt = Mathf.Sin(clock * 3) * 2;
            if (recyclerTex != null)
            {
                float w = carrying ? 118 : 110, h = w * recyclerTex.height / recyclerTex.width;   // 吸进东西时鼓一下
                Matrix4x4 m = GUI.matrix; GUI.matrix = m * Ui.RotateLocal(tilt, Ui.P(p.x, p.y));
                GUI.DrawTexture(Ui.R(p.x - w / 2, p.y - h / 2, w, h), recyclerTex, ScaleMode.StretchToFill, true);
                GUI.matrix = m; return;
            }
            Color body = new Color(.22f, .25f, .27f), edge = new Color(.08f, .09f, .1f), light = new Color(.3f, 1, .88f);
            float spin = Mathf.Abs(Mathf.Sin(clock * 40));
            // 两侧旋翼
            for (int s = -1; s <= 1; s += 2)
            {
                float ax = p.x + s * 34;
                Ui.Stroke(p.x + s * 12, p.y - 6, ax, p.y - 12, 4, edge);
                Ui.Stroke(ax - 22 * spin, p.y - 16, ax + 22 * spin, p.y - 16, 3, new Color(.75f, .8f, .82f, .8f));
            }
            Ui.Disk(p.x, p.y, 17, edge); Ui.Disk(p.x, p.y, 14, body);
            Ui.Fill(p.x - 14, p.y - 2, 28, 4, new Color(1, .45f, .1f));                 // 橙色警示条
            Ui.Disk(p.x + 7, p.y - 4, 4, light); Ui.Disk(p.x + 7, p.y - 4, 7, new Color(light.r, light.g, light.b, .25f));
            // 爪子：抓着东西时收拢
            float open = carrying ? 4 : 10;
            Ui.Stroke(p.x - 4, p.y + 12, p.x - open, p.y + 26, 3, edge); Ui.Stroke(p.x + 4, p.y + 12, p.x + open, p.y + 26, 3, edge);
        }
        private static Vector2 Bez(Vector2 a, Vector2 c, Vector2 b, float t) { float m = 1 - t; return m * m * a + 2 * m * t * c + t * t * b; }

        // ---------- 协议异常三选一：右侧面板，15 秒不选就跳过，不打断挂机 ----------
        private readonly List<string> anomalyOffer = new List<string>(); private float offerAt = -10;
        private void OfferAnomalies()
        {
            anomalyOffer.Clear();
            var pool = new List<string>(); foreach (Chapters.Anomaly a in Chapters.Anomalies) if (!model.state.anomalies.Contains(a.id)) pool.Add(a.id);
            while (pool.Count > 0 && anomalyOffer.Count < 3) { int k = UnityEngine.Random.Range(0, pool.Count); anomalyOffer.Add(pool[k]); pool.RemoveAt(k); }
            offerAt = clock;
        }
        private void AnomalyOffer()
        {
            if (anomalyOffer.Count == 0) return;
            float t = clock - offerAt, left = 15 - t;
            if (left <= 0) { anomalyOffer.Clear(); return; }
            float x = 1600 - 420 - 24, y = 230, w = 420, h = 96 + anomalyOffer.Count * 96;
            Ui.Fill(x, y, w, h, Ui.Alpha(Ui.Ink, .95f)); Ui.Fill(x, y, w, 4, Ui.Orange);
            Ui.Text(x + 20, y + 12, w - 40, 28, "协议异常 · 三选一", 20, Ui.Panel, TextAnchor.MiddleLeft, true);
            Ui.Text(x + 20, y + 40, w - 40, 22, "叠上去更难，掉得更好（最多 3 个）", 13, Ui.MuteOnInk, TextAnchor.MiddleLeft);
            for (int i = 0; i < anomalyOffer.Count; i++)
            {
                Chapters.Anomaly a = Chapters.FindAnomaly(anomalyOffer[i]); float cy = y + 72 + i * 96;
                Ui.Fill(x + 16, cy, w - 32, 84, Ui.Tile2);
                Ui.Text(x + 32, cy + 8, 200, 30, a.name, 20, Ui.Orange, TextAnchor.MiddleLeft, true);
                Ui.Text(x + 32, cy + 40, w - 170, 36, a.desc, 12, Ui.MuteOnInk, TextAnchor.UpperLeft, false, false, true);
                if (Ui.Button(x + w - 126, cy + 20, 94, 44, "接入", 1, true, 15))
                { model.state.anomalies.Add(a.id); anomalyOffer.Clear(); Save(); Notify("协议异常接入：" + a.name, 2.5f); return; }
            }
            Ui.Text(x + 20, y + h - 26, w - 40, 22, "不选 · " + Mathf.CeilToInt(left) + " 秒后跳过", 12, Ui.MuteOnInk, TextAnchor.MiddleRight);
        }

        // ---------- 义体接入：首次击败章节首领后，在角色身上对应部位做 2.6 秒的接入动画（不暂停战斗） ----------
        // 扫描框从外往里锁定部位 → 几根橙色「线」接进去 → 闪一下 → 部位名标签。看不到也没关系：义体会一直显示在身上，整备页的格子带 NEW。
        private void ImplantInstallFx()
        {
            if (implantShown == null || Event.current.type != EventType.Repaint) return;
            float t = clock - implantAt; if (t < 0 || t > 2.6f) return;
            bool eye = implantShown.id == "eye_r";
            Vector2 c = eye ? LayeredRigView.EyeAnchor : LayeredRigView.ArmAnchor;
            float lockK = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .7f)), a = t > 2.2f ? 1 - (t - 2.2f) / .4f : 1;
            float r = Mathf.Lerp(150, eye ? 26 : 40, lockK), L = 16;
            Color o = Ui.Alpha(Ui.Orange, a);
            // 四角扫描框
            Ui.Fill(c.x - r, c.y - r, L, 3, o); Ui.Fill(c.x - r, c.y - r, 3, L, o);
            Ui.Fill(c.x + r - L, c.y - r, L, 3, o); Ui.Fill(c.x + r - 3, c.y - r, 3, L, o);
            Ui.Fill(c.x - r, c.y + r - 3, L, 3, o); Ui.Fill(c.x - r, c.y + r - L, 3, L, o);
            Ui.Fill(c.x + r - L, c.y + r - 3, L, 3, o); Ui.Fill(c.x + r - 3, c.y + r - L, 3, L, o);
            // 0.7–1.4 秒：四根线从四周接进部位
            if (t > .6f)
            {
                float k = Mathf.Clamp01((t - .6f) / .8f);
                for (int i = 0; i < 4; i++)
                {
                    float ang = (35 + i * 90) * Mathf.Deg2Rad; Vector2 from = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 230;
                    Vector2 to = Vector2.Lerp(from, c, k);
                    Ui.Stroke(from.x, from.y, to.x, to.y, 3, Ui.Alpha(Ui.Orange, .85f * a));
                    Ui.Stroke(from.x, from.y, to.x, to.y, 1, Ui.Alpha(Color.white, .7f * a));
                }
            }
            // 1.4 秒：接通闪光
            if (t > 1.35f && t < 1.9f) { float f = 1 - (t - 1.35f) / .55f; EnemyFx.Glow(c, 90, Ui.Orange, .9f * f); Ui.Disk(c.x, c.y, 18 * f + 4, Ui.Alpha(Color.white, f)); }
            // 部位标签
            if (t > 1.4f)
            {
                float lx = c.x + r + 14, ly = c.y - 34;
                Ui.Fill(lx, ly, 200, 40, Ui.Alpha(Ui.Ink, .92f * a)); Ui.Fill(lx, ly, 4, 40, o);
                Ui.Text(lx + 14, ly, 186, 20, "义体接入 · " + implantShown.part, 12, Ui.Alpha(Ui.Orange, a), TextAnchor.MiddleLeft, true);
                Ui.Text(lx + 14, ly + 18, 186, 22, implantShown.name, 15, Ui.Alpha(Ui.Panel, a), TextAnchor.MiddleLeft, true);
            }
        }

        // ---------- 机械飞升：击败首领后装上义体的提示（约 4 秒，不挡操作） ----------
        private ImplantDef implantShown; private float implantAt = -10;
        private void AscensionBanner()
        {
            if (implantShown == null) return;
            float t = clock - implantAt - 2.4f; if (t < 0 || t > 4.2f) return;   // 等身上的接入动画放完再出横幅
            float a = t < .25f ? t / .25f : t > 3.7f ? 1 - (t - 3.7f) / .5f : 1, open = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .3f));
            float w = 760, h = 190 * open, x = StageW / 2 - w / 2, y = 250 - h / 2 + 95;
            Ui.Fill(x, y, w, h, Ui.Alpha(Ui.Ink, .94f * a)); Ui.Fill(x, y, w, 4, Ui.Alpha(Ui.Orange, a)); Ui.Fill(x, y + h - 4, w, 4, Ui.Alpha(Ui.Orange, a));
            if (open < .95f) return;
            int have = model.state.implants.Count;
            Ui.Text(x + 40, y + 20, 400, 26, "机械飞升  " + have + " / " + Catalog.AscensionTotal, 18, Ui.Alpha(Ui.Orange, a), TextAnchor.MiddleLeft, true);
            for (int i = 0; i < Catalog.AscensionTotal; i++) Ui.Fill(x + w - 40 - (Catalog.AscensionTotal - i) * 34, y + 26, 26, 14, Ui.Alpha(i < have ? Ui.Orange : Ui.Steel, a));
            Ui.Text(x + 40, y + 56, w - 80, 56, "装上义体：" + implantShown.name, 40, Ui.Alpha(Ui.Panel, a), TextAnchor.MiddleLeft, true);
            Ui.Text(x + 40, y + 120, w - 80, 48, implantShown.desc + "  身上又多了一根「线」。", 17, Ui.Alpha(Ui.MuteOnInk, a), TextAnchor.MiddleLeft, false, false, true);
        }

        // ---------- v0.20 拿到织网碎片（回声层每过一层、重复打主线首领）：角色头顶冒一个「+N 织网碎片」 ----------
        private int shardsSeen = -1, shardGain; private float shardGainAt = -10;
        private void ShardTicker()
        {
            if (shardsSeen < 0 || model.state.shards < shardsSeen) shardsSeen = model.state.shards;   // 第一次 / 花掉了：只记下来，不冒字
            if (model.state.shards > shardsSeen) { shardGain = model.state.shards - shardsSeen; shardGainAt = clock; shardsSeen = model.state.shards; }
            float age = clock - shardGainAt; if (age > 1.6f || Event.current.type != EventType.Repaint) return;
            float a = age < .15f ? age / .15f : 1 - Mathf.Clamp01((age - 1.1f) / .5f);
            float cx = combat.Movement.HeroScreenX, y = GroundY - 372 - age * 26;   // 深色底条 + 橙色左边线（和关卡条同一套样子），亮背景上也看得清
            Ui.Fill(cx - 86, y, 172, 28, Ui.Alpha(Ui.Ink, .82f * a)); Ui.Fill(cx - 86, y, 4, 28, Ui.Alpha(Ui.Orange, a));
            Ui.Text(cx - 82, y + 1, 168, 26, "+" + shardGain + " 织网碎片", 16, Ui.Alpha(Ui.Orange, a), TextAnchor.MiddleCenter, true);
        }

        // ---------- v0.20 机械飞升跨过一个阶段（30 / 60 / 90 / 100%）：横幅停 6 秒，飞升完成停 9 秒 ----------
        private int stageShown; private float stageAt = -10; private bool stagePending;
        private void AscensionStageBanner()
        {
            if (stageShown <= 0) return;
            if (stagePending) { stagePending = false; stageAt = clock + .35f; }   // 在整备页升级跨过阶段：回到行动页后再放
            bool done = stageShown >= Catalog.AscensionStageAt.Length;
            float t = clock - stageAt, life = done ? 9 : 6;
            if (done && t > life + .3f && !model.state.epilogueSeen && !navigation.HasModal) OpenEpilogue();   // v0.20 飞升完成的横幅放完 → 尾声
            if (t < 0 || t > life) return;
            float a = t < .3f ? t / .3f : t > life - .5f ? (life - t) / .5f : 1, open = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .35f));
            float w = done ? 900 : 780, h = (done ? 250 : 200) * open, x = StageW / 2 - w / 2, y = 250 - h / 2 + 95;
            Ui.Fill(x, y, w, h, Ui.Alpha(Ui.Ink, .95f * a)); Ui.Fill(x, y, w, 4, Ui.Alpha(Ui.Orange, a)); Ui.Fill(x, y + h - 4, w, 4, Ui.Alpha(Ui.Orange, a));
            if (open < .95f) return;
            Ui.Text(x + 40, y + 20, w - 80, 26, "机械飞升 " + (model.Ascension * 100).ToString("0") + "%", 18, Ui.Alpha(Ui.Orange, a), TextAnchor.MiddleLeft, true);
            for (int i = 0; i < Catalog.AscensionStageAt.Length; i++) Ui.Fill(x + w - 40 - (Catalog.AscensionStageAt.Length - i) * 44, y + 26, 36, 14, Ui.Alpha(i < stageShown ? Ui.Orange : Ui.Steel, a));
            Ui.Text(x + 40, y + 58, w - 80, 60, Catalog.AscensionStageNames[stageShown - 1], done ? 46 : 40, Ui.Alpha(Ui.Panel, a), TextAnchor.MiddleLeft, true);
            Ui.Text(x + 40, y + 126, w - 80, done ? 90 : 56, Catalog.AscensionStageLines[stageShown - 1], 18, Ui.Alpha(Ui.MuteOnInk, a), TextAnchor.UpperLeft, false, false, true);
            if (done) Ui.Text(x + 40, y + h - 46, w - 80, 26, "获得称号「零号」", 16, Ui.Alpha(Ui.Orange, a), TextAnchor.MiddleLeft, true);
        }
        /// 测试用（桥接 call TestStageBanner 1）：直接放第几阶段的横幅。
        public void TestArcFx() { OnAbilityFx(new AbilityEvent(Ability.Arc)); }
        public void TestStageBanner(int stage) { stageShown = Mathf.Clamp(stage, 1, Catalog.AscensionStageAt.Length); stageAt = clock; }

        // ---------- 武器技能：动作对时、技能名提示、技能特效 ----------
        private string calloutName = ""; private float calloutAt = -10;
        /// 技能的第 i 段命中时刻对到动作的「开火 / 落锤」帧；一段动作约 0.56 秒（射击）/ 0.7 秒（重砸）。
        private static float SkillRigT(SkillDef s, float age, LayeredRigView.Anim anim)
        {
            float impact = anim == LayeredRigView.Anim.Shoot ? LayeredRigView.FireT : .42f, seg = anim == LayeredRigView.Anim.Shoot ? .56f : .7f;
            if (s.hitTimes == null || s.hitTimes.Length == 0) return Mathf.Clamp01(age / s.duration);
            float start = s.hitTimes[0] - impact * seg;
            for (int i = 1; i < s.hitTimes.Length; i++) { float st = s.hitTimes[i] - impact * seg; if (age >= st) start = st; }
            return Mathf.Clamp01((age - start) / seg);
        }
        /// v0.20 技能专属动作的画面节点（只在 Repaint 调）：每一段命中那一帧的特效 + 顿帧 + 震屏；以及蓄能、排气这类按动作进度出现的效果。
        private void SkillFrameFx(LayeredRigView.Anim anim, SkillDef act, float prevT, float t, Enemy target, float targetCanvas, float enemyMid)
        {
            Vector2 muzzle = LayeredRigView.Muzzle, aim = LayeredRigView.MuzzleDir, feet = new Vector2(LayeredRigView.RootPos.x, GroundY);
            Vector2 aimPt = new Vector2(liveEnemyX - targetCanvas * .2f, enemyMid + UnityEngine.Random.Range(-10f, 10f));
            bool Cross(float at) { return prevT < at && t >= at; }
            if (LayeredRigView.SkillCharge > .01f) WeaponFx.DrawCharge(clock, muzzle, aim, LayeredRigView.SkillCharge, ChargeColor(anim));
            HookTrail(anim == LayeredRigView.Anim.HookCombo);
            moveEvents.Clear(); LayeredRigView.MoveEvents(anim, prevT, t, moveEvents);
            foreach (string ev in moveEvents) MoveEvent(ev, muzzle, aim, feet, aimPt, target, act);
            if (act.hitTimes == null || target == null) return;
            int n = act.hitTimes.Length;
            for (int i = 0; i < n; i++)
            {
                if (!Cross(act.hitTimes[i] / act.duration)) continue;
                bool last = i == n - 1;
                switch (anim)
                {
                    case LayeredRigView.Anim.ChainArc:   // 第一枪、收尾一枪是大光束；中间补的是普通电弧
                        if (i == 1) { WeaponFx.Fire("coil", clock, muzzle, aim, aimPt); Impact.Stop(.04f); Impact.Shake(clock, 3, .18f); }
                        else { WeaponFx.CoilBlast(clock, muzzle, aim, aimPt); Impact.Stop(last ? .09f : .06f); Impact.Shake(clock, last ? 8 : 5, .3f); }
                        break;
                    case LayeredRigView.Anim.PointBlank:  // 三枪：上轰 / 空中下轰 / 落地重轰
                        WeaponFx.Fire(LayeredRigView.Kind, clock, muzzle, aim, aimPt);
                        Impact.Stop(last ? .09f : .05f); Impact.Shake(clock, last ? 8 : 5, .26f);
                        if (i == 0 && target != null) Pop(target, 30);
                        break;
                    case LayeredRigView.Anim.NailBurst:   // 每钉一下小顿
                        WeaponFx.Fire(LayeredRigView.Kind, clock, muzzle, aim, aimPt);
                        Impact.Stop(.02f); Impact.Shake(clock, i >= 4 ? 3 : 2, .12f);
                        break;
                    case LayeredRigView.Anim.Overpressure:   // 爆炸在 OnSkillHit（blast）里画；这里只加重震屏
                    case LayeredRigView.Anim.FlareBolt:
                        Impact.Stop(i == 0 ? .08f : .05f); Impact.Shake(clock, i == 0 ? 9 : 5, .35f, true);
                        break;
                    case LayeredRigView.Anim.Thrust:      // 两次冲刺各拉一道穿刺光，最后矛杆砸地
                        if (i < 2) { float dir = i == 0 ? 1 : -1; WeaponFx.Pierce(clock, new Vector2(muzzle.x - dir * 220, muzzle.y), new Vector2(muzzle.x + dir * 30, muzzle.y)); Impact.Stop(.06f); Impact.Shake(clock, 5, .22f); }
                        else { WeaponFx.Slam(clock, new Vector2(muzzle.x, GroundY)); Impact.Stop(.07f); Impact.Shake(clock, 7, .3f, true); }
                        break;
                    case LayeredRigView.Anim.Whirl:       // 空中五切火星，最后劈地
                        if (!last) { EnemyFx.Burst(clock, muzzle.x + worldX, muzzle.y, new Color(1, .7f, .3f), .8f); Impact.Stop(.02f); Impact.Shake(clock, 2, .1f); }
                        else { WeaponFx.Slam(clock, new Vector2(muzzle.x, GroundY)); Impact.Stop(.08f); Impact.Shake(clock, 8, .32f, true); }
                        break;
                    case LayeredRigView.Anim.Combo:       // 迅切连击：火星在动作里画，这里只给顿帧 / 震屏
                        Impact.Stop(last ? .07f : .03f); Impact.Shake(clock, last ? 6 : 2, last ? .26f : .12f, last);
                        if (last && target != null) Pop(target, 28);
                        break;
                    case LayeredRigView.Anim.HookCombo:   // 钩中 / 横扫 / 下砸
                        if (i == 2) { WeaponFx.Slam(clock, new Vector2(aimPt.x, GroundY)); Impact.Stop(.08f); Impact.Shake(clock, 8, .32f, true); }
                        else { Impact.Stop(i == 0 ? .05f : .07f); Impact.Shake(clock, i == 0 ? 4 : 6, .24f); }
                        break;
                    case LayeredRigView.Anim.Quake:      // 砸地 → 撩锤掀地
                        if (i == 0) { WeaponFx.QuakeHit(clock, new Vector2(muzzle.x, GroundY)); Impact.Stop(.11f); Impact.Shake(clock, 11, .42f, true); }
                        else { WeaponFx.Upheave(clock, new Vector2(aimPt.x, GroundY)); Impact.Stop(.08f); Impact.Shake(clock, 7, .3f, true); }
                        break;
                    default:
                        if (LayeredRigView.IsGun(LayeredRigView.Kind)) WeaponFx.Fire(LayeredRigView.Kind, clock, muzzle, aim, aimPt);
                        else EnemyFx.Burst(clock, muzzle.x + worldX, muzzle.y, new Color(1, .62f, .25f), .9f);
                        break;
                }
            }
        }
        private readonly List<string> moveEvents = new List<string>();
        private readonly List<Vector2> hookTrail = new List<Vector2>();
        /// 吊钩连招：吊钩最近几帧的位置连成一道渐隐的弧光，套索 / 横扫才看得出在转。
        private void HookTrail(bool on)
        {
            if (!on) { hookTrail.Clear(); return; }
            hookTrail.Add(LayeredRigView.Muzzle); if (hookTrail.Count > 9) hookTrail.RemoveAt(0);
            for (int i = 1; i < hookTrail.Count; i++)
            {
                float k = i / (float)hookTrail.Count;
                Ui.Stroke(hookTrail[i - 1].x, hookTrail[i - 1].y, hookTrail[i].x, hookTrail[i].y, 2 + 6 * k, new Color(1, .55f, .2f, .55f * k));
            }
        }
        private float lastHeroFeetX;
        private static Color ChargeColor(LayeredRigView.Anim a)
        {
            switch (a)
            {
                case LayeredRigView.Anim.Overpressure: return new Color(1, .55f, .2f);
                case LayeredRigView.Anim.FlareBolt: return new Color(1, .35f, .3f);
                default: return new Color(.45f, 1, .92f);
            }
        }
        /// 动作表里的 !事件 → 画面效果。
        private void MoveEvent(string ev, Vector2 muzzle, Vector2 aim, Vector2 feet, Vector2 aimPt, Enemy target, SkillDef act)
        {
            switch (ev)
            {
                case "fire":    // 抛射类：开火比命中早一点，弹道飞过去刚好在命中那一下落地
                    if (act.animation == "flarebolt") WeaponFx.Lob(clock, muzzle, new Vector2(aimPt.x, GroundY - 30), .18f);
                    else WeaponFx.Fire(LayeredRigView.Kind, clock, muzzle, aim, aimPt);
                    Impact.Shake(clock, 6, .25f); break;
                case "fire2": WeaponFx.Bolt(clock, muzzle, aimPt, new Color(1, .45f, .3f)); Impact.Shake(clock, 3, .15f); break;
                case "steam": WeaponFx.SteamPuff(clock, muzzle); break;
                case "land": WeaponFx.Land(clock, feet); break;
                case "skid": WeaponFx.Skid(clock, feet); break;
                case "residue": WeaponFx.Residue(clock, muzzle, aim); break;
                case "casing": WeaponFx.Casing(clock, muzzle - aim * 30); break;
                case "twinkle": WeaponFx.Twinkle(clock, muzzle); break;
                case "thud": Impact.Shake(clock, 4, .2f, true); WeaponFx.Land(clock, feet); break;
            }
        }
        /// 被重击打中的怪弹一下 / 被电到抖一下（只是画面，不改位置）。首领、重型弹得低。
        private void Pop(Enemy e, float amp) { if (e == null) return; e.popAt = clock; e.popAmp = e.boss ? amp * .25f : e.Heavy ? amp * .5f : amp; }
        private void Zap(Enemy e, float delay) { if (e != null) e.zapAt = clock + delay; }
        private float PopLift(Enemy e) { float k = (clock - e.popAt) / .45f; return k < 0 || k >= 1 ? 0 : e.popAmp * 4 * k * (1 - k); }
        private float ZapJitter(Enemy e) { float k = (clock - e.zapAt) / .32f; return k < 0 || k >= 1 ? 0 : Mathf.Sin(clock * 120 + e.id) * 4 * (1 - k); }
        private void OnSkillCast(SkillDef s) { hookTrail.Clear(); if (s != SkillBook.QuickCut) { calloutName = s.displayName; calloutAt = clock; } }
        private void SkillCallout(float x, float y)
        {
            float t = clock - calloutAt; if (t < 0 || t > 1.1f || calloutName.Length == 0) return;
            float pop = t < .12f ? EaseOutBack(t / .12f) : 1, fade = t > .8f ? 1 - (t - .8f) / .3f : 1;
            float w = Ui.Width(calloutName, 22, true) + 34, h = 34 * pop;
            Ui.Fill(x - w / 2, y - h / 2, w, h, Ui.Alpha(Ui.Ink, .9f * fade));
            Ui.Fill(x - w / 2, y - h / 2, 5, h, Ui.Alpha(Ui.Orange, fade));
            if (pop > .7f) Ui.Text(x - w / 2 + 6, y - 17, w - 6, 34, calloutName, 22, Ui.Alpha(Ui.Orange, fade), TextAnchor.MiddleCenter, true);
        }
        /// v0.20 线圈普攻跳电：一小段拱起来的电弧从第一只怪连到第二只，第二只抖一下。
        private void CoilJumpFx(Enemy a, Enemy b)
        {
            var pts = new List<Vector2> { new Vector2(a.x - worldX, GroundY + 2 - a.canvas * .5f), new Vector2(b.x - worldX + 8, GroundY + 2 - b.canvas * .38f) };
            WeaponFx.Chain(clock, pts); Zap(b, .04f);
        }
        private void OnSkillHit(SkillDef s, int hit, Enemy target, List<Enemy> extra)
        {
            float Mid(Enemy e) { return GroundY + 2 - e.canvas * .45f; }
            switch (s.fx)
            {
                case "chain":   // v0.20 电弧从被光束打中的那只开始，在怪之间一跳一跳（每跳晚 0.05 秒），被电到的抖一下
                {
                    var pts = new List<Vector2> { new Vector2(target.x - worldX, Mid(target)) };
                    Zap(target, 0);
                    for (int i = 0; i < extra.Count; i++)   // 落点上下错开，怪挤在一起时也看得出是一跳一跳
                    {
                        Enemy e = extra[i]; float y = GroundY + 2 - e.canvas * (i % 2 == 0 ? .62f : .32f);
                        pts.Add(new Vector2(e.x - worldX + (i % 2 == 0 ? 10 : -10), y)); Zap(e, .05f + i * .05f);
                    }
                    WeaponFx.Chain(clock, pts);
                    break;
                }
                case "flare":   // 信号爆矢：目标处红色信号弹炸开，周围的怪各闪一下
                    WeaponFx.FlareBurst(clock, new Vector2(target.x - worldX, Mid(target)), hit == 0 ? 1.4f : 1f);
                    foreach (Enemy e in extra) EnemyFx.Burst(clock + .03f, e.x, Mid(e), new Color(1, .45f, .3f), .8f);
                    break;
                case "blast":   // 过压弹：目标处一团大爆炸
                    EnemyFx.Burst(clock, target.x, Mid(target), new Color(1, .55f, .2f), 2.6f);
                    WeaponFx.Slam(clock, new Vector2(target.x - worldX, GroundY));
                    break;
                case "sweep":   // 吊钩横扫：链子甩过的一道弧光（不是锤子的砸地冲击波），扫到的怪各闪一下
                    if (hit == 1) EnemyFx.Sweep(clock - .15f, combat.Movement.HeroX + 30, GroundY - 70, 300);   // v0.20 只有横扫那一下画弧光
                    EnemyFx.Burst(clock, target.x, Mid(target), new Color(1, .62f, .25f), 1.4f);
                    foreach (Enemy e in extra) EnemyFx.Burst(clock + .03f, e.x, Mid(e), new Color(1, .62f, .25f), .9f);
                    break;
                case "quake":   // 震地：冲击波一路传过去
                    EnemyFx.Shock(clock, combat.Movement.HeroX + 120, GroundY, true);
                    Pop(target, hit == 0 ? 34 : 64); foreach (Enemy e in extra) Pop(e, hit == 0 ? 26 : 50);   // 被震起来；第二下撩锤掀得更高
                    break;
                case "burst":   // 贴脸轰击：目标和周围各炸一下
                    EnemyFx.Burst(clock, target.x, Mid(target), new Color(1, .8f, .45f), 1.5f);
                    foreach (Enemy e in extra) EnemyFx.Burst(clock, e.x, Mid(e), new Color(1, .8f, .45f), .9f);
                    break;
            }
        }

        // ---------- 怪物攻击模组的画面：起手姿态、预警、蓄能球、弹道、命中特效 ----------
        private struct EnemyPoseT { public float dx, dy, rot, sx, sy, flash; }
        private EnemyPoseT EnemyPose(Enemy e)
        {
            var p = new EnemyPoseT { sx = 1, sy = 1 };
            float w = e.Wind01, a = e.sinceStrike, amp = e.boss ? 1.4f : 1;
            switch (e.move)
            {
                case EnemyMove.Lunge:   // 扑咬：往后一缩 → 猛扑出去
                    if (w > 0) { p.dx = 10 * w; p.rot = 7 * w; p.sy = 1 - .08f * w; p.sx = 1 + .05f * w; }
                    else if (a < .3f) { float u = 1 - a / .3f; p.dx = -46 * u * u; p.rot = -10 * u; p.sx = 1 + .06f * u; }
                    break;
                case EnemyMove.Bolt:
                case EnemyMove.Beacon:  // 蓄能：微微后仰、抖动 → 开火后坐
                    if (w > 0) { p.rot = 4 * w; p.dx = 4 * w + Mathf.Sin(clock * 70) * 1.5f * w; }
                    else if (a < .22f) { float u = 1 - a / .22f; p.dx = 14 * u; p.rot = 5 * u; }
                    break;
                default:                // 砸地类：抬起、后仰、发红 → 重重砸下
                    if (w > 0)
                    {
                        p.dy = -16 * w * amp; p.rot = 9 * w; p.sy = 1 + .08f * w; p.sx = 1 - .04f * w;
                        if (w > .6f) { p.dx = Mathf.Sin(clock * 60) * (e.special ? 5 : 2); p.flash = (.5f + .5f * Mathf.Sin(clock * 30)) * (w - .6f) / .4f * .7f; }
                    }
                    else if (a < .35f) { float u = 1 - a / .35f; p.dx = -22 * u * amp; p.rot = -12 * u; p.sy = 1 - .15f * u; p.sx = 1 + .1f * u; }
                    break;
            }
            return p;
        }

        private void EnemyTelegraph(Enemy e, float x, float feetY)
        {
            float w = e.Wind01; if (w <= 0) return;
            float top = feetY - e.canvas * .82f;
            if (e.Heavy)
            {   // 重击预警：角色脚下的红色警示区 + 怪头顶的感叹号
                float hx = combat.Movement.HeroScreenX, half = e.special ? 220 : e.boss ? 120 : 80;
                Color c = e.special ? new Color(.3f, .7f, .9f) : EnemyFx.Warn;
                Ui.Fill(hx - half, GroundY - 5, half * 2 * w, 6, EnemyFx.A(c, .35f + .45f * w));
                for (int i = 0; i < 4; i++) { float cx = hx - half + (i + .5f) * half / 2; Ui.Stroke(cx - 10, GroundY - 18, cx, GroundY - 8, 3, EnemyFx.A(c, w)); Ui.Stroke(cx, GroundY - 8, cx + 10, GroundY - 18, 3, EnemyFx.A(c, w)); }
                if (e.elite || e.boss || e.special)
                    Ui.Text(x - 40, top - 76 - 6 * Mathf.Sin(clock * 12), 80, 50, e.special ? "!!" : "!", e.special ? 44 : 36, c, TextAnchor.MiddleCenter, true);
            }
            else if (e.move == EnemyMove.Bolt || e.move == EnemyMove.Beacon)
            {   // 蓄能球在枪口处变大
                Color c = e.move == EnemyMove.Beacon ? EnemyFx.Beacon : EnemyFx.Bolt;
                Vector2 m = new Vector2(x - e.canvas * .32f, feetY - e.canvas * .45f);
                EnemyFx.Glow(m, 30 + 30 * w, c, .6f * w);
                Ui.Disk(m.x, m.y, 4 + 9 * w, EnemyFx.A(c, .9f));
                Ui.Disk(m.x, m.y, 2 + 4 * w, Color.white);
            }
            if (e.hasteLeft > 0 && !e.boss)   // 被信号塔加速：头顶两道紫色箭头
                for (int i = 0; i < 2; i++) { float yy = top - 30 - i * 9 - (clock * 20 % 9); Ui.Stroke(x - 8, yy + 6, x, yy, 3, EnemyFx.Beacon); Ui.Stroke(x, yy, x + 8, yy + 6, 3, EnemyFx.Beacon); }
        }

        private void DrawEnemyShots()
        {
            Vector2 hero = new Vector2(combat.Movement.HeroScreenX, GroundY - 110);
            foreach (EnemyShot shot in combat.shots)
            {
                float u = Mathf.Clamp01((combat.Elapsed - shot.born) / shot.dur);
                Vector2 a = new Vector2(shot.x0 - worldX, GroundY + 2 - shot.from.canvas * .45f);
                Color c = shot.from.move == EnemyMove.Beacon ? EnemyFx.Beacon : EnemyFx.Bolt;
                Vector2 p = Vector2.Lerp(a, hero, u) + new Vector2(0, -26 * Mathf.Sin(Mathf.PI * u));
                for (int j = 1; j <= 5; j++)
                {
                    float uu = Mathf.Max(0, u - j * .045f);
                    Vector2 q = Vector2.Lerp(a, hero, uu) + new Vector2(0, -26 * Mathf.Sin(Mathf.PI * uu));
                    Ui.Disk(q.x, q.y, 8 - j, EnemyFx.A(c, .55f - j * .09f));
                }
                EnemyFx.Glow(p, 30, c, .6f);
                Ui.Disk(p.x, p.y, 9, Ui.Ink); Ui.Disk(p.x, p.y, 7, c); Ui.Disk(p.x + 2, p.y - 2, 2.5f, Color.white);
            }
        }

        private void OnEnemyAct(EnemyAct act)
        {
            Enemy e = act.e; float heroY = GroundY - 110, mid = GroundY + 2 - e.canvas * .45f;
            switch (act.kind)
            {
                case EnemyActKind.Strike:
                    if (e.move == EnemyMove.Lunge) EnemyFx.Claw(clock, act.x, heroY);
                    else if (e.move == EnemyMove.Bolt || e.move == EnemyMove.Beacon) EnemyFx.Muzzle(clock, act.x, mid, e.move == EnemyMove.Beacon ? EnemyFx.Beacon : EnemyFx.Bolt);
                    else EnemyFx.Shock(clock, act.x, GroundY, e.boss || e.elite);
                    break;
                case EnemyActKind.ShotHit: EnemyFx.Burst(clock, act.x, heroY, e.move == EnemyMove.Beacon ? EnemyFx.Beacon : EnemyFx.Bolt); break;
                case EnemyActKind.Flood:
                    if (e.skin == 1) EnemyFx.Crate(clock, act.x, GroundY);
                    else if (e.skin >= 2) { EnemyFx.Pulse(clock, e.x, mid); EnemyFx.Burst(clock, act.x, heroY, EnemyFx.Beacon, 1.6f); EnemyFx.Shock(clock, act.x, GroundY, true); }   // 断讯脉冲
                    else { EnemyFx.Flood(clock, e.x, act.x, GroundY); EnemyFx.Shock(clock, act.x, GroundY, true); }
                    break;
                case EnemyActKind.Windup:
                    if (e.boss && e.special && e.skin >= 1) EnemyFx.Target(clock, combat.Movement.HeroX, GroundY, e.windDur);
                    break;
                case EnemyActKind.Deploy: EnemyFx.Burst(clock, e.x - 40, mid + 40, EnemyFx.Bolt, .9f); break;
                case EnemyActKind.Pulse: EnemyFx.Pulse(clock, e.x, mid); break;
                case EnemyActKind.Enrage: EnemyFx.Roar(clock, e.x, mid); break;
            }
        }

        private void DrawFloaters(float feetX)
        {
            for (int i = 0; i < floaters.Count; i++)
            {
                Floater f = floaters[i]; float age = (clock - f.born) / .9f, rise = 54 * age, alpha = age < .7f ? 1 : Mathf.Clamp01(1 - (age - .7f) / .3f);
                if (f.onEnemy)
                {
                    float size = f.critical ? 42 : 28, x = f.x - worldX - 30 + f.drift, y = GroundY - 230 - rise - f.lift;
                    string value = f.value.ToString("F0");
                    if (f.critical) { Ui.Fill(x - 56, y + 20, 48, 24, Ui.Alpha(Ui.Orange, alpha)); Ui.Text(x - 56, y + 20, 48, 24, "暴击", 14, Ui.Alpha(Ui.OnAccent, alpha), TextAnchor.MiddleCenter, true); }
                    Ui.ShadowText(x, y, 220, 64, value, size, Ui.Alpha(Ui.Ink, alpha), TextAnchor.MiddleLeft, true, true, .7f);
                }
                else Ui.ShadowText(feetX + 56 + f.drift, GroundY - 250 - rise, 160, 44, "-" + f.value.ToString("F0"), 32, Ui.Alpha(Ui.OrangeInk, alpha), TextAnchor.MiddleLeft, true, true, .7f);
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
            for (int i = 0; i < Catalog.SlotCount; i++)
            {
                Item item = model.Equipped((Slot)i); float x = 144 + i * 76;
                if (item != null) Ui.ItemTile(x, y + 50, 68, 68, Catalog.Find(item.baseId), item.quality, true, false, 5, false);
                else { Ui.Fill(x, y + 50, 68, 68, Ui.Rule); Ui.Text(x, y + 50, 68, 68, Catalog.SlotNames[i], 13, Ui.Mute, TextAnchor.MiddleCenter); }
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
                string note = model.TargetActive ? "精英 / 首领保底 " + misses + " / " + Balance.TargetBossGuarantee : "产地 · " + Catalog.SourceStages(target.id);
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
            Color pauseFill = paused ? Ui.Orange : hotPause ? Ui.Ink : Ui.Panel, pauseText = paused ? Ui.OnAccent : hotPause ? Ui.Panel : Ui.Ink;
            Ui.Fill(1340, y, 228, 60, pauseFill); Ui.Frame(1340, y, 228, 60, 2, Ui.Ink);
            if (!paused) Ui.IconPause(1340 + 78, y + 30, pauseText);
            Ui.Text(paused ? 1340 : 1340 + 18, y, 228, 60, paused ? "继续行动" : "暂停", 18, pauseText, TextAnchor.MiddleCenter, true);
            if (Ui.Click(1340, y, 228, 60)) paused = !paused;
            // v0.16 倍速：开局只有 1× / 2×；打通第一章（击败闸门看守）后开放 3×。4× 取消。
            int[] speeds = { 1, 2, 3 };
            bool fastOpen = model.state.bosses > 0;
            if (!fastOpen && speed > 2) speed = 2;
            for (int i = 0; i < 3; i++)
            {
                bool locked = speeds[i] == 3 && !fastOpen;
                float x = 1340 + i * 76; bool on = speed == speeds[i], hot = !locked && Ui.Hover(x, y + 72, 76, 60);
                Ui.Fill(x, y + 72, 76, 60, on ? Ui.Ink : hot ? Ui.Rule : Ui.Panel);
                Ui.Text(x, y + 72, 76, locked ? 40 : 60, speeds[i] + "×", locked ? 20 : 24, locked ? Ui.Rule : on ? Ui.Panel : Ui.Ink, TextAnchor.MiddleCenter, true, true);
                if (locked) Ui.Text(x, y + 104, 76, 22, "通关后", 11, Ui.Rule, TextAnchor.MiddleCenter);
                if (!locked && Ui.Click(x, y + 72, 76, 60)) speed = speeds[i];
            }
            Ui.Frame(1340, y + 72, 228, 60, 2, Ui.Ink);
        }
    }
}
