#!/usr/bin/env python3
"""Bind fresh item-owned numeric/layered graphs without certifying full NPC items."""
import argparse,hashlib,json,subprocess
from collections import Counter
from pathlib import Path
from record_npc_item_affixes import expected as previous_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/item-property-creation';WIDE=ROOT/'.local/class-skills/item-property-creation-broader';SOURCE=ROOT/'.local/class-skills/item-property-creation-source'
PARENT='9e8e8125e37dcd89b43379e39e0226003bf82bf6'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))
def expected(broader):
    c=previous_expected(broader);c['item-property-creation-check']=1;c['item-property-creation-reload-check']=2;return c
def build(check=False):
    native=load(STAGE/'birth-inputs.json');wide=load(WIDE/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
    assert native['artifacts']==wide['artifacts']==audit['artifacts'] and native['copiedData']==wide['copiedData']==audit['copiedData']
    for p in (STAGE/'birth-inputs.json',WIDE/'birth-inputs.json',STAGE/'audit-inputs.json'):
        snapshot(STAGE/'pre-build-1.json',p)
        for source,h in load(p)['sourceInputs'].items():assert sha(ROOT/source)==h,source
    before=load(ROOT/'.local/class-skills/npc-item-affixes/pre-build-1.json')['inputs'];now=load(STAGE/'pre-build-1.json')['inputs']
    changed=lambda k:{p for p in before[k].keys()|now[k].keys() if before[k].get(p)!=now[k].get(p)}
    assert changed('sourceInputs')=={'src/Sanctuary.Content/'+n+'.cs' for n in ('EquipmentPropertyWriter','ItemPropertyCreationContext')}|{'tests/Sanctuary.Integration/'+n+'.cs' for n in ('EquipmentPropertyWriterChecks','ItemPropertyCreationChecks')}
    assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json'} and before['projectInputs']==now['projectInputs']
    for stage,broader,count in ((STAGE,False,107),(WIDE,True,323)):
        log=(stage/'birth-native-checks.log').read_text();assert markers(stage/'birth-native-checks.log')==expected(broader) and sum(markers(stage/'birth-native-checks.log').values())==count
        assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert log.count('Item affix:41472 complete magic/rare outer results and service traces')==3
        assert log.count('NPC item affixes:22144 current all692base controls/288 weighted-cap controls')==3
        assert log.count('NPC rare names:12456 current/2016 synthetic complete source')==3
        assert log.count('Item property creation:61472 complete writer controls')==3
        assert log.count('Fresh property graphs:24original native items/source LCG compositions')==3
        assert log.count('4752 complete shared selector controls/510 pinned item-level suppliers')==3
        fresh=load(stage/'item-property-creation-fixtures.json');assert len(fresh)==24 and len({r['Item'] for r in fresh})==24 and all(r['Item']>0 and r['State']['HasProperties'] for r in fresh) and sum(r['State']['Ethereal'] for r in fresh)==12
        for r in fresh:
            values={v['Packed']:v['Value'] for v in r['State']['Properties']};assert all(1<=values[(83<<16)|k]<=3 for k in range(8)) and values[152<<16]==1
        s=load(stage/'shadow-equipment-fixtures.json');assert s['Rank']==17 and len(s['Requests'])==8 and all(r['ItemLevel']==72 for r in s['Requests'])
    for name in ('content-build-1.log','integration-build-1.log'):assert all(x in (STAGE/name).read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):assert sha(STAGE/kind/name)==native['artifacts']['Assemblies/'+name]
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert sha(STAGE/'native/Distribution'/name)==native['artifacts'][name]
    source=load(ROOT/'tools/reference/item-property-creation-source-evidence.json');assert source['totalCases']==61472 and source['freshGraphCases']==24 and source['creationPlans']==202 and source['freshAdmittedPlans']==201 and source['originalProjectionPlans']==191 and not source['currentExecutableParity'] and len(source['completeRoutineHashes'])==20
    for k in ('inputs','artifacts'):
        for p,h in source[k].items():assert sha(ROOT/p)==h,p
    original=load(ROOT/'tools/reference/item-property-writers-source-evidence.json')
    assert all(source['completeRoutineHashes'][n]==h for n,h in original['compiledUnchangedRoutineHashes'].items())
    assert not source['skillTabReaderStaticReview']['executed'] and source['skillTabReaderStaticReview']['currentMeaningUnverified']
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
    for name,needle in (('source-check.log','Item property creation:202kernel/201fresh plans'),('npc-source-check.log','NPC source controls:22432magic/14472rare'),('npc-catalogue-check.log','NPC items: 692 current bases'),('valkyrie-catalogue-check.log','Valkyrie: 18 current bases'),('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):assert needle in (STAGE/name).read_text()
    assert '\nOK\n' in (STAGE/'python-tests.log').read_text() and all(r['exitCode']==0 for r in load(STAGE/'verification-commands.json'))
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative;assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protected[key]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for k in ('patchHashes','sharedInputs'):
        for p,h in client[k].items():assert sha(ROOT/p)==h,p
    files=[Path(__file__).resolve(),*(ROOT/'src/Sanctuary.Content'/(n+'.cs') for n in ('EquipmentPropertyWriter','ItemPropertyCreationContext')),*(ROOT/'tests/Sanctuary.Integration'/(n+'.cs') for n in ('EquipmentPropertyWriterChecks','ItemPropertyCreationChecks')),ROOT/'tests/check_item_property_creation.py',ROOT/'tools/probe_item_property_creation.py',ROOT/'tools/build_skill_roadmap.py',
        ROOT/'tools/reference/item-property-creation-source-evidence.json',ROOT/'tools/reference/shadow-summons-preflight.json',
        *(ROOT/'docs'/p for p in ('item-property-creation-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))]
    files.extend(ROOT/p for p in source['artifacts'])
    for stage in (STAGE,WIDE):files.extend(p for p in stage.glob('*') if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
    for name in ('compile-1','initial-61472'):files.extend(p for p in (SOURCE/name).iterdir() if p.is_file())
    files.extend((ROOT/'.local/class-skills').glob('item-property-creation-source-*.log'))
    e=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlerEnabled=False,liveShadowActor=False,generatedNpcItems=False,fullNpcStatPropertyEventDelivery=False,
        scope='Fresh item-owned raw numeric/layered property graph and source LCG continuation;not complete initialization,NPC item factory,AI or learned summons',handlers=194,assassinHandlers=24,completedClasses=[],
        artifacts=native['artifacts'],copiedData=native['copiedData'],focusedMarkers=107,broaderMarkers=323,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
        sourceWriterControlsPerBoot=61472,freshSourceGraphsPerBoot=24,sourceRoutineCopies=20,sourceKernelPlans=202,freshAdmittedPlans=201,originalProjectionPlans=191,originalNativeItems=24,retainedSourceOuterControls=41472,retainedCurrentPoolControlsPerBoot=22144,retainedWeightedCapControlsPerBoot=288,retainedCurrentRareNameControlsPerBoot=12456,retainedSyntheticRareNameControlsPerBoot=2016,retainedShadowSelectorControlsPerBoot=4752,retainedShadowRankSuppliersPerBoot=510,
        retainedValkAffixControlsPerBoot=5184,retainedValkEquipmentControlsPerBoot=80,currentExecutableCodeParity=False,
        pythonTests=71,auditFingerprints=fingerprints['named-item-runtime-audit.json'],affixFingerprints=fingerprints['first-loop-affix-audit.json'],namedDecisionsUnchanged=573,
        protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        diagnostics=['Old ignored property-writer source/log/diagnostic files are absent in this worktree and installed-root copy. No historical evidence is fabricated;the accepted45,840case fixture,284plans,original generator header and15routine hashes are explicitly inherited.',
                     'Initial fresh-source compile required FALSE in the declared fixture ABI;failed source/compiler log/script preserved under compile-1. Copied engine bodies unchanged.',
                     'Initial supplementary formula text grouped division differently from the copied literal oldfunc10;source-only formula corrected and fresh skilltab admission refused before native builds. Initial61,472case source package retained;an optional user adaptation question remains pending.',
                     'Source compiler warns only about the declared i64literal helper needed to parse unchanged old seed expressions;ASan/UBSan runtime stderr is empty.'],
        limits=['Creation kernel admits202plans;fresh context admits201and original saved-item projection remains191. Ordinary skilltab and war-tab-rand modern producer meaning stay unresolved;fresh application refuses them without changing graph or seed.',
                '20complete routine copies use declared item/base/stat/property/allocation services. Numeric range/writer controls use a counter stream;24fresh compositions execute complete source LCG functions and verify actual native graph raw values/flags and exact continued seeds after snapshots/reloads.',
                '24real native items are source-context fixtures with supplied initial bases/body/time/class/change services. Raw ethereal state is captured in the context;no complete ClassGear/native item materialization,initialization,quality fallback or global seed allocation is certified.',
                'Procs,charges,sockets,remaining specialized properties,full NPC stat/property/event/skill delivery and actual Shadow/Valk actor/cap/AI/casting/lifecycle/input/saves remain unfinished. No learned handler enabled;handler presence is not class completion.',
                'Current executable parity,do49calc[1]/extended-passive and extreme signed32/untested modifier combinations remain open. No deployment,native/client/protocol/persistence-schema changes. Effects/audio and icons remain separate identified placeholders.'],
        files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
    out=ROOT/'tools/reference/item-property-creation-native-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Item property creation receipt stale'
    else:out.write_text(encoded)
    print('Fresh properties:107focused/323broader markers,2reloads each;61472writer/24source-LCG graphs;202kernel/201fresh/191projection;71Python/audits/protected pass;no complete NPC item factory.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
