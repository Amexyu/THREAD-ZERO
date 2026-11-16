"""Render a weapon-free, zero-angle rest pose using rig.py's actual matrices.

Usage: python Tools/RigPrototype/review_starter.py manifest.json output_dir
The delivered rig coordinates are the only source of attachment positions.
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw
import rig


def main(manifest_path, output_dir):
    manifest_path = Path(manifest_path)
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    sp = rig.set_from_manifest(str(manifest_path))
    parts = dict(armor=sp["armor"](), head=sp["head"](), pack=sp["pack"](),
                 wpn=sp["wpn"](), hand=sp["hand"]())
    # Neutral, upright, arms down. No animated offsets or squashing.
    pose = dict(dx=0, dy=0, sx=1, sy=1, lean=0, head=0, pack=0,
                A=0, Af=0, L=0, Lf=0, W=0, wt=0, fx=False)
    matrices = rig.matrices(parts, pose)
    canvas = Image.new("RGBA", (300 * rig.R, 260 * rig.R))
    armor = parts["armor"]
    for part, matrix in (
        (parts["pack"], "pack"), (armor["armF"], "armF"),
        (parts["hand"], "handF"), (armor["legF"], "legF"),
        (armor["legN"], "legN"), (armor["torso"], "torso"),
        (parts["head"], "head"), (armor["arm"], "armN"),
        (parts["hand"], "hand"),
    ):
        rig.blit(canvas, part, matrices[matrix])
    bbox = canvas.getbbox()
    pad = 12 * rig.R
    crop = (bbox[0] - pad, bbox[1] - pad, bbox[2] + pad, bbox[3] + pad)
    canvas.crop(crop).save(output_dir / "idle_rest.png")

    measurements = []
    for name, entry in manifest["parts"].items():
        im = Image.open(manifest_path.parent / entry["file"]).convert("RGBA")
        alpha = im.getchannel("A")
        w, h = im.size
        border_clear = all(alpha.getpixel((x, y)) == 0
                           for y in range(h) for x in range(w)
                           if x < 2 or y < 2 or x >= w - 2 or y >= h - 2)
        item = dict(part=name, pixels=list(im.size),
                    units=[v / manifest["scale"] for v in im.size],
                    pivot=entry["pivot"], transparent_border_2px=border_clear,
                    rgba=im.mode == "RGBA")
        if name in ("arm", "leg_near", "leg_far", "weapon"):
            cx, cy = [round(v * manifest["scale"]) for v in entry["pivot"]]
            radius = 3 * manifest["scale"]
            region = alpha.crop((cx-radius, cy-radius, cx+radius, cy+radius))
            item["overlap_3unit_opaque_ratio"] = sum(region.histogram()[129:]) / (region.width * region.height)
        measurements.append(item)
    (output_dir / "measurements.json").write_text(
        json.dumps(measurements, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    head = Image.open(manifest_path.parent / manifest["parts"]["head"]["file"])
    dagger = Image.open(manifest_path.parent / manifest["parts"]["weapon"]["file"])
    head_bbox = head.getchannel("A").getbbox()
    dagger_bbox = dagger.getchannel("A").getbbox()
    height = (bbox[3] - bbox[1]) / rig.R
    head_height = (head_bbox[3] - head_bbox[1]) / manifest["scale"]
    dagger_height = (dagger_bbox[3] - dagger_bbox[1]) / manifest["scale"]
    baseline = dict(
        proportion_profile=manifest["proportion_profile"],
        scale=manifest["scale"], rig=manifest["rig"],
        shoulder=manifest["shoulder"], shoulderF=manifest["shoulderF"],
        rest_pose=pose, rest_has_weapon=False, rest_pixels_per_unit=rig.R,
        ground_origin_in_idle_rest_pixels=[rig.ROOT_X*rig.R-crop[0], rig.GROUND*rig.R-crop[1]],
        height_units=height, head_visible_height_units=head_height,
        head_height_ratio=head_height/height,
        dagger_visible_height_units=dagger_height,
        dagger_height_ratio=dagger_height/height,
        future_parts="h1-h4/c1-c4/b1-b4 and other weapons must use this revised S1 unit scale and attachment coordinates; no per-part fit-to-frame scaling.",
    )
    (output_dir / "proportions.json").write_text(
        json.dumps(baseline, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    assert .45 <= baseline["head_height_ratio"] <= .52, baseline
    assert all(x["transparent_border_2px"] for x in measurements)
    assert all(x.get("overlap_3unit_opaque_ratio", 1) == 1 for x in measurements)
    # Same display scale as rig.render, so the review does not normalize each
    # pose independently and hide proportion differences.
    rest = canvas.crop(crop)
    display_scale = rig.CELL_W / (210 * rig.R)
    rest = rest.resize((round(rest.width*display_scale), round(rest.height*display_scale)), Image.Resampling.LANCZOS)
    contact = Image.new("RGBA", (rig.CELL_W*3, rig.CELL_H+44), rig.BG)
    gx, gy = baseline["ground_origin_in_idle_rest_pixels"]
    ground_y = 22 + (rig.GROUND-50)*rig.CELL_H/200
    contact.alpha_composite(rest, (round(rig.CELL_W/2-gx*display_scale), round(ground_y-gy*display_scale)))
    for column, animation in enumerate(("walk", "attack"), 1):
        contact.alpha_composite(rig.render(sp, animation, .5), (column*rig.CELL_W, 22))
    draw = ImageDraw.Draw(contact)
    for column, label in enumerate(("REST / no weapon", "WALK / rig.py", "ATTACK / rig.py")):
        draw.text((column*rig.CELL_W+16, rig.CELL_H+27), label, fill=(25,26,28))
    contact.convert("RGB").save(output_dir / "assembly.png")
    print(f"idle_rest: height={height:.2f} units; head={head_height/height:.1%}; dagger={dagger_height/height:.1%}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
