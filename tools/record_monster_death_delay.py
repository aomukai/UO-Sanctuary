#!/usr/bin/env python3
"""Bind signal-driven NPC death completion, persistence and retained acceptance."""
import argparse
import hashlib
import json
import re
import subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/monster-death-delay-native'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    _, prior = frozen_receipt('8c168e8', 'tools/reference/monster-return-native-evidence.json')
    assert prior['verified']['passMarkers'] == 194 and prior['verified']['fullClassMarkers'] is None
    artifacts = []
    for kind in ['content', 'integration']:
        p = STAGE / (kind + '-build.log')
        assert all(s in p.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [p, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    audit_inputs = inventory.validate_inventory(STAGE / 'audit-inputs.json')
    assert audit_inputs['artifacts'] == inputs['artifacts'] and audit_inputs['copiedData'] == inputs['copiedData']
    audit_log = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log
    assert audit_log.count(': passed') == 2 and audit_log.count('Shutting down') == 1
    artifacts += [STAGE / n for n in ['audit-inputs.json', 'audit-native-checks.log', 'audit-run.log']]
    artifacts.append(ROOT / 'tests/export_runtime_audits.py')
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 198
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    marker = re.compile(r'\] ([A-Za-z0-9-]+): passed')
    before = Counter(marker.findall((ROOT / '.local/class-skills/monster-return-native/stats-native-checks.log').read_text()))
    after = Counter(marker.findall(log))
    assert sum(before.values()) == 194 and not before - after
    assert after - before == Counter({'monster-death-delay-check': 1, 'monster-death-delay-result': 1, 'monster-death-delay-reload-check': 2})
    assert log.count('Deferred NPC death:9216complete-source two-call states/traces;actual lethal flags/source,zero/positive HP,missing-game,read-only worker and atomic restore pass') == 1
    assert log.count('Deferred NPC death:actual saved batch completes on real clear signal,including healed/missing-attacker/missing-game;no old damage replay or timer') == 2
    saved = json.loads((STAGE / 'focused-fixtures/monster-death-delay-fixtures.json').read_text())
    assert saved['Next'] == 2 and len(saved['Batches']) == 2
    ids = []
    for batch in saved['Batches']:
        assert [s['Kind'] for s in batch] == ['present', 'missing', 'healed', 'no-game']
        for s in batch:
            ids.append(s['Actor'])
            flags = s['Flags']
            assert flags['Actor'] == s['Actor'] and flags['DeathDelay']
            assert flags['Uninterruptible'] == (s['Kind'] != 'no-game')
            assert s['Life256'] == (2048 if s['Kind'] == 'healed' else 0)
            assert (flags['LastAttacker'] == saved['Attacker']) == (s['Kind'] != 'missing')
    assert len(set(ids)) == 8
    p = ROOT / 'tools/reference/monster-death-delay-source-evidence.json'
    source = json.loads(p.read_text()); artifacts.append(p)
    assert not source['currentExecutableParity'] and source['verified'] == dict(contexts=9216, returnRequests=576, directKillRequests=576, deathRequestsAtPositiveLife=388, townSuppressedReturn=176, missingGameRetainsDelay=2304)
    for group in ['inputs', 'artifacts']:
        for name, digest in source[group].items():
            assert sha(ROOT / name) == digest, name
            artifacts.append(ROOT / name)
    assert not (ROOT / '.local/class-skills/monster-death-delay/sanitizer.log').read_bytes()
    artifacts += list((ROOT / '.local/class-skills/monster-death-delay/diagnostics').glob('*.log'))
    diagnostics = STAGE / 'diagnostics/python-before-audit-refresh.log'
    assert 'Stale named runtime evidence: src/Sanctuary.Content/PreparedMonsterDamage.cs' in diagnostics.read_text()
    artifacts.append(diagnostics)
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        p = STAGE / name; audit = json.loads(p.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for name2, digest in audit['InputHashes'].items():
            assert sha(ROOT / name2) == digest, name2
        audits.append(audit); artifacts += [p, ROOT / 'tools/reference' / name]
    old = json.loads(subprocess.check_output(['git', 'show', '8c168e8:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == old['Definitions']
    assert [len(a['InputHashes']) for a in audits] == [718, 21]
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    main = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(main['installedArtifacts']) == 106
    for name, digest in main['installedArtifacts'].items():
        assert sha(Path(main['runtimeRoot']) / name) == digest, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 183 and status['completedClasses'] == []
    artifacts += [STAGE / n for n in ['stats-inputs.json', 'stats-native-checks.log', 'run.log', 'python-tests.log']]
    artifacts += list((STAGE / 'focused-fixtures').glob('*fixtures.json'))
    artifacts += [ROOT / n for n in ['tools/record_monster_death_delay.py', 'tests/check_monster_death_delay.py', 'tools/build_skill_roadmap.py', 'data/Sanctuary/Balance/class-skill-status.json', 'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json', 'tools/reference/creeper-frame-installation-evidence.json']]
    result = dict(referenceVersion='3.3.93847', component='Signal-driven deferred NPC death completion and saved mode flags', installed=False, handlerEnabled=False, currentExecutableParity=False, fullActorAccepted=False, goalComplete=False, handlers=183, warlockHandlers=1, completedClasses=[],
        verified=dict(passMarkers=198, retainedMarkers=194, worldBoots=3, actualWorldReloads=2, separateAuditBoots=1, separateAuditMarkers=2, completeSourceContexts=9216, callsPerSourceContext=2, savedPendingActors=8, completionBatches=2, savedCasesPerBatch=['present', 'missing', 'healed', 'no-game'], atomicRestore=True, readOnlyWorkerSerialization=True, formatVersion=0, persistenceId=187, noInventedTimeout=True, freshDeathOnlyPacket=True, nativeLastAttackerResolution=True, fullClassMarkers=None, pythonTests=70, unchangedNamedDefinitions=573, inputFingerprints=[718, 21], unchangedMainInstallationArtifacts=106),
        inputInventory=inputs, auditInputInventory=audit_inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))}, frozenPredecessor=dict(commit='8c168e8', receipt='tools/reference/monster-return-native-evidence.json'),
        established=[
            'Native policy matches all9216complete unchanged source contexts and exact two-call traces. Uninterruptible changes before game lookup;missing game leaves death pending and skips queued-think deletion. Clearing with a game clears delay before attacker resolution,then dispatches a fresh death-only return or requests direct kill without an attacker. Repeated clearing cannot replay pending death. Queued-think deletion follows completion.',
            'Qualified native NPC release records the actual last attacker. Explicit mode callers mark uninterruptible and delayed flags. A dedicated version0/persistence187 registry preserves flags and source serials,with atomic malformed/duplicate rejection and a read-only worker snapshot. Actual world lookup resolves the saved source;an absent/deleted source uses native uncredited death. This does not migrate unrelated ordinary damage consumers.',
            'Eight actual saved encounter actors form two batches. Each batch retains zero-HP pending death,pending death after healing,missing attacker,and a missing-game clear through actual world reload. An explicit completion signal then consumes native death once,without replaying the old damage,Stun or escaped scoped callback. No timeout completes death. The fixture supplies room/mode services and stops the real queued AI timer;full actor scheduling remains separate.',
            'All194predecessor focused markers remain exact. Both builds,70Python tests,573named-item eligibility decisions and106installed artifact hashes retain acceptance. This package has198focused checks/three boots/two reloads. A separate one-boot/two-check audit export uses identical DLLs/data;the focused launch used a misspelled named-audit environment variable,so it did not export that file. The earlier16ce299package separately retains1184full-class checks;no new full-class acceptance or installation is claimed.'
        ], remaining=[
            'Real actor mode acceptance,source animation/FHR clocks,block/knockback/network/AI actions,queued-think rearming and complete lifecycle/deletion cleanup remain unfinished. The registry is an explicit NPC consumer and does not infer when a real action becomes interruptible. Room/game/row/mode services are mandatory;current executable parity is not certified.',
            'Actual contact/proc/cold/damage-credit/drain/absorb/regeneration services,current helper/dispatcher linkage,extended passives/event16/CrushingBlow/BloodOath/sunder and new Frenzy/Cleave still gate a learned Goatman body/cast/AI. Existing ordinary consumers have not been globally migrated. No class is complete.',
            'Final NPC get-hit/Stun/Berserk/body effects/audio and separate talent/hotkey icons remain pending in their independent registers. Existing presentation is an identified placeholder;this internal completion policy creates no player hotkey identity.'
        ])
    path = ROOT / 'tools/reference/monster-death-delay-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Deferred NPC death:198focused/3boots/2reloads;9216source contexts and8saved actors;70Python;full actor remains gated.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
