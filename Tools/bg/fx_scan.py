# 扫描背景层里的亮点（灯、窗、信号灯），出带网格和编号的预览，供写动效配置用。
import sys, os, json, numpy as np
from PIL import Image, ImageDraw
V=os.path.expanduser('~/mnt/THREAD-ZERO/ThreadZero/Assets/Resources/Art/V16/'); OUT=os.path.expanduser('~/mnt/THREAD-ZERO/Gemini/_tmp/fx/scan/'); os.makedirs(OUT,exist_ok=True)
def blobs(im):
    a=np.asarray(im.convert('RGBA').resize((640,round(640*im.height/im.width)))).astype(int); r,g,b,al=a[...,0],a[...,1],a[...,2],a[...,3]
    mx=np.maximum(np.maximum(r,g),b); mn=np.minimum(np.minimum(r,g),b)
    hot=((mx>225)&(al>200))|((mx>170)&(mx-mn>110)&(al>200))
    m=Image.fromarray((hot*255).astype(np.uint8),'L').copy(); res=[]; lab=1
    ys,xs=np.nonzero(hot)
    for y,x in zip(ys,xs):
        if m.getpixel((int(x),int(y)))!=255 or lab>250: continue
        ImageDraw.floodfill(m,(int(x),int(y)),lab); comp=np.asarray(m)==lab; lab+=1
        cy,cx=np.nonzero(comp)
        if len(cx)<4: continue
        col=a[cy,cx,:3].mean(0)
        res.append({'u':round(cx.mean()/a.shape[1],3),'v':round(cy.mean()/a.shape[0],3),'size':round(max(np.ptp(cx),np.ptp(cy))/a.shape[0],3),'n':int(len(cx)),'rgb':[int(c) for c in col]})
    res.sort(key=lambda d:-d['n']); return res[:24]
report={}
JOBS=[(sys.argv[i].rstrip('/')+'/',sys.argv[i+1]) for i in range(1,len(sys.argv)-1,2)] or [('Stage/','ABD'),('Stage2/','ABCD')]   # 用法：fx_scan.py [Stage3 ABCD ...]
for folder,scenes in JOBS:
    for s in scenes:
        rows=[]; key=folder[:-1]+'_'+s
        for layer in ['far','mid','ground']:
            p=V+folder+s+'_'+layer+'.png'
            if not os.path.exists(p): continue
            im=Image.open(p).convert('RGBA'); bl=blobs(im); report[key+'_'+layer]=bl
            bg=Image.new('RGBA',im.size,(255,0,255,255)); bg.alpha_composite(im); w=900; pv=bg.convert('RGB').resize((w,round(im.height*w/im.width))); d=ImageDraw.Draw(pv)
            for i in range(1,10):
                x=w*i//10; d.line([(x,0),(x,pv.height)],fill=(255,255,0)); d.text((x+2,2),'%.1f'%(i/10),fill=(255,255,0))
                y=pv.height*i//10; d.line([(0,y),(w,y)],fill=(0,255,255)); d.text((2,y+2),'%.1f'%(i/10),fill=(0,255,255))
            for j,bb in enumerate(bl):
                x,y=bb['u']*w,bb['v']*pv.height; d.ellipse([x-6,y-6,x+6,y+6],outline=(255,40,40),width=2); d.text((x+7,y-7),str(j),fill=(255,80,80))
            rows.append(pv)
        H=sum(r.height for r in rows)+8*len(rows); sh=Image.new('RGB',(900,H),(255,255,255)); y=0
        for r in rows: sh.paste(r,(0,y)); y+=r.height+8
        sh.save(OUT+key+'.jpg',quality=82)
json.dump(report,open(OUT+'blobs.json','w'),ensure_ascii=False,indent=0)
for k,v in report.items(): print(k, [(i,b['u'],b['v'],b['size'],b['rgb']) for i,b in enumerate(v[:10])])
