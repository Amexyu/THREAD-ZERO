# 一张图三个姿势（左→右：idle / windup / strike）→ 三张 512×512 透明 PNG。
# 大小和脚底位置对齐第二章同一分工的怪（ref），三个姿势共用同一个缩放和同一个锚点（以 idle 为准），动作不会跳。
# 用法：python3 pose_sheet.py <图> <怪名，如 swarm3> <参考怪，如 swarm2> [--out 目录] [--fit]（--fit：伸出画布的姿势整体缩小放进去）
import sys, os, numpy as np
from PIL import Image
ROOT=os.path.expanduser('~/mnt/THREAD-ZERO/'); E=ROOT+'ThreadZero/Assets/Resources/Art/V16/Enemies/'
src, name, ref = sys.argv[1], sys.argv[2], sys.argv[3]
out = sys.argv[sys.argv.index('--out')+1] if '--out' in sys.argv else E
def key(a):
    r,g,b=a[...,0],a[...,1],a[...,2]; score=np.minimum(r,b)-g; alpha=np.clip((190-score)/110,0,1)
    ex=np.clip(score,0,None); k=np.clip((score-8)/40,0,1)
    return np.dstack([r-ex*k,g,b-ex*k,alpha*255]).clip(0,255)
a=key(np.asarray(Image.open(src).convert('RGB')).astype(np.float32))
H,W=a.shape[:2]; thirds=[a[:, i*W//3:(i+1)*W//3] for i in range(3)]
def bbox(p):
    m=p[...,3]>40; ys,xs=np.nonzero(m); return xs.min(),ys.min(),xs.max(),ys.max()
def bbox_main(p, k=4):
    """主体的包围盒：缩小 k 倍找连通块，比最大块小很多的碎片（隔壁格漏进来的）不算。"""
    m=p[::k,::k,3]>40; pts=set(zip(*np.nonzero(m))); comps=[]
    while pts:
        st=[pts.pop()]; c=[st[0]]
        while st:
            y,x=st.pop()
            for dy in (-1,0,1):
                for dx in (-1,0,1):
                    q=(y+dy,x+dx)
                    if q in pts: pts.remove(q); st.append(q); c.append(q)
        comps.append(c)
    big=max(len(c) for c in comps); keep=[q for c in comps if len(c)>=.03*big for q in c]
    ys=[q[0] for q in keep]; xs=[q[1] for q in keep]
    return min(xs)*k,min(ys)*k,max(xs)*k+k-1,max(ys)*k+k-1
ri=np.asarray(Image.open(E+ref+'_idle.png').convert('RGBA')); rb=bbox(ri.astype(np.float32))
ib=bbox_main(thirds[0]); f=(float(sys.argv[sys.argv.index('--height')+1]) if '--height' in sys.argv else rb[3]-rb[1])/(ib[3]-ib[1])          # 让 idle 和参考怪一样高（--height N：参考怪的形状差太多时直接给 idle 的高度）
ax=(ib[0]+ib[2])/2; ay=ib[3]                                  # 锚点：idle 的水平中心、脚底
cx=(rb[0]+rb[2])/2; cy=rb[3]                                  # 放到参考怪同样的位置
if '--fit' in sys.argv:   # 有姿势（举盾、扬起的天线）会伸出 512 画布时，整体缩小到刚好放得下（三个姿势同一个缩放）
    for p in thirds:
        x0,y0,x1,y1=bbox_main(p)
        if y0<ay: f=min(f,(cy-4)/(ay-y0))
        if x0<ax: f=min(f,(cx-4)/(ax-x0))
        if x1>ax: f=min(f,(508-cx)/(x1-ax))
    print('fit scale',round(f,4))
def clean(im, near=10, small=.03):
    """去掉隔壁格漏进来的碎片：和主体（最大的一块）隔开超过 near 像素、又比主体小很多的块都抹掉。"""
    a=np.asarray(im).copy(); m=a[...,3]>40
    pts=set(zip(*np.nonzero(m))); comps=[]
    while pts:
        st=[pts.pop()]; c=[st[0]]
        while st:
            y,x=st.pop()
            for dy in (-1,0,1):
                for dx in (-1,0,1):
                    q=(y+dy,x+dx)
                    if q in pts: pts.remove(q); st.append(q); c.append(q)
        comps.append(c)
    if len(comps)<2: return im, 0
    comps.sort(key=len,reverse=True); main=np.zeros_like(m); ys,xs=zip(*comps[0]); main[ys,xs]=True
    grow=main.copy()
    for _ in range(near):
        g=grow.copy(); g[1:]|=grow[:-1]; g[:-1]|=grow[1:]; g[:,1:]|=grow[:,:-1]; g[:,:-1]|=grow[:,1:]; grow=g
    n=0
    for c in comps[1:]:
        ys,xs=zip(*c)
        if len(c)<small*len(comps[0]) and not grow[ys,xs].any():
            y0,y1,x0,x1=min(ys),max(ys),min(xs),max(xs); a[max(0,y0-2):y1+3,max(0,x0-2):x1+3,3]=0; n+=1
    return Image.fromarray(a,'RGBA'), n
for p,pose in zip(thirds,['idle','windup','strike']):
    im=Image.fromarray(p.astype(np.uint8),'RGBA'); w,h=im.size
    im=im.resize((max(1,round(w*f)),max(1,round(h*f))),Image.LANCZOS)
    canvas=Image.new('RGBA',(512,512),(0,0,0,0))
    canvas.alpha_composite(im,(int(round(cx-ax*f)),int(round(cy-ay*f)))) if False else canvas.paste(im,(int(round(cx-ax*f)),int(round(cy-ay*f))),im)
    if '--keepbits' not in sys.argv: canvas,removed=clean(canvas); print(pose,'removed bits',removed) if removed else None
    canvas.save(os.path.join(out,name+'_'+pose+'.png'))
    bb=canvas.getchannel('A').getbbox(); print(name,pose,'bbox',bb, '贴边！' if bb and (bb[0]<=0 or bb[1]<=0 or bb[2]>=512) else '')
