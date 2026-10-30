#!/usr/bin/env python3
"""Accept learned Revive only after all frozen build9 scopes have finished."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/revive-learned-casting'
FULL = STAGE / 'full-regression'
SNAPSHOT = STAGE / 'pre-build-attempt-9.json'
PREDECESSOR = '16a8e5a'
sha = inventory.sha


def markers(log):
    return Counter(re.findall(r'\] ([A-Za-z0-9-]+): passed', log))


def checked_log(path, count, saves=3):
    log = path.read_text()
    assert ': failed' not in log and ' ERR]' not in log, path
    assert sum(markers(log).values()) == count, path
    assert log.count('Shutting down') == 3 and log.count('Writing world save snapshot done') == saves, path
    return log


def record(check=False):
    inventories = []
    for stage, name in [(STAGE, 'cast-inputs.json'), (STAGE, 'consumers-inputs.json'),
                        (STAGE, 'stats-inputs.json'), (FULL, 'class-inputs.json')]:
        inventory.STAGE = stage
        item = inventory.validate_inventory(stage / name)
        check_snapshot(SNAPSHOT, stage / name)
        inventories.append(item)
    inventory.STAGE = STAGE
    tested = inventories[2]
    assert all(i['artifacts'] == tested['artifacts'] and i['copiedData'] == tested['copiedData'] for i in inventories)
    _, previous = frozen_receipt(PREDECESSOR, 'tools/reference/revive-world-transfer-evidence.json')
    assert previous['verified']['passMarkers'] == 335 and previous['verified']['nativePatchCount'] == 70
    for name in ['ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll']:
        assert tested['artifacts'][name] == previous['inputInventory']['artifacts'][name]
    native_manifest = ROOT / '.local/class-skills/revive-native-melee/native-pre-build-inputs.json'
    native = json.loads(native_manifest.read_text())
    assert len(native['patches']) == 70
    assert set(native['patches']) == {str(p.relative_to(ROOT)) for p in (ROOT / 'patches/ModernUO').glob('*.patch')}
    for name, value in native['patches'].items():
        assert sha(ROOT / name) == value
    artifacts = [Path(__file__).resolve(), SNAPSHOT, native_manifest]
    for kind in ['content', 'integration']:
        path = STAGE / f'{kind}-build-attempt-9.log'
        assert all(s in path.read_text() for s in ['Build succeeded.', '0 Warning(s)', '0 Error(s)'])
        artifacts += [path, STAGE / kind / ('Sanctuary.' + kind.title() + '.dll')]
    isolated = checked_log(STAGE / 'cast-native-checks.log', 3)
    assert markers(isolated) == Counter({'revive-cast-check': 1, 'revive-cast-reload-check': 2})
    consumers = checked_log(STAGE / 'consumers-native-checks.log', 12)
    assert markers(consumers) == Counter({
        'paladin-travel-check': 1, 'paladin-travel-result': 1,
        'heaven-fist-check': 1, 'heaven-fist-result': 1,
        'paladin-travel-reload-check': 2, 'paladin-travel-reload-result': 2,
        'heaven-fist-reload-check': 2, 'heaven-fist-reload-result': 2})
    retained = checked_log(STAGE / 'stats-native-checks.log', 338)
    prior_retained = (ROOT / '.local/class-skills/revive-world-transfer/stats-native-checks.log').read_text()
    assert markers(retained) == markers(prior_retained) + markers(isolated)
    complete = checked_log(FULL / 'class-native-checks.log', 1184, 35)
    prior_full = (ROOT / '.local/class-skills/revive-native-melee/full-regression/class-native-checks.log').read_text()
    assert markers(complete) == markers(prior_full)
    # These are separate worlds. Their serials may differ, but each persisted
    # active/pending pair must remain distinct and carry its independent loot.
    for name in ['isolated-revive-cast-fixtures.json', 'revive-cast-fixtures.json']:
        fixture = json.loads((STAGE / name).read_text())
        assert fixture['Active'] != fixture['PendingActor'] and fixture['ActiveCorpse'] != fixture['Pending']
        assert fixture['Life256'] > 0 and fixture['Mana'] >= 0 and fixture['Stamina'] >= 0
    audits = []
    for name in ['named-item-runtime-audit.json', 'first-loop-affix-audit.json']:
        path = STAGE / name
        audit = json.loads(path.read_text())
        assert audit == json.loads((ROOT / 'tools/reference' / name).read_text())
        exported = FULL / name
        full_audit = json.loads(exported.read_text())
        if 'ContentAssemblyPath' in audit:
            assert (ROOT / full_audit['ContentAssemblyPath']).resolve(strict=True) == (ROOT / audit['ContentAssemblyPath']).resolve(strict=True)
            full_audit['ContentAssemblyPath'] = audit['ContentAssemblyPath']
        assert full_audit == audit
        assert audit['ContentAssemblySha256'] == tested['artifacts']['Assemblies/Sanctuary.Content.dll']
        for source, value in audit['InputHashes'].items():
            assert sha(ROOT / source) == value, source
        audits.append(audit)
        artifacts += [path, exported, ROOT / 'tools/reference' / name]
    old_named = json.loads(subprocess.check_output(['git', 'show', PREDECESSOR + ':tools/reference/named-item-runtime-audit.json'], cwd=ROOT))
    assert audits[0]['Definitions'] == old_named['Definitions'] and len(audits[0]['Definitions']) == 573
    assert [len(a['InputHashes']) for a in audits] == [781, 21]
    python = STAGE / 'python-tests-attempt-9.log'
    assert 'Ran 71 tests' in python.read_text() and '\nOK\n' in python.read_text() and 'FAILED' not in python.read_text()
    installed_path = ROOT / 'tools/reference/creeper-frame-installation-evidence.json'
    installed = json.loads(installed_path.read_text())
    assert len(installed['installedArtifacts']) == 106
    for name, value in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == value, name
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert sum(s['implementation'] == 'handler-present' for s in status['skills']) == 185 and status['completedClasses'] == []
    artifacts += [python, installed_path]
    artifacts += [STAGE / name for name in ['cast-inputs.json', 'cast-native-checks.log', 'isolated-revive-cast-fixtures.json',
        'consumers-inputs.json', 'consumers-native-checks.log', 'consumers-paladin-travel-checks.json', 'consumers-heaven-fist-checks.json',
        'stats-inputs.json', 'stats-native-checks.log', 'revive-cast-fixtures.json', 'retained-run-attempt-9.log']]
    artifacts += [FULL / name for name in ['class-inputs.json', 'class-native-checks.log', 'run.log']]
    artifacts += [ROOT / name for name in ['tests/check_revive_casting.py', 'tests/check_revive_casting_regression.py',
        'tests/check_revive_corpse_consumers.py', 'tools/build_skill_roadmap.py', 'tools/reference/revive-native-behavior-policy.json']]
    result = dict(referenceVersion='3.3.93847', component='Learned Revive and retained-original corpse consumers',
        installed=False, handlerEnabled=True, automaticRetentionEnabled=True, goalComplete=False, handlers=185, completedClasses=[],
        verified=dict(isolatedMarkers=3, consumerMarkers=12, retainedMarkers=338, fullClassMarkers=1184,
            bootsPerScope=3, actualReloadsPerScope=2, fullClassSaves=35, pythonTests=71,
            nativePatchCount=70, nativeBinariesReused=True, inputFingerprints=[781, 21], unchangedNamedDefinitions=573,
            unchangedMainInstallationArtifacts=106),
        frozenPredecessor=dict(commit=PREDECESSOR, receipt='tools/reference/revive-world-transfer-evidence.json'),
        inputInventory=tested, isolatedInputInventory=inventories[0], consumerInputInventory=inventories[1], fullClassInputInventory=inventories[3],
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in sorted(set(artifacts))},
        established=[
            'Learned Revive uses actual corpse selection, right/quick binding and public hotkey dispatch, shared windup/resolve and once-only payment. Automatic accepted-death capture retains the same original NPC and restores its captured mana/stamina after native cleanup; native parked resources may subsequently regenerate within their maxima.',
            'Movement, competing corpse reservation, insufficient release mana and duplicate/consumed releases do not charge or create another original. Missing legacy eligibility, disabled proposals and unsupported equipped-item combat effects refuse explicitly. No substitute actor, ability whitelist or expiration timer is added.',
            'Consumed or unusable legacy dead-unit projections retire their own inactive originals independently of loot. Redemption verifies the retained original and loot-preserving retirement. Fist of the Heavens bursts for retained dead primaries and explicitly removed primaries. Actual native reloads retain the active/pending originals and corpse metadata.',
            'All four scopes use identical content/integration/native DLL hashes and copied data. Full regression retains every predecessor marker; fresh named/affix exports agree and existing installed artifacts are unchanged.'],
        remaining=[
            'No skill/class completion certificate: source regeneration, facet scaling, actual Revive respec acceptance, Summon Resist reference conflict, inherited sunder and complete independent item/stat/proc consumers remain unfinished or unverified.',
            'Native monster repertoires are inherited as approved. Global UO-to-D2 monster spell conversion is a separate later task. Original NPC balance/AI details not already accepted remain explicit parity work.',
            'No deployment is performed here. Existing native summon particles/audio are identified placeholders; final effects/audio and talent/hotkey icons remain separate registers.'])
    output = ROOT / 'tools/reference/revive-learned-casting-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    print('Learned Revive:3isolated/12consumer/338retained/1184full/71Python;frozen build9 accepted;remaining mechanics and final assets stay open.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
