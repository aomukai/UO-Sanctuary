#!/usr/bin/env python3
"""Bind learned Warrior birth upgrades, original saves and retained mechanics."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
from record_creeper_learned import expected as previous_expected
from record_recycler_learned import sha,load
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/warrior-snapshot'
WIDE=ROOT/'.local/class-skills/warrior-snapshot-broader'
PARENT='80ec937f17dda385dd2f90ac5266fdd91dcc945f'


def expected(broader):
    result=previous_expected(broader)
    for family in ('warrior-snapshot','skeleton-upgrades'):
        result[family+'-check']+=1;result[family+'-reload-check']+=2
    result['skeleton-upgrades-live-result']+=3
    return result


def build(check=False):
    paths=(STAGE/'birth-inputs.json',WIDE/'birth-inputs.json',STAGE/'audit-inputs.json')
    born,wide,audit=map(load,paths)
    assert born['artifacts']==wide['artifacts']==audit['artifacts'] and born['copiedData']==wide['copiedData']==audit['copiedData']
    for path in paths:
        snapshot(STAGE/'pre-build-1.json',path)
        for source,digest in load(path)['sourceInputs'].items():assert sha(ROOT/source)==digest,source
    before=load(ROOT/'.local/class-skills/creeper-learned/pre-build-5.json')['inputs'];now=load(STAGE/'pre-build-1.json')['inputs']
    changed={p for p in before['sourceInputs'].keys()|now['sourceInputs'].keys() if before['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}
    assert changed=={'src/Sanctuary.Content/SkeletonPetUpgrades.cs','src/Sanctuary.Content/SkeletonPets.cs','src/Sanctuary.Content/SummonUpgradeSnapshots.cs','tests/Sanctuary.Integration/SkeletonUpgradeChecks.cs','tests/Sanctuary.Integration/WarriorSnapshotChecks.cs'}
    assert before['copiedData']==now['copiedData'] and before['projectInputs']==now['projectInputs']
    for stage,broader,count in ((STAGE,False,248),(WIDE,True,464)):
        log=(stage/'birth-native-checks.log').read_text()
        assert markers(stage/'birth-native-checks.log')==expected(broader) and sum(markers(stage/'birth-native-checks.log').values())==count
        assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert 'Warrior snapshots:actual learned hard9+soft1 and hard20 births' in log
        assert 'Skeleton upgrades:born hard9+soft1/19+soft1locked' in log
        saved=load(stage/'warrior-snapshot-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==2
        assert [row['Upgrade']['HardRank'] for row in saved['Actors']]==[9,20]
        assert [row['Life'] for row in saved['Actors']]==[1111,2222]
        assert len({row['Body'] for row in saved['Actors']})==2
        for row in saved['Actors']:
            assert row['Upgrade']['Actor']==row['Body'] and row['Upgrade']['Owner']==saved['Owner'] and row['Maximum']>row['Life'] and row['Order']>0
        assert saved['Actors'][0]['Rank']==10 and saved['Actors'][1]['Rank']==21
        assert load(stage/'skeleton-upgrades-fixtures.json')['Generation']==2
        assert load(stage/'creeper-learned-fixtures.json')['Generation']==2
    for name in ('content-build-1.log','integration-build-1.log'):assert all(s in (STAGE/name).read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for kind in ('Content','Integration'):assert sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    prior=load(ROOT/'tools/reference/creeper-learned-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==prior['artifacts'][name]
    fingerprints={}
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        current=load(STAGE/name);assert current==load(ROOT/'tools/reference'/name)
        baseline=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));omit={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in omit}=={k:v for k,v in baseline.items() if k not in omit}
        assert current['ContentAssemblySha256']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
        for source,digest in current['InputHashes'].items():assert sha(ROOT/source)==digest,source
        fingerprints[name]=len(current['InputHashes'])
    assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573
    assert markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    status=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses'] and sum(r['implementation']=='handler-present' for r in status['skills'])==197
    commands=load(STAGE/'verification-commands.json');assert len(commands)>=13 and all(c['exitCode']==0 for c in commands)
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    installation=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installation['installedArtifacts'])==106
    for path,digest in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/path)==digest,path
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installation['runtimeRoot'])/'.local/server'/relative;assert len(protected[kind])==count
        assert {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protected[kind]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for kind in ('patchHashes','sharedInputs'):
        for path,digest in client[kind].items():assert sha(ROOT/path)==digest,path
    files=[Path(__file__).resolve(),ROOT/'tests/check_warrior_snapshot.py',ROOT/'tools/reference/summon-snapshot-decision.json']+[ROOT/p for p in changed]
    files += [ROOT/'docs'/name for name in ('warrior-snapshot-checkpoint.md','summon-stat-snapshots.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
    for stage in (STAGE,WIDE):files += [p for p in stage.iterdir() if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json']
    receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],globalSnapshotRolloutComplete=False,
        scope='Learned Skeleton Warrior hard-point upgrades freeze at birth;optional persistence192;explicit legacy fallback for missing historical metadata.',
        artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=1,integrationBuild=1,sourceSnapshot=1,focusedRun=1,broaderRun=1,
        focusedMarkers=248,broaderMarkers=464,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,originalSnapshotWarriorsPerCohort=2,birthHardRanks=[9,20],ownerHardRankAtSave=20,
        actualNaturalFastMovementPerCohort=3,pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=fingerprints,
        protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        limits=['New learned Warrior births capture hard points;historical actors without metadata retain the previous live rule until resummoned. No invented hard rank from effective rank. Current fixtures construct missing-metadata actors;no separate older-binary writer run is claimed.',
            'Other Necromancer,Druid and cross-class live owner readers remain pending. Existing captured base stats are not rewritten. No handler/class completion or deployment.',
            'No existing actor layout,native/client/protocol or artwork change. Optional immutable upgrade persistence192is new. Existing geometry/current-executable/world/asset limitations remain.'],
        files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
    out=ROOT/'tools/reference/warrior-snapshot-native-evidence.json';encoded=json.dumps(receipt,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Warrior snapshot receipt stale'
    else:out.write_text(encoded)
    print('Warrior snapshots:248focused/464broader markers;born9/20originals through2reloads,actual movement/return,71Python/audits/protection pass;197handlers.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
