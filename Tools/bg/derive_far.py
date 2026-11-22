"""远侧袖子 / 远侧腿从近侧那件派生（同一件衣服，形状按初始远侧部件缩放、颜色压暗）。
用法：python3 derive_far.py c10 c11 …   （c 开头做 arm→arm_far，l 开头做 leg_near→leg_far）"""
import sys, json, os
from PIL import Image, ImageEnhance
import armor_sheet as A
for pid in sys.argv[1:]:
    d = A.ART + 'Parts/' + pid; j = json.load(open(d + '/part.json'))
    src, dst = ('arm', 'arm_far') if pid.startswith('c') else ('leg_near', 'leg_far')
    sN, sF = A.starter(src), A.starter(dst); e = j[src]
    sx, sy = sF.width / sN.width, sF.height / sN.height
    if dst == 'leg_far': sx = sy = 1.0
    im = Image.open(d + '/' + e['file']).convert('RGBA')
    im = im.resize((max(1, round(im.width * sx)), max(1, round(im.height * sy))), Image.LANCZOS)
    a = im.getchannel('A'); rgb = ImageEnhance.Brightness(im.convert('RGB')).enhance(0.78); im = rgb.convert('RGBA'); im.putalpha(a)
    im.save(d + '/' + dst + '.png')
    pN, pF = A.M['parts'][src]['pivot'], A.M['parts'][dst]['pivot']
    # 近侧图里的初始枢轴点按同样比例缩放后，对齐远侧初始枢轴
    piv = [round(e['pivot'][0] * sx, 2), round(e['pivot'][1] * sy, 2)]
    ent = {'file': dst + '.png', 'pivot': piv}
    if 'hand' in e: ent['hand'] = [round(e['hand'][0] * sx, 2), round(e['hand'][1] * sy, 2)]
    j[dst] = ent; json.dump(j, open(d + '/part.json', 'w'), indent=1); print(pid, dst, im.size, ent)
