#!/usr/bin/env python3
"""Bind ordered Plague Javelin parent/moving-cloud delivery, original saves and learned input."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_poison_javelin_contact import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/plague-javelin-contact';BASE='fca8d0a135045aab702ed876d7b69457402fe08a'
sys.path.insert(0,str(ROOT/'tests'))
from check_plague_javelin_contact import configuration

def expected_markers():
    r=prior_markers()
    for f in ('plague-javelin-contact','plague-javelin'):r[f+'-check']+=1;r[f+'-reload-check']+=2
    for n in ('plague-javelin-contact-right','plague-javelin-contact-quick'):r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/poison-javelin-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    metadata='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[metadata]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240 and [b['talentId']for a,b in zip(before['skills'],after['skills'])if a!=b]==['amazon.plaguejavelin']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['src/Sanctuary.Content/BowMissileContact.cs','src/Sanctuary.Content/PlagueJavelins.cs','src/Sanctuary.Content/PlayerWeaponMissileContact.cs','tests/Sanctuary.Integration/PlagueJavelinChecks.cs','tests/Sanctuary.Integration/PlagueJavelinContactChecks.cs'],changed
    assert all(p in born['sourceInputs']for p in parent['sourceInputs']if p.endswith('.cs'))
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',1)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll'];text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==402;log=e.cohort(STAGE,expected_markers(),402);assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==131 and 'denied by connection filter' not in log and 'Plague Javelin cloud unsupported context' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as a:
        for name in ('Content','Integration'):assert hashlib.sha256(a.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    s=e.load(STAGE/'plague-javelin-contact-fixtures.json');assert s['Generation']==2 and s['Life']>0 and s['PrimaryLife']==s['SecondaryLife']==60000*256 and s['Mana']==17 and s['Quantity']>0
    f=s['Flight'];c=s['Cloud'];assert f['Missile']=='plaguejavelin' and f['Frame']>0 and not f['PathChanged'] and f['Damage']['Elements']['PoisonMin']==262 and f['Damage']['Elements']['PoisonFrames']==175
    assert c['Frame']==1 and c['Frames']==87 and c['Vx']==.0234375 and c['Vy']==0 and len(c['SourceSteps']['Cells'])>0 and c['LastCollision']==s['Secondary'] and c['Damage']['Min256']==c['Damage']['Max256']==0 and c['Damage']['Elements']['PoisonMin']==109 and c['Damage']['Elements']['PoisonFrames']==75 and c['NextUtc']==s['Cooldown']
    gear=json.loads(s['Gear']);assert len(gear)==3 and gear[0]['NamedItemId']is None and [g['NamedItemId']for g in gear[1:]]==['unique/232','unique/207'];profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json');baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_plague_javelin_contact import record as review
    review(True)
    files=[ROOT/'tools/record_plague_javelin_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/plague-javelin-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'docs/t29-combat-potions.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Plague Javelin ordered weapon parent and source0 moving cloud contacts',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=402,markers=dict(sorted(expected_markers().items())),connections=131,newWireInputs=6,retainedWireInputs=120,boots=3,saves=3,reloads=2,parentScenarios=10,cloudScenarios=8,savedOriginalContexts=1,savedOriginalParentAndCloudPairs=1,continuedParentAndCloudPairs=2,knownWardrobeSaveWarnings=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[metadata],changedMechanicalRuntimeData=[],metadataOnly='One remaining-description refreshed before builds/native verification;mechanical and completion data unchanged.',contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,failedAttempts=[],retainedFixtureCorrection='Original PlagueJavelinChecks reload now includes109 FP256 initial poison in parent HP before its later clock.',magicalPenetration='User explicitly confirmed weapon-skill exclusion for Poison Javelin and Plague Javelin clouds.',nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Source128 parent reuses ordered weapon item events,HP,initial poison rate and later clock,cold,drain,armor,credit,kill and native return. Source0 clouds use Successful1/GetHit0/HitClass0/HitFlags0 and the shared initial-rate HP/poison receiver without weapon callbacks,proc streams,Crushing Blow,drain,slow or armor. Existing hit02 emission,23-cloud8+15rings,override velocities,87-frame source-step motion,rank/live-hard-synergy/mastery,footprint and LastCollide remain. Ten parent and eight cloud cases,natural right/quick input and two saved parent/cloud/gear/proc/wound/mana/quantity/delay continuations pass,with120retained inputs and all existing source probes. Modern initial-rate callback/provider/owner/RNG/scheduler/room/grid/depleted/exceptional/PvP and full named/class/client/asset acceptance remain open. No saved field/version,native rebuild,installation,updated client read,eligibility change or class certificate.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/plague-javelin-contact')
    e.write('plague-javelin-contact-native-evidence.json',result,files,check);print('Plague Javelin:402results/6new inputs/10parent+8cloud cases/2saved parent-cloud continuations/2reloads.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
