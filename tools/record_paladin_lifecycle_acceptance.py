#!/usr/bin/env python3
"""Bind current Paladin input, aura recipients and original saved lifecycles."""
import argparse,hashlib,json,re,subprocess,sys,tarfile
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT
STAGE=ROOT/'.local/class-skills/paladin-lifecycle-acceptance'
FOCUS=ROOT/'.local/class-skills/paladin-aura-input'
BASE='e280b036082b98ba242be726a2d61f86fa78e0c3'
sys.path.insert(0,str(ROOT/'tests'))
from check_paladin_lifecycle_acceptance import configuration
from check_paladin_aura_input import INPUTS

def expected():
    m=Counter(e.load(ROOT/'tools/reference/paladin-planned-acceptance-native-evidence.json')['markers'])
    assert sum(m.values())==601
    for label in INPUTS:m[label+'-check']+=3;m[label+'-result']+=3
    m['fanaticism-mana-check']+=1;m['fanaticism-mana-reload-check']+=2;m['fanaticism-mana-reload-check-result']+=2
    assert sum(m.values())==750
    return m

def archived_assemblies(stage,artifacts):
    with tarfile.open(stage/'accepted-assemblies-1.tar.gz','r:gz')as t:
        for kind in ('Content','Integration'):
            assert hashlib.sha256(t.extractfile(kind.lower()+f'/Sanctuary.{kind}.dll').read()).hexdigest()==artifacts[f'Assemblies/Sanctuary.{kind}.dll']

