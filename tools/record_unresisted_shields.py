#!/usr/bin/env python3
"""Record the full native channel bridge without extending unsupported contexts."""
import argparse
from collections import Counter
import hashlib
import json
import re
import subprocess
import sys
import tarfile

import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from prepare_unresisted_shield_native import PARENT, prepare

ROOT = e.ROOT
STAGE = ROOT / '.local/class-skills/unresisted-shields'
sys.path.insert(0, str(ROOT / 'tests'))
from check_unresisted_shields import configuration


def expected_markers():
    config = configuration()
    expected = Counter({'iron-golem-birth-check': 3, 'encounter-shields-check': 3})
    for family in ('iron-golem-items', 'npc-resource-callbacks', *config['extra_families']):
        expected[family + '-check'] += 1
        expected[family + '-reload-check'] += 2
    for command in config['extra_target_input_commands']:
        expected[command + '-check'] += 3
        expected[command + '-result'] += 3
    assert sum(expected.values()) == 33
    return expected


def record(check=False):
    run = STAGE / 'native-run-1.log'
    assert 'pass across3boots/2reloads' in run.read_text()
    prepare(check=True)
    native = e.load(STAGE / 'native-source-evidence.json')
    assert e.sha(ROOT / native['patch']) == native['patchSha256']
    assert e.sha(STAGE / 'native-source-1.tar.gz') == native['sourceArchiveSha256']
    with tarfile.open(STAGE / 'native-source-1.tar.gz', 'r|gz') as archive:
        names = set()
        parent_names = {str(path.relative_to(PARENT)) for path in PARENT.rglob('*.cs')
            if not any(part in ('bin', 'obj', 'Distribution', '.git') for part in path.relative_to(PARENT).parts)}
        modified = set()
        for member in archive:
            if not member.isfile() or not member.name.endswith('.cs'):
                continue
            name = member.name
            assert name not in names
            names.add(name)
            digest = hashlib.sha256(archive.extractfile(member).read()).hexdigest()
            if digest != e.sha(PARENT / name):
                modified.add(name)
                assert digest == native['changedSources'][name]['after']
                assert e.sha(PARENT / name) == native['changedSources'][name]['before']
        assert names == parent_names
        assert modified == set(native['changedSources']) and len(modified) == 2
    born = e.load(STAGE / 'birth-inputs.json')
    snapshot(STAGE / 'source-snapshot-2.json', STAGE / 'birth-inputs.json')
    initial = e.load(STAGE / 'source-snapshot-1.json')['inputs']['sourceInputs']
    for name, digest in born['sourceInputs'].items():
        assert e.sha(ROOT / name) == digest, name
        if name.endswith('.cs'):
            assert digest == initial[name], name
    prior = e.load(ROOT / '.local/class-skills/explosive-terrain-adopted/birth-inputs.json')
    changed = {name for name, digest in born['sourceInputs'].items()
        if name.endswith('.cs') and digest != prior['sourceInputs'].get(name)}
    assert changed == {'src/Sanctuary.Content/SkillCombat.cs', 'src/Sanctuary.Content/NecromancerEffects.cs',
                       'tests/Sanctuary.Integration/UnresistedShieldChecks.cs'}
    assert {name for name, digest in born['copiedData'].items() if digest != prior['copiedData'][name]} == {'Data/Sanctuary/Balance/class-skill-status.json'}
    old = json.loads(subprocess.check_output(['git', 'show', '626b0e4f:data/Sanctuary/Balance/class-skill-status.json'], cwd=ROOT))
    current = e.load(ROOT / 'data/Sanctuary/Balance/class-skill-status.json')
    for a, b in zip(old['skills'], current['skills'], strict=True):
        if a['talentId'] in {'sorceress.energyshield', 'druid.cyclonearmor'}:
            assert a['remaining'] != b['remaining'] and a | {'remaining': b['remaining']} == b
        else:
            assert a == b
    assert old | {'skills': current['skills']} == current
    for name, digest in born['copiedData'].items():
        assert e.sha(ROOT / 'data' / name.removeprefix('Data/')) == digest, name
    for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll'):
        assert e.sha(STAGE / 'native/Distribution' / name) == born['artifacts'][name]
        assert (born['artifacts'][name] == prior['artifacts'][name]) == (name != 'Assemblies/UOContent.dll')
    for kind in ('Content', 'Integration'):
        assert e.sha(STAGE / kind.lower() / f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    for name in ('native-build-1.log', 'content-build-1.log', 'integration-build-1.log'):
        build = (STAGE / name).read_text()
        assert all(value in build for value in ('Build succeeded.', '0 Warning(s)', '0 Error(s)'))
    log = e.cohort(STAGE, expected_markers(), 33)
    assert log.count('Client: 127.0.0.1: Connected.') == 6 and 'denied by connection filter' not in log
    files = [ROOT / 'tools/record_unresisted_shields.py', ROOT / 'docs/unresisted-shields-checkpoint.md',
        ROOT / 'tools/build_skill_roadmap.py', ROOT / 'data/Sanctuary/Balance/class-skill-status.json',
        STAGE / 'native-source-evidence.json', STAGE / 'native-source-1.tar.gz',
        STAGE / 'native-prepare-2.log', STAGE / 'native-build-1.log',
        STAGE / 'content-build-1.log', STAGE / 'integration-build-1.log',
        STAGE / 'source-snapshot-1.json', STAGE / 'source-snapshot-2.json',
        STAGE / 'birth-inputs.json', STAGE / 'birth-native-checks.log', run]
    files += list(configuration()['extra_inputs'])
    result = dict(component='Native typed armor-ignore Energy/Cyclone shield producer',
        referenceVersion='3.3.93847', producerCorrection=True, newShieldFormula=False,
        nativeMarkers=33, markers=dict(sorted(expected_markers().items())),
        actualWireInputs=6, connections=6, boots=3, saves=3, reloads=2,
        nativeBuild=1, contentBuild=1, integrationBuild=1, sourceSnapshot=2, nativeRun=1,
        artifacts=born['artifacts'], changedSources=sorted(changed), nativeChangedSources=sorted(modified),
        nativeSourceArchiveSha256=native['sourceArchiveSha256'], nativeSourcesChecked=len(names),
        serverEngineModified=False, persistedSchemaChanged=False, installed=False,
        classCertified=False, goalComplete=False,
        saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.', log),
        unsupportedWarnings=[line for line in log.splitlines() if ' WRN]' in line and 'unsupported' in line.lower()],
        protection=e.protection('tools/reference/slow-missiles-learned-client-evidence.json', 44),
        limits='Normalized native physical/fire/cold/poison/lightning only;direct/remainder and Energy/Ward overlaps refuse before side effects. Monster drain fields remain unproduced,not inferred from poison/leech. Native special modifiers/PvP/event priority and full class parity remain unqualified. Source compilation used RAM;source archive/patch/binaries persist on disk. No charms/runewords/final assets.')
    e.write('unresisted-shields-native-evidence.json', result, files, check)
    print('Native typed shields:33 results/6actual casts/2reloads;engine and installed saves unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
