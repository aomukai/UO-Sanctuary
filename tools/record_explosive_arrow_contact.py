#!/usr/bin/env python3
"""Bind parent weapon and independent element-only explosion child packets."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_guided_arrow_contact import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/explosive-arrow-contact';BASE='c80fb48ba0638ee5d70d96d1996b9a4c5f683427'
sys.path.insert(0,str(ROOT/'tests'))
from check_explosive_arrow_contact import configuration

def expected_markers():
    r=prior_markers()
    for f in ('explosive-arrow-contact','exploding-arrow','freezing-arrow'):r[f+'-check']+=1;r[f+'-reload-check']+=2
    r['freezing-arrow-state-result']+=1
    for a in ('fire','freeze'):
        for m in ('right','quick'):n=f'explosive-arrow-{a}-{m}';r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/guided-arrow-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    metadata='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[metadata]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240 and sorted(b['talentId']for a,b in zip(before['skills'],after['skills'])if a!=b)==['amazon.explodingarrow','amazon.freezingarrow']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['src/Sanctuary.Content/BowExplosions.cs','src/Sanctuary.Content/BowMissileContact.cs','src/Sanctuary.Content/PlayerWeaponMissileContact.cs','tests/Sanctuary.Integration/ExplosiveArrowContactChecks.cs'],changed
    assert all(p in born['sourceInputs']for p in parent['sourceInputs']if p.endswith('.cs'))
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name in ('Content','Integration'):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll'];text=(STAGE/(name.lower()+('-build-1.log'if name=='Content'else'-build-3.log'))).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==293;log=e.cohort(STAGE,expected_markers(),293);assert 'pass across3boots/2reloads' in (STAGE/'native-run-3.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==95 and 'denied by connection filter' not in log and 'Bow explosion unsupported context' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'explosive-arrow-contact-fixtures.json');assert len(saved)==2
    for s,missile in zip(saved,('explodingarrow','freezingarrow')):
        assert s['Generation']==2 and s['Life']>0 and s['TargetLife']>0 and s['Mana']==17 and s['Quantity']>0 and s['Flight']['Frame']>0 and s['Flight']['Missile']==missile
        assert not s['Flight']['Damage']['FreezeColdLength'] and s['Flight']['Damage']['Conversion']is None and s['Flight']['Damage']['Elements']['PoisonMin']==153
        bow,gloves,helm=json.loads(s['Gear']);assert bow['NamedItemId']is None and gloves['NamedItemId']=='unique/232' and helm['NamedItemId']=='unique/207';profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json');baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_explosive_arrow_contact import record as review
    review(True)
    files=[ROOT/'tools/record_explosive_arrow_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/explosive-arrow-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Exploding/Freezing Arrow ordered parent weapon and element-only child area packets',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=293,markers=dict(sorted(expected_markers().items())),connections=95,newWireInputs=12,retainedWireInputs=78,boots=3,saves=3,reloads=2,contactScenarios=22,scenariosOnFirstBoot=20,crossbowChecks=6,savedOriginalContexts=2,continuedParentChildPairs=4,knownWardrobeSaveWarnings=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[metadata],changedMechanicalRuntimeData=[],metadataOnly='Two remaining-description strings refreshed before builds/native verification;mechanical and completion data unchanged.',contentBuild=1,integrationBuild=3,sourceSnapshot=3,nativeRun=3,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Both parent and NPC child now reuse shared ordered HP/status/credit/kill/return. Parent source128 weapon packets retain accuracy PMH,poison/drain,captured armor and pre-HP item hit tickets. Existing independent child creation survives miss/block/terrain/expiry;one elemental roll is copied across radius recipients. Child HitFlags2/ResultFlags5 has normal hit recovery but no weapon hit callbacks,proc tickets,poison,drain,armor or extra resource payment. Existing avoidance and Dragon freeze adapters are reused. Twenty initial cases plus two Crossbow variants,natural right/quick inputs,and two original partial parents through two reloads pass with78retained inputs and unchanged creator/area/pierce/terrain/lifetime/gear/freeze/thaw/corpse/parent-child persistence controls. Four continued pairs each emit one parent ticket and one separate child. Unsupported recipients are explicitly reported;no substitute mode is invented. Content remains its first clean build;two frozen fixture attempts isolate the area recipient from source regeneration awakened by native return. Modern global catch-up/geometry/seed/stat/provider/owner,exceptional/uninterruptable/death-delay/PvP,other missiles and complete named/class/client/asset acceptance remain open. No save field/version,installation,updated game read,item eligibility or class certificate.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/explosive-arrow-contact')
    e.write('explosive-arrow-contact-native-evidence.json',result,files,check);print('Explosive arrows:293results/12new inputs/22cases/4continued parent-child pairs/2reloads.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
