#!/usr/bin/env python3
"""Bind current NPC affix pools/rare names without certifying generated items."""
import argparse,hashlib,json,subprocess
from collections import Counter
from pathlib import Path
from record_item_affixes import expected as previous_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/npc-item-affixes';WIDE=ROOT/'.local/class-skills/npc-item-affixes-broader';SOURCE=ROOT/'.local/class-skills/npc-rare-names-source'
PARENT='614d4421f91de510d260b07a7e8fe54489703f7a'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def historical(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))
def expected(broader):
    c=previous_expected(broader);c['npc-item-affix-check']=1;c['npc-item-affix-reload-check']=2;return c
def build(check=False):
    native=load(STAGE/'birth-inputs.json');wide=load(WIDE/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
    assert native['artifacts']==wide['artifacts']==audit['artifacts'] and native['copiedData']==wide['copiedData']==audit['copiedData']
    for p in (STAGE/'birth-inputs.json',WIDE/'birth-inputs.json',STAGE/'audit-inputs.json'):
        snapshot(STAGE/'pre-build-1.json',p)
        for source,h in load(p)['sourceInputs'].items():assert sha(ROOT/source)==h,source
    before=load(ROOT/'.local/class-skills/item-affixes/pre-build-1.json')['inputs'];now=load(STAGE/'pre-build-1.json')['inputs']
    changed=lambda k:{p for p in before[k].keys()|now[k].keys() if before[k].get(p)!=now[k].get(p)}
    assert changed('sourceInputs')=={'src/Sanctuary.Content/'+n+'.cs' for n in ('SourceAffixPicker','ValkyrieAffixes','BalanceCatalog','NpcItemAffixes')}|{'tests/Sanctuary.Integration/NpcItemAffixChecks.cs'}
    assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/npc-item-affix-catalogue.json'} and before['projectInputs']==now['projectInputs']
    for stage,broader,count in ((STAGE,False,104),(WIDE,True,320)):
        log=(stage/'birth-native-checks.log').read_text();assert markers(stage/'birth-native-checks.log')==expected(broader) and sum(markers(stage/'birth-native-checks.log').values())==count
        assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert log.count('Item affix:41472 complete magic/rare outer results and service traces')==3
        assert log.count('NPC item affixes:22144 current all692base controls/288 weighted-cap controls')==3
        assert log.count('NPC rare names:12456 current/2016 synthetic complete source')==3
        assert log.count('4752 complete shared selector controls/510 pinned item-level suppliers')==3
        assert (stage/'npc-item-affix-fixtures.json').read_text()=='pools-and-names-only:no-generated-item-or-actor'
        s=load(stage/'shadow-equipment-fixtures.json');assert s['Rank']==17 and len(s['Requests'])==8 and all(r['ItemLevel']==72 for r in s['Requests'])
    for name in ('content-build-1.log','integration-build-1.log'):assert all(x in (STAGE/name).read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):assert sha(STAGE/kind/name)==native['artifacts']['Assemblies/'+name]
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert sha(STAGE/'native/Distribution'/name)==native['artifacts'][name]
    source=load(ROOT/'tools/reference/npc-rare-names-source-evidence.json');assert source['currentCases']==12456 and source['syntheticCases']==2016 and not source['currentExecutableCodeParity'] and len(source['completeRoutineHashes'])==4
    for k in ('inputs','artifacts'):
        for p,h in source[k].items():assert sha(ROOT/p)==h,p
    general=load(ROOT/'tools/reference/npc-item-affix-engine-evidence.json');assert general['fixture']['cases']==22432 and general['syntheticWeightedCases']==288 and len(general['compiledUnchangedRoutines'])==9
    assert sha(ROOT/general['fixture']['path'])==general['fixture']['sha256']
    for p,h in general['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/p)==h,p
    pins=load(ROOT/'tools/reference/d2r-named-equipment/sources.json')
    for n,h in general['pinnedTableHashes'].items():assert sha(ROOT/'tools/reference/d2r-named-equipment/json'/(n+'.json'))==h==pins['files']['json/'+n+'.json']['sha256']
    catalogue=load(ROOT/'data/Sanctuary/Balance/npc-item-affix-catalogue.json');assert [len(catalogue[k]) for k in ('affixes','bases','types','rareNames','rareBases')]==[991,692,105,201,692]
    rarepins=load(ROOT/'tools/reference/item-generation/sources.json')
    for p,row in rarepins['files'].items():assert sha(ROOT/'tools/reference/item-generation'/p)==row['sha256']
    assert sum(rarepins[n]['checkedFields'] for n in ('rareprefix','raresuffix'))==811 and not any(rarepins[n]['differences'] for n in ('rareprefix','raresuffix'))
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
    for name,needle in (('source-check.log','NPC source controls:22432magic/14472rare'),('npc-catalogue-check.log','NPC items: 692 current bases'),('valkyrie-catalogue-check.log','Valkyrie: 18 current bases'),('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):assert needle in (STAGE/name).read_text()
    assert '\nOK\n' in (STAGE/'python-tests.log').read_text() and all(r['exitCode']==0 for r in load(STAGE/'verification-commands.json'))
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative;assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protected[key]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for k in ('patchHashes','sharedInputs'):
        for p,h in client[k].items():assert sha(ROOT/p)==h,p
    files=[Path(__file__).resolve(),*(ROOT/'src/Sanctuary.Content'/(n+'.cs') for n in ('SourceAffixPicker','ValkyrieAffixes','BalanceCatalog','NpcItemAffixes')),ROOT/'tests/Sanctuary.Integration/NpcItemAffixChecks.cs',ROOT/'tests/check_npc_item_affixes.py',ROOT/'tools/probe_npc_rare_names.py',ROOT/'tools/probe_valkyrie_affix_reference.py',ROOT/'tools/build_summon_affix_catalogue.py',ROOT/'tools/check_npc_item_affix_sources.py',ROOT/'tools/build_skill_roadmap.py',
        ROOT/'tools/reference/npc-rare-names-source-evidence.json',ROOT/'tools/reference/npc-item-affix-engine-evidence.json',ROOT/'tools/reference/shadow-summons-preflight.json',
        *(ROOT/'docs'/p for p in ('npc-item-affixes-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))]
    files.extend(ROOT/p for p in source['artifacts'])
    for stage in (STAGE,WIDE):files.extend(p for p in stage.glob('*') if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
    files.extend((ROOT/'tools/reference/item-generation').iterdir())
    files.extend(ROOT/'.local/class-skills'/('npc-item-affix-source-probe'+suffix) for suffix in ('','.cpp','.json'))
    files.extend((ROOT/'.local/class-skills').glob('npc-item-affix-source-*.log'))
    for name in ('npc-item-affix-initial','npc-item-affix-compile-3'):files.extend(p for p in (ROOT/'.local/class-skills'/name).iterdir() if p.is_file())
    e=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlerEnabled=False,liveShadowActor=False,generatedNpcItems=False,fullNpcStatPropertyEventDelivery=False,
        scope='Complete current692base magic affix pools/weighted selection and201rare names;not generated items,property execution,live AI or learned Shadows',handlers=194,assassinHandlers=24,completedClasses=[],
        artifacts=native['artifacts'],copiedData=native['copiedData'],focusedMarkers=104,broaderMarkers=320,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
        retainedSourceOuterControls=41472,currentPoolControlsPerBoot=22144,weightedCapControlsPerBoot=288,currentRareNameControlsPerBoot=12456,syntheticRareNameControlsPerBoot=2016,completePickerRoutines=9,completeRareNameRoutines=4,retainedShadowSelectorControlsPerBoot=4752,retainedShadowRankSuppliersPerBoot=510,
        retainedValkAffixControlsPerBoot=5184,retainedValkEquipmentControlsPerBoot=80,currentExecutableCodeParity=False,
        pythonTests=71,auditFingerprints=fingerprints['named-item-runtime-audit.json'],affixFingerprints=fingerprints['first-loop-affix-audit.json'],namedDecisionsUnchanged=573,
        protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
        diagnostics=['Initial invocation used --all-items before the script edit parsed;invocation log retained. Initial22144case source package retained before synthetic expansion.',
                     'Untouched upstream TSVs retain CRLF/trailing empty columns;scoped Git whitespace attributes preserve exact pinned bytes. Initial warning log retained.',
                     'Expanded synthetic harness initially addressed the property-table stub rather than magicTables;compile failed. Script/generated source/compiler log/previous binary retained before fixing the harness member. No copied source routine or tested C# binary changed.'],
        limits=['The current catalogue includes all692bases/991spawnableaffixes/201rare names. Format100 source pools preserve unsupported valid properties;legacy first-socket-property rejection remains explicitly uncertain against modern behavior.',
                'Nine complete affix functions and four complete rare-name/seed functions use declared item/base/type/table/property-lookup services. Source ordinals/localization keys are extraction identities,not claims about modern binary IDs or resolved display strings.',
                'No property execution,final initialization,quality fallback,global item seed allocation,actual generated NPC item/stat/property/event graph,Shadow AI/actor/cap/casting/lifecycle/input or generated-item saves is certified.',
                'Existing Shadow/Valk original owner/gear/rank/request/seed reloads retain;the new pure fixture records its no-generated-item/no-actor scope. No summon handler is enabled.',
                'Current executable parity,do49calc[1]/extended-passive discrepancy and final assets remain open. No deployment,native/client/protocol/persistence-schema changes.'],
        files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
    out=ROOT/'tools/reference/npc-item-affixes-native-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'NPC item affix receipt stale'
    else:out.write_text(encoded)
    print('NPC item affixes:104focused/320broader markers,2reloads each;22144pool/288weighted/12456rare/2016cap controls per boot;Shadow/Valk retained;71Python/audits/protected pass;no generated NPC items.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
