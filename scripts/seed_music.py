"""Prepare this user's verified local client cues for a fresh portable bundle.

Usage: python scripts/seed_music.py SOURCE_WITH_PROVENANCE TARGET_APP_DIRECTORY
Music is supplied separately and is intentionally absent from the source archive.
"""
import hashlib
import json
from pathlib import Path
import shutil
import sys
import wave

source, target = (Path(arg).resolve() for arg in sys.argv[1:3])
data = target / 'Data'
if (data / 'settings.json').exists():
    raise SystemExit('Refusing to overwrite an existing music library.')
provenance = json.loads((source / 'provenance.json').read_text(encoding='utf-8-sig'))
specs = [
    ('WMC2022_spawn.wav', 'client-wmc2022-spawn', '客户端 WMC2022 开场曲', '2022', '9d87eda2bc4808a033c3c9a68cc16ff042a0a6038ff39147878038c583327d9c'),
    ('Worlds2020_spawn.wav', 'worlds2020-spawn', '2020 世界赛开场', 'S10 · 2020', '342582ba2a0133d2f07b4bf46f06de15711e099cdee26a96cf162753e583a884'),
]
tracks, evidence = [], []
for filename, track_id, title, season, expected in specs:
    path = source / filename
    actual = hashlib.file_digest(path.open('rb'), 'sha256').hexdigest()
    if actual != expected:
        raise SystemExit(f'SHA256 mismatch: {filename}')
    with wave.open(str(path)) as audio:
        if (audio.getnchannels(), audio.getframerate(), audio.getsampwidth()) != (2, 44100, 2):
            raise SystemExit(f'Unexpected PCM format: {filename}')
        duration = audio.getnframes() / audio.getframerate()
    record = next(t.copy() for t in provenance['tracks'] if t['sha256'] == actual)
    record['file'] = f'Data/Music/{filename}'
    record.pop('package_file', None)
    evidence.append(record)
    tracks.append(dict(Id=track_id, Name=title, Season=season, Kind='游戏内开场原版', File=filename,
        Source=f"本机国服资源 Common.wad.client；事件 {record['event']}。" + ('2022 曲名尚未通过听辨核实。' if '2022' in filename else ''),
        Duration=duration, Start=0, End=duration, FadeIn=.8, FadeOut=3, Enabled=True))

(data / 'Music').mkdir(parents=True, exist_ok=True)
for filename, *_ in specs:
    shutil.copy2(source / filename, data / 'Music' / filename)
settings = dict(Version=1, AutoPlay=False, Mode='Fixed', FixedTrackId=tracks[0]['Id'], LastTrackId=None,
    Volume=.35, Tracks=tracks, Sessions=[], TrayHintShown=False)
(data / 'settings.json').write_text(json.dumps(settings, ensure_ascii=False, indent=2), encoding='utf-8')
provenance['tracks'] = evidence
provenance['event_mapping_tool'].pop('evidence_dir', None)
(target / 'music-provenance.json').write_text(json.dumps(provenance, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Prepared {len(tracks)} verified local cues in {target}')
