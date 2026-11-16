# ArtPack v13 · S0 风格候选回执

日期：2026-11-16。依据最新提交 `4d3bc9f` 的 `Docs/给素材会话的任务清单_v0.13_世界观UI与全套素材.md`。本轮仅完成 **S0候选图**，尚待用户确认；未进入S1分层部件制作，也未改Unity脚本或实际UI。

## 交付文件

| 文件（位于Review） | 实际尺寸 | 内容与状态 |
|---|---|---|
| `s0_character.png` | 640×640 RGBA | 灰发遮面、短身大头的新角色候选；有效Alpha轮廓高500px；透明背景 |
| `s0_battle_scale.png` | 1280×680 | 角色210px、普通敌180px、精英占位215px、首领占位245px；同脚底基线；下排64px缩略检查 |
| `s0_action_ui.png` | 1600×900 | 维生／回收终端方向的行动页皮肤提案；保留竖栏、路线、舞台和底部配装控制的布局关系 |
| `s0_character_alpha_qa.png` | 1280×640 | 角色在浅底#E8E9E4与深底#1C1F22上的检查图 |
| `s0_character_source.png` | 1173×1341 RGBA | 生成原稿；有效Alpha包围盒[142,25,816,1308] |
| `s0_action_ui_source.png` | 1672×941 | 最终皮肤提案的生成原稿 |
| `s0_measurements.json` | JSON | 导出画布及有效Alpha高度的实际量测记录 |

量测以Alpha>8为有效轮廓。角色和三个占位敌的高度已分别校验为210/180/215/245像素；不是仅将文件画布高度写成这些数字。角色图的头占比以50%～55%为设计目标；部件边界、颈部锚点与头部占比的严格部件量测为TBD，留到S1处理，不将视觉估计冒充实际分层数据。

## 本轮设计

- 角色：保留灰发、暗色面屏和呼吸器、石墨黑夹克、浅灰靴、肩部灰白护片、低饱和青绿、小面积橙色。身体更短、头更大；呼吸管收进领口，不连向可替换背包。当前为默认露发造型提案，S1要求的h0兜帽部件尚未绘制。
- UI：浅色纸质诊断面板、ECG、检疫封条、少量铆钉与青绿接口，警戒橙仍用于操作和封锁；不编造病名、组织名、人物或剧情文字。
- 界面稿里的文字、数值、道具图标及敌人仅作皮肤说明，不作为正式文案、游戏数值或已交付独立资源。角色在界面稿中的呈现仍为示意；精确战斗大小以 `s0_battle_scale.png` 为准。
- 尺寸对照图直接使用V07旧敌人作占位，包含旧蓝青灯色和旧画风；**不代表新敌人已定稿**。S3重绘时遵守橙色敌方标识、大块面Q化、朝左和idle/windup/wreck三态要求。

## 未做与下一步

- S1六个独立部件、parts.json、锚点、肩点、关节重叠检查及三份动作GIF：**尚未制作**。待S0角色比例与皮肤方向确认后，从部件开始绘制，不能裁这张整身图冒充可用分层。
- UI九宫格边距、状态切片与kit.json：TBD，S2制作。S0整张界面稿不能当作可用UI Kit直接铺进游戏。
- 原型校验工具已读过，S0没有parts.json，因此没有声称 `validate_parts.py` 已通过。本机默认Python尚无Pillow/numpy；S1运行前需准备这两项依赖。
- 更高稀有度的独特技能动作与特效要求保留；同锚点的外观变体不替代专属动作。S7过场/标题与S8机械化阶段依赖后续故事，暂缓。

三张候选图另复制到 `ThreadZero/Assets/Resources/Art/V13/Review`，仅供程序方查看，没有运行绘制入口、没有覆盖V07/V08/V12。S0其余原稿与检查图留在Deliverables。

复现导出：`powershell -NoProfile -ExecutionPolicy Bypass -File Tools/ExportArtV13S0.ps1`。此脚本只做图稿导出、Alpha量测及尺寸对照，不生成创意内容，不修改角色动作或游戏UI。

## 2026-11-16 · 中断后重新核对与提交确认

本次以 Windows 工作副本 `F:\毕业制作\THREAD-ZERO` 为准，依次重新阅读 v0.13 唯一有效任务清单、CharacterVisualBaseline_v12、v0.12 的流程与画法及 parts.json 约定，随后查看 rig_idle.gif（14 帧）与 rig_attack.gif（20 帧）的起始和中间关键帧。未依赖旧对话判断完成状态。

仓库中已存在上述 S0 图稿，本次沿用已有文件，没有重新生成或冒称为本次新绘。当前 HEAD 为 `c603efd`；上文 `4d3bc9f` 是原回执记载的依据版本，本次没有将其视作当前 HEAD。

| 项目 | 本次核对结果 | 进度 |
|---|---|---|
| S0① 角色基准与战斗尺寸对照 | 已目视检查角色朝右、大头短身造型；PNG 实测分别为 640×640 和 1280×680；210/180/215/245 的轮廓高度见既有量测记录 | 图稿已保存，待用户确认 |
| S0② 行动页皮肤 | 已目视检查路线条、生命体征、战斗舞台、底部配装与控制；PNG 实测 1600×900 | 图稿已保存，待用户确认 |

