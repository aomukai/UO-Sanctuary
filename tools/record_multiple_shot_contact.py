#!/usr/bin/env python3
"""Qualify Multiple Shot's shared bow impact,fan event partition and saved original."""
import argparse,hashlib,json,re,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_magic_arrow_contact import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/multiple-shot-contact';BASE='311999c2b95ea79e11c1765b6a023f919cf3d988'
sys.path.insert(0,str(ROOT/'tests'))
from check_multiple_shot_contact import configuration

def expected_markers():
    r=prior_markers()
    for f in ('multiple-shot-contact','multiple-shot'):r[f+'-check']+=1;r[f+'-reload-check']+=2
    for n in ('multiple-shot-contact-right','multiple-shot-contact-quick'):r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/magic-arrow-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    metadata='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[metadata]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240
    changes=[b['talentId']for a,b in zip(before['skills'],after['skills'])if a!=b];assert changes==['amazon.multipleshot']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/AmazonBow.cs','src/Sanctuary.Content/BowLaunch.cs','src/Sanctuary.Content/BowMissileContact.cs','src/Sanctuary.Content/WeaponMissiles.cs','tests/Sanctuary.Integration/MagicArrowContactChecks.cs','tests/Sanctuary.Integration/MultipleShotChecks.cs','tests/Sanctuary.Integration/MultipleShotContactChecks.cs'],changed
    removed=sorted(p for p in parent['sourceInputs']if p.endswith('.cs')and p not in born['sourceInputs']);assert removed==['src/Sanctuary.Content/MagicArrowContact.cs'],removed
    path='tests/Sanctuary.Integration/MagicArrowContactChecks.cs';old=subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT).decode().replace('MagicArrowContact.Handles','BowMissileContact.Handles').replace('private sealed class Rolls:','internal sealed class Rolls:')
    for name in ('Own','Flights','Owner','Target','Ready','Clean','Hold','Drain'):old=re.sub(r'private static ([^\n]+\b'+name+r'\()',r'internal static \1',old)
    assert (ROOT/path).read_text()==old,'Retained Magic Arrow fixture changed beyond helper visibility and adapter rename'
    path='tests/Sanctuary.Integration/MultipleShotChecks.cs';old=subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT).decode();assert (ROOT/path).read_text()==old.replace('TimedEffects.CanRegenerateHits(target)==outer,"Actual central/outer item callback partition"','!TimedEffects.CanRegenerateHits(target),"Actual central/outer hit-event partition and separate accuracy PMH"')
    first=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-3.json')['inputs'];assert first['projectInputs']==final['projectInputs'];assert all(final['sourceInputs'].get(p)==d for p,d in first['sourceInputs'].items()if p.startswith(('src/','shared/')))
    assert {p:d for p,d in first['copiedData'].items()if p!=metadata}=={p:d for p,d in final['copiedData'].items()if p!=metadata}
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',2)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll'];text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==182;log=e.cohort(STAGE,expected_markers(),182);assert 'pass across3boots/2reloads' in (STAGE/'native-run-2.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==59 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'multiple-shot-contact-fixtures.json');assert saved['Generation']==2 and saved['Life']>0 and saved['TargetLife']>0 and saved['Mana']==17 and saved['Quantity']>0 and saved['Flight']['Frame']>0
    assert saved['Flight']['Missile']=='multipleshotarrow' and saved['Flight']['Flags']&0x10000==0 and saved['Flight']['NextDelay']==4 and saved['Flight']['Damage']['Elements']['PoisonMin']==114
    bow,gloves,helm=json.loads(saved['Gear']);assert bow['NamedItemId']is None and gloves['NamedItemId']=='unique/232' and helm['NamedItemId']=='unique/207'
    profile=json.loads(saved['Profile']);assert len(profile['Owners'])==1 and profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json');baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_multiple_shot_contact import record as review
    review(True)
    files=[ROOT/'tools/record_multiple_shot_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/multiple-shot-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'initial-contact-run-1.tar.gz']
    result=dict(component='Learned Multiple Shot central/outer ordered NPC missile impacts,item procs and saved central original',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=182,markers=dict(sorted(expected_markers().items())),connections=59,newWireInputs=6,retainedWireInputs=48,boots=3,saves=3,reloads=2,contactScenarios=12,scenariosOnFirstBoot=11,crossbowChecks=3,savedOriginalContexts=1,continuedCentralImpacts=2,knownWardrobeSaveWarnings=1,artifacts=born['artifacts'],changedSources=changed,removedSources=removed,changedRuntimeData=[metadata],changedMechanicalRuntimeData=[],metadataOnly='One remaining-description string refreshed before native verification;all mechanical and completion fields unchanged.',contentBuild=1,integrationBuild=2,sourceSnapshot=3,nativeRun=2,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Qualified learned named Multiple Shot arrow/bolt impacts reuse Magic Arrow and the throwing packet allocator. Central arrows issue pre-HP domissiledamage;outer attacker events are suppressed but released weapon/poison/drain and recipient events remain. Successful accuracy PMH precedes block on both;the retained test expectation is corrected against complete bound source. SrcDam96 poison scales153to114. Eleven initial provider/damage/armor/pierce/within-fan-delay/independent-cast cases plus Crossbow on each boot pass;new right/quick complete fans and48retained wire inputs pass. A captured isolated central original and its gear/proc/wound/mana/ammo state survive two reloads and resume hit procs without repayment;retained Multiple Shot separately checks full saved fan/paid windup. One angled-ray pierce fixture attempt is frozen. The shared four-frame cast lease,fan geometry and bounded owner-seed adaptation are retained,not newly claimed as modern parity. Captured outer armor is a packet-boundary proof,not original supplier/full named admission. Existing compatibility,other arrow/javelin/Blade Fury/Mirrored/NPC paths,modern attack/provider/owner/stat/RNG,exceptional/PvP/full named/class/client/asset acceptance remain separate. No installation,updated game read,new save field or class certificate.')
    e.write('multiple-shot-contact-native-evidence.json',result,files,check);print('Multiple Shot:182results/6new inputs/12cases/2reloads;eligibility and installed state preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
