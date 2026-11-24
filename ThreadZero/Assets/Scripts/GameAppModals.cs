using UnityEngine;

namespace ThreadZero
{
    public sealed partial class GameApp
    {
        private void Modal()
        {
            if (navigation.Modal == GameModal.Epilogue) { EpilogueModal(); return; }   // v0.20 尾声：整屏插图，不用通用弹窗框
            Ui.FillScreen(Ui.Alpha(Ui.Ink, .62f));
            bool map = navigation.Modal == GameModal.Map, offline = navigation.Modal == GameModal.Offline;
            // 选关弹窗 v0.20 加宽 820 → 1000：五章的分页按钮（5 × 152）放在标题栏右侧，原来的宽度下第一章按钮伸出弹窗左边、压住标题
            float x = map ? 300 : offline ? 500 : 450, y = map ? 40 : offline ? 250 : 130, w = map ? 1000 : offline ? 600 : 700, h = map ? 800 : offline ? 380 : 640;
            if (navigation.Modal == GameModal.Settings) { y = 28; h = 844; }   // v0.21 设置多了「语言」「音乐」「展览模式」三行
            string title = map ? "选择街区" : offline ? "欢迎回来" : navigation.Modal == GameModal.Help ? "行动指南" : navigation.Modal == GameModal.Salvage ? "一键分解"
                : navigation.Modal == GameModal.Socket ? (socketSlot >= 0 ? "植入芯片" : "改装位") : "设置";
            Ui.Fill(x, y, w, h, Ui.Panel); Ui.Fill(x, y, w, 72, Ui.Ink); Ui.Fill(x, y, 8, 72, Ui.Orange);
            Ui.FitText(x + 32, y, map ? 92 : w - 160, 72, title, 22, Ui.Panel, TextAnchor.MiddleLeft, true);   // 选关的标题右边紧挨着五个章节页签
            Ui.Frame(x, y, w, h, 2, Ui.Ink);
            if (Ui.Button(x + w - 108, y + 14, 92, 44, "关闭", 3, true, 15)) { navigation.Close(); return; }
            switch (navigation.Modal)
            {
                case GameModal.Map: MapModal(x, y, w); break;
                case GameModal.Help: HelpModal(x, y, w); break;
                case GameModal.Settings: SettingsModal(x, y, w); break;
                case GameModal.Salvage: SalvageModal(x, y, w); break;
                case GameModal.Offline: OfflineModal(x, y, w); break;
                case GameModal.Socket: SocketModal(x, y, w); break;
                default: navigation.Close(); break;
            }
        }

