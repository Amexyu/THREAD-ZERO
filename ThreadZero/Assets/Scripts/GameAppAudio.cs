using UnityEngine;

namespace ThreadZero
{
    /// v0.21 音乐与音效接到游戏里：按场景选曲；战斗、界面的音效挂在已有的事件上。
    public sealed partial class GameApp
    {
        private GameAudio gameAudio;

        private void SetupAudio()
        {
            gameAudio = new GameAudio(gameObject);
            Ui.Clicked = () => gameAudio.Sfx("click", .45f, .03f);
            WeaponFx.OnFire = kind => { if (!WeaponFx.Plain) gameAudio.Sfx(kind == "cannon" ? "shot_cannon" : kind == "coil" ? "shot_coil" : kind == "shotgun" ? "shot_shotgun" : "shot_gun", .5f, .05f); };
            WeaponFx.OnSlam = () => gameAudio.Sfx("smash", .7f, .12f);
        }
        /// 现在该放哪首：标题 / 开场 → title；尾声 → title；零号深网 → deepnet；首领关 → boss；其余按章 ch1–ch5。
        private string CurrentTrack()
        {
            if (titleOpen || openingAt >= 0 || navigation.Modal == GameModal.Epilogue) return "title";
            if (model.InEcho) return combat.Boss ? "boss" : "deepnet";
            if (combat.Boss) return "boss";
            return "ch" + Mathf.Clamp(model.Chapter, 1, 5);
        }
        private void AudioTick(float dt)
        {
            if (gameAudio == null) return;
            gameAudio.Music(CurrentTrack());
            gameAudio.Tick(dt);
        }
        /// 原来几个程序生成的提示音换成音效文件；文件缺了就还用程序音。
        private void Play(AudioClip clip)
        {
            if (clip == null) return;
            string name = clip == equipSound ? "equip" : clip == dropSound ? "drop" : clip == rareSound ? "rare" : clip == salvageSound ? "salvage" : clip == bossSound ? "boss" : null;
            float volume = clip == bossSound ? .8f : clip == dropSound ? .55f : .7f;
            if (gameAudio != null && name != null && gameAudio.Sfx(name, volume, .06f)) return;
            if (gameAudio != null && gameAudio.SfxOn && source != null) source.PlayOneShot(clip, GameAudio.Levels[gameAudio.SfxLevel]);
        }
        private void Sfx(string name, float volume = 1, float gap = .05f) { if (gameAudio != null) gameAudio.Sfx(name, volume, gap); }
        public string TestTrack() { return gameAudio != null ? gameAudio.Track : "-"; }
    }
}
