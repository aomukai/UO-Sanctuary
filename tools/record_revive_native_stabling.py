#!/usr/bin/env python3
"""Bind class-minion stabling protection to actual native targets and retained regressions."""
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
import prepare_revive_stabling_native as prepared

STAGE = ROOT / '.local/class-skills/revive-native-stabling'
FULL = STAGE / 'full-regression'
PREDECESSOR = '41b769a'


def sha(path):
    return digest(path.read_bytes())


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    check_snapshot(STAGE / 'pre-build-inputs.json', STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt(PREDECESSOR, 'tools/reference/revive-native-control-evidence.json')
    assert prior['verified']['retainedMarkers'] == 332
    artifacts = [Path(__file__).resolve()]
    native = STAGE / 'native'
    manifest = json.loads((STAGE / 'native-pre-build-inputs.json').read_text())
    assert manifest == prepared.native.inputs(native)
    assert manifest['upstream'] == 'eff9de028136b11e544fd81375d1e0e710e22dd6' and len(manifest['patches']) == 69
    for kind in ['native', 'content', 'integration']:
        path = STAGE / (kind + '-build.log')
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts.append(path)
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        path = native / 'Distribution' / name
        assert sha(path) == inputs['artifacts'][name], name
        artifacts.append(path)
    for name, hash_ in manifest['patches'].items():
        assert sha(ROOT / name) == hash_, name
        artifacts.append(ROOT / name)
    base = native / 'Projects/UOContent/Mobiles/BaseCreature.cs'
    assert 'public virtual bool Commandable => ClassCombatHooks.PersistentMinion?.Invoke(this) != true;' in base.read_text()
    artifacts.append(base)
    for kind in ['content', 'integration']:
        artifacts.append(STAGE / kind / ('Sanctuary.' + kind.title() + '.dll'))

    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 332
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert 'Retained original NPC and socket lifecycle pass3boots/2reloads against native stabling candidate' in (STAGE / 'run.log').read_text()
    pattern = r'\] ([A-Za-z0-9-]+): passed'
    before = Counter(re.findall(pattern, (ROOT / '.local/class-skills/revive-native-control/stats-native-checks.log').read_text()))
    assert before == Counter(re.findall(pattern, log)) and sum(before.values()) == 332
    clock = inventory.validate_inventory(STAGE / 'stabling-inputs.json')
    check_snapshot(STAGE / 'pre-build-inputs.json', STAGE / 'stabling-inputs.json')
    assert clock['artifacts'] == inputs['artifacts'] and clock['copiedData'] == inputs['copiedData']
    clock_log = (STAGE / 'stabling-native-checks.log').read_text()
    assert ': failed' not in clock_log and ' ERR]' not in clock_log
    assert clock_log.count(': passed') == 15 and clock_log.count('Shutting down') == clock_log.count('Writing world save snapshot done') == 3
    assert Counter(re.findall(pattern, clock_log)) == Counter({'revive-native-control-check':1,
        'revive-native-control-reload-check':2, 'revive-native-control-live-check':3, 'revive-native-control-live-result':3, 'revive-native-stabling-live-check':3, 'revive-native-stabling-live-result':3})
    assert clock_log.count('Revive native control:12forced native hourly boundaries preserve class original at loyalty1;ordinary high-taming pet keeps menus/chance and releases at low loyalty;class pet menu/speech suppressed;natural original guard AI follows owner without a replacement brain;no global time advance') == 3
    assert '36forced loyalty boundaries' in (STAGE / 'stabling-run.log').read_text()
    assert clock_log.count('Revive native stabling:actual trainer target rejects a nonsummoned class original before gold/owner/location/roster mutation and delayed orphan cleanup;ordinary high-taming pet pays30,stables and claims with native follower accounting;class identity remains unchanged') == 3
    saved = json.loads((STAGE / 'revive-native-control-fixtures.json').read_text())
    assert saved['Owner'] == saved['State']['Owner'] and saved['Actor'] == saved['State']['Actor']
    assert saved['Loyalty'] == 1 and saved['Taming'] > 29.1 and saved['State']['Original']['RuntimeType'] == 'Server.Mobiles.Dragon'

    inventory.STAGE = FULL
    complete = inventory.validate_inventory(FULL / 'class-inputs.json')
    inventory.STAGE = STAGE
    check_snapshot(STAGE / 'pre-build-inputs.json', FULL / 'class-inputs.json')
    assert complete['artifacts'] == inputs['artifacts'] and complete['copiedData'] == inputs['copiedData']
    full_log = (FULL / 'class-native-checks.log').read_text()
    assert ': failed' not in full_log and ' ERR]' not in full_log and full_log.count(': passed') == 1184
    assert full_log.count('Shutting down') == 3 and full_log.count('Writing world save snapshot done') == 35
    assert full_log.count('named-gear-check: passed') == 1 and full_log.count('named-gear-reload-check: passed') == 2
    assert 'including two save/reload cycles.' in (FULL / 'run.log').read_text()

    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        full_audit = json.loads((FULL / name).read_text())
        if 'ContentAssemblyPath' in audit:
            assert (ROOT / full_audit['ContentAssemblyPath']).resolve(strict=True) == (ROOT / audit['ContentAssemblyPath']).resolve(strict=True)
            full_audit['ContentAssemblyPath'] = audit['ContentAssemblyPath']
        assert audit == full_audit
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name_, hash_ in audit['InputHashes'].items():
            assert sha(ROOT / name_) == hash_, name_
        audits.append(audit)
        artifacts += [path, FULL / name, ROOT / 'tools/reference' / name]
    old = json.loads(subprocess.check_output(['git', 'show', PREDECESSOR + ':tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert audits[0]['Definitions'] == old['Definitions'] and len(audits[0]['Definitions']) == 573
    fingerprints = [len(a['InputHashes']) for a in audits]
    assert fingerprints == [776, 21]
    tests = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
    main_path = ROOT / 'tools/reference/creeper-frame-installation-evidence.json'
    main = json.loads(main_path.read_text())
    assert len(main['installedArtifacts']) == 106
    for name, hash_ in main['installedArtifacts'].items():
        assert sha(Path(main['runtimeRoot']) / name) == hash_, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 184 and status['completedClasses'] == []
    for path in (ROOT / 'data/Sanctuary').rglob('*'):
        if path.is_file():
            assert path.read_bytes() == subprocess.check_output(['git', 'show', PREDECESSOR + ':' + str(path.relative_to(ROOT))], cwd=ROOT), path
    stabling_path = ROOT / 'tools/reference/revive-native-stabling-preflight.json'
    stabling = json.loads(stabling_path.read_text())
    assert stabling['runtimeVerified'] is False and stabling['nativePatchCount'] == 68
    trainer = native / 'Projects/UOContent/Mobiles/Vendors/NPC/AnimalTrainer.cs'
    assert 'else if (pet.Summoned || ClassCombatHooks.PersistentMinion?.Invoke(pet) == true)' in trainer.read_text()
    artifacts.append(trainer)
    assert sha(ROOT / stabling['nativeSource']) == stabling['sha256']
    artifacts += [main_path, stabling_path, ROOT / stabling['nativeSource'], ROOT / 'tools/prepare_revive_stabling_native.py',
        ROOT / 'tools/prepare_revive_retention_native.py', ROOT / 'tests/check_revive_stabling_runtime.py',
        ROOT / 'tests/check_revive_native_stabling.py', ROOT / 'tools/build_skill_roadmap.py',
        ROOT / 'tools/build_property_integration_backlog.py', ROOT / 'tools/reference/item-property-integration-backlog.json']
    for name in ['pre-build-inputs.json', 'native-pre-build-inputs.json', 'native-prepare.log', 'stats-inputs.json',
                 'stats-native-checks.log', 'run.log', 'stabling-inputs.json', 'stabling-native-checks.log', 'stabling-run.log',
                 'revive-native-control-fixtures.json', 'python-tests.log']:
        artifacts.append(STAGE / name)
    artifacts += [FULL / n for n in ['class-inputs.json', 'class-native-checks.log', 'run.log']]
    artifacts += [p for p in (STAGE / 'diagnostics').rglob('*') if p.is_file()]
    result = dict(referenceVersion='3.3.93847', component='Native trainer stabling protection for class minions',
        installed=False, handlerEnabled=False, automaticRetentionEnabled=False, goalComplete=False,
        handlers=184, completedClasses=[], processMemoryRead=False,
        verified=dict(retainedMarkers=332, retainedWorldBoots=3, retainedActualReloads=2,
            isolatedNativeControlMarkers=15, isolatedControlWorldBoots=3, isolatedControlActualReloads=2,
            forcedNativeHourlyChecks=36, globalTimeAdvanced=False, ordinaryPetReleaseCases=3,
            ordinaryPetMenusAndChanceRetained=True, classPetMenusAndSpeechSuppressed=True,
            naturalOriginalGuardFollowCases=3, savedOriginalActor=1, nativePatchCount=69,
            isolatedNativeCleanBuild=True, serializedFieldsAdded=False, nativeLoyaltyReleaseFixed=True,
            manualStablingGuardImplemented=True, classStableTargetRejections=3, ordinaryStableClaimCases=3, ordinaryStableFee=30, completeApprovedLifetimeVerified=False,
            fullClassMarkers=1184, fullClassBoots=3, fullClassActualReloads=2, fullClassSaves=35,
            preBuildAndNativeSourceDataMatch=True, auditSymlinkPathCanonicalized=True, pythonTests=71, unchangedNamedDefinitions=573,
            inputFingerprints=fingerprints, unchangedMainInstallationArtifacts=106),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt='tools/reference/revive-native-control-evidence.json'),
        inputInventory=inputs, isolatedControlInputInventory=clock, fullClassInputInventory=complete,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},
        established=[
            'Native patch69 adds the existing PersistentMinion predicate to AnimalTrainer.EndStable rejection before gold or ownership mutation. Three actual native stable targets reject nonsummoned class originals without changing gold,owner,location,roster,followers or active metadata; delayed checks confirm no orphan deletion. Ordinary high-taming pets pay30gold,enter the native stable roster and are claimed with original ownership/follower accounting and no second fee.',
            'Three isolated native worlds force twelve hourly LoyaltyTimer boundaries each without advancing global time. Low-loyalty class originals keep their owner/order/loyalty, while an ordinary high-taming pet keeps native menus/control chance and releases through the unchanged low-loyalty transaction. Class pet menus and ordinary owner pet speech are suppressed. The original native guard AI naturally follows its owner in all three cases.',
            'One saved original keeps identity, AI/taming data, loyalty and non-commandable behavior across both reloads. Forced clock checks are intentionally isolated from the retained ordinary NPC fixtures. The same products/data retain all332focused markers and pass1184full-class markers, including named equipment, in their own three-boot/two-reload worlds. All69native patches replay and native/content/integration clean rebuilds have zero warnings/errors.'],
        remaining=[
            'The native stabling gap is closed for the tested target transaction. Source cast/mode/stat/AI/input composition and other unsupported ownership/death contexts still prevent full Revive and lifetime acceptance; the earlier source preflight remains historical evidence.',
            'Source geometry/mode entry, FP256 HP and current passive/sunder/event/stat transfer, dynamic eligibility/AI state, complete equipment/procs, native ability clocks, facet/respec and learned input/payment remain unfinished. No full skill/class completion or installation is claimed. Effects/audio and talent/hotkey icons remain separate placeholders.'])
    output = ROOT / 'tools/reference/revive-native-stabling-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Native stabling:332retained+15isolated/1184full/71Python;class target protection and ordinary30gold stable/claim verified;learned Revive unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
