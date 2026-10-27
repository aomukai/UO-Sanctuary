#!/usr/bin/env python3
"""Bind header-corrected unused form modes; verify the frozen full world predecessor."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'druid-form-modes'
FROZEN = '981b2c9'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def historical(name):
    return subprocess.check_output(['git', 'show', FROZEN + ':' + name], cwd=ROOT)


def record(check=False):
    inputs = json.loads((STAGE / 'audit-inputs.json').read_text())
    expected = {str(path.relative_to(ROOT)) for folder in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for path in (ROOT / folder).glob('*.cs')}
    assert {name for name in inputs['sourceInputs'] if name.endswith('.cs')} == expected
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    artifacts = [STAGE / name for name in ['audit-inputs.json', 'audit-native-checks.log', 'modes-run.log', 'python-tests.log']]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log');text = path.read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        assembly = STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')
        assert sha(assembly) == inputs['artifacts']['Assemblies/' + assembly.name]
        artifacts += [path, assembly]
    log = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 6
    assert log.count('Shutting down') == 1 and 'Writing world save snapshot done' not in log
    assert '350native player lifecycle/stat contexts match complete source' in log
    for name in ['druid-form-model', 'grizzly-ai', 'grizzly-stats', 'dire-wolf-stats', 'named-gear', 'first-loop-catalogue']:
        assert log.count(name + '-check: passed') == 1
    source_path = ROOT / 'tools/reference/druid-form-modes-engine-evidence.json'
    source = json.loads(source_path.read_text())
    assert source['header']['playerCast'] == 10 and source['header']['playerAttack1'] == 7
    assert source['contexts'] == 1000 and source['supportedPlayerContexts'] == 350
    assert sha(BASE / 'd2moo-pinned-source' / source['header']['path']) == source['header']['sha256']
    assert sha(BASE / 'druid-form-modes-source-probe.cpp') == source['generatedSourceSha256']
    assert sha(BASE / 'druid-form-modes-source-probe.json') == source['fixture']['sha256']
    assert sha(BASE / 'druid-form-modes-sanitizer.log') == source['sanitizerLogSha256']
    assert (BASE / 'druid-form-modes-sanitizer.log').read_text() == ''
    artifacts += [source_path, BASE / 'd2moo-pinned-source' / source['header']['path']]
    artifacts += [BASE / name for name in ['druid-form-modes-source-probe.cpp', 'druid-form-modes-source-probe.json', 'druid-form-modes-sanitizer.log']]
    frozen_path = 'tools/reference/druid-form-world-native-evidence.json'
    raw = historical(frozen_path);frozen = json.loads(raw)
    for name, digest in frozen['artifacts'].items():
        value = (ROOT / name).read_bytes() if name.startswith('.local/') else historical(name)
        assert hashlib.sha256(value).hexdigest() == digest, name
    for name, digest in frozen['inputInventory']['sourceInputs'].items():
        value = (ROOT / name).read_bytes() if name.startswith('.local/') else historical(name)
        assert hashlib.sha256(value).hexdigest() == digest, name
    assert frozen['verified']['passMarkers'] == 58 and frozen['verified']['fullClassMarkers'] == 1184
    assert frozen['verified']['boots'] == 3 and frozen['verified']['actualReloads'] == 2
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name;audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for name, digest in audit['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
        audits.append(audit);artifacts += [path, ROOT / 'tools/reference' / path.name]
    baseline = json.loads(historical('tools/reference/named-item-runtime-audit.json'))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [row for row in status['skills'] if row['implementation'] == 'handler-present']
    assert len(handlers) == 172 and sum(row['characterClass'] == 'Druid' for row in handlers) == 16 and status['completedClasses'] == []
    artifacts += [ROOT / name for name in ['tools/record_druid_form_modes.py', 'tools/probe_druid_form_modes_reference.py', 'tests/check_druid_form_modes.py', 'tests/export_runtime_audits.py']]
    result = dict(referenceVersion='3.3.93847', installed=False, playerFormHandlersEnabled=False, handlers=172, druidHandlers=16, completedClasses=[], goalComplete=False,
        verified=dict(nativePlayerContexts=350, sourceContexts=1000, playerCastMode=10, sourceModeWriteEventsCompared=True,
            passMarkers=6, boots=1, actualReloads=0, pythonTests=70, namedDefinitions=573, inputFingerprints=[len(audit['InputHashes']) for audit in audits]),
        frozenWorldAcceptance=dict(commit=FROZEN, receipt=frozen_path, sha256=hashlib.sha256(raw).hexdigest(), package='.local/class-skills/druid-form-world', passMarkers=58, fullClassMarkers=1184, boots=3, actualReloads=2),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        established=[
            'The unused pure native helper now uses Player.hCAST10for entry,unshift and expiry;7is ATTACK1. No UO animation number is inferred from that source skill-mode value. All350supported player state/stat/expiry controls match the1000-context header-corrected complete source probe.',
            'Mode-write presence is compared against source events independently of equal initial/final numeric values. Missing skill avoids a write; allocation failures/refusals preserve that boundary. The saved world adapter consumes only the unchanged state/stat payload and does not yet drive this mode field.',
            'One disposable boot retains Grizzly AI/birth,Dire birth and real named/affix consumers;70Python tests pass and655/21fingerprints preserve573named-item decisions. No new saved-world or full-suite claim is made for this unused result-field correction.',
        ], remaining=[
            'The58focused/1184full/3boot/2reloadsaved-world acceptance belongs to frozen981b2c9and its exact immutable package. Main remains3c0c61bon the preserved Grizzly installation. Do not rebuild either accepted package in place.',
            'Next connect the separately prepared native shape restriction and independently written modern rate consumers,then transformed basic attack/AR/SrcDam and learned casting/payment/input. Current cross-form Param12behavior and ARruntime remain unobserved; neither requires a user respec or stalls unaffected work.',
            'Retain foundation-approved whole native maximum projection. Final40/TGbody/motion/transformation/contact/audio and separate talent/right/quick/passive icons remain pending. No class completion or current-executable equivalence is certified.',
        ])
    output = ROOT / 'tools/reference/druid-form-modes-native-evidence.json';rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Header-backed Druid CAST10:350native controls,6markers/1boot/no reloads,70Python tests;172handlers/Druid16,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
