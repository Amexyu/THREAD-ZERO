import sys,glob
from PIL import Image
d=sys.argv[1]; out=sys.argv[2]
fs=sorted(f for f in glob.glob(d+'/X_*.png') if 'action' not in f)
crops=[]
for f in fs:
    im=Image.open(f).convert('RGB'); w,h=im.size
    crops.append(im.crop((int(w*.08),int(h*.0),int(w*.36),int(h*.5))).resize((300,int(300*h*.5/(w*.28)))))
cw,ch=crops[0].size; n=len(crops); cols=min(n,5); rows=(n+cols-1)//cols
sh=Image.new('RGB',(cw*cols,ch*rows))
for i,c in enumerate(crops): sh.paste(c,((i%cols)*cw,(i//cols)*ch))
sh.save(out,quality=85)
a=glob.glob(d+'/X_action*')
if a: Image.open(a[0]).convert('RGB').crop((640,380,1920,1200)).resize((960,615)).save(out.replace('.jpg','_act.jpg'),quality=85)
