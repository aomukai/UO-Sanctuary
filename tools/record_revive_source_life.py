#!/usr/bin/env python3
"""Bind original-NPC source HP and exact save/reload checks to frozen products."""
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
STAGE = ROOT / '.local/class-skills/revive-source-life'
FULL = STAGE / 'full-regression'
PREDECESSOR = '490ccd4'
SNAPSHOT = STAGE / 'pre-build-attempt-7.json'
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
    _, prior = frozen_receipt(PREDECESSOR, 'tools/reference/revive-native-stabling-evidence.json')
    assert prior['verified']['nativePatchCount'] == 69
    reference = json.loads((STAGE / 'native-reference.json').read_text())
    assert reference['frozenCommit'] == PREDECESSOR
    artifacts = [Path(__file__).resolve(), SNAPSHOT, STAGE / 'native-reference.json']
    for name, value in reference['artifacts'].items():
        path = Path(reference['nativeDistribution']) / name
        assert sha(path) == value == retained['artifacts'][name] == prior['inputInventory']['artifacts'][name]
        artifacts.append(path)
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build-attempt-7.log')
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]

    log = checked_log(STAGE / 'life-native-checks.log', 12)
    assert markers(log) == Counter({'revive-source-life-check': 1, 'revive-source-life-reload-check': 2,
        'revive-source-life-live-check': 3, 'revive-source-life-live-result': 3, 'revive-source-life-snapshot-check': 3})
    assert log.count('4756complete-source release HP/RNG comparisons pass') == 3
    assert log.count('real Hell originals retain native identities,source roll/caster scaling/mastery calc1,FP256damage/heal and prepared rank255Orders above65000') == 3
    saved = json.loads((STAGE / 'revive-source-life-fixtures.json').read_text())
    assert len(saved['Actors']) == len(set(saved['Actors'])) == 2
    assert saved['Maximum'][1] > saved['Life'][1] > 65000 * 256
    for i, row in enumerate(saved['States']):
        plan = row['Setup']['LifePlan']
        assert row['Actor'] == saved['Actors'][i] and row['Owner'] == saved['Owner']
        assert row['SavedLife256'] is None and row['Difficulty'] == plan['Difficulty'] == 2
        assert plan['CasterLevel'] == 50 and plan['MasteryRank'] == 20
        assert plan['Maximum256'] > plan['Scaled256'] and plan['NextSeed'] != plan['Seed']
        assert saved['Life'][i] < saved['Maximum'][i]
    focused = checked_log(STAGE / 'stats-native-checks.log', 332)
    previous = ROOT / '.local/class-skills/revive-native-stabling/stats-native-checks.log'
    assert markers(focused) == markers(previous.read_text())
    full = checked_log(FULL / 'class-native-checks.log', 1184, 35)
    assert markers(full) == markers((ROOT / '.local/class-skills/revive-native-stabling/full-regression/class-native-checks.log').read_text())
    # The main launch omitted explicit audit output paths. Export independently
    # with identical products/data, without rerunning or relabeling its markers.
    audit_inputs = json.loads((FULL / 'named-inputs.json').read_text())
    assert audit_inputs['artifacts'] == retained['artifacts'] and audit_inputs['copiedData'] == retained['copiedData']
    audit_log = checked_log(FULL / 'named-native-checks.log', 4)
    assert markers(audit_log) == Counter({'named-gear-check': 1, 'named-gear-reload-check': 2, 'first-loop-catalogue-check': 1})

    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        exported = FULL / ('full-named-item-runtime-audit.json' if name == 'named-item-runtime-audit.json' else name)
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
    assert fingerprints == [777, 21]
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
    artifacts += [source_probe, main_path, ROOT / 'tests/check_revive_source_life.py', ROOT / 'tests/check_revive_life_regression.py', ROOT / 'tests/check_named_candidate.py',
        ROOT / 'tools/build_skill_roadmap.py', ROOT / 'docs/class-skill-roadmap.md', ROOT / 'docs/class-skills-resume-checkpoint.md']
    artifacts += [STAGE / name for name in ['life-inputs.json', 'life-native-checks.log', 'life-run.log',
        'revive-source-life-fixtures.json', 'stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log']]
    artifacts += [FULL / name for name in ['class-inputs.json', 'class-native-checks.log', 'run.log', 'named-inputs.json', 'named-native-checks.log', 'audit-run.log']]
    artifacts += [p for p in STAGE.glob('attempt-*/**/*') if p.is_file()]
    artifacts += list(STAGE.glob('pre-build-attempt-*.json'))
    result = dict(referenceVersion='3.3.93847', component='Original NPC source HP roll, reduction, calc1 and FP256 persistence',
        installed=False, handlerEnabled=False, automaticRetentionEnabled=False, goalComplete=False,
        handlers=184, completedClasses=[], processMemoryRead=False, modernCodeEquivalenceClaimed=False,
        verified=dict(sourceHpRngCases=4756, isolatedMarkers=12, isolatedBoots=3, isolatedActualReloads=2,
            retainedMarkers=332, retainedBoots=3, retainedActualReloads=2, fullClassMarkers=1184,
            fullClassBoots=3, fullClassActualReloads=2, fullClassSaves=35, pythonTests=71,
            supplementalAuditMarkers=4, supplementalAuditBoots=3, supplementalAuditActualReloads=2,
            actualOriginalHpActors=6, savedOriginalHpActors=2, nativePatchCount=69, nativeBinariesReused=True,
            fractionalDamageHealing=True, above65000SavedCurrentAndMaximum=True, preparedOrdersRank=255,
            testActorRegenerationIntervalHours=1, highLifeLedgerBoundsChecked=True,
            inputFingerprints=fingerprints, unchangedNamedDefinitions=573, unchangedMainInstallationArtifacts=106),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt='tools/reference/revive-native-stabling-evidence.json'),
        inputInventory=retained, isolatedLifeInputInventory=life, fullClassInputInventory=complete,
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))},
        established=[
            'A saved plan reuses pinned corpse min/max bounds, historical source seed, the inclusive older-source roll and caster-level reduction, and current Revive calc1 with captured effective Skeleton Mastery. The numerical kernel matches4756successful older-source release controls per boot; this does not establish complete modern dispatcher equivalence.',
            'The prepared activation applies FP256 life to the same original native NPC through an external resolver and existing SkillHealth/BattleOrders hooks. No replacement subclass, actor clone, loot transfer or expiration timer is added. The corpse retains its historical seed; the plan separately records the advanced RNG seed.',
            'Two saved original identities retain exact fractional maximum/current life through both reloads, including prepared rank255BattleOrders above65000. Saved FP256 current life restores after native loading clamps. This service rank is an explicit stress fixture, not an attainable-build claim. Fixture-only one-hour native regeneration isolates persistence; source regeneration remains unfinished.',
            'The BattleOrders life ledger now accepts whole maxima within the supported FP256 range and rejects the next value. Mana/stamina validation remains65000. Existing integral-only Revive records remain supported; malformed source plans reject atomically. All332retained and1184full-class markers pass with the same products/data.'],
        remaining=[
            'This is the base HP/reduction/calc1 component, not complete source retained-stat/passive/sunder/event composition. The accompanying source SetStat12 level reduction is not yet connected to CreatureProfiles.Level. Complete equipment effects/procs, source regeneration, native ability clocks, dynamic AI/eligibility, source geometry/mode/input/payment, difficulty replanning and respec remain unfinished. Mana/stamina are still explicitly supplied prepared inputs.',
            'Learned Revive and automatic retention remain gated. The approved no-expiration lifetime is retained, but complete skill/class parity is not claimed. Effects/audio and talent/hotkey icons remain separately tracked placeholders. Installed working build and saves are unchanged.'])
    output = ROOT / 'tools/reference/revive-source-life-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Revive source life:4756source controls;12isolated/332retained/1184full/71Python;FP256 and above65000reloads verified;full skill remains gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
