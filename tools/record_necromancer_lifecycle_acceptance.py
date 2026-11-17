#!/usr/bin/env python3
"""Bind current combined Necromancer mechanics and all active input routes."""
import argparse,hashlib,json,subprocess,sys,tarfile
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/necromancer-lifecycle-acceptance';INPUT=ROOT/'.local/class-skills/necromancer-terror-input';BASE='c42e9769dd97948464c867bee00cb16fac6f4105'
sys.path.insert(0,str(ROOT/'tests'))
from check_necromancer_lifecycle_acceptance import configuration
from check_necromancer_terror_input import configuration as input_configuration

def expected():
    m=Counter(e.load(ROOT/'tools/reference/necromancer-requalification-native-evidence.json')['markers']);assert sum(m.values())==938
    for family in ('mage-projectile-lifetime','cold-corpse-proposal','corpse-shatter-guard'):m[family+'-check']+=1;m[family+'-reload-check']+=2
    m['mage-projectile-lifetime-check-result']+=1;m['mage-projectile-lifetime-reload-check-result']+=2;m['corpse-shatter-guard-result']+=3
    assert sum(m.values())==953
    return m
def input_expected():
    m=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for f in input_configuration()['extra_families']:m[f+'-check']+=1;m[f+'-reload-check']+=2
    for n in input_configuration()['extra_target_input_commands']:m[n+'-check']+=3;m[n+'-result']+=3
    assert sum(m.values())==33
    return m
