#!/usr/bin/env python3
"""Build opt-in higher-difficulty numeric variants from the reviewed counterpart roster."""
import argparse
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BALANCE = ROOT / 'data/Sanctuary/Balance'


def build(source):
    pins = json.loads((BALANCE / 'sources.json').read_text())
    tables = {}
    for name in ['MonStats', 'MonStats2', 'MonLvl']:
        raw = (source / (name + '.txt')).read_bytes()
        assert hashlib.sha256(raw).hexdigest() == pins[name]['sha256'], name
        tables[name] = list(csv.DictReader(raw.decode('utf-8-sig').splitlines(), delimiter='\t'))
    monsters = {r['Id']: r for r in tables['MonStats'] if r['Id']}
    modes = {r['Id']: r for r in tables['MonStats2'] if r['Id']}
    levels = {int(r['Level']): r for r in tables['MonLvl'] if r['Level'].isdigit()}
    gold = {difficulty: {r['id']: r for r in json.loads((BALANCE / ('creature-gold-' + difficulty.lower() + '.json')).read_text())['policies']}
            for difficulty in ['Nightmare', 'Hell']}
    groups = []
    for group in json.loads((BALANCE / 'creature-rewards.json').read_text())['groups']:
        variants = {}
        for difficulty, suffix in [('Nightmare', '(N)'), ('Hell', '(H)')]:
            reward = group['variants'][difficulty]
            if not reward['enabled']:
                continue
            mon, level = monsters[reward['reference']], reward['level']
            def scaled(field, column):
                value = int(mon[field + suffix] or 0)
                return max(1, value if mon['noRatio'] == '1' else value * int(levels[level]['L-' + column + suffix]) // 100)
            life = scaled('MaxHP', 'HP')
            assert life <= 65000, (group['id'], difficulty, life)
            variants[difficulty] = dict(enabled=True, applyStats=True, level=level, life=life,
                defense=scaled('AC', 'AC'),
                resistances={key: int(mon[column + suffix] or 0) for key, column in
                    [('physical', 'ResDm'), ('magic', 'ResMa'), ('fire', 'ResFi'), ('lightning', 'ResLi'),
                     ('cold', 'ResCo'), ('poison', 'ResPo')]},
                attack1=dict(minDamage=scaled('A1MinD', 'DM'), maxDamage=scaled('A1MaxD', 'DM'),
                    attackRating=scaled('A1TH', 'TH'), physical=None, fire=None, lightning=None, cold=None, poison=None, magic=None),
                attack2=dict(minDamage=scaled('A2MinD', 'DM'), maxDamage=scaled('A2MaxD', 'DM'),
                    attackRating=scaled('A2TH', 'TH')),
                rewards=dict(treasureClass=gold[difficulty][group["id"]]["treasureClass"]),
                traits=dict(drainEffectiveness=int(mon['Drain' + suffix] or 0),
                    chillEffectiveness=int(mon['coldeffect' + suffix] or 0)),
                sourceStats=mon, sourceModes=modes[mon['MonStatsEx']])
        if variants:
            groups.append(dict(id=group['id'], types=group['types'], variants=variants))
    return dict(sourceCommit='45112569deb9384738ccafe5c24ebbb71f41c7c9', players=1,
                lifePolicy='SourceMaximum', nativeAbilities='Retained', groups=groups)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-dir', type=Path, required=True)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    text = json.dumps(build(args.source_dir), indent=2) + '\n'
    target = BALANCE / 'creature-difficulties.json'
    if args.check:
        assert target.read_text() == text, 'Stale higher-difficulty projection'
    else:
        target.write_text(text)


if __name__ == '__main__':
    main()
