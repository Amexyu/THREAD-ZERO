using UnityEngine;

namespace ThreadZero
{
    public sealed partial class GameApp
    {
        private void Modal()
        {
            Ui.FillScreen(Ui.Alpha(Ui.Ink, .62f));
            bool map = navigation.Modal == GameModal.Map;
            float x = map ? 390 : 450, y = map ? 96 : 130, w = map ? 820 : 700, h = map ? 708 : 640;
            string title = map ? "选择街区" : navigation.Modal == GameModal.Help ? "行动指南" : navigation.Modal == GameModal.Salvage ? "一键分解" : "设置";
            Ui.Fill(x, y, w, h, Ui.Panel); Ui.Fill(x, y, w, 72, Ui.Ink); Ui.Fill(x, y, 8, 72, Ui.Orange);
            Ui.Text(x + 32, y, w - 160, 72, title, 22, Ui.Panel, TextAnchor.MiddleLeft, true);
            Ui.Frame(x, y, w, h, 2, Ui.Ink);
            if (Ui.Button(x + w - 108, y + 14, 92, 44, "关闭", 3, true, 15)) { navigation.Close(); return; }
            switch (navigation.Modal)
            {
                case GameModal.Map: MapModal(x, y, w); break;
                case GameModal.Help: HelpModal(x, y, w); break;
                case GameModal.Settings: SettingsModal(x, y, w); break;
                case GameModal.Salvage: SalvageModal(x, y, w); break;
                default: navigation.Close(); break;
            }
        }

        private void MapModal(float x, float y, float w)
        {
            EquipmentBase target = Catalog.Find(model.state.recoveryTarget);
            for (int r = 0; r < Catalog.RegionNames.Length; r++)
            {
                float top = y + 84 + r * 84; bool unlocked = r <= model.state.unlockedRegion, here = r == model.state.region;
                StageDef stage = Chapters.Stage(r);
                Ui.Fill(x + 28, top, w - 56, 76, here ? Ui.Ink : Ui.Stage); Ui.Frame(x + 28, top, w - 56, 76, here ? 2 : 1, here ? Ui.Ink : Ui.Rule);
                Color main = here ? Ui.Panel : unlocked ? Ui.Ink : Ui.Grey, sub = here ? Ui.MuteOnInk : Ui.Mute;
                Ui.Text(x + 48, top, 110, 76, Catalog.StageCode(r), 36, here ? Ui.Orange : main, TextAnchor.MiddleLeft, true, true);
                string kind = stage.kind == StageKind.Boss ? "  · 首领" : stage.kind == StageKind.Elite ? "  · 精英" : "";
                Ui.Text(x + 160, top + 6, 420, 30, Catalog.RegionNames[r] + kind, 20, main, TextAnchor.MiddleLeft, true);
                int count = 0; foreach (WaveDef wd in stage.waves) count += wd.Count;
                string hint = !unlocked ? "通过上一关后开放" : stage.kind == StageKind.Boss ? "首领：" + Catalog.BossNames[r] + " · 会呼叫支援" : stage.waves.Length + " 波 · 约 " + count + " 只";
                if (target != null && Catalog.SourceRegion(target.id) == Catalog.District(r)) hint += " · 回收目标产地";
                Ui.Text(x + 160, top + 38, 420, 24, hint, 14, sub, TextAnchor.MiddleLeft);
                string label = here ? "正在行动" : unlocked ? "前往" : "未解锁";
                if (Ui.Button(x + w - 208, top + 14, 160, 48, label, here ? 3 : 1, unlocked && !here, 16)) { ChangeRegion(r); return; }
            }
        }

        private void SettingsModal(float x, float y, float w)
        {
            float row = y + 100;
            Ui.Text(x + 32, row, 300, 48, "音效", 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            if (Ui.Button(x + w - 292, row, 260, 48, sound ? "开启" : "关闭", sound ? 2 : 0, true, 15)) sound = !sound;
            row += 68;
            Ui.Text(x + 32, row, 300, 48, "装备辉光", 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            string[] glow = { "关闭", "低强度", "完整" };
            for (int i = 0; i < 3; i++)
                if (Ui.Button(x + w - 292 + i * 88, row, 84, 48, glow[i], model.state.glowMode == i ? 2 : 0, true, 14)) { model.state.glowMode = i; Save(); }
            row += 68;
            Ui.Text(x + 32, row, 300, 48, "自动分解", 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < 3; i++)
                if (Ui.Button(x + w - 372 + i * 115, row, 110, 48, AutoSalvageNames[i], model.state.autoSalvage == i ? 2 : 0, true, 14)) { model.state.autoSalvage = i; Save(); }
            row += 68;
            Ui.Text(x + 32, row, 300, 48, "显示", 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            if (Ui.Button(x + w - 292, row, 260, 48, Screen.fullScreen ? "全屏 · 点击切回窗口" : "窗口 · 点击切到全屏", 0, true, 15)) Screen.fullScreen = !Screen.fullScreen;
            row += 68;
            Ui.Fill(x + 32, row, w - 64, 1, Ui.Rule);
            Ui.Text(x + 32, row + 14, w - 64, 44, "自动分解不会处理第一次获得的外观和当前回收目标。进度每 30 秒和关键操作后自动保存。", 14, Ui.Mute, TextAnchor.UpperLeft, false, false, true);
            row += 72;
            if (Ui.Button(x + 32, row, (w - 76) / 2, 52, "立即保存", 0, true, 16))
            { Save(); Notify(lastSaveOk ? "进度已保存" : "保存暂不可用，原有存档会保留。", 4); }
            if (Ui.Button(x + 44 + (w - 76) / 2, row, (w - 76) / 2, 52, "行动指南", 0, true, 16)) navigation.Open(GameModal.Help);
            Ui.Text(x + 32, y + 584, w - 64, 30, "THREAD ZERO · v0.7    空格 暂停 / 1 2 3 切换页面 / Esc 关闭弹窗", 12, Ui.Mute, TextAnchor.MiddleLeft);
        }

        private void HelpModal(float x, float y, float w)
        {
            string[] titles = { "自动行动", "装备与外观", "有目标地回收", "设备联动" };
            string[] body = {
                "角色会自动战斗。每个街区第 5 波是首领；倒下只需要维修几秒，不会损失装备。切换页面时战斗继续。",
                "在整备里穿戴装备会改变属性；在衣柜里应用外观只改变样子。锁定或穿戴中的装备不能分解。",
                "在衣柜里把想要的装备设为回收目标并查看产地。目标街区的首领最多 8 次必得；品质和随机属性另行抽取。",
                "无人机自动射击；标记会强化无人机和电弧；推进器搭配近战武器冲击更强；护盾会为下一次攻击蓄能。" };
            for (int i = 0; i < 4; i++)
            {
                float top = y + 96 + i * 132;
                Ui.Text(x + 32, top, 60, 40, "0" + (i + 1), 30, Ui.Orange, TextAnchor.MiddleLeft, true, true);
                Ui.Text(x + 92, top, w - 124, 40, titles[i], 19, Ui.Ink, TextAnchor.MiddleLeft, true);
                Ui.Text(x + 92, top + 42, w - 124, 76, body[i], 15, Ui.Body, TextAnchor.UpperLeft, false, false, true);
            }
        }
    }
}
