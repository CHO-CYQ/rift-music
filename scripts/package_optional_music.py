"""Build independently verified music packs; do not edit the installed library."""
import argparse,copy,hashlib,json,zipfile
from collections import defaultdict
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('installed',type=Path);p.add_argument('output',type=Path);args=p.parse_args()
root=args.installed.resolve();out=args.output.resolve();out.mkdir(parents=True,exist_ok=True)
original=(root/'Data/settings.json').read_bytes();settings=json.loads(original.decode('utf-8-sig'))
groups=defaultdict(list)
names={'opening-worlds':'开场 · 全球总决赛','opening-skins':'开场 · 英雄与皮肤','opening-events':'开场 · 活动与宇宙','opening-seasons':'开场 · 赛季与地区','bp-worlds':'BP · 全球总决赛','bp-msi':'BP · MSI 季中赛','bp-firststand':'BP · 全球先锋赛','bp-lck':'BP · LCK','bp-lpl':'BP · LPL'}
for track in settings['Tracks']:
    t=copy.deepcopy(track);t['Favorite']=False;t['Enabled']=True;t.pop('PlaybackError',None)
    key='bp-'+t['DraftUses'][0]['Competition'].lower() if t['Category']=='draft' else 'opening-'+t['Category']
    assert key in names;groups[key].append(t)
catalog=[]
for key in names:
    tracks=groups[key];asset=f'RiftMusic-Music-{key}-v0.1.zip';archive=out/asset
    sources={'Music/'+t['File'] for t in tracks}|{'Artwork/'+t['CoverFile'] for t in tracks if t.get('CoverFile')}
    files=[]
    for relative in sorted(sources):
        source=root/'Data'/relative
        files.append({'Path':relative,'Bytes':source.stat().st_size,'Sha256':hashlib.sha256(source.read_bytes()).hexdigest()})
    if not archive.exists():
        with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
            for f in files:z.write(root/'Data'/f['Path'],f['Path'])
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None and set(z.namelist())==sources
        for f in files:assert hashlib.sha256(z.read(f['Path'])).hexdigest()==f['Sha256']
    catalog.append({'Id':key,'Name':names[key],'Description':'保留各年份与已核实赛事用途；仅下载所选分类。','Asset':asset,'Bytes':archive.stat().st_size,'Sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'Tracks':tracks,'Files':files})
assert sum(len(x['Tracks']) for x in catalog)==148
assert len({t['Id'] for x in catalog for t in x['Tracks']})==148
assert original==(root/'Data/settings.json').read_bytes()
manifest=Path(__file__).resolve().parents[1]/'docs/music-packs-v01.json'
manifest.write_text(json.dumps(catalog,ensure_ascii=False,indent=2),'utf-8')
(out/'music-packs-v01.json').write_bytes(manifest.read_bytes())
(out/'SHA256.txt').write_text(''.join(f"{x['Sha256']}  {x['Asset']}\n" for x in catalog),'utf-8')
print(json.dumps({'packs':len(catalog),'tracks':148,'bytes':sum(x['Bytes'] for x in catalog)},ensure_ascii=True))
