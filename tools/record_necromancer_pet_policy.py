#!/usr/bin/env python3
"""Join approved Necromancer pet requirements to actual consumers and native evidence."""
import argparse
from collections import Counter
from pathlib import Path

import skill_checkpoint_evidence as e

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/necro-pet-snapshot-refresh'
COHORT = ROOT / '.local/class-skills/necromancer-lifecycle-acceptance'

# Each row names production consumers, the fixture that inspects actual behavior,
# and retained combined-run families. Snapshot guarantees are checked separately
# on current public births rather than inferred from older live-upgrade fixtures.
REQUIREMENTS = (
    ('warrior-return', 'Hard 10 grants 34 flat physical attacker return.',
     ('SkeletonPetUpgrades', 'NpcEquipmentRetaliation'), 'SkeletonUpgradeChecks', ('skeleton-upgrades',)),
    ('warrior-fast', 'Hard 20 grants the existing movement-only Extra Fast affix.',
     ('SkeletonPetUpgrades', 'SkeletonWorldBody', 'SkeletonPets'), 'SkeletonUpgradeChecks', ('skeleton-upgrades',)),
    ('mage-elements', 'New Mages use fire/lightning/ice; saved poison Mages become ice without rerolling identity or wounds.',
     ('NecromancerSummons',), 'MageElementChecks', ('mage-elements',)),
    ('mage-basics', 'Basic Sorceress spell rank follows captured effective Mage rank; captured Skeleton Mastery adds its approved flat endpoint difference.',
     ('MageBasicSkills', 'NecromancerMageMissiles'), 'MageBasicChecks', ('mage-basics',)),
    ('mage-triples', 'Hard 5 triples only basic Fire Bolt, Ice Bolt and the normal Charged Bolt volley.',
     ('MageBasicSkills', 'MageOffensiveSkills'), 'MageBasicChecks', ('mage-basics',)),
    ('mage-meteor', 'Fire hard 10 grants a 5% fixed-rank-10 Meteor opportunity.',
     ('MageOffensiveSkills', 'MageMeteors', 'FireGround'), 'MageMeteorChecks', ('mage-meteor',)),
    ('mage-enchant', 'Fire hard 20 automatically enchants eligible unenchanted allies at combat start with rank 20; no owner mana or casting action.',
     ('MagePetBuffs', 'MageWorld', 'SorceressEffects'), 'MagePetBuffChecks', ('mage-pet-buff',)),
    ('mage-chain', 'Lightning hard 10 grants a 5% fixed-rank-10 Chain Lightning opportunity.',
     ('MageOffensiveSkills', 'ChainLightning'), 'MageChainChecks', ('mage-chain',)),
    ('mage-static', 'Lightning hard 20 grants a separate 5% fixed-rank-5 Static Field opportunity.',
     ('MageOffensiveSkills', 'StaticFields'), 'MageStaticFieldChecks', ('mage-static',)),
    ('mage-glacial', 'Ice hard 10 grants a 5% fixed-rank-10 Glacial Spike opportunity, including ordinary freeze/corpse rules.',
     ('MageOffensiveSkills', 'NecromancerMageMissiles'), 'MageGlacialChecks', ('mage-glacial',)),
    ('mage-armor', 'Ice hard 20 has always-on fixed-rank-20 Frozen Armor through the shared armor consumer.',
     ('MagePetBuffs', 'SorceressEffects'), 'MagePetBuffChecks', ('mage-pet-buff',)),
    ('clay', 'Clay uses the Warrior curve with Golem Mastery, Stone Skin at hard 10 and Magic Resistant at hard 20.',
     ('GolemPetDamage', 'ClayGolemUpgrades', 'ClayGolemBody'), 'ClayGolemUpgradeChecks', ('clay-upgrades',)),
    ('blood', 'Blood uses 150% of Clay baseline, 100% Open Wounds at hard 10 and 5% rank-5 Life Tap at hard 20.',
     ('GolemPetDamage', 'NpcOpenWounds', 'NpcHitSkills', 'NpcCombatEvents'), 'BloodLifeTapChecks', ('blood-damage', 'npc-open-wounds', 'blood-life-tap')),
    ('iron', 'Iron keeps Clay baseline plus its qualified original-item recipe; Extra Strong at hard 10 and Spectral Hit at hard 20 reuse existing affixes.',
     ('GolemPetDamage', 'IronGolemBodyNumbers', 'IronGolemUpgrades', 'NpcSpectralHit'), 'IronGolemSpectralChecks', ('iron-golem-damage', 'iron-golem-strong', 'iron-golem-spectral')),
    ('fire', 'Fire uses 200% Clay physical baseline plus own-rank Paladin Holy Fire without Paladin synergies; CBF at hard 10 and ordinary cold immunity at hard 20.',
     ('GolemPetDamage', 'FireGolemUpgrades', 'FireGolemAura'), 'FireGolemUpgradeChecks', ('fire-upgrades',)),
    ('army-caps', 'Current effective rank gives 5 Mages/3 Revives at 20, 7/5 at 40+, with approved intermediate steps and FIFO enforcement.',
     ('NecromancerArmyLimits', 'SkeletonPets', 'ReviveNativeActivation', 'ClassGear'), 'NecromancerArmyChecks', ('necromancer-army',)),
    ('revive-lifetime', 'Qualified original Revives have no expiry; death, owner death and replacement still remove them. Loot remains separate from captured combat effects.',
     ('ReviveNativeActivation', 'ReviveOriginals', 'PersistentMinions'), 'ReviveNativeActivationChecks', ('revive-native-activation', 'revive-cast')),
    ('safe-parking', 'Actual safe regions park armies with original identities and wounds; no automatic healing, cure, refill or effect-deadline reset.',
     ('MinionSafeAreas', 'PersistentMinions', 'TimedEffects'), 'SafeAreaMinionChecks', ('safe-area-minion', 'parking-boundary')),
    ('healer-companion', 'Explicit healer menu can heal the parked living roster; the registered named mercenary remains beside the owner.',
     ('MinionSafeAreas', 'MercenaryCare'), 'SafeAreaMinionChecks', ('safe-area-minion',)),
    ('difficulty', 'Configured facet difficulty rescales existing summons while preserving current life percentage.',
     ('SummonDifficulties', 'ReviveFacets'), 'SummonDifficultyChecks', ('summon-difficulty', 'revive-facet')),
    ('mage-flight-lifetime', 'Already-fired Mage bolts retain original packets and expiry after Mage death, subject to owner lifecycle restrictions.',
     ('NecromancerMageMissiles',), 'MageProjectileLifetimeChecks', ('mage-projectile-lifetime',)),
)


