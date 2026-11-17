#!/usr/bin/env python3
"""Bind the gated ordinary-cold corpse candidate without claiming activation."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/cold-corpse-proposal';BASE='f9e562d58006c81a4d7a4e9d4db9c62a93fbb358'
sys.path.insert(0,str(ROOT/'tests'))
from check_cold_corpse_proposal import configuration

def expected():
    m=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'corpse-shatter-guard-result':3})
    for family in configuration()['extra_families']:m[family+'-check']+=1;m[family+'-reload-check']+=2
    assert sum(m.values())==21
    return m

def prior(path):return subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT,text=True)

def record(check=False):
    assert 'pass across3boots/2reloads'in (STAGE/'native-run-2.log').read_text()
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/mage-projectile-lifetime/birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    assert changed==['src/Sanctuary.Content/ColdCorpseProposal.cs','src/Sanctuary.Content/CorpseSkillRules.cs','tests/Sanctuary.Integration/ColdCorpseProposalChecks.cs'],changed
    assert not any('ColdCorpseProposal.Enroll('in p.read_text()for p in (ROOT/'src/Sanctuary.Content').glob('*.cs')),'Unexpected ordinary production enrollment'
    assert born['copiedData']==parent['copiedData']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for kind,number in (('Content',1),('Integration',2)):
        assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        assert all(s in (STAGE/(kind.lower()+f'-build-{number}.log')).read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
        for kind in ('Content','Integration'):assert hashlib.sha256(t.extractfile(kind.lower()+f'/Sanctuary.{kind}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    failed=e.load(STAGE/'failed-native-run-1-native-inputs.json');assert failed['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']and failed['Assemblies/Sanctuary.Integration.dll']!=born['artifacts']['Assemblies/Sanctuary.Integration.dll']
    with tarfile.open(STAGE/'failed-native-run-1.tar.gz','r:gz')as t:assert 'Shattered corpse public cast/payment/loot:RaiseSkeleton'in t.extractfile(str(STAGE.relative_to(ROOT)/'birth-native-checks.log')).read().decode()
    log=e.cohort(STAGE,expected(),21);assert 'Warning: Attempted to delete'not in log and 'Client: 127.0.0.1: Connected.'not in log
    saved=e.load(STAGE/'cold-corpse-proposal-fixtures.json');assert saved['Generation']==2 and [b['Record']['Shattered']for b in saved['Bodies']]==[True,False]
    assert len(saved['Bodies'])==2 and all(b['Items']for b in saved['Bodies'])
    assert e.load(STAGE/'corpse-shatter-guard-fixtures.json')['Generation']==2
    status='data/Sanctuary/Balance/class-skill-status.json';assert (ROOT/status).read_text()==prior(status);ledger=e.load(ROOT/status);assert len(ledger['skills'])==240 and not ledger['completedClasses']
    gap='tools/reference/cold-corpse-reference-gap.json';assert (ROOT/gap).read_text()==prior(gap)and not e.load(ROOT/gap)['handlerEnabled']
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    paths=['tools/record_cold_corpse_proposal.py','docs/ordinary-cold-corpse-proposal.md','docs/ordinary-cold-corpse-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-coverage-matrix.md','docs/skill-effects.md','docs/skill-icons.md','docs/named-item-readiness.md','tools/reference/named-item-runtime-audit.json',status,gap]
    files=[*(ROOT/p for p in paths),*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),*STAGE.glob('*.tar.gz')]
    result=dict(component='Candidate once-only ordinary-chill corpse shatter rule',referenceVersion='3.3.93847',baselineCommit=BASE,approvalPending=True,ordinaryGameplayEnabled=False,productionEnrollmentCallers=0,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,nativeMarkers=21,markers=dict(sorted(expected().items())),allRollValuesPerBoot=100,shatteredRollValuesPerBoot=20,boots=3,saves=3,reloads=2,socketConnections=0,newAbilityInputs=0,originalSavedCorpses=2,saveDeletionWarnings=0,correctedFixtureFailures=1,gameplayBuildUnchangedAfterFailure=True,changedSources=changed,artifacts=born['artifacts'],runtimeDataChanged=False,nativeRebuilt=False,newPersistenceProfiles=False,newSavedFields=False,newTimers=False,installed=False,classCertified=False,goalComplete=False,exactD2RChanceVerified=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/cold-corpse-proposal',limits='Only individually enrolled candidates enter20percent roll. Existing shared chill/freeze/HolyFreeze and corpse flags/consumer guards reused;all100roll values eachboot,expired/removed/plain/default negatives,duplicate capture,no-extra-roll existing shatter,public corpse selection/payment/loot and paid nonshattered CE controls pass. Two original different corpse decisions/contents/owner wounds/resources persist twice. Existing native-animation/wild-Lich/reservation/unknown-metadata regressions retain. No socket/client claim;ordinary activation and actual Mage cold-kill qualification remain after user decision. No retroactive old-corpse changes,ordinary3.3.93847chance claim,final shatter art/audio or new icons;all240rows/class flags and installed build/saves retain.')
    e.write('cold-corpse-proposal-native-evidence.json',result,files,check);print('Cold corpse candidate:21results/all100roll values perboot/2reloads;ordinary gameplay remains unenrolled pending decision.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
