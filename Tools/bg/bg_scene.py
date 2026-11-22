# 处理一个场景的三层（<S>_far_原图 / <S>_mid_原图 / <S>_ground_原图，D 只有 far / ground），出抠好的 png 和同框预览。
# 用法：python3 bg_scene.py <出图文件夹> <场景字母> <预览.jpg> [--ch2] [--trim 0.0]
import sys, os, glob, numpy as np
from PIL import Image
sys.path.insert(0, os.path.expanduser('~/tz_tools'))
SRC=sys.argv[1].rstrip('/')+'/'; S=sys.argv[2]; PREV=sys.argv[3]; CH2='--ch2' in sys.argv
TRIM=float(sys.argv[sys.argv.index('--trim')+1]) if '--trim' in sys.argv else 0.0
OUT=SRC+'抠好/'; os.makedirs(OUT,exist_ok=True); ROOT=os.path.expanduser('~/mnt/THREAD-ZERO/')
def src(name):
    for ext in ('_净.png','_原图.jpeg','_原图.jpg','_原图.png','.png'):   # _净：手工清理过的副本（原图不动）
        if os.path.exists(SRC+name+ext): return SRC+name+ext
    return None
HARD='--keyhard' in sys.argv   # 中景里有被品红染过的远处结构时，抠得更狠一点
def key(a):
    r,g,b=a[...,0],a[...,1],a[...,2]; score=np.minimum(r,b)-g; alpha=np.clip(((130-score)/90) if HARD else ((190-score)/110),0,1)
    ex=np.clip(score,0,None); k=np.clip((score-8)/40,0,1)
    return np.dstack([r-ex*k,g,b-ex*k,alpha*255]).clip(0,255)
def seam(a,n):
    a=a.copy(); W=a.shape[1]; t=np.linspace(0,1,n)[None,:,None]; a[:,:n]=a[:,W-n:]*(1-t)+a[:,:n]*t; return a[:,:W-n]
def bestcut(a,n=280,win=12):
    W=a.shape[1]; best=None
    if a.shape[2]==4:
        cov=(a[...,3]>20).mean(0); L=min(range(n),key=lambda x:cov[x]); R=min(range(W-n,W),key=lambda x:cov[x])
        if cov[L]<.03 and cov[R]<.03: return L,R,0
    for x0 in range(0,n,4):
        A=a[:,x0:x0+win]
        for x1 in range(W-n,W-win,4):
            d=np.abs(A-a[:,x1:x1+win]).mean()
            if best is None or d<best[0]: best=(d,x0,x1)
    return best[1],best[2],best[0]
def tile(a,blend):
    L,R,d=bestcut(a); W=a.shape[1]; blend=min(blend,W-R) if d>0 else 0
    return a[:,L:R] if blend<=0 else seam(a[:,L:R+blend],blend)
def trim(a,f): n=int(a.shape[1]*f); return a[:,n:a.shape[1]-n] if n>0 else a
def load(p): return np.asarray(Image.open(p).convert('RGB')).astype(np.float32)
def save(a,p,w=2048):
    im=Image.fromarray(a.clip(0,255).astype(np.uint8),'RGBA' if a.shape[2]==4 else 'RGB')
    if im.width>w: im=im.resize((w,round(im.height*w/im.width)),Image.LANCZOS)
    if p.endswith('_far.png') or p.endswith('_mid.png'):   # v0.20：边长补到 4 的倍数，Unity 才能压成 BC7（见 pad4.py）
        from pad4 import align4; im=align4(im)
    im.save(p); return im.size
sizes={}
if S=='D': sizes['far']=save(load(src('D_far')),OUT+'D_far.png')
else:
    sizes['far']=save(tile(trim(load(src(S+'_far')),TRIM),180),OUT+S+'_far.png')
    if src(S+'_mid'):
        m=key(trim(load(src(S+'_mid')),TRIM))
        halo=(m[...,3]<200)&(m[...,0]>m[...,1]+12)&(m[...,0]>m[...,2]-30)   # 灯头周围被品红染过的半透明光晕
        m[...,3]=np.where(halo,0,m[...,3]); print('mid 去光晕',int(halo.sum()))
        sizes['mid']=save(tile(m,40),OUT+S+'_mid.png')
