import numpy as np
def medfilt(v, k):
    h = k // 2; p = np.pad(v, h, mode='edge'); return np.array([np.median(p[i:i + k]) for i in range(len(v))])
