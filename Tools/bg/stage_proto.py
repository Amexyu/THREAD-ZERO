# 把新 C 的三层整理成游戏素材（≤2048），放进 ArtArchive/V16/StageProto/（v0.20 起不进包；要在游戏里试，先把文件夹挪回 Resources/Art/V16/），并找出中景里灯的位置。
import os, json, numpy as np
from PIL import Image
ROOT=os.path.expanduser('~/mnt/THREAD-ZERO/'); SRC=ROOT+'Gemini/卡22_背景重做/出图_第一章_v5_C/'
DST=ROOT+'ThreadZero/Assets/ArtArchive/V16/StageProto/'; os.makedirs(DST,exist_ok=True)
exec(open(os.path.expanduser('~/tz_tools/bg_lib.py')).read())
def fit(a,maxw=2048):
    im=Image.fromarray(a.clip(0,255).astype(np.uint8),'RGBA' if a.shape[2]==4 else 'RGB')
    if im.width>maxw: im=im.resize((maxw,round(im.height*maxw/im.width)),Image.LANCZOS)
    return im
far=fit(tile(trim(load(SRC+'C_far.png'),.11),180)); far.save(DST+'C_far.png')
mid=fit(tile(key(trim(load(SRC+'C_mid2.png'),.02)),48)); mid.save(DST+'C_mid.png')
gr=Image.open(SRC+'抠好/C_ground.png').convert('RGBA'); gr=fit(np.asarray(gr).astype(np.float32)); gr.save(DST+'C_ground.png')
print('far',far.size,'mid',mid.size,'ground',gr.size)
# 灯：中景上半部分里又亮又暖的小块
m=np.asarray(mid).astype(int); r,g,b,al=m[...,0],m[...,1],m[...,2],m[...,3]
hot=(r>235)&(g>200)&(b<190)&(al>200); hot[int(m.shape[0]*.45):]=False
cols=hot.sum(0)>0; lamps=[]; x=0; W=m.shape[1]
while x<W:
    if not cols[x]: x+=1; continue
    x0=x
    while x<W and cols[x]: x+=1
    if x-x0<6: continue
    ys,xs=np.nonzero(hot[:,x0:x])
    if len(xs)<30: continue
    lamps.append({'u':round((xs.mean()+x0)/W,4),'v':round(ys.mean()/m.shape[0],4),'r':round(max(x-x0,np.ptp(ys))/m.shape[0],4)})
lamps.sort(key=lambda d:d['u']); print('lamps',lamps)
json.dump({'lamps':lamps},open(os.path.expanduser('~/tz_tools/c_lamps.json'),'w'))
