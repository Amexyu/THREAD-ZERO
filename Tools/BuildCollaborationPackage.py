from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import json

root=Path(__file__).resolve().parent.parent
docs=root/'Docs'
plan=docs/'THREAD_ZERO_完整企划书.md'
text=plan.read_text(encoding='utf-8')
if '## 附录 A：二十件装备' not in text:
    catalog=(docs/'EquipmentCatalog.md').read_text(encoding='utf-8')
    table=catalog[catalog.index('| ID |'):catalog.index('## 随机词条')]
    text+='\n\n## 附录 A：二十件装备\n\n以下为当前 Lv.1 普通品质模板基础值，以 Balance.cs 与 Catalog 为实现来源。此表描述玩法，不代表对应穿戴素材已经合格。\n\n'+table
    text+='\n## 附录 B：支持文件\n\n《AI协作交接与素材接口.md》负责工作边界、生产门槛与资源合同；EquipmentCatalog.md、QualityStatsGlow.md、Differentiation.md 为既有规则参考；BalanceReport.md 是纯规则模拟结果。旧文档中关于美术完成、旧拼装路线或旧运行环境的描述，以本企划第十二、十四章为准。\n'
    plan.write_text(text,encoding='utf-8')

manifest={
 'project':'THREAD ZERO','contractVersion':'0.6-draft','status':'draft',
 'assetOwner':'本会话：全部美术及质量检查','implementationOwner':'另一 AI：程序与引擎集成',
 'coordinateSystem':'top-left pixel coordinates',
 'proposedCanvasSize':[512,512],'measuredPivotPx':None,
 'requiredStates':['Idle','AttackRanged','AttackMelee','Hit','Repair'],
 'requiredAnchors':['feet','weaponGrip','muzzle','bladeTip','backModule'],
 'approvedAnimationAssets':[],
 'notes':['画布为建议值，实际锚点尚未量测。','完整六姿势图为 reference，不是连续帧动画。','旧六部件拼装路线已被用户否决。']
}
draft=docs/'AssetContract_DRAFT.json';draft.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
delivery=root/'Deliverables';delivery.mkdir(exist_ok=True)
archive=delivery/'THREAD_ZERO_AI_Collaboration_v06.zip'
selected=['THREAD_ZERO_完整企划书.md','AI协作交接与素材接口.md','AssetContract_DRAFT.json','EquipmentCatalog.md','QualityStatsGlow.md','Differentiation.md','BalanceReport.md']
with ZipFile(archive,'w',ZIP_DEFLATED) as z:
    z.writestr('START_HERE.txt','Read THREAD_ZERO_完整企划书.md, then AI协作交接与素材接口.md.\nReferenceCode contains current prototype source for inspection, not a complete importable Unity project.\nExisting puppet graphics were rejected by the user. The material session owns all artwork.\nNo saves, license logs or credentials are included.\n')
    for name in selected:z.write(docs/name,name)
    for folder in ['Scripts','Editor']:
        for p in sorted((root/'ThreadZero/Assets'/folder).glob('*.cs')):
            z.write(p,'ReferenceCode/'+folder+'/'+p.name)
    z.write(root/'Tests/ModelTests.cs','ReferenceCode/Tests/ModelTests.cs')
with ZipFile(archive) as z:
    assert z.testzip() is None
    assert all(not any(t in n.lower() for t in ['.b64','.log','savechecks','smokesave']) for n in z.namelist())
assert text.count('| w')>=5 and text.count('| h')>=5 and text.count('| c')>=5 and text.count('| b')>=5
print('Full plan characters:',len(text))
print('Package files:',len(z.namelist()))
print('Package bytes:',archive.stat().st_size)
