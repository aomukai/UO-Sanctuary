#!/usr/bin/env python3
"""Bind Immolation ordered area,parent and periodic native delivery with originals."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_explosive_arrow_contact import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/immolation-contact';BASE='5fa6641ae3e4ff4d8e1da78f2bea983f6a90bc26'
sys.path.insert(0,str(ROOT/'tests'))
from check_immolation_contact import configuration

def expected_markers():
    r=prior_markers()
    for f in ('immolation-contact','immolation-arrow'):r[f+'-check']+=1;r[f+'-reload-check']+=2
    r['immolation-arrow-state-result']+=1
    for n in ('immolation-contact-right','immolation-contact-quick'):r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/explosive-arrow-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    metadata='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[metadata]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240 and [b['talentId']for a,b in zip(before['skills'],after['skills'])if a!=b]==['amazon.immolationarrow']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['src/Sanctuary.Content/BowMissileContact.cs','src/Sanctuary.Content/FireGround.cs','src/Sanctuary.Content/ImmolationArrows.cs','src/Sanctuary.Content/PlayerWeaponContact.cs','src/Sanctuary.Content/PlayerWeaponMissileContact.cs','src/Sanctuary.Content/WeaponMissiles.cs','tests/Sanctuary.Integration/ImmolationContactChecks.cs'],changed
    assert all(p in born['sourceInputs']for p in parent['sourceInputs']if p.endswith('.cs'))
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',2),('Integration',4)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll'];text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==312;log=e.cohort(STAGE,expected_markers(),312);assert 'pass across3boots/2reloads' in (STAGE/'native-run-3.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==101 and 'denied by connection filter' not in log and 'Immolation unsupported context' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    s=e.load(STAGE/'immolation-contact-fixtures.json');assert s['Generation']==2 and s['Life']>0 and s['TargetLife']>0 and s['Mana']==17 and s['Quantity']>0 and s['Flight']['Frame']>0 and s['Flight']['Missile']=='immolationarrow'
    assert not s['Flight']['Damage']['FreezeColdLength'] and s['Flight']['Damage']['Conversion']is None and s['Flight']['Damage']['Elements']['PoisonMin']==153 and s['Flight']['Damage']['Elements']['FireMin']>0
    assert s['Field']['SkillRank']==s['Flight']['SkillRank']==1 and s['Field']['Ability']==s['Flight']['Ability']==s['Cooldown']['Ability']
    assert s['Field']['Min256']==48 and s['Field']['Max256']==100 and s['Field']['DamageRate']==41 and s['Field']['Size']==1
    assert s['Cooldown']['Owner']==s['Owner'] and s['Field']['Owner']==s['Owner']
    bow,gloves,helm=json.loads(s['Gear']);assert bow['NamedItemId']is None and gloves['NamedItemId']=='unique/232' and helm['NamedItemId']=='unique/207';profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json');baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_immolation_contact import record as review
    review(True)
    files=[ROOT/'tools/record_immolation_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/immolation-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Immolation Arrow ordered immediate area,weapon contact and periodic fields',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=312,markers=dict(sorted(expected_markers().items())),connections=101,newWireInputs=6,retainedWireInputs=90,boots=3,saves=3,reloads=2,contactScenarios=12,scenariosOnFirstBoot=11,fieldScenarios=4,crossbowChecks=3,savedOriginalContexts=1,continuedParentAreaFieldTriples=2,knownWardrobeSaveWarnings=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[metadata],changedMechanicalRuntimeData=[],metadataOnly='One remaining-description string refreshed before builds/native verification;mechanical and completion data unchanged.',contentBuild=2,integrationBuild=4,sourceSnapshot=4,nativeRun=3,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Immediate captured fire-only area reuses HitFlags2/ResultFlags5 ordered packets before the parent. The separate full weapon hit reuses typed HP/source poison/drain/armor/pre-HP item events/native return;accuracy PMH precedes the area,and area-lethal primaries receive no second weapon contact. Source0 cells retain one collider per update with DamageRate41,one19/128 SOFTHIT draw,HitClass32 and periodic damage kind;they emit neither weapon procs nor direct-damage notifications/ordinary GETHIT recovery. Eleven initial cases,four field cases,Crossbow,natural right/quick inputs and two saved original parent/area/field continuations pass with90retained wire inputs and existing source/gear/cold-freeze/terrain/Pierce/footprint/99update/lifetime/cooldown/resource controls. Two exact failed attempts are frozen;production2 snapshots recipients before lethal area delivery. Integration4 isolates saved-target setup from continuing source poison;combat poison remains active in hit controls. Unsupported recipients are reported. Modern global/stat/provider/owner/seed/world geometry,exceptional/uninterruptible/death-delay/hireling/PvP,other missiles,complete named/class/client and separate effects/audio/icons remain unqualified. No saved field/version,native rebuild,installation,updated client read,eligibility change or class certificate.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/immolation-contact')
    e.write('immolation-contact-native-evidence.json',result,files,check);print('Immolation:312results/6new inputs/12contacts/4field cases/2saved continuations/2reloads.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
