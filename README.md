# 零织协议 / THREAD ZERO

> **当前协作基线 v0.6（2026-11-15）**：本会话负责全部美术与素材质量，另一 AI 负责程序和引擎集成。用户已否决 v0.5 六部件硬切衣服的拼装动画；下方旧进度不代表视觉已验收。请先读 [完整企划书](Docs/THREAD_ZERO_完整企划书.md) 和 [协作接口](Docs/AI协作交接与素材接口.md)。

Windows 单机机能风换装与挂机刷宝毕业制作。

- [完整企划](Docs/GameDesign.md)
- [制作进度](Docs/Progress.md)
- [运行与测试说明](Docs/RunGuide.md)
- [装备数值表](Docs/EquipmentCatalog.md)
- [设备联动与定向回收](Docs/Differentiation.md)
- [品质、属性与辉光](Docs/QualityStatsGlow.md)
- [自动数值模拟报告](Docs/BalanceReport.md)
- [首套分层素材与动画方案](Docs/ArtAnimation.md)
- [界面重构](Docs/UiRedesign.md)
- Unity 工程：`ThreadZero/`
- 编辑器：`C:\Program Files\Unity\Hub\Editor\6000.6.9f1\Editor\Unity.exe`

核心：自动战斗、四槽可见换装、随机品质与词条、收藏幻化、Boss 掉落、本地存档。

项目以后台构建和测试推进，不使用桌面自动化。

目前：v0.5，浅色机能工坊 UI、20 件装备贴图、六部件攻击动作与延迟命中。161 项逻辑及存储检查通过，Unity 6000.6.9f1 运行时/编辑器源代码编译通过。

- [本轮视觉与动作说明](Docs/VisualUpdate-v0.5.md)
- 连续动作预览：Artifacts/AttackAnimationV5.gif（与游戏共用姿态及贴图布局，非 Unity 运行录屏）。
- 界面预览：Artifacts/ActionUiV5Review.png（离线布局复核）。

尚待现有 Unity 编辑器刷新后的实际视觉、交互与存档验收；未生成新版 Windows 可执行包。旧预览与文档历史条目请以 v0.5 说明为准。
