"""Append a hashed music catalog without changing existing preferences or tracks.

Usage: python scripts/expand_library.py CATALOG_DIR APP_DIR
Close the target application first. This tool is idempotent and preserves a backup.
"""
import hashlib,json,math,os,shutil,sys
from pathlib import Path

catalog_dir,app_dir=(Path(arg).resolve() for arg in sys.argv[1:3])
catalog=json.loads((catalog_dir/'catalog.json').read_text(encoding='utf-8-sig'))
settings_file=app_dir/'Data'/'settings.json';original=settings_file.read_bytes()
settings=json.loads(original.decode('utf-8-sig'));evidence={r['track_id']:r for r in catalog['provenance']}
existing={t['Id'] for t in settings['Tracks']};additions=[]
for track in sorted(catalog['tracks'],key=lambda t:(t['Kind']!='官方主题曲补充',-int(t['Season'].split(' · ')[-1]) if t['Kind']=='官方主题曲补充' else 0)):
    if track['Id'] in existing:continue
    name=track['File']
    if Path(name).name!=name or '/' in name or '\\' in name or ':' in name:raise ValueError('Invalid music filename')
    if not all(math.isfinite(track[k]) for k in ['Duration','Start','End','FadeIn','FadeOut']):raise ValueError('Invalid time')
    if not 0<=track['Start']<track['End']<=track['Duration']:raise ValueError('Invalid clip')
    src=catalog_dir/'Music'/name
    with src.open('rb') as f:digest=hashlib.file_digest(f,'sha256').hexdigest()
    if digest!=evidence[track['Id']]['sha256']:raise ValueError(f'Audio hash mismatch: {name}')
    target=app_dir/'Data'/'Music'/name
    if target.exists() and hashlib.sha256(target.read_bytes()).hexdigest()!=digest:raise ValueError(f'Existing file conflict: {name}')
    additions.append((track,src,target))
for track,src,target in additions:
    target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,target);settings['Tracks'].append(track)
if settings_file.read_bytes()!=original:raise RuntimeError('Settings changed while preparing update; retry after closing the app.')
if additions:
    backup=settings_file.with_name('settings.before-music-expansion.json')
    if not backup.exists():backup.write_bytes(original)
    temporary=settings_file.with_suffix('.expansion-tmp')
    temporary.write_text(json.dumps(settings,ensure_ascii=False,indent=2),encoding='utf-8')
    os.replace(temporary,settings_file)
provenance_file=app_dir/'music-provenance.json'
old=json.loads(provenance_file.read_text(encoding='utf-8-sig')) if provenance_file.exists() else {'tracks':[]}
old_ids={r.get('track_id') for r in old['tracks']}
for record in catalog['provenance']:
    if record['track_id'] not in old_ids:
        item=record.copy();item['file']='Data/'+record['file'];old['tracks'].append(item)
provenance_file.write_text(json.dumps(old,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Added {len(additions)} tracks; total {len(settings["Tracks"])}. Existing settings preserved.')
