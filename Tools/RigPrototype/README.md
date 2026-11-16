# RigPrototype（分层部件 + 程序驱动动作 原型）

- `rig.py`：渲染器。内置一套占位部件（A 轻装短刃 / B 重装长枪 / C 混搭巨锤），也能读取素材方交付的 `parts.json`。
  - `python rig.py <输出目录>` 渲染内置三套的 待机/走路/攻击 GIF。
  - `python rig.py <输出目录> A/parts.json B/parts.json ...` 渲染交付的素材（每个 manifest 一列）。
  - `python -c "import rig; rig.export_selftest('sample_parts')"` 重新导出参考样例。
- `validate_parts.py`：交付前校验。`python validate_parts.py <set>/parts.json`，不通过不要交。
- `sample_parts/{A,B,C}/`：参考样例（占位几何图形，通过校验）。**只参考尺寸/锚点/重叠边，不参考画风。**
- `rig_idle.gif` `rig_walk.gif` `rig_attack.gif`：用样例部件渲染的效果（三套共用同一套关键帧）。

依赖：Python 3 + Pillow + numpy。规范见 `Docs/给素材会话的任务清单_v0.12_分层部件.md`。
