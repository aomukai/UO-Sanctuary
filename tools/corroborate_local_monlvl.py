#!/usr/bin/env python3
"""Corroborate retained monster-level data with the pinned local installation.

--extract repeats a strict read-only CASC read while build metadata matches.
This compares text data, not a modern executable or monster-stat initializer.
"""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
EXPECTED = 'bda243efe1d1e9591f75b8cf97ac73f78ff691362381f35cc000233eb46c092a'


def build(check=False, extract=False):
    installation = json.loads((ROOT / 'tools/reference/d2r-local-installation-evidence.json').read_text())
    mode = json.loads((ROOT / 'tools/reference/d2r-local-monmode-evidence.json').read_text())
    reader = mode['reader']
    assert hashlib.sha256((ROOT / reader['sourcePath']).read_bytes()).hexdigest() == reader['sourceSha256']
    assert installation['referenceVersion'] == installation['buildInfo']['version'] == '3.3.93847'
    path = ROOT / '.local/research/d2r-3.3.93847/monlvl-corpse-verified.txt'
    archive = 'data:data/global/excel/monlvl.txt'
    if extract:
        info = Path(installation['installationPath']) / '.build.info'
        assert hashlib.sha256(info.read_bytes()).hexdigest() == installation['buildInfo']['sha256']
        subprocess.run([str(STAGE / 'casc-reference-reader'), installation['installationPath'], archive, str(path)], check=True)
        assert hashlib.sha256(info.read_bytes()).hexdigest() == installation['buildInfo']['sha256']
    raw = path.read_bytes()
    assert hashlib.sha256(raw).hexdigest() == EXPECTED
    rows = list(csv.DictReader(raw.decode('utf-8-sig').splitlines(), delimiter='\t'))
    local = {row['Level']: row for row in rows}
    assert len(local) == len(rows) == 111
    pinned = ROOT / 'tools/reference/d2r-named-equipment/json/monlvl.json'
    pin_raw = pinned.read_bytes()
    manifest = json.loads((ROOT / 'tools/reference/d2r-named-equipment/sources.json').read_text())
    digest = hashlib.sha256(pin_raw).hexdigest()
    assert digest == manifest['files']['json/monlvl.json']['sha256']
    reference = json.loads(pin_raw)
    assert set(local) == set(reference)
    fields = 0
    for key, row in reference.items():
        for field, value in row.items():
            if field in local[key] and not field.startswith('*') and field != 'lineNumber':
                assert local[key][field] == str(value), (key, field, value, local[key][field])
                fields += 1
        extra = {k: v for k, v in local[key].items() if k not in row and not k.startswith('*') and v not in ('', '0')}
        assert not extra, (key, extra)
    assert fields == 3441
    hp = ['HP', 'HP(N)', 'HP(H)', 'L-HP', 'L-HP(N)', 'L-HP(H)']
    evidence = dict(
        referenceVersion='3.3.93847', installationEvidence='tools/reference/d2r-local-installation-evidence.json',
        buildInfoSha256=installation['buildInfo']['sha256'], reader=reader,
        file=dict(path=str(path.relative_to(ROOT)), archivePath=archive, size=len(raw), sha256=EXPECTED, rows=111),
        pinned=dict(path=str(pinned.relative_to(ROOT)), sha256=digest), matchingFields=fields,
        samples={key: {field: local[key][field] for field in hp} for key in ['0', '1', '50', '99', '110']},
        established=[
            'Strict local read-only CASC text extraction while build metadata matched3.3.93847 before/after yielded14707bytes and111level rows.',
            'All3441available nonmetadata pinned fields match exactly. No extra nonempty/nonzero local field is missing from a pinned row, including all six classic/expansion HP columns.'
        ],
        limits=[
            'Text-data agreement does not certify binary table compilation/ABI, current executable behavior, source ratio arithmetic, monster initialization or corpse explosion.',
            'No runtime catalogue or learned skill is changed. Current monster-specific min/max HP, noRatio, game/difficulty/level selection and corpse snapshot/persistence must be connected separately.'
        ])
    target = ROOT / 'tools/reference/d2r-local-monlvl-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Current monlvl:111rows/3441fields match strict pinned-build local text; HP columns corroborated; no runtime certificate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--extract', action='store_true')
    args = parser.parse_args()
    build(args.check, args.extract)
