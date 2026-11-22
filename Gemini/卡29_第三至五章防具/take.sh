#!/bin/bash
# 用法：bash take.sh <名字> <部件> id1 … id5 —— 等收件箱里出现一张没用过的 Gemini 图（最多 60 秒），存成 出图/<名字>_原图.png，切图，出预览
set -e; cd "$(dirname "$0")"; name=$1; part=$2; shift 2; touch used.txt
for i in $(seq 60); do
  f=$(ls -t ../_收件箱/出图/Gemini_Generated_Image_*.jpeg ../_收件箱/出图/Gemini_Generated_Image_*.png 2>/dev/null | while read x; do grep -qxF "$(basename "$x")" used.txt || { echo "$x"; break; }; done)
  [ -n "$f" ] && break; sleep 1
done
[ -n "$f" ] || { echo "没等到新图"; exit 1; }
basename "$f" >> used.txt; cp "$f" "出图/${name}_原图.png"; echo "用 $(basename "$f")"
python3 ../../Tools/bg/armor_sheet.py cut $part "出图/${name}_原图.png" "$@" --preview "../_tmp/${name}_prev.jpg" | tail -5
