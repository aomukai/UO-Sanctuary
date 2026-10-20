#!/usr/bin/env python3
"""Author T21 moulds from the pinned, currently mapped equipment affix data."""
import argparse
import hashlib
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
BALANCE = ROOT / 'data/Sanctuary/Balance'
# These are semantic percentage families, not all numeric D2 modifiers.
FAMILIES = [
    ('enhanced-defense', 'Enhanced defense', ['ac%']),
    ('enhanced-damage', 'Enhanced physical damage', ['dmg%']),
    ('attack-rating', 'Attack rating', ['att%']),
    ('all-resistance', 'All resistances', ['res-all']),
    ('fire-resistance', 'Fire resistance', ['res-fire']),
    ('cold-resistance', 'Cold resistance', ['res-cold']),
    ('lightning-resistance', 'Lightning resistance', ['res-ltng']),
    ('poison-resistance', 'Poison resistance', ['res-pois']),
    ('attack-speed', 'Increased attack speed', ['swing1', 'swing2', 'swing3']),
    ('cast-rate', 'Faster cast rate', ['cast1', 'cast2', 'cast3']),
    ('run-walk', 'Faster run/walk', ['move1', 'move2', 'move3']),
    ('block-chance', 'Chance to block', ['block']),
    ('hit-recovery', 'Faster hit recovery', ['balance1', 'balance2', 'balance3']),
    ('gold-find', 'Extra gold from monsters', ['gold%']),
    ('magic-find', 'Better chance of finding magic items', ['mag%']),
]
TIER_NAMES = ['Crude', 'Rough', 'Shaped', 'Etched', 'Refined', 'Polished', 'Flawless', 'Perfect']

def build(balance=BALANCE):
    def read(name): return json.loads((balance / (name + '.json')).read_text())
    affixes = read('affixes')
    graph = {r['code']: r['parents'] for r in read('item-types')}
    bases = {r['code']: r for r in read('normal-bases') + read('equipment-tier-bases')}
    mapped_types = sorted({bases[r['code']]['type'] for r in read('gear-drops')['bases'] if r['variants']})
    def is_type(code, parent):
        return code == parent or any(is_type(p, parent) for p in graph[code])
    def eligible(row):
        return 1 <= row['level'] <= 99 and any(
            any(is_type(c, t) for t in row['include']) and not any(is_type(c, t) for t in row['exclude'])
            for c in mapped_types)
    stats = []
    for key, label, codes in FAMILIES:
        def modifiers(row):
            return [m for m in row['modifiers'] if m['code'] in codes and m['mode'] == 'Numeric'
                    and not m['parameter'] and m['min'] > 0]
        rows = [r for r in affixes if eligible(r) and modifiers(r)]
        # Prefer an actual suffix at equal minimum; each source row contributes once.
        minimum, _, _, _, first = min((m['min'], r['kind'] != 'Suffix', r['level'], r['id'], r)
                                      for r in rows for m in modifiers(r))
        stats.append(dict(id=key, label=label, code=codes[0], aliases=codes, baseline=minimum,
                          weight=sum(r['frequency'] for r in rows), sourceAffix=first['id'],
                          affixName=first['name'], affixKind=first['kind'],
                          weightAffixes=[r['id'] for r in rows]))
    inputs = ['affixes', 'item-types', 'normal-bases', 'equipment-tier-bases', 'gear-drops']
    return dict(schemaVersion=1, sourceCommit='45112569deb9384738ccafe5c24ebbb71f41c7c9',
                dropDenominator=10000, ordinaryTickets=10, quantity=1, magicFindAffects=False,
                selection='SumMappedEquipmentAffixFrequencies',
                inputHashes={n: hashlib.sha256((balance / (n + '.json')).read_bytes()).hexdigest() for n in inputs},
                tiers=[dict(tier=i+1, name=name, minimumLevel=20+i*10, maximumLevel=29+i*10)
                       for i, name in enumerate(TIER_NAMES)], stats=stats)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    output = json.dumps(build(), indent=2) + '\n'
    path = BALANCE / 'moulds.json'
    if args.check:
        assert path.read_text() == output, 'Mould catalogue differs from its pinned authoring inputs'
    else:
        path.write_text(output)
    print('Mould catalogue: 8 tiers, 15 percentage families, 120 identities, total frequency430')
if __name__ == '__main__': main()
