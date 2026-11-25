# 本地拼装预览：按 LayeredRigView 的骨骼链（Build / 画的顺序）把部件拼成一个静止的人，用来在进游戏前检查比例和接缝。
# 用法：python3 rig_preview.py out.png [head=h81] [coat=c81] [back=b81] [legs=l81] [weapon=w81] [--scale 3] [--bg 50,52,60]
import sys, os, json, math
import numpy as np
from PIL import Image
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')); ART = ROOT + '/ThreadZero/Assets/Resources/Art/V13/'   # 本脚本在 Gemini/卡32_三品牌实装部件/
M = json.load(open(ART + 'Starter/parts.json'))

def V(a, fb): return np.array(a[:2], float) if a and len(a) >= 2 else np.array(fb, float)
class Part:
    def __init__(self, folder, e, scale):
        self.im = Image.open(folder + e['file']).convert('RGBA'); self.scale = scale
        self.pivot = V(e.get('pivot'), (0, 0)); self.size = np.array(self.im.size, float) / scale
def load(folder, e, scale):
    if not e or not e.get('file') or not os.path.exists(folder + e['file']): return None
    return Part(folder, e, scale)
S = M['scale']; SP = M['parts']; SF = ART + 'Starter/'
st = {k: load(SF, SP.get(k), S) for k in ('head', 'torso', 'arm', 'arm_far', 'hand', 'hand_far', 'leg_near', 'leg_far', 'pack', 'weapon')}
def hand_pt(part, e): return (V(e['hand'], (0, 0)) - part.pivot) if e and e.get('hand') else np.array([0, part.size[1] - 11 - part.pivot[1]])
rig = M['rig']; hip = V(rig['hip'], (0, -21)); headOff = V(rig['head'], (2, -29)); packOff0 = V(rig['pack'], (-21, -20))
legNOff = V(rig['leg_near'], (5, 0)); legFOff = V(rig['leg_far'], (-5, 0))
look = dict(head=st['head'], torso=st['torso'], arm=st['arm'], armFar=st['arm_far'], legN=st['leg_near'], legF=st['leg_far'], pack=st['pack'], weapon=st['weapon'],
            handPt=hand_pt(st['arm'], SP['arm']), handPtF=hand_pt(st['arm_far'], SP['arm_far']), sh=V(M['shoulder'], (11, -26)), shF=V(M['shoulderF'], (-4, -26)),
            packOff=packOff0, tip=V(SP['weapon'].get('tip'), (0, 0)) - st['weapon'].pivot)
def apply(slot, pid):
    d = ART + 'Parts/' + pid + '/'; j = json.load(open(d + 'part.json')); sc = j.get('scale', 4)
    P = {k: load(d, j.get(k), sc) for k in ('head', 'torso', 'arm', 'arm_far', 'pack', 'weapon', 'leg_near', 'leg_far')}
    if slot == 'head' and P['head']: look['head'] = P['head']
    if slot == 'coat' and P['torso']:
        look['torso'] = P['torso']
        if P['arm']: look['arm'] = P['arm']; look['handPt'] = hand_pt(P['arm'], j['arm']); look['armFar'] = P['arm_far']; look['handPtF'] = hand_pt(P['arm_far'], j.get('arm_far')) if P['arm_far'] else look['handPt']
        if j.get('shoulder'): look['sh'] = V(j['shoulder'], (0, 0)); look['shF'] = V(j.get('shoulderF') or j['shoulder'], (0, 0))
        if j.get('pack_off'): look['packOff'] = V(j['pack_off'], (0, 0))
    if slot == 'back' and P['pack']: look['pack'] = P['pack']
    if slot == 'legs' and P['leg_near'] and P['leg_far']: look['legN'] = P['leg_near']; look['legF'] = P['leg_far']
    if slot == 'weapon' and P['weapon']:
        look['weapon'] = P['weapon']; w = j['weapon']; look['tip'] = (V(w['tip'], (0, 0)) - P['weapon'].pivot) if w.get('tip') else np.array([0, P['weapon'].size[1] - P['weapon'].pivot[1]])

