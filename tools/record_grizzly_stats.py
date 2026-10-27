#!/usr/bin/env python3
"""Bind prepared Grizzly stats to exact native/source inputs and retained checks."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/grizzly-stats'
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inventory = json.loads((STAGE / 'stats-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for folder in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / folder).glob('*.cs')}
    assert {p for p in inventory['sourceInputs'] if p.endswith('.cs')} == expected
    for name, digest in inventory['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inventory['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'python-tests.log']]
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == inventory['artifacts']['Assemblies/' + name]
        build = STAGE / (kind + '-build.log')
        text = build.read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        artifacts += [build, STAGE / kind / name]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 231
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    assert log.count('grizzly-stats-check: passed') == 1
    assert 'Grizzly prepared birth:55152complete source comparisons including72nonzero-sunder transport controls' in log
    assert 'Prepared Grizzly attack-mode stats match6144source contexts' in log
    for name in ['dire-wolf-body', 'dire-wolf-world', 'dire-wolf-casting', 'spirit-wolf-body', 'spirit-wolf-world', 'spirit-wolf-casting', 'barbs-casting', 'wolverine-casting', 'oak-casting']:
        assert log.count(name + '-check: passed') == 1 and log.count(name + '-reload-check: passed') == 2, name
    for name in ['dire-wolf', 'spirit-wolf', 'barbs', 'oak', 'wolverine']:
        for kind in ['right', 'quick']:
            assert log.count(name + '-' + kind + '-result: passed') == 3, name
    for name in ['dire-wolf-world-live', 'spirit-wolf-world-live', 'spirit-wolf-hit-live', 'spirit-wolf-rate-live', 'spirit-wolf-slow-live', 'barbs-world-live', 'oak-world-live', 'wolverine-world-live']:
        assert log.count(name + '-result: passed') == 3, name
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    sources = {}
    for name, evidence_name, count in [('grizzly-creation', 'grizzly-creation-engine-evidence.json', 55158), ('grizzly-attack', 'grizzly-attack-engine-evidence.json', 6368), ('grizzly-ai', 'grizzly-ai-source-evidence.json', 672)]:
        evidence_path = ROOT / 'tools/reference' / evidence_name
        evidence = json.loads(evidence_path.read_text())
        assert evidence['referenceVersion'] == '3.3.93847' and evidence['legacyCommit'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
        fixture = ROOT / evidence['fixture']['path']
        generated = BASE / (name + '-source-probe.cpp')
        sanitizer = BASE / (name + '-sanitizer.log')
        assert evidence['fixture']['cases'] == count and sha(fixture) == evidence['fixture']['sha256']
        assert sha(generated) == evidence['fixture'].get('generatedSourceSha256', evidence.get('generatedSourceSha256'))
        assert sanitizer.read_text() == ''
        for source, digest in evidence['sourceHashes'].items():
            assert sha(BASE / 'd2moo-pinned-source' / source) == digest, source
        sources[name] = count
        artifacts += [evidence_path, fixture, generated, sanitizer]
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
    baseline = json.loads(subprocess.check_output(['git', 'show', '101013b:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 171 and sum(x['characterClass'] == 'Druid' for x in handlers) == 15
    assert status['completedClasses'] == []
    assert next(x for x in status['skills'] if x['talentId'] == 'druid.summongrizzly')['implementation'] != 'handler-present'
    artifacts += [ROOT / name for name in ['tools/record_grizzly_stats.py', 'tools/probe_grizzly_creation_reference.py', 'tools/probe_grizzly_attack_reference.py', 'tools/probe_grizzly_ai_reference.py', 'tools/prepare_grizzly_reference.py', 'tools/reference/grizzly-preflight.json', 'tools/reference/grizzly-tooltip-user-report.json', 'tools/build_class_skills.py', 'tests/check_grizzly_stats.py']]
    result = dict(referenceVersion='3.3.93847', installed=False, handlerEnabled=False, handlers=171, druidHandlers=15, completedClasses=[], goalComplete=False,
        verified=dict(nativeBirthComparisons=55152, nonzeroSunderTransportControls=72, nativeOrdinaryModeComparisons=6144, sourceCases=sources,
            passMarkers=231, boots=3, actualReloads=2, pythonTests=70, namedDefinitions=573, inputFingerprints=[len(x['InputHashes']) for x in audits]),
        inputInventory=inventory, artifacts={str(p.relative_to(ROOT)): sha(p) for p in artifacts},
        established=[
            'Prepared do119 Grizzly birth uses750Normal/650Nightmare-Hell life,245defense,zero initial flags7AR,234/203FP base regeneration,10*own+15*effectiveDire life percent,10*effectiveSpirit armor,25*effectiveSpirit tohit,copied own25+10/rank damagepercent,cap1,40mana,four capped resists and requested equipment level.',
            'Native content matches55152complete creation source contexts and6144ordinary attack-mode contexts. Actual trained owner/Tarnhelm snapshots retain effective synergy ranks and immutable birth; reconstruction uses captured ranks/difficulty and preserves numeric inherited physical-sunder transport. Rank28current user tooltip is corroborated.',
            'Ordinary attack endpoints/AR use live owner skill plus separate monster lanes:rank28raw496..526,A1AR1140,other tested modesAR840. Birth monlvl/passive AR remains separate. Sequence12is explicitly rejected by this ordinary-mode helper.',
            'Retained saved Dire/Spirit Wolf,spirits,corpse,weapon and input consumers pass3boots/2actual reloads; connected natural clocks/right+quick inputs run on each boot. All70Python tests pass,646/21fingerprints preserve573named availability decisions. No Grizzly actor/save/input acceptance is claimed.',
            'Complete supplementary Bear AI112source proof is prepared separately:672ordered service contexts. It has no native consumer in this package.',
        ], remaining=[
            'Implement and verify native Grizzly AI112,saved vulnerable body,recovery/proportional facet life,mode/timing,ordinary physical hit and BearSmite326sequence/start32/do2/post-combat flat damage/knockback. Then learned casting/cap1/coexistence/payment/rollback/right+quick/save tests and rollout.',
            'Whole modern executable equivalence,geometry/path/first-think/busy/seed/event ordering,nonzero sunder delivery/equipment/item-only scope and complete incoming HP/death chronology remain explicit limitations.',
            'Final B7body/emergence/movement/A1/A2sequence/contact/knockback/hurt/death/audio remain pending in the effects register; talent/right/quick icons are tracked separately. Current installed main remains101013bwith its immutable Dire package and preservation receipt.',
        ])
    path = ROOT / 'tools/reference/grizzly-stats-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Prepared Grizzly:55152birth/6144mode comparisons,231markers/3boots/2reloads,70Python tests;171handlers/Druid15,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
