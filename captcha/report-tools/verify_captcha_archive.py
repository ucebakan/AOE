"""Check the exported report and source snapshot without running the GUI or game."""
import argparse
import hashlib
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('bundle', type=Path)
parser.add_argument('--original', type=Path)
args = parser.parse_args()
bundle = args.bundle.resolve()
manifest = json.loads((bundle / 'source-manifest.json').read_text(encoding='utf-8'))
report = (bundle / 'CAPTCHA_TAM_RAPOR.md').read_text(encoding='utf-8')
checked = 0
for entry in manifest['files']:
    path = bundle / entry['path']
    assert path.is_file(), entry['path']
    assert path.stat().st_size == entry['bytes'], entry['path']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], entry['path']
    if args.original and entry['path'].startswith('src/'):
        original = args.original / Path(entry['path']).relative_to('src')
        assert original.read_bytes() == path.read_bytes(), entry['path']
    if entry['path'].startswith(('src/', 'research/', 'evidence/')) and path.suffix in {'.cs', '.csproj', '.manifest', '.py', '.txt', '.md', '.json', '.xml'}:
        assert f'### Ek: {entry["path"]}\n' in report, entry['path']
        text = path.read_text(encoding='utf-8-sig').replace('\r\n', '\n').rstrip('\n')
        assert text in report, entry['path']
        checked += 1
assert len(list((bundle / 'src').glob('*.cs'))) == 11
assert len(list((bundle / 'references').glob('*.jpg'))) == 4
assert 'state' in json.loads((bundle / 'evidence/memory-probe.json').read_text())
assert report.count('\n````\n') == checked
print(json.dumps({'pass': True, 'files_verified': len(manifest['files']),
                  'complete_text_appendices': checked, 'report_bytes': (bundle / 'CAPTCHA_TAM_RAPOR.md').stat().st_size,
                  'original_source_equal': args.original is not None, 'game_accessed': False}))
