"""Build a classified personal-library stage, without modifying the source install.

python scripts/assemble_library.py OLD_APP NEW_STAGE [CATALOG_DIR ...]
The caller copies the tested single-file application to NEW_STAGE separately.
"""
import argparse
import copy
import hashlib
import json
import math
import shutil
import sys
from pathlib import Path

VOCAL_IDS = {'worlds-2019-phoenix', 'worlds-2018-rise_remix', 'worlds-2017-legends_never_die', 'worlds-2015-worlds_collide'}
BASE = {
    'client-wmc2022-spawn': ('worlds','全球总决赛',2022,'S12 · 2022','2022 全球总决赛 · 原版开场','STAR WALKIN 逐星 WMC2022'),
    'worlds2020-spawn': ('worlds','全球总决赛',2020,'S10 · 2020','2020 全球总决赛 · 原版开场','Take Over 所向无前'),
    'worlds-2013-hybrid_worlds': ('worlds','全球总决赛',2013,'S3 · 2013','Hybrid Worlds · 开场剪辑','混合世界'),
    'worlds-2012-silver_scrapes': ('worlds','全球总决赛',2012,'S2 · 2012','Silver Scrapes · 赛事经典','银痕 赛事经典'),
    'local-play_mus_map11_starguardian_spawn': ('skins','星之守护者',0,'初代','星之守护者 · 初代开场','Star Guardian 星之守卫者'),
    'local-play_mus_map11_starguardian2017_spawn': ('skins','星之守护者',2017,'2017','星之守护者 2017','Star Guardian 星之守卫者'),
    'local-play_mus_map11_starguardian2019_spawn': ('skins','星之守护者',2019,'2019','星之守护者 2019','Star Guardian 星之守卫者'),
    'local-play_mus_map11_starguardian2022_spawn': ('skins','星之守护者',2022,'2022','星之守护者 2022','Star Guardian 星之守卫者'),
    'local-play_mus_map11_kda2020_spawn': ('skins','K/DA',2020,'2020','K/DA 2020 · 原版开场','KDA ALL OUT'),
    'local-play_mus_map11_demaciavice2019_spawn': ('skins','德玛西亚警官',2019,'2019','德玛西亚警官','Demacia Vice 卢锡安 盖伦'),
    'local-play_mus_map11_sn2022_spawn': ('seasons','赛季启幕',2022,'2022','2022 赛季开场（SN2022）','Season 2022'),
    'local-play_mus_map11_xp12021_spawn': ('events','2021 活动',2021,'2021','2021 活动开场（XP1）','XP12021'),
    'local-play_mus_map11_hope24_spawn': ('seasons','赛季启幕',2024,'2024','2024 赛季开场（Hope24）','Season 2024'),
    'local-play_mus_map11_tournaments_spawn': ('events','峡谷赛事',0,'','冠军杯赛 · 原版开场','Clash Tournaments'),
    'local-play_mus_map11_teemo_spawn': ('skins','英雄主题',0,'','迅捷斥候 · 提莫','Teemo'),
    'local-play_sfx_swain_music_firstmove': ('skins','英雄主题',2018,'','诺克萨斯统领 · 斯维因','Swain 乌鸦'),
    'local-play_mus_map11_seasonal_phase_select_base': ('seasons','召唤师峡谷',0,'','召唤师峡谷 · 地图序曲','Seasonal map11'),
}

def read(path): return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def digest(path):
    with Path(path).open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest()
def validate(track, path):
    name = track['File']
    if not name or Path(name).name != name or any(c in name for c in '/\\:'): raise ValueError('Unsafe music filename: '+name)
    if not all(isinstance(track[k], (float,int)) and math.isfinite(track[k]) for k in ('Duration','Start','End','FadeIn','FadeOut')): raise ValueError('Invalid timing')
    if not 0 <= track['Start'] < track['End'] <= track['Duration'] or min(track['FadeIn'],track['FadeOut'])<0: raise ValueError('Invalid clip: '+track['Name'])
    if not path.is_file(): raise ValueError('Missing audio: '+str(path))

