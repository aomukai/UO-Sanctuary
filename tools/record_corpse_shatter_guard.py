#!/usr/bin/env python3
"""Bind existing shattered-corpse guards, native animation gates and original saved corpses."""
import argparse,hashlib,json,subprocess,sys,tarfile
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_lightning_strike_contact import expected_markers as prior_markers
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/corpse-shatter-guard';BASE='4e5e6725e8be154b782939db360cb42f93644d57'
sys.path.insert(0,str(ROOT/'tests'))
from check_corpse_shatter_guard import configuration

def expected_markers():
    r=prior_markers();r['corpse-shatter-guard-check']+=1;r['corpse-shatter-guard-reload-check']+=2;r['corpse-shatter-guard-result']+=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/lightning-strike-contact/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    metadata='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[metadata]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    before=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));after=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert len(before['skills'])==len(after['skills'])==240 and [b['talentId']for a,b in zip(before['skills'],after['skills'])if a!=b]==['amazon.icearrow','amazon.freezingarrow']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(before['skills'],after['skills']));assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d);assert changed==['src/Sanctuary.Content/CorpseSkillRules.cs','src/Sanctuary.Content/GraveyardUndead.cs','tests/Sanctuary.Integration/CorpseShatterGuardChecks.cs'],changed
    assert all(p in born['sourceInputs']for p in parent['sourceInputs']if p.endswith('.cs'))
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for name,build in (('Content',1),('Integration',2)):
        assert e.sha(STAGE/name.lower()/f'Sanctuary.{name}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll'];text=(STAGE/(name.lower()+f'-build-{build}.log')).read_text();assert all(s in text for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert sum(expected_markers().values())==489;log=e.cohort(STAGE,expected_markers(),489);assert 'pass across3boots/2reloads' in (STAGE/'native-run-1.log').read_text();assert log.count('Client: 127.0.0.1: Connected.')==151 and 'denied by connection filter' not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete' in l];assert len(warnings)==2 and all('WardrobeSet' in w for w in warnings),warnings
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as a:
        for name in ('Content','Integration'):assert hashlib.sha256(a.extractfile(name.lower()+f'/Sanctuary.{name}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{name}.dll']
    negative=e.load(STAGE/'failed-baseline-1-native-inputs.json');assert negative['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll'] and negative['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
    with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as a:
        text=a.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode();assert 'Blocked corpse accepted:' in text and '"Shattered":true' in text and 'native=True;wild=True' in text
    assert 'AssertionError' in (STAGE/'baseline-run-1.log').read_text()
    s=e.load(STAGE/'corpse-shatter-guard-fixtures.json');assert s['Generation']==2 and s['Life']>0 and s['Mana']>=0 and len(s['Bodies'])==4
    for i,b in enumerate(s['Bodies']):
        assert b['Record']['Corpse']==b['Corpse'] and b['Record']['Shattered']==(i<2) and b['Record']['NoSelect']==(i==2) and b['Record']['Selectable'] and not b['Record']['Owned'] and len(b['Items'])==1
        assert b['Art']==0x2006 and b['Hue']!=1109
    # The behavior is a guard over existing metadata,no new ordinary-cold RNG.
    code=(ROOT/'src/Sanctuary.Content/CorpseSkillRules.cs').read_text();assert 'corpse=>!BlocksReanimation(corpse) && animation?.Invoke(corpse)!=false' in code and 'is{NoSelect:true}or{Shattered:true}' in code
    assert '!CorpseSkillRules.BlocksReanimation(corpse)' in (ROOT/'src/Sanctuary.Content/GraveyardUndead.cs').read_text()
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json');baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert len(audit['Definitions'])==573 and audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll');backlog=e.load(ROOT/'data/Sanctuary/Balance/named-skill-backlog.json');assert len(backlog['items'])==168 and len(backlog['sets'])==4
    from review_corpse_shatter_guard import record as review
    review(True)
    files=[ROOT/'tools/record_corpse_shatter_guard.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/corpse-shatter-guard-checkpoint.md',ROOT/'docs/amazon-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),STAGE/'accepted-assemblies-1.tar.gz',STAGE/'failed-baseline-1.tar.gz']
    result=dict(component='Existing shattered-corpse exclusion for native animation and wild Lich raising',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=489,markers=dict(sorted(expected_markers().items())),connections=151,newWireInputs=0,retainedWireInputs=144,boots=3,saves=3,reloads=2,savedOriginalCorpses=4,continuedCorpseChecks=2,knownWardrobeSaveWarnings=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[metadata],changedMechanicalRuntimeData=[],metadataOnly='Two Amazon freeze-arrow remaining descriptions refreshed;mechanical and completion data unchanged.',contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,negativeControl='The previously accepted production DLL reproduced Shattered=true/NoSelect=false with both native hook and wild Lich CanRaise returning true. Exact baseline sources/inputs/logs/DLLs are frozen before repair.',nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,backlogRows=168,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Shared captured NoSelect/Shattered guard reaches existing native Animate Dead initial target and delayed allocation plus wild/owned Lich selection,BeginRaise and delayed CanRaise revalidation. Existing previous native-hook veto,unknown-metadata compatibility,ordinary raising,loot and producer chance policies remain. Actual freeze/Holy Freeze captured corpses and reserved corpse fail both native entrypoints with observed hook calls and no actors/resources/loot mutation. Fresh plain corpse actually animates through native delayed code. Real wild Lich raises ordinary corpse after its normal startup cooldown;queued reservation blocks commit. Four original corpse flags/identities/gold/art and wounded caster resources persist through two reloads. Current ordinary-chill behavior remains a compatibility control,not proof of a destruction chance. Primary legacy Blizzard descriptions corroborate categorical corpse loss but not3.3.93847chance. Existing source/component/contact and144retained wire inputs pass. No new cold chance,RNG field,persistence version,native rebuild,updated client read,deployment,named gate or class certificate. Ordinary-cold raw SHATTER/deadCol and all remaining full class/client/asset gaps remain open.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/corpse-shatter-guard')
    e.write('corpse-shatter-guard-native-evidence.json',result,files,check);print('Corpse shatter guard:489results/4original corpses/both native gates/real Lich controls/2reloads;baseline bug reproduced.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
