#!/usr/bin/env python3
"""Bind prepared Flight native transfer, shared search and original Teleport input/save."""
import argparse
from collections import Counter
import hashlib
import json
import subprocess
from pathlib import Path
from record_dragon_flight_mode import expected as previous_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/dragon-flight-landing'
BROADER=ROOT/'.local/class-skills/dragon-flight-landing-broader'
PARENT='651ea213a712d14fc9667aa575c24ea8df4585b2'


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))


def expected(broader):
    result=previous_expected(broader)
    for family in ('dragon-flight-landing','player-teleport'):
        result[family+'-check']=1;result[family+'-reload-check']=2
    result['leap-geometry-check']=3;result['player-teleport-reload-result']=2;result['player-teleport-save']=3
    for mode in ('left','right','quick'):
        result['player-teleport-'+mode+'-check']=3;result['player-teleport-'+mode+'-result']=3
    return result


def build(check=False):
    native=load(STAGE/'birth-inputs.json');wide=load(BROADER/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
    assert native['artifacts']==wide['artifacts']==audit['artifacts'] and native['copiedData']==wide['copiedData']==audit['copiedData']
    for p in (STAGE/'birth-inputs.json',BROADER/'birth-inputs.json',STAGE/'audit-inputs.json'):
        snapshot(STAGE/'pre-build-1.json',p)
        for source,h in load(p)['sourceInputs'].items():assert sha(ROOT/source)==h,source
    before=load(ROOT/'.local/class-skills/dragon-flight-mode-native/pre-build-1.json')['inputs'];now=load(STAGE/'pre-build-1.json')['inputs']
    changed=lambda section:{p for p in before[section].keys()|now[section].keys() if before[section].get(p)!=now[section].get(p)}
    production={'src/Sanctuary.Content/'+n+'.cs' for n in ('LeapGeometry','Teleportation','SourceFreeCoordinates','DragonFlightLanding')}
    assert changed('sourceInputs')==production|{'tests/Sanctuary.Integration/DragonFlightLandingChecks.cs'}
    assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json'} and before['projectInputs']==now['projectInputs']
    for stage,broader,count in ((STAGE,False,92),(BROADER,True,308)):
        log=(stage/'birth-native-checks.log').read_text();assert markers(stage/'birth-native-checks.log')==expected(broader)
        assert sum(markers(stage/'birth-native-checks.log').values())==count
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert log.count('free-cell cases: 405; existing Terror distance cases: 32805')==3
        assert log.count('1680 full source mode/rate/event comparisons,1296 native modifier controls')==3
        assert log.count('53830 complete scheduler comparisons')==3
        landing=load(stage/'dragon-flight-landing-fixtures.json');assert landing['Owner']>0 and landing['Target']>0 and landing['Mana']>0 and landing['Life']>0
        teleport=load(stage/'player-teleport-fixtures.json');assert teleport['Generation']==2 and teleport['PendingStaged'] and teleport['PendingScroll']==0
    for name in ('content-build-1.log','integration-build-1.log'):
        assert all(s in (STAGE/name).read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):
        assert sha(STAGE/kind/name)==native['artifacts']['Assemblies/'+name]
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert sha(STAGE/'native/Distribution'/name)==native['artifacts'][name]
    source=load(ROOT/'tools/reference/free-coordinates-source-evidence.json');assert source['cases']==1210 and len(source['compiledUnchangedRoutineHashes'])==2
    assert not source['currentExecutableCodeParity'] and source['sanitizers']=='ASan+UBSan clean'
    for section in ('inputs','artifacts'):
        for p,h in source[section].items():assert sha(ROOT/p)==h,p
    preflight=load(ROOT/'tools/reference/dragon-flight-preflight.json');assert not preflight['handlerEnabled'] and not preflight['installed']
    for name,count in (('named-item-runtime-audit.json',1029),('first-loop-affix-audit.json',21)):
        data=load(STAGE/name);old=historical('tools/reference/'+name)
        assert data==load(ROOT/'tools/reference'/name) and len(data['InputHashes'])==count
        for p,h in data['InputHashes'].items():assert sha(ROOT/p)==h,p
        omit={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in data.items() if k not in omit}=={k:v for k,v in old.items() if k not in omit}
    assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573
    assert markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    status=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');old=historical('data/Sanctuary/Balance/class-skill-status.json')
    assert {x['talentId'] for x,y in zip(status['skills'],old['skills'],strict=True) if x!=y}=={'assassin.dragonflight'}
    assert not status['completedClasses'] and sum(x['implementation']=='handler-present' for x in status['skills'])==194
    assert sum(x['characterClass']=='Assassin' and x['implementation']=='handler-present' for x in status['skills'])==24
    for name,needle in (('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):
        assert needle in (STAGE/name).read_text()
    assert '\nOK\n' in (STAGE/'python-tests.log').read_text() and all(r['exitCode']==0 for r in load(STAGE/'verification-commands.json'))
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative;assert len(protected[key])==count
        assert {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protected[key]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for section in ('patchHashes','sharedInputs'):
        for p,h in client[section].items():assert sha(ROOT/p)==h,p
    files=[Path(__file__).resolve(),*(ROOT/p for p in production),ROOT/'tests/Sanctuary.Integration/DragonFlightLandingChecks.cs',ROOT/'tests/check_dragon_flight_landing.py',
        ROOT/'tools/probe_free_coordinates_reference.py',ROOT/'tools/prepare_dragon_flight_reference.py',ROOT/'tools/build_skill_roadmap.py',
        *(ROOT/'tools/reference'/p for p in ('free-coordinates-source-evidence.json','dragon-flight-preflight.json','creeper-frame-installation-evidence.json')),
        *(ROOT/'docs'/p for p in ('dragon-flight-landing-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))]
    files.extend(ROOT/p for p in source['artifacts'])
    files.append(STAGE/'record-attempt-1.py')
    for stage in (STAGE,BROADER):files.extend(p for p in stage.glob('*') if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
    evidence=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,installed=False,handlerEnabled=False,
        scope='Prepared native current-target landing and actual transfer,shared Leap search and ordinary Teleport regression;not a learned Flight cast',
        handlers=194,assassinHandlers=24,completedClasses=[],artifacts=native['artifacts'],copiedData=native['copiedData'],
        focusedMarkers=92,broaderMarkers=308,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
        sourceFreeCellControls=1210,compiledOlderRoutines=2,sourceSanitizers='ASan+UBSan clean',
        retainedLeapTargetControlsPerBoot=3240,retainedLeapFreeControlsPerBoot=405,retainedTerrorControlsPerBoot=32805,
        actualTeleportInputRequestsPerCohort=9,nativeFlightInputRequests=0,savedFlightActions=0,
        currentExecutableCodeParity=False,pythonTests=71,auditFingerprints=1029,affixFingerprints=21,namedDecisionsUnchanged=573,
        protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        diagnostics=['Initial receipt expected3generations/a final scroll. Existing Teleport increments generation on2actual reloads,not3saves,and only middle generation1stages the scroll. Final generation2/scroll0is correct;native tests and binaries were unchanged.'],
        limits=['The complete source scalar search has declared same-room/field-null/size2/mask1C09/max50/increment1 services;actual D2 room topology/field/footprint equivalence is not established.',
                'Native two-source-cell-per-tile ground projection remains provisional for half-cell/height/size/mask equivalence. Travel/treasure/town/house/multi policies are existing Sanctuary world adapters.',
                'Prepared transfer reports actual movement and decides no mana/contact/charge policy. It cancels a queued unpaid Tail via existing location hooks;full two-stage Flight self-teleport/cancellation is still unfinished.',
                'Original moved owner/wounds/mana/charges and target health survive actual reloads with target AI held frozen/stopped. No unheld external AI/save race or saved Flight action is certified.',
                'Actual left/right/quick requests and paid scroll/belt/resource/pending-action reload controls belong to retained Sorceress Teleport,not Flight input.',
                'Flight landing/payment and earlier Claw charge questions remain unanswered/nonblocking;Tail approval is Tail-specific. Learned Flight remains disabled.',
                'Shared generic Teleport0x3728particles/0x1FEsound are identified placeholders;final Flight effects/audio and separate tree/hotkey icons remain needed. No deployment,native/client/protocol or persistence-schema change.'],
        files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
    out=ROOT/'tools/reference/dragon-flight-landing-native-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Flight landing receipt stale'
    else:out.write_text(encoded)
    print('Prepared Flight landing:92focused/308broader markers,2reloads each;1210source traces and retained Leap/actual Teleport inputs;71Python/1029+21audits/573decisions/protected106+38+20+40 pass;learned Flight disabled.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
