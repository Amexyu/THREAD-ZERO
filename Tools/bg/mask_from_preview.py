# 用聊天里预览图的品红遮罩（JS 读预览像素后行程编码传出来，解成 .npy）从 Gemini 的高清下载里抠出中景物体：高清版有时会混进上一张图的内容。
# 用法：python3 mask_from_preview.py <高清原图> <遮罩.npy> <输出_净.png>
import numpy as np, sys, os
from PIL import Image, ImageFilter
src, maskf, out = sys.argv[1], sys.argv[2], sys.argv[3]
m=np.load(maskf); hi=np.asarray(Image.open(src).convert('RGB')).astype(int); H,W=hi.shape[:2]
mu=Image.fromarray((m*255).astype('uint8')).resize((W,H),Image.NEAREST)
k=max(3, int(round(W/m.shape[1]))*2+1)
core=np.asarray(mu.filter(ImageFilter.MinFilter(3)))>127
dil=np.asarray(mu.filter(ImageFilter.MaxFilter(k)))>127
mag=(np.minimum(hi[:,:,0],hi[:,:,2])-hi[:,:,1])>60
luma=hi[:,:,0]*.3+hi[:,:,1]*.59+hi[:,:,2]*.11
keep=(core&~mag)|((dil&~core)&~mag&(luma<110))
o=np.zeros_like(hi); o[:]=[255,0,255]; o[keep]=hi[keep]
Image.fromarray(o.astype('uint8')).save(out)
Image.fromarray(o.astype('uint8')).resize((W//4,H//4)).save(out.replace(".png","_预览.png"))
print('kept',int(keep.sum()))
