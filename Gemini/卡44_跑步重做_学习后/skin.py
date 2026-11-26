# 卡44：二维网格变形（和 Spine / Live2D 的带权重网格同一个原理）。
# 一条贴图（腿：胯→踝；袖子：肩→腕）铺一张细网格，每个顶点按它在骨头上的位置分给 3 根骨头权重：
#   父骨（胯/上身，不转）→ 第一节（大腿/上臂）→ 第二节（小腿/前臂），关节附近平滑过渡，所以弯的地方是连续的，没有接缝。
import numpy as np, cv2, math
from PIL import Image

def smooth(t): t = np.clip(t, 0, 1); return t * t * (3 - 2 * t)

class Strip:
    def __init__(self, path, joints, sc, r0, r1, cols=6, rows=64, shade=1.0):
        img = np.asarray(Image.open(path).convert('RGBA')).astype(np.float32)
        if shade != 1.0: img[..., :3] *= shade
        self.prem = img.copy(); self.prem[..., :3] *= img[..., 3:4] / 255.     # 预乘 alpha，插值时边缘不发黑
        self.sc = sc; H, W = img.shape[:2]; self.size = (W, H)
        self.J = [np.array(j, float) for j in joints]
        xs = np.linspace(-.5, W - .5, cols + 1); ys = np.linspace(-.5, H - .5, rows + 1)
        X, Y = np.meshgrid(xs, ys); self.uv = np.stack([X.ravel(), Y.ravel()], 1)
        self.pu = (self.uv + .5) / sc                                               # 单位
        t = []
        for r in range(rows):
            for c in range(cols):
                i = r * (cols + 1) + c; t += [(i, i + 1, i + cols + 1), (i + 1, i + cols + 2, i + cols + 1)]
        self.tris = np.array(t)
        y = self.pu[:, 1]; j0, j1 = self.J[0][1], self.J[1][1]
        wp = 1 - smooth((y - (j0 - r0)) / (2 * r0)); w2 = smooth((y - (j1 - r1)) / (2 * r1)); w1 = np.clip(1 - wp - w2, 0, 1)
        self.w = np.stack([wp, w1, w2], 1); self.r0, self.r1 = r0, r1
        self._mip = {}
    def deform(self, Mpar, M1, M2):
        """Mpar：父骨在关节 J0 处的坐标系；M1：第一节（原点 J0）；M2：第二节（原点 J1）。返回顶点的目标坐标。"""
        p0 = np.c_[self.pu - self.J[0], np.ones(len(self.pu))].T; p1 = np.c_[self.pu - self.J[1], np.ones(len(self.pu))].T
        a = (Mpar @ p0)[:2].T; b = (M1 @ p0)[:2].T; c = (M2 @ p1)[:2].T
        return a * self.w[:, :1] + b * self.w[:, 1:2] + c * self.w[:, 2:3]
    def tex_for(self, px_per_unit):
        f = min(1.0, px_per_unit * 1.3 / self.sc); key = round(f, 3)
        if key not in self._mip:
            W, H = self.size; tw, th = max(2, int(round(W * f))), max(2, int(round(H * f)))
            self._mip[key] = (cv2.resize(self.prem, (tw, th), interpolation=cv2.INTER_AREA) if f < 1 else self.prem, tw / W, th / H)
        return self._mip[key]
    def render(self, verts_screen, canvas_size, px_per_unit):
        """verts_screen：顶点屏幕坐标（像素）。返回预乘的 RGBA 图层（numpy，H×W×4）。"""
        Wc, Hc = canvas_size; tex, fx, fy = self.tex_for(px_per_unit)
        mapx = np.full((Hc, Wc), -10, np.float32); mapy = np.full((Hc, Wc), -10, np.float32)
        V = verts_screen; UV = (self.uv + .5) * np.array([fx, fy]) - .5
        for (i, j, k) in self.tris:
            p0, p1, p2 = V[i], V[j], V[k]
            x0 = int(math.floor(min(p0[0], p1[0], p2[0]))); x1 = int(math.ceil(max(p0[0], p1[0], p2[0])))
            y0 = int(math.floor(min(p0[1], p1[1], p2[1]))); y1 = int(math.ceil(max(p0[1], p1[1], p2[1])))
            x0 = max(x0, 0); y0 = max(y0, 0); x1 = min(x1, Wc - 1); y1 = min(y1, Hc - 1)
            if x1 < x0 or y1 < y0: continue
            den = (p1[1] - p2[1]) * (p0[0] - p2[0]) + (p2[0] - p1[0]) * (p0[1] - p2[1])
            if abs(den) < 1e-9: continue
            Y, X = np.mgrid[y0:y1 + 1, x0:x1 + 1].astype(np.float32); X += .5; Y += .5
            l0 = ((p1[1] - p2[1]) * (X - p2[0]) + (p2[0] - p1[0]) * (Y - p2[1])) / den
            l1 = ((p2[1] - p0[1]) * (X - p2[0]) + (p0[0] - p2[0]) * (Y - p2[1])) / den
            l2 = 1 - l0 - l1; m = (l0 >= -.02) & (l1 >= -.02) & (l2 >= -.02)
            if not m.any(): continue
            u = l0 * UV[i, 0] + l1 * UV[j, 0] + l2 * UV[k, 0]; v = l0 * UV[i, 1] + l1 * UV[j, 1] + l2 * UV[k, 1]
            sub_x = mapx[y0:y1 + 1, x0:x1 + 1]; sub_y = mapy[y0:y1 + 1, x0:x1 + 1]; sub_x[m] = u[m]; sub_y[m] = v[m]
        # 补小洞：关节内侧三角形收成很细的扇形时，个别像素可能没被盖到 → 用周围像素的贴图坐标补上
        cov = mapx > -5; closed = cv2.morphologyEx(cov.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8)).astype(bool); holes = closed & ~cov
        kk = np.ones((3, 3), np.float32)
        for _ in range(3):
            if not holes.any(): break
            mx = cv2.filter2D(np.where(cov, mapx, 0).astype(np.float32), -1, kk, borderType=cv2.BORDER_CONSTANT)
            my = cv2.filter2D(np.where(cov, mapy, 0).astype(np.float32), -1, kk, borderType=cv2.BORDER_CONSTANT)
            n = cv2.filter2D(cov.astype(np.float32), -1, kk, borderType=cv2.BORDER_CONSTANT); f = holes & (n > 0)
            mapx[f] = mx[f] / n[f]; mapy[f] = my[f] / n[f]; cov |= f; holes &= ~f
        return cv2.remap(tex, mapx, mapy, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0))

