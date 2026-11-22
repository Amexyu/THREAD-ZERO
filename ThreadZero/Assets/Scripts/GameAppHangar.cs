using System;
using UnityEngine;

namespace ThreadZero
{
    /// v0.21 机库（企划 9A 第四步）：工坊第三个页签 + 战斗画面里上场的两台机器人。
    /// 机器人是被改造过来的敌人：用那种敌人的图，翻过来朝右。回收无人机就是一直跟着主角的那台，上场后多了回血。
    public sealed partial class GameApp
    {
        // 每台机器人的图纸从哪种怪身上掉（只是给玩家看的提示，实际掉落规则见 GameModel.BlueprintDrop）
        private static readonly string[] RobotMob = { "", "叉臂装卸机", "轨道炮车", "屏蔽甲兵", "折光炮台", "镜面无人机", "母线炮", "协议守卫" };
        private static readonly string[] EnemyRoleNames = { "冲锋", "重型", "远程" };

        // ---------- 战斗画面 ----------
        /// 站位（相对主角脚底）：x 偏移、脚底高度偏移、画多大。前排站在主角前面靠里一点，后排在身后，支援机飞在身后上方。
        private static Vector3 RobotSpot(int role)
        {
            return role == 0 ? new Vector3(86, -12, 140) : role == 1 ? new Vector3(-160, -10, 140) : new Vector3(-208, -206, 104);
        }
        private Vector2 RobotFeet(int slot, float heroFeetX)
        {
            int r = model.state.deployed[slot]; RobotDef d = Catalog.Robots[r]; Vector3 sp = RobotSpot(d.role);
            float bob = d.role == 2 ? Mathf.Sin(clock * 2.6f + slot) * 6 : 0;
            return new Vector2(heroFeetX + sp.x, GroundY + 2 + sp.y + bob);
        }
        private void DrawRobots(float heroFeetX, float alpha, bool down, bool walking, Stats stats)
        {
            if (!model.HangarUnlocked || alpha <= 0) return;
            for (int slot = 0; slot < 2; slot++)
            {
                int r = model.state.deployed[slot]; if (r < 0) continue;
                RobotDef d = Catalog.Robots[r]; if (d.art == null) continue;   // 回收无人机一直都在，不另画
                Vector3 sp = RobotSpot(d.role); Vector2 f = RobotFeet(slot, heroFeetX);
                if (walking && d.role != 2) f.y -= Mathf.Abs(Mathf.Sin(combat.Movement.HeroTravel / 22f + slot * 1.3f)) * 4;
                bool broken = d.role == 0 && combat.tankDown > 0;
                Color tint = broken ? new Color(.42f, .44f, .47f, .5f) : down ? new Color(.62f, .66f, .7f) : Color.white;
                if (!broken && d.role == 0 && combat.tankFlash > 0) tint = Color.Lerp(tint, new Color(1, .72f, .64f), combat.tankFlash / .2f);
                tint.a *= alpha;
                if (d.role != 2) Ui.Fill(f.x - sp.z * .24f, GroundY - 3 + sp.y * .2f, sp.z * .48f, 4, Ui.Alpha(Ui.Ink, .14f * alpha));   // 脚下的影子
                StageArt.Pose pose = d.role == 1 && combat.robotFlash[slot] > 0 ? StageArt.Pose.Strike : StageArt.Pose.Idle;
                if (broken)
                {   // 被打坏：歪倒、变灰
                    Matrix4x4 m = GUI.matrix; GUIUtility.RotateAroundPivot(-8, Ui.P(f.x, f.y));
                    StageArt.DrawKey(d.art, StageArt.Pose.Idle, f.x, f.y + 4, sp.z, tint, true); GUI.matrix = m;
                }
                else StageArt.DrawKey(d.art, pose, f.x, f.y, sp.z, tint, true, new Color(.3f, .95f, .85f, .8f * alpha));   // 青色描边 = 自己人
                float tankMax = d.role == 0 ? combat.TankMax(stats) : 0;
                if (d.role == 0 && alpha > .5f && (broken || combat.tankHp < tankMax - .5f))
                {   // 前排的血条（满血时不画，免得和主角身上的东西叠在一起）；被打坏时是回来的进度
                    float top = f.y - sp.z * .82f, w = 72;
                    Ui.Fill(f.x - w / 2 - 2, top - 2, w + 4, 9, Ui.Alpha(Ui.Ink, .6f));
                    if (broken) Ui.Fill(f.x - w / 2, top, w * Mathf.Clamp01(1 - combat.tankDown / Balance.TankRespawn), 5, new Color(.6f, .62f, .64f));
                    else Ui.Fill(f.x - w / 2, top, w * Mathf.Clamp01(combat.tankHp / Mathf.Max(1, tankMax)), 5, new Color(.45f, .85f, 1f));
                }
            }
        }
        private void OnRobotShot(int slot, Enemy t)
        {
            int r = model.state.deployed[slot]; if (r < 0 || t == null || navigation.Screen != GameScreen.Action) return;
            RobotDef d = Catalog.Robots[r]; Vector3 sp = RobotSpot(d.role);
            Vector2 f = RobotFeet(slot, combat.Movement.HeroScreenX);
            Vector2 m = new Vector2(f.x + sp.z * .3f, f.y - sp.z * .5f);
            Vector2 aim = new Vector2(t.x - worldX - t.canvas * .15f, GroundY + 2 - t.canvas * .45f + UnityEngine.Random.Range(-10f, 10f));
            WeaponFx.Plain = true; WeaponFx.Fire(Catalog.RobotFx(r), clock, m, (aim - m).normalized, aim); WeaponFx.Plain = false;   // 机器人不带武器的品质色
        }
        private void OnRobotSupport(int slot)
        {
            int r = model.state.deployed[slot]; if (r < 0 || navigation.Screen != GameScreen.Action) return;
            float hx = combat.Movement.HeroX;
            if (Catalog.Robots[r].id == "r0")
            {   // 回收无人机回血：无人机和主角胸口各一团绿光
                EnemyFx.Burst(clock, hx - 92, GroundY - 196, new Color(.45f, 1f, .6f), .8f);
                EnemyFx.Burst(clock, hx, GroundY - 120, new Color(.45f, 1f, .6f), 1.2f);
            }
            else
            {   // 镜面无人机给护盾
                EnemyFx.Burst(clock, hx - 208, GroundY - 250, new Color(.55f, .85f, 1f), .8f);
                EnemyFx.Shield(clock, hx + 70, GroundY - 110);
            }
        }

