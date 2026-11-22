using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// v0.21 音乐与音效（免费素材，来源和授权见 Docs/音乐音效_来源与授权.md）。
    /// 音乐：两条 AudioSource 交叉淡入淡出（1.6 秒），换场景（标题 / 每章 / 首领 / 零号深网 / 尾声）才换曲。
    /// 音效：一个 OneShot 源；同一个音效 gap 秒内只响一次（群怪时不会叠成一片）；有 name_1、name_2… 变体时随机挑一个。
    /// 音量：设置里音乐 / 音效各 5 档（PlayerPrefs tz_music / tz_sfx）。自动测试时 TZAutoTest 和桥接层整体静音。
    public sealed class GameAudio
    {
        public const string MusicKey = "tz_music", SfxKey = "tz_sfx";
        public static readonly float[] Levels = { 0, .25f, .5f, .75f, 1 };
        public static readonly string[] LevelNames = { "关", "25%", "50%", "75%", "100%" };
        private const float Fade = 1.6f, MusicGain = .55f;
        public int MusicLevel { get; private set; }
        public int SfxLevel { get; private set; }
        private readonly AudioSource a, b, fx;
        private AudioSource current, previous;
        private string currentTrack;
        private float fade = 1;
        private readonly Dictionary<string, AudioClip[]> sfx = new Dictionary<string, AudioClip[]>();
        private readonly Dictionary<string, float> last = new Dictionary<string, float>();

        public GameAudio(GameObject host)
        {
            a = host.AddComponent<AudioSource>(); b = host.AddComponent<AudioSource>(); fx = host.AddComponent<AudioSource>();
            foreach (AudioSource s in new[] { a, b }) { s.loop = true; s.playOnAwake = false; s.volume = 0; }
            fx.playOnAwake = false;
            MusicLevel = Mathf.Clamp(PlayerPrefs.GetInt(MusicKey, 2), 0, 4); SfxLevel = Mathf.Clamp(PlayerPrefs.GetInt(SfxKey, 3), 0, 4);
        }
        public void SetMusic(int level) { MusicLevel = Mathf.Clamp(level, 0, 4); PlayerPrefs.SetInt(MusicKey, MusicLevel); PlayerPrefs.Save(); }
        public void SetSfx(int level) { SfxLevel = Mathf.Clamp(level, 0, 4); PlayerPrefs.SetInt(SfxKey, SfxLevel); PlayerPrefs.Save(); }
        public bool SfxOn { get { return SfxLevel > 0; } }
        public string Track { get { return currentTrack; } }

        /// 换背景音乐（同一首不重来）。track = Resources/Audio/Music/ 下的文件名；没有这个文件就安静淡出。
        public void Music(string track)
        {
            if (track == currentTrack) return;
            currentTrack = track;
            AudioClip clip = track == null ? null : Resources.Load<AudioClip>("Audio/Music/" + track);
            if (previous != null) previous.Stop();
            previous = current;
            current = current == a ? b : a;
            current.clip = clip; current.volume = 0;
            if (clip != null) current.Play();
            fade = 0;
        }
        public void Tick(float dt)
        {
            fade = Mathf.Min(1, fade + dt / Fade);
            float v = Levels[MusicLevel] * MusicGain;
            if (current != null) current.volume = v * fade;
            if (previous != null) { previous.volume = v * (1 - fade); if (fade >= 1) { previous.Stop(); previous = null; } }
        }
        /// 放一个音效。返回 false = 没有这个音效文件（调用方可以退回原来程序生成的提示音）。
        public bool Sfx(string name, float volume = 1, float gap = .05f)
        {
            AudioClip[] clips = Clips(name);
            if (clips.Length == 0) return false;
            if (SfxLevel <= 0) return true;
            float now = Time.unscaledTime, t;
            if (last.TryGetValue(name, out t) && now - t < gap) return true;
            last[name] = now;
            fx.PlayOneShot(clips[Random.Range(0, clips.Length)], Mathf.Clamp01(volume) * Levels[SfxLevel]);
            return true;
        }
        private AudioClip[] Clips(string name)
        {
            AudioClip[] c;
            if (sfx.TryGetValue(name, out c)) return c;
            var list = new List<AudioClip>();
            AudioClip first = Resources.Load<AudioClip>("Audio/Sfx/" + name); if (first != null) list.Add(first);
            for (int i = 1; i < 6; i++) { AudioClip v = Resources.Load<AudioClip>("Audio/Sfx/" + name + "_" + i); if (v == null) break; list.Add(v); }
            sfx[name] = c = list.ToArray(); return c;
        }
    }
}
