# 用法：crop_grid.py 图片 u0 v0 u1 v1 输出 [步长]  —— 裁一块并画细网格（标图层比例坐标）
import sys
from PIL import Image, ImageDraw
p,u0,v0,u1,v1,out=sys.argv[1],*map(float,sys.argv[2:6]),sys.argv[6]; st=float(sys.argv[7]) if len(sys.argv)>7 else .02
im=Image.open(p).convert('RGBA'); bg=Image.new('RGBA',im.size,(255,0,255,255)); bg.alpha_composite(im); im=bg.convert('RGB')
W,H=im.size; c=im.crop((int(u0*W),int(v0*H),int(u1*W),int(v1*H))); k=900/c.width; c=c.resize((900,int(c.height*k))); d=ImageDraw.Draw(c)
import math
u=math.ceil(u0/st)*st
while u<u1:
    x=(u-u0)*W*k; d.line([(x,0),(x,c.height)],fill=(255,255,0)); d.text((x+2,2),'%.2f'%u,fill=(255,255,0)); u+=st
v=math.ceil(v0/st)*st
while v<v1:
    y=(v-v0)*H*k; d.line([(0,y),(900,y)],fill=(0,255,255)); d.text((2,y+2),'%.2f'%v,fill=(0,255,255)); v+=st
c.save(out,quality=85)