def _rot(v, a):
    c, s = math.cos(a), math.sin(a); v = np.asarray(v, float)
    return np.stack([v[..., 0] * c - v[..., 1] * s, v[..., 0] * s + v[..., 1] * c], -1)

def deform_fillet(st, Mpar, M1, M2, r0, rk):
    """关节用「圆角弯」：关节前后 rk 的一段沿着和大腿、小腿都相切的圆弧走，外侧是圆的，内侧收成一道折痕（不会翻折出台阶）。
    最上面（胯/肩）仍然用权重和父骨过渡（藏在短裤/袖口里）。"""
    pu = st.pu; J0, J1, J2 = st.J
    h0 = lambda M, p: (M @ np.c_[p, np.ones(len(p))].T)[:2].T
    out = np.zeros((len(pu), 2))
    yk = pu[:, 1] - J1[1]
    # 1) 第一节（含最上面和父骨的过渡）
    A = yk < -rk
    wp = (1 - smooth((pu[A, 1] - (J0[1] - r0)) / (2 * r0)))[:, None]
    out[A] = h0(Mpar, pu[A] - J0) * wp + h0(M1, pu[A] - J0) * (1 - wp)
    # 2) 第二节
    B = yk > rk
    out[B] = h0(M2, pu[B] - J1)
    # 3) 关节圆角
    C = ~(A | B)
    PJ0 = h0(M1, np.array([[0., 0.]]))[0]; PJ1 = h0(M2, np.array([[0., 0.]]))[0]; PJ2 = h0(M2, np.array([J2 - J1]))[0]
    u1 = (PJ1 - PJ0) / np.linalg.norm(PJ1 - PJ0); u2 = (PJ2 - PJ1) / np.linalg.norm(PJ2 - PJ1)
    phi = math.atan2(u1[0] * u2[1] - u1[1] * u2[0], u1 @ u2)
    sc_ax = np.linalg.norm(M1[:2, 1]); r_px = rk * sc_ax
    lat0 = M1[:2, 0]                                   # 贴图 x 方向（1 单位）在屏幕上的向量
    t = (yk[C] + rk) / (2 * rk); xl = (pu[C, 0] - J1[0])
    P1 = PJ1 - u1 * r_px
    if abs(phi) < math.radians(.5):
        cen = P1[None] + u1[None] * (2 * r_px * t[:, None]); lat = np.repeat(lat0[None], len(t), 0)
    else:
        R = r_px / math.tan(abs(phi) / 2); nt = _rot(u1, math.copysign(math.pi / 2, phi)); O = P1 + nt * R
        al = phi * t; cen = O[None] + _rot(np.repeat((P1 - O)[None], len(t), 0), al) if False else O[None] + np.stack([_rot(P1 - O, a) for a in al])
        lat = np.stack([_rot(lat0, a) for a in al])
        # 内侧（朝圆心那边）不能超过半径：压成折痕
    out[C] = cen + lat * xl[:, None]
    # 内侧挤成一道折痕：上下两节的内侧肉挤在一起，沿着弯角的角平分线贴住（越过平分线的点投影回线上）→ 不留缺口、不互相穿插
    if abs(phi) > math.radians(10):
        a = -u1; bb = u2; d = a + bb; d = d / np.linalg.norm(d)
        cr = lambda v: d[0] * v[..., 1] - d[1] * v[..., 0]
        s_up = np.sign(cr(a))
        rel = out - PJ1; side = np.sign(cr(rel))
        upper = yk < 0
        wrong = np.where(upper, side == -s_up, side == s_up) & (rel @ d > 0)    # 只管弯的内侧那一半
        proj = PJ1 + np.outer(rel[wrong] @ d, d)
        out[wrong] = proj
    return out
