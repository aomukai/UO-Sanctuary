#!/usr/bin/env python3
"""Record final-four interaction proof against unchanged gameplay source and fresh fixture assembly."""
import argparse
from collections import Counter
import re,sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/warlock-final-talents';PARENT=ROOT/'.local/class-skills/consume-learned'
sys.path.insert(0,str(ROOT/'tests'))
from check_warlock_final_talents import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3,'consume-aura-save-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c['extra_wait_results']:r[c['extra_wait_results'][n]]+=count
    for n in c['extra_checks']:r[n]+=3;r[c['extra_wait_results'][n]]+=3
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    assert sum(r.values())==150
    return r

def record(check=False):
    approval=e.load(ROOT/'tools/reference/warlock-final-talents-approval.json');assert approval['approved'] and len(approval['approvedTalents'])==4
    for p,h in approval['filesAtApprovalSha256'].items():assert e.sha(ROOT/p)==h,p
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(PARENT/'birth-inputs.json')
    snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json');assert e.load(PARENT/'source-snapshot-4.json')['inputs']['copiedData']==born['copiedData']
    assert born['copiedData']==parent['copiedData']
    changed={p for p,h in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=h}
    assert changed=={'tests/Sanctuary.Integration/BoundDemonEngorgeChecks.cs'}
    for p,h in born['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
    for p,h in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==h,p
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for k in ('Content','Integration'):
        assert e.sha(STAGE/k.lower()/f'Sanctuary.{k}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
        build=(STAGE/(k.lower()+'-build-2.log')).read_text();assert all(s in build for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(status['skills'])==240 and all(r['implementation']=='handler-present' for r in status['skills'])
    assert all('Approved=>true' in (ROOT/'src/Sanctuary.Content'/n).read_text() for n in ('BloodOathDamageProposal.cs','EngorgeEffects.cs','BloodBoilPrepared.cs','ConsumeEffects.cs'))
    log=e.cohort(STAGE,expected_markers(),150);assert 'pass across3boots/2reloads' in (STAGE/'native-run-2.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==72 and 'denied by connection filter' not in log
    for n in configuration()['extra_target_input_commands']:assert log.count(n+': windup')==3
    all_warnings=[l for l in log.splitlines() if ' WRN]' in l]
    prompts=[l for l in all_warnings if 'This server has no accounts.' in l or 'No owner account created.' in l];assert len(prompts)==6
    warnings=[l for l in all_warnings if l not in prompts];assert len(warnings)==3 and all('Blood Oath refused unsupported incoming context' in l for l in warnings)
    refs=['blood-oath-learned-native-evidence.json','engorge-learned-native-evidence.json','blood-boil-learned-native-evidence.json','consume-learned-native-evidence.json','warlock-demons-acceptance-native-evidence.json','warlock-weapons-acceptance-native-evidence.json','warlock-chaos-acceptance-native-evidence.json']
    files=[ROOT/'tools/record_warlock_final_talents.py',ROOT/'docs/warlock-final-talents-checkpoint.md',ROOT/'docs/class-skills-implementation-checkpoint.md',ROOT/'tools/reference/consume-learned-client-evidence.json',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/check_britain.py',STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json',STAGE/'birth-native-checks.log',STAGE/'native-run-2.log',STAGE/'content-build-2.log',STAGE/'integration-build-2.log',STAGE/'failed-run-1.tar.gz',*configuration()['extra_inputs'],*[ROOT/'tools/reference'/n for n in refs]]
    result=dict(component='All four learned Warlock talents combined original/bound/menu/aura cohort',referenceVersion='3.3.93847',approved=True,allFourLearnedEnabled=True,gameplaySourceUnchanged=True,changedSources=sorted(changed),nativeAndDataReused=True,artifacts=born['artifacts'],nativeMarkers=150,markers=dict(sorted(expected_markers().items())),connections=72,actualAbilityWireInputs=33,passiveLearnedChecks=3,boots=3,saves=3,reloads=2,globalHandlers=240,warlockHandlers=30,routesCovered=4,priorScopedRoutesAccepted=26,warlockScopedRoutesAccepted=30,contentBuild=2,integrationBuild=2,sourceSnapshot=2,nativeRun=2,nativeRebuilt=False,persistedSchemaChanged=False,installed=False,classCertified=False,goalComplete=False,expectedEmptyWorldAccountPrompts=6,expectedUnsupportedWarnings=warnings,saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.',log),protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='All four use ordinary learned/default operation and approved qualified bound/aura extensions;eleven component families and current left/right/quick/ground/town/native-save controls run together. Earlier26routes retain historical scoped subgroup evidence,not a rerun at this head. No whole-class/full-current-D2R parity/graphical certificate. Unknown native identities/modifiers,recipient/property/PvP/ordering boundaries retain explicit refusals/limits;effects/audio/icons remain separate. No charms/runewords,current-client reads,new reference compile,imagegen or deployment.')
    e.write('warlock-final-talents-native-evidence.json',result,files,check)
    print('Final four:150results/33wire inputs/72connections/two reloads;all240routes present,all30Warlock routes have scoped subgroup evidence.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