三张 Review 成图与 Assets/Resources/Art/V13/Review 下对应文件的 SHA256 一致。`git diff HEAD --name-only -- ThreadZero/Assets/Scripts` 返回空；本次未修改该目录。

验收边界：行动页是皮肤方向候选，画面内角色大小与左栏宽度仍是示意，不能据此声称已严格实现 210px 角色及 128 逻辑像素左栏；后续接入须按规范定值。尺寸对照中的旧敌人仍只用于大小比较。部件锚点、切片数据保持 TBD，不进入 S1，等待用户对三张 S0 图的确认。


- S1 head：Character/starter/head.png，100×90 单位，pivot=(50,82)；独立部件 RGBA、固定平色板。

- S1 torso：Character/starter/torso.png，60×44 单位，pivot=(30,40)；独立部件 RGBA、固定平色板。

- S1 arm：Character/starter/arm.png，28×40 单位，pivot=(14,9)；独立部件 RGBA、固定平色板。

- S1 hand：Character/starter/hand.png，18×18 单位，pivot=(9,9)；独立部件 RGBA、固定平色板。

- S1 pack：Character/starter/pack.png，34×50 单位，pivot=(28,33)；独立部件 RGBA、固定平色板。

- S1 leg_near：Character/starter/leg_near.png，34×30 单位，pivot=(12,6)；独立部件 RGBA、固定平色板。

- S1 leg_far：Character/starter/leg_far.png，34×30 单位，pivot=(12,6)；独立部件 RGBA、固定平色板。

- S1 weapon：Character/starter/weapon.png，22×84 单位，pivot=(11,9)；独立部件 RGBA、固定平色板。
- S1 配置：Character/starter/parts.json（scale=4、weapon_class=dagger；独立 hand、leg_near、leg_far；arm.hand=(14,29)，weapon.tip=(11,81)）。8 件尺寸/锚点符合开工单，2px 透明边全通过，arm/双腿/握把中心 ±3 单位区域实测 100% 不透明；实测存 Review/S1/measurements.json。
- S1 检查点：八部件与配置已落盘；Review/S1/assembly.png 为另行拼装的三个静态关节姿势，已目视检查。原 validate_parts.py / rig.py 均已执行但分别缺 Pillow / NumPy 而失败，日志为 Review/S1/validate_parts.log、rig.log；依赖下载 DNS 失败。原脚本也仍要求 legs、未使用独立 hand/双腿，所以不能声称原骨架验收通过；待程序方升级脚本并补齐依赖后重跑。未进入 S2，未改程序脚本。

- S1 修订 head：Character/starter/head.png，84×76 单位，pivot=(42, 68)；按审核调整比例；保持原造型与平色。

- S1 修订 torso：Character/starter/torso.png，60×50 单位，pivot=(30, 46)；按审核调整比例；保持原造型与平色。

- S1 修订 leg_near：Character/starter/leg_near.png，34×34 单位，pivot=(12, 6)；按审核调整比例；保持原造型与平色。

- S1 修订 leg_far：Character/starter/leg_far.png，34×34 单位，pivot=(12, 6)；按审核调整比例；保持原造型与平色。

- S1 修订 weapon：Character/starter/weapon.png，14×40 单位，pivot=(7, 6)；按审核调整比例；保持原造型与平色。

- S1 修订 arm：Character/starter/arm.png，28×40 单位，pivot=(14, 9)；袖口加青绿识别条。

- S1 修订 torso 补正：移除两侧外露的空袖洞圆环，改为连续夹克布面；仍为60×50单位、pivot=(30,46)，避免背侧手臂被躯干遮挡后出现空洞。
- S1 审核修订完成（2026-11-16）：parts.json 已写入真实直立拼装 rig：hip=(-5,-26)、head=(0,-47)、pack=(-27,-26)、leg_near=(-11,0)、leg_far=(11,0)；shoulder=(-22,-26)、shoulderF=(20,-26)。idle_rest.png 使用 rig.py 原矩阵、全零角度、无武器，两臂自然下垂，无另藏的拼装位置。
- 修订比例基准：head=84×76、torso=60×50、双腿=34×34、weapon=14×40 单位；站立实测高140.33，头轮廓高72占51.3%、匕首轮廓高36占25.7%。Review/S1/proportions.json 固化基准；后续 h1–h4/c1–c4/b1–b4 及武器统一照此比例与锚点制作，不自动填满相同画框。
- 验收已重跑通过：8部件尺寸/锚点/透明边/关节，额外3单位重叠区全不透明；原 rig.py 成功生成 idle(14帧)/walk(14帧)/attack(20帧)。Review/S1/idle_rest.png、assembly.png、measurements.json、proportions.json 与三份日志均已更新；已目视检查站立和动作合图。此前缺依赖/旧字段的阻塞记录已解除。
- 本机 Pillow、NumPy 已经离线安装到项目旁 runtime_deps；重跑命令：powershell -ExecutionPolicy Bypass -File Tools/RigPrototype/RunStarterReview.ps1。validate_parts.py 新增显式 s1_revised 比例档以接受审核要求的小头/短匕首，旧默认样例A仍校验通过；没有修改程序方 rig.py 或 ThreadZero/Assets/Scripts，未进入S2。
