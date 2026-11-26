using System.Collections.Generic;
using UnityEngine;

namespace ThreadZero
{
    /// v0.16 界面配色方案。界面代码仍然写 Ui.Ink / Ui.Panel 这些「原色」，绘制时按当前方案换色：
    /// 填充（大块底色）、文字、线条（边框和细分隔线）分开映射，所以浅底黑字的界面能整体换成深色。
    /// F3 循环切换，选择记在 PlayerPrefs。数值之后统一调。
    public static class UiTheme
    {
        public static readonly string[] Names = { "黑白橙 · 浅（原版）", "黑白橙 · 中灰", "黑白橙 · 深灰", "晴天涂鸦 · 亮", "奶白 · 珊瑚", "薄荷汽水", "晴空蓝", "夜空藏青" };
        public const int Bright = 3;   // v0.40 B「晴天涂鸦」：黄色斜纹底、奶白面板、藏青深块、宝蓝导航栏、番茄橙强调
        public static bool Stripes { get { return Current == Bright; } }
        /// v0.41 第 4 套以后（含 B）：标题 / 开场 / 尾声用亮色插画（关卡背景已经全是亮色）。
        public static bool BrightArt { get { return Current >= Bright; } }
        /// 整备、衣柜页的底：浅色方案用和别的页一样的 Ground，不再画深炭灰网格。
        public static bool LightPage { get { return Current >= Bright && Current < Bright + 1 + Lights.Length; } }
        /// 整备页属性板的塑料颜色（null = 原来的深色拉丝金属）。
        public static Color? StatsTint { get { if (Current == Bright) return Ui.Hex(0xF5603A); int i = Current - Bright - 1; return i >= 0 && i < Lights.Length ? Ui.Hex(Lights[i].ink) : (Color?)null; } }
        /// 属性板数字的描边（B 是深橙，其余深色）。
        public static Color StatsOutline { get { return Current == Bright ? Ui.Hex(0x9E2E12) : Ui.Hex(0x0B0C0D); } }
        public static readonly Color StripeColor = new Color(.97f, .78f, .25f, .55f);
        public const int Default = 1;
        public static int Index { get; private set; } = -1;
        public static string Name { get { return Names[Current]; } }
        public static int Current { get { if (Index < 0) Index = Mathf.Clamp(PlayerPrefs.GetInt("tz_ui_theme2", Default), 0, Names.Length - 1); return Index; } }
        public static void Next() { Index = (Current + 1) % Names.Length; PlayerPrefs.SetInt("tz_ui_theme2", Index); maps = null; }
        /// 测试用：临时换方案，不写进 PlayerPrefs（截图对比用，不影响玩家自己选的）。
        public static void Set(int i) { Index = Mathf.Clamp(i, 0, Names.Length - 1); maps = null; }

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
            maps = new[] { new Dictionary<int, Color>(), new Dictionary<int, Color>(), new Dictionary<int, Color>() };
            if (Current == 0) return;
            if (Current == Bright) { BuildBright(); return; }
            int li = Current - Bright - 1;
            if (li >= 0 && li < Lights.Length) { BuildLight(Lights[li]); return; }
            P p = Current < palettes.Length ? palettes[Current] : NightNavy;
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
            Add(Ui.RailInk, p.d0,      p.t1,    p.line);
            Add(Ui.RailHover, p.tile2, p.t1,    p.line);
        }
        // ---------- v0.41 用户说 B 太花：三套干净的浅色 + 一套深色，都不铺斜纹，导航栏和深块同一个颜色 ----------
        private struct L { public int ground, panel, stage, floor, ghost, hair, rule, ink, inkText, tile, tile2, steel, grey, mute, onInk, muteOnInk, accent, accentText, accentInk, onAccent, hover, railHover; }
        private static readonly L[] Lights = {
            // 4 奶白 · 珊瑚：暖灰白底、白卡片、暖炭灰深块、珊瑚红强调
            new L { ground=0xF1ECE4, panel=0xFFFFFF, stage=0xFBF8F3, floor=0xE8E1D6, ghost=0xF2EDE5, hair=0xE2DACD, rule=0xD3C8B6, ink=0x34313C, inkText=0x2E2B35, tile=0x3E3A47, tile2=0x4B4757,
                    steel=0x6C6778, grey=0x9C96A3, mute=0x78717F, onInk=0xFFFFFF, muteOnInk=0xD6D1DC, accent=0xFF6F59, accentText=0xE8543E, accentInk=0xC4432F, onAccent=0xFFFFFF, hover=0xFF8A77, railHover=0x4B4757 },
            // 5 薄荷汽水：淡薄荷底、白卡片、深青深块、橘子橙强调
            new L { ground=0xE4F2EC, panel=0xFFFFFF, stage=0xF4FAF7, floor=0xD5EAE1, ghost=0xE8F4EF, hair=0xCDE3DA, rule=0xB5D4C8, ink=0x22505B, inkText=0x1E3F47, tile=0x2A5D69, tile2=0x336D7B,
                    steel=0x5D8A94, grey=0x8FA9AE, mute=0x5C7C83, onInk=0xFFFFFF, muteOnInk=0xCBE5E0, accent=0xFF8A3D, accentText=0xE8701F, accentInk=0xC0601B, onAccent=0xFFFFFF, hover=0xFFA163, railHover=0x336D7B },
            // 6 晴空蓝：淡天蓝底、白卡片、灰蓝靛深块、阳光橙黄强调（橙黄上用深字）
            new L { ground=0xE7F0FA, panel=0xFFFFFF, stage=0xF5F9FE, floor=0xD6E4F3, ghost=0xECF3FB, hair=0xD1DFEE, rule=0xB9CDE3, ink=0x2E3D63, inkText=0x22304E, tile=0x364775, tile2=0x425488,
                    steel=0x6677A0, grey=0x97A3BC, mute=0x64708C, onInk=0xFFFFFF, muteOnInk=0xD5DDF0, accent=0xFFB03B, accentText=0xD98A0E, accentInk=0xB8780E, onAccent=0x22304E, hover=0xFFC266, railHover=0x425488 },
        };
        private static void BuildLight(L c)
        {
            void Add(Color from, int fill, int text, int line)
            { int k = Key(from); maps[0][k] = Ui.Hex(fill); maps[1][k] = Ui.Hex(text); maps[2][k] = Ui.Hex(line); }
            //              原色            作为填充     作为文字      作为线条
            Add(Ui.Ground,    c.ground,  c.onInk,     c.panel);
            Add(Ui.Panel,     c.panel,   c.onInk,     c.panel);
            Add(Ui.Stage,     c.stage,   c.onInk,     c.panel);
            Add(Ui.Floor,     c.floor,   c.floor,     c.rule);
            Add(Ui.Ghost,     c.ghost,   c.grey,      c.hair);
            Add(Ui.Hair,      c.hair,    c.grey,      c.hair);
            Add(Ui.Rule,      c.rule,    c.mute,      c.rule);
            Add(Ui.Ink,       c.ink,     c.inkText,   c.ink);
            Add(Ui.Tile,      c.tile,    c.inkText,   c.ink);
            Add(Ui.Tile2,     c.tile2,   c.inkText,   c.ink);
            Add(Ui.Steel,     c.steel,   c.mute,      c.steel);
            Add(Ui.Grey,      c.grey,    c.grey,      c.grey);
            Add(Ui.Mute,      c.steel,   c.mute,      c.steel);
            Add(Ui.MuteOnInk, c.muteOnInk, c.muteOnInk, c.muteOnInk);
            Add(Ui.Orange,    c.accent,  c.accentText, c.accent);
            Add(Ui.OrangeInk, c.accentInk, c.accentInk, c.accentInk);
            Add(Ui.OnAccent,  c.onAccent, c.onAccent, c.onAccent);
            Add(Ui.OrangeHover, c.hover, c.accentText, c.accent);
            Add(Ui.RailInk,   c.ink,     c.onInk,     c.ink);
            Add(Ui.RailHover, c.railHover, c.onInk,   c.railHover);
        }
        // 8 夜空藏青：深色方案（和中灰 / 深灰同一套映射），底色带一点蓝，橙色强调
        private static readonly P NightNavy = new P { d0=0x10131F, ground=0x1D2133, panel=0x262B40, stage=0x22263A, floor=0x181B2A, rule=0x3B4263, hair=0x2E344F, ghost=0x2A2F47,
                    tile=0x151827, tile2=0x1F2338, steel=0x4A5276, t1=0xF5F6FA, t2=0xB4BAD0, grey=0x858CA8, accent=0xFF7A45, accentInk=0xC2521F, onAccent=0x10131F, line=0x4A5276 };
        /// v0.40 B「晴天涂鸦」：沿用原版浅色的明暗关系（浅底深字、深块浅字），只换色相。
        private static void BuildBright()
        {
            void Add(Color from, int fill, int text, int line)
            { int k = Key(from); maps[0][k] = Ui.Hex(fill); maps[1][k] = Ui.Hex(text); maps[2][k] = Ui.Hex(line); }
            const int navy = 0x1C2766, navyText = 0x1B2457, cream = 0xFFFDF4, tan = 0xDCC88E;
            //              原色            作为填充    作为文字   作为线条
            Add(Ui.Ground,    0xFFE38A, cream,    cream);      // 页面底：黄（上面再铺斜纹）
            Add(Ui.Panel,     cream,    0xFFFFFF, cream);      // 面板：奶白；当字用时是深块上的白字
            Add(Ui.Stage,     0xFFF8E4, 0xFFFFFF, cream);
            Add(Ui.Floor,     0xF8ECC4, 0xF3E9C8, tan);
            Add(Ui.Ghost,     0xFFF2C6, 0xA8987A, 0xEBDDB0);
            Add(Ui.Hair,      0xEFE2B8, 0xA8987A, 0xEBDDB0);
            Add(Ui.Rule,      0xE2CF94, 0x8F8466, tan);
            Add(Ui.Ink,       navy,     navyText, navy);       // 深块：藏青；浅底上的字：深藏青
            Add(Ui.Tile,      0x243277, navyText, navy);
            Add(Ui.Tile2,     0x31429A, navyText, navy);
            Add(Ui.Steel,     0x4B5AA6, 0x3E4A7A, 0x4B5AA6);   // Body 与 Steel 同色
            Add(Ui.Grey,      0x9AA0BE, 0x8C93B3, 0x9AA0BE);
            Add(Ui.Mute,      0x4B5AA6, 0x5B6488, 0x4B5AA6);
            Add(Ui.MuteOnInk, 0xDDE5FF, 0xDDE5FF, 0xDDE5FF);   // 深块 / 导航栏上的次要色：图标也用它画，要够亮
            Add(Ui.Orange,    0xF5522A, 0xE8461F, 0xF5522A);   // 番茄橙
            Add(Ui.OrangeInk, 0xB8381A, 0xB8381A, 0xB8381A);
            Add(Ui.OnAccent,  0xFFFFFF, 0xFFFFFF, 0xFFFFFF);   // 橙色上的字：白
            Add(Ui.OrangeHover, 0xFF7650, 0xE8461F, 0xF5522A);
            Add(Ui.RailInk,   0x2E5BFF, 0xFFFFFF, 0x2E5BFF);   // 导航栏：宝蓝
            Add(Ui.RailHover, 0x4C74FF, 0xFFFFFF, 0x4C74FF);
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
