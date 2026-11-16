#!/usr/bin/env python3
"""Bind Fury weapon parents,source0 bolts and independently saved flights."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_lightning_bolt_contact import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/lightning-fury-contact';BASE='549ca567825bfcc332cc7ed131c71df15365a40f'
sys.path.insert(0,str(ROOT/'tests'))
from check_lightning_fury_contact import configuration

def expected_markers():
    r=prior_markers()
    for f in ('lightning-fury-contact','lightning-fury'):r[f+'-check']+=1;r[f+'-reload-check']+=2
    for n in ('lightning-fury-contact-right','lightning-fury-contact-quick'):r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/lightning-bolt-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    metadata='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[metadata]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240 and [b['talentId']for a,b in zip(before['skills'],after['skills'])if a!=b]==['amazon.lightningfury']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['src/Sanctuary.Content/BowMissileContact.cs','src/Sanctuary.Content/PlayerWeaponMissileContact.cs','src/Sanctuary.Content/WeaponMissiles.cs','tests/Sanctuary.Integration/LightningFuryContactChecks.cs'],changed
    assert all(p in born['sourceInputs']for p in parent['sourceInputs']if p.endswith('.cs'))
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',1)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll'];text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==366;log=e.cohort(STAGE,expected_markers(),366);assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==119 and 'denied by connection filter' not in log and 'Fury child unsupported context' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    s=e.load(STAGE/'lightning-fury-contact-fixtures.json');assert s['Generation']==2 and s['Life']>0 and s['PrimaryLife']==s['SecondaryLife']==60000*256 and s['Mana']==17 and s['Quantity']>0 and len(s['Flights'])==2
    parentFlight=next(f for f in s['Flights']if f['Missile']=='lightningfury');child=next(f for f in s['Flights']if f['Missile']=='furylightning');assert all(f['Frame']>0 and len(f['SourceSteps']['Cells'])>0 for f in s['Flights'])
    assert parentFlight['Damage']['LightningJavelinPercent']is None and parentFlight['Damage']['Elements']['PoisonMin']==153 and child['Pierces']==0 and child['Damage']['Min256']==child['Damage']['Max256']==0 and child['Damage']['Elements']['LightningMin']>0
    assert all(v==0 for k,v in child['Damage']['Elements'].items()if k not in('LightningMin','LightningMax'))
    gear=json.loads(s['Gear']);assert len(gear)==3 and gear[0]['NamedItemId']is None and [g['NamedItemId']for g in gear[1:]]==['unique/232','unique/207'];profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json');baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_lightning_fury_contact import record as review
    review(True)
    files=[ROOT/'tools/record_lightning_fury_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/lightning-fury-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Lightning Fury ordered weapon parent and independent source0 bolt contacts',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=366,markers=dict(sorted(expected_markers().items())),connections=119,newWireInputs=6,retainedWireInputs=108,boots=3,saves=3,reloads=2,parentScenarios=8,childScenarios=6,savedOriginalContexts=1,savedOriginalFlights=2,continuedParentAndChildPairs=2,knownWardrobeSaveWarnings=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[metadata],changedMechanicalRuntimeData=[],metadataOnly='One remaining-description string refreshed before builds/native verification;mechanical and completion data unchanged.',contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Fury source128 parent reuses ordered typed HP/source poison/drain/cold/armor/pre-HP weapon item events/credit/kill/native return. Existing hit20 search/emission precedes direct or shield-blocked contact. Source0 bolts use elemental lightning HitFlags0/Successful+GetHit/HitClass64 without weapon callbacks,Crushing Blow,poison,cold,drain,slow,armor or proc-stream allocation;direct spell damage/credit/kill/return remain. Existing source path,LastCollide,unlimited bolt contacts,25-frame life,parent Pierce,rank/live-hard-synergy/mastery at birth and null-expiry a4 behavior remain. Eight parent and six child cases prove separate phases,RNG/HP/resources,piercing and no weapon-effect/shield requests on bolts. Natural right/quick input and two saved original parent/child continuations retain exact gear/proc/wound/mana/quantity while qualifying a fresh burst and an independently retained bolt. Existing source/creator/target/gear/Pierce/expiry/quantity/lifecycle and108retained wire inputs pass. First builds/full cohort pass. Modern primary-overlap/callback/seed/stat/provider/owner/scheduler/room-order/geometry,current PMH,depleted/exceptional/uninterruptible/death-delay/hireling/PvP,other missile consumers and full named/class/client/separate effect/audio/icon acceptance remain open. No saved field/version,native rebuild,installation,updated client read,eligibility change or class certificate.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/lightning-fury-contact')
    e.write('lightning-fury-contact-native-evidence.json',result,files,check);print('Lightning Fury:366results/6new inputs/8parent+6bolt cases/2saved parent-child pairs/2reloads.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
