#!/usr/bin/env python3
"""Bind Fire/Cold Arrow ordered impacts and two saved original projectile contexts."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_multiple_shot_contact import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/elemental-arrow-contact';BASE='35b8606d43ff63a0bd038d332127b4ed4ecfefd6'
sys.path.insert(0,str(ROOT/'tests'))
from check_elemental_arrow_contact import configuration

def expected_markers():
    r=prior_markers()
    for f in ('elemental-arrow-contact','fire-arrow','cold-arrow'):r[f+'-check']+=1;r[f+'-reload-check']+=2
    for a in ('fire','cold'):
        for m in ('right','quick'):n=f'elemental-arrow-{a}-{m}';r[n+'-check']+=3;r[n+'-result']+=3
    r['cold-arrow-state-result']+=1
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/multiple-shot-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    metadata='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[metadata]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240
    assert sorted(b['talentId']for a,b in zip(before['skills'],after['skills'])if a!=b)==['amazon.coldarrow','amazon.firearrow'];assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['src/Sanctuary.Content/BowMissileContact.cs','tests/Sanctuary.Integration/ElementalArrowContactChecks.cs'],changed
    assert all(p in born['sourceInputs']for p in parent['sourceInputs']if p.endswith('.cs'))
    first=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-4.json')['inputs'];assert first['projectInputs']==final['projectInputs'] and first['copiedData']==final['copiedData'];assert all(final['sourceInputs'].get(p)==d for p,d in first['sourceInputs'].items()if p.startswith(('src/','shared/')))
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',3)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll'];text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==216;log=e.cohort(STAGE,expected_markers(),216);assert 'pass across3boots/2reloads' in (STAGE/'native-run-4.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==71 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    saved=e.load(STAGE/'elemental-arrow-contact-fixtures.json');assert len(saved)==2
    for s,element in zip(saved,('fire','cold')):
        assert s['Generation']==2 and s['Life']>0 and s['TargetLife']>0 and s['Mana']==17 and s['Quantity']>0 and s['Flight']['Frame']>0
        assert s['Flight']['Missile']==element+'arrow' and s['Flight']['Damage']['Conversion']==dict(Element=element,Percent=3) and s['Flight']['Damage']['Elements']['PoisonMin']==153
        if element=='cold':assert s['Flight']['Damage']['Elements']['ColdFrames']==100
        bow,gloves,helm=json.loads(s['Gear']);assert bow['NamedItemId']is None and gloves['NamedItemId']=='unique/232' and helm['NamedItemId']=='unique/207';profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json');baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_elemental_arrow_contact import record as review
    review(True)
    files=[ROOT/'tools/record_elemental_arrow_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/elemental-arrow-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'unavailable-corridor-run-1.tar.gz',STAGE/'cold-delivery-run-2.tar.gz',STAGE/'uncounted-expiry-run-3.tar.gz']
    result=dict(component='Learned Fire/Cold Arrow ordered conversion impacts,item procs,cold and saved originals',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=216,markers=dict(sorted(expected_markers().items())),connections=71,newWireInputs=12,retainedWireInputs=54,boots=3,saves=3,reloads=2,contactScenarios=15,scenariosOnFirstBoot=13,crossbowChecks=6,savedOriginalContexts=2,continuedImpacts=4,knownWardrobeSaveWarnings=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[metadata],changedMechanicalRuntimeData=[],metadataOnly='Two remaining-description strings refreshed before builds/native verification;all mechanical and completion fields unchanged.',contentBuild=1,integrationBuild=3,sourceSnapshot=4,nativeRun=4,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Only the shared bow qualifier/row metadata changes in production. Learned named Fire/Cold Arrow impacts now use the existing ordered source poison/typed HP/credit/status/kill/return and pre-HP missile item event bridge,with pinned HitClass0 and existing released conversion. Cold length is mitigated once and committed after HP;100-frame,50%-resisted and immune/miss/block paths pass. Thirteen initial provider/damage/poison/cold cases plus two Crossbow variants pass with natural right/quick input for both skills and54retained wire inputs. Two original partial flights preserve gear/proc/wound/mana/ammo context across two reloads and resume four impacts without repayment. Out-of-range corridor and launch-time-versus-impact-time cold assertion attempts are frozen;neither changed production behavior. A complete216-pass native cohort was frozen because its Python runner omitted the existing asynchronous cold expiry marker;the corrected driver explicitly waits for that marker and reuses the exact same assemblies. Existing creator/rank/gear/conversion/cold and Magic/Multiple/throwing/item controls remain unchanged and pass. No new saved field,version,installation,updated game read,item eligibility or class certificate. Modern AR/source/mastery/provider/owner/RNG/attack timing,ordinary cold shatter,other missiles,exceptional/PvP/full named/class/client/asset acceptance remain open.')
    e.write('elemental-arrow-contact-native-evidence.json',result,files,check);print('Fire/Cold Arrow:216results/12new inputs/15cases/2saved originals/4continued impacts/2reloads.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
