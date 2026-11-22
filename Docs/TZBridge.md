# TZ Bridge：让 Claude 自己编译、跑测试、截图

Unity 编辑器开着时，Claude（以及它派出的子 agent）只靠读写文件就能遥控编辑器：触发编译并读报错、进入 Play、按剧本操作游戏、截图、读游戏里的数值。Win 和 Mac 通用，代码都在仓库里，拉下来就能用。

- 编辑器插件：`ThreadZero/Assets/Editor/TZBridge/TZBridge.cs`（单独的 asmdef：`TZBridge.Editor`）
- 运行时执行器：`ThreadZero/Assets/Scripts/Dev/TZAutoTest.cs`（没有剧本时什么都不做）
- 通信目录：`ThreadZero/Logs/TZBridge/`（被 git 忽略）

## 通信目录

| 路径 | 作用 |
|---|---|
| `inbox/<单号>.txt` | 指令文件。插件每 0.5 秒看一次，按文件名顺序一次执行一条，执行后移到 `inbox/done/` |
| `inbox/STOP` | 立刻停止正在跑的剧本 |
| `out/<单号>/result.txt` | 结果。第二行固定是 `status ok / error / fail / timeout / aborted` |
| `out/<单号>/log.txt` | Play 期间的全部日志（报错带堆栈） |
| `out/<单号>/*.png`、`*.json` | 截图和 dump |
| `status.txt` | 心跳，约 2 秒刷新：是否在 Play、是否在编译、有没有编译错误、上一条指令 |
| `console.txt` | 编辑器最近 300 条日志 |
| `compile_errors.txt` | 最近一次编译的错误（没有错误时为空） |
| `compile_warnings.txt` | 最近一次编译的警告原文（`compile` 的结果里只有个数） |

单号建议用 `时间_用途`，比如 `20261121-1630_ch2smoke`，这样文件名顺序就是执行顺序。

插件每次加载时自动清理：`out/` 只留最近 40 次的结果，`inbox/done/` 只留最近 200 条指令。

## 指令（指令文件的第一行）

| 指令 | 作用 |
|---|---|
| `ping` | 回报编辑器状态 |
| `compile` | 刷新资源并重新编译。成功：`status ok`；失败：`status error` 加完整错误列表 |
| `refresh` | 只刷新资源（导入新图） |
| `play` | 进入 Play，按后面的剧本执行，跑完自动退出 Play |
| `menu <菜单路径>` | 执行编辑器菜单，比如 `menu Thread Zero/Prepare Main Scene` |

## 剧本（`play` 后面的每一行）

剧本开头可以写设置：

| 设置 | 作用 |
|---|---|
| `timeout 90` | 整个剧本最多跑多少秒（默认 120） |
| `fresh` | 先清空测试存档，从新档开始 |
| `save main` | 用正式存档（默认用测试存档，跑完自动切回原来的存档槽） |

步骤：

| 步骤 | 例子 | 说明 |
|---|---|---|
| `wait <秒>` | `wait 3` | 按真实时间等待 |
| `frames <帧数>` | `frames 10` | 等若干帧 |
| `key <按键> [shift/ctrl/alt]` | `key F5`、`key Alpha3`、`key F6` | 模拟按键（KeyCode 名称）。事件直接交给 GameApp.OnGUI，Unity 在后台也有效 |
| `click <x> <y>` | `click 48 300` | 在屏幕像素坐标点一下（左上角为原点，和截图坐标一致），同样直接交给 GameApp.OnGUI |
| `call <路径> [参数…]` | `call ChangeRegion 9`、`call Go Wardrobe` | 调用 GameApp 或它字段上的方法，参数自动转换（数字、true/false、枚举名） |
| `set <路径> <值>` | `set speed 4`、`set autoAdvance false` | 写字段或属性 |
| `get <路径>` | `get model.state.region` | 把值记到结果里 |
| `expect <路径> <比较> <值>` | `expect navigation.Screen == Wardrobe` | 断言，失败记 FAIL。比较：`== != > < >= <= has` |
| `waitfor <路径> <比较> <值> [最长秒数]` | `waitfor combat.StageCleared == true 60` | 等条件满足，超时记 FAIL |
| `shot <名字> [倍数]` | `shot 01_stage` | 截图存到 `out/<单号>/01_stage.png` |
| `burst <名字> <张数> <间隔秒> [宽度]` | `burst fx 36 .08 800` | 连拍缩小的 jpg（`fx_000.jpg`…），拼 GIF 看动效 |
| `dump <路径> [文件名]` | `dump model.state save` | 把对象转成 JSON 存起来 |
| `log <文字>` | `log 开始测第二章` | 在结果里留一行备注 |
| `end` | | 提前结束 |

