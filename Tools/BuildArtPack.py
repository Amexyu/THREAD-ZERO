from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import csv, hashlib, json, shutil, struct

root=Path(__file__).resolve().parent.parent
pack=root/'Deliverables/ArtPack_v07'
runtime=pack/'Runtime'
review=pack/'Review'

# Assemble whole-frame render previews, without changing the source artwork.
out=bytearray();frame_count=0
for n,p in enumerate(sorted((review/'gif_frames').glob('*.gif'))):
    b=p.read_bytes();packed=b[10];table=b[13:13+3*2**((packed&7)+1)] if packed&128 else b''
    offset=13+len(table)
    if n==0:out+=b[:13]+table+b'\x21\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00'
    while b[offset]!=0x2c:
        assert b[offset]==0x21
        offset+=2
        while b[offset]:offset+=1+b[offset]
        offset+=1
    desc=bytearray(b[offset:offset+10]);offset+=10
    if desc[9]&128:
        length=3*2**((desc[9]&7)+1);local=b[offset:offset+length];offset+=length
    else:local=table;desc[9]=(desc[9]&0x78)|0x80|(packed&7)
    data=bytearray(b[offset:offset+1]);offset+=1
    while b[offset]:
        length=b[offset];data+=b[offset:offset+length+1];offset+=length+1
    data+=b'\0'
    out+=b'\x21\xf9\x04\x04'+struct.pack('<H',8)+b'\x00\x00'+desc+local+data
    frame_count+=1
out+=b'\x3b'
assert frame_count==24
(review/'ConnectedFrames_Preview.gif').write_bytes(out)

measurements=list(csv.DictReader((pack/'measurements.tsv').open(encoding='utf-8'),delimiter='\t'))
assert len(measurements)==36
poses=['idle','ranged_aim','ranged_fire','melee_ready','melee_windup','melee_strike']
asset_records=[]
for row in measurements:
    p=runtime/row['path'];assert p.is_file()
    b=p.read_bytes();assert b[:8]==b'\x89PNG\r\n\x1a\n'
    assert struct.unpack('>II',b[16:24])==(512,512)
    asset_records.append({'id':row['id']+'.'+row['pose'],'kind':row['kind'],'file':'Runtime/'+row['path'],
                          'canvasSize':[512,512],'pivotPx':[256,470],
                          'sourceOpaqueBounds':[int(row['source'+a]) for a in ['X','Y','W','H']],
                          'sourceFeetPx':[float(row['feetSourceX']),float(row['feetSourceY'])],
                          'uniformScale':float(row['scale']),'sha256':hashlib.sha256(b).hexdigest()})
icons=[]
for slot in ['w','h','c','b']:
    for i in range(5):
        p=runtime/'Icons'/f'{slot}{i}.png';b=p.read_bytes();assert struct.unpack('>II',b[16:24])==(128,128)
        icons.append({'templateId':f'{slot}{i}','file':f'Runtime/Icons/{slot}{i}.png','use':'icon_only','sha256':hashlib.sha256(b).hexdigest()})
manifest={'version':'0.7','status':'integration_candidate','runtimeIntegrated':False,'uiFilesModified':False,
          'rendering':'whole_frame_swap','supportsIndependentFourSlotMixing':False,
          'playerLookIds':['c0','c1','c2','c3','c4'],'playerPoseOrder':poses,
          'playerAtlas':{'columns':3,'rows':2,'cellSize':[512,512],'textureSize':[1536,1024]},
          'coordinates':'top_left_pixels','feetPivotPx':[256,470],'unityPivotNormalized':[.5,.08203125],
          'reviewed':{'lightAndDarkAlpha':True,'opaqueBorderClipping':0,'neighborFootContaminationCorrected':True,
                      'enemyHammerSamplingCorrected':True,'wholeFramePreviewFrameCount':24},
          'missing':['independent_weapon_head_back_wearable_layers','full_inbetween_animation_frames','enemy_attack_frames','dedicated_hit_and_repair_frames'],
          'assets':asset_records,'icons':icons}
(pack/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')

# Install new image resources only. Never overwrite UI code or activate the sample adapter.
resources=root/'ThreadZero/Assets/Resources/Art/V07'
for folder in ['Atlases','Enemies','Icons']:
    for p in sorted((runtime/folder).glob('*.png')):
        dest=resources/folder/p.name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
shutil.copy2(pack/'manifest.json',resources/'manifest.json')

handoff=root/'CLAUDE_ART_HANDOFF.md'
handoff.write_text('''# Claude：美术接入交接 v0.7

用户要求保留你设计的 UI。本会话没有修改 GameApp*.cs、UiKit.cs、UiNavigation.cs 或现有角色/战斗代码。

请先读 `Deliverables/ArtPack_v07/CLAUDE_READ_FIRST.md` 和 `manifest.json`。

新图片已放入 `ThreadZero/Assets/Resources/Art/V07/`。完整交付包为 `Deliverables/THREAD_ZERO_ArtPack_v07.zip`。

三十个人物完整关键姿势帧、六种敌人、二十件物品图标，均有独立 PNG；五套人物还提供标准图集。用整帧替换旧 PuppetGeometry 的肢体/袖片旋转，避免再次散架；绘制参考在 `Deliverables/ArtPack_v07/Integration/WholeFrameAssets.cs`，它未自动安装，也未更改你的 UI。

重要边界：这是稳定整帧替换素材，不是已经完成四槽自由混搭的分层素材；独立头饰/背部/各武器穿戴层和完整中间帧仍由素材会话补齐，不要求你画。不要删减四槽玩法或存档来适配暂缺的素材。

角色使用右朝向，敌人左朝向；统一画布 512×512，脚底像素 (256,470)。不要再次裁 SourceAtlases 的等宽网格，部分武器/脚跨格；Runtime 中已经修正并对齐。

请只做角色、怪物绘制和攻击事件接线，保留你的 UI。接入后把实际画面/视频及使用的 assetId 返回给用户，由素材会话继续修图。当前没有宣称已完成 Unity 内验收。
''',encoding='utf-8')
(pack/'IntegrationStatus.md').write_text('''# 接入状态

- 素材生成、整帧提取、脚底对齐、静帧与浅/深底检查完成。
- 30 人物帧、6 敌人、20 图标；标准人物图集 5 张。
- 新增资源已放入 Resources/Art/V07，旧资源和 Claude UI 保留。
- 示例绘制代码未安装；游戏仍需 Claude 接线后才会使用本包。
- 六关键姿势不等于完整中间帧；整套外观不等于四槽自由混搭。
''',encoding='utf-8')
hashes=[]
for p in sorted(pack.rglob('*')):
    if p.is_file() and 'gif_frames' not in p.parts and p.name!='SHA256SUMS.txt':
        hashes.append(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.relative_to(pack).as_posix())
(pack/'SHA256SUMS.txt').write_text('\n'.join(hashes)+'\n',encoding='utf-8')
archive=root/'Deliverables/THREAD_ZERO_ArtPack_v07.zip'
with ZipFile(archive,'w',ZIP_DEFLATED) as z:
    for p in sorted(pack.rglob('*')):
        if p.is_file() and 'gif_frames' not in p.parts:z.write(p,'ArtPack_v07/'+p.relative_to(pack).as_posix())
    z.write(handoff,'CLAUDE_ART_HANDOFF.md')
with ZipFile(archive) as z:assert z.testzip() is None
print('Validated: 30 player frames, 6 enemies, 20 icons; 31 PNG resources installed; UI unchanged by this script')
print('Preview frames:',frame_count)
print('Zip bytes:',archive.stat().st_size)