        // ---------- 工坊 · 机库页签 ----------
        private void HangarPage()
        {
            Stats s = model.Total();
            for (int slot = 0; slot < 2; slot++)
            {
                float x = 128 + slot * 728, y = 156, w = 712, h = 148;
                int r = model.state.deployed[slot];
                Ui.Fill(x, y, w, h, Ui.Ink); Ui.Fill(x, y, 6, h, Ui.Orange);
                Ui.Text(x + 22, y + 10, 300, 18, "上场 " + (slot + 1), 12, Ui.MuteOnInk, TextAnchor.MiddleLeft);
                if (r < 0) { Ui.Text(x, y, w, h, "空位 · 在下面选一台「上场」", 16, Ui.MuteOnInk, TextAnchor.MiddleCenter); continue; }
                RobotDef d = Catalog.Robots[r];
                RobotPortrait(r, x + 104, y + h - 8, 132, Color.white);
                Ui.Text(x + 196, y + 26, 360, 34, d.name, 22, Ui.Panel, TextAnchor.MiddleLeft, true);
                Ui.Text(x + w - 200, y + 22, 180, 40, "Lv." + model.state.robotLevels[r], 28, Ui.Orange, TextAnchor.MiddleRight, true, true);
                Ui.Text(x + 196, y + 62, w - 216, 20, Catalog.RobotRoles[d.role] + " · " + d.desc, 13, Ui.MuteOnInk, TextAnchor.MiddleLeft);
                Ui.Text(x + 196, y + 84, w - 216, 20, RobotStatText(r, s), 13, Ui.Panel, TextAnchor.MiddleLeft);
                if (Ui.Button(x + w - 140, y + h - 50, 120, 36, "撤下", 0, true, 13)) { model.Undeploy(r); combat.RobotsChanged(); Save(); }
            }
            Ui.Text(128, 312, 1440, 22, "最多上场两台，同一分工（前排 / 后排 / 支援）只能上一台 · 打对应的怪掉图纸碎片，首领必掉 3 片 · 集满 10 片组装 · 升级每级 +5%，最高 " + Catalog.RobotMaxLevel + " 级", 12, Ui.Mute, TextAnchor.MiddleLeft);
            for (int i = 0; i < Catalog.RobotCount; i++)
                RobotCard(i, 128 + (i % 4) * 364, 342 + (i / 4) * 268, 352, 256, s);
        }
        private void RobotCard(int i, float x, float y, float w, float h, Stats s)
        {
            RobotDef d = Catalog.Robots[i];
            bool open = model.RobotOpen(i), built = model.RobotBuilt(i), on = model.IsDeployed(i);
            Ui.Fill(x, y, w, h, on ? Ui.Ink : Ui.Panel); Ui.Frame(x, y, w, h, on ? 0 : 1, on ? Ui.Ink : Ui.Rule);
            if (on) Ui.Fill(x, y, 6, h, Ui.Orange);
            Color ink = on ? Ui.Panel : open ? Ui.Ink : Ui.Grey, mute = on ? Ui.MuteOnInk : Ui.Mute;
            RobotPortrait(i, x + 74, y + 142, 124, built ? Color.white : open ? new Color(.25f, .27f, .3f, .55f) : new Color(.2f, .2f, .22f, .22f));
            Ui.Text(x + 146, y + 14, w - 160, 26, d.name, 17, ink, TextAnchor.MiddleLeft, true);
            Ui.Text(x + 146, y + 40, w - 160, 18, Catalog.RobotRoles[d.role] + (built ? " · Lv." + model.state.robotLevels[i] : " · 第" + Catalog.ChapterNumber(d.chapter) + "章"), 12, on ? Ui.Orange : mute, TextAnchor.MiddleLeft, built);
            Ui.Text(x + 146, y + 60, w - 160, 70, d.desc, 12, on ? Ui.Panel : open ? Ui.Body : Ui.Grey, TextAnchor.UpperLeft);
            if (!open)
            {
                Ui.Text(x + 16, y + h - 46, w - 32, 34, "第" + Catalog.ChapterNumber(d.chapter) + "章开放", 14, Ui.Grey, TextAnchor.MiddleCenter);
                return;
            }
            if (built)
            {
                Ui.Text(x + 16, y + 152, w - 32, 36, RobotStatText(i, s), 12, on ? Ui.Panel : Ui.Body, TextAnchor.UpperLeft);
                int lv = model.state.robotLevels[i]; bool max = lv >= Catalog.RobotMaxLevel;
                if (Ui.Button(x + 16, y + h - 52, (w - 40) * .56f, 38, max ? "已满级" : "升级 · 零件 " + Catalog.RobotUpgradeScrap(lv), 1, model.CanUpgradeRobot(i), 13) && model.UpgradeRobot(i))
                { combat.RobotsChanged(); Save(); Play(equipSound); }
                float bx = x + 24 + (w - 40) * .56f, bw = (w - 40) * .44f;
                if (on) { if (Ui.Button(bx, y + h - 52, bw, 38, "撤下", 0, true, 13)) { model.Undeploy(i); combat.RobotsChanged(); Save(); } }
                else if (Ui.Button(bx, y + h - 52, bw, 38, model.DeployedOfRole(d.role) >= 0 ? "换上场" : "上场", 3, true, 13) && model.Deploy(i))
                { combat.RobotsChanged(); Save(); Play(equipSound); Notify(d.name + " 上场", 3); }
                return;
            }
            // 还没组装：图纸进度 + 掉落提示 + 组装
            int have = model.state.blueprints[i];
            Ui.Text(x + 16, y + 152, w - 32, 18, d.art == null ? "机库开放时送的" : "打「" + RobotMob[i] + "」等" + EnemyRoleNames[(int)d.source] + "怪、首领掉落", 12, Ui.Mute, TextAnchor.MiddleLeft);
            Ui.Fill(x + 16, y + 176, w - 32, 10, Ui.Rule); Ui.Fill(x + 16, y + 176, (w - 32) * have / (float)Catalog.BlueprintNeed, 10, Ui.Orange);
            Ui.Text(x + 16, y + 186, w - 32, 18, "图纸碎片 " + have + " / " + Catalog.BlueprintNeed, 12, Ui.Body, TextAnchor.MiddleRight, true, true);
            bool can = model.CanAssemble(i);
            string label = have < Catalog.BlueprintNeed ? "图纸不够" : "组装 · 零件 " + Catalog.RobotAssembleScrap(i) + " · 核心 " + Catalog.RobotAssembleCores;
            if (Ui.Button(x + 16, y + h - 52, w - 32, 38, label, can ? 1 : 0, can, 13) && model.Assemble(i))
            { combat.RobotsChanged(); Save(); Play(rareSound); Notify("组装完成：" + d.name + (model.IsDeployed(i) ? "，已上场" : "，去上场吧"), 5); }
        }
        private void RobotPortrait(int i, float feetX, float feetY, float canvas, Color tint)
        {
            RobotDef d = Catalog.Robots[i];
            if (d.art == null) { Color old = GUI.color; GUI.color = tint; RecyclerDrone(new Vector2(feetX, feetY - canvas * .45f), false); GUI.color = old; return; }
            StageArt.DrawKey(d.art, StageArt.Pose.Idle, feetX, feetY, canvas, tint, true);
        }
        private string RobotStatText(int i, Stats s)
        {
            RobotDef d = Catalog.Robots[i]; float f = model.RobotFactor(i);
            switch (d.role)
            {
                case 0: return "自身生命 " + (s.health * d.power * f).ToString("0") + "（主角生命 × " + (d.power * f * 100).ToString("0") + "%）";
                case 1: return "每发伤害 = 攻击 × " + (d.power * f * 100).ToString("0") + "%（现在约 " + (s.attack * d.power * f).ToString("0") + "）";
                default: return d.id == "r0" ? "每次回复 " + (Math.Min(.06f, d.power * f) * 100).ToString("0.#") + "% 生命" : "每次护盾 " + (Math.Min(.12f, d.power * f) * 100).ToString("0.#") + "% 生命";
            }
        }

