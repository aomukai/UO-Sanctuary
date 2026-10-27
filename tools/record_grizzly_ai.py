#!/usr/bin/env python3
"""Bind prepared native Bear AI112 policy to its complete source comparisons."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/grizzly-ai'
BASE = ROOT / '.local/class-skills'
FROZEN = '55b6569'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def historical(name):
    return subprocess.check_output(['git', 'show', FROZEN + ':' + name], cwd=ROOT)


def record(check=False):
    inputs = json.loads((STAGE / 'audit-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for folder in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / folder).glob('*.cs')}
    assert {p for p in inputs['sourceInputs'] if p.endswith('.cs')} == expected
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    artifacts = [STAGE / name for name in ['audit-inputs.json', 'audit-native-checks.log', 'ai-run.log', 'python-tests.log']]
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == inputs['artifacts']['Assemblies/' + name]
        path = STAGE / (kind + '-build.log')
        build = path.read_text()
        assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
        artifacts += [path, STAGE / kind / name]
    log = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 5
    assert log.count('Shutting down') == 1 and 'Writing world save snapshot done' not in log
    for name in ['grizzly-ai', 'grizzly-stats', 'dire-wolf-stats', 'named-gear', 'first-loop-catalogue']:
        assert log.count(name + '-check: passed') == 1, name
    assert '2016native ordered-service/owner-memory comparisons match672complete source contexts' in log
    source_path = ROOT / 'tools/reference/grizzly-ai-source-evidence.json'
    source = json.loads(source_path.read_text())
    assert source['referenceVersion'] == '3.3.93847' and source['legacyCommit'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    fixture = ROOT / source['fixture']['path']
    generated = BASE / 'grizzly-ai-source-probe.cpp'
    sanitizer = BASE / 'grizzly-ai-sanitizer.log'
    assert source['fixture']['cases'] == 672 and sha(fixture) == source['fixture']['sha256']
    assert sha(generated) == source['generatedSourceSha256'] and sanitizer.read_text() == ''
    for name, digest in source['sourceHashes'].items():
        assert sha(BASE / 'd2moo-pinned-source' / name) == digest, name
    artifacts += [source_path, fixture, generated, sanitizer]
    # Earlier saved-consumer acceptance belongs to its frozen package/commit,
    # not to this one-boot pure-policy candidate.
    frozen_path = 'tools/reference/grizzly-stats-native-evidence.json'
    frozen = json.loads(historical(frozen_path))
    for name, digest in frozen['artifacts'].items():
        raw = (ROOT / name).read_bytes() if name.startswith('.local/') else historical(name)
        assert hashlib.sha256(raw).hexdigest() == digest, name
    assert frozen['verified']['passMarkers'] == 231 and frozen['verified']['actualReloads'] == 2
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for source, digest in audit['InputHashes'].items():
            assert sha(ROOT / source) == digest, source
        audits.append(audit)
        artifacts += [path, ROOT / 'tools/reference' / name]
    baseline = json.loads(historical('tools/reference/named-item-runtime-audit.json'))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 171 and sum(x['characterClass'] == 'Druid' for x in handlers) == 15 and status['completedClasses'] == []
    assert next(x for x in status['skills'] if x['talentId'] == 'druid.summongrizzly')['implementation'] != 'handler-present'
    artifacts += [ROOT / name for name in ['tools/record_grizzly_ai.py', 'tools/probe_grizzly_ai_reference.py', 'tests/check_grizzly_ai.py', 'tests/export_runtime_audits.py']]
    result = dict(referenceVersion='3.3.93847', installed=False, handlerEnabled=False, handlers=171, druidHandlers=15, completedClasses=[], goalComplete=False,
        verified=dict(nativeAiComparisons=2016, sourceContexts=672, difficultyColumns=3, nativeBirthComparisons=55152, nativeOrdinaryModeComparisons=6144,
            passMarkers=5, boots=1, actualReloads=0, pythonTests=70, namedDefinitions=573, inputFingerprints=[len(x['InputHashes']) for x in audits]),
        frozenSavedAcceptance=dict(commit=FROZEN, receipt=frozen_path, sha256=hashlib.sha256(historical(frozen_path)).hexdigest(), package='.local/class-skills/grizzly-stats', passMarkers=231, boots=3, actualReloads=2),
        inputInventory=inputs, artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'Native AI112pure policy reuses existing wolf context/request shapes with distinct parameter-indexed chance and PetMove services. All2016ordered request/roll/remembered-owner cases match672complete supplementary source contexts across current Normal/Nightmare/Hell columns.',
            'Remembered-owner lookup and >50distance request PetMove3; >28requests walking PetMove0at100velocity and returns regardless of success. At >18walking/townwalking/running-owner follow returns only on success. Visible primary wins over visible potential within strict28full-size distance.',
            'Combat target chooses326sequence at50percent or ordinary A1plus15stall. No target idles15within16or follows. Noncombat target rolls40percent for velocity(0,80,40)with current5/9speeds then WalkToTargetUnit. Wolves\' Run requests and corpse/rage/common-movement branches are not substituted.',
            'One disposable native boot also passes retained Grizzly/Dire arithmetic and real named/affix consumers. All70Python tests pass;648/21input fingerprints preserve573named availability decisions. Earlier231-marker/3boot/2reload acceptance belongs only to frozen55b6569numeric package.',
        ], remaining=[
            'No native Grizzly body,automatic AI caller,movement/target acquisition,attack/sequence execution,save/input or learned handler is enabled. Next: saved vulnerable body,life/defense/traits/recovery/facet/history and then distinct AI112world adapter.',
            'BearSmite326/start32/do2/A2sequence and post-allocation flat physical/knockback ordering require complete composition. Exact modern first-think/busy,geometry/path/event/seed,equipment/sunder,item-only and full incoming HP/death chronology remain explicit limitations.',
            'Learned40mana/cap1/coexistence/right+quick/rollback and preserved rollout remain after actor/combat acceptance. Final B7effects/audio and independent talent/right/quick icons remain pending. Installed main remains101013b.',
        ])
    target = ROOT / 'tools/reference/grizzly-ai-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered
    else:
        target.write_text(rendered)
    print('Prepared Grizzly AI112:2016native comparisons,5markers/1boot/no reloads,70Python tests;171handlers/Druid15,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