路径从 GameApp 出发：`model`、`combat`、`navigation`、`speed`、`paused` 这些都是 GameApp 的字段（私有的也能访问）。也可以从类型出发访问静态成员：`GameModel.TestPower`、`Catalog.LastRegion`、`Time.timeScale`。列表和数组用 `[序号]`：`model.state.items[0]`。带空格的参数用双引号括起来。`#` 开头的行是注释。

关卡序号：`region` 0–6 是第一章 1-1 到 1-7，7–13 是第二章 2-1 到 2-7，14–20 是第三章 3-1 到 3-7，21–27 是第四章，28–34 是第五章（第 c 章第 s 关 = (c−1)×7 + s−1）。

单号：收件箱按文件名排序处理，**新单号要排在上一个单号后面**（同一天用 `-a…-z`、`-zz…`、`-zzz_a…` 往后排）。`shot` 的第二个参数是放大倍数，不是宽度。

测试用的钩子（v0.19）：
- `set slowMo 0.2`：战斗慢放（`speed` 是整数，不能填小数；`Time.timeScale` 对战斗不管用）。录技能动作：先 `waitfor combat.SkillReadiness >= 0.85 30`，再 `set slowMo 0.2`、`waitfor combat.DaggerActive == true 30`、`burst …`。
- `call TestEquip w4`：换上某把武器（特装 30 级）。`call combat.MakeTestDummy`：怪不掉血不打人。
- `call TestReforgeItem` / `call TestReforgeOnce`：工坊里给一件史诗武器、锁第一条、洗一次。
- 数值测试自动重跑已关（调数值时手动跑）：`menu Thread Zero/数值测试/立即运行`。自动重跑有时会用编译前的旧代码跑，手动跑最稳。

数值测试：`menu Thread Zero/数值测试/立即运行` 跑一遍主线模拟，报告在 `Docs/数值测试报告.md`（数值相关脚本改了以后编译完也会自动跑，见 `ThreadZero/Assets/Editor/Balance/BalanceSim.cs`）。

## 例子：第二章冒烟测试

`inbox/20261121-1630_ch2smoke.txt`：

```
play
timeout 90
fresh
wait 3
shot 00_start
key F5
key F6
call ChangeRegion 7
wait 4
shot 01_stage2-1
set speed 4
wait 15
shot 02_fight
expect model.state.region == 7
call Go Wardrobe
wait 1
shot 03_wardrobe
call Go Action
end
```

跑完看 `out/20261121-1630_ch2smoke/result.txt`，再看截图和 `log.txt` 里有没有报错。

## Claude 怎么用

1. 指令文件先写到会话的 `/mnt/user-data/outputs/`，再用 `device_commit_files` 写进电脑上的 `ThreadZero/Logs/TZBridge/inbox/`。
2. 隔几秒用 `device_stage_files` 取 `out/<单号>/result.txt`，没有就再等；`status.txt` 能看出插件是不是活着。
3. 结果里的截图用 `device_stage_files` 取回来看。
4. 编译不过时插件可能还没加载（第一次装插件、项目本来就有错）。这时 `status.txt` 不会更新，要看 Unity 的 Editor.log，或者让人把报错发过来。

## 注意

- Unity 编辑器要开着这个项目。插件只在没人用 Play、没在编译时才接新指令；你自己在 Play 时它会等你退出。
- 指令在后台执行慢的话：Unity 的 Preferences → General → Interaction Mode 改成 No Throttling。
- 截图截的是 Game 视图，Game 视图不能被别的标签页挡住。分辨率建议设成 1920×1080。
- 默认用测试存档，跑完切回原来的存档槽，不碰正式存档。
- 打包版本也能跑剧本：启动参数加 `-tzAutotest <剧本文件>`，结果写到剧本里 `out` 指定的文件夹（没写就放在存档目录的 `tzautotest/` 下）。按键和点击在打包版本里也能用。
