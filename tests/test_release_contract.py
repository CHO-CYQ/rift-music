import importlib.util
from pathlib import Path
import unittest
spec=importlib.util.spec_from_file_location('release_contract',Path(__file__).parents[1]/'scripts/release_contract.py')
contract=importlib.util.module_from_spec(spec);spec.loader.exec_module(contract)

class ReleaseContract(unittest.TestCase):
    def setUp(self):
        self.old=dict(Id='personal',File='personal.wav',Category='custom')
        self.new=dict(Id='bp-new',File='new.mp3',Category='draft')
        self.manifest=dict(tracks=[dict(Id='bp-new',File='new.mp3',Sha256='0'*64)])
    def test_no_additions_is_rejected(self):
        with self.assertRaisesRegex(AssertionError,'Missing or unexpected'):contract.check_additions([self.old],{'personal'},self.manifest)
    def test_unrelated_addition_cannot_fill_the_count(self):
        with self.assertRaisesRegex(AssertionError,'Missing or unexpected'):contract.check_additions([self.old,dict(self.new,Id='wrong')],{'personal'},self.manifest)
    def test_expected_track_and_filename_are_required(self):
        self.assertEqual(set(contract.check_additions([self.old,self.new],{'personal'},self.manifest)),{'bp-new'})
        with self.assertRaisesRegex(AssertionError,'release contract'):contract.check_additions([self.old,dict(self.new,File='wrong.mp3')],{'personal'},self.manifest)
