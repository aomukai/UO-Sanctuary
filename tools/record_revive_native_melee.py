#!/usr/bin/env python3
"""Bind proactive class guard and source weapon base to complete native products."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess

import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_snapshot
import prepare_revive_melee_native as prepared

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/revive-native-melee'
FULL = STAGE / 'full-regression'
PREDECESSOR = '7dadafe'
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
    _, prior = frozen_receipt(PREDECESSOR, 'tools/reference/revive-native-stats-evidence.json')
    assert prior['verified']['nativePatchCount'] == 69
    native = STAGE / 'native'
    manifest = json.loads((STAGE / 'native-pre-build-inputs.json').read_text())
    assert manifest == prepared.native.inputs(native)
    assert manifest['upstream'] == prepared.native.PIN and len(manifest['patches']) == 70
    artifacts = [Path(__file__).resolve(), SNAPSHOT, STAGE / 'native-pre-build-inputs.json', ROOT / 'tools/prepare_revive_melee_native.py']
    for name, value in manifest['patches'].items():
        assert sha(ROOT / name) == value, name
        artifacts.append(ROOT / name)
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        path = native / 'Distribution' / name
        assert sha(path) == retained['artifacts'][name]
        artifacts.append(path)
    for kind in ['native', 'content', 'integration']:
        path = STAGE / (kind + '-build.log')
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts.append(path)
        if kind != 'native':
            artifacts.append(STAGE / kind / ('Sanctuary.' + kind.title() + '.dll'))

    log = checked_log(STAGE / 'life-native-checks.log', 18)
    assert markers(log) == Counter({'revive-source-life-check': 1, 'revive-source-life-reload-check': 2,
        'revive-source-life-live-check': 3, 'revive-source-life-live-result': 3, 'revive-source-life-snapshot-check': 3, 'revive-native-melee-live-check':3, 'revive-native-melee-live-result':3})
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
    assert log.count('Revive native melee:32source/ordinary fixed-base comparisons,full native OnHit physical Mastery delivery') == 3
    for boot in range(3):
        path = STAGE / f'revive-native-melee-result-{boot}.json'
        trace = json.loads(path.read_text())
        assert trace['Actor'] == trace['Original'] and trace['Ai'] == 1
        assert (trace['Minimum'], trace['Maximum']) == (31,78)
        assert trace['FixedBase'] == 20 and trace['FixedExpected256'] == trace['FixedActual256'] == 7680
        assert trace['NaturalDamage256'] > 0 and trace['OrdinaryScaled'] == 98 and trace['OrdinaryUntargeted']
        assert 31*3*256//2 <= trace['NaturalDamage256'] <= 78*3*256//2
        artifacts.append(path)
    focused = checked_log(STAGE / 'stats-native-checks.log', 332)
    previous = ROOT / '.local/class-skills/revive-native-stats/stats-native-checks.log'
    assert markers(focused) == markers(previous.read_text())
    full = checked_log(FULL / 'class-native-checks.log', 1184, 35)
    assert markers(full) == markers((ROOT / '.local/class-skills/revive-native-stats/full-regression/class-native-checks.log').read_text())
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
    assert fingerprints == [779, 21]
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
    artifacts += [source_probe, main_path, ROOT / 'tests/check_revive_melee_runtime.py', ROOT / 'tests/check_revive_melee_regression.py',
        ROOT / 'tools/build_skill_roadmap.py', ROOT / 'docs/class-skill-roadmap.md', ROOT / 'docs/class-skills-resume-checkpoint.md',
        ROOT / 'docs/skill-mechanical-reference.md', ROOT / 'docs/revive-native-behavior-review.md', ROOT / 'tools/reference/revive-native-behavior-policy.json']
    artifacts += [STAGE / name for name in ['life-inputs.json', 'life-native-checks.log', 'life-run.log',
        'revive-source-life-fixtures.json', 'stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log']]
    artifacts += [FULL / name for name in ['class-inputs.json', 'class-native-checks.log', 'run.log']]
    result = dict(referenceVersion='3.3.93847', component='Proactive native class guard and mapped original Revive weapon base',
        installed=False, handlerEnabled=False, automaticRetentionEnabled=False, goalComplete=False,
        handlers=184, completedClasses=[], processMemoryRead=False, modernCodeEquivalenceClaimed=False,
        verified=dict(pairedFixedBaseComparisons=96, naturalIdleHostilePursuitContactCases=3, fullNativeWeaponOnHitCases=3, ordinaryGuardNegativeCases=3,
            sourceHpRngCases=4756, sourceLevelCases=4756, currentFormulaContexts=144, suppliedSunderRefusals=864,
            nativeAosWeaponDeliveryCases=6, sharedHitCheckLevelCases=12, preparedVigorMightCompositionCases=6, legacyOmittedCombatFieldCases=6, isolatedMarkers=18, isolatedBoots=3, isolatedActualReloads=2,
            retainedMarkers=332, retainedBoots=3, retainedActualReloads=2, fullClassMarkers=1184,
            fullClassBoots=3, fullClassActualReloads=2, fullClassSaves=35, pythonTests=71,
            actualOriginalHpActors=6, savedOriginalHpActors=2, nativePatchCount=70, nativeBinariesReused=False, isolatedNativeCleanBuild=True,
            fractionalDamageHealing=True, above65000SavedCurrentAndMaximum=True, preparedOrdersRank=255,
            testActorRegenerationIntervalHours=1, highLifeLedgerBoundsChecked=True,
            inputFingerprints=fingerprints, unchangedNamedDefinitions=573, unchangedMainInstallationArtifacts=106),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt='tools/reference/revive-native-stats-evidence.json'),
        nativeBehaviorPolicy='tools/reference/revive-native-behavior-policy.json',
        inputInventory=retained, isolatedLifeInputInventory=life, fullClassInputInventory=complete,
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))},
        established=[
            'Native patch70 adds nullable guard permission. Null preserves ordinary pet selection; true permits proactive class-minion acquisition; false excludes current combatant, nearby and aggressor-list routes. Persistent-minion owner/class/map/hostility/target rules supply that decision without a replacement brain. This is the approved compatible-native adapter, not exact modern D2 AI dispatcher equivalence.',
            'Mapped originals with an optional saved combat plan use their native damage-range roll directly in ComputeDamageAOS, bypassing UO Strength/Anatomy/Tactics/Lumberjacking scaling. Ordinary NPCs and other callers retain the previous ScaleDamageAOS path. Native RNG is retained; no exact D2 RNG ordering is claimed.',
            'Three fresh Skeleton originals naturally acquire an idle hostile with no initial combatant or aggression, pursue and deliver contact damage while preserving original type/serial/AI. Friendly players/pets are refused, an ordinary guard stays passive, and parking removes proactive permission. Natural attack cadence and every native role are outside this acceptance.',
            'Each boot compares32paired fixed source/ordinary base calculations with800Strength and120skills: source20remains20, ordinary20scales to98. A full native weapon OnHit delivers7680FP(30life) from controlled20base,200%Mastery and50%physical resistance. The original31to78range is restored for natural pursuit. Two saved source-stat originals retain the new opt-in through both reloads; the three natural pursuit cases themselves use fresh originals.',
            'All70patches replay in a fresh isolated pinned tree. Native/content/integration clean rebuilds succeed, identical products/data retain332focused and1184full markers in separate three-boot/two-reload worlds, and71Python tests pass. Both broad scopes export matching fresh779/21audits;573named decisions and106installed artifacts are unchanged.'],
        remaining=[
            'This establishes compatible native guard acquisition and source base delivery for the tested role. Complete AI cadence/selection details, original item/proc/stat graphs, source critical/special-attack behavior, Summon Resist/events, regeneration, native ability-clock policies, accrued-stat/charm collection and inherited sunder remain unfinished or individually unsupported.',
            'Source geometry/mode/input/payment, facet replanning and respec remain unfinished. Mana/stamina are explicit prepared inputs. Learned Revive and automatic retention stay gated. The approved reuse/adaptation/replacement policy applies per behavior; this does not complete a skill/class or replace the full roadmap. Final effects/audio and talent/hotkey icons remain separate placeholders; installed build/saves are unchanged.'])
    output = ROOT / 'tools/reference/revive-native-melee-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Revive native melee:18isolated/332retained/1184full/71Python;70native patches,natural idle-hostile acquisition/pursuit/contact,source base and ordinary pet controls pass;full Revive remains gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
