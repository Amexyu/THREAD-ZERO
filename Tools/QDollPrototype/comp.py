from PIL import Image, ImageDraw
import numpy as np
from scipy import ndimage
U='/mnt/user-data/uploads/毕业制作/Deliverables/ArtPack_v08/Source/'
def parts(name):
    im=Image.open(U+name).convert('RGBA'); a=np.asarray(im)[:,:,3]>40
    lab,n=ndimage.label(ndimage.binary_dilation(a,iterations=6)); out=[]
    for sl in ndimage.find_objects(lab):
        y,x=sl
        if (y.stop-y.start)*(x.stop-x.start)<20000: continue
        out.append((x.start,y.start,im.crop((x.start,y.start,x.stop,y.stop))))
    out.sort(key=lambda t:(t[1]//400,t[0])); return [o[2] for o in out]
W=parts('Weapons.png'); H=parts('Heads.png'); B=parts('Backs.png')
print('weapons',[w.size for w in W]); print('heads',[h.size for h in H]); print('backs',[b.size for b in B])
if __name__=='__main__':
    for i,w in enumerate(W): w.save(f'part_w{i}.png')
    for i,h in enumerate(H): h.save(f'part_h{i}.png')
    for i,b in enumerate(B): b.save(f'part_b{i}.png')
