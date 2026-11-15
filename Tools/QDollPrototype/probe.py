import sys; sys.path.insert(0,"/home/claude/work")
from PIL import Image, ImageDraw, ImageOps
import numpy as np, math
from comp import W,H,B,U
base=Image.open(U+'Base_c0.png').convert('RGBA')
def frame(f): cx,cy=(f%3)*512,(f//3)*512; return base.crop((cx,cy,cx+512,cy+512))
def place(dst,part,scale,angle,anchor,pos,mirror=False):
    p=ImageOps.mirror(part) if mirror else part
    ax=(1-anchor[0]) if mirror else anchor[0]
    w,h=p.size; p=p.resize((max(1,round(w*scale)),max(1,round(h*scale))),Image.LANCZOS)
    # pad so anchor is center, then rotate about center
    ax_px,ay_px=ax*p.width,anchor[1]*p.height
    R=int(max(p.width,p.height)*1.5)+4
    canvas=Image.new('RGBA',(2*R,2*R),(0,0,0,0)); canvas.alpha_composite(p,(round(R-ax_px),round(R-ay_px)))
    canvas=canvas.rotate(-angle,resample=Image.BICUBIC)   # angle: clockwise degrees on screen
    layer=Image.new('RGBA',dst.size,(0,0,0,0)); layer.paste(canvas,(round(pos[0]-R),round(pos[1]-R)))
    dst.alpha_composite(layer)
def fist(dst,src,pos,r):
    m=Image.new('L',src.size,0); ImageDraw.Draw(m).ellipse([pos[0]-r,pos[1]-r,pos[0]+r,pos[1]+r],fill=255)
    patch=src.copy(); a=np.asarray(patch).copy(); mm=np.asarray(m)
    a[:,:,3]=np.minimum(a[:,:,3],mm); dst.alpha_composite(Image.fromarray(a))
def build(f,head,weapon,wscale,wangle,wanchor,hand,r,headpos,hscale,hangle=0,back=None,backpos=None,bscale=.3):
    src=frame(f); out=Image.new('RGBA',(512,512),(0,0,0,0))
    if back is not None: place(out,B[back],bscale,0,(.5,.5),backpos,True)
    out.alpha_composite(src)
    if head is not None: place(out,H[head],hscale,hangle,(.5,.5),headpos,True)
    place(out,W[weapon],wscale,wangle,wanchor,hand)
    fist(out,src,hand,r)
    return out
shots=[
 build(0,0,0,.30,62,(.22,.70),(352,300),24,(282,100),.47,back=0,backpos=(200,215)),
 build(1,0,0,.30,0,(.22,.70),(350,163),26,(246,100),.47,back=0,backpos=(170,215)),
 build(2,2,2,.36,0,(.42,.72),(322,172),26,(222,104),.47,back=0,backpos=(150,222)),
 build(3,1,1,.42,-10,(.12,.86),(292,272),24,(266,96),.46),
 build(4,3,1,.42,-95,(.12,.86),(152,78),26,(268,98),.46),
 build(5,4,1,.42,48,(.12,.86),(300,258),26,(222,108),.46),
]
sheet=Image.new('RGBA',(1536,1024),(243,243,239,255))
for i,s in enumerate(shots): sheet.alpha_composite(s,((i%3)*512,(i//3)*512))
sheet.convert('RGB').save('probe.png')
sheet.convert('RGB').resize((1200,800),Image.LANCZOS).save('probe_s.jpg',quality=88)
