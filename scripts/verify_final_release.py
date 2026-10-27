"""Snapshot and verify a no-catalog-change release, without rewriting personal data."""
import argparse
import hashlib
import json
from pathlib import Path

def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

parser=argparse.ArgumentParser()
parser.add_argument('mode',choices=['snapshot','verify'])
parser.add_argument('data')
parser.add_argument('manifest')
parser.add_argument('--report')
args=parser.parse_args()
root=Path(args.data).resolve(strict=True)
manifest=Path(args.manifest)
files={p.relative_to(root).as_posix():digest(p) for p in root.rglob('*') if p.is_file()}
settings=json.loads((root/'settings.json').read_text('utf-8-sig'))
if args.mode=='snapshot':
    manifest.parent.mkdir(parents=True,exist_ok=True)
    manifest.write_text(json.dumps({'files':files,'settings':settings},ensure_ascii=False,indent=2),'utf-8')
    print(f'SNAPSHOT {len(files)} files, {len(settings["Tracks"])} tracks')
else:
    original=json.loads(manifest.read_text('utf-8'))
    assert settings==original['settings'],'Settings or track metadata changed'
    assert len({t['Id'] for t in settings['Tracks']})==len(settings['Tracks']),'Duplicate track ids'
    immutable={name:sha for name,sha in original['files'].items() if name not in ('settings.json','settings.json.bak')}
    assert all(files.get(name)==sha for name,sha in immutable.items()),'Audio, artwork, font or appearance data changed'
    assert set(files)==set(original['files']),'Data files were added or omitted'
    report={'version':'0.1.0-beta.2','tracks':len(settings['Tracks']),'opening':sum(t['Category']!='draft' for t in settings['Tracks']),
            'bp':sum(t['Category']=='draft' for t in settings['Tracks']),'immutable_files_verified':len(immutable),
            'all_settings_and_track_metadata_preserved':True,'settings_bytes_preserved':files['settings.json']==original['files']['settings.json'],
            'all_audio_artwork_fonts_and_appearance_preserved':True}
    if args.report:Path(args.report).write_text(json.dumps(report,ensure_ascii=False,indent=2),'utf-8')
    print(json.dumps(report,ensure_ascii=True))
