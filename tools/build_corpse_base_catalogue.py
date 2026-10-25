#!/usr/bin/env python3
"""Stage current corpse base-HP tables for the native Death Sentry bridge.

The default destination is uninstalled. This packages verified table cells;
it neither guesses life from native HitsMax nor activates a skill handler.
"""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'tools/reference/d2r-named-equipment'
DEFAULT = ROOT / '.local/class-skills/corpse-base-catalogue'
HP = ('minHP', 'maxHP', 'MinHP(N)', 'MaxHP(N)', 'MinHP(H)', 'MaxHP(H)')
LEVEL_HP = ('HP', 'HP(N)', 'HP(H)', 'L-HP', 'L-HP(N)', 'L-HP(H)')


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def encoded(value):
    return json.dumps(value, indent=2) + '\n'


def pinned(name, manifest):
    path = SOURCE / 'json' / (name + '.json')
    relative = str(path.relative_to(SOURCE))
    if digest(path) != manifest['files'][relative]['sha256']:
        raise ValueError('Pinned table changed: ' + relative)
    return path, json.loads(path.read_text())


def compile_catalogue():
    manifest_path = SOURCE / 'sources.json'
    manifest = json.loads(manifest_path.read_text())
    if manifest['mechanicsReferenceVersion'] != '3.3.93847':
        raise ValueError('Unexpected mechanical baseline')
    monsters_path, monsters = pinned('monstats', manifest)
    levels_path, levels = pinned('monlvl', manifest)
    rules_path = ROOT / 'data/Sanctuary/Balance/corpse-source-rules.json'
    ids = sorted(row['source'] for row in json.loads(rules_path.read_text()) if row['selectable'])
    if len(ids) != 521 or len(set(ids)) != len(ids):
        raise ValueError('Unexpected selectable-corpse coverage')
    if set(levels) != {str(i) for i in range(111)}:
        raise ValueError('Missing source level rows, including levels 0 and 110')

    rows, raw_monsters, inverted = [], [], []
    for key in ids:
        source = monsters[key]
        values = [source.get(name, 0) for name in ('noRatio', *HP)]
        if source['Id'] != key or values[0] not in (0, 1):
            raise ValueError('Invalid monster identity/noRatio: ' + key)
        if any(type(v) is not int or not 0 <= v <= 2147483647 for v in values):
            raise ValueError('Invalid monster HP cells: ' + key)
        for difficulty in range(3):
            if values[1 + 2*difficulty] > values[2 + 2*difficulty]:
                # Preserve authored cells even when the source range is odd.
                # The verified corpse formula averages them; sorting or
                # repairing here would change its mechanical input.
                inverted.append(dict(source=key, difficulty=difficulty))
        raw_monsters.append(values)
        rows.append(dict(source=key, noRatio=bool(values[0]),
                         minimum=values[1::2], maximum=values[2::2]))

    level_rows, raw_levels = [], []
    for level in range(111):
        source = levels[str(level)]
        values = [source[name] for name in LEVEL_HP]
        if source['Level'] != level or any(type(v) is not int or v < 0 for v in values):
            raise ValueError('Invalid monster-level HP row: ' + str(level))
        raw_levels.append(values)
        level_rows.append(dict(level=level, classic=values[:3], expansion=values[3:]))

    # Preserve the exact ordered inputs used by the existing complete source
    # calculator proof, rather than introducing a second set of HP formulas.
    proof_path = ROOT / 'tools/reference/corpse-base-life-source-evidence.json'
    proof = json.loads(proof_path.read_text())
    fixture_path = ROOT / proof['fixture']['path']
    if digest(fixture_path) != proof['fixture']['sha256']:
        raise ValueError('Source calculator fixture changed')
    fixture = json.loads(fixture_path.read_text())
    if (fixture['ids'], fixture['monsterInputs'], fixture['levelHp']) != (ids, raw_monsters, raw_levels):
        raise ValueError('Packaged data differs from verified calculator inputs')
    if len(fixture['cases']) != 52100:
        raise ValueError('Incomplete source calculator fixture')

    outputs = {'corpse-base-monsters.json': rows, 'corpse-base-levels.json': level_rows}
    paths = [manifest_path, monsters_path, levels_path, rules_path, proof_path, fixture_path]
    evidence = dict(referenceVersion='3.3.93847', installed=False, handlerEnabled=False,
                    sourceCommit=manifest['commit'], corpseSourceRows=len(rows), levelRows=len(level_rows),
                    sourceCalculatorContexts=52100,
                    invertedSourceRangesPreserved=inverted,
                    inputHashes={str(p.relative_to(ROOT)): digest(p) for p in paths},
                    outputHashes={name: hashlib.sha256(encoded(value).encode()).hexdigest()
                                  for name, value in outputs.items()},
                    scope='Exact current table inputs for native corpse base-life lookup; classic and expansion columns retained.',
                    limits=['No native source/level/difficulty capture or corpse persistence is enabled by this export.',
                            'Base life must use the source calculator, not native HitsMax or elite/player-count life bonuses.',
                            'RNG, corpse search/consumption, area damage and learned input remain separate.'])
    outputs['corpse-base-catalogue-evidence.json'] = evidence
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output-dir', type=Path, default=DEFAULT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    outputs = compile_catalogue()
    if not args.check:
        args.output_dir.mkdir(parents=True, exist_ok=True)
    for name, value in outputs.items():
        path = args.output_dir / name
        text = encoded(value)
        if args.check:
            if path.read_text() != text:
                raise ValueError('Stale corpse base catalogue: ' + str(path))
        else:
            path.write_text(text)
    print('Staged corpse base catalogue:521 monster rows/111 levels exactly match verified source inputs; runtime unchanged.')


if __name__ == '__main__':
    main()
