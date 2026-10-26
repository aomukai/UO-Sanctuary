#!/usr/bin/env python3
"""Bind learned Spirit Wolf payment/input/FIFO to the isolated saved-world run."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/spirit-wolf'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False, full_class=False):
    world_path = ROOT / 'tools/reference/spirit-wolf-world-native-evidence.json'
    world = json.loads(world_path.read_text())
    for name, digest in world['hashes'].items():
        assert sha(ROOT / name) == digest, name
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    assert log.count('spirit-wolf-casting-check: passed') == 1
    assert log.count('spirit-wolf-casting-reload-check: passed') == 2
    for name in ['right', 'quick']:
        assert log.count('spirit-wolf-' + name + '-check: passed') == 3
        assert log.count('spirit-wolf-' + name + '-result: passed') == 3
    fixture = json.loads((STAGE / 'casting-fixtures.json').read_text())
    assert len(fixture['Wolves']) == len(fixture['Orders']) == len(fixture['States']) == 2
    assert fixture['Orders'] == sorted(set(fixture['Orders'])) and fixture['Orders'][0] > 0
    for text in fixture['States']:
        body = json.loads(text)
        assert body['LearnedCast'] and body['Owner'] == fixture['Owner']
        assert body['Snapshot']['Birth']['Rank'] == 2 and body['Controller']['Fault'] is None
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert len([x for x in status['skills'] if x['implementation'] == 'handler-present']) == 167
    assert status['completedClasses'] == []
    paths = [ROOT / name for name in [
        'src/Sanctuary.Content/DruidSpiritWolfCasting.cs', 'src/Sanctuary.Content/DruidSpiritWolfBody.cs',
        'src/Sanctuary.Content/DruidSpiritWolfWorld.cs', 'src/Sanctuary.Content/DruidCombat.cs',
        'src/Sanctuary.Content/ClassCombat.cs', 'src/Sanctuary.Content/SkillBindings.cs',
        'src/Sanctuary.Content/BattleCommand.cs', 'src/Sanctuary.Content/DruidRavenCasting.cs',
        'tests/Sanctuary.Integration/SpiritWolfCastingChecks.cs', 'tests/check_spirit_wolf_body.py',
        'tools/record_spirit_wolf_casting.py', 'tools/reference/spirit-wolf-world-native-evidence.json',
        'tools/reference/spirit-wolf-equipment-scope.json', 'data/Sanctuary/Balance/class-skill-status.json']]
    paths += [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log',
        'casting-fixtures.json', 'content/Sanctuary.Content.dll', 'integration/Sanctuary.Integration.dll']]
    full_markers = None
    if full_class:
        full = json.loads((STAGE / 'class-inputs.json').read_text())
        focused = json.loads((STAGE / 'stats-inputs.json').read_text())
        assert full['artifacts'] == focused['artifacts'] and full['copiedData'] == focused['copiedData']
        for name, digest in full['sourceInputs'].items():
            assert sha(ROOT / name) == digest, name
        full_log = (STAGE / 'class-native-checks.log').read_text()
        assert ' ERR]' not in full_log and ': failed' not in full_log
        assert full_log.count('Shutting down') == 3
        assert 'Native class skills and projectile checks passed, including two save/reload cycles.' in (STAGE / 'class-run.log').read_text()
        full_markers = len(re.findall(r': passed', full_log))
        assert full_markers > 1000
        paths += [STAGE / name for name in ['class-inputs.json', 'class-native-checks.log', 'class-run.log']]
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=True, installed=False,
        boots=3, actualReloads=2, connectedRightCasts=3, connectedQuickCasts=3,
        passedMarkers=len(re.findall(r': passed', log)), handlers=167, druidHandlers=11,
        fullClassRegression=full_class, fullClassBoots=3 if full_class else 0, fullClassPassedMarkers=full_markers,
        currentExecutableParity=False, completedClasses=[], goalComplete=False,
        hashes={str(path.relative_to(ROOT)): sha(path) for path in paths},
        established=[
            'Learned Summon Spirit Wolf routes through shared SC windup/release. The native row requires15mana; fractional insufficiency refuses, early release does nothing, and one successful action pays once after allocation, before per-type replacement. Stop, move-out-and-back, facet roundtrip, recovery, freeze, lost mana and newly blocked ground do not pay or create a wolf.',
            'Live target coordinates are re-read at release; a deleted target falls back to its cached point. Ground actors cannot be placed on occupied enemy footprints; deterministic native adjacent retry is explicitly an adapter. Town casting follows the pinned intown flag. Existing wolves retain individual targets when another is cast.',
            'Only learned wolves participate in the learned per-type cap; internal component factories deliberately bypass it. Effective-rank cap uses the existing source formula min(rank,5). Actual Tarnhelm and Battle Command changes add slots, remove oldest wolves after rank loss, and retain immutable birth stats. Raven survives wolf replacement and cap changes. Owner death removes learned wolves.',
            'Successful creation uses the existing saved Battle Command order ledger. Two real world reloads retain two parked learned wolves, complete saved controller/birth state and FIFO orders; casting after each reload replaces the actual oldest. Right/quick bindings persist and an unpaid saved windup is discarded without mana charge or summon replay.',
            'Three authenticated loopback right-hold casts and three quick-key casts exercise actual protocol packets, unpaid animation windup, release,15mana payment and ground placement under the natural server clock. The unsupported left slot is refused by SkillBindings.'],
        remaining=[
            'The source spawn retry parameter4 is projected to a deterministic two-native-tile perimeter. Source room/collision search order, complete ground footprint geometry and current-client cached/dead target fallback remain unverified.',
            'The cold-only physical-floor omission and observed-bite-continuation-neutral-delay-v1 remain explicitly provisional. Exact modern busy/mid-swing cadence, path repair/AStar branches, global event/RNG order, cold sunder, recipient passive avoidance and graphical client acceptance are not certified by casting tests.',
            ('Exact-package full retained class regression passes three boots/two reloads; installation remains separate.' if full_class
                else 'Full retained class regression and installation are not claimed by this focused run.')
                + ' Installed main remains at166handlers/Druid10. Missing wolf model/emergence/bite/cold/teleport/hurt/death/audio and talent/right/quick icons remain separate identified placeholders.'])
    target = ROOT / 'tools/reference/spirit-wolf-casting-native-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print(f'Learned Spirit Wolf:3boots/2reloads/3right+3quick casts,15mana payment and saved FIFO; candidate167/Druid11, fullClass={full_class}, no class completion or installation claim.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
