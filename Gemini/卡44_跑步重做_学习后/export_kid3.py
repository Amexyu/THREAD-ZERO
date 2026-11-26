# 把检查过的跑步（run_ref.py）导出成游戏用的表 + 装好 Resources/Art/V17/Kid3（rig_version 3：整条腿 / 整条袖子网格变形）
import sys, os, json, math, shutil, uuid, numpy as np
sys.argv = ['x', '部件A4', 'out']
import run_ref as R
from PIL import Image
RES = os.path.expanduser('~/mnt/THREAD-ZERO/ThreadZero/Assets/Resources/Art/V17')
DST = os.path.join(RES, 'Kid3'); os.makedirs(DST, exist_ok=True)
N = 240
tab = {k: [] for k in ('dy', 'lean', 'head', 'th0', 'k0', 'f0', 'th1', 'k1', 'f1', 'a0', 'e0', 'a1', 'e1')}
for i in range(N):
    p = R.pose(i / N)
    tab['dy'].append(p['dy']); tab['lean'].append(p['lean']); tab['head'].append(p['head'])
    for s in (0, 1):
        a_th, a_kn, A, st, th, K = p['legs'][s]; tab['th%d' % s].append(th); tab['k%d' % s].append(K); tab['f%d' % s].append(A)
        Arel, E = p['arms'][s]; tab['a%d' % s].append(Arel); tab['e%d' % s].append(E)
run = dict(n=N, stride=round(R.STRIDE, 4), stance=R.ST, note='卡44 run_ref.py 导出：每循环 %d 个采样；角度单位度，dy 单位 = 部件单位（y 向下）。th = 大腿相对竖直（往前为正），k = 膝弯，f = 鞋相对小腿；a = 上臂相对上身，e = 手肘弯。' % N)
for k, v in tab.items(): run[k] = [round(float(x), 3) for x in v]
json.dump(run, open(os.path.join(DST, 'run3.json'), 'w'), ensure_ascii=False)
# 部件：身体、头、手、鞋（部件A4：修过被抠掉的颜料点），条带（腿、袖子），武器沿用 Kid 的
for f in ('torso', 'head', 'hand', 'hand_far', 'foot', 'foot_far', 'pack'): shutil.copy(os.path.join('部件A4', f + '.png'), os.path.join(DST, f + '.png'))
shutil.copy('条带/leg.png', os.path.join(DST, 'leg.png')); shutil.copy('条带/sleeve.png', os.path.join(DST, 'sleeve.png'))
shutil.copy(os.path.join(RES, 'Kid', 'weapon.png'), os.path.join(DST, 'weapon.png'))
A3 = json.load(open('部件A4/parts.json')); KID = json.load(open(os.path.join(RES, 'Kid', 'parts.json'))); SM = json.load(open('条带/strips.json'))
P = A3['parts']; L = SM['leg']; Sl = SM['sleeve']
parts = dict(torso=P['torso'], head=P['head'], hand=P['hand'], hand_far=P['hand_far'], foot=P['foot'], foot_far=P['foot_far'], pack=P['pack'], weapon=KID['parts']['weapon'],
             # 旧代码要的部件名：指向整条袖子 / 整条腿（如果哪里还按旧骨骼画，至少是一条直的手臂 / 腿）
             arm=dict(file='sleeve.png', pivot=Sl['joints'][0], hand=Sl['joints'][2]), arm_far=dict(file='sleeve.png', pivot=Sl['joints'][0], hand=Sl['joints'][2]),
             leg_near=dict(file='leg.png', pivot=L['joints'][0], hand=L['joints'][2]), leg_far=dict(file='leg.png', pivot=L['joints'][0], hand=L['joints'][2]))
man = dict(scale=5.0, weapon_class='dagger', rig_version=3, far_shade=A3['far_shade'], bar_lift=A3['bar_lift'], proportion_profile='kid_standard_a4_skinned', parts=parts,
           shoulder=A3['shoulder'], shoulderF=A3['shoulderF'], rig=A3['rig'],
           rig3=dict(leg=dict(file='leg.png', joints=sum(L['joints'], []), cols=6, rows=64, r0=3.0, r1=R.KNEE_R),
                     sleeve=dict(file='sleeve.png', joints=sum(Sl['joints'], []), cols=8, rows=60, r0=2.6, r1=R.ELBOW_R), run='run3'),
           note='11-26 卡44：跑步按动画关键姿势 + 真人跑步数据重做（run3.json 是导出的表）；腿和袖子各是一整条贴图，用网格变形在膝盖 / 手肘处弯（圆角弯 + 内侧折痕），不再是两节拼件。')
json.dump(man, open(os.path.join(DST, 'parts.json'), 'w'), ensure_ascii=False, indent=1)
# meta：照 Kid 里的 png / json meta 抄，换新 guid（已有的保留原 guid）
pm = open(os.path.join(RES, 'Kid', 'torso.png.meta')).read(); jm = open(os.path.join(RES, 'Kid', 'parts.json.meta')).read()
import re
for f in os.listdir(DST):
    if f.endswith('.meta'): continue
    mp = os.path.join(DST, f + '.meta')
    if os.path.exists(mp): continue
    src = pm if f.endswith('.png') else jm
    open(mp, 'w').write(re.sub(r'guid: [0-9a-f]+', 'guid: ' + uuid.uuid4().hex, src, count=1))
print(sorted(os.listdir(DST)))
print('stride', run['stride'], 'dy', min(run['dy']), max(run['dy']))
