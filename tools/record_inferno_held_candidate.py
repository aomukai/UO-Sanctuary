#!/usr/bin/env python3
"""Bind the review-only Inferno input prototype without enabling gameplay."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers

ROOT=e.ROOT
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'inferno-held-candidate'
WIDE=BASE/'inferno-held-candidate-retained'
PARENT='ef838622492119600be3a981be4a39a508db8775'


def record(check=False):
    born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-4.json')
    prior=e.load(BASE/'npc-item-fear/source-snapshot-8.json')['inputs']
    current=e.load(STAGE/'source-snapshot-4.json')['inputs']
    assert current['sourceInputs'].keys()-prior['sourceInputs'].keys()=={'tests/Sanctuary.Integration/InfernoHeldCandidateChecks.cs'}
    assert prior['sourceInputs'].keys()<=current['sourceInputs'].keys()
    assert all(current['sourceInputs'][p]==h for p,h in prior['sourceInputs'].items())
    assert current['projectInputs']==prior['projectInputs'] and current['copiedData']==prior['copiedData']
    parent=e.load(BASE/'npc-item-fear/birth-inputs.json')
    for name,h in parent['artifacts'].items():
        if name!='Assemblies/Sanctuary.Integration.dll':assert born['artifacts'][name]==h,name
    assert all(s in(STAGE/'integration-build-4.log').read_text()for s in('Build succeeded.','0 Warning(s)','0 Error(s)'))
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'inferno-held-candidate-check':1,'inferno-held-candidate-reload-check':2})
    oldlog=BASE/'player-inferno-modes/birth-native-checks.log'
    receipt=e.load(ROOT/'tools/reference/player-inferno-modes-native-evidence.json')
    assert e.sha(oldlog)==receipt['evidence'][str(oldlog.relative_to(ROOT))]
    retained=markers(oldlog);assert sum(retained.values())==182
    retained.update({'inferno-held-candidate-check':1,'inferno-held-candidate-reload-check':2})
    logs=[e.cohort(STAGE,expected,12),e.cohort(WIDE,retained,185)]
    for stage,run in ((STAGE,4),(WIDE,3)):
        assert 'pass across3boots/2reloads' in(stage/f'run-{run}.log').read_text()
        saved=e.load(stage/'inferno-held-candidate-fixtures.json')
        assert saved['Generation']==2 and saved['HeldAtFirstSave'] and saved['UnresumedSaves']==2
        assert saved['Mana256']==80*256-23*96 and len(saved['Flights'])==20
        assert all(f['Owner']==saved['Owner']and f['Rank']==1 for f in saved['Flights'])
    assert all(log.count('first flame frame12,then2frame renewal')==1 for log in logs)
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protection=e.protection()
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==5 and all(c['exitCode']==0 for c in commands)
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert sum(r['implementation']=='handler-present' for r in status['skills'])==197 and not status['completedClasses']
    context=e.load(ROOT/'tools/reference/inferno-reference-context-2026-11-06.json')
    assert context['bytesRead']==0 and not context['processMemoryOpened'] and not context['callbackLinkVerified'] and not context['completeCandidateReadable']
    files=e.stage_files(STAGE,WIDE)
    for folder,message in ((STAGE/'failed-native-1','Candidate real soft-rank gear'),(WIDE/'failed-retained-1','Candidate first save lost held mode or reload restored it'),(STAGE/'failed-native-3','Disconnected candidate retained held input')):
        inputs=e.load(folder/'birth-inputs.json')
        assert e.sha(folder/'InfernoHeldCandidateChecks.cs')==inputs['sourceInputs']['tests/Sanctuary.Integration/InfernoHeldCandidateChecks.cs']
        assert message in(folder/'birth-native-checks.log').read_text()
        for kind in ('Content','Integration'):
            p=folder/kind.lower()/f'Sanctuary.{kind}.dll';assert e.sha(p)==inputs['artifacts'][f'Assemblies/Sanctuary.{kind}.dll'];files.append(p)
        files+=e.stage_files(folder)+list(folder.glob('*.cs'))+list(folder.glob('*.py'))
    for folder,count in ((STAGE/'prior-focused-2',12),(WIDE/'prior-retained-2',185)):
        inputs=e.load(folder/'birth-inputs.json')
        assert e.sha(folder/'InfernoHeldCandidateChecks.cs')==inputs['sourceInputs']['tests/Sanctuary.Integration/InfernoHeldCandidateChecks.cs']
        log=(folder/'birth-native-checks.log').read_text()
        assert sum(markers(folder/'birth-native-checks.log').values())==count and ': failed'not in log and ' ERR]'not in log
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        for kind in ('Content','Integration'):
            p=folder/kind.lower()/f'Sanctuary.{kind}.dll';assert e.sha(p)==inputs['artifacts'][f'Assemblies/Sanctuary.{kind}.dll'];files.append(p)
        files+=e.stage_files(folder)+list(folder.glob('*.cs'))+list(folder.glob('*.py'))
    files+=[Path(__file__).resolve(),ROOT/'tests/check_inferno_held_candidate.py',ROOT/'tools/observe_inferno_current_context.py',ROOT/'tools/reference/inferno-reference-context-2026-11-06.json',ROOT/'tools/reference/player-inferno-modes-native-evidence.json']
    files +=[ROOT/'docs'/n for n in ('inferno-held-input-proposal.md','sorceress-inferno-preflight.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,candidateOnly=True,approved=False,gameplayEnabled=False,productionChanged=False,installed=False,
        handlers=197,completedClasses=[],integrationBuild=4,sourceSnapshot=4,focusedRun=4,retainedRun=3,auditRun=3,focusedMarkers=12,retainedMarkers=185,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,
        rankOne=dict(entryThresholdMana=6,invocationMana=0.375,firstFlameFrames=12,renewalFrames=2,steadyManaPerSecond=4.6875,twoSecondDebits=22,twoSecondFlames=20),
        originalSavedFlights=20,pythonTests=71,auditFingerprints=fingerprints,protection=protection,artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=['An explicitly supplied candidate policy reuses existing production channel/resources/sequence/fire-stream APIs in an integration fixture only. Low mana,1/3/7frame catchup,same-frame duplicate delivery,early release/fresh windup,real gear and independent released damage are checked.',
               'The first save uses a deliberately connected fresh press;original wounds and20exact emitted packets survive two reloads while held intent is not restored. This is not a natural held-wire acceptance.',
               'All182historical retained markers remain,including source controls,fire/cold modes and real spell inputs;current unchanged production preserves the existing refusal gates.'],
        limits=['No modern D2R input/payment chronology is certified;explicit user approval is still required before implementing the proposed Sanctuary input rule in gameplay.',
                'No learned Inferno,Arctic Blast or Blade Fury route is enabled;no complete-class,full-client,PvP,world geometry or owner lifecycle parity claim.',
                'A changed process justified one new maps-only observation;in-memory identity and dispatcher linkage remain unverified. No process memory was read.',
                'Existing Inferno placeholder effects/icons remain separate. Source and binary archives preserve the failed gear/save fixture assumptions.'])
    e.write('inferno-held-candidate-native-evidence.json',result,files,check)
    print('Inferno review-only candidate:12focused/185retained,2reloads each;unchanged production/installation;197handlers;approval pending.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
