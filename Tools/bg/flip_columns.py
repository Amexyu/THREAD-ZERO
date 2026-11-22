# 三姿势图里的怪朝右时：三格各自水平翻转（顺序不变），存一份 _朝左.png（原图不动）。
# 用法：python3 flip_columns.py <三姿势原图> [格数=3]
import sys, numpy as np
from PIL import Image
src=sys.argv[1]; n=int(sys.argv[2]) if len(sys.argv)>2 else 3
a=np.asarray(Image.open(src).convert('RGB')); W=a.shape[1]; o=a.copy()
for i in range(n): x0,x1=i*W//n,(i+1)*W//n; o[:,x0:x1]=a[:,x0:x1][:,::-1]
out=src.rsplit('_原图',1)[0]+'_朝左.png'; Image.fromarray(o).save(out); print(out)
