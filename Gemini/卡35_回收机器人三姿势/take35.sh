#!/bin/bash
# 卡35：把收件箱里最新下载的一张（2 分钟内）改名移到 出图/<怪>_三姿势_原图[_N].jpeg，再切图。用法：bash take35.sh <怪名> [cut35.py 的其它参数]
cd "$(dirname "$0")"; name="$1"; shift
f=$(ls -t ../_收件箱/出图/Gemini_Generated_Image_* 2>/dev/null | head -1)
[ -n "$f" ] && [ -z "$(find "$f" -mmin -2)" ] && f=""   # 只认 2 分钟内的新下载
[ -z "$f" ] && { echo "没有新下载"; exit 1; }
dst="出图/${name}_三姿势_原图.jpeg"; n=2; while [ -e "$dst" ]; do dst="出图/${name}_三姿势_原图_$n.jpeg"; n=$((n+1)); done
mv "$f" "$dst" && echo "取到 $(basename "$f") → $dst" && python3 cut35.py "$dst" "$name" "$@"
