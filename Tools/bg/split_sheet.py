# 一张品红底的图里放了几件东西（左→右），抠出来各存一张透明 PNG（按外框裁好，四周留 8 像素）。
# 用法：python3 split_sheet.py <图> <输出目录> <名字1> <名字2> ...   （名字个数 = 东西件数，按从左到右）
import sys, os, numpy as np
from PIL import Image, ImageDraw
src, out, names = sys.argv[1], sys.argv[2], sys.argv[3:]
a=np.asarray(Image.open(src).convert('RGB')).astype(np.float32)
r,g,b=a[...,0],a[...,1],a[...,2]; score=np.minimum(r,b)-g; alpha=np.clip((190-score)/110,0,1)
ex=np.clip(score,0,None); k=np.clip((score-8)/40,0,1)
rgba=np.dstack([r-ex*k,g,b-ex*k,alpha*255]).clip(0,255).astype(np.uint8)
# 按列找空白缝把东西分开（东西之间至少隔 20 像素的全空列）
cols=(rgba[...,3]>40).sum(0); filled=cols>2; spans=[]; x=0; W=len(cols)
while x<W:
    if filled[x]:
        s=x
        while x<W and (filled[x] or (x+20<W and filled[x:x+20].any())): x+=1
        spans.append((s,x))
    x+=1
spans=[sp for sp in spans if sp[1]-sp[0]>40]
print('找到', len(spans), '件：', spans)
if len(spans)!=len(names): sys.exit('件数和名字对不上，检查图或手动指定')
os.makedirs(out,exist_ok=True)
for (x0,x1),n in zip(spans,names):
    part=rgba[:,x0:x1]; ys=np.nonzero((part[...,3]>40).any(1))[0]; y0,y1=ys.min(),ys.max()+1
    im=Image.fromarray(part[max(0,y0-8):y1+8, :]); pad=Image.new('RGBA',(im.width+16,im.height),(0,0,0,0)); pad.paste(im,(8,0))
    pad.save(os.path.join(out,n+'.png')); print(n, pad.size)
