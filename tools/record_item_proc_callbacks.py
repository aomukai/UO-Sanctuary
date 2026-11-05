#!/usr/bin/env python3
"""Bind complete source proc callback contracts and actual Blood gate reuse."""
import argparse,hashlib,json,subprocess
from collections import Counter
from pathlib import Path
from record_item_skill_properties import expected as previous_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/item-proc-callbacks';WIDE=ROOT/'.local/class-skills/item-proc-callbacks-broader';SOURCE=ROOT/'.local/class-skills/item-proc-callbacks-source'
PARENT='589b66968d527c4ff67957154a8fd23110d0187e'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))
def expected(broader):
    c=previous_expected(broader);c['item-proc-callback-check']=1;c['item-proc-callback-reload-check']=2;c['blood-life-tap-check']=1;c['blood-life-tap-reload-check']=2;c['blood-life-tap-live-result']=3;return c
def build(check=False):
    native=load(STAGE/'birth-inputs.json');wide=load(WIDE/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
    assert native['artifacts']==wide['artifacts']==audit['artifacts'] and native['copiedData']==wide['copiedData']==audit['copiedData']
    for p in (STAGE/'birth-inputs.json',WIDE/'birth-inputs.json',STAGE/'audit-inputs.json'):
        snapshot(STAGE/'pre-build-1.json',p)
        for source,h in load(p)['sourceInputs'].items():assert sha(ROOT/source)==h,source
    before=load(ROOT/'.local/class-skills/item-skill-properties/pre-build-1.json')['inputs'];now=load(STAGE/'pre-build-1.json')['inputs']
    changed=lambda k:{p for p in before[k].keys()|now[k].keys() if before[k].get(p)!=now[k].get(p)}
    assert changed('sourceInputs')=={'src/Sanctuary.Content/'+n+'.cs' for n in ('ItemProcCallbacks','NpcHitSkills')}|{'tests/Sanctuary.Integration/ItemProcCallbackChecks.cs'}
    assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json'} and before['projectInputs']==now['projectInputs']
    for stage,broader,count in ((STAGE,False,119),(WIDE,True,335)):
        log=(stage/'birth-native-checks.log').read_text();assert markers(stage/'birth-native-checks.log')==expected(broader) and sum(markers(stage/'birth-native-checks.log').values())==count
        assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert log.count('Item affix:41472 complete magic/rare outer results and service traces')==3
        assert log.count('NPC item affixes:22144 current all692base controls/288 weighted-cap controls')==3
        assert log.count('NPC rare names:12456 current/2016 synthetic complete source')==3
        assert log.count('Item property creation:61472 complete writer controls')==3
        assert log.count('Fresh property graphs:24original native items/source LCG compositions')==3
        assert log.count('Item skill properties:72558 complete source controls/7plans/429rows/131NPC specs')==3
        assert log.count('Encoded skill graphs:12original native items')==3
        assert log.count('Item proc callbacks:25920complete gate/decode/LCG/lookup/cast-role/flag/position/return controls')==3
        assert 'Blood Life Tap:720complete proc comparisons' in log
        blood=load(stage/'blood-life-tap-fixtures.json');assert blood['Generation']==2 and blood['Curse']['Source']==blood['Source'] and blood['Curse']['Rank']==5
        assert (stage/'item-proc-callback-fixtures.json').read_text()=='source-callback-contract-only:cast-services-not-spell-delivery'
        assert log.count('4752 complete shared selector controls/510 pinned item-level suppliers')==3
        fresh=load(stage/'item-skill-property-fixtures.json');assert len(fresh)==12 and len({r['Item'] for r in fresh})==12 and all(r['Item']>0 and r['State']['HasProperties'] and not r['State']['Ethereal'] for r in fresh)
        for r in fresh:
            values=r['State']['Properties'];assert {v['Packed']>>16 for v in values}=={195,196,197,198,199,201,204}
        s=load(stage/'shadow-equipment-fixtures.json');assert s['Rank']==17 and len(s['Requests'])==8 and all(r['ItemLevel']==72 for r in s['Requests'])
    for name in ('content-build-1.log','integration-build-1.log'):assert all(x in (STAGE/name).read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):assert sha(STAGE/kind/name)==native['artifacts']['Assemblies/'+name]
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert sha(STAGE/'native/Distribution'/name)==native['artifacts'][name]
    source=load(ROOT/'tools/reference/item-proc-callbacks-source-evidence.json');assert source['cases']==25920 and len(source['completeRoutineHashes'])==5 and not source['currentExecutableParity']
    for k in ('inputs','artifacts'):
        for p,h in source[k].items():assert sha(ROOT/p)==h,p
    catalog=load(ROOT/'data/Sanctuary/Balance/item-skill-properties.json');assert catalog['ChargedLevelBits']==6 and len(catalog['Skills'])==429
    plans=load(ROOT/'data/Sanctuary/Balance/equipment-property-plans.json');assert len(plans)==284 and sum(r['Supported'] for r in plans)==191
    fingerprints={}
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        data=load(STAGE/name);old=historical('tools/reference/'+name);assert data==load(ROOT/'tools/reference'/name)
        fingerprints[name]=len(data['InputHashes'])
        for p,h in data['InputHashes'].items():assert sha(ROOT/p)==h,p
        omit={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'};assert {k:v for k,v in data.items() if k not in omit}=={k:v for k,v in old.items() if k not in omit}
    assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573 and markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    status=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');old=historical('data/Sanctuary/Balance/class-skill-status.json')
    assert {x['talentId'] for x,y in zip(status['skills'],old['skills'],strict=True) if x!=y}=={'assassin.shadowwarrior','assassin.shadowmaster','amazon.valkyrie'}
    assert not status['completedClasses'] and sum(x['implementation']=='handler-present' for x in status['skills'])==194 and sum(x['characterClass']=='Assassin' and x['implementation']=='handler-present' for x in status['skills'])==24
    assert not load(ROOT/'tools/reference/shadow-summons-preflight.json')['handlerEnabled']
    for name,needle in (('source-check.log','Item proc callbacks:25920complete source controls'),('skill-property-source-check.log','Item skill properties:72558writer/12fresh source controls'),('npc-source-check.log','NPC source controls:22432magic/14472rare'),('npc-catalogue-check.log','NPC items: 692 current bases'),('valkyrie-catalogue-check.log','Valkyrie: 18 current bases'),('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):assert needle in (STAGE/name).read_text()
    assert '\nOK\n' in (STAGE/'python-tests.log').read_text() and all(r['exitCode']==0 for r in load(STAGE/'verification-commands.json'))
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative;assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protected[key]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for k in ('patchHashes','sharedInputs'):
        for p,h in client[k].items():assert sha(ROOT/p)==h,p
    files=[Path(__file__).resolve(),*(ROOT/'src/Sanctuary.Content'/(n+'.cs') for n in ('ItemProcCallbacks','NpcHitSkills')),ROOT/'tests/Sanctuary.Integration/ItemProcCallbackChecks.cs',ROOT/'tests/check_item_proc_callbacks.py',ROOT/'tools/probe_item_proc_callbacks.py',ROOT/'tools/build_skill_roadmap.py',
        ROOT/'tools/reference/item-proc-callbacks-source-evidence.json',ROOT/'tools/reference/shadow-summons-preflight.json',
        *(ROOT/'docs'/p for p in ('item-proc-callbacks-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))]
    files.extend(ROOT/p for p in source['artifacts'])
    for stage in (STAGE,WIDE):files.extend(p for p in stage.glob('*') if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
    files.extend((ROOT/'.local/class-skills').glob('item-proc-callbacks-source-*.log'))
    files.extend(p for p in (STAGE/'invocation-1').iterdir() if p.is_file())
    e=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlerEnabled=False,liveShadowActor=False,generatedNpcItems=False,fullNpcStatPropertyEventDelivery=False,
        scope='Complete older proc callback contracts with mandatory cast services;actual Blood gate reused,not generic NPC subscriptions or spell delivery',handlers=194,assassinHandlers=24,completedClasses=[],
        artifacts=native['artifacts'],copiedData=native['copiedData'],focusedMarkers=119,broaderMarkers=335,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
        callbackControlsPerBoot=25920,callbackRoutineCopies=3,sourceSeedRoutineCopies=2,bloodSourceControls=720,actualConnectedBloodChecksPerCohort=3,retainedEncodedWriterControls=72558,retainedEncodedFreshGraphs=12,encodedPlans=7,currentSkillRows=429,blankAddressableSkill=362,originalNativeItems=12,retainedNumericWriterControls=61472,retainedFreshSourceGraphs=24,sourceKernelPlans=202,freshAdmittedPlans=201,originalProjectionPlans=191,retainedSourceOuterControls=41472,retainedCurrentPoolControlsPerBoot=22144,retainedWeightedCapControlsPerBoot=288,retainedCurrentRareNameControlsPerBoot=12456,retainedSyntheticRareNameControlsPerBoot=2016,retainedShadowSelectorControlsPerBoot=4752,retainedShadowRankSuppliersPerBoot=510,
        retainedValkAffixControlsPerBoot=5184,retainedValkEquipmentControlsPerBoot=80,currentExecutableCodeParity=False,
        pythonTests=71,auditFingerprints=fingerprints['named-item-runtime-audit.json'],affixFingerprints=fingerprints['first-loop-affix-audit.json'],namedDecisionsUnchanged=573,
        protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        diagnostics=['Initial runner invocation assumed optional extra_live_commands existed;KeyError occurred before any native process launched. Script/log/snapshot/builtDLLs preserved under invocation-1;focused2uses identical frozen binaries/data.'],
        limits=['Callbacks20/21/30match complete older source with declared stat/bit/skill-flag/path/target/position cast services. Callback1reports an attempted route regardless of nested cast failure;it is not proof of delivered spell.',
                'Shared outgoing chance/hit gate is used by actual Blood Life Tap. Native hard19/soft1refusal/train20/respec,zero mana,first-hit/overkill,zero/absorbed contact,uninterruptible,curse arbitration,owner credit,connected casting and original saved curse checks retain.',
                'Generic NPC event subscriptions/trigger order,full target/position item-effect casts,NPC spell contexts,charge actions/spending/recharge,full item initialization/fallback and generated NPC actor delivery remain open. Cast attempts are not silent placeholder successes.',
                'Ordinary skilltab and war-tab-rand questions,current executable parity,do49/AI/actor/cap/casting/lifecycle/input and final assets remain unfinished. No new learned summon or completed class.',
                'No deployment,native/client/protocol/persistence-schema change. Installed artifacts,saves,configuration/client are unchanged. Effects/audio and talent/hotkey icons remain separate identified placeholders.'],
        files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
    out=ROOT/'tools/reference/item-proc-callbacks-native-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Item proc callback receipt stale'
    else:out.write_text(encoded)
    print('Item proc callbacks:119focused/335broader markers,2reloads each;25920source controls and actual Blood gate/curse/input/source-removal coverage;71Python/audits/protected pass;generic casts remain services.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
