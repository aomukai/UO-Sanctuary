#!/usr/bin/env python3
"""Bind pure native player-form acceptance; retain installed Grizzly evidence separately."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'druid-form-model'
FROZEN = '40e8daa'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def historical(name):
    return subprocess.check_output(['git', 'show', FROZEN + ':' + name], cwd=ROOT)


def record(check=False):
    inputs = json.loads((STAGE / 'audit-inputs.json').read_text())
    expected = {str(p.relative_to(ROOT)) for folder in ['src/Sanctuary.Content', 'shared', 'tests/Sanctuary.Integration'] for p in (ROOT / folder).glob('*.cs')}
    assert {name for name in inputs['sourceInputs'] if name.endswith('.cs')} == expected
    for name, digest in inputs['sourceInputs'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in inputs['copiedData'].items():
        assert sha(ROOT / 'data/Sanctuary' / name.removeprefix('Data/Sanctuary/')) == digest, name
    artifacts = [STAGE / name for name in ['audit-inputs.json', 'audit-native-checks.log', 'model-run.log', 'python-tests.log', 'first-attempt-stale-audit-python.log']]
    for kind in ['content', 'integration']:
        name = 'Sanctuary.' + kind.title() + '.dll'
        assert sha(STAGE / kind / name) == inputs['artifacts']['Assemblies/' + name]
        path = STAGE / (kind + '-build.log')
        text = path.read_text()
        assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
        artifacts += [path, STAGE / kind / name]
    log = (STAGE / 'audit-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed') == 6
    assert log.count('Shutting down') == 1 and 'Writing world save snapshot done' not in log
    for command in ['druid-form-model', 'grizzly-ai', 'grizzly-stats', 'dire-wolf-stats', 'named-gear', 'first-loop-catalogue']:
        assert log.count(command + '-check: passed') == 1, command
    assert '350native player lifecycle/stat contexts match complete source;150entries/150unshifts/50allocation failures/150expiry+JSON controls' in log
    python = (STAGE / 'python-tests.log').read_text()
    assert 'Ran 70 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    stale = (STAGE / 'first-attempt-stale-audit-python.log').read_text()
    assert 'Incomplete runtime input inventory: reexport after adding/removing sources' in stale
    source_path = ROOT / 'tools/reference/druid-forms-engine-evidence.json'
    source = json.loads(source_path.read_text())
    assert source['referenceVersion'] == '3.3.93847' and source['contexts'] == 1000 and not source['nativeAccepted']
    assert sha(ROOT / 'tools/reference/druid-forms-preflight.json') == source['preparationSha256']
    assert sha(BASE / 'druid-forms-source-probe.cpp') == source['generatedSourceSha256']
    assert sha(BASE / 'druid-forms-source-contexts.json') == sha(BASE / 'druid-forms-source-probe.json') == source['fixtureSha256']
    assert sha(BASE / 'druid-forms-source-sanitizer.log') == source['sanitizerLogSha256']
    assert (BASE / 'druid-forms-source-sanitizer.log').read_text() == ''
    artifacts += [BASE / name for name in ['druid-forms-source-probe.cpp', 'druid-forms-source-contexts.json', 'druid-forms-source-probe.json', 'druid-forms-source-sanitizer.log']]
    # These full/regression/saved checks belong to the immutable installed
    # predecessor, never to this one-boot unused-helper candidate.
    frozen_path = 'tools/reference/grizzly-casting-native-evidence.json'
    frozen_bytes = historical(frozen_path)
    frozen = json.loads(frozen_bytes)
    for name, digest in frozen['artifacts'].items():
        raw = (ROOT / name).read_bytes() if name.startswith('.local/') else historical(name)
        assert hashlib.sha256(raw).hexdigest() == digest, name
    for name, digest in frozen['inputInventory']['sourceInputs'].items():
        raw = (ROOT / name).read_bytes() if name.startswith('.local/') else historical(name)
        assert hashlib.sha256(raw).hexdigest() == digest, name
    assert frozen['verified']['fullClassMarkers'] == 1184 and frozen['verified']['actualReloads'] == 2
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
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    handlers = [row for row in status['skills'] if row['implementation'] == 'handler-present']
    assert len(handlers) == 172 and sum(row['characterClass'] == 'Druid' for row in handlers) == 16 and status['completedClasses'] == []
    for talent in ['druid.werewolf', 'druid.werebear', 'druid.lycanthropy']:
        assert next(row for row in status['skills'] if row['talentId'] == talent)['implementation'] != 'handler-present'
    artifacts += [ROOT / name for name in ['tools/record_druid_form_model.py', 'tools/prepare_druid_forms_reference.py', 'tools/probe_druid_forms_reference.py', 'tests/check_druid_form_model.py', 'tests/export_runtime_audits.py', 'tools/reference/druid-forms-engine-evidence.json', 'tools/reference/druid-forms-preflight.json', 'tools/reference/druid-forms-user-observations.json']]
    result = dict(referenceVersion='3.3.93847', installed=False, playerFormHandlersEnabled=False, handlers=172, druidHandlers=16, completedClasses=[], goalComplete=False,
        verified=dict(nativePlayerContexts=350, entries=150, unshifts=150, allocationFailures=50, expiryAndJsonControls=150, sourceContexts=1000,
            passMarkers=6, boots=1, actualReloads=0, pythonTests=70, namedDefinitions=573, inputFingerprints=[len(audit['InputHashes']) for audit in audits]),
        frozenFullAcceptance=dict(commit=FROZEN, receipt=frozen_path, sha256=hashlib.sha256(frozen_bytes).hexdigest(), package='.local/class-skills/grizzly-casting', passMarkers=267, fullClassMarkers=1184, boots=3, actualReloads=2),
        inputInventory=inputs, artifacts={str(path.relative_to(ROOT)):sha(path) for path in artifacts},
        established=[
            'Pure native player form captures typed stamina,attackrate/OTHER_ANIMRATE,AR,physical damage,armor,life and concentration channels through existing SkillValues and current data. Captured own/effective Lycanthropy ranks and start/end frames validate against recomputed modifiers; no ad-hoc stat defaults or absent-skill baseline donation.',
            'All350supported player contexts match complete legacy source final stat/lifetime/mode/zero-mana boundaries:150entries,150exclusive-group unshifts,50allocation failures and150expiry/JSON controls. Same/cross-group removal precedes allocation and requests25frames of delay; successful entry leaves its mana/global delay to the outer handler. The user confirms free same-form unshift.',
            'Monster contexts,corrupt table/state rows and orphaned inactive stat lists are outside the production player-state domain; they are not counted as native comparisons. Malformed captured modifiers/ranks/lifetimes reject explicitly. No live world caller,HP mutation,attack-rate consumer,save profile or learned handler is enabled.',
            'The user confirms proportional current life when entering a form. Existing player proportional-resource helpers are reuse candidates; voluntary/expired/low-life removal and rounding remain separate. The native helper does not yet mutate any resource pool.',
            'One disposable boot retains Grizzly AI/birth,Dire Wolf birth and real named/affix consumers. Reexported654/21fingerprints preserve573named-item decisions. The first Python run correctly refused stale central fingerprints after adding a source; fresh native exports restore all70tests without weakening that guard.',
        ], remaining=[
            'Connect saved player form/stat/resource projection with confirmed proportional entry life and bounded expiry/unshift behavior. Resolve life percentage buckets,stamina,gear/BO/aura stacking,post-2.4attack-rate composition,form-specific attack/cast/block/hurt/movement clocks and skill/weapon restrictions before accepting those consumers.',
            'Learned15mana/right+quick/free-unshift transactions,SC versus reverse sequence22,cancellation/global25frame delay,failed payment rollback and actual save/offline/death/reload remain unfinished. Cross-form modern selection and rank1payment versus the legacy minimum-mana quirk remain explicit reference gaps.',
            'Earlier full1184marker/3boot/2reload acceptance belongs only to frozen40e8daaGrizzly package. Main remains installed on that preserved package; no new full or saved-world claim is made for this unused pure helper. Final player-form effects/audio and separate talent/right/quick/passive icons remain pending.',
        ])
    path = ROOT / 'tools/reference/druid-form-model-native-evidence.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == rendered
    else:
        path.write_text(rendered)
    print('Pure Druid forms350native comparisons,6markers/1boot/no reloads,70Python tests;172handlers/Druid16,not installed.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
