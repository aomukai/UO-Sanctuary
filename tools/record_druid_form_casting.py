#!/usr/bin/env python3
"""Bind learned Druid form casting to native input, transactions and saved state."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt, sha

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'druid-form-casting'


def record(check=False, full_class=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    names = ['stats-inputs.json', 'stats-native-checks.log', 'casting-run.log', 'python-tests.log', 'druid-form-casting-fixtures.json',
        'druid-form-world-fixtures.json', 'druid-form-expired-fixtures.json', 'first-attempt-body-api-integration-build.log',
        'first-attempt-corridor-native-checks.log', 'first-attempt-corridor-run.log',
        'second-attempt-94-stats-native-checks.log', 'second-attempt-94-casting-run.log', 'second-attempt-94-stats-inputs.json']
    artifacts = [STAGE / name for name in names]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log');text = path.read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 94
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    for name in ['druid-form-casting', 'druid-form-model', 'druid-form-world', 'shape-restriction', 'druid-form-rate', 'druid-form-attack', 'grizzly-ai', 'grizzly-stats', 'dire-wolf-stats']:
        assert log.count(name + '-check: passed') == 1, name
    for name in ['druid-form-casting-reload', 'druid-form-world-reload', 'shape-restriction-reload', 'druid-form-attack-reload', 'druid-form-world-snapshot', 'druid-form-expiry-stage']:
        assert log.count(name + '-check: passed') == 2, name
    for label in ['form-wolf-right', 'form-bear-quick', 'form-wolf-unshift-quick', 'form-bear-unshift-right']:
        assert log.count(label + '-check: passed') == log.count(label + '-result: passed') == 3, label
    assert log.count('druid-form-delay-result: passed') == log.count('druid-form-live-result: passed') == 1
    assert log.count('battle-orders-result: passed') == 3
    assert '2590native action/end comparisons match ten licensed records' in log
    assert '14400native action/end schedules match complete source scheduler' in log
    assert '2400native complete-source policy comparisons' in log and '14400native arithmetic comparisons' in log
    assert log.count('Druid form reload:original captured ranks/start/deadline,typed pools and20007FPinjury preserved') == 2
    assert log.count('Druid form expired reload:elapsed original deadline removed before gameplay') == 2
    saved = json.loads((STAGE / 'druid-form-casting-fixtures.json').read_text())
    assert saved['Owner'] == saved['Form']['Owner'] and saved['Form']['Form']['Ability'] == 164
    assert saved['Form']['Form']['Rank'] == saved['Form']['Form']['LycanthropyRank'] == 20
    assert saved['Life'] > 0 and saved['Mana'] == 1000 and saved['Body'] == 0x190
    for stem, contexts in [('druid-form-cast-clock', 2590), ('druid-form-payment', 1296)]:
        path = ROOT / 'tools/reference' / (stem + '-engine-evidence.json');source = json.loads(path.read_text())
        assert source['contexts'] == contexts
        for name, digest in source['sourceHashes'].items():
            assert sha(BASE / 'd2moo-pinned-source' / name) == digest
        assert sha(ROOT / source['fixture']['path']) == source['fixture']['sha256']
        assert sha(BASE / (stem + '-source-probe.cpp')) == source['generatedSourceSha256']
        assert sha(BASE / (stem + '-sanitizer.log')) == source['sanitizerLogSha256']
        assert (BASE / (stem + '-sanitizer.log')).read_text() == ''
        artifacts += [path] + [BASE / (stem + suffix) for suffix in ['-source-probe.cpp', '-source-probe.json', '-sanitizer.log', '-compiler.log']]
    frozen_raw, frozen = frozen_receipt('31ce147', 'tools/reference/druid-form-attack-native-evidence.json')
    assert frozen['verified']['passMarkers'] == 65 and frozen['verified']['fullClassMarkers'] == 1184
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name;audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        assert audit['ContentAssemblySha256'] == sha(STAGE / 'content/Sanctuary.Content.dll')
        for name, digest in audit['InputHashes'].items():
            assert sha(ROOT / name) == digest, name
        artifacts += [path, ROOT / 'tools/reference' / path.name];audits.append(audit)
    baseline = json.loads(subprocess.check_output(['git', 'show', '31ce147:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
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
    assert len(handlers) == 175 and sum(row['characterClass'] == 'Druid' for row in handlers) == 19 and status['completedClasses'] == []
    for talent in ['druid.werewolf', 'druid.werebear', 'druid.lycanthropy']:
        assert next(row for row in handlers if row['talentId'] == talent)['complete'] is False
    artifacts += [ROOT / path for path in ['tools/record_druid_form_casting.py', 'tests/check_druid_form_casting.py', 'tests/check_creeper_stats.py',
        'tools/build_class_skills.py', 'tools/build_skill_roadmap.py', 'data/Sanctuary/Balance/skill-effects.json', 'data/Sanctuary/Balance/skill-icons.json']]
    result = dict(referenceVersion='3.3.93847', installed=False, playerFormHandlersEnabled=True, lycanthropyPassiveOnly=True,
        handlers=175, druidHandlers=19, completedClasses=[], goalComplete=False,
        verified=dict(passMarkers=94, boots=3, actualReloads=2, actualInputCasts=12, inputPaths=['wolf-right-entry', 'bear-quick-entry', 'wolf-quick-zero-mana-unshift', 'bear-right-zero-mana-unshift'],
            nativeScSchedules=2590, nativeAttackSchedules=14400, nativeRateContexts=14400, shapePolicyContexts=2400, nativeLifecycleContexts=350,
            naturalSharedCooldownExpiry=1, pythonTests=70, namedDefinitions=573, inputFingerprints=[len(audit['InputHashes']) for audit in audits],
            fullClassMarkers=full_markers, fullClassBoots=3 if full_class else None, fullClassReloads=2 if full_class else None, fullClassSaveSnapshots=full_saves),
        frozenPriorAcceptance=dict(commit='31ce147', receipt='tools/reference/druid-form-attack-native-evidence.json', sha256=hashlib.sha256(frozen_raw).hexdigest(), passMarkers=65, fullClassMarkers=1184),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        established=[
            'Learned Werewolf/Werebear use self-canonicalized SC/FCR windup,15mana human entry and free same-form return,with passive effective Lycanthropy captured in life/duration. Null/offscreen/self target inputs canonicalize to the caster. Early release keeps context; actual action commits once. Shared stat/resource/attack/restriction ledgers retain proportional life and the approved native maximum projection.',
            'Both forms share one25frame deadline represented by matching keys in the existing saved cooldown ledger; ordinary zero-delay Attack remains usable. Real expiry removes the delay without removing the form. Both actual reloads preserve a separately held inspection deadline,original learned form/ranks/injury/mana/body/bindings and no replay of an unpaid saved windup. The held10minute deadline is an inspection control,not gameplay duration.',
            'Twelve actual socket casts across three boots verify right wolf entry,quick bear entry,quick wolf unshift at zero mana and right bear unshift at zero mana. Entry does not pay during windup and spends15once; return spends nothing. Left/passive bindings refuse. Stop and out-and-back movement cancel. Below15mana at release and direct cross-form/item-only routes are explicitly unsupported/refused,not claimed current D2Rbehavior.',
            'Allocation failure changes neither state,mana nor delay. The first integration compile used an obsolete test BodyValueAPI; the first native run exhausted an eastern test corridor. Reusing four verified fixture bands fixed placement without weakening world-fit checks. A94marker run preceded the final supported-rank and asset metadata refinement; the final94marker run uses the exact recorded candidate. Failure/prior logs are retained.',
            'Current source-wide weapon animations,shape restrictions,BO,summon and saved form expiry consumers remain checked. Both zero-warning builds and70Python tests pass; fresh named/affix fingerprints preserve573decisions. Main remains on the installed Grizzly build; this candidate is not installed.',
        ], remaining=[
            'Forced low-life removal,form concentration/uninterruptibility and other hurt/block/movement clocks,modern rank/gear/respec refresh/cleanup,extra-AR,current action/reentry/distance and ordinary-client presentation remain incomplete or unverified. No class is complete. Do not treat handler coverage as exact D2Rparity.',
            'Current Param12direct switching,lost-mana outcome and item-only/staffmod/foreign-class form grants are explicitly unsupported. The old rank1minimum debit and ignored failed payment are source findings,not adopted native mechanics. Current ordinary entry retains table15mana and confirmed free same-form return.',
            'Human avatar,SCpose16,flash0x373A,sound0x1E5 and melee Wrestle pose are identified placeholders. Final40/TGbody/motion/entry/return/contact/audio and talent/right/quick icons remain separate pending assets; Lycanthropy only needs its passive talent icon. Shared client SelfTargetmetadata needs the matching future client build before rollout.',
        ])
    output = ROOT / 'tools/reference/druid-form-casting-native-evidence.json';rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print(f'Learned forms:94markers/3boots/2reloads/12real input casts,2590SCschedules,70Python;fullClass={full_markers};175handlers/Druid19,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__);parser.add_argument('--check', action='store_true');parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args();record(args.check, args.full_class)
