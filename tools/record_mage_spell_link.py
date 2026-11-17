#!/usr/bin/env python3
"""Bind the old scalar Mage fault and the tested shared typed-impact repair."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/mage-spell-link';BASE='4ce4f5a916d556a4e39a0e94233fca53b3a629b3'
sys.path.insert(0,str(ROOT/'tests'))
from check_mage_spell_link import configuration

def expected_markers():
    r=Counter({'iron-golem-birth-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks','defiler-health-link','hellslayer-readiness','teeth','bone-spear','bone-spirit','mage-basics','mage-missile','mage-glacial'):
        r[f+'-check']=1;r[f+'-reload-check']=2
    r['defiler-health-link-check-result']=1;r['defiler-health-link-reload-check-result']=2
    for f in ('teeth','bone-spear','bone-spirit'):
        for mode in ('right','quick'):r[f+'-'+mode+'-check']=3;r[f+'-'+mode+'-result']=3
    return r

def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/necro-spell-link/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/NecromancerMageMissiles.cs','tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs'],changed
    data='Data/Sanctuary/Balance/class-skill-status.json';assert [p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d]==[data]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    old=json.loads(subprocess.check_output(['git','show',BASE+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));new=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert [b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b]==['necromancer.skeletalmage']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
    assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'} and len(new['skills'])==240
    prior=subprocess.check_output(['git','show',BASE+':src/Sanctuary.Content/NecromancerMageMissiles.cs'],cwd=ROOT,text=True)
    assert (ROOT/'src/Sanctuary.Content/NecromancerMageMissiles.cs').read_text()==prior.replace('int loss=SkillHealth.Damage(actor,target,NamedElements.To256(damage.Total));','int loss=SkillHealth.TypedDamage(actor,target,NamedElements.To256(damage.Total),DirectDamageParts.From(damage));',1)
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for kind,build in (('Content',1),('Integration',2)):
        assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        assert all(t in (STAGE/(kind.lower()+f'-build-{build}.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected_markers(),72);assert 'pass across3boots/2reloads'in (STAGE/'native-run-1.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==21 and 'denied by connection filter'not in log
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete'in l];assert len(warnings)==3 and all('WardrobeSet'in l for l in warnings)
    negatives=[l for l in log.splitlines()if ' WRN]'in l and 'Defiler link sharing refused unsupported context'in l]
    assert len(negatives)==3 and all('Direct damage lacks settled typed components'in l for l in negatives)
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as a:
        for kind in ('Content','Integration'):assert hashlib.sha256(a.extractfile(kind.lower()+f'/Sanctuary.{kind}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    with tarfile.open(STAGE/'failed-baseline-1.tar.gz','r:gz')as a:
        base=a.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
        assert 'Mage 1/basic/critical=False typed link:fault=Direct damage lacks settled typed components;primary=6507/6507;secondary=0/3448;events=1;typed=False'in base
    baseline=e.load(STAGE/'failed-baseline-1-native-inputs.json')
    assert baseline['Assemblies/Sanctuary.Content.dll']==parent['artifacts']['Assemblies/Sanctuary.Content.dll'] and baseline['Assemblies/Sanctuary.Content.dll']!=born['artifacts']['Assemblies/Sanctuary.Content.dll']
    for name in ('defiler-health-link','teeth','bone-spear','bone-spirit','mage-basics','mage-missile','mage-glacial'):assert e.load(STAGE/(name+'-fixtures.json'))['Generation']==2,name
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    baseline=json.loads(subprocess.check_output(['git','show',BASE+':tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audit['Definitions']==baseline['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    assert e.load(ROOT/'tools/reference/defiler-health-link-rule-approval.json')['approved']
    files=[ROOT/'tools/record_mage_spell_link.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'docs/mage-spell-link-checkpoint.md',ROOT/'docs/class-skill-roadmap.md',ROOT/'docs/necromancer-coverage-matrix.md',ROOT/'docs/named-item-readiness.md',ROOT/'tools/reference/named-item-runtime-audit.json',*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*(STAGE/p for p in ('accepted-assemblies-1.tar.gz','failed-baseline-1.tar.gz'))]
    result=dict(component='Skeletal Mage current/historical elemental impacts publish settled components to approved Defiler Health Link',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=72,markers=dict(sorted(expected_markers().items())),mageLinkScenariosPerBoot=14,retainedMagicSpellLinkScenariosPerBoot=9,affectedTalents=['necromancer.skeletalmage'],retainedWireInputs=18,newWireInputs=0,connections=21,boots=3,saves=3,reloads=2,artifacts=born['artifacts'],changedSources=changed,changedRuntimeData=[data],mechanicalRuntimeDataChanged=False,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=1,failedCorrectedNativeAttempts=0,negativeBaseline='Old accepted content DLL:Ice Bolt primary6507/6507FP256,secondary0/3448,one untyped event;link faults. Frozen before repair.',fixtureCompileFailures=0,driverPreflightFailures=0,knownWardrobeSaveWarnings=3,intentionalUntypedRefusalWarnings=3,nativeRebuilt=False,persistenceVersionChanged=False,newSavedFields=[],installed=False,namedAdmissionChanged=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='One gameplay line publishes existing settled elemental channels through the approved typed health API. Current Mage basics,Glacial and historical elemental packets gain sharing;periodic poison stays excluded. Fourteen summoned-Mage contact cases per boot verify primary/secondary/actor/critical RNG/mana with normal and resistant critical contexts. Existing basic/Mastery/triples,legacy poison,Glacial area/freeze/original actors/flights/save scopes retain;the new matrix invokes contact directly and makes no new movement/area claim. Existing three learned Necromancer magic spell/link/input scopes retain. Other scalar direct producers,exact modern global callbacks/PvP/full items/current class/client/assets remain separately unfinished. No new design approval required.',archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/mage-spell-link')
    e.write('mage-spell-link-native-evidence.json',result,files,check)
    print('Mage typed link:72results/14Mage scenarios per boot/18retained inputs/2reloads;old-DLL fault reproduced,shared API repaired.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
