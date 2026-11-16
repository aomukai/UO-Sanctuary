#!/usr/bin/env python3
"""Bind complete Warpspear acquisition and two newly admitted direct grants."""
import argparse
from collections import Counter
import json,subprocess,sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/warpspear-grants';BASE='9066c559'
sys.path.insert(0,str(ROOT/'tests'))
from check_warpspear_grants import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3,'warpspear-readiness-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c['extra_wait_results']:r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    assert sum(r.values())==42
    return r

def record(check=False):
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/named-active-skills.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/named-active-skills.json')
    assert len(before)==16 and after==before+['sorceress.telekinesis','sorceress.energyshield']
    prior=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/named-skill-backlog.json'],cwd=ROOT));backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json')
    assert len(prior['items'])==176 and len(backlog['items'])==175 and set(r['id']for r in prior['items'])-set(r['id']for r in backlog['items'])=={'unique/186'}
    assert len(backlog['workingDirectSkills'])==18 and len(backlog['mechanicallyImplementedSkills'])==240
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/boneshade-grant/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    changed={p for p,h in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=h}
    assert changed=={'tests/Sanctuary.Integration/WarpspearGrantChecks.cs'}
    for p,h in born['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
    changed_data={p for p,h in born['copiedData'].items()if parent['copiedData'].get(p)!=h}
    assert changed_data=={'Data/Sanctuary/Balance/named-active-skills.json','Data/Sanctuary/Balance/named-item-coverage.json','Data/Sanctuary/Balance/named-skill-backlog.json'}
    for p,h in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==h,p
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for k in ('Content','Integration'):
        assert e.sha(STAGE/k.lower()/f'Sanctuary.{k}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
        text=(STAGE/(k.lower()+'-build-2.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),42);assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==15 and 'denied by connection filter' not in log
    for n in configuration()['extra_target_input_commands']:assert log.count(n+': windup')==3
    assert ': failed' not in log and 'Warning: Attempted to delete' not in log
    files=[ROOT/'tools/record_warpspear_grants.py',ROOT/'docs/warpspear-grants-checkpoint.md',ROOT/'tools/reference/consume-learned-client-evidence.json',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/check_britain.py',ROOT/'tools/build_named_equipment.py',ROOT/'tools/build_named_skill_backlog.py',STAGE/'birth-inputs.json',STAGE/'birth-native-checks.log',STAGE/'source-snapshot-2.json',STAGE/'native-run-1.log',STAGE/'content-build-2.log',STAGE/'integration-build-2.log',STAGE/'failed-build-1.tar.gz',*configuration()['extra_inputs']]
    result=dict(component='Complete Warpspear Telekinesis/Energy Shield direct grants',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=42,markers=dict(sorted(expected_markers().items())),connections=15,actualAbilityWireInputs=12,boots=3,saves=3,reloads=2,namedDirectGrantRegistryBefore=16,namedDirectGrantRegistryAfter=18,newDirectGrants=['sorceress.telekinesis','sorceress.energyshield'],newNamedItemsAdmitted=['unique/186'],namedDefinitionsCompared=573,artifacts=born['artifacts'],changedSources=sorted(changed),changedRuntimeData=sorted(changed_data),contentBuild=2,integrationBuild=2,sourceSnapshot=2,nativeRun=1,gameplaySourceChanged=False,nativeRebuilt=False,persistedSchemaChanged=False,installed=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Normal weighted generation and complete-item untrained spell use/equipment/native damage/save controls qualify this one admission. No kill/gamble economy or graphical-client certificate. Existing source special/PvP/property limits remain;other direct grants,procs,charges,item auras and whole Hellslayer registration are separate. No readiness bypass,owner hard-point invention,new reference compilation,imagegen or deployment.')
    e.write('warpspear-grants-native-evidence.json',result,files,check)
    print('Warpspear:42results/12wire inputs/15connections/two reloads;two direct grants,one complete item newly admitted.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
