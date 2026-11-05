#!/usr/bin/env python3
"""Bind approved Creeper snapshots, actual casting/poison and retained saves."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess

from record_recycler_learned import expected as previous_expected, sha, load
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/creeper-learned'
WIDE=ROOT/'.local/class-skills/creeper-learned-broader'
PARENT='f14b0b706c387ecd785c1ad59e6581c0048c8dc9'


def expected(broader):
    result=previous_expected(broader)
    for family in ('creeper-learned','creeper-poison','creeper-ground','creeper-world','creeper-body','creeper-balance'):
        result[family+'-check']+=1
        result[family+'-reload-check']+=2
    for command in ('creeper-ai-check','creeper-stats-check','creeper-fields-check','creeper-learned-live-result'):
        result[command]+=3
    for family in ('creeper-learned-right','creeper-learned-quick'):
        result[family+'-check']+=3
        result[family+'-result']+=3
    return result


def build(check=False):
    paths=(STAGE/'birth-inputs.json',WIDE/'birth-inputs.json',STAGE/'audit-inputs.json')
    born,wide,audit=map(load,paths)
    assert born['artifacts']==wide['artifacts']==audit['artifacts']
    assert born['copiedData']==wide['copiedData']==audit['copiedData']
    for path in paths:
        snapshot(STAGE/'pre-build-5.json',path)
        for source,digest in load(path)['sourceInputs'].items():
            assert sha(ROOT/source)==digest,source
    before=load(ROOT/'.local/class-skills/recycler-learned/pre-build-7.json')['inputs']
    now=load(STAGE/'pre-build-5.json')['inputs']
    changed=lambda kind:{p for p in before[kind].keys()|now[kind].keys() if before[kind].get(p)!=now[kind].get(p)}
    production={'ClassCombat','DruidCombat','DruidVineCasting','DruidCreeperBody','DruidCreeperCasting',
                'DruidCreeperGround','DruidCreeperPoison','DruidCreeperStats','DruidCreeperWorld','PoisonStates'}
    fixtures={'CreeperAiChecks','CreeperBalanceChecks','CreeperBodyChecks','CreeperCastingChecks',
              'CreeperPoisonChecks','CreeperStatsChecks','CreeperWorldChecks','RecyclerLearnedChecks','CreeperLearnedChecks'}
    assert changed('sourceInputs')=={f'src/Sanctuary.Content/{n}.cs' for n in production}|{f'tests/Sanctuary.Integration/{n}.cs' for n in fixtures}
    assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json'}
    assert before['projectInputs']==now['projectInputs']
    for stage,broader,count in ((STAGE,False,239),(WIDE,True,455)):
        log=(stage/'birth-native-checks.log').read_text()
        assert markers(stage/'birth-native-checks.log')==expected(broader)
        assert sum(markers(stage/'birth-native-checks.log').values())==count
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert 'Learned Creeper:public paid SCcast/rollback/hard bindings' in log
        assert log.count('Learned recycler vines:252approved Solar whole-point controls')==1
        saved=load(stage/'creeper-learned-fixtures.json')
        assert saved['Generation']==2 and saved['Life']==1111 and saved['Mana']>0
        birth=saved['State']['Snapshot']
        assert birth['Birth']['Rank']>1 and birth['RabiesHardRank']==1
        assert saved['State']['Owner']==saved['Owner']
        assert len(saved['Grounds'])==2
        assert [g['PoisonContext']['Profile'] for g in saved['Grounds']]==[
            'sanctuary-creeper-summon-snapshot-2000-v2','sanctuary-creeper-base-2000-v1']
        for ground in saved['Grounds']:
            assert ground['Owner']==saved['Owner'] and ground['Actor']!=saved['Body']
            assert ground['Rank']==1 and ground['PoisonContext']['RabiesHardRank']==0
            assert ground['Fields']['Mats'] and all(m['Poison']['Damage']['Frames']==100 for m in ground['Fields']['Mats'])
        assert load(stage/'creeper-balance-fixtures.json')['Generation']==2
        assert load(stage/'recycler-learned-fixtures.json')['Generation']==2
        assert load(stage/'npc-kill-proc-fixtures.json')['Generation']==2
    for name in ('content-build-2.log','integration-build-4.log'):
        assert all(s in (STAGE/name).read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for kind in ('Content','Integration'):
        assert sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    decision=load(ROOT/'tools/reference/summon-snapshot-decision.json')
    assert decision['approvedDeviation'] and len(decision['answers'])==2 and 'all classes' in decision['answers'][1]
    fingerprints={}
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        current=load(STAGE/name)
        assert current==load(ROOT/'tools/reference'/name)
        baseline=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT))
        omit={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in omit}=={k:v for k,v in baseline.items() if k not in omit}
        assert current['ContentAssemblySha256']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
        for source,digest in current['InputHashes'].items():
            assert sha(ROOT/source)==digest,source
        fingerprints[name]=len(current['InputHashes'])
    assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573
    assert markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    status=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert not status['completedClasses']
    assert sum(r['implementation']=='handler-present' for r in status['skills'])==197
    assert sum(r['characterClass']=='Druid' and r['implementation']=='handler-present' for r in status['skills'])==29
    assert all(c['exitCode']==0 for c in load(STAGE/'verification-commands.json'))
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    installation=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json')
    assert len(installation['installedArtifacts'])==106
    for path,digest in installation['installedArtifacts'].items():
        assert sha(Path(installation['runtimeRoot'])/path)==digest,path
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installation['runtimeRoot'])/'.local/server'/relative
        assert len(protected[kind])==count
        assert {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protected[kind]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json')
    assert client['patches']==40
    for kind in ('patchHashes','sharedInputs'):
        for path,digest in client[kind].items():
            assert sha(ROOT/path)==digest,path
    files=[Path(__file__).resolve(),ROOT/'tests/check_creeper_learned.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/reference/summon-snapshot-decision.json']
    files += [ROOT/p for p in changed('sourceInputs')]
    files += [ROOT/'docs'/name for name in ('creeper-learned-checkpoint.md','summon-stat-snapshots.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
    for stage in (STAGE,WIDE):
        files += [p for p in stage.iterdir() if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json']
    for name in ('failed-focused-1','failed-focused-2','failed-live-diagnostic-1'):
        folder=STAGE/name
        files += [p for p in folder.rglob('*') if p.is_file()]
        frozen=load(folder/'birth-inputs.json')
        for kind in ('Content','Integration'):
            assert sha(folder/kind.lower()/f'Sanctuary.{kind}.dll')==frozen['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,druidHandlers=29,completedClasses=[],
        learned='druid.poisoncreeper',globalSnapshotRolloutComplete=False,sunderDelivery=False,
        artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=2,integrationBuild=4,sourceSnapshot=5,
        focusedRun=3,broaderRun=1,focusedMarkers=239,broaderMarkers=455,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
        actualInputCastsPerCohort=6,naturalLearnedCastAiPoisonPerCohort=3,originalSavedCreeperPerCohort=1,mixedSavedFieldProfiles=2,
        pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=fingerprints,
        protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        diagnostics=['Focused1 stopped at unavailable Trang set gear; used the available Death Web poison-pierce control without weakening the frozen-context assertion.',
            'Focused2 and isolated diagnostic1 reached real field contacts and life loss, but the fixture required native UO Poisoned. The same native-only query in Creeper AI missed managed poison. Shared PoisonStates.Active observes both without advancing clocks; world checks now verify managed-poison escape, expiry and cure. Failed sources/inputs/binaries/logs are archived.'],
        limits=['Summon-time inheritance is an approved Sanctuary adaptation. Other summon/trap live owner readers and hard-point unlock controllers need the separate cross-class rollout.',
            'Nonzero poison sunder still refuses before payment; modern table value300 is not silently treated as a boolean. No generic sunder consumer or item admission is added.',
            'Source AStar path fallback, exact current room/path/vision/global scheduling, extreme inherited skill caps and graphical acceptance remain unfinished. No class is complete; Arctic Blast is Druid\'s missing handler.',
            'No deployment, native/client/protocol change or asset generation. Final effects/audio and talent/hotkey icons remain separately identified placeholders.'],
        files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
    out=ROOT/'tools/reference/creeper-learned-native-evidence.json'
    encoded=json.dumps(receipt,indent=2)+'\n'
    if check:
        assert out.read_text()==encoded,'Learned Creeper receipt stale'
    else:
        out.write_text(encoded)
    print('Learned Creeper:239focused/455broader markers;actual input/AI poison and frozen fields through2reloads;71Python/audits/protection pass;197handlers,Druid29/30.')


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--check',action='store_true')
    build(parser.parse_args().check)
