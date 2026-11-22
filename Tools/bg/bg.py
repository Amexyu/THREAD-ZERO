# 背景处理：品红抠图 + 横向无缝 + 地面上沿对齐；再出同框预览。用法：python3 bg.py <出图文件夹> <预览输出.jpg>
import sys, os, numpy as np
from PIL import Image
SRC=sys.argv[1].rstrip('/')+'/'; OUT=SRC+'抠好/'; os.makedirs(OUT,exist_ok=True)
ROOT=os.path.expanduser('~/mnt/THREAD-ZERO/')
def key(path):
    a=np.asarray(Image.open(path).convert('RGB')).astype(np.float32); r,g,b=a[...,0],a[...,1],a[...,2]
    score=np.minimum(r,b)-g; alpha=np.clip((190-score)/110,0,1)
    ex=np.clip(score,0,None); k=np.clip((score-8)/40,0,1)
    return np.dstack([r-ex*k,g,b-ex*k,alpha*255]).clip(0,255)
def seam(a,n):
    a=a.copy(); W=a.shape[1]; t=np.linspace(0,1,n)[None,:,None]; a[:,:n]=a[:,W-n:]*(1-t)+a[:,:n]*t; return a[:,:W-n]
def bestcut(a,n=280,win=12):
    W=a.shape[1]
    if a.shape[2]==4:
        cov=(a[...,3]>20).mean(0); L=min(range(n),key=lambda x:cov[x]); R=min(range(W-n,W),key=lambda x:cov[x])
        if cov[L]<.03 and cov[R]<.03: return L,R,0
    best=None
    for x0 in range(0,n,4):
        A=a[:,x0:x0+win]
        for x1 in range(W-n,W-win,4):
            d=np.abs(A-a[:,x1:x1+win]).mean()
            if best is None or d<best[0]: best=(d,x0,x1)
    return best[1],best[2],best[0]
def tile(a,blend):
    L,R,d=bestcut(a); W=a.shape[1]; blend=min(blend,W-R) if d>0 else 0
    return a[:,L:R] if blend<=0 else seam(a[:,L:R+blend],blend)
def save(a,p): Image.fromarray(a.clip(0,255).astype(np.uint8),'RGBA' if a.shape[2]==4 else 'RGB').save(p)
for s in 'ABC':
    save(tile(np.asarray(Image.open(SRC+s+'_far.png').convert('RGB')).astype(np.float32),180),OUT+s+'_far.png')
    save(tile(key(SRC+s+'_mid.png'),32),OUT+s+'_mid.png')
for s in 'ABCD':
    g=key(SRC+s+'_ground.png'); al=g[...,3]>200; H=g.shape[0]; rows=al.mean(1)
    y0=next(y for y in range(H-40) if (rows[y:y+40]>.85).all()); want=int(round(.25*(H-y0)))
    g=g[y0-want:] if y0>want else np.concatenate([np.zeros((want-y0,g.shape[1],4)),g],0)
    if s!='D': g=tile(g,24)
    save(g,OUT+s+'_ground.png')
save(np.asarray(Image.open(SRC+'D_far.png').convert('RGB')).astype(np.float32),OUT+'D_far.png')
# 同框预览
E=ROOT+'ThreadZero/Assets/Resources/Art/V16/Enemies/'; hero=Image.open(ROOT+'ThreadZero/Assets/ArtArchive/V13/Review/s0_character.png').convert('RGBA'); GY=690
def layer(c,img,h,y,scroll=0):
    w=int(h*img.width/img.height); im=img.resize((w,int(h))); x=-int(scroll)%w-w
    while x<1600: c.alpha_composite(im,(x,int(y))); x+=w
def enemy(c,n,fx,fy,cv): im=Image.open(E+n+'_idle.png').resize((cv,cv)); c.alpha_composite(im,(int(fx-cv/2),int(fy-cv*.875)))
outs=[]
for s,lineup in [('A',[('swarm',800,160),('swarm',930,160),('brute',1080,220),('ranged',1300,200)]),('B',[('swarm',700,160),('elite_crusher',900,264),('brute',1150,220)]),('C',[('swarm',780,160),('ranged',1050,200),('elite_beacon',1250,240)]),('D',[('boss_gatekeeper',1050,380)])]:
    c=Image.new('RGBA',(1600,900),(0,0,0,255)); far=Image.open(OUT+s+'_far.png').convert('RGBA')
    if s=='D': k=max(1600/far.width,900/far.height); c.alpha_composite(far.resize((int(far.width*k),int(far.height*k))),(0,0))
    else: layer(c,far,900,0,300); layer(c,Image.open(OUT+s+'_mid.png'),900,0,700)
    h=(900-GY)/.8; layer(c,Image.open(OUT+s+'_ground.png'),h,GY-h*.2,1100)
    c.alpha_composite(hero.resize((260,260)),(428-130,GY+2-int(260*.93)))
    for n,x,cv in lineup: enemy(c,n,x,GY+2,cv)
    outs.append(c.convert('RGB').resize((800,450)))
sh=Image.new('RGB',(1600,900)); [sh.paste(o,((i%2)*800,(i//2)*450)) for i,o in enumerate(outs)]; sh.save(sys.argv[2],quality=88)
print('ok')
