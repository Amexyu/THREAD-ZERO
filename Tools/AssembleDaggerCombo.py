"""Export six approved key poses without blending limbs or inventing inbetweens."""
from pathlib import Path
import json
import struct
import sys

folder = Path(__file__).resolve().parent.parent / 'Deliverables/ArtPack_v12/Review'
sequence = [5, 0, 1, 2, 3, 4, 5]
durations = [200, 90, 50, 60, 80, 70, 100]
combo18 = '--combo18' in sys.argv
v2 = '--v2' in sys.argv
suffix = '_v2' if v2 else ''
if combo18:
    sequence = list(range(18))
    durations = [120,40,50,20,20,30,20,20,40,30,30,50,20,20,50,30,30,60]

def assemble(multiplier):
    out = bytearray()
    for n, pose in enumerate(sequence):
        frame_dir = 'Combo18v2Frames' if v2 else 'Combo18Frames'
        frame_path = f'{frame_dir}/pose_{pose:02d}.gif' if combo18 else f'Combo6Frames/pose_{pose}.gif'
        data = (folder / frame_path).read_bytes()
        packed = data[10]
        table = data[13:13 + 3 * 2 ** ((packed & 7) + 1)] if packed & 128 else b''
        offset = 13 + len(table)
        if n == 0:
            out += data[:13] + table + b'\x21\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00'
        while data[offset] != 0x2c:
            assert data[offset] == 0x21
            offset += 2
            while data[offset]:
                offset += 1 + data[offset]
            offset += 1
        desc = bytearray(data[offset:offset + 10])
        offset += 10
        if desc[9] & 128:
            length = 3 * 2 ** ((desc[9] & 7) + 1)
            local = data[offset:offset + length]
            offset += length
        else:
            local = table
            desc[9] = (desc[9] & 0x78) | 0x80 | (packed & 7)
        blocks = bytearray(data[offset:offset + 1])
        offset += 1
        while data[offset]:
            length = data[offset]
            blocks += data[offset:offset + length + 1]
            offset += length + 1
        blocks += b'\0'
        delay = durations[n] * multiplier // 10
        out += b'\x21\xf9\x04\x04' + struct.pack('<H', delay) + b'\0\0' + desc + local + blocks
    return out + b'\x3b'

for name, multiplier in [('fast', 1), ('slow', 3)]:
    target = folder / f'V12_Dagger_Combo{18 if combo18 else 6}{suffix}_{name}.gif'
    target.write_bytes(assemble(multiplier))
    print(f'{target.name}: 640x560, {len(sequence)} playback frames, {sum(durations)*multiplier}ms, {target.stat().st_size} bytes')
(folder / f'Combo{18 if combo18 else 6}{suffix}_timing.json').write_text(json.dumps({
    'source': f'Dagger_Combo{18 if combo18 else 6}{suffix}.png', 'source_grid': [6, 3] if combo18 else [3, 2],
    'sequence_zero_based': sequence, 'duration_ms': durations,
    'slow_multiplier': 3, 'kind': '18 drawn motion phases preview, not rigged runtime animation' if combo18 else 'six-key-pose timing preview, not runtime animation'
}, indent=2), encoding='utf-8')
