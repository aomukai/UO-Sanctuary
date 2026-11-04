#!/usr/bin/env python3
"""Bind learned Tail, copied-area knockback, source geometry and original saves."""
import argparse
from collections import Counter
import hashlib,json,subprocess,sys
from pathlib import Path
from snapshot_skill_build import check as snapshot
from record_poison_dagger_contact import markers
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/dragon-tail-learned';BROADER=ROOT/'.local/class-skills/dragon-tail-learned-broader'
PARENT='5a5096350372ede9f105fd01f35554841e12beb4'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))
def expected(broader):
    if broader:
        from record_wake_learned import expected as wake
        result=wake()
    else:result=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for f in ('dragon-tail-preparation','dragon-kick','kick-packet','tiger-strike','dragon-tail'):
        result[f+'-check']=1;result[f+'-reload-check']=2
    for c in ('dragon-tail-mechanics-check','melee-geometry-check'):result[c]=3
    for mode in ('left','right','quick'):
        result['dragon-tail-'+mode+'-check']=3;result['dragon-tail-'+mode+'-result']=3
    return result
def build(check=False):
    native=load(STAGE/'birth-inputs.json');wide=load(BROADER/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
    assert native['artifacts']==wide['artifacts']==audit['artifacts']and native['copiedData']==wide['copiedData']==audit['copiedData']
    for p in (STAGE/'birth-inputs.json',BROADER/'birth-inputs.json',STAGE/'audit-inputs.json'):snapshot(STAGE/'pre-build-8.json',p)
    before=load(STAGE/'pre-build-7.json')['inputs'];current=load(STAGE/'pre-build-8.json')['inputs']
    assert {p for p in before['sourceInputs']if before['sourceInputs'][p]!=current['sourceInputs'][p]}=={'tests/Sanctuary.Integration/DragonTailChecks.cs'}
    assert before['copiedData']==current['copiedData']and before['projectInputs']==current['projectInputs']
    production={'src/Sanctuary.Content/'+n+'.cs'for n in ('ActionTiming','AssassinCombat','ClassCombat','DragonKickPackets','DragonTails','FistsFireRelease','HitRecovery','MartialCold','MartialLightning','PlayerMeleeGeometry','PlayerWeaponContact','ReviveMeleeGeometry','SkillCombat','SourceMeleeGeometry')}
    tests={'tests/Sanctuary.Integration/'+n+'.cs'for n in ('DragonKickChecks','DragonTailChecks','DragonTailPreparationChecks','KickPacketChecks','MeleeGeometryChecks')}
    old=load(ROOT/'.local/class-skills/dragon-tail-preparation/pre-build-4.json')['inputs']
    assert {p for p in set(old['sourceInputs'])|set(current['sourceInputs'])if old['sourceInputs'].get(p)!=current['sourceInputs'].get(p)}==production|tests
    assert old['projectInputs']==current['projectInputs']
    assert {p for p in set(old['copiedData'])|set(current['copiedData'])if old['copiedData'].get(p)!=current['copiedData'].get(p)}=={'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/player-melee-ranges.json'}
    parent=historical('tools/reference/dragon-tail-preparation-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==parent['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    for stage,count,broad in ((STAGE,48,False),(BROADER,264,True)):
        assert markers(stage/'birth-native-checks.log')==expected(broad)and sum(expected(broad).values())==count
        log=(stage/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log
        assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
        saved=load(stage/'dragon-tail-fixtures.json');assert saved['Charge']['Owner']==saved['Owner']and saved['Charge']['Count']==2
        assert saved['Motion']['Target']==saved['Target']and saved['Motion']['PauseFrozen']and saved['Motion']['Travel16']==saved['Motion']['Budget16']==0
        assert saved['Mana']%256==17 and saved['Health']>0 and saved['PendingMana']>0
    # Diagnostics are bound to the actual tested binaries; no failed run is a pass.
    for attempt,reason in ((1,'Hit recovery retained queued Tail'),(2,'Collection was modified'),(4,'Sequence contains no matching element')):
        folder=STAGE/f'attempt-{attempt}';inputs=load(folder/'birth-inputs.json')
        for kind in ('content','integration'):
            name='Sanctuary.'+('Content'if kind=='content'else'Integration')+'.dll'
            assert sha(folder/kind/name)==inputs['artifacts']['Assemblies/'+name]
        assert reason in(folder/'birth-native-checks.log').read_text()
    assert 'error CS0411'in(STAGE/'content-build-4.log').read_text()and '2377 Error(s)'in(STAGE/'integration-build-3.log').read_text()
    for name in ('content-build-7.log','integration-build-6.log'):
        assert all(x in(STAGE/name).read_text()for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    previous=STAGE/'verified-before-knockback';old_inputs=load(previous/'birth-inputs.json')
    assert sum(markers(previous/'birth-native-checks.log').values())==48 and sum(markers(BROADER/'before-knockback/birth-native-checks.log').values())==264
    for kind in ('content','integration'):
        name='Sanctuary.'+('Content'if kind=='content'else'Integration')+'.dll';assert sha(previous/kind/name)==old_inputs['artifacts']['Assemblies/'+name]
    for name,count in (('named-item-runtime-audit.json',1025),('first-loop-affix-audit.json',21)):
        data=load(STAGE/name);prior=historical('tools/reference/'+name);assert data==load(ROOT/'tools/reference'/name)and len(data['InputHashes'])==count
        for p,h in data['InputHashes'].items():assert sha(ROOT/p)==h,p
        skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in data.items()if k not in skip}=={k:v for k,v in prior.items()if k not in skip}
    assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573
    assert markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    status=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');prior=historical('data/Sanctuary/Balance/class-skill-status.json')
    assert {x['talentId']for x,y in zip(status['skills'],prior['skills'],strict=True)if x!=y}=={'assassin.dragontail'}
    assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==194
    assert sum(r['characterClass']=='Assassin'and r['implementation']=='handler-present'for r in status['skills'])==24
    geometry=load(ROOT/'tools/reference/player-melee-geometry-source-evidence.json');assert geometry['weaponRows']==306 and geometry['rangeContexts']==68040 and geometry['itemContexts']==1224
    assert sha(ROOT/geometry['runtimePath'])==geometry['runtimeSha256']
    for p,h in geometry['artifacts'].items():assert sha(ROOT/p)==h,p
    burst=load(ROOT/'tools/reference/dragon-tail-burst-source-evidence.json');assert burst['cases']==24 and burst['resultFlags']['do50']==9 and burst['resultFlags']['admittedNpc']==13
    for p,h in burst['artifacts'].items():assert sha(ROOT/p)==h,p
    for e in (geometry,burst):
        for p,h in e['inputs'].items():assert sha(ROOT/p)==h,p
    decision=load(ROOT/'tools/reference/dragon-tail-sanctuary-decision.json');assert not decision['isD2RObservation']and decision['decision']=='approved Sanctuary adaptation'
    preflight=load(ROOT/'tools/reference/dragon-tail-preflight.json');assert preflight['handlerEnabled']and not preflight['installed']
    for name,text in (('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):assert text in(STAGE/name).read_text()
    assert '\nOK\n'in(STAGE/'python-tests.log').read_text()
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative;assert len(protected[key])==count
        assert {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for section in ('patchHashes','sharedInputs'):
        for p,h in client[section].items():assert sha(ROOT/p)==h,p
    files=[Path(__file__).resolve(),*(ROOT/p for p in production|tests),
        *(ROOT/'tools'/p for p in ('build_player_melee_reference.py','prepare_dragon_tail_action_reference.py','probe_dragon_tail_burst_reference.py','prepare_dragon_tail_reference.py','build_skill_roadmap.py')),
        ROOT/'tests/check_dragon_tail.py',ROOT/'tests/check_dragon_tail_preparation.py',
        *(ROOT/'tools/reference'/p for p in ('player-melee-geometry-source-evidence.json','dragon-tail-action-source-evidence.json','dragon-tail-burst-source-evidence.json','dragon-tail-preflight.json','dragon-tail-sanctuary-decision.json')),
        *(ROOT/'docs'/p for p in ('dragon-tail-learned-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))]
    for e in (geometry,burst):files.extend(ROOT/p for p in e['artifacts'])
    for stage in (STAGE,BROADER):
        files.extend(p for p in stage.rglob('*')if p.is_file()and not p.is_symlink()and p.suffix in ('.json','.log')and p.name!='execution-state.json'and not any(x in ('content','integration','native','upstream')for x in p.relative_to(stage).parts))
    evidence=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',installed=False,handlerEnabled=True,handlers=194,assassinHandlers=24,completedClasses=[],artifacts=native['artifacts'],copiedData=native['copiedData'],
        scope='Learned targeted Tail and bounded native source composition; current executable/world/client and full class completion unclaimed',
        verified=dict(contentBuild=7,integrationBuild=6,focusedRun=5,broaderRun=2,auditRun=2,focusedMarkers=48,broaderMarkers=264,bootsPerRun=3,reloadsPerRun=2,savesPerRun=3,playerWeaponRows=306,itemGetterCases=1224,playerRangeCases=68040,copiedRouterCases=24,pythonTests=71,namedDefinitions=573),
        diagnostics=['Runtime attempts1/2 caught missing recovery cleanup and live area enumeration;both are fixed. Failed content4/integration3 are not passes.','Earlier48/264 green runs omitted burst knockback coverage. Final copied-area flags9/13 consumer and movement/immunity/non-melee/held-motion tests replace that weaker acceptance.','Attempt4 caught an expired inspection clock on reload; the fixture holds its deadline/reapplies freeze. Production movement/expiry is unchanged.'],
        remaining=preflight['openMechanics'],protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        evidence={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))})
    path=ROOT/'tools/reference/dragon-tail-learned-native-evidence.json';text=json.dumps(evidence,indent=2)+'\n'
    if check:assert path.read_text()==text,'Learned Tail receipt stale'
    else:path.write_text(text)
    print('Learned Dragon Tail:48/264 native markers,3boots/2reloads,306weapon/68040geometry/24copied-router controls,71Python and unchanged573decisions/protected installation verified;194handlers,no complete class.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
