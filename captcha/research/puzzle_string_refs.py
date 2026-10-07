"""Disassemble only static references to the puzzle's display labels; no process access."""
import json
import re
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / 'speed_analysis_deps'))
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

path = Path(r'C:\Games\4Unity\TClient.exe')
blob = path.read_bytes()
pe = pefile.PE(data=blob)
base = pe.OPTIONAL_HEADER.ImageBase
labels = {}
for literal in [b'Macro protection puzzle\0', b'Click the matching symbol\0', b'Target:  %s\0', b'Step %u of %u\0', b'%u attempts - %u seconds\0']:
    off = blob.find(literal)
    if off >= 0:
        labels[pe.get_rva_from_offset(off)] = literal[:-1].decode()
decoder = Cs(CS_ARCH_X86, CS_MODE_64)
entries = [(e.struct.BeginAddress, e.struct.EndAddress) for e in pe.DIRECTORY_ENTRY_EXCEPTION]
refs = []
for sec in pe.sections:
    if not sec.Characteristics & 0x20000000:
        continue
    data = sec.get_data()
    for match in re.finditer(rb'[\x48-\x4f]\x8d[\x05\x0d\x15\x1d\x25\x2d\x35\x3d].{4}', data, re.S):
        rva = sec.VirtualAddress + match.start()
        target = rva + 7 + struct.unpack_from('<i', match.group(), 3)[0]
        if target not in labels:
            continue
        bounds = next(((b, e) for b, e in entries if b <= rva < e), None)
        if bounds is None:
            continue
        b, e = bounds
        instructions = list(decoder.disasm(blob[pe.get_offset_from_rva(b):pe.get_offset_from_rva(b) + e - b], base + b))
        detail = [{'rva': hex(i.address-base), 'instruction': i.mnemonic + ' ' + i.op_str} for i in instructions]
        refs.append({'label': labels[target], 'label_rva': hex(target), 'ref_rva': hex(rva), 'function': [hex(b),hex(e)], 'instructions': detail})
out = Path('logs/puzzle_static/refs.json')
out.write_text(json.dumps({'labels': {hex(k):v for k,v in labels.items()}, 'refs': refs}, indent=2), encoding='utf-8')
for ref in refs:
    print(ref['label'], ref['ref_rva'], ref['function'])
    for i in ref['instructions']:
        print(i['rva'], i['instruction'])
