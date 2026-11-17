#!/usr/bin/env python3
"""Attribute Druid's approved pet policies and ten-body safe-area qualification."""
import argparse
from collections import Counter
from pathlib import Path

import skill_checkpoint_evidence as e

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/druid-safe-area'
COHORT = ROOT / '.local/class-skills/druid-lifecycle-acceptance'
REQUIREMENTS = (
    ('spirit-wolf', 'Hard 10 grants Open Wounds; hard 20 grants ordinary cold immunity; retain cold-only attacks and baseline unchillability.',
     ('SpiritWolfUpgrades', 'NpcOpenWounds', 'DruidSpiritWolfHit', 'DruidSpiritWolfBody'), 'SpiritWolfUpgradeChecks', ('spirit-wolf-upgrades', 'spirit-wolf-mode')),
    ('dire-wolf', 'Hard 10 grants existing movement-only Extra Fast; hard 20 grants existing Extra Strong.',
     ('DireWolfUpgrades', 'DruidDireWolfHit', 'DruidDireWolfBody'), 'DireWolfUpgradeChecks', ('dire-wolf-upgrades',)),
    ('grizzly', 'Ordinary hits at hard 10 stun normal mobs 50%/3 seconds and bosses 10%/1 second; no passive stun on Smite. Hard 20 grants 10% Crushing Blow.',
     ('GrizzlyUpgrades', 'DruidGrizzlyHit', 'NpcCrushing'), 'GrizzlyUpgradeChecks', ('grizzly-upgrades',)),
    ('creeper-damage', 'Scale the existing curve to 2000 average base damage over four seconds at rank 20, then apply the sole hard-Rabies synergy.',
     ('DruidCreeperPoison', 'DruidCreeperStats'), 'CreeperBalanceChecks', ('creeper-balance',)),
    ('creeper-snapshot', 'Current summon captures rank and hard Rabies; existing fields retain their original packets. Owner poison gear is excluded; unsupported sunder remains refused.',
     ('DruidCreeperStats', 'DruidCreeperPoison', 'DruidCreeperGround'), 'CreeperLearnedChecks', ('creeper-learned',)),
    ('vine-vulnerability', 'All three vines remain vulnerable; no underground exposure immunity is introduced.',
     ('DruidCreeperBody', 'DruidRecyclerBody'), 'RecyclerLearnedChecks', ('creeper-body', 'creeper-learned', 'recycler-body', 'recycler-learned')),
    ('recyclers', 'Carrion Vine keeps baseline recovery; Solar uses the approved current table values with whole-point rounding.',
     ('DruidRecyclerRecovery', 'DruidRecyclerBody'), 'RecyclerRecoveryChecks', ('recycler-recovery', 'recycler-model', 'recycler-learned')),
    ('spirits', 'Oak Sage, Heart of Wolverine and Spirit of Barbs retain baseline behavior and their captured birth stats.',
     ('DruidSpiritStats', 'DruidOakBody', 'DruidWolverineBody', 'DruidBarbsBody'), 'OakCastingChecks', ('oak-casting', 'wolverine-casting', 'barbs-casting')),
    ('raven', 'Retain baseline budget and retarget behavior; born stats and already-issued packets are preserved.',
     ('DruidRavenBody', 'DruidRavenStats'), 'RavenSnapshotChecks', ('raven-snapshot', 'raven-casting')),
    ('beast-snapshots', 'Current public Wolf/Grizzly births retain inherited ranks, hard unlocks, resources and future attack-entry packets through training, gear, respec and reload.',
     ('DruidBeastAttackStats', 'SummonUpgradeSnapshots'), 'DruidSnapshotChecks', ('druid-snapshot',)),
    ('safe-area', 'All ten prepared Druid summon types park in actual safe regions, retain wounds/resources/identity, suppress recovery, and return without slot or Raven-budget resets.',
     ('MinionSafeAreas', 'PersistentMinions', 'TimedEffects'), 'DruidSafeAreaChecks', ()),
)


