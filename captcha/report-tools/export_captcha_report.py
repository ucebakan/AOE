"""Archive the existing puzzle source and evidence; never access the game process."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def render(bundle):
    source_files = sorted(p for p in bundle.rglob('*') if p.is_file()
                          and p.name not in {'CAPTCHA_TAM_RAPOR.md', 'source-manifest.json'}
                          and not any(part in {'bin', 'obj', '__pycache__', 'releases', 'logs'}
                                      for part in p.relative_to(bundle).parts))
    entries = [{'path': p.relative_to(bundle).as_posix(), 'bytes': p.stat().st_size, 'sha256': sha(p)}
               for p in source_files]
    (bundle / 'source-manifest.json').write_text(json.dumps({'report_date': '2026-10-07',
        'snapshot_version': '1.1.0', 'game_input_during_export': False, 'files': entries},
        ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    body = (bundle / 'report-tools/captcha_report_body.md').read_text(encoding='utf-8')
    chunks = [body.rstrip() + '\n']
    for category, languages in [('src', {'.cs': 'csharp', '.csproj': 'xml', '.manifest': 'xml'}),
                                 ('research', {'.py': 'python', '.txt': 'text'}),
                                 ('src', {'.md': 'markdown', '.txt': 'text'}),
                                 ('evidence', {'.json': 'json', '.xml': 'xml'})]:
        for p in sorted((bundle / category).rglob('*')):
            if not p.is_file() or p.suffix not in languages:
                continue
            if any(part in {'bin', 'obj', 'logs', '__pycache__'} for part in p.relative_to(bundle).parts):
                continue
            relative = p.relative_to(bundle).as_posix()
            content = p.read_text(encoding='utf-8-sig').replace('\r\n', '\n').rstrip('\n')
            fence = '````'
            while fence in content:
                fence += '`'
            chunks.append(f'\n### Ek: {relative}\n\nSHA-256: `{sha(p)}`. '
                          f'Boyut: {p.stat().st_size} bayt.\n\n'
                          f'{fence}{languages[p.suffix]}\n{content}\n{fence}\n')
    chunks.append('\n### Ek: Dosya envanteri\n\n| Dosya | Bayt | SHA-256 |\n|---|---:|---|\n')
    chunks.extend(f'| {e["path"]} | {e["bytes"]} | {e["sha256"]} |\n' for e in entries)
    report = bundle / 'CAPTCHA_TAM_RAPOR.md'
    report.write_text(''.join(chunks), encoding='utf-8', newline='\n')
    return report, entries


def export(workspace, bundle):
    bundle.mkdir(parents=True, exist_ok=True)
    source = workspace / 'tools/4UnityPuzzleTest'
    for p in sorted(source.rglob('*')):
        if not p.is_file() or any(part in {'bin', 'obj', 'logs', '__pycache__'}
                                 for part in p.relative_to(source).parts):
            continue
        dest = bundle / 'src' / p.relative_to(source)
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(p, dest)
    research = bundle / 'research'
    research.mkdir(exist_ok=True)
    for name in ['puzzle_static_probe.py', 'puzzle_string_refs.py', 'puzzle_vtable_probe.py']:
        shutil.copy2(workspace / 'tools' / name, research / name)
    (research / 'requirements.txt').write_text('pefile\ncapstone\n', encoding='utf-8')
    release = workspace / 'dist/4UnityPuzzleTest-v1.1'
    evidence = bundle / 'evidence'
    shutil.copytree(release / 'verification', evidence, dirs_exist_ok=True)
    shutil.copy2(release / 'uac-manifest.xml', evidence / 'uac-manifest.xml')
    references = bundle / 'references'
    references.mkdir(exist_ok=True)
    ids = ['e7f7b9fb-73ef-4f85-a2ad-1a68ad3bbc54', 'd47d49d7-b571-44a1-9ba5-8054f18efe5f',
           'a483ba0e-1185-49aa-8e84-e00df742a535', '9e8d9c50-d452-425d-a43d-b8482c7d11fd']
    for step, image_id in enumerate(ids, 1):
        origin = Path('C:/Users/xaofx/AppData/Local/Temp') / f'codex-clipboard-{image_id}.jpg'
        shutil.copy2(origin, references / f'step-{step}.jpg')
    report_tools = bundle / 'report-tools'
    report_tools.mkdir(exist_ok=True)
    shutil.copy2(workspace / 'docs/captcha_report_body.md', report_tools / 'captcha_report_body.md')
    shutil.copy2(Path(__file__), report_tools / 'export_captcha_report.py')
    for report_name in ['self-tests.json', 'auto-tests.json', 'window-tests.json', 'automatic-session.json']:
        if json.loads((evidence / report_name).read_text(encoding='utf-8-sig'))['pass'] is not True:
            raise ValueError(f'Archived verification is not passing: {report_name}')
    build = json.loads((evidence / 'build.json').read_text(encoding='utf-8-sig'))
    executable = release / '4UnityPuzzleTest.exe'
    if sha(executable).upper() != build['sha256'].upper() or executable.stat().st_size != build['bytes']:
        raise ValueError('Existing EXE differs from archived build identity')
    report, entries = render(bundle)
    print(json.dumps({'report': str(report), 'bytes': report.stat().st_size,
                      'files': len(entries), 'exe_hash_verified': True}, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--workspace', type=Path)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--refresh', action='store_true')
    args = parser.parse_args()
    if args.refresh:
        report, entries = render(args.output.resolve() if args.output else Path(__file__).resolve().parent.parent)
        print(json.dumps({'report': str(report), 'files': len(entries)}, ensure_ascii=False))
    elif args.workspace and args.output:
        export(args.workspace.resolve(), args.output.resolve())
    else:
        parser.error('Export requires --workspace and --output; use --refresh to rebuild a packaged report')
