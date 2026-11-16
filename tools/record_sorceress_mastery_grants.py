#!/usr/bin/env python3
"""Bind Fire Bolt/masteries and every Tal Rasha item released by their registry."""
import argparse,sys
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/sorceress-mastery-grants';BASE='7fd87e3f'
sys.path.insert(0,str(ROOT/'tests'))
from check_sorceress_mastery_grants import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3,'sorceress-staff-readiness-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c['extra_wait_results']:r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/sorceress-staff-grants/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['tests/Sanctuary.Integration/SorceressStaffGrantChecks.cs'],changed
    data=sorted(p for p,d in born['copiedData'].items() if parent['copiedData'].get(p)!=d)
    assert data==['Data/Sanctuary/Balance/named-active-skills.json','Data/Sanctuary/Balance/named-item-coverage.json','Data/Sanctuary/Balance/named-skill-backlog.json'],data
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in [('Content',2),('Integration',4)]:
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
        text=(STAGE/f'{name.lower()}-build-{build}.log').read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),30);assert 'pass across3boots/2reloads' in (STAGE/'native-run-2.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==9 and 'denied by connection filter' not in log and 'Warning: Attempted to delete' not in log
    for n in configuration()['extra_target_input_commands']:assert log.count(n+': windup')==3
    saved=e.load(STAGE/'sorceress-staff-grants-fixtures.json');assert saved['Generation']==2 and len(saved['Items'])==6 and len(saved['Partners'])==4
    registry=e.load(ROOT/'data/Sanctuary/Balance/named-active-skills.json');grants=['sorceress.firebolt','sorceress.firemastery','sorceress.lightningmastery','sorceress.coldmastery'];assert len(registry)==31 and all(t in registry for t in grants)
    files=[ROOT/'tools/record_sorceress_mastery_grants.py',ROOT/'docs/sorceress-mastery-grants-checkpoint.md',ROOT/'tools/reference/consume-learned-client-evidence.json',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/check_britain.py',ROOT/'tools/build_named_equipment.py',ROOT/'tools/build_named_skill_backlog.py',STAGE/'birth-inputs.json',STAGE/'birth-native-checks.log',STAGE/'sorceress-staff-grants-fixtures.json',STAGE/'source-snapshot-4.json',STAGE/'native-run-2.log',STAGE/'content-build-2.log',STAGE/'integration-build-4.log',*(STAGE/n for n in ('failed-build-1-complete.tar.gz','failed-build-2.tar.gz','failed-run-1.tar.gz')),*configuration()['extra_inputs']]
    result=dict(component='Bane Ash,Chromatic Ire and full Tal Rasha original-item direct grants',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=30,markers=dict(sorted(expected_markers().items())),connections=9,actualAbilityWireInputs=6,boots=3,saves=3,reloads=2,savedOriginals=10,namedDirectGrantRegistryBefore=27,namedDirectGrantRegistryAfter=31,newDirectGrants=grants,newNamedItemsAdmitted=['unique/54','unique/185','piece/76','piece/77','piece/78','piece/79','piece/80'],namedDefinitionsCompared=573,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=data,contentBuild=2,integrationBuild=4,sourceSnapshot=4,nativeRun=2,gameplaySourceChanged=False,nativeRebuilt=False,persistedSchemaChanged=False,installed=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Four direct grants through complete weighted originals,actual Fire Bolt input and passive typed life damage,all Tal partial/full thresholds,original gear/resources and two reloads. Prior staff equipment controls reused without repeating their eight unchanged active routes. No readiness bypass,proc/charge/aura admission,graphical/full-class certificate,new reference read,imagegen or deployment;whole Hellslayer remains gated.')
    e.write('sorceress-mastery-grants-native-evidence.json',result,files,check)
    print('Fire Bolt/masteries:30results/6wire inputs/9connections/two reloads;four grants,seven originals admitted;all Tal thresholds retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
