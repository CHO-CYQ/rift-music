import importlib.util
import json
from pathlib import Path
from temp_fixture import TemporaryDirectory
import unittest

spec=importlib.util.spec_from_file_location('assembler',Path(__file__).parents[1]/'scripts/assemble_library.py')
assembler=importlib.util.module_from_spec(spec);spec.loader.exec_module(assembler)

class LibraryMigration(unittest.TestCase):
    def test_v3_keeps_already_clean_worlds_replacement_and_metadata(self):
        with TemporaryDirectory() as d:
            root=Path(d);old,settings=self.fixture(root)
            track=settings['Tracks'][0];track.update(Id='worlds-2017-legends_never_die',Name='Clean original cue',Category='worlds',Kind='游戏开场音源',Quality='社区转载游戏原曲',Artist='Riot Music Team',CoverFile='cover.jpg',DraftUses=[])
            (old/'Data/settings.json').write_text(json.dumps(settings),encoding='utf-8')
            result=assembler.assemble(old,root/'new',[],preserve_existing=True)
            self.assertEqual(result['Tracks'],settings['Tracks'])

    def fixture(self,root):
        old=root/'old';(old/'Data/Music').mkdir(parents=True)
        t=dict(Id='personal',Name='用户自己的音乐',File='personal.wav',Duration=80,Start=12,End=60,FadeIn=1,FadeOut=2,Enabled=False,Favorite=True)
        (old/'Data/Music/personal.wav').write_bytes(b'fixture media')
        settings=dict(Version=1,Tracks=[t],AutoPlay=True,Mode='Random',Volume=.42,RandomScope='favorites',FixedTrackId='personal',Sessions=[{'test':'preserve'}])
        (old/'Data/settings.json').write_text(json.dumps(settings),encoding='utf-8')
        return old,settings

    def test_preserves_user_settings_and_custom_music(self):
        with TemporaryDirectory() as d:
            root=Path(d);old,original=self.fixture(root);before=(old/'Data/settings.json').read_bytes()
            result=assembler.assemble(old,root/'new',[])
            for key in ('AutoPlay','Mode','Volume','RandomScope','FixedTrackId','Sessions'):self.assertEqual(original[key],result[key])
            self.assertEqual(result['Tracks'][0]['Start'],12);self.assertFalse(result['Tracks'][0]['Enabled']);self.assertTrue(result['Tracks'][0]['Favorite'])
            self.assertEqual((old/'Data/settings.json').read_bytes(),before)
            self.assertEqual((root/'new/Data/Music/personal.wav').read_bytes(),b'fixture media')

    def test_same_audio_update_keeps_user_clip_and_enable_state(self):
        with TemporaryDirectory() as d:
            root=Path(d);old,settings=self.fixture(root);catalog=root/'catalog';(catalog/'Music').mkdir(parents=True)
            (catalog/'Music/personal.wav').write_bytes(b'fixture media')
            t=dict(settings['Tracks'][0],Start=0,End=45,Enabled=True,Favorite=False,Category='skins',Theme='英雄主题')
            (catalog/'catalog.json').write_text(json.dumps({'tracks':[t],'provenance':[{'track_id':'personal','sha256':assembler.digest(catalog/'Music/personal.wav')}]}),encoding='utf-8')
            result=assembler.assemble(old,root/'new',[catalog]);track=result['Tracks'][0]
            self.assertEqual(track['Start'],12);self.assertEqual(track['End'],60);self.assertFalse(track['Enabled']);self.assertTrue(track['Favorite']);self.assertEqual(track['Category'],'skins')

    def test_rejects_tampered_catalog_before_writing_stage(self):
        with TemporaryDirectory() as d:
            root=Path(d);old,settings=self.fixture(root);catalog=root/'catalog';(catalog/'Music').mkdir(parents=True)
            (catalog/'Music/new.wav').write_bytes(b'bad hash')
            t=dict(settings['Tracks'][0],Id='new',File='new.wav')
            (catalog/'catalog.json').write_text(json.dumps({'tracks':[t],'provenance':[{'track_id':'new','sha256':'0'*64}]}),encoding='utf-8')
            with self.assertRaisesRegex(ValueError,'hash mismatch'):assembler.assemble(old,root/'new',[catalog])
            self.assertFalse((root/'new').exists())

    def test_unsafe_file_is_rejected(self):
        with TemporaryDirectory() as d:
            root=Path(d);old,settings=self.fixture(root);t=settings['Tracks'][0];t['File']='../outside.wav'
            with self.assertRaisesRegex(ValueError,'Unsafe'):assembler.validate(t,old/'Data/Music'/t['File'])

    def test_legacy_file_key_and_current_provenance_are_preserved(self):
        with TemporaryDirectory() as d:
            root=Path(d);old,_=self.fixture(root)
            proof={'tracks':[{'file':'Data/Music/personal.wav','sha256':assembler.digest(old/'Data/Music/personal.wav'),'source_archive':'original.wad'}]}
            (old/'music-provenance.json').write_text(json.dumps(proof),encoding='utf-8')
            assembler.assemble(old,root/'new',[])
            assembler.assemble(root/'new',root/'next',[])
            final=json.loads((root/'next/说明/音乐来源.json').read_text(encoding='utf-8'))
            self.assertEqual(final['tracks'][0]['source_archive'],'original.wad')
            (old/'Data/Music/personal.wav').write_bytes(b'tampered')
            with self.assertRaisesRegex(ValueError,'hash mismatch'):assembler.assemble(old,root/'rejected',[])

    def test_packaged_sha256_contract_is_verified(self):
        with TemporaryDirectory() as d:
            root=Path(d);old,settings=self.fixture(root);catalog=root/'catalog';(catalog/'Music').mkdir(parents=True)
            (catalog/'Music/new.wav').write_bytes(b'changed')
            t=dict(settings['Tracks'][0],Id='new',File='new.wav')
            (catalog/'catalog.json').write_text(json.dumps({'tracks':[t],'provenance':[{'track_id':'new','packaged_sha256':'a'*64}]}),encoding='utf-8')
            with self.assertRaisesRegex(ValueError,'hash mismatch'):assembler.assemble(old,root/'new',[catalog])

    def test_catalog_without_expected_hash_is_rejected(self):
        with TemporaryDirectory() as d:
            root=Path(d);old,settings=self.fixture(root);catalog=root/'catalog';catalog.mkdir()
            (catalog/'catalog.json').write_text(json.dumps({'tracks':settings['Tracks']}),encoding='utf-8')
            with self.assertRaisesRegex(ValueError,'requires a source hash'):assembler.assemble(old,root/'new',[catalog])

if __name__=='__main__':unittest.main()
