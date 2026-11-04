#!/usr/bin/env python3
"""Bind an actual old-writer/current-reader Necromancer upgrade, without deployment."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess

from record_grizzly_body import sha
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/necro-legacy-upgrade'
PARENT = 'edad1e52a21687e147b5754e8c57c3dffd0f898e'


def load(path):
    return json.loads(path.read_text())


def hashes(folder):
    return {str(p.relative_to(folder)):sha(p) for p in sorted(folder.rglob('*')) if p.is_file()}


def record(check=False):
    saved = snapshot(STAGE / 'pre-build-2.json')['inputs']
    previous = load(ROOT / 'tools/reference/player-melee-return-native-evidence.json')
    parent_inventory_path = ROOT / '.local/class-skills/player-melee-return/audit-inputs.json'
    assert sha(parent_inventory_path) == previous['evidence'][str(parent_inventory_path.relative_to(ROOT))]
    parent_inventory = load(parent_inventory_path)
    added = 'tests/Sanctuary.Integration/NecromancerLegacyUpgradeChecks.cs'
    assert set(saved['sourceInputs']) - set(parent_inventory['sourceInputs']) == {added}
    assert all(saved['sourceInputs'][p] == h for p,h in parent_inventory['sourceInputs'].items())
    assert saved['copiedData'] == parent_inventory['copiedData']
    assert saved['projectInputs'] == load(ROOT / '.local/class-skills/player-melee-return/pre-build-5.json')['inputs']['projectInputs']
    proof = load(STAGE / 'migration-inputs.json')
    for path, digest in proof['sourceInputs'].items():
        assert sha(ROOT / path) == digest
    installed_receipt_path = ROOT / 'tools/reference/creeper-frame-installation-evidence.json'
    installed = load(installed_receipt_path)
    old_receipt_path = ROOT / 'tools/reference/creeper-frame-native-evidence.json'
    assert sha(old_receipt_path) == installed['sourceArtifacts'][str(old_receipt_path.relative_to(ROOT))]
    old = load(old_receipt_path)
    assert proof['oldArtifacts'] == proof['actualOldArtifacts'] == old['inputInventory']['artifacts']
    runtime_root = Path(installed['runtimeRoot'])
    for path,digest in proof['oldArtifacts'].items():
        old_path = runtime_root / ('.local/class-skills/creeper-frame-correction/integration/Sanctuary.Integration.dll'
            if path.endswith('Sanctuary.Integration.dll') else '.local/server/' + path)
        assert sha(old_path) == digest, path
    assert proof['oldCopiedData'] == hashes(runtime_root / '.local/server/Data/Sanctuary')
    assert {'Data/Sanctuary/' + p:h for p,h in proof['oldCopiedData'].items()} == old['inputInventory']['copiedData']
    assert proof['seedAssemblySha256'] == sha(STAGE / 'legacy-seed/Sanctuary.LegacySeed.dll')
    for path,digest in proof['currentArtifacts'].items():
        if path.startswith('Assemblies/Sanctuary.'):
            kind = 'integration' if path.endswith('Sanctuary.Integration.dll') else 'content'
            artifact = STAGE / kind / Path(path).name
        else:
            artifact = STAGE / 'native/Distribution' / path
        assert sha(artifact) == digest
        if not path.endswith('Sanctuary.Integration.dll'):
            assert digest == parent_inventory['artifacts'][path]
    assert {'Data/Sanctuary/' + p:h for p,h in proof['currentCopiedData'].items()} == saved['copiedData']
    baseline_sources = load(STAGE / 'old-source-inputs.json')
    assert baseline_sources['commit'] == installed['originalMainSourceCommit']
    for path,digest in baseline_sources['sources'].items():
        content = subprocess.check_output(['git','show',baseline_sources['commit'] + ':' + path], cwd=ROOT)
        assert (STAGE / 'old-source' / path).read_bytes() == content
        assert sha(STAGE / 'old-source' / path) == old['inputInventory']['sourceInputs'][path] == digest
    total = Counter()
    assert len(proof['boots']) == 3 and [row['writer'] for row in proof['boots']] == ['installed','current','current']
    for boot,row in enumerate(proof['boots']):
        text = (STAGE / f'boot-{boot}.log').read_text()
        assert ': failed' not in text and ' ERR]' not in text
        assert text.count('Writing world save snapshot done') == text.count('Shutting down') == 1
        total += markers(STAGE / f'boot-{boot}.log')
        assert hashes(STAGE / f'world-{boot}') == row['saves'] and row['saves']
    assert total == Counter({'class-skills-check':1, 'necro-legacy-seed':1, 'necro-legacy-upgrade-check':2})
    seeds = [load(STAGE / f'boot-{boot}-necro-legacy-seed.json') for boot in range(3)]
    assert seeds[0] == seeds[1] == seeds[2]
    seed = seeds[0]
    assert (seed['Armor'],seed['OwnerLife'],seed['OwnerMaximum'],seed['Mana256']) == (620,119,192,52096)
    assert (seed['MinionLife'],seed['MinionMaximum'],seed['Damage'],seed['Kind'],seed['Rank'],seed['Mastery']) == (112,125,2,65,1,1)
    assert seed['OldState'] == dict(Curse=None, BoneArmor=620, ManaFraction=128)
    for boot in (1,2):
        assert load(STAGE / f'boot-{boot}-necro-legacy-progress.json') == dict(Generation=boot, Remaining256=620*256-64*boot)
    for name in ('legacy-seed-build-2.log', 'integration-build-2.log'):
        assert all(value in (STAGE / name).read_text() for value in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name in ('named-item-runtime-audit.json', 'first-loop-affix-audit.json'):
        audit = load(ROOT / 'tools/reference' / name)
        assert audit == json.loads(subprocess.check_output(['git','show',PARENT + ':tools/reference/' + name], cwd=ROOT))
        assert audit['ContentAssemblySha256'] == proof['currentArtifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in audit['InputHashes'].items():
            assert sha(ROOT / path) == digest, path
    for name,text in (('python-tests.log','Ran 71 tests'), ('readiness-check.log','573 definitions accounted for'),
                      ('catalogue-check.log','Eight classes, 240 source skills'), ('roadmap-check.log','recorded separately'),
                      ('bone-followup-check.log','modern overflow remains unverified')):
        assert text in (STAGE / name).read_text()
    assert '\nOK\n' in (STAGE / 'python-tests.log').read_text()
    assert len(installed['installedArtifacts']) == 106
    for path,digest in installed['installedArtifacts'].items():
        assert sha(runtime_root / path) == digest
    protected = load(ROOT / '.local/class-skills/creeper-frame-installation.json')['protected']
    for name,count in (('saves',38),('configuration',20)):
        folder = runtime_root / '.local/server' / ('Saves' if name == 'saves' else 'Configuration')
        assert len(protected[name]) == count and hashes(folder) == protected[name]
    status = load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    assert not status['completedClasses'] and sum(row['implementation'] == 'handler-present' for row in status['skills']) == 191
    followup_path = ROOT / 'tools/reference/bone-armor-current-followup.json'
    followup = load(followup_path)
    assert followup['bytesRead'] == 1216 and not followup['allUnwindFragmentsCaptured'] and not followup['modernOverflowVerified']
    assert followup['process'] != load(ROOT / 'tools/reference/bone-armor-current-reference.json')['process']
    assert all(not row['captured'] and row['permissions'] == '---s' for row in followup['fragments'])
    assert not any(followup[key] for key in ('processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten','semanticCallbackIdentityVerified'))
    for path,digest in followup['artifacts'].items():
        assert sha(ROOT / path) == digest
    files = [Path(__file__).resolve(), installed_receipt_path, old_receipt_path, parent_inventory_path, followup_path,
        *(ROOT / path for path in proof['sourceInputs']),
        *(ROOT / 'docs' / name for name in ('necro-legacy-upgrade-checkpoint.md','bone-armor-checkpoint.md','class-skill-roadmap.md',
            'class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))]
    files += [STAGE / name for name in ('pre-build-2.json','old-source-inputs.json','migration-inputs.json',
        'legacy-seed-build-2.log','integration-build-2.log','migration-run-1.log','migration-run-2.log','migration-run-3.log',
        'python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','bone-followup-check.log')]
    files += [p for p in STAGE.glob('boot-*') if p.is_file()]
    files += [STAGE / 'old-source' / path for path in baseline_sources['sources']]
    files += [p for p in (STAGE / 'attempt-1').rglob('*') if p.is_file() and 'runtime-1' not in p.relative_to(STAGE).parts]
    result = dict(schemaVersion=1, referenceVersion='D2R Reign of the Warlock 3.3.93847', parent=PARENT,
        scope='Actual earlier-installed writer to current reader: original Necromancer owner, legacy Clay and integer Bone Armor',
        productionChanged=False, installed=False, goalComplete=False, completedClasses=[],
        verified=dict(commandMarkers=4, boots=3, actualReloads=2, worldSaves=3, originalActorWoundsAndOwnership=True,
            originalFractionalManaAndF12=True, realOldWriterIntegerArmor=True, quarterPointAbsorptions=2,
            unknownOriginalRankAndMaximumRetained=True, approvedClayDamageChange=[2,3], pythonTests=71,
            unchangedInstalledArtifacts=106, unchangedSaves=38, unchangedConfigurations=20, handlers=191,
            reusedAuditFingerprints=[1020,21], namedDecisionsUnchanged=573, currentReferenceCallbackStillUnavailable=True),
        oldArtifacts=proof['oldArtifacts'], currentArtifacts=proof['currentArtifacts'],
        evidence={str(path.relative_to(ROOT)):sha(path) for path in sorted(set(files))},
        limitations=['Four focused migration markers, not a fresh full-class or parent 487-marker regression. Current production/data/native/content bytes are reused unchanged.',
            'A fixture seed assembly uses old public APIs; the actual old application writes real saves. This is one older integer-armor and Clay representation, not arbitrary historical state.',
            'Internalized inspection actors and an existing owner regeneration exclusion preserve wounds. The original fixture holds Dim Vision; unheld curses/online regeneration/client presentation are not established.',
            'Restarted reference process exposes only immutable anchors. Candidate callback identity and modern Bone Armor overflow remain unverified; no protected code was read.',
            'No deployment, original save copies, schema/protocol change, new handler, class completion or final assets. Effects/audio and icons remain separate.'])
    encoded = json.dumps(result, indent=2) + '\n'
    target = ROOT / 'tools/reference/necro-legacy-upgrade-evidence.json'
    if check:
        assert target.read_text() == encoded
    else:
        target.write_text(encoded)
    print('Actual earlier-writer Necromancer upgrade: 4 command markers/3 boots/2 reloads/3 saves; old armor provenance, original wounds and bindings retained.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