# 地面：上方品红（整块或者只有一条线）以下才是走道
GAME=ROOT+'ThreadZero/Assets/Resources/Art/V16/'+(sys.argv[sys.argv.index('--game')+1]+'/' if '--game' in sys.argv else ('Stage2/' if CH2 else 'Stage/'))
if src(S+'_ground') is None:   # 地面还没出：预览先用游戏里现有的地面
    print('没有新地面，预览用游戏里现有的', GAME+S+'_ground.png')
    import shutil; shutil.copy(GAME+S+'_ground.png', OUT+S+'_ground_游戏现有.png'); GROUND=OUT+S+'_ground_游戏现有.png'
else: GROUND=None
g=load(src(S+'_ground') or GAME+S+'_ground.png'); r,gg,b=g[...,0],g[...,1],g[...,2]; H=g.shape[0]
mag=((np.minimum(r,b)-gg)>60).mean(1)
top=int(H*.5); cand=[y for y in range(top) if mag[y]>.5]
line=max(cand) if cand else 0; below=line+1
while below<H and mag[below]>.05: below+=1
print('地面：品红到第',line,'行（%.0f%%），走道从第'%(100*line/H),below,'行开始')
g=key(g); g[:below,:,3]=0; g=trim(g,TRIM)
want=int(round(.25*(H-below)))
g=g[below-want:] if below>want else np.concatenate([np.zeros((want-below,g.shape[1],4)),g],0)
if S!='D': g=tile(g,24)
if GROUND is None: sizes['ground']=save(g,OUT+S+'_ground.png')
print(sizes)
# 同框预览
E=ROOT+'ThreadZero/Assets/Resources/Art/V16/Enemies/'; hero=Image.open(ROOT+'ThreadZero/Assets/ArtArchive/V13/Review/s0_character.png').convert('RGBA'); GY=690
def layer(c,img,h,y,scroll=0):
    w=int(h*img.width/img.height); im=img.resize((w,int(h))); x=-int(scroll)%w-w
    while x<1600: c.alpha_composite(im,(x,int(y))); x+=w
def enemy(c,n,fx,fy,cv):
    p=E+n+'_idle.png'
    if os.path.exists(p): c.alpha_composite(Image.open(p).convert('RGBA').resize((cv,cv)),(int(fx-cv/2),int(fy-cv*.875)))
c=Image.new('RGBA',(1600,900),(0,0,0,255)); far=Image.open(OUT+S+'_far.png').convert('RGBA')
if S=='D': k=max(1600/far.width,900/far.height); c.alpha_composite(far.resize((int(far.width*k),int(far.height*k))),(0,0))
else:
    layer(c,far,900,0,300)
    if os.path.exists(OUT+S+'_mid.png'): layer(c,Image.open(OUT+S+'_mid.png').convert('RGBA'),900,0,700)
h=(900-GY)/.8; layer(c,Image.open(GROUND or OUT+S+'_ground.png').convert('RGBA'),h,GY-h*.2,1100)
c.alpha_composite(hero.resize((260,260)),(428-130,GY+2-int(260*.93)))
two=CH2
L={'A':[('swarm2',800,160),('swarm2',930,160),('brute2',1080,220),('ranged2',1300,200)],'B':[('swarm2',700,160),('elite_hauler',900,264),('brute2',1150,220)],
   'C':[('swarm2',780,160),('ranged2',1050,200),('elite_carrier',1250,240)],'D':[('boss_crane',1050,380)]}
for n,x,cv in L[S]: enemy(c,n if two else n.replace('2','').replace('elite_hauler','elite_crusher').replace('elite_carrier','elite_beacon').replace('boss_crane','boss_gatekeeper'),x,GY+2,cv)
c.convert('RGB').save(PREV,quality=88); print('preview',PREV)