def record(check=False):
    born = e.load(STAGE / 'birth-inputs.json')
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    for name, digest in e.load(STAGE / 'source-inputs-2.json').items():
        assert e.sha(ROOT / name) == digest, name
    before = e.load(STAGE / 'before-source.json')
    assert all(e.sha(ROOT / name) == digest for name, digest in before.items())
    for name, digest in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest, name
    runtime = Path('/media/aomukai/FILES/Sanctuary')
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll', 'Assemblies/Sanctuary.Content.dll'):
        assert e.sha(runtime / '.local/server' / name) == born['artifacts'][name], name
    assert e.sha(STAGE / 'integration-2/Sanctuary.Integration.dll') == born['artifacts']['Assemblies/Sanctuary.Integration.dll']
    expected = Counter({'iron-golem-birth-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', 'druid-safe-area'):
        expected[family + '-check'] = 1
        expected[family + '-reload-check'] = 2
    log_path = STAGE / 'birth-native-checks.log'
    log = log_path.read_text()
    assert e.markers(log_path) == expected and sum(expected.values()) == 12
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    saved = e.load(STAGE / 'druid-safe-area-fixtures.json')
    assert saved['Generation'] == 2 and len(saved['Actors']) == 10
    assert len({row['Ability'] for row in saved['Actors']}) == 10
    assert len({row['Pet'] for row in saved['Actors']}) == len({row['Owner'] for row in saved['Actors']}) == 10
    for row in saved['Actors']:
        assert 0 < row['Life'] < row['Maximum'] and row['Order'] > 0
    for build in (1, 2):
        text = (STAGE / f'build-{build}.log').read_text()
        assert '0 Warning(s)' in text and '0 Error(s)' in text
    failure = STAGE / 'failed-run-1'
    for name, digest in e.load(failure / 'frozen-hashes.json').items():
        assert e.sha(failure / name) == digest, name
    assert 'Wrong parking/return state for Raven' in (failure / 'birth-native-checks.log').read_text()
    cohort_path = ROOT / 'tools/reference/druid-lifecycle-acceptance-native-evidence.json'
    cohort = e.load(cohort_path)
    assert cohort['nativeMarkers'] == 1139
    assert e.markers(COHORT / 'birth-native-checks.log') == Counter(cohort['markers'])
    # Druid already has current public-birth coverage; do not repeat it.
    snapshots = e.load(COHORT / 'druid-snapshot-fixtures.json')
    assert snapshots['Generation'] == 2 and len(snapshots['Actors']) == 12
    rows, files = [], []
    for key, rule, consumers, fixture, families in REQUIREMENTS:
        source = ROOT / 'tests/Sanctuary.Integration' / (fixture + '.cs')
        assert source.is_file(), fixture
        markers = {name: count for name, count in cohort['markers'].items()
                   if any(name.startswith(family + '-') for family in families)}
        assert all(any(name.startswith(family + '-') for name in markers) for family in families), key
        rows.append(dict(requirement=key, approvedRule=rule,
            productionConsumers=[str(Path('src/Sanctuary.Content') / (name + '.cs')) for name in consumers],
            fixture=str(source.relative_to(ROOT)), retainedNativeMarkers=markers,
            currentNativeMarkers=dict(expected) if key == 'safe-area' else {}, status='verified-scoped'))
        files.append(source)
        files.extend(ROOT / name for name in rows[-1]['productionConsumers'])
    files += [p for p in failure.rglob('*') if p.is_file()]
    files += [STAGE / name for name in ('birth-inputs.json', 'before-source.json', 'source-inputs-2.json',
        'sources-2.tar.gz', 'birth-native-checks.log', 'druid-safe-area-fixtures.json',
        'build-1.log', 'build-2.log', 'run-2.log', 'integration-2/Sanctuary.Integration.dll')]
    files += [cohort_path, COHORT / 'druid-snapshot-fixtures.json', ROOT / 'tests/check_druid_safe_area.py',
              ROOT / 'docs/druid-pet-policy-audit.md', ROOT / 'docs/pet-adjustment-review.md', Path(__file__)]
    e.write('druid-pet-policy-audit.json', dict(referenceVersion='3.3.93847', requirements=rows,
        reviewedRequirements=len(rows), currentNativeMarkers=dict(expected), preparedSpeciesParked=10,
        safeAreaSaves=3, safeAreaReloads=2, currentPublicBirthSnapshotActorsReused=12,
        retainedDruidResults=1139, retainedCountsNotAddedAgain=True, productionChanged=False,
        artifacts=born['artifacts'], failure='Temporary test region used Internal instead of saved world facet after offline reload; Integration2 corrects only that fixture.',
        limits=['Parking controls use real prepared bodies and offline explicit return admission; authenticated learned casts and public-birth snapshots retain their existing separate proofs.',
                'No new cross-facet rescaling, healer/companion, arbitrary region or graphical combat claim is made by these parking controls.',
                'Unsupported sunder, legacy profiles and modern source/geometry limitations remain explicit. Final effects/audio/icons remain separately tracked.'],
        classCertified=False, goalComplete=False), files, check)
    print('Druid pet policy:11 requirements;ten original prepared species park/return through two reloads;12 current public-birth snapshots reused.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
