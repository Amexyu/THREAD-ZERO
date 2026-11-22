# ArtArchive（不进包的图）

v0.20（2026-11-22）从 `Resources/Art/` 挪出来的图。放在 `Resources` 里的东西不管用没用到都会打进正式包，这几组游戏里没有代码读取（挪之前全仓库搜过路径）：

| 文件夹 | 是什么 | 原来的位置 |
|---|---|---|
| `V13/Review` | v0.13 审图截图 | `Resources/Art/V13/Review` |
| `V14/Review` | v0.14 第一章审图（场景、可读性） | `Resources/Art/V14/Review` |
| `V16/StageProto` | v0.16 第一章背景样板（正式图出来前的测试版） | `Resources/Art/V16/StageProto` |
| `V08/EquipmentIcon` | v0.8 旧装备图标（现在图标用 V13 部件，见 PaperDollView） | `Resources/Art/V08/EquipmentIcon` |

要用哪组，在 Unity 里把文件夹拖回原来的位置就行（`.meta` 跟着走，引用不会断）。`StageArt.ProtoFolder` 那个测试开关要用样板时，得先把 `StageProto` 挪回去。
