#!/usr/bin/env python3
"""Extract the reachable Normal players-1 gold projection from pinned LoD tables."""
import argparse
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BALANCE = ROOT / 'data/Sanctuary/Balance'
COMMIT = '45112569deb9384738ccafe5c24ebbb71f41c7c9'


def build(source):
    sources = json.loads((BALANCE / 'sources.json').read_text())
    tables = {}
    for name in ['MonStats', 'TreasureClassEx']:
        raw = (source / (name + '.txt')).read_bytes()
        assert hashlib.sha256(raw).hexdigest() == sources[name]['sha256'], name
        tables[name] = list(csv.DictReader(raw.decode('utf-8-sig').splitlines(), delimiter='\t'))
    monsters = {r['Id']: r for r in tables['MonStats'] if r['Id']}
    classes = {r['Treasure Class']: r for r in tables['TreasureClassEx'] if r['Treasure Class']}
    policies, extracted = [], {}

    def visit(name, path=()):
        assert name not in path, f'Treasure class cycle: {path} -> {name}'
        if name in extracted:
            return
        row = classes[name]
        # The entire approved Normal roster uses one positive pick. Fail closed
        # if an input change needs additional source semantics.
        assert int(row['Picks']) == 1, name
        entries = []
        for i in range(1, 11):
            item, weight = row[f'Item{i}'], int(row[f'Prob{i}'] or 0)
            if not item or not weight:
                continue
            assert weight > 0 and ',' not in item, (name, item)
            nested = item if item in classes else None
            entries.append(dict(item=item, weight=weight, nested=nested, gold=item == 'gld'))
            if nested:
                visit(nested, (*path, name))
        assert entries, name
        extracted[name] = dict(name=name, picks=1, noDrop=int(row['NoDrop'] or 0), entries=entries)

    rewards = json.loads((BALANCE / 'creature-rewards.json').read_text())
    for group in rewards['groups']:
        row = group['variants']['Normal']
        reference = row['reference'] if row['enabled'] else None
        tc = monsters[reference]['TreasureClass1'] if reference else None
        if tc:
            visit(tc)
        policies.append(dict(id=group['id'], types=group['types'], eligible=row['enabled'],
                             reference=reference, treasureClass=tc))
    return dict(sourceCommit=COMMIT, difficulty='Normal', players=1, maxDrops=6,
                policies=policies, classes=[extracted[k] for k in sorted(extracted)])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-dir', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    text = json.dumps(build(args.source_dir), indent=2) + '\n'
    target = BALANCE / 'creature-gold.json'
    if args.check:
        assert target.read_text() == text, 'Stale creature gold extraction'
    else:
        target.write_text(text)


if __name__ == '__main__':
    main()
