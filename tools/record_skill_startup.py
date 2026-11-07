#!/usr/bin/env python3
"""Accept the shared startup kernel without certifying its missing NPC hosts."""
import argparse
from collections import Counter
from pathlib import Path
import sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected

ROOT = e.ROOT
BASE = ROOT/'.local/class-skills'
STAGE = BASE/'skill-startup'
PARENT = '54b7ad9341f71ad8b4189333d50e3b257496f785'


def record(check=False):
    wide, isolated, shared = [BASE/('skill-startup'+s) for s in ['-retained','-isolate','-shared']]
    born = e.inventory(STAGE, wide, STAGE/'source-snapshot-1.json')
    sys.path.insert(0, str(ROOT/'tests'))
    from check_skill_startup import configuration
    parent = e.load(ROOT/'tools/reference/npc-item-skills-native-evidence.json')
    extra = Counter({'skill-startup-check':1, 'skill-startup-reload-check':2})
    for old, stage, count in [('npc-item-skills', STAGE, 150), ('npc-item-skills-retained', wide, 458)]:
        path = BASE/old/'birth-native-checks.log'
        assert e.sha(path) == parent['files'][str(path.relative_to(ROOT))]
        e.cohort(stage, markers(path)+extra, count)
    e.cohort(isolated, expected(configuration(isolate=True)), 57)
    e.cohort(shared, expected(configuration(shared=True)), 255)
    proof = e.load(ROOT/'tools/reference/skill-startup-source-evidence.json')
    assert proof['cases'] == 6796 and proof['startFunctionCount'] == 91
    cases = BASE/'skill-startup-source/cases.json'
    assert e.sha(cases) == proof['casesSha256']
    for stage, driver in [(STAGE,'focused-driver-1.log'),(wide,'retained-driver-1.log'),
                          (isolated,'isolate-driver-1.log'),(shared,'shared-driver-1.log')]:
        assert 'pass across3boots/2reloads' in (STAGE/driver).read_text()
        inputs = e.load(stage/'birth-inputs.json')
        snapshot(STAGE/'source-snapshot-1.json', stage/'birth-inputs.json')
        assert inputs['artifacts'] == born['artifacts'] and inputs['copiedData'] == born['copiedData']
        for p,h in inputs['sourceInputs'].items():
            assert e.sha(ROOT/p) == h, p
        if stage != shared:
            assert (stage/'skill-startup-fixtures.json').read_text().lower() == proof['casesSha256']
    for name in ['content-build-1.log','integration-build-1.log']:
        log = (STAGE/name).read_text()
        assert all(t in log for t in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    commands = e.load(STAGE/'verification-commands.json')
    assert len(commands) == 14 and all(c['exitCode'] == 0 for c in commands)
    log = (STAGE/'python-tests.log').read_text()
    assert 'Ran 71 tests' in log and '\nOK\n' in log
    review = e.load(ROOT/'tools/reference/skill-startup-review-evidence.json')
    assert review['sourceContexts'] == 6796
    assert review['completeOlderStartupWrapper'] and review['completeOlderStartDispatcher']
    assert not any(review[k] for k in ['currentExecutableParityVerified','npcWorldAdapterImplemented','handlerEnabled'])
    for p,h in review['inputs'].items():
        assert e.sha(ROOT/p) == h, p
    frozen = STAGE/'frozen-c1-i1'
    for name,h in e.load(frozen/'hashes.json').items():
        assert e.sha(frozen/name) == h, name
    for kind in ['Content','Integration']:
        assert e.sha(frozen/f'Sanctuary.{kind}.dll') == born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    fingerprints = e.audits(STAGE, PARENT, born['artifacts'])
    protected = preservation()
    assert protected == e.load(STAGE/'protection-check.json')
    client = e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json')
    assert len(client['patchHashes']) == 41
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():
            assert e.sha(ROOT/p) == h, p
    status = e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert sum(s['implementation']=='handler-present' for s in status['skills']) == 203 and not status['completedClasses']
    files = [p for p in e.stage_files(STAGE,wide,isolated,shared) if not p.name.startswith('receipt-')]
    files += list(frozen.iterdir()) + [Path(__file__).resolve(), cases]
    files += [ROOT/p for p in review['inputs'] if not p.startswith('.local/')]
    files += [ROOT/'tools/reference'/p for p in ['skill-startup-source-evidence.json','skill-startup-review-evidence.json']]
    files += [ROOT/'docs'/p for p in ['skill-startup-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
    result = dict(referenceVersion='3.3.93847', parent=PARENT, installed=False,
        completeOlderStartupWrapper=True, completeOlderStartDispatcher=True,
        currentExecutableParityVerified=False, npcWorldAdapterImplemented=False,
        handlerEnabled=False, learnedShadows=False, handlers=203, completedClasses=[],
        contentBuild=1, integrationBuild=1, sourceSnapshot=1,
        focusedMarkers=150, retainedMarkers=458, isolatedMarkers=57, sharedMarkers=255,
        bootsPerCohort=3, reloadsPerCohort=2, savesPerCohort=3,
        sourceContexts=6796, startFunctionCount=91, pythonTests=71, verificationCommands=14,
        auditFingerprints=fingerprints, protection=protected, artifacts=born['artifacts'], copiedData=born['copiedData'],
        scope=review['scope'], limits=review['limits'])
    e.write('skill-startup-native-evidence.json', result, files, check)
    print('Shared startup:6796 source contexts,150focused/458retained/57isolated/255shared;NPC hosts/AI/learned Shadows remain gated,no deployment.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check', action='store_true')
    record(p.parse_args().check)