def assemble(old_app, stage, catalogs, preserve_existing=False):
    old_app, stage = Path(old_app).resolve(), Path(stage).resolve()
    if old_app == stage or old_app in stage.parents: raise ValueError('Stage must be separate from original installation')
    original_settings = (old_app/'Data/settings.json').read_bytes()
    old = json.loads(original_settings.decode('utf-8-sig')); settings=copy.deepcopy(old)
    old_tracks = {t['Id']:t for t in old['Tracks']}
    tracks = {}; paths = {}; evidence = {}; removed = []
    ids_by_file={t['File'].casefold():t['Id'] for t in old['Tracks']}
    for old_proof in (old_app/'music-provenance.json',old_app/'说明/音乐来源.json'):
        if old_proof.exists():
            for p in read(old_proof).get('tracks',[]):
                key=p.get('track_id') or ids_by_file.get(Path(str(p.get('file','')).replace('\\','/')).name.casefold())
                if key:evidence[key]=p
    for original in old['Tracks']:
        t=copy.deepcopy(original)
        if not preserve_existing and t['Id'] in VOCAL_IDS:
            removed.append({'id':t['Id'],'name':t['Name'],'reason':'旧版含人声整曲；保留在旧版本归档，不作为纯净开场补充。'}); continue
        if not preserve_existing and t['Id'] in BASE:
            category,theme,year,season,name,tags=BASE[t['Id']]
            t.update(Category=category,Theme=theme,Year=year,Season=season,Name=name,SearchTags=tags)
            if t['Id'].startswith('worlds-'):
                t.update(Quality='官方纯音乐',Kind='官方纯音乐剪辑')
            else:t.update(Quality='原始游戏音频')
        elif not preserve_existing:
            t.setdefault('Category','custom');t.setdefault('Theme','');t.setdefault('Year',0)
        tracks[t['Id']]=t; paths[t['Id']]=old_app/'Data/Music'/t['File']
    for folder in map(Path,catalogs):
        catalog=read(folder/'catalog.json')
        records=catalog.get('provenance',[])
        if isinstance(records,dict):records=list(records.values())
        incoming={p.get('track_id',p.get('Id',p.get('id'))):p for p in records}
        for original in catalog['tracks']:
            t=copy.deepcopy(original); previous=old_tracks.get(t['Id']); old_file=paths.get(t['Id'])
            record=incoming.get(t['Id'],{})
            expected=record.get('sha256') or record.get('packaged_sha256')
            if not isinstance(expected,str) or len(expected)!=64 or any(c not in '0123456789abcdefABCDEF' for c in expected):
                raise ValueError('Catalog requires a source hash: '+t['Name'])
            if previous:
                for key in ('Enabled','Favorite'): 
                    if key in previous:t[key]=previous[key]
                candidate=folder/'Music'/t['File']
                if old_file and old_file.is_file() and candidate.is_file() and digest(old_file)==digest(candidate):
                    for key in ('Start','End','FadeIn','FadeOut'):t[key]=previous[key]
            tracks[t['Id']]=t; paths[t['Id']]=folder/'Music'/t['File']
        for p in records:
            key=p.get('track_id',p.get('Id',p.get('id')))
            if key:evidence[key]=p
        for patch in catalog.get('updates',[]):
            if patch['Id'] in tracks: tracks[patch['Id']].update(patch)
    # Validate every input and detect collisions before changing the stage.
    file_hashes={}
    for key,t in tracks.items():
        validate(t,paths[key]); actual=digest(paths[key])
        expected=evidence.get(key,{}).get('sha256') or evidence.get(key,{}).get('packaged_sha256')
        if expected and expected.lower()!=actual:raise ValueError('Source hash mismatch: '+t['Name'])
        if t['File'] in file_hashes and file_hashes[t['File']]!=actual:raise ValueError('Conflicting audio filename')
        file_hashes[t['File']]=actual
    if (old_app/'Data/settings.json').read_bytes()!=original_settings:raise RuntimeError('Source settings changed during assembly')
    music=stage/'Data/Music';music.mkdir(parents=True,exist_ok=True)
    if (old_app/'Data/Artwork').is_dir():shutil.copytree(old_app/'Data/Artwork',stage/'Data/Artwork',dirs_exist_ok=True)
    if (old_app/'Data/Fonts').is_dir():shutil.copytree(old_app/'Data/Fonts',stage/'Data/Fonts',dirs_exist_ok=True)
    if (old_app/'Data/appearance.json').is_file():shutil.copy2(old_app/'Data/appearance.json',stage/'Data/appearance.json')
    for key,t in tracks.items():shutil.copy2(paths[key],music/t['File'])
    settings['Tracks']=list(tracks.values());settings.setdefault('RandomScope','all')
    if settings.get('FixedTrackId') not in tracks:
        settings['FixedTrackId']=None
        if settings.get('Mode') in ('Fixed',0):settings['AutoPlay']=False
    (stage/'Data/settings.json').write_text(json.dumps(settings,ensure_ascii=False,indent=2),encoding='utf-8')
    docs=stage/'说明';docs.mkdir(exist_ok=True)
    proof=[]
    for key,t in tracks.items():
        p=copy.deepcopy(evidence.get(key,{}));p.update(track_id=key,name=t['Name'],file='Data/Music/'+t['File'],sha256=file_hashes[t['File']],quality=t.get('Quality','用户导入'))
        proof.append(p)
    (docs/'音乐来源.json').write_text(json.dumps({'tracks':proof,'excluded_legacy':removed},ensure_ascii=False,indent=2),encoding='utf-8')
    report={'total':len(tracks),'source_settings_sha256':hashlib.sha256(original_settings).hexdigest(),'categories':{c:sum(t.get('Category')==c for t in tracks.values()) for c in ('worlds','skins','events','seasons','custom','draft')},'preserved_preferences':{k:settings.get(k) for k in ('AutoPlay','Mode','Volume','FixedTrackId','LastTrackId')},'excluded_legacy':removed,'preserve_existing':preserve_existing}
    (docs/'迁移记录.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(report,ensure_ascii=True))
    return settings

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('old_app');parser.add_argument('stage');parser.add_argument('catalogs',nargs='*');parser.add_argument('--preserve-existing',action='store_true')
    args=parser.parse_args();assemble(args.old_app,args.stage,args.catalogs,args.preserve_existing)
