#!/usr/bin/env python3
"""Bind the current Barbarian cohort to inputs, original saves and exact DLLs."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/barbarian-lifecycle-acceptance';FOCUS=ROOT/'.local/class-skills/barbarian-remaining-input'
BASE='3a27987a0144a269be313db933e5c6825de65ffa'
sys.path.insert(0,str(ROOT/'tests'))
from check_barbarian_lifecycle_acceptance import configuration
from check_barbarian_remaining_input import INPUTS
def expected():
    m=Counter(e.load(ROOT/'tools/reference/barbarian-planned-acceptance-native-evidence.json')['markers']);assert sum(m.values())==245
    for label in INPUTS:m[label+'-check']+=3;m[label+'-result']+=3
    for family in ('barbarian-contact','warcry-potion','defiler-health-link','hellslayer-readiness'):
        m[family+'-check']+=1;m[family+'-reload-check']+=2
    m['defiler-health-link-check-result']+=1;m['defiler-health-link-reload-check-result']+=2;m['berserk-snapshot-result']+=3
    assert sum(m.values())==473
    return m
def archived(stage,artifacts):
    with tarfile.open(stage/'accepted-assemblies-1.tar.gz','r:gz')as t:
        for kind in ('Content','Integration'):
            assert hashlib.sha256(t.extractfile(kind.lower()+f'/Sanctuary.{kind}.dll').read()).hexdigest()==artifacts[f'Assemblies/Sanctuary.{kind}.dll']
def record(check=False):
    born=e.load(STAGE/'birth-inputs.json');focus=e.load(FOCUS/'birth-inputs.json');parent=e.load(ROOT/'.local/class-skills/paladin-lifecycle-acceptance/birth-inputs.json')
    assert born['artifacts']==focus['artifacts']and born['copiedData']==focus['copiedData']==parent['copiedData']
    for stage in (STAGE,FOCUS):
        assert 'pass across3boots/2reloads'in(stage/'native-run-1.log').read_text();snapshot(stage/'source-snapshot-1.json',stage/'birth-inputs.json');archived(stage,born['artifacts'])
        for p,d in e.load(stage/'birth-inputs.json')['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    assert {p:d for p,d in born['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}=={p:d for p,d in parent['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}
    cs={p:d for p,d in born['sourceInputs'].items()if p.endswith('.cs')};old={p:d for p,d in parent['sourceInputs'].items()if p.endswith('.cs')}
    new='tests/Sanctuary.Integration/BarbarianRemainingInputChecks.cs';clock='tests/Sanctuary.Integration/BarbarianCastClockChecks.cs'
    assert {p for p,d in cs.items()if old.get(p)!=d}=={new,clock}
    prior=subprocess.check_output(['git','show',BASE+':'+clock],cwd=ROOT).decode()
    assert (ROOT/clock).read_text()==prior.replace('private static PlayerMobile Owner()','internal static PlayerMobile Owner()').replace('private static void Clean(PlayerMobile p,BaseCreature enemy=null)','internal static void Clean(PlayerMobile p,BaseCreature enemy=null)')
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]
    for kind in ('Content','Integration'):assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    build=(FOCUS/'integration-build-1.log').read_text();assert all(s in build for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected(),473);assert log.count('Client: 127.0.0.1: Connected.')==168
    f=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for label in INPUTS:f[label+'-check']+=3;f[label+'-result']+=3
    focused=e.cohort(FOCUS,f,219);assert focused.count('Client: 127.0.0.1: Connected.')==105
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete'in l]
    unsupported=[l for l in log.splitlines()if ' WRN]'in l and 'unsupported'in l.lower()]
    assert len(unsupported)==12 and all('Defiler link sharing refused unsupported context'in l for l in unsupported)
    audit=e.load(ROOT/'tools/reference/barbarian-current-input-coverage.json')
    assert (audit['verifiedActiveInputRoutes'],audit['activeTalents'],audit['passiveTalents'],audit['ordinaryInputCases'],audit['foreignInputCasesSeparatelyExcluded'])==(20,20,10,144,0)
    assert audit['artifacts']==born['artifacts']
    for p,d in audit['files'].items():assert e.sha(ROOT/p)==d,p
    for name in ('warcry-potion','defiler-health-link'):assert e.load(STAGE/(name+'-fixtures.json'))['Generation']==2
    named=e.load(ROOT/'tools/reference/named-item-runtime-audit.json');assert named==e.load(STAGE/'hellslayer-readiness-fixtures.json')
    from build_named_readiness import validate
    validate(named,assembly=STAGE/'content/Sanctuary.Content.dll')
    c=configuration();assert tuple(len(c[k])for k in ('extra_families','extra_checks','extra_target_input_commands','extra_files'))==(29,9,48,21)
    status='data/Sanctuary/Balance/class-skill-status.json';assert(ROOT/status).read_bytes()==subprocess.check_output(['git','show',BASE+':'+status],cwd=ROOT)
    ledger=e.load(ROOT/status);assert len(ledger['skills'])==240 and not ledger['completedClasses']and len([r for r in ledger['skills']if r['characterClass']=='Barbarian'and r['implementation']=='handler-present'])==30
    paths=['tools/record_barbarian_lifecycle_acceptance.py','tools/class_input_coverage.py','tools/review_barbarian_input_coverage.py','tools/reference/barbarian-current-input-coverage.json','docs/barbarian-lifecycle-acceptance-checkpoint.md','docs/barbarian-coverage-matrix.md','docs/class-skill-roadmap.md','docs/skill-effects.md','docs/skill-icons.md',status]
    files=[*(ROOT/p for p in paths),*c['extra_inputs'],*e.stage_files(STAGE,FOCUS),STAGE/'accepted-assemblies-1.tar.gz',FOCUS/'accepted-assemblies-1.tar.gz']
    result=dict(component='Current Barbarian lifecycle, contact and actual input acceptance',referenceVersion='3.3.93847',baselineCommit=BASE,nativeMarkers=473,markers=dict(sorted(expected().items())),retainedPredecessorMarkers=245,focusedMarkers=219,focusedInputs=105,fixtureFamilies=29,sharedChecks=9,inputCommands=48,ordinaryInputCases=144,verifiedActiveTalents=20,passiveTalents=10,connections=168,boots=3,saves=3,reloads=2,contentReused='cold-corpse-rule-adoption Content1',integrationBuild=1,sourceSnapshot=1,nativeRun=1,artifacts=born['artifacts'],gameplayChanged=False,runtimeDataChanged=False,contentRebuilds=0,nativeRebuilds=0,integrationBuilds=1,correctedNativeFailures=0,correctedCompileFailures=0,saveDeletionWarnings=warnings,intentionalScalarRefusals=12,installed=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/barbarian-lifecycle-acceptance',limits='Retains245earlier results;35new wire controls add210results across14previously missing active talents. Current contact/proc/poison/snapshot,WarCry potion,HealthLink and named-readiness families add18results. All20active routes have actual positive execution evidence;10passives retain separate item/stat/birth consumers. FindItem inputs deliberately test a failed roll that still pays/reserves the original corpse,not successful treasure or resolved current chance. Natural movement/attacks/throws run without forced resolution. Approved startup/context/landing/potion rules retain;modern FindItem chance/special treasure,Bash total,exceptional hand/landing/world/recipient/PvP/client/RNG/event scopes remain explicit. No gameplay/data/client/install/save reset or class certification;effects/audio/icons separate.')
    e.write('barbarian-lifecycle-acceptance-native-evidence.json',result,files,check)
    print('Barbarian:473exactresults/144inputs/20active+10passive scopes/2reloads;production unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
