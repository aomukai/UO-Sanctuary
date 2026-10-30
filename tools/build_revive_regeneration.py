#!/usr/bin/env python3
"""Export pinned monster regeneration rates without guessing from native stats."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def build(check=False):
    directory = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((directory / 'sources.json').read_text())
    source = directory / 'json/monstats.json'
    assert manifest['mechanicsReferenceVersion'] == '3.3.93847'
    assert hashlib.sha256(source.read_bytes()).hexdigest() == manifest['files']['json/monstats.json']['sha256']
    rows = json.loads(source.read_text())
    assert len(rows) == 751
    result = []
    for name, row in rows.items():
        rate = row.get('DamageRegen', 0)
        assert isinstance(rate, int) and 0 <= rate <= 255, (name, rate)
        result.append(dict(source=name, damageRegen=rate))
    path = ROOT / 'data/Sanctuary/Balance/revive-regeneration.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Pinned regeneration rates:751source rows.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
