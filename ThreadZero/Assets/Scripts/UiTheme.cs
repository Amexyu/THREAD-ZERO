using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// v0.16 界面配色方案。界面代码仍然写 Ui.Ink / Ui.Panel 这些「原色」，绘制时按当前方案换色：
    /// 填充（大块底色）、文字、线条（边框和细分隔线）分开映射，所以浅底黑字的界面能整体换成深色。
    /// F3 循环切换，选择记在 PlayerPrefs。数值之后统一调。
    public static class UiTheme
    {
        public static readonly string[] Names = { "黑白橙 · 浅（原版）", "黑白橙 · 中灰", "黑白橙 · 深灰" };
        public const int Default = 1;
        public static int Index { get; private set; } = -1;
        public static string Name { get { return Names[Current]; } }
        public static int Current { get { if (Index < 0) Index = Mathf.Clamp(PlayerPrefs.GetInt("tz_ui_theme2", Default), 0, Names.Length - 1); return Index; } }
        public static void Next() { Index = (Current + 1) % Names.Length; PlayerPrefs.SetInt("tz_ui_theme2", Index); maps = null; }

        public enum Role { Fill, Text, Line }
        // 每套：深块 D0、底 Ground、面板 Panel、舞台 Stage、地 Floor、分隔 Rule、细线 Hair、淡块 Ghost、
        //       格 Tile、格亮 Tile2、钢 Steel、正文 T1、次要 T2、弱 Grey、强调 Accent、强调暗 AccentInk、强调上的字 OnAccent、线 Line
        private struct P { public int d0, ground, panel, stage, floor, rule, hair, ghost, tile, tile2, steel, t1, t2, grey, accent, accentInk, onAccent, line; }
        // v0.16b 全黑太闷：改成中等明度的雾灰底，层次拉开（深块 < 底 < 面板），大块底色带一点透明，背景能透出来。
        // v0.16d 只保留黑白橙：中性灰（不带绿、不带暖），黑色块，白字，原版的橙色。
        private static readonly P[] palettes = {
            new P(),   // 0 = 原版浅色，不映射
            new P { d0=0x111213, ground=0x3C3D3E, panel=0x48494A, stage=0x424344, floor=0x333435, rule=0x5E5F60, hair=0x525354, ghost=0x4A4B4C,
                    tile=0x1A1B1C, tile2=0x262728, steel=0x6A6B6C, t1=0xF4F4F1, t2=0xBDBEBB, grey=0x8E8F8D, accent=0xFF4A12, accentInk=0xB5300A, onAccent=0x111213, line=0x727374 },
            new P { d0=0x0A0A0B, ground=0x222324, panel=0x2C2D2E, stage=0x27282A, floor=0x1C1D1E, rule=0x444547, hair=0x38393B, ghost=0x303133,
                    tile=0x141516, tile2=0x1E1F20, steel=0x55575A, t1=0xF4F4F1, t2=0xAEB0AF, grey=0x7E8082, accent=0xFF4A12, accentInk=0xB5300A, onAccent=0x0A0A0B, line=0x5A5C5E },
        };
        public const float Glass = 1f;    // 大块底色（Ground / Panel / Stage）的不透明度

        private static Dictionary<int, Color>[] maps;
        private static int Key(Color c) { return (Mathf.RoundToInt(c.r * 255) << 16) | (Mathf.RoundToInt(c.g * 255) << 8) | Mathf.RoundToInt(c.b * 255); }
        private static void Build()
        {
            P p = palettes[Current];
            maps = new[] { new Dictionary<int, Color>(), new Dictionary<int, Color>(), new Dictionary<int, Color>() };
            if (Current == 0) return;
            void Add(Color from, int fill, int text, int line)
            { int k = Key(from); maps[0][k] = Ui.Hex(fill); maps[1][k] = Ui.Hex(text); maps[2][k] = Ui.Hex(line); }
            //            原色            作为填充     作为文字   作为线条
            Add(Ui.Ground,  p.ground,  p.t1,    p.line);
            Add(Ui.Panel,   p.panel,   p.t1,    p.line);
            Add(Ui.Stage,   p.stage,   p.t1,    p.line);
            Add(Ui.Floor,   p.floor,   p.t2,    p.line);
            Add(Ui.Ghost,   p.ghost,   p.grey,  p.hair);
            Add(Ui.Hair,    p.hair,    p.grey,  p.hair);
            Add(Ui.Rule,    p.rule,    p.grey,  p.rule);
            Add(Ui.Ink,     p.d0,      p.t1,    p.line);
            Add(Ui.Tile,    p.tile,    p.t1,    p.line);
            Add(Ui.Tile2,   p.tile2,   p.t1,    p.line);
            Add(Ui.Steel,   p.steel,   p.t2,    p.line);   // Body 与 Steel 同色
            Add(Ui.Grey,    p.grey,    p.grey,  p.line);
            Add(Ui.Mute,    p.steel,   p.t2,    p.line);
            Add(Ui.MuteOnInk, p.steel, p.t2,    p.line);
            Add(Ui.Orange,  p.accent,  p.accent, p.accent);
            Add(Ui.OrangeInk, p.accentInk, p.accent, p.accentInk);
            Add(Ui.OnAccent, p.onAccent, p.onAccent, p.onAccent);
            Add(Ui.OrangeHover, Brighten(p.accent), p.accent, p.accent);
        }
        private static int Brighten(int rgb)
        { Color c = Ui.Hex(rgb); c = Color.Lerp(c, Color.white, .18f); return Key(c); }

        public static Color Map(Color c, Role role)
        {
            if (Current == 0) return c;
            if (maps == null) Build();
            Color m; if (!maps[(int)role].TryGetValue(Key(c), out m)) return c;
            m.a = c.a;
            if (role == Role.Fill && c.a > .99f && (Key(c) == Key(Ui.Ground) || Key(c) == Key(Ui.Panel) || Key(c) == Key(Ui.Stage))) m.a = Glass;
            return m;
        }
    }
}
