#!/usr/bin/env python3
"""Bind prepared Oak Sage arithmetic to native acceptance and retained save consumers."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/oak-sage'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inputs = json.loads((STAGE / 'stats-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for directory in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / directory).glob('*.cs')}
    assert {name for name in inputs['sourceInputs'] if name.endswith('.cs')} == expected
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == inputs['artifacts']['Assemblies/' + name]
        log = (STAGE / (kind + '-build.log')).read_text()
        assert '0 Warning(s)' in log and '0 Error(s)' in log
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    assert log.count('oak-sage-stats-check: passed') == 1
    assert '180complete source creation compositions,255rank aura/cost/tooltip controls and real owner gear snapshot' in log
    for name in ['creeper-body', 'creeper-world', 'creeper-ground', 'creeper-poison', 'creeper-casting', 'spirit-wolf-world']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    assert log.count('creeper-world-live-result: passed') == 3
    assert log.count('creeper-poison-live-result: passed') == 3
    assert log.count('Writing world save snapshot done') == 3 and log.count('Shutting down') == 3
    source = json.loads((ROOT / 'tools/reference/oak-sage-engine-evidence.json').read_text())
    assert source['fixture']['cases'] == 186
    assert sha(ROOT / source['fixture']['path']) == source['fixture']['sha256']
    assert sha(ROOT / '.local/class-skills/oak-sage-source-probe.cpp') == source['generatedSourceSha256']
    for name, digest in source['sourceHashes'].items():
        assert sha(ROOT / '.local/class-skills/d2moo-pinned-source' / name) == digest, name
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        audit = json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit == json.loads((STAGE / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for path, digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
        audits.append(audit)
    baseline = json.loads(subprocess.check_output(['git', 'show', '8cf01d9:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'content-build.log', 'integration-build.log', 'python-tests.log', 'named-item-runtime-audit.json', 'first-loop-affix-audit.json']]
    artifacts += [ROOT / 'tools/reference' / name for name in ['oak-sage-preflight.json', 'oak-sage-engine-evidence.json']]
    artifacts += [ROOT / name for name in ['tools/prepare_oak_sage_reference.py', 'tools/probe_oak_sage_reference.py', 'tools/build_class_skills.py', 'tests/check_oak_sage_stats.py', 'tools/record_oak_sage_stats.py']]
    artifacts += [STAGE / name for name in ['body-fixtures.json', 'world-fixtures.json', 'ground-fixtures.json', 'poison-fixtures.json', 'casting-fixtures.json', 'wolf-world-fixtures.json']]
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, installed=False,
        scope='Numeric creation/aura component acceptance; actual prepared Oak body/aura acceptance and remaining rollout gates are tracked in the current resume checkpoint. Learned Oak remains unavailable.',
        verified=dict(sourceCompositions=180, sourceFailureBranches=6, effectiveRankControls=255,
            realOwnerTarnhelm=True, sourceSanitizers='ASan and UBSan', retainedBoots=3, retainedReloads=2,
            retainedNaturalPoisonRuns=3, passMarkers=log.count(': passed'), pythonTests=70,
            namedDecisions=573, namedInputFingerprints=len(audits[0]['InputHashes']), firstLoopInputFingerprints=len(audits[1]['InputHashes'])),
        sourceInputs=inputs['sourceInputs'], copiedData=inputs['copiedData'], testedAssemblies=inputs['artifacts'],
        artifacts={str(path.relative_to(ROOT)): sha(path) for path in artifacts},
        established=[
            'Complete older do119/base/passive paths match prepared native creation for supplied noRatio spawn bases, owner level, classic/expansion difficulty columns, fractional life, both poison passives, inherited aura rank and requested equipment level.',
            'All255effective ranks retain separate NPC aura radius/life formulas and player tooltip mirrors. A real learned rank1owner captures immutable60life; Tarnhelm yields rank2/78life without changing the prior snapshot. Difficulty reconstruction retains captured rank/owner level.',
            'Oak Sage learned availability and right/quick bindings remain unavailable. New data remains in the isolated package; original installed package, client and saves are not written by the disposable tests.'
        ], remaining=[
            'Totem movement/escape/teleport, shared spirit cap/replacement and learned casting remain unfinished. Saved body/aura/lifecycle acceptance is recorded separately; numeric values alone do not establish those behaviors.',
            'Current executable movement/event/RNG/room/geometry/clock equivalence, staffmod-only permission and full incoming exceptional state/owner/client contexts remain to verify.',
            'Learned SC/resource/cancellation/right/quick input, full retained regression, graphical client acceptance and preserved installation. Modern event/RNG/room/path/geometry/clock equivalence is not claimed.',
            'Final spirit/body/emergence/movement/death/aura/audio effects and separate talent/right/quick icons remain pending in their existing registers.'
        ])
    output = ROOT / 'tools/reference/oak-sage-native-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Oak Sage numeric preparation:180source/native compositions,255rank controls,3retained boots/2reloads and70Python tests; learned Oak remains disabled.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
