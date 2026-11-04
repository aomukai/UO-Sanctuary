#!/usr/bin/env python3
"""Bind current SQ21 native planning and its retained original cohorts."""
import argparse
from collections import Counter
import hashlib
import json
import subprocess
from pathlib import Path

from record_dragon_tail_learned import expected as tail_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/dragon-flight-preparation'
BROADER=ROOT/'.local/class-skills/dragon-flight-preparation-broader'
SOURCE=ROOT/'.local/class-skills/dragon-flight-source'
PARENT='31432ffbb666a9a4682901b37567b1e883f1340b'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def load(path):return json.loads(path.read_text())
def historical(path):return json.loads(subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT))


def expected(broader):
    result=tail_expected(broader)
    for family in ('dragon-flight-sequence','cleave','mirrored-blades'):
        result[family+'-check']=1;result[family+'-reload-check']=2
    return result


def build(check=False):
    native=load(STAGE/'birth-inputs.json');wide=load(BROADER/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
    assert native['artifacts']==wide['artifacts']==audit['artifacts']
    assert native['copiedData']==wide['copiedData']==audit['copiedData']
    for p in (STAGE/'birth-inputs.json',BROADER/'birth-inputs.json',STAGE/'audit-inputs.json'):
        snapshot(STAGE/'pre-build-1.json',p)
    before=load(ROOT/'.local/class-skills/dragon-tail-learned/pre-build-8.json')['inputs']
    current=load(STAGE/'pre-build-1.json')['inputs']
    changed=lambda section:{p for p in before[section].keys()|current[section].keys() if before[section].get(p)!=current[section].get(p)}
    assert changed('sourceInputs')=={'src/Sanctuary.Content/PlayerWeaponSequences.cs','tests/Sanctuary.Integration/DragonFlightSequenceChecks.cs'}
    assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/dragon-flight-sequence.json'}
    assert before['projectInputs']==current['projectInputs']
    for stage,broader,count in ((STAGE,False,57),(BROADER,True,273)):
        log=(stage/'birth-native-checks.log').read_text()
        assert markers(stage/'birth-native-checks.log')==expected(broader)
        assert sum(markers(stage/'birth-native-checks.log').values())==count
        assert ' ERR]' not in log and ': failed' not in log
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert log.count('53830 complete scheduler comparisons')==3
        assert load(stage/'dragon-flight-sequence-fixtures.json')==dict(Profiles=14,Comparisons=53830,LearnedCast=False)
    for name in ('content-build-1.log','integration-build-1.log'):
        assert all(s in (STAGE/name).read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):
        assert sha(STAGE/kind/name)==native['artifacts']['Assemblies/'+name]
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert sha(STAGE/'native/Distribution'/name)==native['artifacts'][name]
    source=load(ROOT/'tools/reference/dragon-flight-source-evidence.json')
    assert source['cases']==dict(start=5376,release=768,landing=384,warp=256,coordinates=18,sequence=3845)
    assert len(source['compiledUnchangedRoutineHashes'])==9 and source['classicDescriptorsMatched']==322
    assert not source['currentExecutableParity'] and not source['handlerEnabled'] and not source['installed']
    for section in ('inputs','artifacts'):
        for p,h in source[section].items():assert sha(ROOT/p)==h,p
    assert sha(ROOT/source['runtimePath'])==source['runtimeSha256']
    preflight=load(ROOT/'tools/reference/dragon-flight-preflight.json')
    assert preflight['legacyInspection']['executed'] and not preflight['handlerEnabled'] and not preflight['installed']
    for name,count in (('named-item-runtime-audit.json',1026),('first-loop-affix-audit.json',21)):
        data=load(STAGE/name);old=historical('tools/reference/'+name)
        assert data==load(ROOT/'tools/reference'/name) and len(data['InputHashes'])==count
        for p,h in data['InputHashes'].items():assert sha(ROOT/p)==h,p
        omit={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in data.items() if k not in omit}=={k:v for k,v in old.items() if k not in omit}
    assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573
    assert markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    status=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');prior=historical('data/Sanctuary/Balance/class-skill-status.json')
    assert {x['talentId'] for x,y in zip(status['skills'],prior['skills'],strict=True) if x!=y}=={'assassin.dragonflight'}
    assert sum(x['implementation']=='handler-present' for x in status['skills'])==194 and not status['completedClasses']
    assert sum(x['characterClass']=='Assassin' and x['implementation']=='handler-present' for x in status['skills'])==24
    for name,needle in (('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):
        assert needle in (STAGE/name).read_text()
    assert '\nOK\n' in (STAGE/'python-tests.log').read_text()
    assert all(r['exitCode']==0 for r in load(STAGE/'verification-commands.json'))
    # Historical reproduction failures remain diagnostics, not green results.
    assert 'cleave/skilldesc.txt' in (SOURCE/'mirrored-historical-reproduction.log').read_text()
    assert 'Stale named runtime evidence' in (SOURCE/'initial-source-pass/python-tests.log').read_text()
    assert all(r['matchesExistingRuntimeData'] for r in load(SOURCE/'shared-reader-controls.json'))
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative;assert len(protected[key])==count
        assert {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protected[key]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for section in ('patchHashes','sharedInputs'):
        for p,h in client[section].items():assert sha(ROOT/p)==h,p
    files=[Path(__file__).resolve(),ROOT/'src/Sanctuary.Content/PlayerWeaponSequences.cs',ROOT/'tests/Sanctuary.Integration/DragonFlightSequenceChecks.cs',
        ROOT/'tests/check_dragon_flight_preparation.py',ROOT/'tools/player_weapon_sequences.py',ROOT/'tools/probe_dragon_flight_reference.py',
        ROOT/'tools/prepare_dragon_flight_reference.py',ROOT/'tools/build_skill_roadmap.py',
        *(ROOT/'tools/reference'/p for p in ('dragon-flight-source-evidence.json','dragon-flight-preflight.json','creeper-frame-installation-evidence.json')),
        *(ROOT/'docs'/p for p in ('dragon-flight-preparation-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))]
    files.extend(ROOT/p for p in source['artifacts'])
    for stage in (STAGE,BROADER):
        files.extend(p for p in stage.glob('*') if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
    files.extend(SOURCE/p for p in ('shared-reader-controls.json','mirrored-historical-reproduction.log','initial-source-pass/python-tests.log','initial-source-pass/probe'))
    evidence=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,installed=False,handlerEnabled=False,
        scope='Prepared current SQ21 data and supplied-rate native planner; not a learned Flight cast',
        handlers=194,assassinHandlers=24,completedClasses=[],artifacts=native['artifacts'],copiedData=native['copiedData'],
        focusedMarkers=57,broaderMarkers=273,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
        sourceControls=10647,compiledOlderRoutines=9,sourceSanitizers='ASan+UBSan clean',currentSequenceProfiles=14,nativeSchedulerComparisonsPerBoot=53830,
        sourceExecutableCodeParity=False,pythonTests=71,auditFingerprints=1026,affixFingerprints=21,namedDecisionsUnchanged=573,
        protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        diagnostics=['Initial Python failures from stale descriptive-status audit fingerprints are retained; refreshed final audits/tests pass.',
                     'Historical Mirrored extraction generator cannot reproduce without preexisting ignored cleave/skilldesc.txt. No new generator acceptance is claimed. Direct current SQ24/SQ25 data and native original-actor/gear reload controls pass.'],
        limits=['The sequence planner supplies no attack rate, target acquisition, teleport permission/free-space order, kick packet, payment or interruption policy.',
                'No learned Flight cast or persisted Flight action is enabled or tested. Existing original-cohort reloads are retained acceptance for those cohorts only.',
                'Modern code parity and failed-landing behavior remain separate. Tail approval is Tail-specific; Flight landing and earlier Claw contact questions remain unanswered and nonblocking.',
                'Final departure/arrival/kick/charge/audio effects and separate talent/hotkey icons remain needed. No installation, native/client/protocol or persistence-schema change.'],
        files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
    path=ROOT/'tools/reference/dragon-flight-preparation-native-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Dragon Flight preparation receipt stale'
    else:path.write_text(encoded)
    print('Dragon Flight preparation:57 focused/273 broader markers,2actual reloads each,53830 source scheduler comparisons per boot;71Python/1026+21audits/573decisions/protected106+38+20+40 pass; learned action remains disabled.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
