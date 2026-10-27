#!/usr/bin/env python3
"""Bind live shape guards and pure modern form arithmetic to their exact package."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'shape-restrictions'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def frozen_receipt(commit, name):
    def read(path):
        return (ROOT / path).read_bytes() if path.startswith('.local/') else subprocess.check_output(['git', 'show', commit + ':' + path], cwd=ROOT)
    raw = read(name)
    receipt = json.loads(raw)
    for path, digest in receipt['artifacts'].items():
        assert hashlib.sha256(read(path)).hexdigest() == digest, (commit, path)
    for path, digest in receipt['inputInventory']['sourceInputs'].items():
        assert hashlib.sha256(read(path)).hexdigest() == digest, (commit, path)
    return raw, receipt


def record(check=False, full_class=False):
    inventory.STAGE = STAGE
    inputs = inventory.validate_inventory(STAGE / 'stats-inputs.json')
    artifacts = [STAGE / name for name in ['stats-inputs.json', 'stats-native-checks.log', 'restrictions-run.log', 'python-tests.log',
        'druid-form-world-fixtures.json', 'druid-form-expired-fixtures.json']]
    for kind in ['content', 'integration']:
        path = STAGE / (kind + '-build.log')
        text = path.read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 62
    assert log.count('Shutting down') == log.count('Writing world save snapshot done') == 3
    for name in ['druid-form-model', 'druid-form-world', 'shape-restriction', 'druid-form-rate', 'grizzly-ai', 'grizzly-stats', 'dire-wolf-stats']:
        assert log.count(name + '-check: passed') == 1, name
    for name in ['druid-form-world-reload', 'shape-restriction-reload', 'druid-form-world-snapshot', 'druid-form-expiry-stage']:
        assert log.count(name + '-check: passed') == 2, name
    assert log.count('druid-form-live-result: passed') == 1 and log.count('battle-orders-result: passed') == 3
    assert '2400native complete-source policy comparisons' in log
    assert '14400native arithmetic comparisons' in log and '4800IAS totals retain partition invariance' in log
    assert log.count('Druid form expired reload:elapsed original deadline removed before gameplay') == 2
    assert log.count('Druid form reload:original captured ranks/start/deadline,typed pools and20007FPinjury preserved') == 2
    saved = json.loads((STAGE / 'druid-form-world-fixtures.json').read_text())
    form = saved['Form']['Form']
    assert saved['Owner'] == saved['Form']['Owner'] and form['Rank'] == form['LycanthropyRank'] == 20
    assert saved['MaximumLife'] * 256 - saved['Life256'] == 20007
    source_path = ROOT / 'tools/reference/shape-restrictions-engine-evidence.json'
    source = json.loads(source_path.read_text())
    assert source['contexts'] == 2400 and source['guards'] == 9 and source['corroboratedFields'] == 1192
    for path, digest in source['sourceHashes'].items():
        path = BASE / 'd2moo-pinned-source' / path
        assert sha(path) == digest
        artifacts.append(path)
    assert sha(ROOT / source['fixture']['path']) == source['fixture']['sha256']
    assert sha(BASE / 'shape-restrictions-source-probe.cpp') == source['generatedSourceSha256']
    assert sha(BASE / 'shape-restrictions-sanitizer.log') == source['sanitizerLogSha256']
    assert (BASE / 'shape-restrictions-sanitizer.log').read_text() == ''
    artifacts += [source_path] + [BASE / name for name in ['shape-restrictions-source-probe.cpp', 'shape-restrictions-source-probe.json', 'shape-restrictions-sanitizer.log']]
    progress_path = ROOT / 'tools/reference/modern-form-progress-source-evidence.json'
    progress = json.loads(progress_path.read_text())
    rate_path = ROOT / progress['predecessor']['path']
    assert sha(rate_path) == progress['predecessor']['sha256']
    rates = json.loads(rate_path.read_text())
    assert progress['contexts'] == rates['contexts'] == 14400 and rates['distinctIasContexts'] == 4800
    for name, digest in progress['sourceFiles'].items():
        path = ROOT / rates['source']['cache'] / name
        assert sha(path) == digest
        artifacts.append(path)
    assert sha(ROOT / progress['fixture']['path']) == progress['fixture']['sha256']
    assert sha(BASE / 'modern-form-progress-source-probe.mjs') == progress['harnessSha256']
    assert sha(BASE / 'modern-form-progress-source-probe.log') == progress['stderrSha256']
    artifacts += [progress_path, rate_path, ROOT / progress['fixture']['path'], BASE / 'modern-form-progress-source-probe.mjs', BASE / 'modern-form-progress-source-probe.log']
    old_raw, old = frozen_receipt('ed6b583', 'tools/reference/druid-form-modes-native-evidence.json')
    assert old['verified']['nativePlayerContexts'] == 350 and old['verified']['playerCastMode'] == 10
    linked = old['frozenWorldAcceptance']
    world_raw, world = frozen_receipt(linked['commit'], linked['receipt'])
    assert hashlib.sha256(world_raw).hexdigest() == linked['sha256']
    assert world['verified']['passMarkers'] == 58 and world['verified']['fullClassMarkers'] == 1184
    assert world['verified']['boots'] == 3 and world['verified']['actualReloads'] == 2
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
    baseline = json.loads(subprocess.check_output(['git', 'show', 'ed6b583:tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
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
    assert len(handlers) == 172 and sum(row['characterClass'] == 'Druid' for row in handlers) == 16 and status['completedClasses'] == []
    for talent in ['druid.werewolf', 'druid.werebear', 'druid.lycanthropy']:
        assert next(row for row in status['skills'] if row['talentId'] == talent)['implementation'] != 'handler-present'
    artifacts += [ROOT / name for name in ['tools/record_shape_restrictions.py', 'tests/check_shape_restrictions.py', 'tools/probe_modern_form_progress.py']]
    result = dict(referenceVersion='3.3.93847', installed=False, playerFormHandlersEnabled=False, handlers=172, druidHandlers=16, completedClasses=[], goalComplete=False,
        verified=dict(passMarkers=62, boots=3, actualReloads=2, shapePolicyContexts=2400, nativeRateArithmeticContexts=14400, iasPartitionContexts=4800,
            liveShapeGuards=True, liveRateClock=False, currentExecutableParity=False, pythonTests=70, namedDefinitions=573,
            inputFingerprints=[len(audit['InputHashes']) for audit in audits], fullClassMarkers=full_markers,
            fullClassBoots=3 if full_class else None, fullClassReloads=2 if full_class else None, fullClassSaveSnapshots=full_saves),
        frozenModeAcceptance=dict(commit='ed6b583', receipt='tools/reference/druid-form-modes-native-evidence.json', sha256=hashlib.sha256(old_raw).hexdigest(), frozenWorldAcceptance=linked),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        established=[
            'Source restrict0/1/2 policy now guards actual availability, casting start and release, including direct Druid Firestorm release. Starting Firestorm in human form and shifting during its unpaid windup cancels without payment. Allowed transformed Cyclone Armor and Raven still cast and pay once; rank, mana and freeze checks remain in force.',
            'All240talents across five state contexts and both mask flags match2400complete-source results. Missing state sentinels stop the list immediately. Basic Attack remains shape-eligible independently of weapon/ammunition constraints; unsupported transformations are not inferred.',
            'Independently written single-weapon standard-attack arithmetic compares actual progress, EIAS and cycles against14400pinned3.3author-calculator outputs and4800IAS partition contexts, using the current human A1lengths. This supplementary model has no live animation/event/impact caller and does not certify the current executable.',
            'Three boots and two actual reloads retain saved forms, original deadlines and injury, live/offline expiry, Battle Orders and summon consumers. Fresh named/affix audits retain573decisions;70Python tests and both zero-warning builds pass. Approved whole native maxima remain unchanged.',
        ], remaining=[
            'Connect modern rate arithmetic to actual form A1clock and impact, then melee-only weapon/range/ammunition behavior and source AR/SrcDam. Current extra-AR runtime and Param12cross-form transitions remain explicitly uncertain; no user respec or answer is required for unaffected work.',
            'Form SC/reverseS1casting,15mana/free-same-form-unshift/global25frame delay,right/quick input,cancellation and remaining movement/block/hurt/concentration clocks are unfinished. Learned forms remain disabled and main stays3c0c61bon the preserved Grizzly installation.',
            'Final40/TGbody,motion,transformation/contact effects and audio remain identified placeholders. Talent/right/quick/passive icons are recorded separately. No class completion or complete D2R-equivalence claim is made.',
        ])
    output = ROOT / 'tools/reference/shape-restrictions-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print(f'Shape guards:2400policy/14400rate arithmetic,62markers/3boots/2reloads,70Python; fullClass={full_markers};172handlers/Druid16,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--full-class', action='store_true')
    args = parser.parse_args()
    record(args.check, args.full_class)
