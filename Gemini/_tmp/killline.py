# 中景去掉底线：找画面下半部"几乎整行都是深色"的细线，线和线以下全部抹成品红
import numpy as np, sys
from PIL import Image
src,out=sys.argv[1],sys.argv[2]
a=np.asarray(Image.open(src).convert('RGB')).astype(int); H,W=a.shape[:2]
dark=a.sum(axis=2)<300; frac=dark.mean(axis=1)
line=[y for y in range(int(H*.6),H) if frac[y]>.85]
if not line: print('no line'); Image.fromarray(a.astype('uint8')).save(out); sys.exit()
top=min(line); print('line rows',top,max(line))
b=a.copy(); b[top-2:]=[255,0,255]
Image.fromarray(b.astype('uint8')).save(out); print('saved',out)
