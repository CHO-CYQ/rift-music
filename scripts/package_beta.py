"""Package an existing local library using clean public defaults, never personal sessions."""
import argparse
import copy
import hashlib
import json
import shutil
import zipfile
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('installed')
parser.add_argument('stage')
parser.add_argument('archive')
args=parser.parse_args()
installed,stage,archive=map(lambda p:Path(p).resolve(),(args.installed,args.stage,args.archive))
assert installed!=stage and (stage/'RiftMusic.exe').is_file()
original=(installed/'Data/settings.json').read_bytes()
private=json.loads(original.decode('utf-8-sig'))
tracks=copy.deepcopy(private['Tracks'])
for track in tracks:
    track['Favorite']=False;track['Enabled']=True
    track.pop('PlaybackError',None)
assert len({t['Id'] for t in tracks})==len(tracks)
settings={'Version':1,'AutoPlay':False,'Mode':'Random','FixedTrackId':None,'LastTrackId':None,'Volume':.35,
          'Tracks':tracks,'Sessions':[],'TrayHintShown':False,'RandomScope':'worlds','AutoDraft':False,
          'DraftMode':'Random','FixedDraftTrackId':None,'LastDraftTrackId':None,'DraftYear':0,
          'DraftCompetition':'all','LeagueClientDirectory':''}
data=stage/'Data';data.mkdir(exist_ok=True)
for folder in ('Music','Artwork'):
    shutil.copytree(installed/'Data'/folder,data/folder,dirs_exist_ok=True)
(data/'settings.json').write_text(json.dumps(settings,ensure_ascii=False,indent=2),'utf-8')
(data/'appearance.json').write_text('{"MotionEnabled": true}','utf-8')
proof=installed/'说明/音乐来源.json'
if proof.is_file():shutil.copy2(proof,stage/'说明/音乐来源.json')
for name in ('BP 年份覆盖.md','BP 用途与来源.json','BP 排除音源.json'):
    source=installed/'说明'/name
    if source.is_file():shutil.copy2(source,stage/'说明'/name)
assert original==(installed/'Data/settings.json').read_bytes(),'Personal settings modified'
archive.parent.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in sorted(stage.rglob('*')):
        if p.is_file():z.write(p,'RiftMusic/'+p.relative_to(stage).as_posix())
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    packaged=json.loads(z.read('RiftMusic/Data/settings.json'))
    assert not packaged['AutoPlay'] and not packaged['AutoDraft'] and not packaged['Sessions']
    assert packaged['LeagueClientDirectory']=='' and not any(t['Favorite'] for t in packaged['Tracks'])
    assert sum('/Data/Music/' in p for p in z.namelist())==len(tracks)
    for t in tracks:
        assert hashlib.sha256(z.read('RiftMusic/Data/Music/'+t['File'])).digest()==hashlib.sha256((installed/'Data/Music'/t['File']).read_bytes()).digest()
sha=hashlib.sha256(archive.read_bytes()).hexdigest()
archive.with_name('SHA256.txt').write_text(sha+'  '+archive.name+'\n','utf-8')
print(json.dumps({'version':'v0.1','tracks':len(tracks),'private_sessions_included':False,'default_autoplay':False,'sha256':sha,'bytes':archive.stat().st_size},ensure_ascii=True))
