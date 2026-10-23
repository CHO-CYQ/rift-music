import hashlib
import importlib.util
import json
from pathlib import Path
from temp_fixture import TemporaryDirectory
import unittest

spec=importlib.util.spec_from_file_location('bp_packaging',Path(__file__).parents[1]/'scripts/prepare_bp_catalog.py')
packaging=importlib.util.module_from_spec(spec)
if spec.loader and Path(spec.origin).exists(): spec.loader.exec_module(packaging)

class BpPackaging(unittest.TestCase):
    def fixture(self,root):
        source=root/'research';(source/'playable').mkdir(parents=True)
        audio=source/'playable/clean.mp3';audio.write_bytes(b'fixture audio')
        track=dict(Id='bp-clean',Name='Clean',File=str(audio),Enabled=True,Category='draft',Duration=60,Start=0,End=60,FadeIn=0,FadeOut=0,Artist='Author',Source='https://example.com/clean',
            Sha256=hashlib.sha256(audio.read_bytes()).hexdigest(),DraftUses=[dict(Year=2021,Competition='LCK',Stage='BP',Evidence='https://example.com/use')],AudioReview=dict(FullMp3DecodePassed=True))
        (source/'catalog.json').write_text(json.dumps(dict(tracks=[track])),encoding='utf-8')
        return source,track
    def test_compatible_basename_and_review_provenance(self):
        with TemporaryDirectory() as d:
            root=Path(d);source,track=self.fixture(root);stage=root/'ready'
            packaging.prepare(source,stage)
            result=json.loads((stage/'catalog.json').read_text(encoding='utf-8'))
            self.assertEqual(result['tracks'][0]['File'],'clean.mp3')
            self.assertEqual(result['tracks'][0]['DraftUses'],track['DraftUses'])
            self.assertEqual(result['tracks'][0]['Artist'],'Author')
            self.assertTrue(result['provenance'][0]['audio_review']['FullMp3DecodePassed'])
            self.assertEqual((stage/'Music/clean.mp3').read_bytes(),b'fixture audio')
    def test_vocal_or_unqualified_track_is_not_packaged(self):
        with TemporaryDirectory() as d:
            root=Path(d);source,track=self.fixture(root);track['Enabled']=False
            (source/'catalog.json').write_text(json.dumps(dict(tracks=[track])),encoding='utf-8')
            packaging.prepare(source,root/'ready')
            self.assertFalse((root/'ready/Music/clean.mp3').exists())
            self.assertEqual(len(json.loads((root/'ready/catalog.json').read_text(encoding='utf-8'))['excluded']),1)
    def test_tamper_rejected_before_stage_creation(self):
        with TemporaryDirectory() as d:
            root=Path(d);source,track=self.fixture(root);Path(track['File']).write_bytes(b'tampered')
            with self.assertRaisesRegex(ValueError,'hash'):packaging.prepare(source,root/'ready')
            self.assertFalse((root/'ready').exists())
    def test_source_path_cannot_escape_playable_directory(self):
        with TemporaryDirectory() as d:
            root=Path(d);source,track=self.fixture(root);outside=root/'outside.mp3';outside.write_bytes(b'fixture audio');track['File']=str(outside)
            (source/'catalog.json').write_text(json.dumps(dict(tracks=[track])),encoding='utf-8')
            with self.assertRaisesRegex(ValueError,'outside'):packaging.prepare(source,root/'ready')
            self.assertFalse((root/'ready').exists())

if __name__=='__main__':unittest.main()
