#!/usr/bin/env python3
"""Bind pure-lightning reset,ordered item events and original javelin continuation."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_strafe_contact import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/lightning-bolt-contact';BASE='038c91b1da0bad7612ac3f68bcd170451792f25e'
sys.path.insert(0,str(ROOT/'tests'))
from check_lightning_bolt_contact import configuration

def expected_markers():
    r=prior_markers()
    for f in ('lightning-bolt-contact','lightning-bolt'):r[f+'-check']+=1;r[f+'-reload-check']+=2
    for n in ('lightning-bolt-contact-right','lightning-bolt-contact-quick'):r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/strafe-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    metadata='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[metadata]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240 and [b['talentId']for a,b in zip(before['skills'],after['skills'])if a!=b]==['amazon.lightningbolt']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['src/Sanctuary.Content/BowMissileContact.cs','src/Sanctuary.Content/PlayerWeaponMissileContact.cs','src/Sanctuary.Content/WeaponMissiles.cs','tests/Sanctuary.Integration/LightningBoltContactChecks.cs'],changed
    assert all(p in born['sourceInputs']for p in parent['sourceInputs']if p.endswith('.cs'))
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',1)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll'];text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==348;log=e.cohort(STAGE,expected_markers(),348);assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==113 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    s=e.load(STAGE/'lightning-bolt-contact-fixtures.json');assert s['Generation']==2 and s['Life']>0 and s['TargetLife']==60000*256 and s['Mana']==17 and s['Quantity']>0 and s['Flight']['Frame']>0 and s['Flight']['Missile']=='lightningjavelin'
    damage=s['Flight']['Damage'];assert damage['LightningJavelinPercent']==100 and damage['Elements']['PoisonMin']==114 and damage['Elements']['ColdMin']>0 and damage['Elements']['LifeDrain']>0 and damage['Elements']['ManaDrain']>0 and len(s['Flight']['SourceSteps']['Cells'])>0
    gear=json.loads(s['Gear']);assert len(gear)==5 and gear[0]['NamedItemId']is None and [g['NamedItemId']for g in gear[1:]]==['unique/232','unique/207','unique/118','unique/121'];profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json');baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_lightning_bolt_contact import record as review
    review(True)
    files=[ROOT/'tools/record_lightning_bolt_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/lightning-bolt-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Lightning Bolt pure-lightning ordered weapon hit events and original source-step flight',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=348,markers=dict(sorted(expected_markers().items())),connections=113,newWireInputs=6,retainedWireInputs=102,boots=3,saves=3,reloads=2,contactScenarios=8,scenariosOnFirstBoot=8,savedOriginalContexts=1,continuedPureImpacts=2,knownWardrobeSaveWarnings=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[metadata],changedMechanicalRuntimeData=[],metadataOnly='One remaining-description string refreshed before builds/native verification;mechanical and completion data unchanged.',contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Lightning Bolt retains source96 capture,enhanced/critical physical conversion,second lightning roll and full reset. Actual lightningjavelin impacts now reuse ordered typed HP,pre-HP item hit events,credit/kill/native return and captured armor. The allocator explicitly zeroes poison/rate/length and drain;row ToHit0 bypasses accuracy PMH and pure lightning requests no physical shield check. Owner item-freeze remains independent. Eight cases cover normal,immunity,resistance,removed provider,lethal,armor,pierce and Iceblink,with actual poison/cold/life/mana-leech gear before reset and exact packet/HP/actor/world/resource assertions. AR and physical shield delegates throw. Natural right/quick input and two original partial source-step/gear/proc/wound/mana/quantity continuations pass with102retained wire inputs and existing creator/reset/second-roll/material/potion/TH/quantity/Crushing Blow/Pierce/lifecycle controls. First builds/full cohort pass. Current live PMH,scheduler/stat/provider/owner/seed/geometry,depleted providers,exceptional/uninterruptible/death-delay/hireling/PvP,other missile consumers,full named/class/client and separate effect/audio/icon acceptance remain open. No saved field/version,native rebuild,installation,updated client read,eligibility change or class certificate.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/lightning-bolt-contact')
    e.write('lightning-bolt-contact-native-evidence.json',result,files,check);print('Lightning Bolt:348results/6new inputs/8cases/2saved pure-lightning impacts/2reloads.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
