import sys, math; sys.path.insert(0,'/home/claude/q'); sys.path.insert(0,'/home/claude/work')
import numpy as np
from PIL import Image, ImageDraw
import qdoll as Q
WEAPONS=Q.WEAPONS; WEAPONS[1]=(.46,(.10,.90),17)
ENEMY=Image.open('/mnt/user-data/uploads/毕业制作/Deliverables/ArtPack_v07/Runtime/Enemies/patrol.png').convert('RGBA')
SIZE=(860,480); GROUND=440; CS=.72; CX=250; EX=660
LW,LH,OX,OY=900,680,140,130   # 角色图层留足边距，前冲和跳起时不被裁掉
def ease(t): t=max(0,min(1,t)); return t*t*(3-2*t)
def keys(t,ks):
    for (t0,v0),(t1,v1) in zip(ks,ks[1:]):
        if t<=t1: return v0+(v1-v0)*ease((t-t0)/max(1e-6,t1-t0))
    return ks[-1][1]
def rot(v,deg):
    r=math.radians(deg); return (v[0]*math.cos(r)-v[1]*math.sin(r), v[0]*math.sin(r)+v[1]*math.cos(r))
def stage(scroll,enemy=True,enemy_flash=0,enemy_dx=0):
    im=Image.new('RGBA',SIZE,(243,243,239,255)); d=ImageDraw.Draw(im)
    for i in range(-1,8):   # 远景楼块
        x=i*150-(scroll*.3)%150; h=80+((i+int(scroll*.3//150))*37%5)*30
        d.rectangle([x,GROUND-h,x+96,GROUND],fill=(232,233,228,255))
    d.rectangle([0,GROUND,SIZE[0],GROUND+5],fill=(18,20,22,255))
    for i in range(-1,12):
        x=i*90-scroll%90; d.rectangle([x,GROUND+5,x+45,GROUND+13],fill=(255,74,18,255)); d.rectangle([x+45,GROUND+5,x+90,GROUND+13],fill=(18,20,22,255))
    for i in range(-1,8):
        x=i*140-(scroll*1.1)%140; d.rectangle([x,GROUND+34,x+56,GROUND+37],fill=(196,199,193,255))
    if enemy:
        e=ENEMY.resize((300,300),Image.LANCZOS)
        if enemy_flash>0:
            a=np.asarray(e).astype(float); a[:,:,:3]=a[:,:,:3]*(1-enemy_flash*.6)+np.array([255,120,100])*enemy_flash*.6
            e=Image.fromarray(a.clip(0,255).astype(np.uint8))
        im.alpha_composite(e,(int(EX-150+enemy_dx),int(GROUND+2-300*470/512)))
    return im
def hero(head,widx,extra=0,ang=0,sx=1,sy=1,dx=0,dy=0,tint=None,back=0):
    body,src=Q.doll(head,back); lay=body.copy()
    if widx is not None:
        sc,anc,rest=WEAPONS[widx]; w=Image.new('RGBA',(512,512),(0,0,0,0)); Q.PR.place(w,Q.W[widx],sc,rest+extra,anc,Q.HAND); lay.alpha_composite(w)
        lay.alpha_composite(Q.fist_layer(src))
    if tint is not None:
        a=np.asarray(lay).astype(float); a[:,:,:3]=a[:,:,:3]*(1-tint)+np.array([255,70,60])*tint; lay=Image.fromarray(a.clip(0,255).astype(np.uint8))
    big=Image.new('RGBA',(LW,LH),(0,0,0,0)); big.alpha_composite(lay,(OX,OY))
    big=Q.tf(big,ang,sx,sy,dx/CS,dy/CS,pivot=(256+OX,486+OY))
    return big.resize((round(LW*CS),round(LH*CS)),Image.LANCZOS)
def put(im,h): im.alpha_composite(h,(round(CX-(256+OX)*CS),round(GROUND+2-(486+OY)*CS)))
def to_screen(p): return (CX+(p[0]-256)*CS, GROUND+2+(p[1]-486)*CS)
def muzzle(widx,extra,dx,dy):
    sc,anc,rest=WEAPONS[widx]; w,h=Q.W[widx].size
    v=((0.98-anc[0])*w*sc,(0.20-anc[1])*h*sc); v=rot(v,rest+extra)
    sx,sy=to_screen((Q.HAND[0]+v[0],Q.HAND[1]+v[1])); return sx+dx,sy+dy
CACHE={}
def frame_walk(t,scroll,head,widx):
    s=abs(math.sin(2*math.pi*t)); up=-16*s
    sy=1.04 if s>.6 else (.94 if s<.15 else 1); sx=2-sy
    im=stage(scroll,enemy=False); 
    if s<.15:
        d=ImageDraw.Draw(im)
        for k in range(3): r=4+k*3; x=CX-60-k*16; d.ellipse([x-r,GROUND-r-2,x+r,GROUND+r-2],fill=(196,199,193,200))
    put(im,hero(head,widx,extra=6*math.sin(2*math.pi*t),ang=3*math.sin(2*math.pi*t),sx=sx,sy=sy,dy=up)); return im
def frame_shoot(t,head):
    ext=keys(t,[(0,0),(.18,-62),(.3,-62),(.34,-74),(.5,-62),(.82,-62),(1,0)])
    dx=keys(t,[(0,0),(.3,0),(.34,-10),(.5,0),(1,0)]); ang=keys(t,[(0,0),(.3,0),(.34,-4),(.5,0),(1,0)])
    hitf=1-abs(t-.42)/.08 if abs(t-.42)<.08 else 0
    im=stage(0,True,max(0,hitf),enemy_dx=4*max(0,hitf)); put(im,hero(head,0,ext,ang,1,1,dx,0))
    d=ImageDraw.Draw(im)
    if .3<=t<.38:
        mx,my=muzzle(0,ext,dx,0); d.polygon([(mx,my-16),(mx+44,my),(mx,my+16),(mx+12,my)],fill=(255,170,40,255))
    if .32<=t<.42:
        mx,my=muzzle(0,ext,dx,0); p=(t-.32)/.1; x=mx+p*(EX-60-mx); d.rectangle([x,my-3,x+46,my+3],fill=(255,74,18,255))
    if .4<=t<.55:
        r=14+(t-.4)*220; d.ellipse([EX-70-r,GROUND-150-r,EX-70+r,GROUND-150+r],outline=(255,200,60,255),width=4)
    return im
def frame_melee(t,head):
    ext=keys(t,[(0,0),(.3,-110),(.38,-115),(.5,120),(.68,110),(1,0)])
    dx=keys(t,[(0,0),(.3,-14),(.5,150),(.7,140),(1,0)]); ang=keys(t,[(0,0),(.3,-7),(.5,9),(.7,7),(1,0)])
    sy=keys(t,[(0,1),(.3,.95),(.45,1.05),(.6,1),(1,1)]); sx=2-sy
    hitf=1-abs(t-.52)/.1 if abs(t-.52)<.1 else 0
    im=stage(0,True,max(0,hitf),enemy_dx=10*max(0,hitf))
    if .42<=t<.64:   # 刀光：一道月牙，随时间变淡
        from PIL import ImageFilter
        hx,hy=to_screen(Q.HAND); hx+=dx; a=(t-.42)/.22
        lay=Image.new('RGBA',SIZE,(0,0,0,0)); d=ImageDraw.Draw(lay)
        a0,a1=-110,min(75,-110+185*min(1,a*2.2)); R=175
        outer=[(hx+math.cos(math.radians(th))*R,hy+math.sin(math.radians(th))*R) for th in np.linspace(a0,a1,24)]
        inner=[(hx+math.cos(math.radians(th))*R*(.62+.3*(th-a0)/max(1,a1-a0)),hy+math.sin(math.radians(th))*R*(.62+.3*(th-a0)/max(1,a1-a0))) for th in np.linspace(a1,a0,24)]
        d.polygon(outer+inner,fill=(120,236,255,int(210*(1-a*.7))))
        d.line(outer,fill=(240,255,255,int(255*(1-a*.7))),width=4)
        im.alpha_composite(lay.filter(ImageFilter.GaussianBlur(1.2)))
    put(im,hero(head,1,ext,ang,sx,sy,dx,0)); return im
def frame_hit(t,head,widx):
    sh=math.sin(t*math.pi*6)*7*(1-t); tint=.55*(1-t)
    im=stage(0,True,0,enemy_dx=-20*math.sin(min(1,t*3)*math.pi)); put(im,hero(head,widx,0,-5*(1-t),1,1,sh-8*(1-t),0,tint)); return im
if __name__=='__main__':
    # 先出关键帧检查图
    shots=[frame_walk(.0,0,2,0),frame_walk(.25,40,2,0),frame_shoot(.32,2),frame_shoot(.42,2),frame_melee(.3,0),frame_melee(.5,0),frame_hit(.1,4,0),frame_walk(.25,0,4,0)]
    sheet=Image.new('RGB',(860*2,480*4),(255,255,255))
    for i,s in enumerate(shots): sheet.paste(s.convert('RGB'),((i%2)*860,(i//2)*480))
    sheet.resize((1290,1440),Image.LANCZOS).save('qkeys.jpg',quality=85)
