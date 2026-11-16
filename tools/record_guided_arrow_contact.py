#!/usr/bin/env python3
"""Bind Guided Arrow ordered auto-hit impact and proc-bearing homing continuation."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_ice_arrow_contact import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/guided-arrow-contact';BASE='cd6597ea01d08d215bb4190b6e652a44fbf2adf6'
sys.path.insert(0,str(ROOT/'tests'))
from check_guided_arrow_contact import configuration

def expected_markers():
    r=prior_markers()
    for f in ('guided-arrow-contact','guided-arrow'):r[f+'-check']+=1;r[f+'-reload-check']+=2
    for n in ('guided-arrow-contact-right','guided-arrow-contact-quick'):r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/ice-arrow-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    metadata='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[metadata]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240
    assert [b['talentId']for a,b in zip(before['skills'],after['skills'])if a!=b]==['amazon.guidedarrow'];assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['src/Sanctuary.Content/BowMissileContact.cs','src/Sanctuary.Content/WeaponMissiles.cs','tests/Sanctuary.Integration/GuidedArrowContactChecks.cs'],changed
    assert all(p in born['sourceInputs']for p in parent['sourceInputs']if p.endswith('.cs'))
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name in ('Content','Integration'):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll'];text=(STAGE/(name.lower()+'-build-1.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==259;log=e.cohort(STAGE,expected_markers(),259);assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==83 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    s=e.load(STAGE/'guided-arrow-contact-fixtures.json');assert s['Generation']==2 and s['Life']>0 and s['TargetLife']>0 and s['Mana']==17 and s['Quantity']>0 and s['Flight']['Frame']>0 and s['Flight']['Missile']=='guidedarrow'
    assert s['Flight']['Guided']['Mode']==1 and s['Flight']['Guided']['Target']==s['Target'] and s['Flight']['Pierces']==0 and s['Flight']['Damage']['Elements']['PoisonMin']==153
    bow,gloves,helm=json.loads(s['Gear']);assert bow['NamedItemId']is None and gloves['NamedItemId']=='unique/232' and helm['NamedItemId']=='unique/207';profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json');baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_guided_arrow_contact import record as review
    review(True)
    files=[ROOT/'tools/record_guided_arrow_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/guided-arrow-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Learned Guided Arrow auto-hit ordered impact,item procs and saved homing original',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=259,markers=dict(sorted(expected_markers().items())),connections=83,newWireInputs=6,retainedWireInputs=72,boots=3,saves=3,reloads=2,contactScenarios=10,scenariosOnFirstBoot=9,crossbowChecks=3,savedOriginalContexts=1,continuedImpacts=2,knownWardrobeSaveWarnings=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[metadata],changedMechanicalRuntimeData=[],metadataOnly='One remaining-description string refreshed before builds/native verification;mechanical and completion data unchanged.',contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Guided named NPC impacts reuse the qualified bow/throwing ordered poison/typed HP/credit/status/kill/return bridge and pre-HP missile item hit tickets. Current ToHit0 plus the older complete router imply bypass of accuracy PMH;the implementation skips only that setup for guided controllers and records current live3.3PMH as unverified. Exact long PMH state is tested separately from temporary poison inhibition. Nine initial cases cover targeted/block/immune/provider/physical/lethal/interceptor/ground/armor,plus Crossbow;AR delegates throw and source shields remain. Authenticated right/quick inputs and an original partial homing controller/packet/proc/wound/mana/ammo context pass two reloads without repayment. Existing360launch/2016steer/288hit-state and world/three-phase persistence controls remain unchanged with72retained wire inputs. First builds/full cohort pass. Modern PMH/path/target-loss/provider/owner/stat/RNG/attack,other missiles,exceptional/PvP/full named/class/client/asset acceptance remain explicit. No new save field/version,installation,updated game read,item eligibility or class certificate.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/guided-arrow-contact')
    e.write('guided-arrow-contact-native-evidence.json',result,files,check);print('Guided Arrow:259results/6new inputs/10cases/2reloads;current PMH inference stays explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
