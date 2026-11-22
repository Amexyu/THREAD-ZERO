using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// v0.20 新手提示（待定方案第 4 条）：不做强制教程，五条只出现一次的提示，在合适的时候从行动页上方滑出来，
    /// 11 秒后自己收起，点一下也能关。看过的记在存档 GameState.tips（按位）；测试存档按 F9 清空后会重新出现。不影响数值。
    public sealed partial class GameApp
    {
        public enum Tip { Start = 0, Drop = 1, Defeat = 2, Elite = 3, Echo = 4 }
        private static readonly string[] TipTexts = {
            "角色会自己往前打，不用操作。鼠标移到屏幕上沿、下沿或左边，终端界面（整备、衣柜、工坊）就会滑出来。",
            "掉装备了：去「整备」比一比，穿上更好的。左边导航上的数字是还没看过的件数。",
            "被打倒了也没关系：会先回前面的关攒零件、变强，再自动挑战这一关。",
            "精英关：每章第 3、6 关是精英，第 7 关是首领，比普通关难一截。打不过就先回刷，升升装备。",
            "打败了第一个首领，零号深网开放了：在选关最下面，一层比一层难，还能攒织网碎片升义体。" };
        private const float TipLife = 11, TipStartDelay = 2.5f;
        /// 自动测试默认不弹提示（免得盖住截图）；要测提示的剧本 set GameApp.SuppressTips false。
        public static bool SuppressTips;
        private readonly List<Tip> tipQueue = new List<Tip>();
        private int tipShown = -1; private float tipAge, tipDoneAt = -99, tipStartClock = -1;   // tipAge：这条提示实际露面了多久（被挡住的时候不算）

        private bool TipSeen(Tip t) { return (model.state.tips & (1 << (int)t)) != 0; }
        private void QueueTip(Tip t)
        {
            if (SuppressTips || daggerReview || TipSeen(t) || tipShown == (int)t || tipQueue.Contains(t)) return;
            tipQueue.Add(t);
        }
        /// 测试用（桥接 call TestTip 3）：不管看没看过，马上弹这一条。
        public void TestTip(int i) { tipQueue.Remove((Tip)i); tipShown = Mathf.Clamp(i, 0, TipTexts.Length - 1); tipAge = 0; }

        // 每帧在行动页检查一遍：刚进游戏、进了精英关、回声层刚开放（掉装备、被打倒在事件里排）。
        private void TipTriggers()
        {
            if (tipStartClock < 0) tipStartClock = clock;
            if (clock - tipStartClock > TipStartDelay) QueueTip(Tip.Start);
            if (!model.InEcho && Chapters.Stage(model.state.region).kind == StageKind.Elite) QueueTip(Tip.Elite);
            if (model.EchoUnlocked) QueueTip(Tip.Echo);
        }
        // 这些时候提示先藏起来、计时停住，等画面空出来再接着放：不在行动页、开着弹窗、暂停 / 维修卡片、转场、
        // 关卡名牌（左上角）、首领义体接入和飞升阶段横幅（屏幕中间）。
        private bool BannerBusy { get { return clock - implantAt < 7f || (stageAt > -9 && clock - stageAt < 9.5f) || stagePending; } }
        private bool TipBlocked
        {
            get
            {
                return navigation.Screen != GameScreen.Action || navigation.HasModal || paused || combat.recovery > 0 || transitionLeft > 0
                    || (clock - stageTagAt > -.5f && clock - stageTagAt < 2.1f) || BannerBusy;
            }
        }

        private void Tips()
        {
            if (model == null) return;
            if (navigation.Screen == GameScreen.Action) TipTriggers();
            if (TipBlocked) return;
            if (tipShown < 0)
            {
                if (tipQueue.Count == 0 || clock - tipDoneAt < 1.2f) return;   // 两条之间隔一小会
                Tip next = tipQueue[0]; tipQueue.RemoveAt(0);
                if (TipSeen(next)) return;
                tipShown = (int)next; tipAge = 0;
                model.state.tips |= 1 << tipShown; Save();   // 露面了就算看过（没看完就退出也不会再弹）
            }
            if (Event.current.type == EventType.Repaint) tipAge += Mathf.Min(Time.unscaledDeltaTime, .1f);
            if (tipAge > TipLife) { tipShown = -1; tipDoneAt = clock; return; }
            DrawTip();
        }
        private void DrawTip()
        {
            float a = Mathf.Clamp01(tipAge / .22f) * Mathf.Clamp01((TipLife - tipAge) / .45f);
            if (a <= 0) return;
            string text = TipTexts[tipShown];
            float tw = Ui.Width(text, 16, true); bool two = tw > 680;
            // 一般放在顶栏下面；首领关这里是首领血条（名字 y 110 起、血条到 y 187 左右），挪到血条下面
            float top = combat.BossEnemy != null ? 196 : 112;
            float w = Mathf.Min(760, tw + 84), h = two ? 92 : 70, x = 800 - w / 2, y = top - (1 - Mathf.Clamp01(tipAge / .22f)) * 12;
            Ui.Fill(x, y, w, h, Ui.Alpha(Ui.Ink, .94f * a)); Ui.Fill(x, y, 6, h, Ui.Alpha(Ui.Orange, a));
            Ui.Text(x + 24, y + 9, 300, 18, "新手提示 · " + (tipShown + 1) + " / " + TipTexts.Length, 12, Ui.Alpha(Ui.Orange, a), TextAnchor.MiddleLeft, true);
            Ui.Text(x + w - 150, y + 9, 130, 18, "点一下关掉", 11, Ui.Alpha(Ui.MuteOnInk, a * .8f), TextAnchor.MiddleRight);
            Ui.Text(x + 24, y + 30, w - 48, h - 36, text, 16, Ui.Alpha(Ui.Panel, a), TextAnchor.UpperLeft, true, false, true);
            if (Ui.Click(x, y, w, h)) tipAge = Mathf.Max(tipAge, TipLife - .45f);   // 点了就开始收起
        }
    }
}