def record(check=False):
    assert 'pass across3boots/2reloads'in (STAGE/'native-run-1.log').read_text()
    assert 'pass across3boots/2reloads'in (FOCUS/'native-run-5.log').read_text()
    born=e.load(STAGE/'birth-inputs.json');focused=e.load(FOCUS/'birth-inputs.json')
    parent=e.load(ROOT/'.local/class-skills/cold-corpse-rule-adoption/birth-inputs.json')
    for stage,snap in ((STAGE,'source-snapshot-1.json'),(FOCUS,'source-snapshot-6.json')):
        snapshot(stage/snap,stage/'birth-inputs.json')
        for p,d in e.load(stage/'birth-inputs.json')['sourceInputs'].items():assert e.sha(ROOT/p)==d,p
        archived_assemblies(stage,born['artifacts'])
    assert born['artifacts']==focused['artifacts'] and born['copiedData']==focused['copiedData']==parent['copiedData']
    assert {p:d for p,d in born['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}=={p:d for p,d in parent['artifacts'].items()if p!='Assemblies/Sanctuary.Integration.dll'}
    cs={p:d for p,d in born['sourceInputs'].items()if p.endswith('.cs')}
    new='tests/Sanctuary.Integration/PaladinRemainingAuraInputChecks.cs'
    assert new in cs and {p:d for p,d in cs.items()if p!=new}=={p:d for p,d in parent['sourceInputs'].items()if p.endswith('.cs')}
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]
    for kind in ('Content','Integration'):assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    build=(FOCUS/'integration-build-6.log').read_text();assert all(s in build for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    log=e.cohort(STAGE,expected(),750)
    assert log.count('Client: 127.0.0.1: Connected.')==281
    f=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for mode in ('right','quick'):f['remaining-aura-holyfire-'+mode+'-check']+=3;f['remaining-aura-holyfire-'+mode+'-result']+=3
    focused_log=e.cohort(FOCUS,f,21);assert focused_log.count('Client: 127.0.0.1: Connected.')==6
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete'in l]
    warning_types=Counter(re.search(r'"([^"]+)" during world save',l).group(1)for l in warnings)
    assert warning_types==Counter(WardrobeSet=4,WoodenKiteShield=1)
    old_log=(ROOT/'.local/class-skills/paladin-planned-acceptance/birth-native-checks.log').read_text()
    assert warning_types==Counter(re.findall(r'Warning: Attempted to delete \S+ "([^"]+)" during world save',old_log))
    unsupported=[l for l in log.splitlines()if ' WRN]'in l and 'unsupported'in l.lower()];assert not unsupported
    audit=e.load(ROOT/'tools/reference/paladin-current-input-coverage.json')
    assert (audit['verifiedActiveInputRoutes'],audit['activeTalents'],audit['passiveTalents'],audit['ordinaryInputCases'],audit['foreignInputCasesSeparatelyExcluded'])==(30,30,0,258,9)
    assert audit['artifacts']==born['artifacts']
    for p,d in audit['files'].items():assert e.sha(ROOT/p)==d,p
    c=configuration();assert tuple(len(c[k])for k in ('extra_families','extra_checks','extra_target_input_commands','extra_files'))==(32,15,89,54)
    status='data/Sanctuary/Balance/class-skill-status.json'
    assert (ROOT/status).read_bytes()==subprocess.check_output(['git','show',BASE+':'+status],cwd=ROOT)
    ledger=e.load(ROOT/status);rows=[r for r in ledger['skills']if r['characterClass']=='Paladin']
    assert len(rows)==30 and all(r['implementation']=='handler-present'for r in rows)and len(ledger['skills'])==240 and not ledger['completedClasses']
    archives=[FOCUS/(n+'.tar.gz')for n in ('failed-native-1','failed-native-2','failed-native-3','failed-native-4','failed-build-4','diagnostic-accepted-5')]
    for p in archives:assert p.is_file(),p
    paths=['tools/record_paladin_lifecycle_acceptance.py','tools/class_input_coverage.py','tools/review_paladin_input_coverage.py','tools/reference/paladin-current-input-coverage.json','docs/paladin-lifecycle-acceptance-checkpoint.md','docs/paladin-coverage-matrix.md','docs/class-skill-roadmap.md','docs/skill-effects.md','docs/skill-icons.md',status]
    files=[*(ROOT/p for p in paths),*c['extra_inputs'],*e.stage_files(STAGE,FOCUS),*archives,STAGE/'accepted-assemblies-1.tar.gz',FOCUS/'accepted-assemblies-1.tar.gz']
    result=dict(component='Current combined Paladin lifecycle and actual input acceptance',referenceVersion='3.3.93847',baselineCommit=BASE,
        nativeMarkers=750,markers=dict(sorted(expected().items())),retainedPredecessorMarkers=601,focusedDiagnosticMarkers=21,
        fixtureFamilies=32,sharedChecks=15,declaredInputCommands=89,totalWireCases=267,paladinInputCases=258,foreignTeleportInputs=9,
        verifiedActiveTalents=30,passiveTalents=0,connections=281,boots=3,saves=3,reloads=2,
        contentReused='cold-corpse-rule-adoption Content1',integrationBuild=6,sourceSnapshot=1,nativeRun=1,artifacts=born['artifacts'],
        gameplayChangedDuringRequalification=False,runtimeDataChangedDuringRequalification=False,contentRebuilds=0,nativeRebuilds=0,
        integrationBuildAttempts=6,correctedIntegrationCompileFailures=1,correctedFocusedNativeFailures=4,combinedNativeFailures=0,
        knownSaveDeletionWarnings=len(warnings),saveDeletionWarningTypes=dict(warning_types),unsupportedWarnings=0,installed=False,classCertified=False,goalComplete=False,
        protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),
        archiveRoot='/home/aomukai/Sanctuary-checkpoint-archives/revive-respec/paladin-lifecycle-acceptance',
        limits='All601predecessor results retain;24new real right/quick aura routes add144results and adopted Fanaticism adds5. Actual258Paladin inputs cover30active talents;9foreignTeleport inputs do not count. Natural zero-mana effects include delayed Thorns,elemental damage/self-only weapon stats,HolyFreeze slow,Sanctuary undead damage,Conviction hostile penalties and Redemption one-use corpse/loot recovery. Four focused fixture failures and one compile failure were frozen before fixes;monster recovery was explicitly blocked rather than changing gameplay. Existing aura/projectile/resources/gear/bindings/pending casts retain their declared save checks. No whole-class,exact binary/client/PvP/global RNG or unsupported recipient/world certification. Missing effects/audio and icons remain separate;installed build and saves protected.')
    e.write('paladin-lifecycle-acceptance-native-evidence.json',result,files,check)
    print('Paladin:750exactresults/258class+9foreign inputs/30active routes/2reloads;production DLL/data unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
