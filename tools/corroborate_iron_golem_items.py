#!/usr/bin/env python3
"""Corroborate Iron Golem's material/slot metadata against owned pinned tables."""
import argparse
import csv
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/iron-golem-items-source/owned'


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(parents=True, exist_ok=True)
    installationpath = ROOT / 'tools/reference/d2r-local-installation-evidence.json'
    installation = json.loads(installationpath.read_text())
    animationpath = ROOT / 'tools/reference/d2r-local-animation-evidence.json'; animation = json.loads(animationpath.read_text())
    assert installation['referenceVersion'] == animation['referenceVersion'] == '3.3.93847'
    reader = ROOT / '.local/class-skills/warlock-foundation/casc-reference-reader'
    assert sha(reader.with_suffix('.cpp')) == animation['reader']['sourceSha256']
    game = Path(installation['installationPath']); exe = game / installation['executable']['filename']; info = game / '.build.info'
    tables = {}; reads = []
    for name in ['weapons', 'armor', 'misc', 'itemtypes', 'bodylocs']:
        p = STAGE / (name + '.txt'); archive = 'data:data/global/excel/' + name + '.txt'
        if not p.exists():
            assert not check, 'Missing preserved owned table: ' + name
            assert sha(exe) == installation['executable']['sha256'] and sha(info) == installation['buildInfo']['sha256']
            subprocess.run([str(reader), str(game), archive, str(p)], check=True, capture_output=True)
            assert sha(exe) == installation['executable']['sha256'] and sha(info) == installation['buildInfo']['sha256']
        tables[name] = list(csv.DictReader(p.read_text(encoding='utf-8-sig').splitlines(), delimiter='\t'))
        reads.append(dict(archivePath=archive, path=str(p.relative_to(ROOT)), sha256=sha(p)))
    directory = ROOT / 'tools/reference/d2r-named-equipment'; manifest = json.loads((directory / 'sources.json').read_text()); inputs = {}
    compared = 0; items = 0; types = 0
    def number(value):
        return int(value) if str(value).strip() else 0
    for name in ['weapons', 'armor', 'misc', 'itemtypes']:
        p = directory / 'json' / (name + '.json'); assert sha(p) == manifest['files']['json/' + name + '.json']['sha256']; inputs[str(p.relative_to(ROOT))] = sha(p)
        cached = json.loads(p.read_text()); key = 'Code' if name == 'itemtypes' else 'code'
        owned = {r[key]: r for r in tables[name] if r.get(key)}
        for r in cached.values():
            local = owned[r[key]]
            if name == 'itemtypes':
                for field in ['BodyLoc1', 'BodyLoc2']:
                    assert local.get(field, '') == r.get(field, ''), (name, r[key], field)
                    compared += 1
                types += 1
            else:
                assert number(local.get('bitfield1', '')) == r.get('bitfield1', 0), (name, r[key], 'bitfield1')
                assert local['type'] == r['type'], (name, r[key], 'type')
                compared += 2; items += 1
    bodylocs = {r['Code']: i for i, r in enumerate(tables['bodylocs']) if r.get('Code')}
    assert tables['bodylocs'][0]['Code'] == ''
    assert [bodylocs[k] for k in ['head', 'neck', 'tors', 'rarm', 'larm', 'rrin', 'lrin', 'belt', 'feet', 'glov']] == list(range(1, 11))
    assert len(tables['bodylocs']) == 11 and items == 692 and types == 105 and compared == 1594
    inputs.update({str(p.relative_to(ROOT)): sha(p) for p in [installationpath, animationpath, reader, reader.with_suffix('.cpp'), Path(__file__).resolve()]})
    result = dict(referenceVersion='3.3.93847', component='Owned Iron Golem item metal and body-slot metadata', localReadOnly=True, processMemoryRead=False, processInput=False, networkDownloads=False,
        executableSha256=installation['executable']['sha256'], buildInfoSha256=installation['buildInfo']['sha256'], readerSha256=sha(reader), inputs=inputs, tables=reads,
        verified=dict(itemBases=items, itemTypes=types, comparedFields=compared, bodyLocationCodes=11, unexplainedDifferences=0),
        limits=['Corroborates cached base metal/type and type body-location fields against owned3.3.93847tables. Item-class numeric IDs remain pinned catalogue metadata;this does not inspect live item IDs or execute the current inventory/stat/proc engine. Raw licensed tables stay local and ignored.'])
    p = ROOT / 'tools/reference/iron-golem-local-items-evidence.json'; encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert p.read_text() == encoded
    else:
        p.write_text(encoded)
    print('Owned Iron Golem metadata:692bases/105types/1594fields and11body locations match;read-only,no game process or input.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
