using UnityEngine;

namespace ThreadZero
{
    /// v0.20 机械飞升 100% 的尾声（机械飞升_设计.md 实现顺序第 4 步）：一张插图（Gemini 卡 26 第 4 张）+ 几句话。
    /// 飞升完成的横幅放完以后自动弹一次（存档记 epilogueSeen），整备页飞升栏的「重看尾声」能再看。文案是草稿。
    public sealed partial class GameApp
    {
        private static readonly string[] EpilogueLines = {
            "零号织网的最深处没有敌人，只有一颗在呼吸的核心。",
            "回收员把最后一根线缆接上的时候，整座城的声音同时安静了下来——不是被吞没，是被听见了。",
            "零织协议不再追捕回收员。它在等第零号的下一道指令。" };
        private Texture2D epilogueArt; private bool epilogueTried; private float epilogueAt;

        private void OpenEpilogue()
        {
            if (!model.state.epilogueSeen) { model.state.epilogueSeen = true; Save(); }
            epilogueAt = clock; navigation.Open(GameModal.Epilogue);
        }
        /// 测试用（桥接 call TestEpilogue）：直接打开尾声。
        public void TestEpilogue() { OpenEpilogue(); }

        private void EpilogueModal()
        {
            if (!epilogueTried) { epilogueTried = true; epilogueArt = Resources.Load<Texture2D>("Art/V16/Ascension/epilogue"); }
            float t = clock - epilogueAt, fade = Mathf.Clamp01(t / .8f);
            Ui.FillScreen(Ui.Alpha(Ui.Ink, .97f * fade));
            const float w = 1200, h = 675, x = 800 - w / 2, y = 36;   // 16:9，插图下面留三行字和按钮
            if (epilogueArt != null)
            {
                Color old = GUI.color; GUI.color = new Color(1, 1, 1, fade);
                GUI.DrawTexture(Ui.R(x, y, w, h), epilogueArt, ScaleMode.ScaleAndCrop);
                GUI.color = old;
            }
            float ty = y + h + 20;
            Ui.Text(x, ty, 400, 30, "尾声 · 第零号", 20, Ui.Alpha(Ui.Orange, fade), TextAnchor.MiddleLeft, true);
            for (int i = 0; i < EpilogueLines.Length; i++)   // 一句一句淡入
            {
                float a = Mathf.Clamp01((t - 1f - i * 1.1f) / .7f);
                Ui.Text(x, ty + 36 + i * 30, 780, 28, EpilogueLines[i], 17, Ui.Alpha(Ui.Panel, a), TextAnchor.MiddleLeft);
            }
            float ba = Mathf.Clamp01((t - 1f - EpilogueLines.Length * 1.1f) / .5f);
            if (ba > 0 && Ui.Button(x + w - 220, ty + 52, 220, 60, "继续回收", 1, true, 18)) navigation.Close();
        }
    }
}
