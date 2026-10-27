#!/usr/bin/env python3
"""Bind prepared saved player-form pools to native lifecycle and retained evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'druid-form-world'
FROZEN = '76727ed'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def historical(name):
    return subprocess.check_output(['git', 'show', FROZEN + ':' + name], cwd=ROOT)


def record(check=False, full_class=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    names = ['stats-inputs.json', 'stats-native-checks.log', 'world-run.log', 'python-tests.log',
        'druid-form-world-fixtures.json', 'druid-form-expired-fixtures.json',
        'first-attempt-bo-mana-world-run.log', 'second-attempt-async-bo-stats-native-checks.log', 'third-attempt-controlled-54-stats-native-checks.log']
    artifacts = [STAGE / name for name in names]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        text = path.read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 58
    assert log.count('Writing world save snapshot done') == log.count('Shutting down') == 3
    assert log.count('druid-form-world-check: passed') == log.count('druid-form-model-check: passed') == 1
    assert log.count('druid-form-world-reload-check: passed') == log.count('druid-form-world-snapshot-check: passed') == 2
    assert log.count('druid-form-expiry-stage-check: passed') == 2 and log.count('druid-form-live-result: passed') == 1
    assert log.count('Druid form expired reload:elapsed original deadline removed before gameplay') == 2
    assert log.count('Druid form reload:original captured ranks/start/deadline,typed pools and20007FPinjury preserved') == 2
    assert log.count('battle-orders-result: passed') == 3
    for name in ['grizzly-ai', 'grizzly-stats', 'dire-wolf-stats']:
        assert log.count(name + '-check: passed') == 1
    saved = json.loads((STAGE / 'druid-form-world-fixtures.json').read_text())
    form = saved['Form']['Form']
    assert saved['Owner'] == saved['Form']['Owner'] and form['Rank'] == form['LycanthropyRank'] == 20
    assert form['Modifiers']['DamagePercent'] == 340 and form['Modifiers']['LifePercent'] == 190
    assert saved['MaximumLife'] * 256 - saved['Life256'] == 20007
    expired = json.loads((STAGE / 'druid-form-expired-fixtures.json').read_text())
    assert expired['Owner'] == expired['Form']['Owner'] and expired['Form']['Form']['Rank'] == 1
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    # The older one-boot pure model and full Grizzly predecessor stay attached
    # to their own immutable source/package identities.
    frozen_path = 'tools/reference/druid-form-model-native-evidence.json'
    frozen_bytes = historical(frozen_path)
    frozen = json.loads(frozen_bytes)
    for name, digest in frozen['artifacts'].items():
        raw = (ROOT / name).read_bytes() if name.startswith('.local/') else historical(name)
        assert hashlib.sha256(raw).hexdigest() == digest, name
    for name, digest in frozen['inputInventory']['sourceInputs'].items():
        raw = (ROOT / name).read_bytes() if name.startswith('.local/') else historical(name)
        assert hashlib.sha256(raw).hexdigest() == digest, name
    assert frozen['verified']['nativePlayerContexts'] == 350 and frozen['frozenFullAcceptance']['fullClassMarkers'] == 1184
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
    baseline = json.loads(historical('tools/reference/named-item-runtime-audit.json'))
    assert len(audits[0]['Definitions']) == 573 and audits[0]['Definitions'] == baseline['Definitions']
    full_markers = full_saves = None
    if full_class:
        full_inputs = inventory.validate_inventory(STAGE / 'class-inputs.json')
        assert full_inputs['artifacts'] == inputs['artifacts'] and full_inputs['copiedData'] == inputs['copiedData']
        text = (STAGE / 'class-native-checks.log').read_text()
        assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down') == 3
        assert 'including two save/reload cycles.' in (STAGE / 'full-run.log').read_text()
        assert text.count('named-gear-check: passed') == 1 and text.count('named-gear-reload-check: passed') == 2
        full_markers = text.count(': passed');full_saves = text.count('Writing world save snapshot done')
        assert full_markers == 1184 and full_saves >= 3
        for name, expected in [('full-named-item-runtime-audit.json', audits[0]), ('full-first-loop-affix-audit.json', audits[1])]:
            path = STAGE / name;assert json.loads(path.read_text()) == expected;artifacts.append(path)
        artifacts += [STAGE / name for name in ['class-inputs.json', 'class-native-checks.log', 'full-run.log']]
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [row for row in status['skills'] if row['implementation'] == 'handler-present']
    assert len(handlers) == 172 and sum(row['characterClass'] == 'Druid' for row in handlers) == 16 and status['completedClasses'] == []
    for talent in ['druid.werewolf', 'druid.werebear', 'druid.lycanthropy']:
        assert next(row for row in status['skills'] if row['talentId'] == talent)['implementation'] != 'handler-present'
    artifacts += [ROOT / name for name in ['tools/record_druid_form_world.py', 'tests/check_druid_form_world.py', 'tests/check_creeper_stats.py',
        'tools/review_druid_form_resources.py', 'tools/reference/druid-form-resource-review.json', 'tools/reference/druid-form-removal-user-observations.json',
        'tools/reference/druid-forms-compatibility-review.json']]
    result = dict(referenceVersion='3.3.93847', installed=False, playerFormHandlersEnabled=False, handlers=172, druidHandlers=16, completedClasses=[], goalComplete=False,
        verified=dict(passMarkers=58, boots=3, actualReloads=2, strictSnapshotGuards=2, naturalTimerExpiry=1, offlineExpiredReloads=2,
            nativePlayerContexts=350, pythonTests=70, namedDefinitions=573, inputFingerprints=[len(audit['InputHashes']) for audit in audits],
            fullClassMarkers=full_markers, fullClassBoots=3 if full_class else None, fullClassReloads=2 if full_class else None, fullClassSaveSnapshots=full_saves),
        frozenModelAcceptance=dict(commit=FROZEN, receipt=frozen_path, sha256=hashlib.sha256(frozen_bytes).hexdigest(), package='.local/class-skills/druid-form-model', nativePlayerContexts=350, passMarkers=6, boots=1, actualReloads=0),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        established=[
            'Prepared saved player form uses an immutable validated owner/rank/Lycan/modifier/UTC ledger and separate profile. Form life/stamina join existing ClassResources percentage pools,AR joins the player item-percent pool,bear physical damage joins shared weapon percent and armor joins existing skill defense. Attackrate/concentration remain captured but unconsumed.',
            'Existing BattleOrders capture/change rescales projected life/stamina atomically entering,clearing and expiring forms. Current source percentage pools stack additively with BO; form entry/removal leaves mana unchanged,whereas BO removal retains its own proportional mana change. Both direction life ratios are user-confirmed; exact maximum/rounding precision remains open.',
            'Actual Tarnhelm captures own/effective Lycan ranks; this bounded snapshot remains after gear loss. Duplicate/corrupt saved rows reject atomically. Stop/freeze/movement retain the established state; death/deletion prune it. No body/protection/invisibility change is introduced; human appearance is a preparation placeholder.',
            'One real40ms timer removes an elapsed consistent old cast. Two actual offline-expired reloads remove the original deadline before gameplay with proportional projected pools,no renewal/healing. Both actual reloads preserve a separate live rank20bear,its original start/deadline,typed maxima and20007FPinjury,71FPmana injury and31FPstamina injury without refresh or duplicate scaling.',
            'The prepared factory requires a live trained Druid and entering from human. It refuses replacement rather than guessing current Param12cross-form semantics. No learned casting/mana transaction,input,body/rate/restriction or oskill handler is enabled. Coverage remains172/Druid16and zero completed classes.',
            'Retained source/model,summon,BO,named and affix consumers pass. The first failure was a test assuming BO removal cannot rescale mana; the second exposed an asynchronous BO result running behind heavy commands. The shared harness now optionally waits for explicit results,with unchanged defaults for other callers. All70Python tests pass and655/21fingerprints preserve573named decisions.',
        ], remaining=[
            'Current managed-player NativeMaximum truncates FP256maxima before SkillHealth reconstructs them from integer HitsMax. The pinned statcost/source review proves this shared precision gap; resolve a consistent exact player-resource/storage/display contract before certifying or enabling complete forms. Projected ratio/injury tests do not certify full source maxima or rounding.',
            'Current direct cross-form Param12eligibility/payment/timing,post-2.4attack rates,transformed basic extraAR/SrcDam,skill/weapon restrictions and form-specific cast/block/hurt/movement clocks remain unfinished. Captured attackrate/concentration need real consumers; low-life forced removal and modern stat-refresh transport remain separate.',
            'Learned15mana/right+quick/free-same-form-unshift/cancellation/global25frame delay and casting rollback remain pending. Final models40/TGbody/motion/transformation/contact/audio and separate tree/right/quick/passive icons remain placeholders. Main stays on the preserved Grizzly installation; this component is not a finished class or current-executable certificate.',
        ])
    path = ROOT / 'tools/reference/druid-form-world-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print(f'Prepared saved Druid forms58markers/3boots/2reloads,natural+offline expiry,70Python tests; fullClass={full_markers};172handlers/Druid16,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
