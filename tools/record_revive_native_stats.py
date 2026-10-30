#!/usr/bin/env python3
"""Bind original-NPC level, physical mastery and speed to exact native products."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess

import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/revive-native-stats'
FULL = STAGE / 'full-regression'
PREDECESSOR = '2518f64'
SNAPSHOT = STAGE / 'pre-build-inputs.json'
sha = inventory.sha


def markers(log):
    return Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed', log))


def checked_log(path, count, saves=3):
    log = path.read_text()
    assert ': failed' not in log and ' ERR]' not in log, path
    assert sum(markers(log).values()) == count, path
    assert log.count('Shutting down') == 3 and log.count('Writing world save snapshot done') == saves, path
    return log


def record(check=False):
    inventory.STAGE = STAGE
    retained = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    life = inventory.validate_inventory(STAGE / 'life-inputs.json')
    inventory.STAGE = FULL
    complete = inventory.validate_inventory(FULL / 'class-inputs.json')
    inventory.STAGE = STAGE
    for name in [STAGE / 'stats-inputs.json', STAGE / 'life-inputs.json', FULL / 'class-inputs.json']:
        check_snapshot(SNAPSHOT, name)
    assert retained['artifacts'] == life['artifacts'] == complete['artifacts']
    assert retained['copiedData'] == life['copiedData'] == complete['copiedData']
    _, prior = frozen_receipt(PREDECESSOR, 'tools/reference/revive-source-life-evidence.json')
    assert prior['verified']['nativePatchCount'] == 69
    reference = json.loads((STAGE / 'native-reference.json').read_text())
    assert reference['frozenCommit'] == PREDECESSOR
    artifacts = [Path(__file__).resolve(), SNAPSHOT, STAGE / 'native-reference.json']
    for name, value in reference['artifacts'].items():
        path = Path(reference['nativeDistribution']) / name
        assert sha(path) == value == retained['artifacts'][name] == prior['inputInventory']['artifacts'][name]
        artifacts.append(path)
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]

    log = checked_log(STAGE / 'life-native-checks.log', 12)
    assert markers(log) == Counter({'revive-source-life-check': 1, 'revive-source-life-reload-check': 2,
        'revive-source-life-live-check': 3, 'revive-source-life-live-result': 3, 'revive-source-life-snapshot-check': 3})
    assert log.count('4756complete-source release HP/RNG comparisons pass') == 3
    assert log.count('real Hell originals retain native identities,source roll/caster scaling/mastery calc1,FP256damage/heal and prepared rank255Orders above65000') == 3
    assert log.count('Revive combat:4756complete-source level controls,144current formula contexts,864explicit sunder refusals') == 3
    saved = json.loads((STAGE / 'revive-source-life-fixtures.json').read_text())
    assert len(saved['Actors']) == len(set(saved['Actors'])) == 2
    assert saved['Maximum'][1] > saved['Life'][1] > 65000 * 256
    for i, row in enumerate(saved['States']):
        plan = row['Setup']['LifePlan']
        assert row['Actor'] == saved['Actors'][i] and row['Owner'] == saved['Owner']
        assert row['SavedLife256'] is None and row['Difficulty'] == plan['Difficulty'] == 2
        assert plan['CasterLevel'] == 50 and plan['MasteryRank'] == 20
        assert plan['Maximum256'] > plan['Scaled256'] and plan['NextSeed'] != plan['Seed']
        assert row['Setup']['CombatPlan'] == dict(Level=min(plan['OriginalLevel'],plan['CasterLevel']),DamagePercent=plan['MasteryRank']*10,VelocityPercent=50)
        assert saved['Life'][i] < saved['Maximum'][i]
    focused = checked_log(STAGE / 'stats-native-checks.log', 332)
    previous = ROOT / '.local/class-skills/revive-source-life/stats-native-checks.log'
    assert markers(focused) == markers(previous.read_text())
    full = checked_log(FULL / 'class-native-checks.log', 1184, 35)
    assert markers(full) == markers((ROOT / '.local/class-skills/revive-source-life/full-regression/class-native-checks.log').read_text())
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        exported = FULL / name
        full_audit = json.loads(exported.read_text())
        if 'ContentAssemblyPath' in audit:
            assert (ROOT / full_audit['ContentAssemblyPath']).resolve(strict=True) == (ROOT / audit['ContentAssemblyPath']).resolve(strict=True)
            full_audit['ContentAssemblyPath'] = audit['ContentAssemblyPath']
        assert audit == full_audit
        assert audit['ContentAssemblySha256'] == retained['artifacts']['Assemblies/Sanctuary.Content.dll']
        for source, value in audit['InputHashes'].items():
            assert sha(ROOT / source) == value, source
        artifacts += [path, exported, ROOT / 'tools/reference' / name]
        audits.append(audit)
    old = json.loads(subprocess.check_output(['git', 'show', PREDECESSOR + ':tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert audits[0]['Definitions'] == old['Definitions'] and len(audits[0]['Definitions']) == 573
    fingerprints = [len(a['InputHashes']) for a in audits]
    assert fingerprints == [778, 21]
    tests = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
    main_path = ROOT / 'tools/reference/creeper-frame-installation-evidence.json'
    main = json.loads(main_path.read_text())
    assert len(main['installedArtifacts']) == 106
    for name, value in main['installedArtifacts'].items():
        assert sha(Path(main['runtimeRoot']) / name) == value, name
    for path in (ROOT / 'data/Sanctuary').rglob('*'):
        if path.is_file():
            assert path.read_bytes() == subprocess.check_output(['git', 'show', PREDECESSOR + ':' + str(path.relative_to(ROOT))], cwd=ROOT), path
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 184 and status['completedClasses'] == []
    source_probe = ROOT / '.local/class-skills/revive-source-policy-source-probe.json'
    _, policy = frozen_receipt('f0dc651', 'tools/reference/revive-source-policy-native-evidence.json')
    assert sha(source_probe) == policy['artifacts'][str(source_probe.relative_to(ROOT))]
    artifacts += [source_probe, main_path, ROOT / 'tests/check_revive_stats_runtime.py', ROOT / 'tests/check_revive_stats_regression.py',
        ROOT / 'tools/build_skill_roadmap.py', ROOT / 'docs/class-skill-roadmap.md', ROOT / 'docs/class-skills-resume-checkpoint.md',
        ROOT / 'docs/skill-mechanical-reference.md', ROOT / 'docs/revive-native-behavior-review.md', ROOT / 'tools/reference/revive-native-behavior-policy.json']
    artifacts += [STAGE / name for name in ['life-inputs.json', 'life-native-checks.log', 'life-run.log',
        'revive-source-life-fixtures.json', 'stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log']]
    artifacts += [FULL / name for name in ['class-inputs.json', 'class-native-checks.log', 'run.log']]
    result = dict(referenceVersion='3.3.93847', component='Original NPC Revive effective level, physical Mastery damage and velocity',
        installed=False, handlerEnabled=False, automaticRetentionEnabled=False, goalComplete=False,
        handlers=184, completedClasses=[], processMemoryRead=False, modernCodeEquivalenceClaimed=False,
        verified=dict(sourceHpRngCases=4756, sourceLevelCases=4756, currentFormulaContexts=144, suppliedSunderRefusals=864,
            nativeAosWeaponDeliveryCases=6, sharedHitCheckLevelCases=12, preparedVigorMightCompositionCases=6, legacyOmittedCombatFieldCases=6, isolatedMarkers=12, isolatedBoots=3, isolatedActualReloads=2,
            retainedMarkers=332, retainedBoots=3, retainedActualReloads=2, fullClassMarkers=1184,
            fullClassBoots=3, fullClassActualReloads=2, fullClassSaves=35, pythonTests=71,
            actualOriginalHpActors=6, savedOriginalHpActors=2, nativePatchCount=69, nativeBinariesReused=True,
            fractionalDamageHealing=True, above65000SavedCurrentAndMaximum=True, preparedOrdersRank=255,
            testActorRegenerationIntervalHours=1, highLifeLedgerBoundsChecked=True,
            inputFingerprints=fingerprints, unchangedNamedDefinitions=573, unchangedMainInstallationArtifacts=106),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt='tools/reference/revive-source-life-evidence.json'),
        nativeBehaviorPolicy='tools/reference/revive-native-behavior-policy.json',
        inputInventory=retained, isolatedLifeInputInventory=life, fullClassInputInventory=complete,
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))},
        established=[
            'The optional saved combat plan records the source level reduction and current-row physical Mastery/velocity values. Effective level enters CreatureProfiles/ClassStats and the shared native hit-check gate for either NPC recipient. Complete older release controls provide4756independent level outcomes per boot.144current formula contexts and864explicit nonzero supplied sunder refusals are checked per boot; those are data/adapter checks, not a complete modern passive dispatcher certificate.',
            'Six real original NPC activations verify actual native AOS physical-only weapon damage from explicitly supplied raw100damage, unchanged nonweapon fire, both attacker/defender level effects in shared hit resolution, and the same serial/type/HP plan. This does not prove native BaseWeapon base-roll/skill scaling or a natural AI melee loop. Prepared Vigor/Might leases verify additive speed and physical-percent aggregation. No replacement actor, subclass, native patch or expiration timer is added.',
            'Two saved original combat/HP plans survive both actual reloads, including fractional current/max life above65000. Legacy integral setup remains covered by332retained markers. The dedicated cases decode a source-life setup with the new combat field omitted, verify no retroactive bonuses, and restore the current plan. Malformed combat plans reject atomically.',
            'The same products/data pass332retained and1184full-class markers in separate three-boot/two-reload worlds. Explicit export paths provide both scopes with fresh matching named/affix audits. The candidate reuses the accepted69-patch binaries. All573named decisions and106installed artifacts remain unchanged.'],
        remaining=[
            'Nonzero inherited sunder is refused explicitly because the global native immunity consumer is unfinished. The current available player gear bonus supplier is used at capture; complete current accrued-stat/active-charm collection is not established. The seven current passive fields do not prove the modern dispatcher from the older five-slot helper.',
            'Complete retained stat dependencies, original item effects/procs, source regeneration, Summon Resist/event composition, native ability clocks, dynamic AI/eligibility, proactive enemy acquisition, native BaseWeapon UO strength/skill scaling, source geometry/mode/input/payment, facet replanning and respec remain unfinished. The approved behavior policy permits scoped reuse/adaptation/replacement; these are concrete adapter tasks rather than a request for wholesale native preservation. Mana/stamina remain explicit prepared inputs. Learned Revive and automatic retention stay gated; no full skill/class parity is claimed. Effects/audio and talent/hotkey icons remain separately tracked placeholders; installed build/saves are unchanged.'])
    output = ROOT / 'tools/reference/revive-native-stats-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Revive native stats:4756source level controls;12isolated/332retained/1184full/71Python;level,physical mastery and speed verified;full skill remains gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
