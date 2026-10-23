"""Make a portable, hash-checked BP catalog from a research snapshot.

Disabled/unqualified sources stay in the exclusion report, never in Music.
Run before assemble_library.py; neither script changes the old installation.
"""
import copy
import hashlib
import json
from pathlib import Path
import shutil
import sys

FIELDS='Id Name Season Kind File Source Artist CoverFile DraftUses Category Theme Year Quality SearchTags Favorite Duration Start End FadeIn FadeOut Enabled'.split()
COMPETITIONS={'Worlds','MSI','FirstStand','LCK','LPL'}

def prepare(source, destination):
    source=Path(source).resolve();destination=Path(destination).resolve()
    if destination.exists() or destination.is_relative_to(source):raise ValueError('Destination must be a new, separate stage')
    catalog=json.loads((source/'catalog.json').read_text(encoding='utf-8-sig'))
    paths=[];tracks=[];provenance=[];excluded=[];ids=set();names=set()
    for original in catalog['tracks']:
        if not original.get('Enabled',False):
            excluded.append({k:copy.deepcopy(original.get(k)) for k in ('Id','Name','Source','Quality','AudioReview')});continue
        audio=Path(original['File']).resolve()
        if not audio.is_relative_to(source/'playable') or audio.suffix.lower()!='.mp3':raise ValueError('Audio is outside playable source directory')
        actual=hashlib.sha256(audio.read_bytes()).hexdigest()
        if actual!=original.get('Sha256'):raise ValueError('Audio hash mismatch: '+original['Id'])
        review=original.get('AudioReview',{})
        if review.get('FullMp3DecodePassed') is not True or review.get('SungVocalReviewRequired') is True:raise ValueError('Unqualified audio review')
        uses=original.get('DraftUses',[])
        if not uses or any(u.get('Competition') not in COMPETITIONS or not 2009<=u.get('Year',0)<=2100 or not u.get('Evidence') or not u.get('Stage') for u in uses):raise ValueError('Invalid draft use')
        if original.get('Category')!='draft' or original['Id'] in ids or audio.name.casefold() in names:raise ValueError('Duplicate or wrong-purpose track')
        ids.add(original['Id']);names.add(audio.name.casefold())
        track={k:copy.deepcopy(original[k]) for k in FIELDS if k in original};track['File']=audio.name
        tracks.append(track);paths.append(audio)
        provenance.append(dict(track_id=track['Id'],sha256=actual,source=track.get('Source',''),artist=track.get('Artist',''),
            draft_uses=uses,quality=track.get('Quality',''),kind=track.get('Kind',''),audio_review=review))
    # Validate the whole snapshot before copying anything.
    (destination/'Music').mkdir(parents=True)
    for audio in paths:shutil.copy2(audio,destination/'Music'/audio.name)
    result=dict(tracks=tracks,provenance=provenance,excluded=excluded,complete=False)
    (destination/'catalog.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(packaged=len(tracks),excluded=len(excluded))))
    return result

if __name__=='__main__':prepare(sys.argv[1],sys.argv[2])
