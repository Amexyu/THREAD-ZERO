# 卡48：rig 3 的几何（和游戏里 LayeredRigViewKid3.Build3 同一套约定），给攻击动作求解用。
# 角度：0 = 竖直朝下，90 = 水平朝前，180 = 朝上，270 = 朝后（往前转为正）。y 向下为正，单位 = 部件单位（游戏 PixelsPerUnit 换算）。
import json, math, os, numpy as np
from PIL import Image
ROOT = os.path.expanduser('~/mnt/THREAD-ZERO')
K3 = ROOT + '/ThreadZero/Assets/Resources/Art/V17/Kid3'
M = json.load(open(K3 + '/parts.json'))
V = lambda a: np.array(a, float)
def ang(v): return math.degrees(math.atan2(v[0], v[1]))
def rot(v, a):   # 和 Rm 一样：正 = 往前转
    a = math.radians(a); c, s = math.cos(a), math.sin(a); v = np.asarray(v, float)
    return np.array([c * v[0] + s * v[1], -s * v[0] + c * v[1]])
HIP = V(M['rig']['hip']); LEGN = V(M['rig']['leg_near']); LEGF = V(M['rig']['leg_far'])
SH = V(M['shoulder']); SHF = V(M['shoulderF']); HEAD = V(M['rig']['head'])
lj = M['rig3']['leg']['joints']; sj = M['rig3']['sleeve']['joints']
LV1 = V(lj[2:4]) - V(lj[0:2]); LV2 = V(lj[4:6]) - V(lj[2:4]); SV1 = V(sj[2:4]) - V(sj[0:2]); SV2 = V(sj[4:6]) - V(sj[2:4])
L1, L2 = float(np.linalg.norm(LV1)), float(np.linalg.norm(LV2)); A1, A2 = float(np.linalg.norm(SV1)), float(np.linalg.norm(SV2))
LR1, LR2 = ang(LV1), ang(LV2); SR1, SR2 = ang(SV1), ang(SV2)
HAND_GRIP = V(M['parts']['hand']['grip']) - V(M['parts']['hand']['pivot'])
WP = M['parts']['weapon']; WGRIP = V(WP['grip']) - V(WP['pivot']); WTIP = V(WP['tip']) - V(WP['pivot'])
# 鞋：轮廓（相对脚踝挂点）
_fp = M['parts']['foot']; _im = np.asarray(Image.open(K3 + '/' + _fp['file']).convert('RGBA'))[..., 3] > 128
_ys, _xs = np.nonzero(_im); SC = M['scale']
FOOT_SIL = np.stack([(_xs + .5) / SC - _fp['pivot'][0], (_ys + .5) / SC - _fp['pivot'][1]], 1)[::7]
SOLE = float(FOOT_SIL[:, 1].max())
def foot_low(pitch):   # 鞋按 pitch（往前转为正 = 鞋尖抬起）转了以后，最低点比脚踝低多少；还有最低点的 x
    p = np.array([rot(q, pitch) for q in FOOT_SIL]); i = int(np.argmax(p[:, 1])); return float(p[i, 1]), float(p[i, 0])
def ik2(target, base, l1, l2, bend=+1):
    """两节 IK：返回第一节、第二节的世界角。bend=+1：关节往前弯（膝盖 / 第二节相对第一节往前转为正 = 屈）。"""
    d = np.asarray(target, float) - base; dist = float(np.linalg.norm(d)); dist = min(max(dist, abs(l1 - l2) + 1e-4), l1 + l2 - 1e-4)
    phi = ang(d)
    al = math.degrees(math.acos(max(-1, min(1, (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist)))))
    be = math.degrees(math.acos(max(-1, min(1, (l2 * l2 + dist * dist - l1 * l1) / (2 * l2 * dist)))))
    return (phi + al, phi - be) if bend > 0 else (phi - al, phi + be)

# 远侧手握孔、各类武器的几何（单位，相对武器挂点；挂点 = 近侧手握的地方）
HAND_GRIP_F = V(M['parts']['hand_far'].get('grip', [7.0, 8.8])) - V(M['parts']['hand_far']['pivot'])
_PARTS = ROOT + '/ThreadZero/Assets/Resources/Art/V13/Parts/'
def weapon_geo(kind):
    if kind == 'dagger': return dict(grip=WGRIP, tip=WTIP, grip2=None, gamma=80., box=None)
    pid = {'hammer': 'w4', 'pole': 'w5'}[kind]; d = json.load(open(_PARTS + pid + '/part.json')); wp = d['weapon']
    piv = V(wp['pivot']); tip = V(wp['tip']) - piv
    axis = tip / float(np.linalg.norm(tip))
    g2 = axis * (-9.0 if kind == 'hammer' else 22.0)   # 锤：另一只手在柄尾（挂点往后 9）；长杆：前手在挂点往前 22
    box = (15.9, 8.75) if kind == 'hammer' else None   # 锤头方块的半宽（垂直于柄）/ 半高（沿柄），量自 w4 的图
    return dict(grip=np.zeros(2), tip=tip, grip2=g2, gamma=80., box=box)
