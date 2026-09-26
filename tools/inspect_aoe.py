"""Offline-only AOE PE/signature audit. Never opens a process or writes the target."""
import bisect
import datetime
import hashlib
import json
from pathlib import Path
import re
import struct
import sys

sys.path.insert(0, str(Path(r"C:\Users\Public\Documents\4UnityAOETracer\tools\vendor")))
import capstone

ROOT = Path(__file__).resolve().parents[1]
data = Path(r"C:\Games\4Unity\TClient.exe").read_bytes()
u16 = lambda p: struct.unpack_from('<H', data, p)[0]
u32 = lambda p: struct.unpack_from('<I', data, p)[0]
pe = u32(0x3c)
assert data[:2] == b'MZ' and data[pe:pe+4] == b'PE\0\0'
opt = pe + 24
assert u16(pe+4) == 0x8664 and u16(opt) == 0x20b
sections = []
for n in range(u16(pe+6)):
    p = opt + u16(pe+20) + n*40
    sections.append((data[p:p+8].rstrip(b'\0').decode(), u32(p+12), u32(p+8), u32(p+20), u32(p+16), u32(p+36)))

def offset(rva, size=1):
    for _, v, _, raw, count, _ in sections:
        if v <= rva and rva-v+size <= count:
            assert raw+rva-v+size <= len(data)
            return raw+rva-v
    raise ValueError(f'Unbacked RVA {rva:X} size {size}')

def read(rva, size):
    p = offset(rva, size)
    return data[p:p+size]

ex, exsize = struct.unpack_from('<II', data, opt+112+24)
functions = [struct.unpack_from('<III', data, offset(ex)+i) for i in range(0, exsize, 12)]
starts = [f[0] for f in functions]
def extent(rva):
    i = bisect.bisect_right(starts, rva)-1
    return functions[i] if i >= 0 and rva < functions[i][1] else None

cs = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
cs.detail = True
def scan(pattern):
    rex = re.compile(b''.join(b'.' if x in ('?', '??') else re.escape(bytes([int(x,16)])) for x in pattern.split()), re.DOTALL)
    hits = []
    for _, v, _, raw, count, flags in sections:
        if flags & 0x20000000:
            hits += [v+m.start() for m in re.finditer(b'(?='+rex.pattern+b')', data[raw:raw+count], re.DOTALL)]
    return hits

def asm(start, end):
    for i in cs.disasm(read(start, end-start), start):
        print(f'{i.address:08X} {i.bytes.hex(" ").upper():<44} {i.mnemonic:<8} {i.op_str}')

if __name__ == '__main__':
    command = sys.argv[1] if len(sys.argv)>1 else 'identity'
    if command == 'identity':
        print(json.dumps(dict(path=r'C:\Games\4Unity\TClient.exe', sha256=hashlib.sha256(data).hexdigest().upper(), file_size=len(data), architecture='AMD64', timestamp=hex(u32(pe+8)), timestamp_utc=datetime.datetime.fromtimestamp(u32(pe+8),datetime.timezone.utc).isoformat(), preferred_base=hex(struct.unpack_from('<Q',data,opt+24)[0]), image_size=hex(u32(opt+56)), sections=sections), indent=2))
    elif command == 'signatures':
        for a in json.loads((ROOT/'signatures/aoe_signatures.json').read_text())['anchors']:
            hits = scan(a['maskedPattern'])
            print(a['anchorName'], 'matches=',len(hits), 'rvas=',','.join(hex(x) for x in hits[:30]))
    elif command == 'scan':
        hits = scan(sys.argv[2]); print('matches=',len(hits), 'rvas=',','.join(hex(x) for x in hits[:100]))
    elif command == 'asm':
        start=int(sys.argv[2],0); end=int(sys.argv[3],0) if len(sys.argv)>3 else (extent(start) or [start,start+256])[1]
        print('extent=',extent(start)); asm(start,end)
    elif command == 'callers':
        target=int(sys.argv[2],0)
        for _,v,_,raw,count,flags in sections:
            if not flags & 0x20000000: continue
            for m in re.finditer(b'\xE8',data[raw:raw+count-4]):
                rva=v+m.start()
                if rva+5+struct.unpack('<i',read(rva+1,4))[0] != target: continue
                f=extent(rva)
                if f and any(i.address==rva and i.mnemonic=='call' for i in cs.disasm(read(f[0],rva+5-f[0]),f[0])):
                    print(hex(rva),'return=',hex(rva+5),'function=',hex(f[0]))
