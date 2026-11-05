#!/usr/bin/env python3
"""Bind actual NPC on-kill curse delivery,original graphs and retained regressions."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from collections import Counter
from record_unit_events import expected as previous_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/npc-kill-procs';WIDE=ROOT/'.local/class-skills/npc-kill-procs-broader';PARENT='9a83498913e85c81d085baf44afced25ba028cdd'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def old(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))
def expected(broader):
 c=previous_expected(broader);c['npc-kill-proc-check']=1;c['npc-kill-proc-reload-check']=2;c['npc-kill-proc-live-result']=3;return c

def build(check=False):
 born=load(STAGE/'birth-inputs.json');wide=load(WIDE/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
 assert born['artifacts']==wide['artifacts']==audit['artifacts'] and born['copiedData']==wide['copiedData']==audit['copiedData']
 for p in (STAGE/'birth-inputs.json',WIDE/'birth-inputs.json',STAGE/'audit-inputs.json'):
  snapshot(STAGE/'pre-build-2.json',p)
  for source,h in load(p)['sourceInputs'].items():assert sha(ROOT/source)==h,source
 before=load(ROOT/'.local/class-skills/unit-events/pre-build-4.json')['inputs'];now=load(STAGE/'pre-build-2.json')['inputs']
 changed=lambda k:{p for p in before[k].keys()|now[k].keys() if before[k].get(p)!=now[k].get(p)}
 assert changed('sourceInputs')=={'src/Sanctuary.Content/NpcItemProcGraph.cs','tests/Sanctuary.Integration/UnitEventsChecks.cs','tests/Sanctuary.Integration/NpcKillProcChecks.cs'}
 assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json'} and before['projectInputs']==now['projectInputs']
 for stage,broader,count in ((STAGE,False,140),(WIDE,True,356)):
  log=(stage/'birth-native-checks.log').read_text();assert markers(stage/'birth-native-checks.log')==expected(broader) and sum(markers(stage/'birth-native-checks.log').values())==count
  assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
  assert log.count('Unit events:1488complete source sequence controls/8routines')==3
  assert log.count('NPC proc graphs:real original carriers/source writer and qualified hit/attack paths')==1
  assert log.count('Item execution metadata:429raw current rows/742missile links/43explicit missing animations')==3
  assert log.count('Skill execution:37680complete source common-executor controls')==3 and log.count('Item effect casts:18912complete wrapper/handler')==3 and log.count('Item proc callbacks:25920complete gate/decode/LCG')==3
  assert log.count('NPC kill procs:actual stat196/function20/source-written carriers')==1
  saved=load(stage/'npc-kill-proc-fixtures.json');graph=saved['Graph'];assert saved['Generation']==2 and graph['Actor']==saved['Actor'] and graph['Owner']==saved['Owner']
  assert saved['Items']==[i['Item'] for i in graph['Items']] and len(graph['Items'])==2 and saved['Mana']==0 and saved['Life']>0
  assert len(graph['Events']['Entries'])==1
  event=graph['Events']['Entries'][0];assert event['Event']==9 and event['Function']==20 and event['Queue']==2 and event['Guid1']==event['Key']==((196<<16)|(72<<6)|5) and event['Guid2']==event['Flags']==0
  assert all(i['State']['Properties']==[dict(Packed=(196<<16)|(72<<6)|5,Value=50)] for i in graph['Items'])
  assert saved['Curse']['Source']==saved['Actor'] and saved['Curse']['Rank']==5 and saved['Curse']['Ability']==next(r['ability']for r in load(ROOT/'data/Sanctuary/Balance/class-skills.json')if r['sourceId']==72)
  assert load(stage/'unit-events-fixtures.json')['Generation']==2
  assert len(load(stage/'item-execution-metadata-fixtures.json')['Targets'])==8
 for name in ('content-build-1.log','integration-build-2.log','recovery-build-1.log'):assert all(x in (STAGE/name).read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):assert sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
 source=load(ROOT/'tools/reference/npc-kill-proc-source-review.json');assert source['stat']==196 and source['function']==20 and source['event']=='kill' and not source['currentExecutableParity']
 for group in ('inputs',):
  for p,h in source[group].items():assert sha(ROOT/p)==h,p
 fingerprints={}
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  current=load(STAGE/name);assert current==load(ROOT/'tools/reference'/name);baseline=old('tools/reference/'+name);omit={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in current.items() if k not in omit}=={k:v for k,v in baseline.items() if k not in omit};fingerprints[name]=len(current['InputHashes'])
  for p,h in current['InputHashes'].items():assert sha(ROOT/p)==h,p
 assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573 and markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
 status=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses'] and sum(r['implementation']=='handler-present' for r in status['skills'])==194 and sum(r['characterClass']=='Assassin' and r['implementation']=='handler-present' for r in status['skills'])==24
 assert not load(ROOT/'tools/reference/shadow-summons-preflight.json')['handlerEnabled']
 for name,needle in (('kill-source-check.log','NPC kill source review:current196/kill/function20'),('combat-metadata-check.log','21item event stats'),('callback-source-check.log','Item proc callbacks:25920complete source'),('unit-source-check.log','Unit events:1488complete source'),('metadata-check.log','Item execution metadata:429complete current rows'),('executor-source-check.log','Skill execution:37680complete source'),('python-tests.log','Ran 71 tests'),('npc-source-check.log','NPC source controls:22432magic/14472rare'),('npc-catalogue-check.log','NPC items: 692 current bases'),('valkyrie-catalogue-check.log','Valkyrie: 18 current bases'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):assert needle in (STAGE/name).read_text(),name
 assert '\nOK\n' in (STAGE/'python-tests.log').read_text() and all(c['exitCode']==0 for c in load(STAGE/'verification-commands.json'))
 installation=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installation['installedArtifacts'])==106
 for p,h in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/p)==h,p
 protection=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
 for kind,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
  folder=Path(installation['runtimeRoot'])/'.local/server'/relative;assert len(protection[kind])==count and {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protection[kind]
 client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
 for kind in ('patchHashes','sharedInputs'):
  for p,h in client[kind].items():assert sha(ROOT/p)==h,p
 files=[Path(__file__).resolve(),ROOT/'tests/check_npc_kill_procs.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/reference/npc-kill-proc-source-review.json',ROOT/'tools/reference/shadow-summons-preflight.json']
 files.extend(ROOT/p for p in changed('sourceInputs'));files.append(ROOT/'tools/review_npc_kill_procs.py')
 files.extend(ROOT/'docs'/n for n in ('npc-kill-procs-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))
 for stage in (STAGE,WIDE):files.extend(p for p in stage.iterdir() if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
 for name in ('failed-focused-1',):files.extend(p for p in (STAGE/name).rglob('*') if p.is_file())
 recovery=load(STAGE/'failed-focused-1/recovery.json');failed=load(STAGE/'failed-focused-1/birth-inputs.json')
 assert recovery['originalSourceMatches'] and recovery['originalContentMatches'] and recovery['originalIntegrationMatches']
 assert sha(STAGE/'failed-focused-1/integration/Sanctuary.Integration.dll')==failed['artifacts']['Assemblies/Sanctuary.Integration.dll']==recovery['integrationSha256']
 assert sha(STAGE/'failed-focused-1/NpcKillProcChecks.cs')==failed['sourceInputs']['tests/Sanctuary.Integration/NpcKillProcChecks.cs']
 e=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=194,assassinHandlers=24,completedClasses=[],generatedNpcItems=False,genericNpcProcAdmission=False,
  scope='Actual stat196/kill/function20 curse delivery through original event-only NPC carriers and the existing qualified pre-death contact pipeline;other producers/full item factory remain gated.',artifacts=born['artifacts'],copiedData=born['copiedData'],focusedRun=2,broaderRun=1,contentBuild=1,integrationBuild=2,focusedMarkers=140,broaderMarkers=356,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
  originalSavedCarriersPerCohort=2,originalSavedActorsPerCohort=1,heldOriginalKillCurseVictimsPerCohort=1,actualConnectedKillChecksPerCohort=3,retainedConnectedGraphChecksPerCohort=3,retainedConnectedBloodChecksPerCohort=3,retainedNpcCursesPerCohort=8,
  pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=fingerprints['named-item-runtime-audit.json'],affixFingerprints=fingerprints['first-loop-affix-audit.json'],protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
  diagnostics=['Focused1mechanical checks passed,but the natural-clock fixture timed out before a save/export. Corrected fixture prevents incidental healing and sets lethal HP during captured windup. Root cause of the old timeout is not independently established. Its initial archive command stopped on the absent manifest;exact old source was recovered against the frozen hash and the original integration DLL reproduced byte-for-byte. Logs,inputs,source,binaries and recovery receipt are preserved. Final focused2and broader1use the restored Content1/Integration2candidate matching frozen snapshot2.'],
  limits=['Current table maps on-kill196to callback20,with outgoing packet hitflag/chance rules. Static complete-function chronology review is distinct from retained compiled callback/event proof and actual native delivery;current executable parity remains unverified.',
   'Tests establish actual ordinary pre-death damage/kill/attack events and naturally scheduled summon kills. A native target remains Alive at source HP0until the return phase commits death. Later casts against parked/deleted/removed bodies,ground aim,missile/incoming/death/level-up producers and arbitrary NPC spells remain separate and gated.',
   'Only supported curse195/196/198event carriers are admitted. Original wounded actor/items/order/LCG/mana/held curse survive2reloads and another kill after each. This does not complete generated NPC equipment,the full initializer/low-seed fallback,charge actions,Shadow/Valk actor/AI/casting/lifecycle/input or unanswered skill-tab decisions.',
   'No new handler or completed class,deployment,native/client/protocol/schema change. Separate final effects/audio/tree/hotkey assets remain identified placeholders.'],files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
 out=ROOT/'tools/reference/npc-kill-procs-native-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'NPC kill proc native receipt stale'
 else:out.write_text(encoded)
 print('NPC kill procs:140focused/356broader markers;actual196/function20kill delivery,original graphs/curse through2reloads and natural-clock kills;71Python/audits/protected pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