def T(x, y): return np.array([[1, 0, x], [0, 1, y], [0, 0, 1]], float)
def Rm(a): a = math.radians(a); c, s = math.cos(a), math.sin(a); return np.array([[c, s, 0], [-s, c, 0], [0, 0, 1]], float)
def Sc(x, y): return np.array([[x, 0, 0], [0, y, 0], [0, 0, 1]], float)

def render(out, unit=3.0, bg=(50, 52, 60), pose=None, size=None):
    p = dict(lean=1, head=0, pack=0, A=8, Af=-8, L=0, Lf=0, W=None); p.update(pose or {})
    if p['W'] is None: p['W'] = p['A']
    root = np.eye(3)
    b = {}
    b['torso'] = root @ T(*hip) @ Rm(-p['lean'])
    b['head'] = b['torso'] @ T(*headOff) @ Rm(-p['head'])
    b['pack'] = b['torso'] @ T(*look['packOff']) @ Rm(-p['pack'])
    b['armN'] = b['torso'] @ T(*look['sh']) @ Rm(p['A'])
    b['armF'] = b['torso'] @ T(*look['shF']) @ Rm(p['Af'])
    b['hand'] = b['armN'] @ T(*look['handPt'])
    b['handF'] = b['armF'] @ T(*look['handPtF'])
    b['wpn'] = b['hand'] @ Rm(p['W'] - p['A'])
    b['legN'] = root @ T(*(hip + legNOff)) @ Rm(p['L'])
    b['legF'] = root @ T(*(hip + legFOff)) @ Rm(p['Lf'])
    W_, H_ = size or (int(150 * unit), int(215 * unit))
    canvas = Image.new('RGBA', (W_, H_), bg + (255,)); O = T(W_ / 2, H_ - 20 * unit) @ Sc(unit, unit)
    def blit(bone, part, mirror=False, dark=1.0):
        if part is None: return
        im = part.im
        if mirror: im = im.transpose(Image.FLIP_LEFT_RIGHT)
        px = part.size[0] - part.pivot[0] if mirror else part.pivot[0]
        Mx = O @ bone @ T(-px, -part.pivot[1]) @ Sc(1 / part.scale, 1 / part.scale)
        inv = np.linalg.inv(Mx)
        if dark != 1.0:
            a = im.getchannel('A'); rgb = Image.fromarray((np.asarray(im.convert('RGB')).astype(float) * dark).astype(np.uint8)); im = rgb.convert('RGBA'); im.putalpha(a)
        t = im.transform((W_, H_), Image.AFFINE, data=tuple(inv[:2].flatten()), resample=Image.BICUBIC)
        canvas.alpha_composite(t)
    blit(b['pack'], look['pack']); blit(b['legF'], look['legF'])
    blit(b['armF'], look['armFar'] or look['arm'], mirror=look['armFar'] is None, dark=.62)
    blit(b['handF'], st['hand_far'] or st['hand'], mirror=st['hand_far'] is None, dark=.62)
    blit(b['legN'], look['legN']); blit(b['torso'], look['torso']); blit(b['head'], look['head']); blit(b['armN'], look['arm'])
    blit(b['wpn'], look['weapon']); blit(b['hand'], st['hand'])
    canvas.convert('RGB').save(out); return canvas

if __name__ == '__main__':
    a = sys.argv[1:]; out = a[0]; unit = 3.0; bg = (50, 52, 60); pose = {}
    i = 1
    while i < len(a):
        if a[i] == '--scale': unit = float(a[i + 1]); i += 2; continue
        if a[i] == '--bg': bg = tuple(int(x) for x in a[i + 1].split(',')); i += 2; continue
        if a[i] == '--pose': pose = {kv.split(':')[0]: float(kv.split(':')[1]) for kv in a[i + 1].split(',')}; i += 2; continue
        slot, pid = a[i].split('='); apply(slot, pid); i += 1
    render(out, unit, bg, pose); print(out)
