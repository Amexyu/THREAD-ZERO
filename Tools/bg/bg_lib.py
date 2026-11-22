def key(a):
    r,g,b=a[...,0],a[...,1],a[...,2]; score=np.minimum(r,b)-g; alpha=np.clip((190-score)/110,0,1)
    ex=np.clip(score,0,None); k=np.clip((score-8)/40,0,1)
    return np.dstack([r-ex*k,g,b-ex*k,alpha*255]).clip(0,255)
def seam(a,n):
    a=a.copy(); W=a.shape[1]; t=np.linspace(0,1,n)[None,:,None]; a[:,:n]=a[:,W-n:]*(1-t)+a[:,:n]*t; return a[:,:W-n]
def bestcut(a,n=280,win=12):
    W=a.shape[1]; best=None
    for x0 in range(0,n,4):
        A=a[:,x0:x0+win]
        for x1 in range(W-n,W-win,4):
            d=np.abs(A-a[:,x1:x1+win]).mean()
            if best is None or d<best[0]: best=(d,x0,x1)
    return best[1],best[2],best[0]
def tile(a,blend):
    L,R,d=bestcut(a); W=a.shape[1]; blend=min(blend,W-R)
    return a[:,L:R] if blend<=0 else seam(a[:,L:R+blend],blend)
def trim(a,f): n=int(a.shape[1]*f); return a[:,n:a.shape[1]-n]   # 去掉左右两边当画框的管子
def load(p): return np.asarray(Image.open(p).convert('RGB')).astype(np.float32)
def save(a,p): Image.fromarray(a.clip(0,255).astype(np.uint8),'RGBA' if a.shape[2]==4 else 'RGB').save(p)
