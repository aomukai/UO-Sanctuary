#!/usr/bin/env python3
"""Bind canonical Hunger, shared form blocking and retained combat/reload evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/druid-hunger'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False, full_class=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    names = ['stats-inputs.json', 'stats-native-checks.log', 'hunger-run.log', 'python-tests.log',
        'druid-hunger-fixtures.json', 'druid-fire-claws-fixtures.json', 'druid-feral-fixtures.json', 'druid-maul-fixtures.json',
        'druid-form-reaction-fixtures.json', 'druid-form-interruption-fixtures.json', 'druid-form-casting-fixtures.json',
        'druid-form-world-fixtures.json', 'druid-form-expired-fixtures.json']
    artifacts = [STAGE / name for name in names]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        text = path.read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 175
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    assert 'pass3boots/2actual reloads' in (STAGE / 'hunger-run.log').read_text()
    for name in ['druid-form-basic-block-check', 'druid-hunger-model-check', 'druid-hunger-check',
        'druid-fire-claws-scalars-check', 'druid-fire-claws-check', 'druid-feral-check', 'druid-feral-live-result',
        'druid-maul-check', 'druid-maul-live-result', 'druid-charge-model-check', 'druid-form-reaction-check',
        'druid-form-interruption-check', 'druid-form-casting-check', 'shape-restriction-check', 'druid-form-attack-check']:
        assert log.count(name + ': passed') == 1, name
    for name in ['druid-hunger-reload-check', 'druid-hunger-snapshot-check', 'druid-fire-claws-reload-check',
        'druid-feral-reload-check', 'druid-maul-reload-check', 'druid-maul-expiry-stage-check']:
        assert log.count(name + ': passed') == 2, name
    assert '34560source cases and28800S3author/source/native schedules pass' in log
    assert '16shield/no-block impacts' in log and log.count('release-time hit,post-critical physical reduction') == 2
    assert '189unique native mastered endpoint/hard-synergy/mana/AR comparisons' in log
    assert '1120native gain/cap/current-rank/expiry/channel comparisons' in log
    for skill in ['hunger', 'fire-claws', 'feral', 'maul']:
        for kind in ['right', 'quick']:
            assert log.count(skill + '-' + kind + '-result: passed') == log.count(skill + '-' + kind + ': windup') == 3
    saved = json.loads((STAGE / 'druid-hunger-fixtures.json').read_text())
    assert saved['Form']['Form']['Ability'] == 162 and saved['Form']['Form']['LycanthropyRank'] == 20
    assert saved['Charge']['Ability'] == 166 and saved['Charge']['Count'] == 1 and saved['Charge']['LifeDrainMinimum'] == 4
    parent_raw, parent = frozen_receipt('9bb73b3', 'tools/reference/druid-fire-claws-native-evidence.json')
    assert parent['verified']['passMarkers'] == 156 and parent['verified']['fullClassMarkers'] == 1184
    prepared_path = 'tools/reference/druid-hunger-block-preparation.json'
    prepared_raw = subprocess.check_output(['git', 'show', '0e42889:' + prepared_path], cwd=ROOT)
    prepared = json.loads(prepared_raw)
    assert prepared['verified']['markers'] == 20 and prepared['verified']['nativeBlockControls'] == 16
    for relative, digest in prepared['artifacts'].items():
        raw = (ROOT / relative).read_bytes() if relative.startswith('.local/') else subprocess.check_output(['git', 'show', '0e42889:' + relative], cwd=ROOT)
        assert hashlib.sha256(raw).hexdigest() == digest, relative
    for prefix, contexts, routine_count in [('druid-hunger', 34560, 8), ('druid-s3-clock', 28800, 3)]:
        path = ROOT / 'tools/reference' / (prefix + '-engine-evidence.json')
        source = json.loads(path.read_text())
        assert source['contexts'] == contexts and len(source['completeRoutineHashes']) == routine_count
        assert sha(ROOT / source['fixture']['path']) == source['fixture']['sha256']
        artifacts += [path, ROOT / source['fixture']['path']]
    for prefix, suffixes in [('druid-hunger', ['-source-probe.cpp', '-compiler.log', '-sanitizer.log']),
        ('druid-s3-clock', ['-source-probe.cpp', '-compiler.log', '-sanitizer.log']),
        ('druid-s3-rate', ['-source-probe.mjs', '-source-probe.log'])]:
        artifacts += [ROOT / '.local/class-skills' / (prefix + suffix) for suffix in suffixes]
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for relative, digest in audit['InputHashes'].items():
            assert sha(ROOT / relative) == digest, relative
        audits.append(audit)
        artifacts.append(path)
    baseline = json.loads(subprocess.check_output(['git', 'show', '9bb73b3:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
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
        full_markers, full_saves = text.count(': passed'), text.count('Writing world save snapshot done')
        assert full_markers == 1184 and full_saves == 35
        for name, expected in [('full-named-item-runtime-audit.json', audits[0]), ('full-first-loop-affix-audit.json', audits[1])]:
            path = STAGE / name
            assert json.loads(path.read_text()) == expected
            artifacts.append(path)
        artifacts += [STAGE / name for name in ['class-inputs.json', 'class-native-checks.log', 'full-run.log']]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(handlers) == 179 and sum(x['characterClass'] == 'Druid' for x in handlers) == 23 and status['completedClasses'] == []
    artifacts += [ROOT / name for name in ['tools/record_druid_hunger.py', 'tests/check_druid_hunger.py', 'tests/check_creeper_stats.py',
        'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json']]
    result = dict(referenceVersion='3.3.93847', installed=False, handlers=179, druidHandlers=23, completedClasses=[], goalComplete=False, currentExecutableParity=False,
        verified=dict(passMarkers=175, boots=3, actualReloads=2, sourceContexts=34560, sourceRoutines=8, nativeS3Schedules=28800,
            retainedFireEndpointComparisons=189, retainedChargeComparisons=1120, nativeBlockControls=16,
            actualHungerInputs=6, retainedFireFeralMaulInputs=18, retainedFormInputs=15, pythonTests=70,
            inputFingerprints=[len(x['InputHashes']) for x in audits], unchangedNamedDefinitions=573,
            fullClassMarkers=full_markers, fullClassBoots=3 if full_class else None, fullClassReloads=2 if full_class else None, fullClassSaveSnapshots=full_saves),
        frozenPriorAcceptance=dict(commit='9bb73b3', receipt='tools/reference/druid-fire-claws-native-evidence.json', sha256=hashlib.sha256(parent_raw).hexdigest(), passMarkers=156, fullClassMarkers=1184),
        frozenPreparation=dict(commit='0e42889', receipt=prepared_path, sha256=hashlib.sha256(prepared_raw).hexdigest()),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        established=[
            'Learned wolf/bear Hunger242uses release-time contact/result/weapon capture, distinct S3frame6/length10timing and three-mana post-do payment. The existing normalized single-weapon form progress is reused, not raw S3animation speed or the A1action schedule. The native model compares34560bounded damage/formula cases and28800S3schedules; Rabies/bear rows are synthetic clock controls,not skill permission.',
            'Hunger reduces filled physical damage after physical critical using the existing signed SourcePercentage helper, before mitigation. Typed weapon components remain separate. Life/mana drain adds to captured gear/Feral channels and heals from supported physical damage with existing drain/difficulty policy. Actual Highlord gear, full-mana post-leech payment, miss, shield block, physical immunity and unchanged Feral/Maul count/deadline pass both forms.',
            'The frozen before-control proves ordinary transformed Attack ignored NPC shields. The corrected shared form path applies the existing SourceBlocking consumer after a successful hit. Sixteen successful/failed-block controls cover both forms and direct Attack plus Hunger/Fire Claws/Feral/Maul low-mana fallbacks. Blocked fallback attacks do no damage/leech/mana debit. Human/ranged ordinary-attack paths are outside this scoped correction.',
            'Movement out/back, weapon change, Stop, unshift and lost contact cancel pending Hunger. Lost mana during windup is explicitly refused. Six actual Hunger casts and18retained Fire Claws/Feral/Maul casts pass across three boots/two actual reloads. Original form,charge,bindings and fractional injuries/resources survive without packet replay; all70Python tests and573unchanged named decisions pass. Main installed build/client/configuration/saves remain preserved.',
        ], remaining=[
            'Exact modern event/seed/geometry/reentry, target-loss/lost-mana/minimum-mana/payment quirks, reentrant effects, complete current form clocks and client acceptance remain unverified. Old source routines execute around declared weapon/mitigation/formula/Fog/seed services,not a whole modern executable. Low-mana ordinary Attack remains the table/older-dispatcher adapter. Human/ranged common-Attack NPC blocking needs its own scope; item-only/foreign classes/PvP/unmapped recipients remain unsupported.',
            'Final wolf/bear S3body,bite/contact,leech and audio are pending. Human Wrestle,contact sound0x238 and shared leech0x376A are identified placeholders; current Hunger has no separate source impact overlay. Talent/left/right/quick icons are tracked independently. Remaining Druid skills,earlier class gaps and Warlock remain open; no class or goal completion is claimed.',
        ])
    path = ROOT / 'tools/reference/druid-hunger-native-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print(f'Hunger:175focused/3boots/2reloads/6actual inputs,16shield controls,34560model/28800S3,70Python;fullClass={full_markers};179handlers/Druid23,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
