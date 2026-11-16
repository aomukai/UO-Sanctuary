#!/usr/bin/env python3
"""Qualify released Double Throw proc flights with first and initialized actor streams."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/thrown-proc-flight';BASE='c6ebada6fb13feafe9e2eb186d0dd30755cfe074'
sys.path.insert(0,str(ROOT/'tests'))
from check_thrown_proc_flight import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c.get('extra_wait_results',{}):
                waits=c['extra_wait_results'][n]
                for result in ((waits,)if isinstance(waits,str)else waits):r[result]+=count
    for n in c.get('extra_checks',()):
        r[n]+=3
        if n in c.get('extra_wait_results',{}):r[c['extra_wait_results'][n]]+=3
    for n in c.get('extra_pre_save_commands',()):r[n]+=3
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/thrown-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['tests/Sanctuary.Integration/ThrownContactChecks.cs','tests/Sanctuary.Integration/ThrownProcFlightChecks.cs'],changed
    import re
    path='tests/Sanctuary.Integration/ThrownContactChecks.cs';old=subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT).decode().replace('private sealed class Rolls:','internal sealed class Rolls:')
    for name in ('Own','Flights','Owner','Target','Ready','Clean','Drain'):old=re.sub(r'private static ([^\n]+\b'+name+r'\()',r'internal static \1',old)
    assert (ROOT/path).read_text()==old,'Retained fixture changed beyond helper visibility'
    for p,d in parent['sourceInputs'].items():
        if p.startswith(('src/','shared/'))and p.endswith('.cs'):assert born['sourceInputs'][p]==d
    assert e.sha(STAGE/'content/Sanctuary.Content.dll')==parent['artifacts']['Assemblies/Sanctuary.Content.dll']
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name in ('Content','Integration'):assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    text=(STAGE/'integration-build-1.log').read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==143
    log=e.cohort(STAGE,expected_markers(),143);assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==47 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'thrown-proc-flight-fixtures.json');assert len(saved)==2 and [s['Initialized']for s in saved]==[False,True]
    for s in saved:
        assert s['Generation']==2 and s['OwnerLife']>0 and s['TargetLife']>0 and s['First']==39 and s['Second']==40 and s['Mana']>0 and s['Flight']['Frame']>0
        primary,secondary,gloves,helm=json.loads(s['Gear']);assert primary['NamedItemId']is None and secondary['NamedItemId']is None and gloves['NamedItemId']=='unique/232' and helm['NamedItemId']=='unique/207'
        profile=json.loads(s['Profile']);assert len(profile['Owners'])==(1 if s['Initialized']else 0)
        assert profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    files=[ROOT/'tools/record_thrown_proc_flight.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/thrown-proc-flight-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Proc-bearing released Double Throw flight continuation with first and initialized actor streams',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=143,markers=dict(sorted(expected_markers().items())),connections=47,newWireInputs=0,retainedWireInputs=42,boots=3,saves=3,reloads=2,savedOriginalContexts=2,continuedImpacts=4,knownWardrobeSaveWarnings=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[],contentBuild=None,contentReusedFrom='thrown-contact/content-build-2',integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeRebuilt=False,persistenceVersionChanged=False,existingSchemasChanged=False,installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Exact verified production DLL/data reused. The retained fixture changes only helper visibility. Two partially travelled original flights preserve their complete released packet,source identity,original gear,proc attachment/actor initialization,wounds,mana and quantities through each reload. Test-only recipient frozen/resistance overrides are restored only after saved-state comparison. Resumed impact issues one pre-HP missile hit event and uses either the existing actor stream or one first-hit initialization from the current shared world stream,then one separate child allocation. The emitted rank20Fire Ball deals actual native damage with player credit,without recursive procs or resource debits. Four continued impacts through two reloads pass with all42retained wire inputs and existing sequence/travel/melee/item controls. Modern hand/provider/owner/world/attack timing,other missile families,exceptional/PvP/full named admission and class/client/asset certification remain explicit. No production rewrite,new schema or deployment.')
    e.write('thrown-proc-flight-native-evidence.json',result,files,check)
    print('Released proc flights:143results/2saved contexts/4continued impacts/2reloads;production unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
