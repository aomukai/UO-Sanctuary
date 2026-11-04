#!/usr/bin/env python3
"""Bind shared Shadow/Valk requests without certifying generated NPC items or AI."""
import argparse,hashlib,json,subprocess
from collections import Counter
from pathlib import Path
from record_dragon_flight_landing import expected as previous_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/shadow-equipment';WIDE=ROOT/'.local/class-skills/shadow-equipment-broader';SOURCE=ROOT/'.local/class-skills/shadow-equipment-source'
PARENT='ceb801b8c42ec733855e2ab6b1b5d24f77a39a0e'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))
def expected(broader):
    c=previous_expected(broader)
    for f in ('shadow-equipment','valkyrie-affix'):c[f+'-check']=1;c[f+'-reload-check']=2
    return c
def build(check=False):
    native=load(STAGE/'birth-inputs.json');wide=load(WIDE/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
    assert native['artifacts']==wide['artifacts']==audit['artifacts'] and native['copiedData']==wide['copiedData']==audit['copiedData']
    for p in (STAGE/'birth-inputs.json',WIDE/'birth-inputs.json',STAGE/'audit-inputs.json'):
        snapshot(STAGE/'pre-build-1.json',p)
        for source,h in load(p)['sourceInputs'].items():assert sha(ROOT/source)==h,source
    before=load(ROOT/'.local/class-skills/dragon-flight-landing/pre-build-1.json')['inputs'];now=load(STAGE/'pre-build-1.json')['inputs']
    changed=lambda k:{p for p in before[k].keys()|now[k].keys() if before[k].get(p)!=now[k].get(p)}
    production={'src/Sanctuary.Content/'+n+'.cs' for n in ('SummonEquipment','ShadowEquipment','ValkyrieAffixes')}
    assert changed('sourceInputs')==production|{'tests/Sanctuary.Integration/ShadowEquipmentChecks.cs'}
    assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/shadow-equipment.json'} and before['projectInputs']==now['projectInputs']
    for stage,broader,count in ((STAGE,False,98),(WIDE,True,314)):
        log=(stage/'birth-native-checks.log').read_text();assert markers(stage/'birth-native-checks.log')==expected(broader) and sum(markers(stage/'birth-native-checks.log').values())==count
        assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert log.count('4752 complete shared selector controls/510 pinned item-level suppliers')==3
        s=load(stage/'shadow-equipment-fixtures.json');assert s['Owner']>0 and s['Helm']>0 and s['Armor']>0 and s['Rank']==17 and len(s['Requests'])==8 and s['Seed']>0
        assert all(r['ItemLevel']==72 for r in s['Requests'])
    for name in ('content-build-1.log','integration-build-1.log'):assert all(x in (STAGE/name).read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):assert sha(STAGE/kind/name)==native['artifacts']['Assemblies/'+name]
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert sha(STAGE/'native/Distribution'/name)==native['artifacts'][name]
    source=load(ROOT/'tools/reference/shadow-equipment-source-evidence.json');assert source['cases']==4752 and source['actualProfiles']==2 and source['syntheticProfiles']==1 and not source['handlerEnabled'] and not source['currentExecutableCodeParity']
    assert source['routineSha256']==load(ROOT/'tools/reference/valkyrie-equipment-engine-evidence.json')['blockHashes']['sub_6FCF9580']
    for k in ('inputs','artifacts'):
        for p,h in source[k].items():assert sha(ROOT/p)==h,p
    assert sha(ROOT/source['runtimePath'])==source['runtimeSha256']
    profiles=load(ROOT/source['runtimePath']);assert [len(p['Rows']) for p in profiles]==[4,19]
    for name,count in (('named-item-runtime-audit.json',1032),('first-loop-affix-audit.json',21)):
        data=load(STAGE/name);old=historical('tools/reference/'+name);assert data==load(ROOT/'tools/reference'/name) and len(data['InputHashes'])==count
        for p,h in data['InputHashes'].items():assert sha(ROOT/p)==h,p
        omit={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'};assert {k:v for k,v in data.items() if k not in omit}=={k:v for k,v in old.items() if k not in omit}
    assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573 and markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    status=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');old=historical('data/Sanctuary/Balance/class-skill-status.json')
    assert {x['talentId'] for x,y in zip(status['skills'],old['skills'],strict=True) if x!=y}=={'assassin.shadowwarrior','assassin.shadowmaster'}
    assert not status['completedClasses'] and sum(x['implementation']=='handler-present' for x in status['skills'])==194 and sum(x['characterClass']=='Assassin' and x['implementation']=='handler-present' for x in status['skills'])==24
    assert not load(ROOT/'tools/reference/shadow-summons-preflight.json')['handlerEnabled']
    for name,needle in (('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):assert needle in (STAGE/name).read_text()
    assert '\nOK\n' in (STAGE/'python-tests.log').read_text() and all(r['exitCode']==0 for r in load(STAGE/'verification-commands.json'))
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative;assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protected[key]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for k in ('patchHashes','sharedInputs'):
        for p,h in client[k].items():assert sha(ROOT/p)==h,p
    initial=load(SOURCE/'initial-case-pass/source-evidence.json');assert initial['routineSha256']==source['routineSha256']
    files=[Path(__file__).resolve(),*(ROOT/p for p in production),ROOT/'tests/Sanctuary.Integration/ShadowEquipmentChecks.cs',ROOT/'tests/check_shadow_equipment.py',ROOT/'tools/probe_shadow_equipment_reference.py',ROOT/'tools/build_skill_roadmap.py',
        *(ROOT/'tools/reference'/p for p in ('shadow-equipment-source-evidence.json','shadow-summons-preflight.json','creeper-frame-installation-evidence.json')),
        *(ROOT/'docs'/p for p in ('shadow-equipment-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))]
    files.extend(ROOT/p for p in source['artifacts'])
    for stage in (STAGE,WIDE):files.extend(p for p in stage.glob('*') if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
    files.extend(p for p in (SOURCE/'initial-case-pass').iterdir() if p.is_file())
    e=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlerEnabled=False,liveShadowActor=False,generatedNpcItems=False,fullNpcStatPropertyEventDelivery=False,
        scope='Prepared shared Shadow/Valk equipment request selection;not generated items,live AI or learned Shadows',handlers=194,assassinHandlers=24,completedClasses=[],
        artifacts=native['artifacts'],copiedData=native['copiedData'],focusedMarkers=98,broaderMarkers=314,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
        sourceSelectorControls=4752,nativeSelectorControlsPerBoot=4752,nativeRankSuppliersPerBoot=510,shadowRows=[4,19],retainedValkAffixControlsPerBoot=5184,retainedValkEquipmentControlsPerBoot=80,
        originalOwnerItems=2,originalEffectiveShadowRank=17,originalItemLevel=72,originalEquipmentRequests=8,currentExecutableCodeParity=False,
        pythonTests=71,auditFingerprints=1032,affixFingerprints=21,namedDecisionsUnchanged=573,protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        diagnostics=['Initial scalar fixture supplied positive-rank level formula at rank0;corrected to reviewed caller fallback1before any native build. Original source-case package preserved;selector body unchanged.'],
        limits=['Complete source selector uses declared bounded-choice and item-creation/equip services,not a full random item/property/event generator. No source properties are filtered away.',
                'Item-level caller formula/current23rows are pinned metadata/static caller evidence,not full do49life/stat/passive execution. Literal older calc[1]and current extended-passive discrepancy remain unresolved.',
                'Original native owner/gear/rank/request plan/seed reloads do not certify generated NPC items,Shadow ownership/shared cap/dual-claw combat/AI105or106/143/NPC skill contexts/casting/input or live Shadow saves.',
                'Existing NPC numeric suppliers/frozen Revive graphs remain bounded reuse components;full generated stat/property/event transport is unfinished. No learned Shadow handler enabled.',
                'Shadow body/summon/cast/weapon/skill effects/audio and separate talent/hotkey icons remain needed. No deployment,native/client/protocol/persistence-schema change.'],files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
    out=ROOT/'tools/reference/shadow-equipment-native-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Shadow equipment receipt stale'
    else:out.write_text(encoded)
    print('Shadow equipment:98focused/314broader markers,2reloads each;4752selector/510rank controls per boot;original gear/rank/plan/seed and Valkyrie retain;71Python/1032+21audits/573decisions/protected106+38+20+40 pass;no live Shadow.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
