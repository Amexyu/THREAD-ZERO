import sys, os, numpy as np
from PIL import Image
OUT=sys.argv[1].rstrip('/')+'/'
ROOT=os.path.expanduser('~/mnt/THREAD-ZERO/')
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
