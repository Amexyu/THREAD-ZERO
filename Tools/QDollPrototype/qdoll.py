# Q 版单姿势纸娃娃：身体、头、背包、武器拼成一个角色，动作全靠整体动效。
import sys, math; sys.path.insert(0,'/home/claude/work')
import numpy as np
from PIL import Image, ImageDraw, ImageOps, ImageFilter
from scipy import ndimage
import probe as PR
def clean(im):
    a=np.asarray(im).copy(); lab,n=ndimage.label(a[:,:,3]>40)
    if n>1:
        s=ndimage.sum(a[:,:,3]>40,lab,range(1,n+1)); a[lab!=(int(np.argmax(s))+1)]=0
    ys,xs=np.where(a[:,:,3]>40); return Image.fromarray(a).crop((xs.min(),ys.min(),xs.max()+1,ys.max()+1))
W=[clean(w) for w in PR.W]
H=[ImageOps.mirror(h) for h in PR.H]      # place() 会再翻一次，抵消后保持原朝向（朝右）
B=PR.B
HAND=(352,300)
HEADS={0:((286,104),.43),1:((284,100),.45),2:((282,106),.45),3:((284,100),.44),4:((282,108),.45)}
WEAPONS={0:(.30,(.22,.70),62),1:(.46,(.10,.90),-30),2:(.36,(.40,.72),40),4:(.34,(.10,.92),-30)}
def doll(head=0,back=0):
    src=PR.frame(0); out=Image.new('RGBA',(512,512),(0,0,0,0))
    if back is not None: PR.place(out,B[back],.30,0,(.5,.5),(200,215),True)
    out.alpha_composite(src)
    if head is not None:
        pos,sc=HEADS[head]; PR.place(out,H[head],sc,0,(.5,.5),pos,True)
    return out, src
def fist_layer(src):
    m=Image.new('L',src.size,0); ImageDraw.Draw(m).ellipse([HAND[0]-24,HAND[1]-24,HAND[0]+24,HAND[1]+24],fill=255)
    a=np.asarray(src).copy(); a[:,:,3]=np.minimum(a[:,:,3],np.asarray(m)); return Image.fromarray(a)
def weapon_layer(widx,extra):
    sc,anc,rest=WEAPONS[widx]; lay=Image.new('RGBA',(512,512),(0,0,0,0))
    PR.place(lay,W[widx],sc,rest+extra,anc,HAND); return lay
def tf(img,ang,sx,sy,dx,dy,pivot=(256,486)):
    """整体变换：绕脚底旋转、缩放（压扁/拉伸），再平移"""
    px,py=pivot; r=math.radians(ang); c,s=math.cos(r),math.sin(r)
    M=np.array([[c*sx,-s*sy,0],[s*sx,c*sy,0],[0,0,1.]])
    T=np.array([[1,0,px+dx],[0,1,py+dy],[0,0,1.]])@M@np.array([[1,0,-px],[0,1,-py],[0,0,1.]])
    inv=np.linalg.inv(T); return img.transform(img.size,Image.AFFINE,tuple(inv[:2].reshape(-1)),resample=Image.BICUBIC)
