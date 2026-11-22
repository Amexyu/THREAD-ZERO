import numpy as np
from collections import deque
def components(m, k=4):
    """m: bool mask. returns list of (size, (x0,y0,x1,y1)) in full-res coords, biggest first, computed on a k-downsampled grid"""
    h,w=m.shape; hs,ws=h//k,w//k
    g=m[:hs*k,:ws*k].reshape(hs,k,ws,k).any(axis=(1,3))
    seen=np.zeros_like(g); out=[]
    for y in range(hs):
        for x in range(ws):
            if g[y,x] and not seen[y,x]:
                q=deque([(y,x)]); seen[y,x]=True; pts=[]
                while q:
                    cy,cx=q.popleft(); pts.append((cy,cx))
                    for dy in (-1,0,1):
                        for dx in (-1,0,1):
                            ny,nx=cy+dy,cx+dx
                            if 0<=ny<hs and 0<=nx<ws and g[ny,nx] and not seen[ny,nx]: seen[ny,nx]=True; q.append((ny,nx))
                ys=[p[0] for p in pts]; xs=[p[1] for p in pts]
                out.append((len(pts),(min(xs)*k,min(ys)*k,max(xs)*k+k-1,max(ys)*k+k-1)))
    out.sort(reverse=True); return out