def prior(path):return subprocess.check_output(['git','show',BASE+':'+path],cwd=ROOT)
def record(check=False):
    parent=e.load(ROOT/'.local/class-skills/cold-corpse-rule-adoption/birth-inputs.json');born=e.load(STAGE/'birth-inputs.json');other=e.load(INPUT/'birth-inputs.json')
    assert born['artifacts']==other['artifacts']==parent['artifacts']and born['copiedData']==other['copiedData']==parent['copiedData']
    for stage in (STAGE,INPUT):
        snapshot(stage/'source-snapshot-1.json',stage/'birth-inputs.json');inv=e.load(stage/'birth-inputs.json')
        assert 'pass across3boots/2reloads'in (stage/'native-run-1.log').read_text()
        assert {p:d for p,d in inv['sourceInputs'].items()if p.endswith('.cs')}=={p:d for p,d in parent['sourceInputs'].items()if p.endswith('.cs')}
        for p,d in inv['sourceInputs'].items():
            if p=='tools/review_necromancer_input_coverage.py' and e.sha(ROOT/p)!=d:
                # This post-run observer was hashed as an ancillary input but
                # never executed by the native driver. Preserve its exact old
                # bytes and permit only the corrected enum-file lookup.
                assert stage==INPUT
                with tarfile.open(INPUT/'metadata-audit-attempt-1.tar.gz','r:gz')as t:old=t.extractfile(p).read()
                assert hashlib.sha256(old).hexdigest()==d
                assert (ROOT/p).read_bytes()==old.replace(b'shared/ClassAbilities.cs',b'shared/DiabloClass.cs')
            else:assert e.sha(ROOT/p)==d,p
        with tarfile.open(stage/'accepted-assemblies-1.tar.gz','r:gz')as t:
            for k in ('Content','Integration'):assert hashlib.sha256(t.extractfile(k.lower()+f'/Sanctuary.{k}.dll').read()).hexdigest()==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
    log=e.cohort(STAGE,expected(),953);small=e.cohort(INPUT,input_expected(),33)
    assert log.count('Client: 127.0.0.1: Connected.')==284 and small.count('Client: 127.0.0.1: Connected.')==9
    warnings=[l for l in log.splitlines()if 'Warning: Attempted to delete'in l];assert all('"WardrobeSet"'in l for l in warnings)and 'Warning: Attempted to delete'not in small
    negatives=[l for l in log.splitlines()if 'Defiler link sharing refused unsupported context'in l];assert len(negatives)==12
    for name in ('mage-projectile-lifetime','cold-corpse-proposal','corpse-shatter-guard','defiler-health-link'):assert e.load(STAGE/(name+'-fixtures.json'))['Generation']==2
    for stage in (STAGE,INPUT):assert e.load(stage/'hellslayer-readiness-fixtures.json')==e.load(ROOT/'tools/reference/named-item-runtime-audit.json')
    from build_named_readiness import validate
    validate(e.load(ROOT/'tools/reference/named-item-runtime-audit.json'),assembly=STAGE/'content/Sanctuary.Content.dll')
    audit=e.load(ROOT/'tools/reference/necromancer-current-input-coverage.json');assert audit['verifiedActiveInputRoutes']==27 and audit['activeTalents']==27 and audit['passiveTalents']==3
    assert audit['successfulNecromancerInputs']==168 and audit['foreignInputsSeparatelyExcluded']==3 and audit['artifacts']==born['artifacts']
    status='data/Sanctuary/Balance/class-skill-status.json';assert (ROOT/status).read_bytes()==prior(status);ledger=e.load(ROOT/status);assert len(ledger['skills'])==240 and not ledger['completedClasses']
    for p,d in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==d,p
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]
    for k in ('Content','Integration'):assert e.sha(STAGE/k.lower()/f'Sanctuary.{k}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
    paths=['tools/record_necromancer_lifecycle_acceptance.py','tools/review_necromancer_input_coverage.py','tools/reference/necromancer-current-input-coverage.json','docs/necromancer-lifecycle-acceptance-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-coverage-matrix.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json',status]
    files=[*(ROOT/p for p in paths),*configuration()['extra_inputs'],*input_configuration()['extra_inputs'],*e.stage_files(STAGE,INPUT),STAGE/'accepted-assemblies-1.tar.gz',INPUT/'accepted-assemblies-1.tar.gz',INPUT/'metadata-audit-attempt-1.tar.gz',INPUT/'metadata-recorder-attempt-1.tar.gz']
    result=dict(component='Current Necromancer combined lifecycle and complete active-input accounting',referenceVersion='3.3.93847',baselineCommit=BASE,contentReused='cold-corpse-rule-adoption Content1',integrationReused='cold-corpse-rule-adoption Integration1',nativeRun=1,combinedMarkers=953,markers=dict(sorted(expected().items())),retainedPredecessorMarkers=938,fixtureFamilies=125,sharedChecks=23,declaredLiveActions=111,combinedAbilityInputs=162,combinedConnections=284,focusedInputMarkers=33,focusedInputMarkerCounts=dict(sorted(input_expected().items())),additionalAbilityInputs=9,additionalConnections=9,verifiedActiveTalents=27,passiveTalentsWithSeparateBirthRecipientChecks=3,successfulNecromancerInputs=168,retainedForeignInputs=3,bootsPerCohort=3,savesPerCohort=3,reloadsPerCohort=2,artifacts=born['artifacts'],gameplayChanged=False,runtimeDataChanged=False,rebuilds=0,correctedNativeFailures=0,knownWardrobeSaveWarnings=len(warnings),intentionalScalarRefusals=12,installed=False,classCertified=False,goalComplete=False,protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),archiveRoots=[str(Path('/home/aomukai/Sanctuary-checkpoint-archives/revive-respec')/s.name)for s in (STAGE,INPUT)],limits='All938predecessor markers retain;15new Mage lifetime/corpse markers cover approved changes under the exact current binaries/data. Original dead emitter/shot,cold corpse decisions/loot,Defiler links and other existing actors/items/resources retain across two reloads. Focused existing Terror right/quick and Confuse quick fixtures add9wire actions with natural escape/combat/expiry and no rebuild. Actual ready class/ability IDs and successful result markers cover all27active talents;three passives use dedicated birth/recipient evidence,not invented casts. Current-engine geometry/global RNG/event/NHD/PvP/special recipients,unclassified native world conversion and historical unheld/old-binary limits remain individually flagged. Assets/audio/icons remain separate placeholders;no full class/parity certificate or deployment.')
    result['postRunMetadataAuditCorrection']=dict(distinctObserverFailures=1,archive='metadata-audit-attempt-1.tar.gz',scope='Only enum lookup shared/ClassAbilities.cs -> shared/DiabloClass.cs;the observer was an ancillary hashed file,not executed by the native driver. Exact old/new bytes validated;all native binaries,data,drivers and C# remain unchanged.',distinctReceiptWriterFailures=1,receiptWriterArchive='metadata-recorder-attempt-1.tar.gz',receiptWriterCorrection='Missing pathlib.Path import;failed writer source/log frozen. Post-run metadata only;no native replay or rebuild.')
    e.write('necromancer-lifecycle-acceptance-native-evidence.json',result,files,check);print('Necromancer:953combined+33focused results,171total ability inputs,all27active routes;no rebuild,installed build/saves preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