def record(check=False):
    birth = e.load(STAGE / 'birth-inputs.json')
    for name, digest in birth['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
    for name, digest in e.load(STAGE / 'source-inputs.json').items():
        assert e.sha(ROOT / name) == digest, name
    for name, digest in birth['copiedData'].items():
        assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest, name
    runtime = Path('/media/aomukai/FILES/Sanctuary')
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll', 'Assemblies/Sanctuary.Content.dll'):
        assert e.sha(runtime / '.local/server' / name) == birth['artifacts'][name], name
    assert e.sha(STAGE / 'integration/Sanctuary.Integration.dll') == birth['artifacts']['Assemblies/Sanctuary.Integration.dll']
    assert e.sha(ROOT / '.local/class-skills/class-roster-ui/integration/Sanctuary.Integration.dll') == birth['artifacts']['Assemblies/Sanctuary.Integration.dll']
    expected = Counter({'iron-golem-birth-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', 'warrior-snapshot', 'necro-snapshot'):
        expected[family + '-check'] = 1
        expected[family + '-reload-check'] = 2
    log_path = STAGE / 'birth-native-checks.log'
    log = log_path.read_text()
    assert e.markers(log_path) == expected and sum(expected.values()) == 15
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    warrior = e.load(STAGE / 'warrior-snapshot-fixtures.json')
    pets = e.load(STAGE / 'necro-snapshot-fixtures.json')
    assert warrior['Generation'] == pets['Generation'] == 2
    assert len(warrior['Actors']) == 2 and len(pets['Actors']) == 18
    assert [row['Upgrade']['HardRank'] for row in warrior['Actors']] == [9, 20]
    assert {row['OwnerHard'] for row in pets['Actors']} == {1, 20}
    for row in [*warrior['Actors'], *pets['Actors']]:
        assert 0 < row['Life'] < row['Maximum'] and row['Order'] > 0
        assert row['Upgrade']['Actor'] == row['Body']
    for row in pets['Actors']:
        assert row['Upgrade']['HardRank'] == row['Hard'] and row['Rank'] == row['Hard'] + 1
    cohort_path = ROOT / 'tools/reference/necromancer-lifecycle-acceptance-native-evidence.json'
    cohort = e.load(cohort_path)
    assert e.markers(COHORT / 'birth-native-checks.log') == Counter(cohort['markers'])
    rows = []
    files = [ROOT / 'src/Sanctuary.Content' / (name + '.cs') for _, _, consumers, _, _ in REQUIREMENTS for name in consumers]
    for key, rule, consumers, fixture, families in REQUIREMENTS:
        source = ROOT / 'tests/Sanctuary.Integration' / (fixture + '.cs')
        assert source.is_file(), fixture
        markers = {name: count for name, count in cohort['markers'].items()
                   if any(name.startswith(family + '-') for family in families)}
        assert all(any(name.startswith(family + '-') for name in markers) for family in families), key
        rows.append(dict(requirement=key, approvedRule=rule,
            productionConsumers=[str(Path('src/Sanctuary.Content') / (name + '.cs')) for name in consumers],
            fixture=str(source.relative_to(ROOT)), retainedNativeMarkers=markers,
            status='verified-for-qualified-actors', modernParityClaimed=False))
        files.append(source)
    # The separate snapshot cohorts inspect hard/soft boundaries and actual
    # effects after owner respec; their numeric assertions are not replaced by
    # a check that a birth dictionary merely exists.
    rows.append(dict(requirement='birth-snapshots',
        approvedRule='New Warriors, Mages and four golems retain captured effective ranks, hard unlocks, mastery, wounds, order and RNG through training/gear/respec and reload; missing historical supplements keep explicit compatibility behavior.',
        productionConsumers=['src/Sanctuary.Content/SummonUpgradeSnapshots.cs', 'src/Sanctuary.Content/NecromancerSummons.cs',
                             'src/Sanctuary.Content/SkeletonPets.cs', 'src/Sanctuary.Content/IronGolemCasting.cs'],
        fixtures=['tests/Sanctuary.Integration/WarriorSnapshotChecks.cs', 'tests/Sanctuary.Integration/NecroSnapshotChecks.cs'],
        currentNativeMarkers={name: count for name, count in expected.items() if 'snapshot' in name},
        savedOriginalWarriors=2, savedOriginalMageGolemActors=18, status='verified-for-current-public-births'))
    files += [ROOT / path for row in rows for path in row['productionConsumers']]
    files += [ROOT / 'tests/Sanctuary.Integration' / (name + '.cs') for name in ('WarriorSnapshotChecks', 'NecroSnapshotChecks')]
    files += [STAGE / name for name in ('birth-inputs.json', 'source-inputs.json', 'sources.tar.gz',
        'birth-native-checks.log', 'warrior-snapshot-fixtures.json', 'necro-snapshot-fixtures.json',
        'iron-golem-items-fixtures.json', 'npc-resource-callbacks-fixtures.json', 'run-1.log',
        'integration/Sanctuary.Integration.dll')]
    files += [cohort_path, ROOT / 'tests/check_necromancer_pet_snapshot_refresh.py',
              ROOT / 'tools/reference/class-roster-ui-evidence.json',
              ROOT / 'docs/necromancer-pet-policy-audit.md', ROOT / 'docs/pet-adjustment-review.md',
              ROOT / 'docs/summon-stat-snapshots.md', Path(__file__)]
    e.write('necromancer-pet-policy-audit.json', dict(referenceVersion='3.3.93847',
        reviewedRequirements=len(rows), requirements=rows, snapshotRefreshMarkers=dict(expected),
        currentSnapshotOriginalActors=20, saves=3, actualReloads=2, gameplayChanged=False,
        artifacts=birth['artifacts'], existingClassMarkersReused=cohort['combinedMarkers'],
        existingClassMarkerCountsNotAddedAgain=True, installedGameplayReused=True,
        limits=['Only qualified original actors and item projections; unsupported species/properties still refuse.',
                'Historical missing-supplement compatibility is constructed explicitly; no old-binary writer run.',
                'Shared sunder delivery, native monster spell conversion and exact modern path/event gaps retain their individual scopes.',
                'Graphical combat and final effects/audio/icons remain separate. This is not a class or goal certificate.'],
        classCertified=False, goalComplete=False), files, check)
    print('Necromancer pet policy:22 approved requirements mapped;15 focused results/20 original actors/two reloads;existing class proofs reused.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
