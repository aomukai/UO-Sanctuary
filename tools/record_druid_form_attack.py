#!/usr/bin/env python3
"""Bind actual prepared form A1/melee consumers and frozen prior full acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt, sha

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'druid-form-attack'


def record(check=False, full_class=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    names = ['stats-inputs.json', 'stats-native-checks.log', 'attack-run.log', 'python-tests.log',
        'druid-form-world-fixtures.json', 'druid-form-expired-fixtures.json', 'first-attempt-wrapper-path-failure.log',
        'second-attempt-classification-native-checks.log', 'second-attempt-classification-run.log',
        'third-attempt-ordinary-glaive-metadata-native-checks.log', 'third-attempt-ordinary-glaive-metadata-run.log']
    artifacts = [STAGE / name for name in names]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log');text = path.read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 65
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    for name in ['druid-form-model', 'druid-form-world', 'shape-restriction', 'druid-form-rate', 'druid-form-attack', 'grizzly-ai', 'grizzly-stats', 'dire-wolf-stats']:
        assert log.count(name + '-check: passed') == 1, name
    for name in ['druid-form-world-reload', 'shape-restriction-reload', 'druid-form-attack-reload', 'druid-form-world-snapshot', 'druid-form-expiry-stage']:
        assert log.count(name + '-check: passed') == 2, name
    assert log.count('druid-form-live-result: passed') == 1 and log.count('battle-orders-result: passed') == 3
    assert '14400native action/end schedules match complete source scheduler' in log
    assert 'empty-quantity javelin/dagger melee impacts' in log
    assert '2400native complete-source policy comparisons' in log and '14400native arithmetic comparisons' in log
    assert log.count('Druid form reload:original captured ranks/start/deadline,typed pools and20007FPinjury preserved') == 2
    assert log.count('Druid form expired reload:elapsed original deadline removed before gameplay') == 2
    for stem, contexts in [('druid-form-attack-clock', 14400), ('druid-form-weapon-routing', 72), ('druid-form-basic-packet', 420)]:
        path = ROOT / 'tools/reference' / (stem + '-engine-evidence.json');source = json.loads(path.read_text())
        assert source['contexts'] == contexts
        for name, digest in source['sourceHashes'].items():
            assert sha(BASE / 'd2moo-pinned-source' / name) == digest
        assert sha(ROOT / source['fixture']['path']) == source['fixture']['sha256']
        assert sha(BASE / (stem + '-source-probe.cpp')) == source['generatedSourceSha256']
        assert sha(BASE / (stem + '-sanitizer.log')) == source['sanitizerLogSha256']
        assert (BASE / (stem + '-sanitizer.log')).read_text() == ''
        artifacts += [path] + [BASE / (stem + suffix) for suffix in ['-source-probe.cpp', '-source-probe.json', '-sanitizer.log', '-compiler.log']]
    metadata = ROOT / 'data/Sanctuary/Balance/weapon-animation-classes.json'
    rows = json.loads(metadata.read_text())
    assert len(rows) == len({row['code'] for row in rows}) == 306
    public_path = ROOT / 'tools/reference/d2r-named-equipment/json/weapons.json'
    manifest = json.loads((ROOT / 'tools/reference/d2r-named-equipment/sources.json').read_text())
    assert sha(public_path) == manifest['files']['json/weapons.json']['sha256']
    source = json.loads(public_path.read_text());expected = {r['code']:r for r in source.values() if r.get('code') and r.get('wclass')}
    for row in rows:
        original = expected[row['code']]
        assert row['oneHanded'] == original['wclass'].upper()
        assert row['twoHanded'] == (original.get('2handedwclass') or original['wclass']).upper()
    frozen_raw, frozen = frozen_receipt('9ee3cad', 'tools/reference/shape-restrictions-native-evidence.json')
    assert frozen['verified']['passMarkers'] == 62 and frozen['verified']['fullClassMarkers'] == 1184
    assert frozen['verified']['boots'] == 3 and frozen['verified']['actualReloads'] == 2
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name;audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for name, digest in audit['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
        artifacts += [path, ROOT / 'tools/reference' / path.name];audits.append(audit)
    baseline = json.loads(subprocess.check_output(['git', 'show', '9ee3cad:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
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
        full_markers = text.count(': passed');full_saves = text.count('Writing world save snapshot done')
        assert full_markers == 1184 and full_saves == 35
        for name, expected in [('full-named-item-runtime-audit.json', audits[0]), ('full-first-loop-affix-audit.json', audits[1])]:
            path = STAGE / name;assert json.loads(path.read_text()) == expected;artifacts.append(path)
        artifacts += [STAGE / name for name in ['class-inputs.json', 'class-native-checks.log', 'full-run.log']]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [row for row in status['skills'] if row['implementation'] == 'handler-present']
    assert len(handlers) == 172 and sum(row['characterClass'] == 'Druid' for row in handlers) == 16 and status['completedClasses'] == []
    artifacts += [metadata, public_path] + [ROOT / path for path in ['tools/record_druid_form_attack.py', 'tests/check_druid_form_attack.py', 'tools/build_weapon_animations.py']]
    result = dict(referenceVersion='3.3.93847', installed=False, playerFormHandlersEnabled=False, handlers=172, druidHandlers=16, completedClasses=[], goalComplete=False,
        verified=dict(passMarkers=65, boots=3, actualReloads=2, nativeActionEndSchedules=14400, rateArithmeticContexts=14400, shapePolicyContexts=2400,
            weaponAnimationBases=306, pythonTests=70, namedDefinitions=573, inputFingerprints=[len(audit['InputHashes']) for audit in audits],
            fullClassMarkers=full_markers, fullClassBoots=3 if full_class else None, fullClassReloads=2 if full_class else None, fullClassSaveSnapshots=full_saves),
        frozenPriorAcceptance=dict(commit='9ee3cad', receipt='tools/reference/shape-restrictions-native-evidence.json', sha256=hashlib.sha256(frozen_raw).hexdigest(), passMarkers=62, fullClassMarkers=1184),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        established=[
            'Prepared Druid ordinary Attack now uses current human weapon normalization,verified modern progress and source frame7action/ENDANIM timing. The live ClassCombat start/release path consumes this timing. A captured form identity cancels changed-form windups; existing movement,weapon,frozen and recovery guards remain.',
            'Both forms use real bow/crossbow/empty-quantity javelin/dagger melee impacts once,without ammunition,pierce or projectile range. Existing scoped melee selection preserves original weapon identity,source stat scaling and ordinary full128weapon damage. Hit/block,defense,retaliation and on-hit consumers use melee classification. Human bow classification/timing restore after removal.',
            'All306source weapon animation classes are available independently of named-item catalogue membership. The first wrapper failed before starting a boot; the next two attempts exposed/diagnosed an ordinary glaive absent from the named-only lookup. Source-wide metadata fixes that gap without weakening the refusal or impact checks. Failed logs are retained.',
            'Retained saved form resources,original deadline/injury,live/offline expiry,shape restrictions,BO and summon consumers pass three boots/two real reloads. Both builds and70Python tests pass; fresh named/affix audits preserve573decisions. Foundation-approved resource projection remains unchanged.',
        ], remaining=[
            'Modern executable exact action/end/reentry/cancellation,source footprint/distance and graphical/client acceptance remain open. Native melee reach and human Wrestle pose are identified adapters; final40/TGbody,motion,effects/audio and separate talent/hotkey icons remain pending.',
            'The older extra ToHit allocation uses incoming attack level; current retention is unverified and is not added to the captured native form item-percent bonus. Cross-form Param12,form-specific other clocks/concentration,learned15mana/free-unshift/global25frame delay/right+quick/casting remain unfinished. Learned forms stay disabled.',
            'Main remains3c0c61bon preserved installed Grizzly40e8daa. This prepared attack component is neither class completion nor a current-executable certificate. Keep the accepted predecessor package immutable.',
        ])
    output = ROOT / 'tools/reference/druid-form-attack-native-evidence.json';rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print(f'Prepared Druid form Attack:65markers/3boots/2reloads,14400action schedules,306base animations,70Python;fullClass={full_markers};172handlers/Druid16,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true');parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args();record(args.check, args.full_class)
