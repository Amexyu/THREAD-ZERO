"""Validate a delivered layered-part set (parts.json + PNGs).
Usage: python validate_parts.py path/to/parts.json
Exit code 0 = all pass, 1 = failures. Sizes/pivots are in 1x units (PNG px / manifest.scale).
"""
import json, os, sys
from PIL import Image

# slot -> (w_min, w_max, h_min, h_max, pivot rule, description)
SPEC = {
    "head":   (88, 110, 76, 96, "bottom-center: pivot.x ~ w/2 (+-8), pivot.y in [h-12, h-4]"),
    "torso":  (46, 68, 34, 50, "hip: pivot.x ~ w/2 (+-6), pivot.y in [h-6, h-1]"),
    "arm":    (18, 36, 32, 44, "shoulder: pivot.x ~ w/2 (+-4), pivot.y in [5, 13]"),
    "hand":   (14, 26, 14, 26, "palm centre: pivot within +-3 of the image centre"),
    "leg_near": (28, 38, 24, 34, "hip: pivot.x in [8, 16], pivot.y in [2, 7]"),
    "leg_far":  (28, 38, 24, 34, "hip: pivot.x in [8, 16], pivot.y in [2, 7]"),
    "legs":   (28, 38, 24, 34, "hip: pivot.x in [8, 16], pivot.y in [2, 7]"),
    "pack":   (22, 44, 40, 62, "attach: pivot.x in [w-10, w-2], pivot.y in [0.5h-4, 0.85h]"),
    "weapon": (10, 40, 60, 140, "grip: pivot.x ~ w/2 (+-5), pivot.y in [2, 45]; needs 'tip'"),
}

# S1 review deliberately reduces head / dagger sizes. Keep the original sample
# limits for manifests without this explicit proportion profile.
PROFILE_SPEC = {
    "s1_revised": {
        "head": (80, 88, 68, 80, SPEC["head"][4]),
        "weapon": (10, 40, 40, 50, SPEC["weapon"][4]),
    },
}

def pivot_ok(slot, w, h, px, py):
    if slot == "head":   return abs(px - w / 2) <= 8 and h - 12 <= py <= h - 4
    if slot == "torso":  return abs(px - w / 2) <= 6 and h - 6 <= py <= h - 1
    if slot == "arm":    return abs(px - w / 2) <= 4 and 5 <= py <= 13
    if slot in ("legs", "leg_near", "leg_far"): return 8 <= px <= 16 and 2 <= py <= 7
    if slot == "hand":   return abs(px - w / 2) <= 3 and abs(py - h / 2) <= 3
    if slot == "pack":   return w - 10 <= px <= w - 2 and 0.5 * h - 4 <= py <= 0.85 * h
    if slot == "weapon": return abs(px - w / 2) <= 5 and 2 <= py <= 45
    return False

def main(path):
    m = json.load(open(path, encoding="utf-8")); base = os.path.dirname(os.path.abspath(path))
    sc = float(m.get("scale", 4)); fails = []; notes = []
    parts = m.get("parts", {})
    profile = m.get("proportion_profile")
    if profile is not None and profile not in PROFILE_SPEC:
        fails.append(f"unknown proportion_profile: {profile}")
    spec = {**SPEC, **PROFILE_SPEC.get(profile, {})}
    need = ["head", "torso", "arm", "hand", "pack", "weapon"] + (["leg_near", "leg_far"] if "leg_near" in parts or "legs" not in parts else ["legs"])
    for slot in need:
        if slot not in parts: fails.append(f"{slot}: 缺少该部件"); continue
        e = parts[slot]; fp = os.path.join(base, e["file"])
        if not os.path.exists(fp): fails.append(f"{slot}: 文件不存在 {e['file']}"); continue
        im = Image.open(fp)
        if im.mode != "RGBA": fails.append(f"{slot}: 必须是带透明通道的 RGBA PNG（现在是 {im.mode}）"); im = im.convert("RGBA")
        w, h = im.size[0] / sc, im.size[1] / sc
        wmin, wmax, hmin, hmax, rule = spec[slot]
        if not (wmin <= w <= wmax and hmin <= h <= hmax):
            fails.append(f"{slot}: 尺寸 {w:.0f}x{h:.0f} 单位超出范围 {wmin}-{wmax} x {hmin}-{hmax}（×{sc:g} 才是像素）")
        px, py = e["pivot"]
        if not pivot_ok(slot, w, h, px, py): fails.append(f"{slot}: 锚点 ({px},{py}) 不符合规则：{rule}")
        a = im.split()[3]
        W, H = im.size
        # transparent 0.5-unit border so parts never get clipped
        b = max(1, int(sc / 2))
        border = [a.crop((0, 0, W, b)), a.crop((0, H - b, W, H)), a.crop((0, 0, b, H)), a.crop((W - b, 0, W, H))]
        if any(x.getextrema()[1] > 8 for x in border): fails.append(f"{slot}: 图像边缘 0.5 单位内有不透明像素，部件被裁切或没留边")
        # pivot must sit on opaque pixels (joint overlap)
        # 只检查会外露接缝的部件：手臂/腿/武器（头、躯干、背部件的关节被相邻部件盖住）
        r = int((1.5 if slot == "weapon" else 4) * sc); cx, cy = int(px * sc), int(py * sc); cnt = tot = 0
        if slot not in ("arm", "legs", "leg_near", "leg_far", "weapon"): r = 0
        for yy in range(max(0, cy - r), min(H, cy + r), 2):
            for xx in range(max(0, cx - r), min(W, cx + r), 2):
                tot += 1; cnt += a.getpixel((xx, yy)) > 128
        if tot and cnt / tot < 0.85: fails.append(f"{slot}: 锚点周围 {r / sc:g} 单位内只有 {cnt / tot:.0%} 不透明，关节处没有画重叠，转动会露缝")
        if slot == "arm" and "hand" not in e: fails.append("arm: manifest 缺少 hand（手点坐标，单位同 pivot）")
        if slot == "weapon":
            if "tip" not in e: fails.append("weapon: manifest 缺少 tip（武器尖端坐标）")
        bbox = a.point(lambda v: 255 if v > 8 else 0).getbbox()
        if bbox is None: fails.append(f"{slot}: 图像全透明")
        else: notes.append(f"{slot}: {w:.0f}x{h:.0f} 单位, 锚点 ({px:g},{py:g}) OK")
    if m.get("weapon_class") not in ("dagger", "spear", "hammer", "gun", "sword", "shield", "other"):
        fails.append("weapon_class 必须是 dagger/spear/hammer/gun/sword/shield/other 之一")
    print("\n".join(notes))
    if fails:
        print("\n不通过：\n- " + "\n- ".join(fails)); return 1
    print("\n全部通过"); return 0

if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
