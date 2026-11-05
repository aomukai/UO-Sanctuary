#!/usr/bin/env python3
"""Bind complete common skill execution and retained actual Blood/native checks."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from collections import Counter
from record_item_effect_casts import expected as previous_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/skill-execution';WIDE=ROOT/'.local/class-skills/skill-execution-broader';PARENT='dd577d965bfb4f244d9e4f69360ff4c03bd08426'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def old(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))
def expected(broader):
 c=previous_expected(broader);c['skill-execution-check']=1;c['skill-execution-reload-check']=2;return c

def build(check=False):
 born=load(STAGE/'birth-inputs.json');wide=load(WIDE/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
 assert born['artifacts']==wide['artifacts']==audit['artifacts'] and born['copiedData']==wide['copiedData']==audit['copiedData']
 for p in (STAGE/'birth-inputs.json',WIDE/'birth-inputs.json',STAGE/'audit-inputs.json'):
  snapshot(STAGE/'pre-build-2.json',p)
  for source,h in load(p)['sourceInputs'].items():assert sha(ROOT/source)==h,source
 before=load(ROOT/'.local/class-skills/item-effect-casts/pre-build-6.json')['inputs'];now=load(STAGE/'pre-build-2.json')['inputs']
 changed=lambda k:{p for p in before[k].keys()|now[k].keys() if before[k].get(p)!=now[k].get(p)}
 assert changed('sourceInputs')=={'src/Sanctuary.Content/'+n+'.cs' for n in ('SkillExecution','NpcItemSkillExecution','NpcItemSpellCasts')}|{'tests/Sanctuary.Integration/SkillExecutionChecks.cs'}
 assert changed('copiedData')=={'Data/Sanctuary/Balance/class-skill-status.json'} and before['projectInputs']==now['projectInputs']
 for stage,broader,count in ((STAGE,False,125),(WIDE,True,341)):
  log=(stage/'birth-native-checks.log').read_text();assert markers(stage/'birth-native-checks.log')==expected(broader) and sum(markers(stage/'birth-native-checks.log').values())==count
  assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
  assert log.count('Skill execution:37680complete source common-executor controls')==3
  assert log.count('Item effect casts:18912complete wrapper/handler')==3
  assert log.count('Item proc callbacks:25920complete gate/decode/LCG')==3
  assert log.count('Item skill properties:72558 complete source controls')==3
  assert log.count('Fresh property graphs:24original native items')==3
  assert log.count('4752 complete shared selector controls/510 pinned item-level suppliers')==3
  blood=load(stage/'blood-life-tap-fixtures.json');assert blood['Generation']==2 and blood['Curse']['Source']==blood['Source'] and blood['Curse']['Rank']==5
  assert (stage/'skill-execution-fixtures.json').read_text()=='complete-common-executor-contract:actual-blood-item-frame-only:other-services-not-admitted'
 for name in ('content-build-1.log','integration-build-2.log'):assert all(x in (STAGE/name).read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):assert sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
 source=load(ROOT/'tools/reference/skill-execution-source-evidence.json');assert source['cases']==37680 and source['legacyDoSlots']==191 and source['skillFieldWidthsVerified'] and len(source['completeRoutineHashes'])==1 and not source['currentExecutableParity']
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
 for name,needle in (('source-check.log','Skill execution:37680complete source'),('cast-source-check.log','Item effect casts:18912complete source'),('python-tests.log','Ran 71 tests'),('npc-source-check.log','NPC source controls:22432magic/14472rare'),('npc-catalogue-check.log','NPC items: 692 current bases'),('valkyrie-catalogue-check.log','Valkyrie: 18 current bases'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):assert needle in (STAGE/name).read_text(),name
 assert '\nOK\n' in (STAGE/'python-tests.log').read_text() and all(c['exitCode']==0 for c in load(STAGE/'verification-commands.json'))
 installation=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installation['installedArtifacts'])==106
 for p,h in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/p)==h,p
 protection=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
 for kind,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
  folder=Path(installation['runtimeRoot'])/'.local/server'/relative;assert len(protection[kind])==count and {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protection[kind]
 client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
 for kind in ('patchHashes','sharedInputs'):
  for p,h in client[kind].items():assert sha(ROOT/p)==h,p
 files=[Path(__file__).resolve(),ROOT/'tools/probe_skill_execution.py',ROOT/'tests/check_skill_execution.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/reference/skill-execution-source-evidence.json',ROOT/'tools/reference/shadow-summons-preflight.json']
 files.extend(ROOT/p for p in changed('sourceInputs'));files.extend(ROOT/p for p in source['artifacts'])
 files.extend(ROOT/'docs'/n for n in ('skill-execution-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))
 for stage in (STAGE,WIDE):files.extend(p for p in stage.iterdir() if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
 files.extend(ROOT/'.local/class-skills'/f'skill-execution-source-run-{i}.log' for i in (1,2,3,4))
 files.append(ROOT/'.local/class-skills/skill-execution-source-run-3.py')
 for folder in (STAGE/'pre-width-review-1',WIDE/'pre-width-review-1'):files.extend(p for p in folder.rglob('*') if p.is_file())
 assert (ROOT/'.local/class-skills/skill-execution-source/cases.json').read_bytes()==(STAGE/'pre-width-review-1/source/cases.json').read_bytes()
 e=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=194,assassinHandlers=24,completedClasses=[],generatedNpcItems=False,genericNpcProcAdmission=False,
  scope='Complete older common skill executor with explicit services;actual native Blood synchronous item frame supplies do30 only.',artifacts=born['artifacts'],copiedData=born['copiedData'],focusedRun=2,broaderRun=2,contentBuild=1,integrationBuild=2,focusedMarkers=125,broaderMarkers=341,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
  sourceCasesPerBoot=37680,completeSourceRoutines=1,legacyDoSlots=191,skillFieldWidthsVerified=True,correctedSourceResultsUnchanged=True,additionalInvalidOverrideChecksPerBoot=1,retainedCastControlsPerBoot=18912,retainedCallbackControlsPerBoot=25920,actualConnectedBloodChecksPerCohort=3,pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=fingerprints['named-item-runtime-audit.json'],affixFingerprints=fingerprints['first-loop-affix-audit.json'],protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
  diagnostics=['Earlier green focused1/broader1/audit1 inputs,binaries and source are preserved under pre-width-review-1. Header review corrected synthetic skill-record field widths/signedness and native test word conversion;all37680observable source results/traces are byte-identical,but accepted focused2/broader2/audit2 rebind the corrected proof. Source probe3had a Python newline insertion syntax error before execution;the failed producer/log are preserved and source4passes.'],
  limits=['Complete older body preserves ordinary/item/outgoing admission,independent do/missile paths,override timing,aim zero edges,payment/quantity/delay and return semantics. Nested tables/skill lists/ranks/room/resources/do-functions/missile factories/formulas are explicit services,not all implemented spells. Corrupt override indexes and signed-overflow contexts are outside defined source acceptance;unmapped production overrides refuse explicitly.',
   'Actual Blood uses the existing protected synchronous native item frame,which has no selected ordinary D2 skill-list entry;this is not proof of original D2 monster used-skill/town/global scheduler context. Only pinned Life Tap do30/no direct missile/no use-mana-on-do is admitted here. Other item execution services throw rather than silently succeed.',
   'Generic event subscriptions,other NPC spell/stat/skill-list/room/missile/resource contexts,charge skills,full generated item initializer/fallback and Shadow/Valk actor/AI/casting/lifecycle remain unfinished. Current executable/client parity remains unverified.',
   'No new learned handler or completed class;no deployment,native/client/protocol/persistence-schema change. Effects/audio and talent/hotkey artwork remain separate identified placeholders.'],files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
 out=ROOT/'tools/reference/skill-execution-native-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Skill execution native receipt stale'
 else:out.write_text(encoded)
 print('Skill execution:125focused/341broader markers,37680source executor controls,actual Blood context/target/aggression/no-mana delivery;71Python/audits/protected pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
