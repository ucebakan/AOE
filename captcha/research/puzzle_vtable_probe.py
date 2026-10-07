"""Recover UI class identity and vtable from the static display method; no live process access."""
import json
import struct
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent / 'speed_analysis_deps'))
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

blob = Path(r'C:\Games\4Unity\TClient.exe').read_bytes()
pe = pefile.PE(data=blob)
base = pe.OPTIONAL_HEADER.ImageBase
def raw(rva, size):
    try:
        off = pe.get_offset_from_rva(rva)
        return blob[off:off+size]
    except (pefile.PEFormatError, TypeError):
        return b''
results = []
for sec in pe.sections:
    if sec.Characteristics & 0x20000000:
        continue
    data = sec.get_data()
    needle = struct.pack('<Q', base + 0x3839D0)
    pos = data.find(needle)
    while pos >= 0:
        hit = sec.VirtualAddress + pos
        for back in range(8, 256*8, 8):
            pointer = raw(hit-back, 8)
            if len(pointer) != 8:
                break
            col = struct.unpack('<Q', pointer)[0] - base
            fields = raw(col, 24) if 0 <= col < pe.OPTIONAL_HEADER.SizeOfImage else b''
            if len(fields) != 24:
                continue
            sig, offset, cd, td, chd, own = struct.unpack('<6I', fields)
            if sig != 1 or own != col or offset != 0:
                continue
            name = raw(td+16, 180).split(b'\0')[0].decode('ascii', errors='replace')
            vt = hit-back+8
            record = {'method': hex(hit), 'vtable': hex(vt), 'slot': hex(hit-vt), 'col': hex(col), 'class': name,
                      'slots': {hex(i*8):hex(struct.unpack('<Q', raw(vt+i*8,8))[0]-base) for i in range(45)}}
            results.append(record)
            break
        pos = data.find(needle, pos+1)
Path('logs/puzzle_static/vtable.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
print(json.dumps(results, indent=2))
