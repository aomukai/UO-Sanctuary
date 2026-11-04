#!/usr/bin/env python3
"""Bind learned Wake casting, approved execution identity and original fire saves."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/wake-learned'
TARGET=ROOT/'.local/class-skills/wake-learned-targeted'
PARENT='e9cec8ac2c9d060b11c3eb3f030e1774d5c93809'
sha=inventory.sha
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))
def expected(focused=False):
    sys.path.insert(0,str(ROOT/'tests'))
    from check_wake_fire_automatic import configuration
    c=configuration();families=(*c['extra_families'],'wake-learned');checks=(*c['extra_checks'],'wake-learned-mechanics-check')
    inputs=(*c['extra_target_input_commands'],'wake-learned-left','wake-learned-right','wake-learned-quick')
    waits={**c['extra_wait_results'],'wake-learned-check':'wake-learned-result','wake-learned-reload-check':'wake-learned-reload-result'}
    if focused:
        families=('wake-fire-prepared','wake-fire-actor','wake-fire-missile','wake-fire-automatic','wake-learned')
        checks=('wake-fire-automatic-live-check','wake-learned-mechanics-check');inputs=('wake-learned-left','wake-learned-right','wake-learned-quick')
        waits={k:v for k,v in waits.items()if k in(*checks,'wake-learned-check','wake-learned-reload-check')}
    result=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for f in families:result[f+'-check']=1;result[f+'-reload-check']=2
    for c in checks:result[c]=3
    for c in inputs:result[c+'-check']=3;result[c+'-result']=3
    for c,r in waits.items():result[r]+=3 if c in checks else 2 if c.endswith('-reload-check')else 1
    return result

def record(check=False):
    inventory.STAGE=STAGE;native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    inventory.STAGE=TARGET;focused=inventory.validate_inventory(TARGET/'birth-inputs.json');inventory.STAGE=STAGE
    assert native==focused and native['artifacts']==audit['artifacts']and native['copiedData']==audit['copiedData']
    for path in(STAGE/'birth-inputs.json',TARGET/'birth-inputs.json',STAGE/'audit-inputs.json'):snapshot(STAGE/'pre-build-3.json',path)
    current=load(STAGE/'pre-build-3.json')['inputs'];prior=load(ROOT/'.local/class-skills/creeper-balance/pre-build-2.json')['inputs']
    production={'src/Sanctuary.Content/'+n+'.cs'for n in('ActionTiming','AssassinCombat','AssassinSentryCasts','AssassinWakeFireMissiles','ClassCombat')}
    tests={'tests/Sanctuary.Integration/'+n+'.cs'for n in('SentryLearnedChecks','WakeFireActorChecks','WakeFireAutomaticChecks','WakeFireMissileChecks','WakeFirePreparedChecks','WakeLearnedChecks')}
    assert {p for p in set(current['sourceInputs'])|set(prior['sourceInputs'])if current['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}==production|tests
    assert current['projectInputs']==prior['projectInputs']
    assert {p for p in current['copiedData']if current['copiedData'][p]!=prior['copiedData'].get(p)}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    edited_fixtures={'tests/Sanctuary.Integration/WakeLearnedChecks.cs','tests/Sanctuary.Integration/SentryLearnedChecks.cs'}
    for n in(1,2):
        old=load(STAGE/f'pre-build-{n}.json')['inputs']
        assert {k:v for k,v in old['sourceInputs'].items()if k not in edited_fixtures}=={k:v for k,v in current['sourceInputs'].items()if k not in edited_fixtures}
        assert old['projectInputs']==current['projectInputs']and old['copiedData']==current['copiedData']
    parent=historical('tools/reference/creeper-balance-native-evidence.json')
    for n in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][n]==parent['artifacts'][n]==sha(STAGE/'native/Distribution'/n)
    before=historical('data/Sanctuary/Balance/class-skill-status.json');after=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert {k:v for k,v in before.items()if k!='skills'}=={k:v for k,v in after.items()if k!='skills'}
    assert {x['talentId']:[k for k in x if x[k]!=y[k]]for x,y in zip(before['skills'],after['skills'],strict=True)if x!=y}=={'assassin.wakeoffire':['implementation','remaining']}
    assert not after['completedClasses']and sum(r['implementation']=='handler-present'for r in after['skills'])==193
    assert sum(r['implementation']=='handler-present'and r['characterClass']=='Assassin'for r in after['skills'])==23
    for stage,count,small in((STAGE,225,False),(TARGET,54,True)):
        exp=expected(small);assert sum(exp.values())==count and markers(stage/'birth-native-checks.log')==exp
        log=(stage/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log and log.count('Writing world save snapshot done')==log.count('Shutting down')==3
        saved=load(stage/'wake-learned-fixtures.json');assert saved['Generation']==2 and saved['Mana']>0 and saved['Actor']['Owner']==saved['Owner']
        creation=saved['Actor']['Creation'];assert creation['Ability']==206 and creation['Rank']==21
        skills={r['Name']:r['Value']for r in creation['Skills']};assert skills['Fire Trauma']==skills['Inferno Sentry']==20
        control=saved['Actor']['WakeControl'];assert control['Dispatches']==control['Attempts']==1 and control['Ai']['RemainingShots']==4
        assert len(saved['Pool']['Flights'])==23 and not saved['Pool']['Leases']and len({f['Execution']for f in saved['Pool']['Flights']})==1
        assert all(f['Actor']==saved['Body']and f['Owner']==saved['Owner']and f['Rank']==21 and f['Fault']is None for f in saved['Pool']['Flights'])
        assert any(f['Wave']for f in saved['Pool']['Flights'])and any(not f['Wave']for f in saved['Pool']['Flights'])
        assert load(stage/'wake-fire-automatic-fixtures.json')['Generation']==load(stage/'wake-fire-missile-fixtures.json')['Generation']==2
    for n in('content-build-1.log','integration-build-3.log'):assert all(w in(STAGE/n).read_text()for w in('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for n,count in(('named-item-runtime-audit.json',1021),('first-loop-affix-audit.json',21)):
        fresh=load(STAGE/n);old=historical('tools/reference/'+n);assert fresh==load(ROOT/'tools/reference'/n)and len(fresh['InputHashes'])==count
        for p,h in fresh['InputHashes'].items():assert sha(ROOT/p)==h,p
        skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in fresh.items()if k not in skip}=={k:v for k,v in old.items()if k not in skip}
    assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573 and markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    for n,word in(('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):assert word in(STAGE/n).read_text()
    assert '\nOK\n'in(STAGE/'python-tests.log').read_text()
    decision=load(ROOT/'tools/reference/wake-learned-sanctuary-decision.json');assert decision['decision']=='approved Sanctuary adaptation'and not decision['isD2RObservation']
    capture=load(ROOT/'tools/reference/warlock-new-process-20261105-evidence.json');assert capture['bytesRead']==1216 and not any(capture[k]for k in('processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'))
    for p,h in capture['evidence'].items():assert sha(ROOT/p)==h,p
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in(('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative;assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for section in('patchHashes','sharedInputs'):
        for p,h in client[section].items():assert sha(ROOT/p)==h,p
    files=[Path(__file__).resolve(),*(ROOT/p for p in production|tests),ROOT/'tools/build_skill_roadmap.py',ROOT/'tests/check_wake_learned.py',ROOT/'tests/check_wake_fire_automatic.py',ROOT/'tests/check_iron_golem_birth.py',
        *(ROOT/'tools/reference'/n for n in('wake-learned-sanctuary-decision.json','wake-fire-automatic-native-evidence.json','warlock-new-process-20261105-evidence.json')),
        *(ROOT/'docs'/n for n in('wake-learned-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(p for p in STAGE.iterdir()if p.is_file()and p.name!='execution-state.json'and not p.name.startswith('receipt-')),
        *(p for p in TARGET.iterdir()if p.is_file())]
    for stage in(STAGE,TARGET):
        for p in stage.glob('attempt-*'):files.extend(x for x in p.rglob('*')if x.is_file())
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,installed=False,goalComplete=False,completedClasses=[],
        component='Learned Wake of Fire through shared do45 S2 casting; approved per-firing NextDelay identity',builds=dict(content=1,integration=3),nativeRun=2,focusedRun=3,auditRun=1,
        verified=dict(nativeMarkers=225,focusedMarkers=54,bootsPerRun=3,actualReloadsPerRun=2,savesPerRun=3,retainedSentryWireRequests=36,newWakeWireRequests=9,
            originalPaidRank21ActorAndFireResume=True,capturedHardFireAndInfernoRanks=20,originalFinalFlightsPerCohort=23,sharedPerFiringLease=True,
            manaPerPlayerCast=13,noNpcEmissionPayment=True,cap=5,shots=5,pythonTests=71,handlers=193,assassinHandlers=23,auditFingerprints=[1021,21],namedDecisionsUnchanged=573,
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,clientPatchesUnchanged=40),artifacts=native['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))},
        limitations=['Four-frame per-firing identity is explicitly user-approved Sanctuary policy. Exact modern placement-versus-firing identity remains unresolved reference data.',
            'Free-cell placement reuses the existing learned sentry contract. Occupied radius1/4 fallback remains unsupported and refuses before payment; source world/terrain/room/event/pool/owner/PvP/client equivalence remains bounded.',
            'Original paid actor/fire/lease inspection clocks are held/rearmed. Existing natural five-shot death and offline expiry controls remain separate; no unheld concurrent-save or older-writer/fault-migration certificate.',
            'Main1/focused1-2 exposed an invalid fixture assumption requiring a five-frame quiet AI window; the actual first-hit snapshot had a two-frame window. Final resumption checks account for legitimate firings through shot budget/execution identities. Diagnostics and tested binaries are retained.',
            'The new D2R process matches pinned immutable anchors but uses D2RMM. Event36/37/40 pages remain unreadable; no code or gameplay behavior from that process is used to certify these mechanics.',
            'No complete class, native/client patch, deployment or final asset. Effects/audio and tree/hotkey icons remain separate.'])
    out=ROOT/'tools/reference/wake-learned-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Stale Wake learned acceptance receipt'
    else:out.write_text(encoded)
    print('Wake learned:225 broader/54 focused markers,each3boots/2reloads;193 handlers,Assassin23,no completed class;installed build and saves preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
