#!/usr/bin/env python3
"""Bind original-NPC death identity and real socket replacement lifecycle evidence."""
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

STAGE = ROOT / '.local/class-skills/revive-native-lifecycle'
PREDECESSOR = '2cb31bb'


def sha(path):
    return digest(path.read_bytes())


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    check_snapshot(STAGE / 'pre-build-inputs.json', STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt(PREDECESSOR, 'tools/reference/revive-native-activation-evidence.json')
    assert prior['verified']['passMarkers'] == 321
    assert prior['verified']['combinedRegressionMarkers'] == 1184
    # This increment adds integration coverage without modifying production C#.
    production = lambda rows: {n:h for n,h in rows.items() if n.startswith(('src/', 'shared/'))}
    assert production(inputs['sourceInputs']) == production(prior['inputInventory']['sourceInputs'])
    artifacts = [Path(__file__).resolve()]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    native_ref = json.loads((STAGE / 'native-reference.json').read_text())
    assert native_ref['frozenCommit'] == PREDECESSOR and prior['verified']['reusedNativePatchCount'] == 67
    for name, hash_ in native_ref['artifacts'].items():
        path = Path(native_ref['nativeDistribution']) / name
        assert sha(path) == hash_ == inputs['artifacts'][name] == prior['inputInventory']['artifacts'][name]
        artifacts.append(path)

    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 332
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert 'Original NPC death identity and actual socket replacement lifecycle pass3boots/2reloads;fixture login bootstrap is not normal packet login;source stats/AI and learned Revive remain gated.' in (STAGE / 'run.log').read_text()
    pattern = r'\] ([A-Za-z0-9-]+): passed'
    before = Counter(re.findall(pattern, (ROOT / '.local/class-skills/revive-native-activation/stats-native-checks.log').read_text()))
    after = Counter(re.findall(pattern, log))
    assert sum(before.values()) == 321 and not before - after
    assert after - before == Counter({'revive-lifecycle-check': 3, 'revive-lifecycle-reconnect-check': 3,
        'revive-lifecycle-result': 3, 'revive-lifecycle-reload-check': 2})
    assert log.count('revive-lifecycle: disconnect-ready') == log.count('revive-lifecycle: disconnected') == 3
    assert log.count('Revive lifecycle:actual original death creates a distinct owned corpse while consumed loot stays separate;native socket disposal parks/protects and cancels queued breath;new socket plus fixture login bootstrap returns the same original;no normal login-packet or source-stat/AI completeness claimed') == 3
    saved = json.loads((STAGE / 'revive-lifecycle-death-fixtures.json').read_text())
    assert saved['Actor'] > 0 and saved['OldCorpse'] != saved['NewCorpse']
    assert saved['Old']['Corpse'] == saved['OldCorpse'] and saved['New']['Corpse'] == saved['NewCorpse']
    assert saved['Old']['UnitReactivated'] and saved['Old']['NoSelect'] and not saved['New']['UnitReactivated'] and saved['New']['Owned']
    assert saved['Old']['BaseLife']['OriginalMonsterSerial'] == saved['New']['BaseLife']['OriginalMonsterSerial'] == saved['Actor']
    assert len(saved['OldLoot']) == len(saved['NewLoot']) == 1 and not set(saved['OldLoot']) & set(saved['NewLoot'])

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
    assert fingerprints == [774, 21]
    tests = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
    main_path = ROOT / 'tools/reference/creeper-frame-installation-evidence.json'
    main = json.loads(main_path.read_text())
    assert len(main['installedArtifacts']) == 106
    for name, hash_ in main['installedArtifacts'].items():
        assert sha(Path(main['runtimeRoot']) / name) == hash_, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 184 and status['completedClasses'] == []
    assert next(s for s in status['skills'] if s['talentId'] == 'necromancer.revive')['implementation'] != 'handler-present'
    for path in (ROOT / 'data/Sanctuary').rglob('*'):
        if path.is_file():
            assert path.read_bytes() == subprocess.check_output(['git', 'show', PREDECESSOR + ':' + str(path.relative_to(ROOT))], cwd=ROOT), path
    diagnostic = STAGE / 'diagnostics'
    assert 'NullReferenceException' in (diagnostic / 'attempt1/stats-native-checks.log').read_text()
    assert 'AllFollowers to null' in (diagnostic / 'attempt1/reason.txt').read_text()
    assert 'revive-lifecycle-result: passed' in (diagnostic / 'attempt2/stats-native-checks.log').read_text()
    assert 'Server did not reach' in (diagnostic / 'attempt2/run.log').read_text()
    for kind in ['Content', 'Integration']:
        assert sha(diagnostic / 'attempt2' / ('Sanctuary.' + kind + '.dll')) == sha(STAGE / kind.lower() / ('Sanctuary.' + kind + '.dll'))
    assert sha(diagnostic / 'attempt2/ReviveLifecycleChecks.cs') == sha(ROOT / 'tests/Sanctuary.Integration/ReviveLifecycleChecks.cs')
    artifacts += [p for p in diagnostic.rglob('*') if p.is_file()]
    control_path = ROOT / 'tools/reference/revive-native-control-preflight.json'
    control = json.loads(control_path.read_text())
    assert control['runtimeVerified'] is False and control['nativePatchCount'] == 67
    control_source = ROOT / control['nativeSource']
    assert sha(control_source) == control['sha256']
    existing_source = ROOT / control['existingClassPattern']['source']
    assert sha(existing_source) == control['existingClassPattern']['sha256']
    artifacts += [control_path, control_source, existing_source]
    for name in ['pre-build-inputs.json', 'stats-inputs.json', 'stats-native-checks.log', 'run.log', 'native-reference.json',
                 'python-tests.log', 'revive-lifecycle-death-fixtures.json']:
        artifacts.append(STAGE / name)
    artifacts += [main_path, ROOT / 'tests/check_revive_native_lifecycle.py', ROOT / 'tests/check_creeper_stats.py',
        ROOT / 'tools/build_skill_roadmap.py', ROOT / 'tools/build_property_integration_backlog.py',
        ROOT / 'tools/reference/item-property-integration-backlog.json']
    result = dict(referenceVersion='3.3.93847', component='Original Revive death identity and real socket replacement lifecycle',
        installed=False, handlerEnabled=False, automaticRetentionEnabled=False, productionSourceUnchanged=True,
        goalComplete=False, handlers=184, completedClasses=[], processMemoryRead=False,
        verified=dict(passMarkers=332, retainedMarkers=321, worldBoots=3, actualWorldReloads=2,
            activeOriginalDeaths=3, savedOldAndNewCorpsePair=1, savedPairReloads=2, oldCorpseCleanupCases=2,
            actualSocketClosures=3, replacementSocketConnections=3, nativeDisconnectEvents=True,
            nativePlayerLoginEventWithFixtureBootstrap=True, normalClientLoginPacketsVerified=False,
            originalIdentityPreservedOnReconnect=True, parkedIncomingDamageRefused=True, queuedBreathCancelled=True,
            noAgeTimerAdded=True, nativeControlAndLoyaltyExemptionsImplemented=False,
            approvedLifetimeFullyVerified=False, reusedNativePatchCount=67, newNativeBuild=False,
            preBuildAndNativeSourceDataMatch=True, pythonTests=71, unchangedNamedDefinitions=573,
            inputFingerprints=fingerprints, unchangedMainInstallationArtifacts=106, newFullClassRun=False),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt='tools/reference/revive-native-activation-evidence.json'),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))},
        established=[
            'Three actual deaths of prepared revived original NPCs produce new owned native corpses while their consumed original loot containers remain. The old containers never regain their dead source identity. A saved old/new pair with independent loot and the original mobile GUID survives both reloads; the fresh owned corpse alone projects that GUID and cannot be retained as another wild Revive source.',
            'Two further pairs verify old-corpse deletion after the subsequent death: removing the consumed old container leaves the new death identity intact, and deleting the final new corpse removes it. Native death removes the active Revive row and follower; the native follower collection becomes null when its last member is removed.',
            'Each boot closes an actual loopback client TCP socket and waits for native disposal and player NetState detachment. Common disconnect events park and protect the same original and cancel queued dragon breath. A different real TCP connection is attached to the same account/player through fixture bootstrap, then the native PlayerLoginEvent resumes the same NPC. This is not a normal authentication/character-selection packet test.',
            'Production C# and runtime data are unchanged from the accepted predecessor. Integration helpers expose existing fixture setup, and the retained driver adds an optional actual socket replacement lane. Both assemblies are clean rebuilt and all321previous focused markers are retained. Two rejected attempts are preserved with source/products/logs: the initial fixture dereferenced the native null follower set, then the Python driver consumed the synchronous success result while seeking the later acknowledgement. The final driver collects both markers in either order; that correction changes no C# or products. No fresh full-class run or installation is claimed.'],
        remaining=[
            'Pinned native source still applies hourly loyalty decay/release and taming/lore command chance to the original controlled NPC. PersistentMinion is not checked by those paths; Tamable=false does not exempt Commandable creatures. This can violate the approved lifetime and requires native exclusions with ordinary-pet negative controls before learned enablement. Source review is recorded separately; no long-duration runtime proof is claimed.',
            'Compose actual source start/release geometry, mode entry, FP256 HP and current passive/sunder/event/stat transfer with the native adapter. Dynamic eligibility/AI state, complete equipment effects/procs, native ability clock restore/aging and special AI scheduling remain unfinished.',
            'Normal client login packets, cross-difficulty replanning, respec/rank changes and unsupported native spawn/guild/waypoint/home/death contexts still need separate implementation or verification. Learned Revive and automatic capture remain gated. No class is complete; effects/audio and talent/hotkey icons remain separate placeholders.'])
    output = ROOT / 'tools/reference/revive-native-lifecycle-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Revive native lifecycle:332 focused/71 Python;3 real socket replacements and original deaths,2saved-pair reloads;learned casting unfinished.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
