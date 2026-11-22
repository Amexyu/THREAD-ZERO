# 去掉抠图边缘残留的品红色偏：只处理离透明区 band 像素以内的边缘像素，把 min(r,b) 高出 g 的部分减掉（橙、青、灰等正常颜色不受影响）。
# 用法：despill.py [--band 4] 文件...（原地改写，只用于已经抠好的游戏内副本，不碰 Gemini 原图）
import sys, numpy as np
from PIL import Image, ImageFilter
args=sys.argv[1:]; band=4
if args and args[0]=='--band': band=int(args[1]); args=args[2:]
for f in args:
    im=Image.open(f)
    if im.mode!='RGBA': print('skip (no alpha)',f); continue
    a=np.asarray(im).astype(int); al=a[...,3]
    near=np.asarray(Image.fromarray(((al<200)*255).astype(np.uint8)).filter(ImageFilter.MaxFilter(band*2+1)))>0
    r,g,b=a[...,0],a[...,1],a[...,2]
    m=np.clip(np.minimum(r,b)-g,0,None)*near
    n=int((m>2).sum())
    a[...,0]=r-m; a[...,2]=b-m
    Image.fromarray(np.clip(a,0,255).astype(np.uint8),'RGBA').save(f,optimize=True)
    print('despill',f,'px',n)
