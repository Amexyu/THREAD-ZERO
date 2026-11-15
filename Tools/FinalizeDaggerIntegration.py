from pathlib import Path
import json
import re
import uuid

root=Path(__file__).resolve().parent.parent
assets=root/'ThreadZero/Assets'
new_files=[
 'Scripts/DaggerSkill.cs','Scripts/DaggerActorView.cs','Scripts/GameAppDaggerReview.cs',
 'Editor/DaggerReviewMenu.cs','Editor/DaggerIntegrationBuild.cs',
 'Resources/Art/V12/StarterDagger/Body.png','Resources/Art/V12/StarterDagger/Weapon.png']
texture_template=(assets/'Resources/Art/V08/Weapon/w1.png.meta').read_bytes().decode('utf-8-sig')
for relative in new_files:
 path=assets/relative
 meta=path.with_name(path.name+'.meta')
 if not meta.exists():
  guid=uuid.uuid4().hex
  content=re.sub(r'guid: [0-9a-f]+','guid: '+guid,texture_template,count=1) if path.suffix=='.png' else f'fileFormatVersion: 2\nguid: {guid}\n'
  meta.write_bytes(content.encode('utf-8'))
for relative in ['Resources/Art/V12','Resources/Art/V12/StarterDagger']:
 path=assets/relative;meta=path.with_name(path.name+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')

notice=('> 最新接入（2026-11-16）：用户授权将已认可的初始匕首动作接入程序。请先读 `Docs/DaggerRuntimeIntegration_v12.md`。'
        '新增透明两层18姿势样本与三段战斗事件，修改了角色绘制入口和CombatSession，但没有重做UI。'
        '本次只验证c0/默认头/b0/Common w1这一套，尚非四槽自由换装骨骼。高品质不复用初始技能。'
        '源代码编译、18项专项与161项模型检查通过；Unity后台启动因许可IPC失败退出，未完成实机截图或exe构建。\n\n')
p=root/'CLAUDE_ART_HANDOFF.md';text=p.read_bytes().decode('utf-8-sig')
if '最新接入（2026-11-16）' not in text:p.write_bytes(text.replace('\n','\n'+notice,1).encode('utf-8'))
p=root/'Deliverables/ArtPack_v12/Review/HANDOFF.md'
with p.open('a',encoding='utf-8') as f:f.write('\n\n## 用户认可与源码接入\n\n用户反馈十八姿势v2“嗯，还行”，作为初始匕首动作基准。随后明确要求制作并接入。运行样本、时间轴、隔离测试入口和真实限制见 `Docs/DaggerRuntimeIntegration_v12.md`；正式资源在 `Runtime/StarterDagger`。18项匕首专项与161项模型检查、运行时/编辑器源码编译均通过。旧模型测试关于开局四槽全穿戴的过时断言按已存在的默认露发空头槽行为更新，未改开局装备逻辑。后台Unity因许可客户端IPC失败退出，未产出新exe或实机截图，不能称引擎画面验收完成。\n')

p=root/'Docs/DaggerRuntimeIntegration_v12.md'
with p.open('a',encoding='utf-8') as f:f.write('\n## 本轮验证结果\n\n- 匕首专项18项、既有模型161项全部通过。\n- 当前Unity6000.6.9f1引用下的运行时与编辑器源码独立编译通过。\n- 透明图集alpha为0至255；共835386个全透明像素；浅深背景叠图见 `Deliverables/ArtPack_v12/Review/RuntimeLayers_QA.png`。\n- 后台Unity启动日志出现 `LicenseClient-Administrator refused`，许可IPC连接失败并退出1；未成功导入/打包或进行实机截图。日志为 `Artifacts/DaggerUnityBuild.log`。原始环境中正常启动Unity后仍需跑上述测试局确认。\n- 源码接入前备份在 `Backups/DaggerIntegration_20261116`。\n')
print('New asset metadata and handoff finalized')
