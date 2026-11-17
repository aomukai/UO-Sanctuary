#!/usr/bin/env python3
"""Bind approved ordinary-cold corpse activation and actual Mage kill checks."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/cold-corpse-rule-adoption';BASE='2efa73a22d62554b77c6dd94c4e60aa536ba8b06'
sys.path.insert(0,str(ROOT/'tests'))
from check_cold_corpse_adoption import configuration

def expected():
    m=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'corpse-shatter-guard-result':3})
    for family in configuration()['extra_families']:m[family+'-check']+=1;m[family+'-reload-check']+=2
    assert sum(m.values())==27
    return m

def prior(path):return subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT)

def record(check=False):
    assert 'pass across3boots/2reloads'in (STAGE/'native-run-1.log').read_text()
    born=e.load(STAGE/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/cold-corpse-proposal/birth-inputs.json');snapshot(STAGE/'source-snapshot-1.json',STAGE/'birth-inputs.json')
    for p,d in born['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    changed=sorted(p for p,d in born['sourceInputs'].items()if p.endswith('.cs')and parent['sourceInputs'].get(p)!=d)
    removed=sorted(p for p in parent['sourceInputs']if p.endswith('.cs')and p not in born['sourceInputs'])
    assert changed==['src/Sanctuary.Content/ColdCorpseRules.cs','src/Sanctuary.Content/CorpseSkillRules.cs','tests/Sanctuary.Integration/ColdCorpseProposalChecks.cs','tests/Sanctuary.Integration/CorpseShatterGuardChecks.cs'],changed
    assert removed==['src/Sanctuary.Content/ColdCorpseProposal.cs']and not(ROOT/removed[0]).exists()
    code=(ROOT/changed[0]).read_text();assert 'int roll=supplied?.Roll()??Utility.Random(100);'in code and 'Candidates'not in code and 'Enroll'not in code
    assert not any('ColdCorpseRules.SetRoll('in p.read_text()for p in (ROOT/'src/Sanctuary.Content').glob('*.cs'))
    data='Data/Sanctuary/Balance/class-skill-status.json';assert sorted(p for p,d in born['copiedData'].items()if parent['copiedData'].get(p)!=d)==[data]
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for kind in ('Content','Integration'):
        assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        assert all(s in (STAGE/(kind.lower()+'-build-1.log')).read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    with tarfile.open(STAGE/'accepted-assemblies-1.tar.gz','r:gz')as t:
        for kind in ('Content','Integration'):assert hashlib.sha256(t.extractfile(kind.lower()+f'/Sanctuary.{kind}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    log=e.cohort(STAGE,expected(),27);assert 'Warning: Attempted to delete'not in log and 'Client: 127.0.0.1: Connected.'not in log
    saved=e.load(STAGE/'cold-corpse-proposal-fixtures.json');assert saved['Generation']==2 and [b['Record']['Shattered']for b in saved['Bodies']]==[True,False]and all(b['Items']for b in saved['Bodies'])
    assert e.load(STAGE/'corpse-shatter-guard-fixtures.json')['Generation']==2
    status='data/Sanctuary/Balance/class-skill-status.json';old=json.loads(prior(status));new=e.load(ROOT/status)
    assert sorted(b['talentId']for a,b in zip(old['skills'],new['skills'])if a!=b)==['amazon.coldarrow','necromancer.skeletalmage']
    assert all({k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}for a,b in zip(old['skills'],new['skills']))
    assert {k:v for k,v in old.items()if k!='skills'}=={k:v for k,v in new.items()if k!='skills'}and len(new['skills'])==240 and not new['completedClasses']
    gap='tools/reference/cold-corpse-reference-gap.json';assert (ROOT/gap).read_bytes()==prior(gap)
    approval=e.load(ROOT/'tools/reference/ordinary-cold-corpse-approval.json');assert approval['approved']and not approval['implementationVerifiedAtDecision']and not approval['exactD2RChanceVerified']
    assert approval['proposalSha256AtDecision']==hashlib.sha256(prior(approval['proposal'])).hexdigest()
    audit=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert audit==e.load(STAGE/'hellslayer-readiness-fixtures.json')and audit['Definitions']==json.loads(prior('tools/reference/named-item-runtime-audit.json'))['Definitions']
    from build_named_readiness import validate
    validate(audit,assembly=STAGE/'content/Sanctuary.Content.dll')
    paths=['tools/record_cold_corpse_adoption.py','tools/build_skill_roadmap.py','docs/ordinary-cold-corpse-adoption-checkpoint.md','docs/ordinary-cold-corpse-proposal.md','docs/class-skill-roadmap.md','docs/necromancer-coverage-matrix.md','docs/skill-effects.md','docs/skill-icons.md','docs/named-item-readiness.md','tools/reference/named-item-runtime-audit.json','tools/reference/ordinary-cold-corpse-approval.json',status,gap]
    files=[*(ROOT/p for p in paths),*configuration()['extra_inputs'],*e.stage_files(STAGE),*(ROOT/p for p in changed),STAGE/'accepted-assemblies-1.tar.gz']
    result=dict(component='Approved ordinary-chill once-only corpse shatter rule enabled',referenceVersion='3.3.93847',baselineCommit=BASE,approvalAdopted=True,ordinaryGameplayEnabled=True,candidateEnrollmentRequired=False,contentBuild=1,integrationBuild=1,sourceSnapshot=1,nativeRun=1,nativeMarkers=27,markers=dict(sorted(expected().items())),allRollValuesPerBoot=100,shatteredRollValuesPerBoot=20,actualMageIceBoltKills=6,boots=3,saves=3,reloads=2,socketConnections=0,newAbilityInputs=0,originalSavedCorpses=2,saveDeletionWarnings=0,correctedNativeFailures=0,changedSources=changed,removedSources=removed,artifacts=born['artifacts'],changedRuntimeData=[data],runtimeNumericalDataChanged=False,nativeRebuilt=False,newPersistenceProfiles=False,newSavedFields=False,newTimers=False,installed=False,classCertified=False,goalComplete=False,exactD2RChanceVerified=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/cold-corpse-rule-adoption',limits='Approved20percent once-only death-time roll uses active shared ordinary chill regardless of killing damage. Existing freeze/HolyFreeze precedence and absent/expired/removed chill omit new draws. Existing corpse flag,loot,consumer guards and historical decisions retain. Actual summoned Mage Ice Bolt flights cause six controlled kills with shatter/nonshatter outcomes,single roll,availability,loot and unchanged owner mana;fixture updates do not establish natural timing or client input. All100roll values repeat on eachboot;two original corpse decisions and owner wounds/resources persist through two reloads. Default native RNG and retained Mage/basic/missile/native-animation/wild-Lich/reservation/unknown-metadata scopes pass. Candidate21result receipt/failure remain historical;only ColdArrow/Mage annotations refresh. Exact current engine chance/global RNG/geometry/event/PvP and other producer input/class/client scopes remain bounded;final shatter visuals/audio and existing icons separate. No deployment,class certificate or charm/runeword support.')
    e.write('ordinary-cold-corpse-adoption-native-evidence.json',result,files,check);print('Approved ordinary cold:27results,6actual Mage cold kills,2reloads;loot and installed saves preserved.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
