"""Read-only offline inspection of UI-related strings and RTTI. Never opens a process."""
import hashlib
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / 'speed_analysis_deps'))
import pefile

root = Path(r'C:\Games\4Unity')
output = Path('logs/puzzle_static')
output.mkdir(parents=True, exist_ok=True)
patterns = [b'captcha', b'puzzle', b'macro protection', b'matching symbol', b'attempts', b'Target:', b'protect']
report = []
for path in sorted(root.iterdir()):
    if path.suffix.lower() not in ['.exe', '.dll']:
        continue
    data = path.read_bytes()
    hits = []
    lower = data.lower()
    for term in patterns:
        for encoding in ['ascii', 'utf-16le']:
            needle = term.decode().encode(encoding)
            at = 0
            while len(hits) < 100:
                at = lower.find(needle, at)
                if at < 0:
                    break
                start = max(0, at - (40 if encoding == 'ascii' else 80))
                end = min(len(data), at + (120 if encoding == 'ascii' else 240))
                text = data[start:end].decode(encoding, errors='replace')
                hits.append({'term': term.decode(), 'offset': hex(at), 'encoding': encoding, 'context': text})
                at += len(needle)
    rtti = [m.group().decode('ascii') for m in re.finditer(rb'\.\?AV[A-Za-z0-9_?$]+@@', data)
            if any(word in m.group().lower() for word in [b'captcha', b'puzzle', b'macro', b'protect'])]
    if not hits and not rtti:
        continue
    pe = pefile.PE(data=data, fast_load=True)
    for hit in hits:
        try:
            rva = pe.get_rva_from_offset(int(hit['offset'], 16))
            if rva is not None:
                hit['rva'] = hex(rva)
        except pefile.PEFormatError:
            pass
    report.append({'file': path.name, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(), 'image_base': hex(pe.OPTIONAL_HEADER.ImageBase), 'rtti': sorted(set(rtti)), 'hits': hits})
(output / 'strings.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
for item in report:
    print(item['file'], item['sha256'], 'rtti=', item['rtti'])
    for hit in item['hits']:
        text = re.sub(r'[^\x20-\x7e]', '.', hit['context'])
        print(hit.get('rva', hit['offset']), hit['term'], hit['encoding'], text)