        // ---------- 测试 ----------
        /// 全部机器人：给够碎片和资源组装好，升到 level 级，上场 a、b（-1 = 不动）。
        public void TestRobots(int level, int a, int b)
        {
            model.state.unlockedRegion = Math.Max(model.state.unlockedRegion, Chapters.FirstRegion(5));
            model.GiftHangar();
            for (int i = 0; i < Catalog.RobotCount; i++)
            {
                if (!model.RobotBuilt(i)) { model.state.blueprints[i] = Catalog.BlueprintNeed; model.state.scrap += Catalog.RobotAssembleScrap(i); model.state.cores += Catalog.RobotAssembleCores; model.Assemble(i); }
                while (model.state.robotLevels[i] < level && model.state.robotLevels[i] < Catalog.RobotMaxLevel) { model.state.scrap += Catalog.RobotUpgradeScrap(model.state.robotLevels[i]); model.UpgradeRobot(i); }
            }
            if (a >= 0) { model.state.deployed[0] = -1; model.state.deployed[1] = -1; model.Deploy(a); if (b >= 0) model.Deploy(b); }
            combat.RobotsChanged();
        }
        public void TestBlueprints(int robot, int count) { model.state.blueprints[robot] = Math.Min(Catalog.BlueprintNeed, count); }
        /// 把一台拆回去（等级 0、碎片 count 片、撤下），看没组装时的卡片。
        public void TestUnbuild(int robot, int count) { model.Undeploy(robot); model.state.robotLevels[robot] = 0; TestBlueprints(robot, count); combat.RobotsChanged(); }
    }
}
