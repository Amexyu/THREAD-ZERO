from pathlib import Path
root=Path(__file__).resolve().parent.parent
p=root/'README.md';s=p.read_text(encoding='utf-8')
pos=s.index('目前：')
s=s[:pos]+'''目前：v0.5，浅色机能工坊 UI、20 件装备贴图、六部件攻击动作与延迟命中。161 项逻辑及存储检查通过，Unity 6000.6.9f1 运行时/编辑器源代码编译通过。

- [本轮视觉与动作说明](Docs/VisualUpdate-v0.5.md)
- 连续动作预览：Artifacts/AttackAnimationV5.gif（与游戏共用姿态及贴图布局，非 Unity 运行录屏）。
- 界面预览：Artifacts/ActionUiV5Review.png（离线布局复核）。

尚待现有 Unity 编辑器刷新后的实际视觉、交互与存档验收；未生成新版 Windows 可执行包。旧预览与文档历史条目请以 v0.5 说明为准。
'''
p.write_text(s,encoding='utf-8')
for name in ['Progress.md','ArtAnimation.md','UiRedesign.md']:
    p=root/'Docs'/name;s=p.read_text(encoding='utf-8')
    note='''> **v0.5 最新状态（2026-11-15）**：已改为浅色机能工坊 UI，全部 20 件装备接入贴图，攻击采用身体六部件与衣袖、武器联动，伤害改在前摇之后结算。161 项检查与 6000.6.9f1 源码编译通过。见 [本轮完整说明](VisualUpdate-v0.5.md)。下方为旧版本记录，其中“仅初始装备有贴图”“只有武器转动”“136 项检查”等描述已被本轮替代；Unity 内实际验收仍待完成。

'''
    if '> **v0.5 最新状态' not in s:
        at=s.index('\n')+1;s=s[:at]+'\n'+note+s[at:]
    p.write_text(s,encoding='utf-8')
p=root/'Tools/BalanceReport.cs';s=p.read_text(encoding='utf-8')
s=s.replace('v0.3 数值模拟报告','v0.5 数值模拟报告')
s=s.replace('Unity 许可/IPC 问题尚未完成隔离，因此不能从此报告推断实际帧率、辉光观感、音效与所有鼠标交互已经通过。', '本报告基于纯 C# 规则模拟，包含攻击前摇与延迟命中；不能据此推断 Unity 内实际帧率、辉光观感、音效与鼠标交互已经通过。')
p.write_text(s,encoding='utf-8')
