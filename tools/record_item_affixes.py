#!/usr/bin/env python3
"""Bind magic/rare outer control flow without certifying complete NPC items."""
import argparse,hashlib,json,subprocess
from collections import Counter
from pathlib import Path
from record_shadow_equipment import expected as previous_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/item-affixes';WIDE=ROOT/'.local/class-skills/item-affixes-broader';SOURCE=ROOT/'.local/class-skills/item-affix-orchestration-source'
PARENT='91a19a0c39f20398768b4dc0ee7984b5575ebeb1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))
def expected(broader):
    c=previous_expected(broader);c['item-affix-check']=1;c['item-affix-reload-check']=2;return c
def build(check=False):
    native=load(STAGE/'birth-inputs.json');wide=load(WIDE/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
    assert native['artifacts']==wide['artifacts']==audit['artifacts'] and native['copiedData']==wide['copiedData']==audit['copiedData']
    for p in (STAGE/'birth-inputs.json',WIDE/'birth-inputs.json',STAGE/'audit-inputs.json'):
        snapshot(STAGE/'pre-build-1.json',p)
        for source,h in load(p)['sourceInputs'].items():assert sha(ROOT/source)==h,source
    before=load(ROOT/'.local/class-skills/shadow-equipment/pre-build-1.json')['inputs'];now=load(STAGE/'pre-build-1.json')['inputs']
    changed=lambda k:{p for p in before[k].keys()|now[k].keys() if before[k].get(p)!=now[k].get(p)}
    assert changed('sourceInputs')=={'src/Sanctuary.Content/SourceItemAffixes.cs','tests/Sanctuary.Integration/ItemAffixChecks.cs'}
    assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json'} and before['projectInputs']==now['projectInputs']
    for stage,broader,count in ((STAGE,False,101),(WIDE,True,317)):
        log=(stage/'birth-native-checks.log').read_text();assert markers(stage/'birth-native-checks.log')==expected(broader) and sum(markers(stage/'birth-native-checks.log').values())==count
        assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert log.count('Item affix:41472 complete magic/rare outer results and service traces')==3
        assert log.count('4752 complete shared selector controls/510 pinned item-level suppliers')==3
        assert (stage/'item-affix-fixtures.json').read_text()=='source-outer-services-only:no-item-or-actor'
        s=load(stage/'shadow-equipment-fixtures.json');assert s['Rank']==17 and len(s['Requests'])==8 and all(r['ItemLevel']==72 for r in s['Requests'])
    for name in ('content-build-1.log','integration-build-1.log'):assert all(x in (STAGE/name).read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):assert sha(STAGE/kind/name)==native['artifacts']['Assemblies/'+name]
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert sha(STAGE/'native/Distribution'/name)==native['artifacts'][name]
    source=load(ROOT/'tools/reference/item-affix-orchestration-source-evidence.json');assert source['cases']==41472 and not source['currentExecutableCodeParity'] and len(source['compiledUnchangedRoutineHashes'])==2
    for k in ('inputs','artifacts'):
        for p,h in source[k].items():assert sha(ROOT/p)==h,p
    fingerprints={}
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        data=load(STAGE/name);old=historical('tools/reference/'+name);assert data==load(ROOT/'tools/reference'/name)
        fingerprints[name]=len(data['InputHashes'])
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
    files=[Path(__file__).resolve(),ROOT/'src/Sanctuary.Content/SourceItemAffixes.cs',ROOT/'tests/Sanctuary.Integration/ItemAffixChecks.cs',ROOT/'tests/check_item_affixes.py',ROOT/'tools/probe_item_affix_orchestration.py',ROOT/'tools/build_skill_roadmap.py',
        ROOT/'tools/reference/item-affix-orchestration-source-evidence.json',ROOT/'tools/reference/shadow-summons-preflight.json',
        *(ROOT/'docs'/p for p in ('item-affixes-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))]
    files.extend(ROOT/p for p in source['artifacts'])
    for stage in (STAGE,WIDE):files.extend(p for p in stage.glob('*') if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
    for name in ('attempt-1','initial-6912'):files.extend(p for p in (SOURCE/name).iterdir() if p.is_file())
    e=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlerEnabled=False,liveShadowActor=False,generatedNpcItems=False,fullNpcStatPropertyEventDelivery=False,
        scope='Complete older magic/rare outer orchestration with declared nested services;not generated items,property execution,live AI or learned Shadows',handlers=194,assassinHandlers=24,completedClasses=[],
        artifacts=native['artifacts'],copiedData=native['copiedData'],focusedMarkers=101,broaderMarkers=317,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
        sourceOuterControls=41472,nativeOuterControlsPerBoot=41472,compiledCompleteRoutines=2,retainedShadowSelectorControlsPerBoot=4752,retainedShadowRankSuppliersPerBoot=510,
        retainedValkAffixControlsPerBoot=5184,retainedValkEquipmentControlsPerBoot=80,currentExecutableCodeParity=False,
        pythonTests=71,auditFingerprints=fingerprints['named-item-runtime-audit.json'],affixFingerprints=fingerprints['first-loop-affix-audit.json'],namedDecisionsUnchanged=573,
        protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        diagnostics=['Initial probe matrix cardinality expectation27648was arithmetic error;actual matrix6912. Preserved initial source/binary/logs/script before correcting expectation. Finish-trace check made positional. No copied source routine changed.',
                     'Expanded accepted matrix includes independent preferred prefix/suffix IDs,per-slot offsets and absent property records:41472cases. Initial6912successful package retained.'],
        limits=['Names,pickers,property records/application,legacy-format rare path,outer RNG and final initialization are declared observed services;no real item or native actor is generated.',
                'Production outer contract does not filter unsupported valid affixes. Complete pools/name/property writer/event/initialization/quality fallback and NPC graph integration remain required.',
                'Original Shadow owner/gear/rank/request/seed and retained learned skills reload;the pure outer-service fixture only records its explicit no-item/no-actor scope. No generated-item or Shadow AI/save claim.',
                'Current executable parity,do49literal calc[1]/extended-passive discrepancy,NPC dual-claw combat/skill contexts,actor/cap/casting/lifecycle/input and final assets remain open.',
                'No deployment,native/client/protocol/persistence-schema changes. Effects/audio and talent/hotkey icons remain separate identified placeholders.'],
        files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
    out=ROOT/'tools/reference/item-affixes-native-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Item affix receipt stale'
    else:out.write_text(encoded)
    print('Item affixes:101focused/317broader markers,2reloads each;41472outer controls per boot;Shadow/Valk retained;71Python/audits/573decisions/protected106+38+20+40 pass;no generated NPC items.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
