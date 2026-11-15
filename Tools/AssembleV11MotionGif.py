from pathlib import Path
import struct
import sys
root = Path(__file__).resolve().parent.parent
v12 = '--v12' in sys.argv
folder = root / ('Deliverables/ArtPack_v12/Review' if v12 else 'Deliverables/ArtPack_v11/Review')
out = bytearray()
v2 = '--v2' in sys.argv
v3 = '--v3' in sys.argv
dagger = '--dagger' in sys.argv
power = '--power' in sys.argv
stab = '--stab' in sys.argv
stabfix = '--stabfix' in sys.argv
guard = '--guard' in sys.argv
fast = '--fast' in sys.argv
frames = sorted((folder / ('StabGuardFrames' if guard else 'StabCleanFrames' if stabfix else 'Stab12Frames' if stab else 'PowerFrames' if power else 'DaggerFramesV4' if dagger else 'MotionFramesV3' if v3 else 'MotionFramesV2' if v2 else 'MotionFrames')).glob('*.gif'))
melee_only = '--melee' in sys.argv
if melee_only:
    frames = frames[120:160]
for n, p in enumerate(frames):
    b = p.read_bytes()
    packed = b[10]
    table = b[13:13+3*(2**((packed&7)+1))] if packed&128 else b''
    offset = 13+len(table)
    if n == 0:
        out += b[:13]+table+b'\x21\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00'
    while b[offset] != 0x2c:
        assert b[offset] == 0x21
        offset += 2
        while b[offset]:
            offset += 1+b[offset]
        offset += 1
    desc = bytearray(b[offset:offset+10]); offset += 10
    if desc[9]&128:
        length = 3*2**((desc[9]&7)+1)
        local = b[offset:offset+length]; offset += length
    else:
        local = table; desc[9] = (desc[9]&0x78)|0x80|(packed&7)
    data = bytearray(b[offset:offset+1]); offset += 1
    while b[offset]:
        length = b[offset]; data += b[offset:offset+length+1]; offset += length+1
    data += b'\0'
    delay = ([16,8,6,4,3,3,5,3,5,7,8,12][n] * (1 if fast else 3)) if stab else (2 if fast else 7)
    if stabfix:
        delay = [10,3,3,2,2,3,2,2,2,2,2,10][n] * (1 if fast else 3)
    if guard:
        delay = [10,3,2,2,2,2,3,2,2,2,3,10][n] * (1 if fast else 3)
    out += b'\x21\xf9\x04\x04'+struct.pack('<H',delay)+b'\x00\x00'+desc+local+data
out += b'\x3b'
target = folder / ('V11_Outfit_Motion_v3.gif' if v3 else 'V11_Outfit_Motion_v2.gif' if v2 else 'V11_Outfit_Motion.gif')
if melee_only:
    target = folder / 'V11_Melee_v3.gif'
if dagger:
    target = folder / (('V12_Dagger_Motion_fast.gif' if fast else 'V12_Dagger_Motion_slow.gif') if v12 else ('V11_Dagger_Skills_v4_fast.gif' if fast else 'V11_Dagger_Skills_v4.gif'))
if power:
    target = folder / 'V12_Dagger_Power_v2.gif'
if stab:
    target = folder / ('V12_Stab12_fast.gif' if fast else 'V12_Stab12_slow.gif')
if stabfix:
    target = folder / ('V12_Stab_fixed_fast.gif' if fast else 'V12_Stab_fixed_slow.gif')
if guard:
    target = folder / ('V12_Stab_guard_fast.gif' if fast else 'V12_Stab_guard_slow.gif')
target.write_bytes(out)
duration = .8 * (1 if fast else 3) if stab else len(frames)*(.02 if fast else .07)
if stabfix or guard:
    duration = .43 * (1 if fast else 3)
print(f'{target}: {len(frames)} frames, {len(out)} bytes, {duration:.1f} seconds')
