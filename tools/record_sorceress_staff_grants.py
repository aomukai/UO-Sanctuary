#!/usr/bin/env python3
"""Bind three complete Sorceress staffs and unchanged-stat resource regression."""
import argparse
from collections import Counter
import sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/sorceress-staff-grants';BASE='8487a7b7'
sys.path.insert(0,str(ROOT/'tests'))
from check_sorceress_staff_grants import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3,'sorceress-staff-readiness-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c['extra_wait_results']:r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:
        r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/warpspear-grants/birth-inputs.json')
    snapshot(STAGE/'source-snapshot-6.json',STAGE/'birth-inputs.json')
    for source,digest in born['sourceInputs'].items():assert e.sha(ROOT/source)==digest,source
    for path,digest in born['copiedData'].items():assert e.sha(ROOT/'data'/path.removeprefix('Data/'))==digest,path
    changed=sorted(p for p,d in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/ClassGear.cs','tests/Sanctuary.Integration/SorceressStaffGrantChecks.cs'],changed
    data=sorted(p for p,d in born['copiedData'].items() if parent['copiedData'].get(p)!=d)
    assert data==['Data/Sanctuary/Balance/named-active-skills.json','Data/Sanctuary/Balance/named-item-coverage.json','Data/Sanctuary/Balance/named-skill-backlog.json'],data
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for k in ('Content','Integration'):
        assert e.sha(STAGE/k.lower()/f'Sanctuary.{k}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
        text=(STAGE/(k.lower()+'-build-6.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),66)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-4.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==27 and 'denied by connection filter' not in log
    for n in configuration()['extra_target_input_commands']:assert log.count(n+': windup')==3
    assert 'Warning: Attempted to delete' not in log
    failed=['failed-run-1.tar.gz','failed-run-2.tar.gz','failed-run-3.tar.gz','failed-chain-focus-1.tar.gz','failed-build-5.tar.gz']
    # The isolated diagnostic archive belongs to the same preserved candidate.
    focus=ROOT/'.local/class-skills/sorceress-staff-grants-chain-focus'
    focus_archive=focus/'failed-run-1.tar.gz'
    files=[ROOT/'tools/record_sorceress_staff_grants.py',ROOT/'docs/sorceress-staff-grants-checkpoint.md',ROOT/'tools/reference/consume-learned-client-evidence.json',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/check_britain.py',ROOT/'tools/build_named_equipment.py',ROOT/'tools/build_named_skill_backlog.py',STAGE/'birth-inputs.json',STAGE/'birth-native-checks.log',STAGE/'source-snapshot-6.json',STAGE/'native-run-4.log',STAGE/'content-build-6.log',STAGE/'integration-build-6.log',*(STAGE/n for n in failed if n!='failed-chain-focus-1.tar.gz'),focus_archive,*configuration()['extra_inputs']]
    grants=['sorceress.nova','sorceress.frostnova','sorceress.blaze','sorceress.firewall','sorceress.fireball','sorceress.warmth','sorceress.chainlightning','sorceress.lightning','sorceress.staticfield']
    registry=e.load(ROOT/'data/Sanctuary/Balance/named-active-skills.json');assert len(registry)==27 and all(t in registry for t in grants)
    result=dict(component='Complete Lazarus Spire/Salamander/Iron Jang Bong direct grants and idempotent equipment-stat refresh',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=66,markers=dict(sorted(expected_markers().items())),connections=27,actualAbilityWireInputs=24,boots=3,saves=3,reloads=2,namedDirectGrantRegistryBefore=18,namedDirectGrantRegistryAfter=27,newDirectGrants=grants,newNamedItemsAdmitted=['unique/56','unique/57','unique/58'],namedDefinitionsCompared=573,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=data,contentBuild=6,integrationBuild=6,sourceSnapshot=6,nativeRun=4,gameplaySourceChanged=True,nativeRebuilt=False,persistedSchemaChanged=False,installed=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Complete weighted originals;untrained active input and passive Warmth;equipment/trade/identity/fractional resource and two-reload controls. Unchanged item/set stat modifiers now retain identity to avoid native transient mana clamps. No graphical-client certificate,full-class claim,proc/charge/aura admission,new reference read,imagegen or deployment;whole Hellslayer remains gated.')
    e.write('sorceress-staff-grants-native-evidence.json',result,files,check)
    print('Three staffs:66results/24wire inputs/27connections/two reloads;nine direct grants,three complete items admitted;unchanged-refresh mana regression passes.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
