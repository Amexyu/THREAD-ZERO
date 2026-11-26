using UnityEngine;

namespace ThreadZero
{
    /// v0.20 机械飞升 100% 的尾声（机械飞升_设计.md 实现顺序第 4 步）：一张插图（Gemini 卡 26 第 4 张）+ 几句话。
    /// 飞升完成的横幅放完以后自动弹一次（存档记 epilogueSeen），整备页飞升栏的「重看尾声」能再看。文案是草稿。
    public sealed partial class GameApp
    {
        private static readonly string[] EpilogueLines = {
            "零号织网的最深处没有敌人，只有一颗在呼吸的核心。",
            "回收员接上最后一根线缆，零号醒了，第一次听见了整座城——不是被吞没，是被听见了。",
            "机器人都回了家，全城办了一场涂鸦节。",
            "零号问回收员下一道指令。回收员说：「一起把城刷成彩色的。」" };   // v0.38 世界观 A（四行：插图下面最多放得下四行）
        private Texture2D epilogueArt, epilogueArtB; private bool epilogueTried; private float epilogueAt;

        private void OpenEpilogue()
        {
            if (!model.state.epilogueSeen) { model.state.epilogueSeen = true; Save(); }
            epilogueAt = clock; navigation.Open(GameModal.Epilogue);
        }
        /// 测试用（桥接 call TestEpilogue）：直接打开尾声。
        public void TestEpilogue() { OpenEpilogue(); }

        private void EpilogueModal()
        {
            if (!epilogueTried) { epilogueTried = true; epilogueArt = Resources.Load<Texture2D>("Art/V16/Ascension/epilogue"); epilogueArtB = Resources.Load<Texture2D>("Art/V16/Ascension/epilogue_b"); }
            Texture2D art = UiTheme.BrightArt && epilogueArtB != null ? epilogueArtB : epilogueArt;   // v0.40 B「晴天涂鸦」：亮色的尾声插画
            float t = clock - epilogueAt, fade = Mathf.Clamp01(t / .8f);
            Ui.FillScreen(Ui.Alpha(Ui.Ink, .97f * fade));
            const float w = 1200, h = 675, x = 800 - w / 2, y = 36;   // 16:9，插图下面留三行字和按钮
            if (art != null)
            {
                Color old = GUI.color; GUI.color = new Color(1, 1, 1, fade);
                GUI.DrawTexture(Ui.R(x, y, w, h), art, ScaleMode.ScaleAndCrop);
                GUI.color = old;
            }
            float ty = y + h + 20;
            Ui.Text(x, ty, 400, 30, "尾声 · 第零号", 20, Ui.Alpha(Ui.Orange, fade), TextAnchor.MiddleLeft, true);
            for (int i = 0; i < EpilogueLines.Length; i++)   // 一句一句淡入
            {
                float a = Mathf.Clamp01((t - 1f - i * 1.1f) / .7f);
                Ui.FitText(x, ty + 36 + i * 30, 780, 28, EpilogueLines[i], 17, Ui.Alpha(Ui.Panel, a), TextAnchor.MiddleLeft);
            }
            float ba = Mathf.Clamp01((t - 1f - EpilogueLines.Length * 1.1f) / .5f);
            if (ba > 0 && Ui.Button(x + w - 220, ty + 52, 220, 60, "继续回收", 1, true, 18)) navigation.Close();
        }
    }
}
