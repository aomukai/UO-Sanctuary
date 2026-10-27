#!/usr/bin/env python3
"""Bind scoped modern Werebear recovery protection to real damage/input and reloads."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt, sha

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'druid-form-interruption'


def record(check=False, full_class=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    names = ['stats-inputs.json', 'stats-native-checks.log', 'interruption-run.log', 'python-tests.log',
        'python-before-audit-refresh.log', 'druid-form-interruption-fixtures.json', 'druid-form-casting-fixtures.json',
        'druid-form-world-fixtures.json', 'druid-form-expired-fixtures.json']
    names += ['first-mechanics-105-' + name for name in ['stats-native-checks.log', 'stats-inputs.json', 'interruption-run.log',
        'named-item-runtime-audit.json', 'first-loop-affix-audit.json', 'druid-form-interruption-fixtures.json']]
    artifacts = [STAGE / name for name in names]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        text = path.read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 105
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert 'Modern bear:windup/effect/tail/expiry,Stop,first movement,form replacement,wolf/human entry/death controls pass' in log
    assert log.count('druid-form-interruption-check: passed') == 1
    for name in ['druid-form-interruption-reload', 'druid-form-interruption-snapshot', 'druid-form-casting-reload',
        'druid-form-world-reload', 'druid-form-attack-reload', 'shape-restriction-reload']:
        assert log.count(name + '-check: passed') == 2, name
    labels = ['bear-interrupt-quick', 'form-wolf-right', 'form-bear-quick', 'form-wolf-unshift-quick', 'form-bear-unshift-right']
    for label in labels:
        assert log.count(label + '-check: passed') == log.count(label + '-result: passed') == 3, label
        assert log.count(label + ': windup') == 3, label
    for text in ['2590native action/end comparisons match ten licensed records', '14400native action/end schedules match complete source scheduler',
        '2400native complete-source policy comparisons', '14400native arithmetic comparisons']:
        assert text in log, text
    initial = (STAGE / 'first-mechanics-105-stats-native-checks.log').read_text()
    assert initial.count(': passed') == 105 and ': failed' not in initial and ' ERR]' not in initial
    original = json.loads((STAGE / 'first-mechanics-105-stats-inputs.json').read_text())
    assert original['sourceInputs'] == inputs['sourceInputs'] and original['artifacts'] == inputs['artifacts']
    # Only descriptive roadmap metadata changed after the first identical-code run.
    assert {name for name in inputs['copiedData'] if inputs['copiedData'][name] != original['copiedData'][name]} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    saved = json.loads((STAGE / 'druid-form-interruption-fixtures.json').read_text())
    assert saved['Owner'] == saved['Form']['Owner'] and saved['Form']['Form']['Ability'] == 164
    assert saved['Form']['Form']['Rank'] == 1 and saved['Form']['Form']['LycanthropyRank'] == 20
    assert saved['Form']['Form']['Modifiers']['Concentration'] == 100 and saved['Mana'] > 0 and saved['Life'] > 0
    frozen_raw, frozen = frozen_receipt('9a0e949', 'tools/reference/druid-form-casting-native-evidence.json')
    assert frozen['verified']['passMarkers'] == 94 and frozen['verified']['fullClassMarkers'] == 1184
    source_path = ROOT / 'tools/reference/druid-form-death-interrupt-engine-evidence.json'
    source = json.loads(source_path.read_text())
    assert source['verified'] == dict(deathCases=60, interruptCases=4800) and source['licensedSkills']['bearConcentration'] == 100
    for name, digest in source['sourceHashes'].items():
        assert sha(BASE / 'd2moo-pinned-source' / name) == digest, name
    assert sha(BASE / 'druid-form-death-interrupt-probe.cpp') == source['generatedSourceSha256']
    assert sha(BASE / 'druid-form-death-interrupt-sanitizer.log') == source['sanitizerLogSha256']
    assert (BASE / 'druid-form-death-interrupt-sanitizer.log').read_text() == ''
    artifacts += [source_path, BASE / 'druid-form-death-interrupt-probe.cpp', BASE / 'druid-form-death-interrupt-sanitizer.log']
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for name, digest in audit['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
        audits.append(audit)
        artifacts.append(path)
    baseline = json.loads(subprocess.check_output(['git', 'show', '9a0e949:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    full_markers = full_saves = None
    if full_class:
        complete = inventory.validate_inventory(STAGE / 'class-inputs.json')
        assert complete['artifacts'] == inputs['artifacts'] and complete['copiedData'] == inputs['copiedData']
        text = (STAGE / 'class-native-checks.log').read_text()
        assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down') == 3
        assert 'including two save/reload cycles.' in (STAGE / 'full-run.log').read_text()
        full_markers = text.count(': passed')
        full_saves = text.count('Writing world save snapshot done')
        assert full_markers == 1184 and full_saves == 35
        for name, expected in [('full-named-item-runtime-audit.json', audits[0]), ('full-first-loop-affix-audit.json', audits[1])]:
            path = STAGE / name
            assert json.loads(path.read_text()) == expected
            artifacts.append(path)
        artifacts += [STAGE / name for name in ['class-inputs.json', 'class-native-checks.log', 'full-run.log']]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [row for row in status['skills'] if row['implementation'] == 'handler-present']
    assert len(handlers) == 175 and sum(row['characterClass'] == 'Druid' for row in handlers) == 19 and status['completedClasses'] == []
    artifacts += [ROOT / name for name in ['tools/record_druid_form_interruption.py', 'tools/build_skill_roadmap.py',
        'tests/check_druid_form_interruption.py', 'tests/check_creeper_stats.py', 'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json']]
    result = dict(referenceVersion='3.3.93847', installed=False, handlers=175, druidHandlers=19, completedClasses=[], goalComplete=False,
        verified=dict(passMarkers=105, boots=3, actualReloads=2, realInputCasts=15, incomingDamageInputCasts=3,
            actualProductionDamageCalls=9, sourceDeathCases=60, sourceInterruptionCases=4800, pythonTests=70,
            inputFingerprints=[len(audit['InputHashes']) for audit in audits], unchangedNamedDefinitions=573,
            fullClassMarkers=full_markers, fullClassBoots=3 if full_class else None, fullClassReloads=2 if full_class else None, fullClassSaveSnapshots=full_saves),
        frozenPriorAcceptance=dict(commit='9a0e949', receipt='tools/reference/druid-form-casting-native-evidence.json', sha256=hashlib.sha256(frozen_raw).hexdigest(), passMarkers=94, fullClassMarkers=1184),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        established=[
            'Current bear100concentration protects only its captured actual attack/skill action interval and original form identity. Basic Attack,Raven and Cyclone tests retain windup/effect/post-effect tail. Stop,first accepted/external out-and-back movement,form replacement and end boundary invalidate protection. Human entry windup,wolf,idle and human death controls retain their ordinary behavior. The successful human-to-bear effect captures only the remaining bear animation tail.',
            'Three actual Quick Raven casts invoke the production GraveyardUndead.Deal path during windup,after the effect and after expiry. All nine calls cause actual nonlethal guaranteed-recovery-sized life loss. Protected hits leave pending effect/deadline intact; idle hits cause recovery. Each input produces one actual Raven and pays6mana once. Twelve existing right/quick form entry/zero-mana-return inputs remain covered across all three boots.',
            'Both actual reloads preserve the original captured bear/form duration,life and mana while transient action/protection is not serialized or replayed. Existing form shared cooldown,learned rank20bear,proportional injury,bindings,expiry,shape,rate,attack,BO and summon checks remain. The first105-marker run is identical code/DLLs; only descriptive roadmap metadata changes before the final105-marker run and fresh audit capture. The preliminary Python run correctly rejected stale old audit fingerprints; final70tests use current663/21exports and573unchanged decisions.',
        ], remaining=[
            'Forced lethal-unshift numeric life restoration,other hurt/block/movement clocks,rank/gear/respec refresh/cleanup,modern extraAR/reentry/geometry and complete current-client acceptance remain incomplete or unverified. Do not infer permanent stun/knockback/death immunity or full D2Rparity from this recovery guard. No class is complete.',
            'Direct cross-form switching,item-only/foreign-class grants and current lost-mana release behavior remain unsupported. Main retains the installed Grizzly package. Final40/TGmodels,motion,skill effects/audio and talent/right/quick icons remain separately identified placeholders; this guard needs no new art.',
        ])
    output = ROOT / 'tools/reference/druid-form-interruption-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print(f'Werebear interruption:105checks/3boots/2reloads,15real inputs/9production damage calls,70Python;fullClass={full_markers};175handlers/Druid19,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
