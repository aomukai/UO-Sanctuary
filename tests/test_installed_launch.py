"""An installed launch must not replace the working assembly or its matching data."""
import importlib.machinery
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

ROOT=Path(__file__).resolve().parents[1]
loader=importlib.machinery.SourceFileLoader('installed_launch_dev',str(ROOT/'dev'))
spec=importlib.util.spec_from_loader(loader.name,loader);dev=importlib.util.module_from_spec(spec);loader.exec_module(dev)


class InstalledLaunchTests(unittest.TestCase):
    def exercise(self, installed):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);server=root/'.local/server';content=root/'.local/content'
            (server/'Configuration').mkdir(parents=True);(server/'Assemblies').mkdir();(server/'Data/Sanctuary').mkdir(parents=True)
            (server/'Configuration/modernuo.json').write_text(json.dumps({'dataDirectories':[str(root/'assets')]}))
            (server/'Data/assemblies.json').write_text(json.dumps(['Sanctuary.Content.dll']))
            (server/'ModernUO.dll').write_bytes(b'installed-server')
            assembly=server/'Assemblies/Sanctuary.Content.dll';assembly.write_bytes(b'working-content')
            data=server/'Data/Sanctuary/talents.json';data.write_text('["working-seven-class-catalogue"]')
            content.mkdir();(content/'Sanctuary.Content.dll').write_bytes(b'new-staging-content')
            source=root/'data/Sanctuary';source.mkdir(parents=True);(source/'talents.json').write_text('["new-eight-class-catalogue"]')
            with patch.multiple(dev, ROOT=root, LOCAL=root/'.local', SERVER=server), patch.object(dev,'require_sdk'), \
                patch.object(dev,'require_stopped'), patch.object(dev,'validate_data'), patch.object(dev,'run'):
                dev.launch('server',installed=installed)
            return assembly.read_bytes(),data.read_text()

    def test_installed_launch_preserves_actual_assembly_and_catalogue(self):
        self.assertEqual(self.exercise(True),(b'working-content','["working-seven-class-catalogue"]'))

    def test_ordinary_development_launch_still_stages_matching_source(self):
        self.assertEqual(self.exercise(False),(b'new-staging-content','["new-eight-class-catalogue"]'))


if __name__=='__main__':unittest.main()
