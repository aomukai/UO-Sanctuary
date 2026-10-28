#!/usr/bin/env python3
"""Bind prepared Rabies packet acceptance, separately from the installed Hunger build."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from record_shape_restrictions import frozen_receipt

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/druid-rabies-packets'
FROZEN = '1fb5518'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inputs = json.loads((STAGE / 'audit-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for directory in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / directory).glob('*.cs')}
    assert {path for path in inputs['sourceInputs'] if path.endswith('.cs')} == expected
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    artifacts = [STAGE / name for name in ['audit-inputs.json', 'audit-native-checks.log', 'run.log', 'python-tests.log']]
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == inputs['artifacts']['Assemblies/' + name]
        log = STAGE / (kind + '-build.log')
        assert all(text in log.read_text() for text in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [log, STAGE / kind / name]
    log = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 3
    for command in ['druid-rabies-packet', 'named-gear', 'first-loop-catalogue']:
        assert log.count(command + '-check: passed') == 1
    assert '12240 full-source damage comparisons / 1296 source infection windows' in log
    assert log.count('Shutting down') == 1 and 'Writing world save snapshot done' not in log
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    for name in ['bite', 'factory', 'spread', 'router']:
        path = ROOT / f'tools/reference/druid-rabies-{name}-engine-evidence.json'
        proof = json.loads(path.read_text())
        assert proof['sourcePreparation'] and not proof['nativeAccepted'] and not proof['currentExecutableParity']
        fixture = ROOT / proof['fixture']['path']
        assert sha(fixture) == proof['fixture']['sha256']
        artifacts += [path, fixture]
        if name == 'factory':
            assert proof['damageContexts'] == 12240 and proof['factoryContexts'] == 5400
            for name, digest in proof['artifacts'].items():
                assert sha(ROOT / name) == digest
                artifacts.append(ROOT / name)
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for name, digest in audit['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
        audits.append(audit)
        artifacts += [path, ROOT / 'tools/reference' / path.name]
    previous = json.loads(subprocess.check_output(['git', 'show', FROZEN + ':tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == previous['Definitions']
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 179 and sum(x['characterClass'] == 'Druid' for x in handlers) == 23 and not status['completedClasses']
    assert next(x for x in status['skills'] if x['talentId'] == 'druid.rabies')['implementation'] != 'handler-present'
    frozen_path = 'tools/reference/druid-hunger-native-evidence.json'
    frozen_raw, frozen = frozen_receipt(FROZEN, frozen_path)
    assert frozen['verified']['fullClassMarkers'] == 1184
    artifacts += [ROOT / path for path in ['src/Sanctuary.Content/DruidRabiesPackets.cs', 'tests/Sanctuary.Integration/DruidRabiesPacketChecks.cs', 'tests/check_druid_rabies_packets.py', 'tests/export_runtime_audits.py', 'tools/probe_druid_rabies_bite.py', 'tools/probe_druid_rabies_factory.py', 'tools/record_druid_rabies_packets.py']]
    result = dict(referenceVersion='3.3.93847', installed=False, rabiesHandlerEnabled=False, handlers=179, druidHandlers=23, completedClasses=[], goalComplete=False,
        verified=dict(damageComparisons=12240, infectionWindowComparisons=1296, liveCasterCapture=True, passMarkers=3, boots=1, actualReloads=0, pythonTests=70, namedDefinitions=573, inputFingerprints=[len(x['InputHashes']) for x in audits]),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        frozenInstalledPredecessor=dict(commit=FROZEN, receipt=frozen_path, sha256=hashlib.sha256(frozen_raw).hexdigest(), fullClassMarkers=1184, installationReceipt='tools/reference/druid-hunger-installation-evidence.json'),
        established=[
            'Prepared Rabies poison reuses the existing source-zero Poison Javelin helper.12240native comparisons match complete older creator/skill-getter/stat-writer outputs across every effective rank,hard Creeper points,positive/negative mastery,owner stat noise and origin presence. No weapon,item poison/drain,critical or double mastery enters the packet.',
            '1296source hit53windows verify absolute-deadline remaining poison,inclusive10frame lower bound and original skill-length upper bound. Invalid/overflowing native clock inputs refuse. This helper does not conflate poison cure with removal of the independent infection marker.',
            'Actual trained Druid/hard Creeper/all-skill helm/weapon contexts verify retained Rabies rank with live hard synergy,soft synergy exclusion,immutable older packets,no weapon requirement and no mana/charge mutation. One disposable native boot also checks named/affix consumers. These are prepared packet checks,not casting or spreading gameplay acceptance.',
        ], remaining=[
            'Connect initial start57/do121,two separate ordinary/skill-poison packets and independent saved marker/emitter/child movement/contact to true input. Current payment/rank1quirk,initial miss/block,extra transformed AR,range-loss attachment,lethal/reentrant ordering and capture phases remain explicitly uncertain.',
            'Full native spread/poison cure/replacement/ownership/host death/logout/facet,resource/cancellation,input and save/reload acceptance remain unfinished. No new native Rabies handler,full-class regression,installation or final effects/icons is claimed. Earlier1184full markers belong to the frozen Hunger package only.',
        ])
    path = ROOT / 'tools/reference/druid-rabies-packets-native-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Rabies prepared packets:12240damage/1296deadline comparisons,3markers/1boot/70Python;179handlers/Druid23,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
