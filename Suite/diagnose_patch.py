"""Read-only comparison of shipped AOBs against the installed game PE."""
import json, re, struct, pathlib, hashlib

root = pathlib.Path(__file__).resolve().parent.parent
data = pathlib.Path(r'C:\Games\4Unity\TClient.exe').read_bytes()
pe = struct.unpack_from('<I', data, 0x3c)[0]
count, opt = struct.unpack_from('<H', data, pe+6)[0], struct.unpack_from('<H', data, pe+20)[0]
sections = []
for i in range(count):
    p = pe+24+opt+40*i
    name = data[p:p+8].rstrip(b'\0').decode()
    vs, va, size, raw = struct.unpack_from('<IIII', data, p+8)
    flags = struct.unpack_from('<I', data, p+36)[0]
    sections.append((name, va, raw, size, flags))
print('SHA256', hashlib.sha256(data).hexdigest().upper())
print('PE', hex(struct.unpack_from('<I', data, pe+8)[0]), hex(struct.unpack_from('<I', data, pe+24+56)[0]))
groups = [(project,json.loads((root/rel).read_text())) for project,rel in [('XYZ','PlayerXYZ/src/signatures.json'), ('SpeedJump','SpeedJump/src/signatures.json'), ('MobTP','MobTP/tools/MobTP/signatures.json')]]
invisible = {}
for i,(rva,pattern,mask) in enumerate(re.findall(r'\(0x([0-9A-F]+), "([0-9A-F ]+)", "([x?]+)"\)',(root/'InvisibleAggro/src/Profile.cs').read_text())):
    invisible[str(i)] = {'rva':int(rva,16),'pattern':' '.join('??' if m=='?' else t for t,m in zip(pattern.split(),mask))}
groups.append(('InvisibleAggro',invisible))
for project, signatures in groups:
    for name, sig in signatures.items():
        tokens = sig.get('pattern',sig.get('Pattern')).split()
        pat = b''.join(b'.' if t == '??' else re.escape(bytes([int(t,16)])) for t in tokens)
        hits = []
        for _,va,raw,size,flags in sections:
            if flags & 0x20000000:
                hits.extend(va+m.start() for m in re.finditer(b'(?='+pat+b')',data[raw:raw+size],re.S))
        print(project,name,'hits', [hex(h) for h in hits])
        if not hits:
            prefix = b''.join(b'.' if t == '??' else re.escape(bytes([int(t,16)])) for t in tokens[:min(12,len(tokens))])
            near = []
            for _,va,raw,size,flags in sections:
                if flags & 0x20000000:
                    for m in re.finditer(b'(?='+prefix+b')',data[raw:raw+size],re.S):
                        actual = data[raw+m.start():raw+m.start()+len(tokens)]
                        score = sum(t!='??' and int(t,16)!=a for t,a in zip(tokens,actual))
                        near.append((score, va+m.start(), actual))
            for score,rva,actual in sorted(near)[:3]:
                print(' candidate',hex(rva),'fixed-byte mismatches',score,'bytes',actual.hex(' '))
