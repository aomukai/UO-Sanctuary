#!/usr/bin/env python3
"""Bind accepted-death resource and independent equipment-effect capture to native saves."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess

import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_snapshot
from probe_warlock_runtime import ROOT, digest

STAGE = ROOT / '.local/class-skills/revive-predeath-state'
PREDECESSOR = 'c55d389'


def sha(path):
    return digest(path.read_bytes())


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    check_snapshot(STAGE / 'pre-build-inputs.json', STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt(PREDECESSOR, 'tools/reference/revive-original-retention-native-evidence.json')
    assert prior['verified']['passMarkers'] == 309
    artifacts = [Path(__file__).resolve()]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 312
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    pattern = r'\] ([A-Za-z0-9-]+): passed'
    before = Counter(re.findall(pattern, (ROOT / '.local/class-skills/revive-original-retention/stats-native-checks.log').read_text()))
    after = Counter(re.findall(pattern, log))
    assert sum(before.values()) == 309 and not before - after
    assert after - before == Counter({'revive-before-death-check':1,'revive-before-death-reload-check':2})
    assert log.count('Revive before death:3actual deaths/6equipped items retain pre-cleanup mana/stamina,independent gear rolls and native ability clock data;99recipient levels/45bonus codes match existing suppliers,loot stays lootable and deleted loot cannot alter frozen effects;10invalid payloads per corpse and old-v0 compatibility pass;activation,procs,custom timers and learned Revive remain unfinished') == 3
    import prepare_revive_predeath_native as predeath
    native = STAGE / 'native'
    manifest = json.loads((STAGE / 'native-pre-build-inputs.json').read_text())
    assert manifest == predeath.native.inputs(native)
    assert manifest['upstream'] == 'eff9de028136b11e544fd81375d1e0e710e22dd6' and len(manifest['patches']) == 67
    native_log = (STAGE / 'native-build.log').read_text()
    assert all(s in native_log for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        path = native / 'Distribution' / name
        assert sha(path) == inputs['artifacts'][name], name
        artifacts.append(path)
    for name, hash_ in manifest['patches'].items():
        assert sha(ROOT / name) == hash_, name
        artifacts.append(ROOT / name)
    saved = json.loads((STAGE / 'revive-before-death-fixtures.json').read_text())
    assert len(saved) == 3 and len({r['Original']['Actor'] for r in saved}) == 3
    assert sum(len(r['Equipment']) for r in saved) == 6
    for row in saved:
        state = json.loads(row['Original']['BeforeDeathJson'])
        assert state['Mana'] == row['Mana'] and state['Stamina'] == row['Stamina'] and state['Hits'] == row['Hits']
        assert json.loads(state['EquipmentJson']) == row['Equipment']
        for item in row['Equipment']:
            assert item['UnavailableReason'] is None and len(json.loads(item['LevelsJson'])) == 99
    policy_path = ROOT / 'tools/reference/revive-equipment-deviation.json'
    assert json.loads(policy_path.read_text())['userAnswer'] == 'Keep loot; preserve combat effects separately'
    artifacts += [STAGE/'native-pre-build-inputs.json', STAGE/'native-build.log', ROOT/'tools/prepare_revive_predeath_native.py',ROOT/'tools/prepare_revive_retention_native.py',policy_path]
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name_, hash_ in audit['InputHashes'].items():
            assert sha(ROOT / name_) == hash_, name_
        audits.append(audit)
        artifacts += [path, ROOT / 'tools/reference' / name]
    old = json.loads(subprocess.check_output(['git', 'show', PREDECESSOR + ':tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert audits[0]['Definitions'] == old['Definitions'] and len(audits[0]['Definitions']) == 573
    fingerprints = [len(a['InputHashes']) for a in audits]
    assert fingerprints == [773, 21]
    tests = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
    main_path = ROOT / 'tools/reference/creeper-frame-installation-evidence.json'
    main = json.loads(main_path.read_text())
    assert len(main['installedArtifacts']) == 106
    for name, hash_ in main['installedArtifacts'].items():
        assert sha(Path(main['runtimeRoot']) / name) == hash_, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 184
    # This increment changes source and persistence, not balance, assets or runtime data.
    for path in (ROOT / 'data/Sanctuary').rglob('*'):
        if path.is_file():
            assert path.read_bytes() == subprocess.check_output(['git', 'show', PREDECESSOR + ':' + str(path.relative_to(ROOT))], cwd=ROOT), path
    diagnostic = STAGE/'diagnostics'
    assert 'Gear was returned' in (diagnostic/'attempt1/stats-native-checks.log').read_text()
    assert 'weaponDeleted=True,armorDeleted=True' in (diagnostic/'attempt2/stats-native-checks.log').read_text()
    assert 'resource clearing boundary' in (diagnostic/'attempt3/stats-native-checks.log').read_text()
    assert 'Unknown gem family' in (diagnostic/'attempt4/stats-native-checks.log').read_text()
    artifacts += [p for p in diagnostic.rglob('*') if p.is_file()]
    for name in ['pre-build-inputs.json', 'stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log', 'revive-before-death-fixtures.json']:
        artifacts.append(STAGE / name)
    artifacts += [main_path, ROOT / 'tests/check_revive_before_death.py', ROOT / 'tools/build_skill_roadmap.py', ROOT / 'tools/build_property_integration_backlog.py', ROOT / 'tools/reference/item-property-integration-backlog.json', ROOT / 'tools/reference/uo-monster-strength-classification-policy.json']
    result = dict(referenceVersion='3.3.93847', component='Accepted-death resources, native clock data and independent converted gear effects', installed=False, handlerEnabled=False, retainedNativeActorImplemented=True, activeReviveActorImplemented=False, automaticRetentionEnabled=False, goalComplete=False, handlers=184, completedClasses=[], processMemoryRead=False,
        verified=dict(passMarkers=312, retainedMarkers=309, worldBoots=3, actualWorldReloads=2, acceptedDeathFixtures=3, equippedItems=6, recipientLevels=99, numericBonusQueries=45, numericBonusComparisons=26730, localResourceLevelComparisons=594, invalidPayloadChecksPerBoot=30, lootRemainsSeparate=True, deletedLootIndependence=True, nativeAbilityClockDataCaptured=True, nativeCustomTimersCaptured=False, oldV0Compatible=True, nativePatchCount=67, isolatedNativeCleanBuild=True, preBuildAndNativeSourceDataMatch=True, pythonTests=71, unchangedNamedDefinitions=573, inputFingerprints=fingerprints, unchangedMainInstallationArtifacts=106, newFullClassRun=False),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt='tools/reference/revive-original-retention-native-evidence.json'), inputInventory=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))},
        established=[
            'An accepted BaseCreature.OnBeforeDeath hook captures actual resources, effective attributes, original active creature descriptors, equipped item identities/full gear rolls and native framework ability cooldown data before Mobile.Kill clears resources and moves equipment. Incoming damage/death callbacks may already have changed resources; comparison uses the values at the actual hook. Revive HP remains its separate source base-life roll/passive path.',
            'Frozen converted gear effects reuse existing ItemStatContext and NamedPropertyRuntime suppliers, preserving intrinsic parameters and fixed gem/rune/material contributions. Six equipped items across three deaths compare99recipient-level tables and26730 numeric bonus queries over45codes, including ordinary magic, named armor and actual gem data. Immutable JSON payloads and private parsed contexts retain data independently of native item references; decoded gear changes cannot mutate saved rolls.',
            'The approved policy keeps original droppable equipped gear as corpse loot. RewardLife previously destroyed those items in its generic native-loot cleanup; captured original droppable equipment is now exempt, while default uncaptured cleanup is explicitly verified unchanged. Loot can be acquired and destroyed without changing frozen effects, and physical items are never copied or returned to the NPC. Before-death capture remains gated with the unfinished learned route.',
            'The native framework clears singleton ability cooldown dictionaries on death and does not serialize them. New read-only root/group traversal preserves their remaining values in captured data before that clearing; a real dragon breath clock is retained in the saved snapshot while the live native clock becomes zero. Applying/aging that clock during actual Revive activation is not claimed, and other custom ability timers remain unverified.',
            'Both actual reloads retain original actors, accepted-death records, equipment effect payloads and corpse loot. Thirty malformed payload controls per boot refuse atomically; old v0 retained originals without new fields remain loadable and do not fabricate accepted-death data. Unsupported item suppliers or capture failures remain explicit, not guessed. Transient unconfirmed death captures are discarded rather than persisted.',
            'Four rejected native attempts are preserved. The first two exposed destruction of equipped items by RewardLife; the second logged actual deletion/loot flags. A later fixture incorrectly compared resources before damage rather than at the capture hook, and another used lowercase ruby instead of catalog ID Ruby. The accepted run follows the policy fix and corrected controls with fresh source snapshots/clean rebuilds. Native patch67, matching isolated source/binaries,71Python tests,573named decisions and106installed artifacts pass.'],
        remaining=[
            'Compose actual activation of the same original actor, including resource/state policy, current passives/sunder/events, AI/ownership/cap, geometric placement and lease detachment before corpse removal. Numeric suppliers and archived property data are not a completed live NPC stat graph, typed item-effect/proc dispatch or native ability-clock restoration policy.',
            'Custom native timers, conditional set effects/native nonconverted properties, full source dynamic-state production, other death paths and complete owner/death/replacement/facet/logout/respec/save lifecycle remain unfinished. The descriptor NativeAbilityStateCaptured flag remains false. No learned Revive, full-class run, modern executable equivalence, installation or class completion is claimed; approved no-expiration lifetime and separate effects/audio/icon ledgers are unchanged.'])
    output = ROOT / 'tools/reference/revive-before-death-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Before-death state:312 native/71 Python;3 deaths,6items,independent99level numeric suppliers and clock data;activation/procs/learned Revive unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
