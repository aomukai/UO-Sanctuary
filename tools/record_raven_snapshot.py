#!/usr/bin/env python3
"""Bind Raven stat-profile capture and original saves to the tested candidate."""
import argparse
from collections import Counter
import json
from pathlib import Path
from record_druid_snapshot import expected as previous_expected
import skill_checkpoint_evidence as evidence

ROOT=evidence.ROOT
STAGE=ROOT/'.local/class-skills/raven-snapshot'
WIDE=ROOT/'.local/class-skills/raven-snapshot-broader'
COMPACT=ROOT/'.local/class-skills/raven-snapshot-compact'
PARENT='9d661883a76f31b63365cdbcc3c60e12b023f9bf'
FAMILIES=('raven-snapshot','raven-body','raven-casting')
def expected(broader=False,compact=False):
    result=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})if compact else previous_expected(broader)
    for family in FAMILIES:result[family+'-check']+=1;result[family+'-reload-check']+=2
    for name in ('raven-stats-check','raven-flight-check','raven-snapshot-live-result','raven-right-check','raven-right-result','raven-quick-check','raven-quick-result'):result[name]+=3
    return result

def build(check=False):
    born=evidence.inventory(STAGE,WIDE,STAGE/'pre-build-1.json')
    before=evidence.load(ROOT/'.local/class-skills/druid-snapshot/pre-build-11.json')['inputs']
    now=evidence.load(STAGE/'pre-build-1.json')['inputs']
    changed={p for p in before['sourceInputs'].keys()|now['sourceInputs'].keys()if before['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}
    assert changed=={'src/Sanctuary.Content/DruidRavenBody.cs','tests/Sanctuary.Integration/RavenBodyChecks.cs','tests/Sanctuary.Integration/RavenSnapshotChecks.cs'}
    assert before['copiedData']==now['copiedData']and before['projectInputs']==now['projectInputs']
    for stage,broader,compact,count in ((STAGE,False,False,380),(WIDE,True,False,596),(COMPACT,False,True,39)):
        log=evidence.cohort(stage,expected(broader,compact),count)
        assert 'Raven snapshots:3 public births' in log
        saved=evidence.load(stage/'raven-snapshot-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==3
        assert [r['Hard']for r in saved['Actors']]==[1,7,20]
        assert len({r['Owner']for r in saved['Actors']})==len({r['Body']for r in saved['Actors']})==3
        for row in saved['Actors']:
            state=json.loads(row['State'])
            assert row['OwnerHard']==(1 if row['Hard']==20 else 20)
            assert state['Owner']==row['Owner']and state['StatProfile']=='sanctuary-summon-snapshot-v1'
            assert state['TimingProfile']=='legacy-npc-clock-current-hk-metadata-v1'
            assert state['Birth']['Rank']==state['Packet']['Rank']==row['Hard']+1
            assert state['Remaining']+state['Attempts']==row['Hard']+12 and state['Attempts']==2
            assert state['Remaining']>0 and row['Life']>0 and row['Order']>0
        current=evidence.load(stage/'birth-inputs.json');assert current['artifacts']==born['artifacts']and current['copiedData']==born['copiedData']
        for source,digest in current['sourceInputs'].items():assert evidence.sha(ROOT/source)==digest,source
    for name in ('content-build-1.log','integration-build-1.log'):
        assert all(t in (STAGE/name).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    audit=evidence.audits(STAGE,PARENT,born['artifacts']);evidence.verification(STAGE);protected=evidence.protection()
    prior=evidence.load(ROOT/'tools/reference/druid-snapshot-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==prior['artifacts'][name]
    status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
    files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_raven_snapshot.py',ROOT/'tools/reference/summon-snapshot-decision.json']+[ROOT/p for p in changed]
    files += [ROOT/'docs'/name for name in ('raven-snapshot-checkpoint.md','summon-stat-snapshots.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
    files+=evidence.stage_files(STAGE,WIDE,COMPACT)
    folder=STAGE/'failed-compact-1';files += [p for p in folder.rglob('*')if p.is_file()]
    failed=evidence.load(folder/'birth-inputs.json')
    assert evidence.sha(folder/'check_raven_snapshot.py')==failed['sourceInputs']['tests/check_raven_snapshot.py']
    for kind in ('Content','Integration'):assert evidence.sha(folder/kind.lower()/f'Sanctuary.{kind}.dll')==failed['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],globalSnapshotRolloutComplete=False,
        artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=1,integrationBuild=1,sourceSnapshot=1,focusedRun=1,broaderRun=1,compactRun=2,
        focusedMarkers=380,broaderMarkers=596,compactMarkers=39,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,originalSavedCurrentBirthsPerCohort=3,
        hardRanks=[1,7,20],naturalCurrentLifetimesPerCohort=3,naturalLifetimeAttempts=13,pythonTests=71,auditFingerprints=audit,protection=protected,
        scope='Approved Raven stat profile captures the original effective/hard-synergy result packet and initial budget;three original public births through2reloads;three connected natural13attempt lifetimes after owner changes per cohort.',
        limits=['Historical missing-profile JSON is deliberately constructed,not a separate old-binary writer run. Historical live owner mode and first-think semantics remain explicit.',
            'Existing Raven timing profile remains unchanged and unproven against current executable AI cadence. Source path/geometry and nonzero sunder remain unfinished.',
            'Remaining cross-class readers and handlers,graphical acceptance/effects/icons remain unfinished. No class is complete;no deployment/native/client/protocol/persistence ID change.'])
    evidence.write('raven-snapshot-native-evidence.json',receipt,files,check)
    print('Raven snapshots:380focused/596broader/39compact markers;3originals through2reloads and3natural13attempt lifetimes per cohort;71Python/audits/protection pass.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
