from pathlib import Path
import struct
root=Path(__file__).resolve().parent.parent
out=bytearray()
for n,p in enumerate(sorted((root/'Artifacts/AttackGifFrames').glob('*.gif'))):
    b=p.read_bytes();packed=b[10];table=b[13:13+3*(2**((packed&7)+1))] if packed&128 else b''
    offset=13+len(table)
    if n==0:
        out+=b[:13]+table+b'\x21\xff\x0bNETSCAPE2.0\x03\x01\x00\x00\x00'
    while b[offset]!=0x2c:
        assert b[offset]==0x21
        offset+=2
        while b[offset]:offset+=1+b[offset]
        offset+=1
    desc=bytearray(b[offset:offset+10]);offset+=10
    if desc[9]&128:
        length=3*2**((desc[9]&7)+1);local=b[offset:offset+length];offset+=length
    else:
        local=table;desc[9]=(desc[9]&0x78)|0x80|(packed&7)
    data=bytearray(b[offset:offset+1]);offset+=1
    while b[offset]:
        length=b[offset];data+=b[offset:offset+length+1];offset+=length+1
    data+=b'\0'
    out+=b'\x21\xf9\x04\x04'+struct.pack('<H',4)+b'\x00\x00'+desc+local+data
out+=b'\x3b'
target=root/'Artifacts/AttackAnimationV5.gif';target.write_bytes(out)
print(f'Animation: {target.name} ({len(out)} bytes, 36 frames)')
