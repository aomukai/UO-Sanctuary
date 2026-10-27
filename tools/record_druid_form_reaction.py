#!/usr/bin/env python3
"""Bind form FHR/FBR source clocks and the explicitly provisional native projection."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt, sha

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'druid-form-reaction'


def record(check=False, full_class=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    names = ['stats-inputs.json', 'stats-native-checks.log', 'reaction-run.log', 'python-tests.log',
        'integration-pre-inspection-build.log', 'druid-form-reaction-fixtures.json', 'druid-form-interruption-fixtures.json',
        'druid-form-casting-fixtures.json', 'druid-form-world-fixtures.json', 'druid-form-expired-fixtures.json',
        'wrong-stage-attempt-stats-inputs.json', 'wrong-stage-attempt-stats-native-checks.log', 'wrong-stage-attempt-run.log']
    names += [name for name in ['wrong-stage-attempt-named-item-runtime-audit.json', 'wrong-stage-attempt-first-loop-affix-audit.json'] if (STAGE / name).exists()]
    artifacts = [STAGE / name for name in names]
    assert 'KeyboardInterrupt' in (STAGE / 'wrong-stage-attempt-run.log').read_text()
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        text = path.read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 112
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert log.count('druid-form-reaction-check: passed') == log.count('druid-form-reaction-live-check: passed') == log.count('druid-form-reaction-live-result: passed') == 1
    assert '2072native complete-source GH/BL speed/end comparisons' in log
    for name in ['druid-form-reaction-reload', 'druid-form-reaction-snapshot', 'druid-form-interruption-reload',
        'druid-form-interruption-snapshot', 'druid-form-casting-reload', 'druid-form-world-reload', 'druid-form-attack-reload', 'shape-restriction-reload']:
        assert log.count(name + '-check: passed') == 2, name
    for label in ['bear-interrupt-quick', 'form-wolf-right', 'form-bear-quick', 'form-wolf-unshift-quick', 'form-bear-unshift-right']:
        assert log.count(label + '-check: passed') == log.count(label + '-result: passed') == log.count(label + ': windup') == 3, label
    for text in ['2590native action/end comparisons match ten licensed records', '14400native action/end schedules match complete source scheduler',
        '2400native complete-source policy comparisons', '14400native arithmetic comparisons']:
        assert text in log, text
    saved = json.loads((STAGE / 'druid-form-reaction-fixtures.json').read_text())
    assert saved['Owner'] == saved['Form']['Owner'] and saved['Form']['Form']['Ability'] == 162
    assert saved['Form']['Form']['Rank'] == 1 and saved['Form']['Form']['LycanthropyRank'] == 20
    assert saved['Gloves'] != saved['Armor'] and saved['InspectionRecoveryUntil'] > 0
    assert saved['Hit'] == dict(Advance256=189, SourceEndFrame=6)
    assert saved['Block'] == dict(Advance256=189, SourceEndFrame=7)
    frozen_raw, frozen = frozen_receipt('cfd45b1', 'tools/reference/druid-form-interruption-native-evidence.json')
    assert frozen['verified']['passMarkers'] == 105 and frozen['verified']['fullClassMarkers'] == 1184
    source_path = ROOT / 'tools/reference/druid-form-recovery-clock-engine-evidence.json'
    source = json.loads(source_path.read_text())
    assert source['contexts'] == 7770 and source['formContexts'] == 2072 and len(source['animations']) == 15
    assert source['nativeAccepted'] is False and source['currentExecutableParity'] is False
    for name, digest in source['sourceHashes'].items():
        assert sha(BASE / 'd2moo-pinned-source' / name) == digest, name
    assert sha(BASE / 'druid-form-recovery-clock-source-probe.cpp') == source['generatedSourceSha256']
    assert sha(ROOT / source['fixture']['path']) == source['fixture']['sha256']
    assert sha(BASE / 'druid-form-recovery-clock-sanitizer.log') == source['sanitizerLogSha256']
    assert (BASE / 'druid-form-recovery-clock-sanitizer.log').read_text() == ''
    artifacts += [source_path] + [BASE / ('druid-form-recovery-clock' + suffix) for suffix in ['-source-probe.cpp', '-source-probe.json', '-compiler.log', '-sanitizer.log']]
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
    baseline = json.loads(subprocess.check_output(['git', 'show', 'cfd45b1:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
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
    artifacts += [ROOT / name for name in ['tools/record_druid_form_reaction.py', 'tests/check_druid_form_reaction.py',
        'tests/check_creeper_stats.py', 'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json']]
    result = dict(referenceVersion='3.3.93847', installed=False, handlers=175, druidHandlers=19, completedClasses=[], goalComplete=False,
        currentExecutableParity=False, sourceEndEqualsNativeDeadline=False,
        verified=dict(passMarkers=112, boots=3, actualReloads=2, nativeSourceComparisons=2072, licensedReactionRecords=15,
            realGear=['Bloodfist30FHR', 'Guardian Angel30FBR'], nativeReactionFixtureEntries=15, naturalWolfRecoveryMilliseconds=200,
            heldRestartInspectionOnly=True, retainedRealInputs=15, retainedProductionMonsterDamageCalls=9, pythonTests=70,
            inputFingerprints=[len(audit['InputHashes']) for audit in audits], unchangedNamedDefinitions=573,
            fullClassMarkers=full_markers, fullClassBoots=3 if full_class else None, fullClassReloads=2 if full_class else None, fullClassSaveSnapshots=full_saves),
        frozenPriorAcceptance=dict(commit='cfd45b1', receipt='tools/reference/druid-form-interruption-native-evidence.json', sha256=hashlib.sha256(frozen_raw).hexdigest(), passMarkers=105, fullClassMarkers=1184),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        established=[
            'Form-only GH/BL rates now feed the two existing shared native reaction-duration consumers. The pure helper compares2072form cases against complete older dispatcher/writers/scheduler with current licensed metadata. Human functions remain unchanged. Source advance/end and the inherited native ceil-minus-one projection are separate fields; wolf zero-gear native7/9 and bear13/12 are adapter values,not a current-executable reentry certificate.',
            'Both forms use live Bloodfist30FHR/Guardian Angel30FBR changes,entry/unshift and human fallback. Native get-hit/block entries retain exact projected durations and expiry boundaries. Blocking preserves active form attacks and the existing bear concentration guard still protects action windup/tail. A real200ms wolf recovery rejects an actual step and then releases actual movement after its natural deadline.',
            'Two reloads preserve original captured wolf/rank/Lycanthropy/start/deadline and exact worn item identities/reaction values. A real projected recovery is verified then held only for restart inspection immediately before saving; it is not serialized/replayed. The separate natural timer keeps200ms. All retained casting/interruption/shape/rate/attack/BO/summon checks and15actual inputs/nine monster damage calls pass with70Python tests and664/21fresh fingerprints,573unchanged item decisions.',
            'The first harness incorrectly pointed at the prior interruption package. That exact run43581/PID161412was interrupted with exit130 and its child terminated. Its wrong input/log/audit artifacts are retained. Two overwritten prior proof files were restored byte-exactly from the verified main backup; frozen_receipt(cfd45b1)revalidated every artifact and historical source. The corrected harness has an explicit package-name guard and run38420ended0. Installed content/configuration/client/saves were never changed.',
        ], remaining=[
            'Exact modern D2Rreentry remains unverified. The source schedules wolf GH/BL ENDANIM8/10and bear14/13at zero bonus; native reaction durations deliberately retain the existing minus-one convention. This is a provisional inherited adapter,not a newly approved deviation. Do not equate those timeline quantities or infer full D2Rparity from green source/native checks.',
            'Numeric forced-unshift life,knockback/movement,rank/gear/respec refresh/cleanup,current extraAR/action geometry/client and unsupported direct switching/item-only/lost-mana routes remain separate. Maul/Feral preflight is prepared but no charged-attack handler is enabled. Main retains installed Grizzly; no class is complete.',
            'Generic human animation20remains an identified form hurt/block placeholder. Final40/TGmotion/effects/audio and independent talent/right/quick icons stay pending; these assets do not gate mechanical implementation.',
        ])
    path = ROOT / 'tools/reference/druid-form-reaction-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Form reactions:112checks/3boots/2reloads,2072source comparisons,realFHR/FBR/natural200msmovement,70Python;fullClass={full_markers};current reentry remains provisional.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
