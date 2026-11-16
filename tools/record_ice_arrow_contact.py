#!/usr/bin/env python3
"""Bind Ice Arrow packet freeze,shared player status delivery and saved original."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_elemental_arrow_contact import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/ice-arrow-contact';BASE='714d8b992937d79fee65fabbc8e5f6ec654a1544'
sys.path.insert(0,str(ROOT/'tests'))
from check_ice_arrow_contact import configuration

def expected_markers():
    r=prior_markers()
    for f in ('ice-arrow-contact','ice-arrow','dragon-cold-duration'):r[f+'-check']+=1;r[f+'-reload-check']+=2
    r['freeze-state-check']+=3;r['ice-arrow-state-result']+=1
    for n in ('ice-arrow-contact-right','ice-arrow-contact-quick'):r[n+'-check']+=3;r[n+'-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/elemental-arrow-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    metadata='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[metadata]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240 and [b['talentId']for a,b in zip(before['skills'],after['skills'])if a!=b]==['amazon.icearrow']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['src/Sanctuary.Content/BowMissileContact.cs','src/Sanctuary.Content/PlayerWeaponContact.cs','src/Sanctuary.Content/PlayerWeaponMissileContact.cs','tests/Sanctuary.Integration/IceArrowContactChecks.cs'],changed
    assert all(p in born['sourceInputs']for p in parent['sourceInputs']if p.endswith('.cs'))
    first=e.load(STAGE/'source-snapshot-1.json')['inputs'];final=e.load(STAGE/'source-snapshot-2.json')['inputs'];assert first['projectInputs']==final['projectInputs'] and first['copiedData']==final['copiedData'];assert all(final['sourceInputs'].get(p)==d for p,d in first['sourceInputs'].items()if p.startswith(('src/','shared/')))
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',2)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll'];text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==241;log=e.cohort(STAGE,expected_markers(),241);assert 'pass across3boots/2reloads' in (STAGE/'native-run-2.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==77 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==1 and 'WardrobeSet' in warnings[0],warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as archive:
        for name in ('Content','Integration'):assert hashlib.sha256(archive.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    s=e.load(STAGE/'ice-arrow-contact-fixtures.json');assert s['Generation']==2 and s['Life']>0 and s['TargetLife']>0 and s['Mana']==17 and s['Quantity']>0 and s['Flight']['Frame']>0 and s['Flight']['Missile']=='icearrow'
    assert s['Flight']['Damage']['FreezeColdLength'] and s['Flight']['Damage']['Conversion']is None and s['Flight']['Damage']['Elements']['ColdFrames']==50 and s['Flight']['Damage']['Elements']['PoisonMin']==153
    bow,gloves,helm=json.loads(s['Gear']);assert bow['NamedItemId']is None and gloves['NamedItemId']=='unique/232' and helm['NamedItemId']=='unique/207';profile=json.loads(s['Profile']);assert len(profile['Owners'])==1 and profile['Items'][0]['State']['Properties']==[dict(Packed=(stat<<16)|(47<<6)|20,Value=100)for stat in(195,198)]
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json');baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_ice_arrow_contact import record as review
    review(True)
    files=[ROOT/'tools/record_ice_arrow_contact.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/ice-arrow-contact-checkpoint.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'difficulty-fixture-run-1.tar.gz']
    result=dict(component='Learned Ice Arrow ordered packet freeze and shared player cold/freeze/poison delivery',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=241,markers=dict(sorted(expected_markers().items())),connections=77,newWireInputs=6,retainedWireInputs=66,boots=3,saves=3,reloads=2,contactScenarios=11,scenariosOnFirstBoot=10,crossbowChecks=3,savedOriginalContexts=1,continuedImpacts=2,knownWardrobeSaveWarnings=1,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[metadata],changedMechanicalRuntimeData=[],metadataOnly='One remaining-description string refreshed before builds/native verification;mechanical and completion data unchanged.',contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Ice Arrow uses the existing bow/throwing bridge and Dragon cold/freeze length adapter. Existing prepared player freeze is now visible in the actual transient incoming packet;the shared receiver commits cold,freeze,poison after HP and before kill/return in source order. Ten initial cases plus Crossbow,natural right/quick input,an original flight through two reloads,and retained four-Dragon/standalone freeze/creator/corpse/thaw controls pass with66retained wire inputs. Tests verify ordinary/resisted/immune/miss/block/provider/lethal corpse/elite/Nightmare/Hell behavior,50/25/12-frame state lengths,pre-HP item packets,exact actor/world draws and no resource repayment. A fresh-difficulty fixture construction attempt is frozen;production did not change afterward. The current dParam1=100 is guarded. No save field/version,installation,updated game read,item eligibility or class certificate is added. Native uninterruptable/hireling mapping,modern AR/provider/owner/stat/RNG/attack timing,ordinary nonfreeze cold shatter,other missiles and exceptional/PvP/full named/class/client/asset acceptance remain open.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/ice-arrow-contact')
    e.write('ice-arrow-contact-native-evidence.json',result,files,check);print('Ice Arrow:241results/6new inputs/11cases/Dragon and freeze controls/2reloads;installed state protected.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