        /// v0.20 离线收益：离开多久、带回多少零件、怎么算的。
        private void OfflineModal(float x, float y, float w)
        {
            int minutes = (int)(offlineAway / 60), hours = minutes / 60;
            string away = hours > 0 ? hours + " 小时 " + (minutes % 60) + " 分" : minutes + " 分钟";
            Ui.Text(x + 40, y + 96, w - 80, 30, "你离开了 " + away, 18, Ui.Ink, TextAnchor.MiddleLeft, true);
            Ui.FitText(x + 40, y + 136, w - 80, 56, "回收无人机带回了 " + offlineGain + " 零件", 30, Ui.Orange, TextAnchor.MiddleLeft, true);
            // 11-24 还有装备（只留新的和军规以上）和织网碎片
            string extra = (offlineItems > 0 ? "装备 " + offlineItems + " 件（最好的是" + Catalog.QualityNames[(int)offlineBest] + "）" : "")
                + (offlineItems > 0 && offlineShards > 0 ? " · " : "") + (offlineShards > 0 ? "织网碎片 " + offlineShards : "");
            if (extra.Length > 0) Ui.FitText(x + 40, y + 190, w - 80, 28, extra, 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            string rule = "离线按挂机收益的 " + Mathf.RoundToInt(Balance.OfflineEfficiency * 100) + "% 算，最多算 " + Mathf.RoundToInt(model.OfflineMax / 3600) + " 小时"
                + (offlineAway > model.OfflineMax ? "（这次超出的部分没算）" : "") + "，只在离开时那一关刷，不会推关。开着挂机条或小窗收益是离线的 2 倍。";
            Ui.Text(x + 40, y + 226, w - 80, 76, rule, 14, Ui.Alpha(Ui.Ink, .7f), TextAnchor.UpperLeft, false, false, true);   // 英文要三行
            Ui.Text(x + 40, y + 310, w - 290, 56, "持有零件 " + model.state.scrap, 14, Ui.Alpha(Ui.Ink, .7f), TextAnchor.MiddleLeft, false);   // 和「收下」按钮同一行
            if (Ui.Button(x + w - 220, y + 310, 180, 56, "收下", 1, true, 18)) navigation.Close();
        }

        private void MapModal(float x, float y, float w)
        {
            EquipmentBase target = Catalog.Find(model.state.recoveryTarget);
            // v0.16 章节分页（标题栏右侧）。打开时默认显示当前所在章。
            if (mapChapter < 1) mapChapter = model.Chapter;
            for (int c = 1; c <= Chapters.ChapterCount; c++)
            {
                bool open = Chapters.FirstRegion(c) <= model.state.unlockedRegion;
                if (Ui.Button(x + w - 108 - (Chapters.ChapterCount - c + 1) * 152, y + 14, 144, 44, "第" + "一二三四五"[c - 1] + "章 · " + Catalog.ChapterNames[c - 1], mapChapter == c ? 1 : 3, open, 14)) mapChapter = c;
            }
            for (int i = 0; i < Chapters.StagesPerChapter; i++)
            {
                int r = Chapters.FirstRegion(mapChapter) + i;
                float top = y + 84 + i * 84; bool unlocked = r <= model.state.unlockedRegion, here = r == model.state.region && !model.InEcho;
                StageDef stage = Chapters.Stage(r);
                Ui.Fill(x + 28, top, w - 56, 76, here ? Ui.Ink : Ui.Stage); Ui.Frame(x + 28, top, w - 56, 76, here ? 2 : 1, here ? Ui.Ink : Ui.Rule);
                Color main = here ? Ui.Panel : unlocked ? Ui.Ink : Ui.Grey, sub = here ? Ui.MuteOnInk : Ui.Mute;
                Ui.Text(x + 48, top, 110, 76, Catalog.StageCode(r), 36, here ? Ui.Orange : main, TextAnchor.MiddleLeft, true, true);
                string kind = stage.kind == StageKind.Boss ? "  · 首领" : stage.kind == StageKind.Elite ? "  · 精英" : "";
                Ui.Text(x + 160, top + 6, 420, 30, Catalog.RegionNames[r] + kind, 20, main, TextAnchor.MiddleLeft, true);
                int count = 0; foreach (WaveDef wd in stage.waves) count += wd.Count;
                string hint = !unlocked ? "通过上一关后开放" : stage.kind == StageKind.Boss ? "首领：" + Catalog.BossName(mapChapter) + " · 会呼叫支援" : stage.waves.Length + " 波 · 约 " + count + " 只";
                if (target != null && Catalog.DropsIn(target.id, false, mapChapter)) hint += " · 回收目标产地";
                Ui.Text(x + 160, top + 38, 420, 24, hint, 14, sub, TextAnchor.MiddleLeft);
                string label = here ? "正在行动" : unlocked ? "前往" : "未解锁";
                if (Ui.Button(x + w - 208, top + 14, 160, 48, label, here ? 3 : 1, unlocked && !here, 16)) { ChangeRegion(r); return; }
            }
            // 回声层入口（打通 1-7 后开放）
            {
                float top = y + 84 + Chapters.StagesPerChapter * 84; bool open = model.EchoUnlocked, here = model.InEcho;
                Ui.Fill(x + 28, top, w - 56, 76, here ? Ui.Ink : Ui.Stage); Ui.Frame(x + 28, top, w - 56, 76, 2, here ? Ui.Ink : open ? Ui.Orange : Ui.Rule);
                Color main = here ? Ui.Panel : open ? Ui.Ink : Ui.Grey, sub = here ? Ui.MuteOnInk : Ui.Mute;
                Ui.Text(x + 48, top, 110, 76, "∞", 40, here || open ? Ui.Orange : main, TextAnchor.MiddleLeft, true);
                Ui.Text(x + 160, top + 6, 420, 30, "零号深网" + (here ? " · 第 " + model.state.echoFloor + " 层" : ""), 20, main, TextAnchor.MiddleLeft, true);
                int resume = model.EchoStartFloor();
                // v0.21 零号深网：最高层、从哪层开始、下一个里程碑（每 25 层核心 +2；第 50 / 100 / 150 层另给限定外观）
                int nextMile = (model.state.echoBest / Chapters.MilestoneEvery + 1) * Chapters.MilestoneEvery;
                string hint = !open ? "击败 1-7 闸门看守后开放" : "最高 " + model.state.echoBest + " 层 · 从第 " + resume + " 层开始 · 下一奖励 第 " + nextMile + " 层" + (System.Array.IndexOf(Catalog.DeepNetFloors, nextMile) >= 0 ? "（限定外观）" : "");
                if (open && model.state.anomalies.Count > 0 && Ui.Width(hint + " · 异常 " + model.state.anomalies.Count, 14) < 436) hint += " · 异常 " + model.state.anomalies.Count;
                Ui.Text(x + 160, top + 38, 440, 24, hint, 14, sub, TextAnchor.MiddleLeft);
                if (open && model.state.anomalies.Count > 0 && Ui.Button(x + w - 336, top + 14, 120, 48, "清除异常", 0, true, 14)) { model.state.anomalies.Clear(); Save(); Notify("协议异常已清除", 2); }
                if (Ui.Button(x + w - 208, top + 14, 160, 48, here ? "正在行动" : open ? "进入" : "未开放", here ? 3 : 1, open && !here, 16))
                { navigation.Close(); Go(GameScreen.Action); EnterEcho(); return; }
            }
        }

        private void SettingsModal(float x, float y, float w)
        {
            float row = y + 100;
            // v0.21 四语：语言名字本身不翻译（Loc 认得这四个），切换后立刻换字体、重新查表
            Ui.Text(x + 32, row, 300, 48, "语言", 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < Loc.Names.Length; i++)
                if (Ui.Button(x + w - 388 + i * 89, row, 85, 48, Loc.Names[i], (int)Loc.Get() == i ? 2 : 0, true, 14)) Loc.Set((Loc.Lang)i);
            row += 68;
            // v0.21 音乐、音效分开，各 5 档
            Ui.Text(x + 32, row, 300, 48, "音乐", 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < 5; i++)
                if (Ui.Button(x + w - 432 + i * 80, row, 76, 48, GameAudio.LevelNames[i], gameAudio.MusicLevel == i ? 2 : 0, true, 13)) gameAudio.SetMusic(i);
            row += 68;
            Ui.Text(x + 32, row, 300, 48, "音效", 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < 5; i++)
                if (Ui.Button(x + w - 432 + i * 80, row, 76, 48, GameAudio.LevelNames[i], gameAudio.SfxLevel == i ? 2 : 0, true, 13)) gameAudio.SetSfx(i);
            row += 68;
            Ui.Text(x + 32, row, 300, 48, "装备辉光", 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            string[] glow = { "关", "低强度", "完整" };   // v0.21 「关」= 关掉（「关闭」留给弹窗的关闭按钮，翻译时两个意思不一样）
            for (int i = 0; i < 3; i++)
                if (Ui.Button(x + w - 292 + i * 88, row, 84, 48, glow[i], model.state.glowMode == i ? 2 : 0, true, 14)) { model.state.glowMode = i; Save(); }
            row += 68;
            // v0.20 技能连招加了震屏，给怕晕的玩家一个开关（存的值 0 完整 / 1 减弱 / 2 关闭，老存档默认完整；顿帧不受影响）
            Ui.Text(x + 32, row, 300, 48, "震屏", 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            string[] shake = { "关", "减弱", "完整" };
            for (int i = 0; i < 3; i++)
                if (Ui.Button(x + w - 292 + i * 88, row, 84, 48, shake[i], model.state.shakeMode == 2 - i ? 2 : 0, true, 14)) { model.state.shakeMode = 2 - i; Save(); }
            row += 68;
            Ui.Text(x + 32, row, 300, 48, "自动分解", 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < 3; i++)
                if (Ui.Button(x + w - 372 + i * 115, row, 110, 48, AutoSalvageNames[i], model.state.autoSalvage == i ? 2 : 0, true, 14)) { model.state.autoSalvage = i; Save(); }
            row += 68;
            Ui.Text(x + 32, row, 300, 48, "显示", 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            // 11-24 加两种挂机小窗：挂机条（屏幕底部一条）/ 小窗（右下角）
            if (Ui.Button(x + w - 456, row, 104, 48, "全屏", Screen.fullScreen ? 2 : 0, true, 14)) Screen.fullScreen = true;
            if (Ui.Button(x + w - 348, row, 104, 48, "窗口", Screen.fullScreen ? 0 : 2, true, 14)) Screen.fullScreen = false;
            if (Ui.Button(x + w - 240, row, 104, 48, "挂机条", 0, !ExhibitMode && !titleOpen, 14)) EnterCompact(CompactKind.Strip);
            if (Ui.Button(x + w - 132, row, 100, 48, "小窗", 0, !ExhibitMode && !titleOpen, 14)) EnterCompact(CompactKind.Corner);
            row += 68;
            // v0.21 展览模式（GameAppExhibit.cs）：开着的时候只显示怎么退出
            Ui.Text(x + 32, row, 300, 48, "展览模式", 17, Ui.Ink, TextAnchor.MiddleLeft, true);
            if (ExhibitMode) Ui.Text(x + w - 392, row, 360, 48, "展览中 · 按 Ctrl+Shift+E 退出", 14, Ui.Orange, TextAnchor.MiddleRight, true);
            else if (Ui.Button(x + w - 292, row, 260, 48, "开启…", 0, true, 15)) exhibitPanel = true;
            row += 68;
            Ui.Fill(x + 32, row, w - 64, 1, Ui.Rule);
            Ui.Text(x + 32, row + 14, w - 64, 44, "自动分解不会处理第一次获得的外观和当前回收目标。进度每 30 秒和关键操作后自动保存。", 14, Ui.Mute, TextAnchor.UpperLeft, false, false, true);
            row += 72;
            float bw = titleOpen ? (w - 76) / 2 : (w - 88) / 3;   // v0.21 加「回到标题」（标题画面里打开设置时不显示）
            if (Ui.Button(x + 32, row, bw, 52, "立即保存", 0, true, 16))
            { Save(); Notify(lastSaveOk ? "进度已保存" : "保存暂不可用，原有存档会保留。", 4); }
            if (Ui.Button(x + 44 + bw, row, bw, 52, "行动指南", 0, true, 16)) navigation.Open(GameModal.Help);
            if (!titleOpen && Ui.Button(x + 56 + bw * 2, row, bw, 52, "回到标题", 0, true, 16)) { BackToTitle(); return; }
            Ui.Text(x + 32, y + 788, w - 64, 30, "THREAD ZERO · v0.21    空格 暂停 / 1 2 3 4 切换页面 / Esc 关闭弹窗", 12, Ui.Mute, TextAnchor.MiddleLeft);
        }

        private void HelpModal(float x, float y, float w)
        {
            // v0.20 更新：原来写的“每个街区第 5 波是首领”早就不对了；加上回声层 / 机械飞升和离线收益。
            string[] titles = { "自动行动", "装备与外观", "有目标地回收", "设备联动", "零号深网与机械飞升" };
            string[] body = {
                "角色会自动战斗、自动推进。每章 7 关，x-3、x-6 是精英，x-7 是首领。倒下只要维修几秒：先回刷前面的关攒零件，变强后自动再挑战。关掉游戏也有离线收益（最多算 2 小时）。",
                "在整备里穿戴装备改变属性；在衣柜里应用外观只改变样子（武器外观只能套在同类武器上）。锁定或穿戴中的装备不能分解。",
                "在衣柜里把想要的装备设为回收目标并查看产地：在它那一章打精英和首领，最多 8 次必得；品质和随机属性另行抽取。",
                "无人机自动射击；标记会强化无人机和电弧；推进器搭配近战武器冲击更强；护盾会为下一次攻击蓄能。成群的关用手炮、重锤这类群伤武器更轻松。",
                "打过第一个首领后，选关最下面可以进零号深网：一层层往上刷，每层都有掉落和织网碎片。织网碎片在整备页左边升级义体，义体越多越强，主角一步步机械飞升。" };
            for (int i = 0; i < titles.Length; i++)
            {
                float top = y + 90 + i * 108;
                Ui.Text(x + 32, top, 60, 36, "0" + (i + 1), 28, Ui.Orange, TextAnchor.MiddleLeft, true, true);
                Ui.Text(x + 92, top, w - 124, 36, titles[i], 18, Ui.Ink, TextAnchor.MiddleLeft, true);
                Ui.WrapText(x + 92, top + 36, w - 124, 68, body[i], 14, Ui.Body);   // 11-23 译文四五行时原来会压到下一节
            }
        }
    }
}
