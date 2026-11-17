#!/usr/bin/env python3
"""Verify the final scoped class release without rewriting historical evidence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import runpy
import shutil
import sys

import skill_checkpoint_evidence as e
from prepare_class_skills_handoff import hashes, write
from build_skill_roadmap import build

ROOT = e.ROOT
RUNTIME = Path('/media/aomukai/FILES/Sanctuary')
STAGE = ROOT / '.local/class-skills/class-skill-release'
CLASSES = ('necromancer', 'sorceress', 'paladin', 'barbarian', 'amazon', 'assassin', 'druid', 'warlock')


def original_world():
    original = e.load(ROOT / '.local/class-skills/class-skills-handoff/prepared-inputs.json')
    for name in ('.local/server/Saves', '.local/server/Configuration', '.local/client/Data', '.local/client/Profiles'):
        assert hashes(RUNTIME / name) == original['original'][name], name
    assert e.sha(RUNTIME / '.local/client/settings.json') == original['originalFiles']['.local/client/settings.json']


def smoke():
    original_world()
    dev = runpy.run_path(str(RUNTIME / 'dev'))
    dev['require_stopped']('server'); dev['require_stopped']('client')
    policy = e.load(ROOT / 'tools/reference/class-mechanics-release-policy.json')
    name = 'Data/Sanctuary/Balance/class-skill-status.json'
    source = ROOT / 'data/Sanctuary/Balance/class-skill-status.json'
    target = RUNTIME / '.local/server' / name
    assert e.sha(target) == policy['historicalLedgerSha256']
    backup = STAGE / 'runtime-ledger-before.json'
    assert not backup.exists()
    shutil.copy2(target, backup)
    pending = target.with_suffix('.json.release-pending')
    shutil.copy2(source, pending); assert e.sha(pending) == e.sha(source)
    pending.replace(target)
    configpath = RUNTIME / '.local/server/Configuration/modernuo.json'
    original_config = configpath.read_bytes()
    try:
        config = json.loads(original_config)
        config['settings'].update({'autosave.enabled': 'False', 'crashGuard.restartServer': 'False'})
        configpath.write_text(json.dumps(config))
        sys.path.insert(0, str(ROOT / 'tests'))
        from check_britain import Session
        with (STAGE / 'final-world-smoke.log').open('w') as log:
            session = Session(RUNTIME / '.local/server', log)
            try:
                session.expect('Listening: 127.0.0.1:2593')
                session.send('shutdown'); session.expect('Shutting down')
                assert session.process.wait(timeout=20) == 0
            finally:
                session.close()
        text = (STAGE / 'final-world-smoke.log').read_text()
        assert 'Loading world done (4698 items, 572 mobiles)' in text
        assert ' ERR]' not in text and 'Saving world' not in text
    except BaseException:
        shutil.copy2(backup, target)
        raise
    finally:
        configpath.write_bytes(original_config)
    original_world()
    write(STAGE / 'metadata-install.json', dict(metadataOnly=True, file=name,
        beforeSha256=e.sha(backup), afterSha256=e.sha(target), originalEntitiesLoaded=5290,
        originalSavesUnchanged=True, serverStopped=True))


def verify(check=False):
    policy_path = ROOT / 'tools/reference/class-mechanics-release-policy.json'
    policy = e.load(policy_path)
    ledger_path = ROOT / 'data/Sanctuary/Balance/class-skill-status.json'
    ledger = e.load(ledger_path)
    assert ledger == build()
    assert len(ledger['skills']) == 240 and all(row['complete'] for row in ledger['skills'])
    assert ledger['completedClasses'] == policy['completedClasses'] == ledger['classOrder']
    assert len({row['talentId'] for row in ledger['skills']}) == len({row['sourceId'] for row in ledger['skills']}) == 240
    authority = e.load(ROOT / policy['authority']['path'])
    assert e.sha(ROOT / policy['authority']['path']) == policy['authority']['sha256']
    assert authority['delegatedAdaptations']['approved'] and not authority['futureWorldBalance']['implemented']
    assert not policy['exactUnpublishedModernParityClaimed']
    historical = STAGE / 'before/data/Sanctuary/Balance/class-skill-status.json'
    assert e.sha(historical) == policy['historicalLedgerSha256']
    assert e.sha(STAGE / 'runtime-ledger-before.json') == policy['historicalLedgerSha256']
    current = e.load(ROOT / '.local/class-skills/find-item-adoption/birth-inputs.json')
    for name, digest in policy['productionSources'].items():
        assert e.sha(ROOT / name) == digest == current['sourceInputs'][name], name
    metadata = 'Data/Sanctuary/Balance/class-skill-status.json'
    assert e.sha(RUNTIME / '.local/server' / metadata) == e.sha(ledger_path)
    for name, digest in current['copiedData'].items():
        if name == metadata:
            assert digest == policy['historicalLedgerSha256']
        else:
            assert e.sha(RUNTIME / '.local/server' / name) == digest
            assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest
    assert {'Data/Sanctuary/' + name for name in hashes(ROOT / 'data/Sanctuary')} == set(current['copiedData'])
    assert e.sha(RUNTIME / '.local/content/Sanctuary.Content.dll') == policy['installedContentSha256']
    assert e.sha(RUNTIME / '.local/server/Assemblies/Sanctuary.Content.dll') == policy['installedContentSha256']
    assert e.load(RUNTIME / '.local/server/Data/assemblies.json') == ['UOContent.dll', 'Sanctuary.Content.dll']
    assert not (RUNTIME / '.local/server/Assemblies/Sanctuary.Integration.dll').exists()
    files = [policy_path, ledger_path, historical, STAGE / 'runtime-ledger-before.json',
        STAGE / 'final-world-smoke.log', STAGE / 'metadata-install.json']
    active, passive = 0, 0
    for name in CLASSES:
        inputs_path = ROOT / ('tools/reference/' + name + '-current-input-coverage.json')
        inputs = e.load(inputs_path)
        receipt_path = ROOT / ('tools/reference/' + name + '-lifecycle-acceptance-native-evidence.json')
        receipt = e.load(receipt_path)
        stage = ROOT / ('.local/class-skills/' + name + '-lifecycle-acceptance')
        assert inputs['artifacts'] == receipt['artifacts'] == e.load(stage / 'birth-inputs.json')['artifacts']
        assert e.markers(stage / 'birth-native-checks.log') == Counter(receipt['markers'])
        identities = {row['talentId'] for row in inputs['skills']} | {row['talentId'] for row in inputs['passiveSkills']}
        assert identities == {row['talentId'] for row in ledger['skills'] if row['talentId'].startswith(name + '.')}
        assert inputs['activeTalents'] == inputs['verifiedActiveInputRoutes']
        active += inputs['activeTalents']; passive += inputs['passiveTalents']
        files += [inputs_path, receipt_path, ROOT / ('docs/' + name + '-coverage-matrix.md')]
    assert (active, passive) == (210, 30)
    for receipt in ('class-closing-package-evidence', 'class-combat-rendered-evidence'):
        path = ROOT / ('tools/reference/' + receipt + '.json')
        for name, digest in e.load(path)['files'].items():
            assert e.sha(ROOT / name) == digest, name
        files.append(path)
    for receipt in ('class-roster-ui-evidence', 'necromancer-pet-policy-audit', 'druid-pet-policy-audit',
                    'necromancer-rendered-evidence', 'bash-flat-adoption-native-evidence', 'find-item-adoption-evidence'):
        files.append(ROOT / ('tools/reference/' + receipt + '.json'))
    for name in ('skill-effects', 'skill-icons'):
        path = ROOT / ('data/Sanctuary/Balance/' + name + '.json')
        rows = e.load(path)
        assert len(rows) == 240 and all(row['placeholder'] for row in rows)
        assert {row['talentId'] for row in rows} == set(policy['talentIds'])
        files.append(path)
    original_world()
    dev = runpy.run_path(str(RUNTIME / 'dev'))
    dev['require_stopped']('server'); dev['require_stopped']('client')
    files += [ROOT / name for name in ('tools/build_skill_roadmap.py', 'docs/class-skill-roadmap.md',
        'docs/class-skills-complete.md', 'docs/class-mechanics-supported-contexts.md',
        'README.md', 'docs/class-skill-status-overview.md', 'docs/implementation-roadmap.md')]
    files.append(Path(__file__))
    e.write('class-skill-release-evidence.json', dict(referenceVersion='3.3.93847',
        mechanicallyQualified=True, qualificationScope='approved-sanctuary-pve',
        completedClasses=policy['completedClasses'], talents=240, activeRoutes=210, passiveEntries=30,
        dedicatedClassEvidencePreserved=True, allEightRenderedRepresentatives=True,
        clientPatches=45, originalEntitiesLoaded=5290, originalSaveFilesUnchanged=38,
        metadataOnlyLedgerPromotion=True, numericalDataChanged=False,
        finalAssetsPending=True, exactUnpublishedModernParityClaimed=False,
        deferred=policy['deferred'], goalRequirementsSatisfied=True), files, check)
    print('Final release:8classes/240talents/210active routes/30passives;installed code and all8client representatives;original saves intact;limits and assets explicit.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--smoke', action='store_true')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    if args.smoke:
        smoke()
    else:
        verify(args.check)
