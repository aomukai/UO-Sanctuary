#!/usr/bin/env python3
"""Bind raw proc/charge encoding without certifying actor consumers or full NPC items."""
import argparse,hashlib,json,subprocess
from collections import Counter
from pathlib import Path
from record_item_property_creation import expected as previous_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/item-skill-properties';WIDE=ROOT/'.local/class-skills/item-skill-properties-broader';SOURCE=ROOT/'.local/class-skills/item-skill-property-source'
PARENT='878854d5b1f4e5def32c3e34ceb0121c8f73f95f'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))
def expected(broader):
    c=previous_expected(broader);c['item-skill-property-check']=1;c['item-skill-property-reload-check']=2;return c
def build(check=False):
    native=load(STAGE/'birth-inputs.json');wide=load(WIDE/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
    assert native['artifacts']==wide['artifacts']==audit['artifacts'] and native['copiedData']==wide['copiedData']==audit['copiedData']
    for p in (STAGE/'birth-inputs.json',WIDE/'birth-inputs.json',STAGE/'audit-inputs.json'):
        snapshot(STAGE/'pre-build-1.json',p)
        for source,h in load(p)['sourceInputs'].items():assert sha(ROOT/source)==h,source
    before=load(ROOT/'.local/class-skills/item-property-creation/pre-build-1.json')['inputs'];now=load(STAGE/'pre-build-1.json')['inputs']
    changed=lambda k:{p for p in before[k].keys()|now[k].keys() if before[k].get(p)!=now[k].get(p)}
    assert changed('sourceInputs')=={'src/Sanctuary.Content/'+n+'.cs' for n in ('EquipmentPropertyWriter','ItemPropertyCreationContext','ItemSkillPropertyWriter')}|{'tests/Sanctuary.Integration/ItemSkillPropertyChecks.cs'}
    assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/item-skill-properties.json'} and before['projectInputs']==now['projectInputs']
    for stage,broader,count in ((STAGE,False,110),(WIDE,True,326)):
        log=(stage/'birth-native-checks.log').read_text();assert markers(stage/'birth-native-checks.log')==expected(broader) and sum(markers(stage/'birth-native-checks.log').values())==count
        assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert log.count('Item affix:41472 complete magic/rare outer results and service traces')==3
        assert log.count('NPC item affixes:22144 current all692base controls/288 weighted-cap controls')==3
        assert log.count('NPC rare names:12456 current/2016 synthetic complete source')==3
        assert log.count('Item property creation:61472 complete writer controls')==3
        assert log.count('Fresh property graphs:24original native items/source LCG compositions')==3
        assert log.count('Item skill properties:72558 complete source controls/7plans/429rows/131NPC specs')==3
        assert log.count('Encoded skill graphs:12original native items')==3
        assert log.count('4752 complete shared selector controls/510 pinned item-level suppliers')==3
        fresh=load(stage/'item-skill-property-fixtures.json');assert len(fresh)==12 and len({r['Item'] for r in fresh})==12 and all(r['Item']>0 and r['State']['HasProperties'] and not r['State']['Ethereal'] for r in fresh)
        for r in fresh:
            values=r['State']['Properties'];assert {v['Packed']>>16 for v in values}=={195,196,197,198,199,201,204}
        s=load(stage/'shadow-equipment-fixtures.json');assert s['Rank']==17 and len(s['Requests'])==8 and all(r['ItemLevel']==72 for r in s['Requests'])
    for name in ('content-build-1.log','integration-build-1.log'):assert all(x in (STAGE/name).read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):assert sha(STAGE/kind/name)==native['artifacts']['Assemblies/'+name]
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert sha(STAGE/'native/Distribution'/name)==native['artifacts'][name]
    source=load(ROOT/'tools/reference/item-skill-property-source-evidence.json');assert source['cases']==dict(current=36036,npc=3930,synthetic=32256,missing=336,fresh=12) and source['totalCases']==72570 and source['plans']==7 and source['currentSkillRows']==429 and source['blankAddressableSkill']==362 and source['chargedLevelBits']==6 and not source['currentExecutableParity'] and len(source['newCompleteRoutineHashes'])==5 and len(source['inheritedCompleteRoutineHashes'])==20
    for k in ('inputs','artifacts'):
        for p,h in source[k].items():assert sha(ROOT/p)==h,p
    original=load(ROOT/'tools/reference/item-property-creation-source-evidence.json');assert source['inheritedCompleteRoutineHashes']==original['completeRoutineHashes']
    catalog=load(ROOT/'data/Sanctuary/Balance/item-skill-properties.json');assert catalog['ChargedLevelBits']==6 and len(catalog['Skills'])==429 and [r['Id'] for r in catalog['Skills'] if not r['Name']]==[362]
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
    for name,needle in (('source-check.log','Item skill properties:72558writer/12fresh source controls'),('skill-metadata-check.log','Item skill properties:429current rows including blank362'),('npc-source-check.log','NPC source controls:22432magic/14472rare'),('npc-catalogue-check.log','NPC items: 692 current bases'),('valkyrie-catalogue-check.log','Valkyrie: 18 current bases'),('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):assert needle in (STAGE/name).read_text()
    assert '\nOK\n' in (STAGE/'python-tests.log').read_text() and all(r['exitCode']==0 for r in load(STAGE/'verification-commands.json'))
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative;assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protected[key]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for k in ('patchHashes','sharedInputs'):
        for p,h in client[k].items():assert sha(ROOT/p)==h,p
    files=[Path(__file__).resolve(),*(ROOT/'src/Sanctuary.Content'/(n+'.cs') for n in ('EquipmentPropertyWriter','ItemPropertyCreationContext','ItemSkillPropertyWriter')),ROOT/'tests/Sanctuary.Integration/ItemSkillPropertyChecks.cs',ROOT/'tests/check_item_skill_properties.py',ROOT/'tools/probe_item_skill_properties.py',ROOT/'tools/build_item_skill_properties.py',ROOT/'tools/build_skill_roadmap.py',
        ROOT/'tools/reference/item-skill-property-source-evidence.json',ROOT/'tools/reference/shadow-summons-preflight.json',
        *(ROOT/'docs'/p for p in ('item-skill-property-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))]
    files.extend(ROOT/p for p in source['artifacts'])
    for stage in (STAGE,WIDE):files.extend(p for p in stage.glob('*') if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
    for name in ('metadata-1','initial-72558'):files.extend(p for p in (SOURCE/name).iterdir() if p.is_file())
    files.extend((ROOT/'.local/class-skills').glob('item-skill-property-source-*.log'))
    e=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlerEnabled=False,liveShadowActor=False,generatedNpcItems=False,fullNpcStatPropertyEventDelivery=False,
        scope='Raw proc/charge encoding on item-owned graphs;not actor subscriptions,triggers,casting,spending/recharge or full item/NPC creation',handlers=194,assassinHandlers=24,completedClasses=[],
        artifacts=native['artifacts'],copiedData=native['copiedData'],focusedMarkers=110,broaderMarkers=326,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
        encodedWriterControlsPerBoot=72558,encodedFreshGraphsPerBoot=12,newRoutineCopies=5,inheritedRoutineCopies=20,encodedPlans=7,currentSkillRows=429,blankAddressableSkill=362,originalNativeItems=12,retainedNumericWriterControls=61472,retainedFreshSourceGraphs=24,sourceKernelPlans=202,freshAdmittedPlans=201,originalProjectionPlans=191,retainedSourceOuterControls=41472,retainedCurrentPoolControlsPerBoot=22144,retainedWeightedCapControlsPerBoot=288,retainedCurrentRareNameControlsPerBoot=12456,retainedSyntheticRareNameControlsPerBoot=2016,retainedShadowSelectorControlsPerBoot=4752,retainedShadowRankSuppliersPerBoot=510,
        retainedValkAffixControlsPerBoot=5184,retainedValkEquipmentControlsPerBoot=80,currentExecutableCodeParity=False,
        pythonTests=71,auditFingerprints=fingerprints['named-item-runtime-audit.json'],affixFingerprints=fingerprints['first-loop-affix-audit.json'],namedDecisionsUnchanged=573,
        protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        diagnostics=['Initial metadata builder assumed every current skill row had a name;blank addressable row362 caused KeyError. Failed producer/log retained under metadata-1 before retaining the exact blank record. No source row is fabricated or discarded.',
                     'Accepted72,558case source package retained under initial-72558 before adding12fresh graph/seed continuation controls. No copied engine routine changed.'],
        limits=['Seven encoded skill-property plans use a separate explicit API;numeric kernel202,fresh numeric admission201and original projection191remain unchanged. Ordinary skilltab adaptation and modern war-tab-rand remain unresolved.',
                'Five new complete source routines plus20inherited copies use declared valid-item/base/immutable metadata/allocation services and complete source LCG. Loader bit-context is statically reviewed;modern executable parity remains unverified.',
                '12real native items verify raw proc accumulation,charge overwrite,missing-record no-op,source seed and snapshot continuation. No actual actor event subscriptions,proc trigger/spell delivery,charge-backed skills,charge spending/recharge,full item initialization/fallback or generated NPC graph is certified.',
                'NpcCombatEvents active-proc refusal remains enforced. No new learned summon or completed class;full Shadow/Valk body/cap/AI/casting/lifecycle/input and actor saves remain pending.',
                'No deployment,native/client/protocol/persistence-schema change. Installed artifacts,saves,configuration/client are unchanged. Effects/audio and talent/hotkey icons remain separate identified placeholders.'],
        files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
    out=ROOT/'tools/reference/item-skill-property-native-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Item skill-property receipt stale'
    else:out.write_text(encoded)
    print('Encoded skill properties:110focused/326broader markers,2reloads each;72558writer/12source-LCG graphs;7plans/429rows;71Python/audits/protected pass;actor dispatch gate retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
