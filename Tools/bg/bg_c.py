# 只处理新出的 C（远景/中景/地面），和 v5 修好的 A/B/D 一起出同框预览。
# 用法：python3 bg_c.py <C 出图文件夹> <预览输出.jpg>
import sys, os, numpy as np
from PIL import Image
SRC=sys.argv[1].rstrip('/')+'/'; OUT=SRC+'抠好/'; os.makedirs(OUT,exist_ok=True)
ROOT=os.path.expanduser('~/mnt/THREAD-ZERO/'); FIX=ROOT+'Gemini/卡22_背景重做/出图_第一章_v5/修/'
def key(a):
    r,g,b=a[...,0],a[...,1],a[...,2]; score=np.minimum(r,b)-g; alpha=np.clip((190-score)/110,0,1)
    ex=np.clip(score,0,None); k=np.clip((score-8)/40,0,1)
    return np.dstack([r-ex*k,g,b-ex*k,alpha*255]).clip(0,255)
def seam(a,n):
    a=a.copy(); W=a.shape[1]; t=np.linspace(0,1,n)[None,:,None]; a[:,:n]=a[:,W-n:]*(1-t)+a[:,:n]*t; return a[:,:W-n]
def bestcut(a,n=280,win=12):
    W=a.shape[1]; best=None
    for x0 in range(0,n,4):
        A=a[:,x0:x0+win]
        for x1 in range(W-n,W-win,4):
            d=np.abs(A-a[:,x1:x1+win]).mean()
            if best is None or d<best[0]: best=(d,x0,x1)
    return best[1],best[2],best[0]
def tile(a,blend):
    L,R,d=bestcut(a); W=a.shape[1]; blend=min(blend,W-R)
    return a[:,L:R] if blend<=0 else seam(a[:,L:R+blend],blend)
def trim(a,f): n=int(a.shape[1]*f); return a[:,n:a.shape[1]-n]   # 去掉左右两边当画框的管子
def load(p): return np.asarray(Image.open(p).convert('RGB')).astype(np.float32)
def save(a,p): Image.fromarray(a.clip(0,255).astype(np.uint8),'RGBA' if a.shape[2]==4 else 'RGB').save(p)
# 远景：不透明，去边后横向无缝
save(tile(trim(load(SRC+'C_far.png'),.11),180),OUT+'C_far.png')
# 中景：品红抠掉（主要是底部 1/8），去边后横向无缝
save(tile(key(trim(load(SRC+'C_mid.png'),.11)),40),OUT+'C_mid.png')
# 地面：Gemini 只在走道上沿画了一条品红线 → 以这条线为界，线以上全部挖空
g=load(SRC+'C_ground.png'); r,gg,b=g[...,0],g[...,1],g[...,2]
mag=((np.minimum(r,b)-gg)>60).mean(1); line=int(np.argmax(mag)); below=line+1
while below<len(mag) and mag[below]>.05: below+=1
print('品红线在第',line,'行（%.0f%%），走道从第'%(100*line/len(mag)),below,'行开始')
g=key(g); g[:below,:,3]=0; g=trim(g,.05)
H=g.shape[0]; y0=below; want=int(round(.25*(H-y0)))
g=g[y0-want:] if y0>want else np.concatenate([np.zeros((want-y0,g.shape[1],4)),g],0)
save(tile(g,24),OUT+'C_ground.png')
# 同框预览（和 bg.py 一样的摆法）
E=ROOT+'ThreadZero/Assets/Resources/Art/V16/Enemies/'; hero=Image.open(ROOT+'ThreadZero/Assets/ArtArchive/V13/Review/s0_character.png').convert('RGBA'); GY=690
def layer(c,img,h,y,scroll=0):
    w=int(h*img.width/img.height); im=img.resize((w,int(h))); x=-int(scroll)%w-w
    while x<1600: c.alpha_composite(im,(x,int(y))); x+=w
def enemy(c,n,fx,fy,cv): im=Image.open(E+n+'_idle.png').convert('RGBA').resize((cv,cv)); c.alpha_composite(im,(int(fx-cv/2),int(fy-cv*.875)))
def scene(s,lineup):
    d=OUT if s=='C' else FIX
    c=Image.new('RGBA',(1600,900),(0,0,0,255)); far=Image.open(d+s+'_far.png').convert('RGBA')
    if s=='D': k=max(1600/far.width,900/far.height); c.alpha_composite(far.resize((int(far.width*k),int(far.height*k))),(0,0))
    else: layer(c,far,900,0,300); layer(c,Image.open(d+s+'_mid.png').convert('RGBA'),900,0,700)
    h=(900-GY)/.8; layer(c,Image.open(d+s+'_ground.png').convert('RGBA'),h,GY-h*.2,1100)
    c.alpha_composite(hero.resize((260,260)),(428-130,GY+2-int(260*.93)))
    for n,x,cv in lineup: enemy(c,n,x,GY+2,cv)
    return c.convert('RGB')
L={'A':[('swarm',800,160),('swarm',930,160),('brute',1080,220),('ranged',1300,200)],'B':[('swarm',700,160),('elite_crusher',900,264),('brute',1150,220)],
   'C':[('swarm',780,160),('ranged',1050,200),('elite_beacon',1250,240)],'D':[('boss_gatekeeper',1050,380)]}
outs=[scene(s,L[s]) for s in 'ABCD']
sh=Image.new('RGB',(1600,900)); [sh.paste(o.resize((800,450)),((i%2)*800,(i//2)*450)) for i,o in enumerate(outs)]; sh.save(sys.argv[2],quality=88)
outs[2].save(sys.argv[2].replace('.jpg','_C大图.jpg'),quality=88)
print('ok', [Image.open(OUT+f).size for f in ('C_far.png','C_mid.png','C_ground.png')])
