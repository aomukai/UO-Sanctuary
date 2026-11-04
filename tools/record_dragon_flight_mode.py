#!/usr/bin/env python3
"""Bind Flight's prepared fixed clock to complete source and retained native cohorts."""
import argparse
from collections import Counter
import hashlib
import json
import subprocess
from pathlib import Path
from record_dragon_flight_preparation import expected as previous_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/dragon-flight-mode-native'
BROADER=ROOT/'.local/class-skills/dragon-flight-mode-broader'
SOURCE=ROOT/'.local/class-skills/dragon-flight-mode'
PARENT='915088006a6dc055c22232f6d6743b1c3435f576'


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))


def expected(broader):
    result=previous_expected(broader)
    result['dragon-flight-mode-check']=1;result['dragon-flight-mode-reload-check']=2
    return result


def build(check=False):
    native=load(STAGE/'birth-inputs.json');wide=load(BROADER/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
    assert native['artifacts']==wide['artifacts']==audit['artifacts'] and native['copiedData']==wide['copiedData']==audit['copiedData']
    for p in (STAGE/'birth-inputs.json',BROADER/'birth-inputs.json',STAGE/'audit-inputs.json'):
        snapshot(STAGE/'pre-build-1.json',p)
        for source,h in load(p)['sourceInputs'].items():assert sha(ROOT/source)==h,source
    before=load(ROOT/'.local/class-skills/dragon-flight-preparation/pre-build-1.json')['inputs']
    current=load(STAGE/'pre-build-1.json')['inputs']
    changed=lambda section:{p for p in before[section].keys()|current[section].keys() if before[section].get(p)!=current[section].get(p)}
    assert changed('sourceInputs')=={'src/Sanctuary.Content/ActionTiming.cs','src/Sanctuary.Content/DragonFlightPreparation.cs','tests/Sanctuary.Integration/DragonFlightModeChecks.cs'}
    assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json'} and before['projectInputs']==current['projectInputs']
    for stage,broader,count in ((STAGE,False,60),(BROADER,True,276)):
        log=(stage/'birth-native-checks.log').read_text();assert markers(stage/'birth-native-checks.log')==expected(broader)
        assert sum(markers(stage/'birth-native-checks.log').values())==count
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert log.count('1680 full source mode/rate/event comparisons,1296 native modifier controls')==3
        assert log.count('53830 complete scheduler comparisons')==3
        owner=load(stage/'dragon-flight-mode-fixtures.json');assert owner['Owner']>0 and owner['Mana']>0
    for name in ('content-build-1.log','integration-build-1.log'):
        assert all(s in (STAGE/name).read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):
        assert sha(STAGE/kind/name)==native['artifacts']['Assemblies/'+name]
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert sha(STAGE/'native/Distribution'/name)==native['artifacts'][name]
    source=load(ROOT/'tools/reference/dragon-flight-mode-source-evidence.json')
    assert source['cases']==960 and source['flightClockCases']==120 and source['compiledRoutineCount']==15 and source['modulatorArrays']==3
    assert source['currentFields']==dict(anim='SQ',seqtrans='A1',seqnum=21,UseAttackRate=0)
    assert not source['currentExecutableCodeParity'] and not source['handlerEnabled'] and not source['installed']
    for section in ('inputs','artifacts'):
        for p,h in source[section].items():assert sha(ROOT/p)==h,p
    prior_source=load(ROOT/'tools/reference/dragon-flight-source-evidence.json')
    assert prior_source['currentSequence']==21 and prior_source['classicDescriptorsMatched']==322
    for section in ('inputs','artifacts'):
        for p,h in prior_source[section].items():assert sha(ROOT/p)==h,p
    preflight=load(ROOT/'tools/reference/dragon-flight-preflight.json')
    assert preflight['modeFlags']==dict(UseAttackRate=0,ownedRawUseAttackRate='') and not preflight['handlerEnabled']
    for name,count in (('named-item-runtime-audit.json',1027),('first-loop-affix-audit.json',21)):
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
    assert load(SOURCE/'validation-attempt-2/diagnostic.json')['actual'][0:2]==[256,32767]
    assert sha(SOURCE/'validation-attempt-2/probe')==sha(SOURCE/'before-abi-alignment/probe')  # expectation correction only
    assert sha(SOURCE/'before-abi-alignment/cases.json')==sha(SOURCE/'cases.json')
    assert load(SOURCE/'before-abi-alignment/source-evidence.json')['compiledUnchangedRoutineHashes']==source['compiledUnchangedRoutineHashes']
    for stage,broader in ((STAGE,False),(BROADER,True)):
        assert markers(stage/'before-abi-alignment/birth-native-checks.log')==expected(broader)
        for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):
            assert sha(stage/'before-abi-alignment'/kind/name)==sha(STAGE/kind/name)
    files=[Path(__file__).resolve(),ROOT/'src/Sanctuary.Content/DragonFlightPreparation.cs',ROOT/'src/Sanctuary.Content/ActionTiming.cs',ROOT/'tests/Sanctuary.Integration/DragonFlightModeChecks.cs',
        ROOT/'tests/check_dragon_flight_mode.py',ROOT/'tools/probe_dragon_flight_mode_reference.py',ROOT/'tools/prepare_dragon_flight_reference.py',ROOT/'tools/build_skill_roadmap.py',
        *(ROOT/'tools/reference'/p for p in ('dragon-flight-mode-source-evidence.json','dragon-flight-source-evidence.json','dragon-flight-preflight.json','creeper-frame-installation-evidence.json')),
        *(ROOT/'docs'/p for p in ('dragon-flight-mode-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))]
    files.extend(ROOT/p for p in source['artifacts'])
    for stage in (STAGE,BROADER):files.extend(p for p in stage.glob('*') if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
    for name in ('compile-attempt-1','validation-attempt-2','before-abi-alignment'):files.extend(p for p in (SOURCE/name).iterdir() if p.is_file())
    for stage in (STAGE,BROADER):files.extend(p for p in (stage/'before-abi-alignment').rglob('*') if p.is_file())
    evidence=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,installed=False,handlerEnabled=False,
        scope='Prepared ordinary-human fixed Flight clock and shared CombatTiming supplier; not a learned teleport/kick cast',
        handlers=194,assassinHandlers=24,completedClasses=[],artifacts=native['artifacts'],copiedData=native['copiedData'],
        focusedMarkers=60,broaderMarkers=276,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
        focusedRun=2,broaderRun=2,auditRun=1,headerAlignmentChangedCaseBytes=False,headerAlignmentChangedCSharpOrData=False,
        modeSourceControls=960,compiledAdditionalOlderRoutines=15,modulatorArrays=3,sourceSanitizers='ASan+UBSan clean',
        selectedSourceModeCases=120,nativeSourceClockComparisonsPerBoot=1680,nativeModifierControlsPerBoot=1296,
        clock=dict(advance=256,teleport=9,kick=14,end=23),currentExecutableCodeParity=False,
        pythonTests=71,auditFingerprints=1027,affixFingerprints=21,namedDecisionsUnchanged=573,
        protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        diagnostics=['Initial compiler lacked limits/MONMODE count in the declared scaffold; original files/logs retained.',
                     'Initial Python expected an unclamped artificial Other display speed. Full setter clamps32767; corrected expectation and unchanged pre-alignment tested binary/raw cases are retained. Sequence advance256 is unaffected.',
                     'Unselected monster block/knockback and NPC/stun constants were aligned with pinned headers. All960 case bytes and complete routine/array bodies are identical. Initial native packages/binaries retained;final focused2/broader2 rerun with current oracle receipt on the same frozen DLLs.'],
        limits=['Older complete mode/rate behavior plus owned current flag-off data is the prepared reference projection;current executable code parity remains unverified.',
                'No learned Flight cast,teleport permission/search,landing payment,contact/charge/cancellation,natural input or saved Flight action is enabled or certified. The original saved owner verifies unchanged mana/ordinary/Tail timing only.',
                'Alternate form/passive/used-skill contexts and generic skill-start/payment chain remain separate. Positive attack/cast writer controls are explicit services,not rate arithmetic acceptance.',
                'Flight landing rule and earlier Claw charge-consumption questions remain unanswered and nonblocking;Tail approval is Tail-specific.',
                'Final departure/arrival/kick/charge/audio effects and tree/hotkey icons remain needed. No deployment,native/client/protocol or persistence-schema change.'],
        files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
    out=ROOT/'tools/reference/dragon-flight-mode-native-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Flight mode native receipt stale'
    else:out.write_text(encoded)
    print('Dragon Flight mode:60 focused/276 broader markers,2actual reloads each,1680 source clock/1296 modifier controls per boot;71Python/1027+21audits/573decisions/protected106+38+20+40 pass;learned cast remains disabled.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
