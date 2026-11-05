#!/usr/bin/env python3
"""Bind unit-event source controls to actual original NPC graphs and regressions."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from collections import Counter
from record_item_execution_metadata import expected as previous_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/unit-events';WIDE=ROOT/'.local/class-skills/unit-events-broader';PARENT='f1433491919878d01dd6aca30500cf5e7da54064'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def old(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))
def expected(broader):
 c=previous_expected(broader);c['unit-events-check']=1;c['unit-events-reload-check']=2;c['unit-events-live-result']=3;return c

def build(check=False):
 born=load(STAGE/'birth-inputs.json');wide=load(WIDE/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
 assert born['artifacts']==wide['artifacts']==audit['artifacts'] and born['copiedData']==wide['copiedData']==audit['copiedData']
 for p in (STAGE/'birth-inputs.json',WIDE/'birth-inputs.json',STAGE/'audit-inputs.json'):
  snapshot(STAGE/'pre-build-4.json',p)
  for source,h in load(p)['sourceInputs'].items():assert sha(ROOT/source)==h,source
 before=load(ROOT/'.local/class-skills/item-execution-metadata/pre-build-2.json')['inputs'];now=load(STAGE/'pre-build-4.json')['inputs']
 changed=lambda k:{p for p in before[k].keys()|now[k].keys() if before[k].get(p)!=now[k].get(p)}
 assert changed('sourceInputs')=={'src/Sanctuary.Content/'+n+'.cs' for n in ('SourceUnitEvents','ItemEventBindings','NpcItemProcGraph','NpcItemProcs','IronGolemContact','NpcCombatEvents','NpcItemSpellCasts','SkeletonPets')}|{'tests/Sanctuary.Integration/UnitEventsChecks.cs'}
 assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json'} and before['projectInputs']==now['projectInputs']
 for stage,broader,count in ((STAGE,False,134),(WIDE,True,350)):
  log=(stage/'birth-native-checks.log').read_text();assert markers(stage/'birth-native-checks.log')==expected(broader) and sum(markers(stage/'birth-native-checks.log').values())==count
  assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
  assert log.count('Unit events:1488complete source sequence controls/8routines')==3
  assert log.count('NPC proc graphs:real original carriers/source writer and qualified hit/attack paths')==1
  assert log.count('Item execution metadata:429raw current rows/742missile links/43explicit missing animations')==3
  assert log.count('Skill execution:37680complete source common-executor controls')==3 and log.count('Item effect casts:18912complete wrapper/handler')==3 and log.count('Item proc callbacks:25920complete gate/decode/LCG')==3
  saved=load(stage/'unit-events-fixtures.json');graph=saved['Graph'];assert saved['Generation']==2 and graph['Actor']==saved['Actor'] and graph['Owner']==saved['Owner']
  assert saved['Items']==[i['Item'] for i in graph['Items']] and len(graph['Items'])==2 and saved['Mana']==0 and saved['Life']>0
  assert len(graph['Events']['Entries'])==4 and graph['Events']['NextId']==16 and all(e['Queue']==2 and e['Guid1']==e['Key'] and e['Guid2']==e['Flags']==0 and e['Function']==20 for e in graph['Events']['Entries'])
  assert [e['Guid1']&65535 for e in graph['Events']['Entries']]==[(82<<6)|5]*2+[(66<<6)|5]*2
  assert len(load(stage/'item-execution-metadata-fixtures.json')['Targets'])==8
 for name in ('content-build-4.log','integration-build-3.log'):assert all(x in (STAGE/name).read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):assert sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
 source=load(ROOT/'tools/reference/unit-events-source-evidence.json');assert source['cases']==1488 and source['completeRoutines']==8 and not source['currentExecutableParity']
 for group in ('inputs','artifacts'):
  for p,h in source[group].items():assert sha(ROOT/p)==h,p
 fingerprints={}
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  current=load(STAGE/name);assert current==load(ROOT/'tools/reference'/name);baseline=old('tools/reference/'+name);omit={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in current.items() if k not in omit}=={k:v for k,v in baseline.items() if k not in omit};fingerprints[name]=len(current['InputHashes'])
  for p,h in current['InputHashes'].items():assert sha(ROOT/p)==h,p
 assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573 and markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
 status=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses'] and sum(r['implementation']=='handler-present' for r in status['skills'])==194 and sum(r['characterClass']=='Assassin' and r['implementation']=='handler-present' for r in status['skills'])==24
 assert not load(ROOT/'tools/reference/shadow-summons-preflight.json')['handlerEnabled']
 for name,needle in (('unit-source-check.log','Unit events:1488complete source'),('metadata-check.log','Item execution metadata:429complete current rows'),('executor-source-check.log','Skill execution:37680complete source'),('python-tests.log','Ran 71 tests'),('npc-source-check.log','NPC source controls:22432magic/14472rare'),('npc-catalogue-check.log','NPC items: 692 current bases'),('valkyrie-catalogue-check.log','Valkyrie: 18 current bases'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):assert needle in (STAGE/name).read_text(),name
 assert '\nOK\n' in (STAGE/'python-tests.log').read_text() and all(c['exitCode']==0 for c in load(STAGE/'verification-commands.json'))
 installation=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installation['installedArtifacts'])==106
 for p,h in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/p)==h,p
 protection=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
 for kind,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
  folder=Path(installation['runtimeRoot'])/'.local/server'/relative;assert len(protection[kind])==count and {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protection[kind]
 client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
 for kind in ('patchHashes','sharedInputs'):
  for p,h in client[kind].items():assert sha(ROOT/p)==h,p
 files=[Path(__file__).resolve(),ROOT/'tests/check_unit_events.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/reference/unit-events-source-evidence.json',ROOT/'tools/reference/shadow-summons-preflight.json']
 files.extend(ROOT/p for p in changed('sourceInputs'));files.extend(ROOT/p for p in source['artifacts'])
 files.extend(ROOT/'docs'/n for n in ('unit-events-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))
 for stage in (STAGE,WIDE):files.extend(p for p in stage.iterdir() if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
 for name in ('failed-build-1','pre-review-1','failed-focused-2'):files.extend(p for p in (STAGE/name).rglob('*') if p.is_file())
 e=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=194,assassinHandlers=24,completedClasses=[],generatedNpcItems=False,genericNpcProcAdmission=False,
  scope='Complete older event-list and event-stat contracts;original event-only NPC carriers with admitted outgoing curse delivery and separate optional persistence. Other producers/services/full item factory remain gated.',artifacts=born['artifacts'],copiedData=born['copiedData'],focusedRun=3,broaderRun=1,contentBuild=4,integrationBuild=3,focusedMarkers=134,broaderMarkers=350,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
  sourceSequencesPerBoot=1488,completeSourceRoutines=8,originalSavedCarriersPerCohort=2,originalSavedActorsPerCohort=1,actualConnectedGraphChecksPerCohort=3,retainedConnectedBloodChecksPerCohort=3,retainedNpcCursesPerCohort=8,
  pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=fingerprints['named-item-runtime-audit.json'],affixFingerprints=fingerprints['first-loop-affix-audit.json'],protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
  diagnostics=['Build1variable-scope collision corrected;failed source retained. Passing focused1inputs/binaries/log/manifest retained before added hard-point/attack-phase tests. Focused2caught a missing nullable clock service during the Open Wounds derivative query. This closed graph admits no time-property operands;null service leaves that provably zero contribution without inventing world time. Final focused3and broader1use rebuilt frozen inputs.'],
  limits=['Source comparison certifies the bounded older allocation/reentrancy domain,not the current executable. Cached-successor/free-all-in-callback and unsafe nested one-shot deletion are explicitly unsupported. Non-event stat-change side effects remain mandatory external services.',
   'Only event-only original carriers with195/198 and the eight existing do30curses are admitted. Live qualified surviving attack/damage and natural summon-clock checks do not establish incoming201,missile,kill/death/level/ground,removed/dead-target or arbitrary NPC spell producers. Stat195dead/removed-target cast explicitly refuses pending its area adapter.',
   'Optional new persistence table adds no fields to old body/contact schemas. Workers read immutable cached JSON;deleted native carriers are pruned on next main-thread graph refresh. Original actor/item/order/seed/wound/mana persistence and post-reload contact are checked;frozen pending-contact persistence is not established.',
   'Full item factory/initializer,quality low-seed fallback,charge actions,other stat/spell contexts,Shadow/Valk actor/AI/casting/lifecycle/input and unanswered skill-tab questions remain open. No new learned handler/completed class/deployment/native/client/protocol change. Effects/audio/tree/hotkey assets remain separate placeholders.'],files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
 out=ROOT/'tools/reference/unit-events-native-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Unit events native receipt stale'
 else:out.write_text(encoded)
 print('Unit events:134focused/350broader markers;1488source sequences/8routines;original actor/carriers/order/seed/wounds/mana through2reloads and actual natural-clock procs;71Python/audits/protected pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
