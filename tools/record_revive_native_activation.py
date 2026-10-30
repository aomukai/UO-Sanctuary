#!/usr/bin/env python3
"""Bind prepared original-NPC activation to focused and full native regression evidence."""
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

STAGE = ROOT / '.local/class-skills/revive-native-activation'
PREDECESSOR = 'bd58df3'


def sha(path):
    return digest(path.read_bytes())


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    check_snapshot(STAGE / 'pre-build-inputs.json', STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt(PREDECESSOR, 'tools/reference/revive-before-death-native-evidence.json')
    assert prior['verified']['passMarkers'] == 312
    artifacts = [Path(__file__).resolve()]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]

    # Reuse precisely the preceding accepted 67-patch binaries; no new native build.
    reference = json.loads((STAGE / 'native-reference.json').read_text())
    assert reference['frozenCommit'] == PREDECESSOR and prior['verified']['nativePatchCount'] == 67
    native = Path(reference['nativeDistribution'])
    for name, hash_ in reference['artifacts'].items():
        assert sha(native / name) == hash_ == inputs['artifacts'][name] == prior['inputInventory']['artifacts'][name]
        artifacts.append(native / name)

    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 321
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert 'Prepared original-native activation and shared lifecycle pass3boots/2reloads;source planning and learned Revive remain gated.' in (STAGE / 'run.log').read_text()
    pattern = r'\] ([A-Za-z0-9-]+): passed'
    before = Counter(re.findall(pattern, (ROOT / '.local/class-skills/revive-predeath-state/stats-native-checks.log').read_text()))
    after = Counter(re.findall(pattern, log))
    assert sum(before.values()) == 312 and not before - after
    assert after - before == Counter({'revive-native-activation-check': 1,
        'revive-native-activation-reload-check': 2, 'revive-native-activation-live-check': 3,
        'revive-native-activation-live-result': 3})
    assert log.count('Prepared native Revive activation:same original actors/types/serials,explicit integral setup,native owner/zero slots,skill-cap FIFO,corpse lease detachment,loot rights and permanent source-identity retirement pass;shared offline/save lifecycle and atomic invalid state checks retained;source stat/passive/FP256/AI/facet/input composition and learned spell remain gated') == 3
    saved = json.loads((STAGE / 'revive-native-activation-fixtures.json').read_text())
    rows = saved['Actors']
    assert len(rows) == 2 and len({r['Actor'] for r in rows}) == 2
    assert {r['Original']['RuntimeType'] for r in rows} == {'Server.Mobiles.Dragon', 'Server.Mobiles.Skeleton'}
    assert {r['Order'] for r in rows} == {2, 3} and len(saved['Loot']) == 1
    for row in rows:
        assert row['Owner'] == saved['Owner'] and row['Original']['Actor'] == row['Actor']
        assert row['Setup']['Rank'] == 2 and row['Setup']['Life'] == row['Setup']['MaximumLife'] - 17
        assert row['Setup']['Mana'] == 4 and row['Setup']['Stamina'] == 10
        assert row['Original']['BeforeDeathIssue'] is None
        assert json.loads(row['Original']['BeforeDeathJson'])['Creature']['RuntimeType'] == row['Original']['RuntimeType']

    complete = inventory.validate_inventory(STAGE / 'class-inputs.json')
    check_snapshot(STAGE / 'pre-build-inputs.json', STAGE / 'class-inputs.json')
    assert complete['artifacts'] == inputs['artifacts'] and complete['copiedData'] == inputs['copiedData']
    assert {n:h for n,h in complete['sourceInputs'].items() if n.endswith('.cs')} == {n:h for n,h in inputs['sourceInputs'].items() if n.endswith('.cs')}
    full = (STAGE / 'class-native-checks.log').read_text()
    assert ': failed' not in full and ' ERR]' not in full
    assert full.count('Shutting down') == 3 and full.count('Writing world save snapshot done') == 35
    assert 'including two save/reload cycles.' in (STAGE / 'full-run.log').read_text()
    assert full.count('first-loop-catalogue-check: passed') == 1
    full_markers = full.count(': passed')
    assert full_markers == 1181
    named_inputs = json.loads((STAGE / 'named-inputs.json').read_text())
    assert named_inputs['artifacts'] == complete['artifacts'] and named_inputs['copiedData'] == complete['copiedData']
    named = (STAGE / 'named-native-checks.log').read_text()
    assert ': failed' not in named and ' ERR]' not in named
    assert named.count(': passed') == named.count('Shutting down') == named.count('Writing world save snapshot done') == 3
    assert 'Named candidate:3markers/3boots/2actual reloads' in (STAGE / 'named-run.log').read_text()
    named_markers = Counter(re.findall(pattern, named))
    assert named_markers == Counter({'named-gear-check': 1, 'named-gear-reload-check': 2})
    _, historical_full = frozen_receipt('16ce299', 'tools/reference/monster-ordered-release-native-evidence.json')
    assert historical_full['verified']['fullClassMarkers'] == 1184
    historical_log = ROOT / '.local/class-skills/monster-ordered-release/class-native-checks.log'
    historical_markers = Counter(re.findall(pattern, historical_log.read_text()))
    assert Counter(re.findall(pattern, full)) + named_markers == historical_markers
    artifacts += [historical_log, ROOT / 'tests/check_named_candidate.py']

    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        audit = json.loads((STAGE / name).read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit == json.loads((STAGE / ('full-' + name)).read_text())
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name_, hash_ in audit['InputHashes'].items():
            assert sha(ROOT / name_) == hash_, name_
        audits.append(audit)
        artifacts += [STAGE / name, STAGE / ('full-' + name), ROOT / 'tools/reference' / name]
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
    assert 'error CS1003' in (diagnostic / 'compile1/integration-build.log').read_text()
    assert 'Direct queued incoming damage' in (diagnostic / 'preflight3/reason.txt').read_text() or 'Native direct incoming damage' in (diagnostic / 'preflight3/reason.txt').read_text()
    assert 'Stale named runtime evidence' in (diagnostic / 'python-stale-audit/python-tests.log').read_text()
    artifacts += [p for p in diagnostic.rglob('*') if p.is_file()]
    for name in ['pre-build-inputs.json', 'stats-inputs.json', 'stats-native-checks.log', 'run.log', 'native-reference.json',
                 'python-tests.log', 'revive-native-activation-fixtures.json', 'class-inputs.json', 'class-native-checks.log', 'full-run.log',
                 'named-inputs.json', 'named-native-checks.log', 'named-run.log']:
        artifacts.append(STAGE / name)
    artifacts += [main_path, ROOT / 'tests/check_revive_native_activation.py', ROOT / 'tools/build_skill_roadmap.py',
        ROOT / 'tools/build_property_integration_backlog.py', ROOT / 'tools/reference/item-property-integration-backlog.json',
        ROOT / 'tools/reference/revive-equipment-deviation.json']
    result = dict(referenceVersion='3.3.93847', component='Prepared native activation of the original retained NPC',
        installed=False, handlerEnabled=False, automaticRetentionEnabled=False, activePreparedNativeActorImplemented=True,
        sourceReviveCompositionComplete=False, goalComplete=False, handlers=184, completedClasses=[], processMemoryRead=False,
        verified=dict(passMarkers=321, retainedMarkers=312, worldBoots=3, actualWorldReloads=2,
            savedActiveOriginals=2, connectedLifecycleFixtures=3, invalidRestoresPerBoot=12, refusedPreflightCases=3,
            originalIdentityPreserved=True, skillCapFifo=True, nativeFollowerSlots=0, corpseLootPreserved=True,
            corpseLeaseDetachedBeforeCleanup=True, consumedCorpseUnitIdentityRetired=True,
            parkedIncomingDamageRefused=True, parkedQueuedBreathRefused=True, classHistoryParkResume=True,
            ownerDeathDeletesActor=True, expirationMetadataAbsent=True, actualSocketReconnectVerified=False,
            crossDifficultyRescaleImplemented=False, reusedNativePatchCount=67, newNativeBuild=False,
            preBuildAndNativeSourceDataMatch=True, pythonTests=71, unchangedNamedDefinitions=573,
            inputFingerprints=fingerprints, unchangedMainInstallationArtifacts=106,
            fullClassPassMarkers=full_markers, fullClassWorldBoots=3, fullClassActualReloads=2,
            fullClassSaves=full.count('Writing world save snapshot done'), supplementalNamedMarkers=3,
            supplementalNamedBoots=3, supplementalNamedActualReloads=2, combinedRegressionMarkers=1184),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt='tools/reference/revive-before-death-native-evidence.json'),
        inputInventory=inputs, fullClassInputInventory=complete, supplementalNamedInputInventory=named_inputs,
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))},
        established=[
            'Prepared activation revives the same actual retained native NPC under its original serial, concrete type and AI. Explicit integral setup is deliberately distinct from the unfinished D2 FP256/stat/passive planner. Native ownership uses zero follower slots and rank-limited FIFO replacement, not a generic replacement pet or an age timer.',
            'The storage lease is detached before consuming the corpse. Original loot and its loot rights remain intact; taking the final loot can delete the container without deleting its active original. The consumed corpse permanently stops projecting a dead source unit, including after both reloads. A subsequent death producing another corpse is not covered by this fixture.',
            'A behavior provider integrates existing NPC types with shared persistent-minion parking/resume, class-history handling and save restoration. Both saved originals stay owned and alive through two reloads. Three connected fixtures test shared parking, incoming damage immunity, cancellation of queued dragon breath, resuming, class changes and owner death. They do not claim an actual socket disconnect/reconnect test.',
            'Three preflight refusals leave storage, loot and follower accounting unchanged. Twelve malformed-state controls per boot refuse atomically. Unsupported spawn/guild/waypoint/home transfer is explicitly refused. Native expiration metadata is absent; no advancing-clock expiry simulation is claimed.',
            'Focused and broader class suites use identical source/data/products, with fresh matching audits and unchanged named-item decisions. The full runner omitted its optional named-equipment switch;1181core markers and3supplemental named markers pass in separate three-boot/two-reload worlds. Their combined marker multiset exactly retains the historical1184checks. No single1184marker world is claimed. The preceding accepted67-patch native binaries are reused exactly; content/integration are clean rebuilt. Compiler and unexecuted preflight candidates are preserved, as is a Python readiness failure caused by publishing audits after the first test attempt.'],
        remaining=[
            'Compose source start/release with native destination geometry, mode setup, FP256 base HP, current passives/sunder/events, complete eligibility and dynamic AI-state production. No learned cast, automatic retention or mana/input integration is enabled.',
            'Apply independent equipment effects through a complete NPC stat/proc/event path. Archived rolls and cooldown data are not active effects; native clock restore/aging and custom timers remain unresolved.',
            'Cross-difficulty stat replanning is unfinished and currently parks originals rather than guessing. Actual disconnect/reconnect, respec/rank changes, native original death/new-corpse coexistence and full special-AI scheduling need further checks. Final effects/audio and talent/hotkey icons remain separate placeholders.'])
    output = ROOT / 'tools/reference/revive-native-activation-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print(f'Prepared original-NPC activation:321 focused/{full_markers} core+3 supplemental named native/71 Python;learned Revive remains gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
