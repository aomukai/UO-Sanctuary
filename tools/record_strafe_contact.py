#!/usr/bin/env python3
"""Bind Strafe ordered contacts,proc streams and original flight/lease continuation."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_immolation_contact import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/strafe-contact';BASE='976d1cc0232c0ba1ea3b60bc140597c0ee1ef547'
sys.path.insert(0,str(ROOT/'tests'))
from check_strafe_contact import configuration

def expected_markers():
    r=prior_markers()
    for f in ('strafe-contact','strafe'):r[f+'-check']+=1;r[f+'-reload-check']+=2
    for n in ('strafe-contact-right','strafe-contact-quick'):r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/immolation-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    metadata='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[metadata]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240 and [b['talentId']for a,b in zip(before['skills'],after['skills'])if a!=b]==['amazon.strafe']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['src/Sanctuary.Content/BowMissileContact.cs','tests/Sanctuary.Integration/StrafeContactChecks.cs'],changed
    assert all(p in born['sourceInputs']for p in parent['sourceInputs']if p.endswith('.cs'))
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',4)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll'];text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==330;log=e.cohort(STAGE,expected_markers(),330);assert 'pass across3boots/2reloads' in (STAGE/'native-run-4.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==107 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    s=e.load(STAGE/'strafe-contact-fixtures.json');assert s['Generation']==2 and s['Life']>0 and s['TargetLife']==60000*256 and s['Mana']==17 and s['Quantity']>0 and s['Flight']['Frame']>0 and s['Flight']['Missile']=='strafearrow'
    assert s['Flight']['Damage']['SkillAttackPercent']==30 and s['Flight']['Damage']['Elements']['PoisonMin']==153 and s['Flight']['NextDelay']==4 and s['Flight']['Cast']==s['Lease']['Cast'] and s['Target']==s['Lease']['Target'] and s['Lease']['RetainWithoutFlight']
    bow,gloves,helm=json.loads(s['Gear']);assert bow['NamedItemId']is None and gloves['NamedItemId']=='unique/232' and helm['NamedItemId']=='unique/207';profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json');baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_strafe_contact import record as review
    review(True)
    files=[ROOT/'tools/record_strafe_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/strafe-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Strafe ordered weapon impacts,series-local NHD,item procs and original flight/lease continuation',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=330,markers=dict(sorted(expected_markers().items())),connections=107,newWireInputs=6,retainedWireInputs=96,boots=3,saves=3,reloads=2,contactScenarios=13,scenariosOnFirstBoot=12,crossbowChecks=3,savedOriginalContexts=1,continuedGateAndImpactPairs=2,knownWardrobeSaveWarnings=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[metadata],changedMechanicalRuntimeData=[],metadataOnly='One remaining-description string refreshed before builds/native verification;mechanical and completion data unchanged.',contentBuild=1,integrationBuild=4,sourceSnapshot=4,nativeRun=4,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Strafe arrow/bolt impacts reuse the bow/throwing ordered typed HP/source poison/drain/cold/armor/pre-HP item event/credit/kill/return bridge. The original sequence,per-shot collector and retained per-cast four-frame lease remain. Twelve initial cases and Crossbow qualify miss/block/immunity/provider/physical/lethal/armor/pierce,empty-gap delay,expiry and independent casts,with exact actor/world streams and once-only series resources. Expiry checks separate ongoing poison settlement from weapon impacts. Natural right/quick full series and two saved original partial-flight/lease continuations qualify suppressed contacts without damage/proc/RNG and later actual damage without repayment. Existing source count/timing/resource/live gear/AR/target/NHD/input-lock and paid-windup controls pass with96retained inputs. Three failed fixture attempts are frozen;production remains build1. Modern cross-cast NHD,input queuing,provider/owner/stat/seed/geometry,exceptional/uninterruptible/death-delay/hireling/PvP,remaining missile consumers,full named/class/client and separate effect/audio/icon acceptance remain open. No saved field/version,native rebuild,installation,updated client read,item eligibility change or class certificate.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/strafe-contact')
    e.write('strafe-contact-native-evidence.json',result,files,check);print('Strafe:330results/6new inputs/13cases/2saved gate-and-hit continuations/2reloads.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
