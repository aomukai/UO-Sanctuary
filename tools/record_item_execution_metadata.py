#!/usr/bin/env python3
"""Bind current execution metadata, actual eight NPC curses and retained cohorts."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from collections import Counter
from record_skill_execution import expected as previous_expected
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/item-execution-metadata';WIDE=ROOT/'.local/class-skills/item-execution-metadata-broader';PARENT='75a133770bd38c4a7025e110dc582c6000168796'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p):return json.loads(p.read_text())
def old(p):return json.loads(subprocess.check_output(['git','show',PARENT+':'+p],cwd=ROOT))
def expected(broader):
 c=previous_expected(broader);c['item-execution-metadata-check']=1;c['item-execution-metadata-reload-check']=2;return c

def build(check=False):
 born=load(STAGE/'birth-inputs.json');wide=load(WIDE/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
 assert born['artifacts']==wide['artifacts']==audit['artifacts'] and born['copiedData']==wide['copiedData']==audit['copiedData']
 for p in (STAGE/'birth-inputs.json',WIDE/'birth-inputs.json',STAGE/'audit-inputs.json'):
  snapshot(STAGE/'pre-build-2.json',p)
  for source,h in load(p)['sourceInputs'].items():assert sha(ROOT/source)==h,source
 before=load(ROOT/'.local/class-skills/skill-execution/pre-build-2.json')['inputs'];now=load(STAGE/'pre-build-2.json')['inputs']
 changed=lambda k:{p for p in before[k].keys()|now[k].keys() if before[k].get(p)!=now[k].get(p)}
 assert changed('sourceInputs')=={'src/Sanctuary.Content/'+n+'.cs' for n in ('ItemSkillExecution','NpcItemSkillExecution','NpcItemSpellCasts','NecromancerCurseCasts')}|{'tests/Sanctuary.Integration/ItemExecutionMetadataChecks.cs'}
 assert changed('copiedData')=={'Data/Sanctuary/Balance/'+n+'.json' for n in ('class-skill-status','item-skill-execution')} and before['projectInputs']==now['projectInputs']
 abilities={r['ability'] for r in load(ROOT/'data/Sanctuary/Balance/class-skills.json') if r['sourceId'] in (66,71,72,76,77,82,87,91)}
 assert len(abilities)==8
 for stage,broader,count in ((STAGE,False,128),(WIDE,True,344)):
  log=(stage/'birth-native-checks.log').read_text();assert markers(stage/'birth-native-checks.log')==expected(broader) and sum(markers(stage/'birth-native-checks.log').values())==count
  assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
  assert log.count('Item execution metadata:429raw current rows/742missile links/43explicit missing animations')==3
  assert log.count('NPC item curses:8actual do30 spells')==1
  assert log.count('Skill execution:37680complete source common-executor controls')==3 and log.count('Item effect casts:18912complete wrapper/handler')==3 and log.count('Item proc callbacks:25920complete gate/decode/LCG')==3
  assert log.count('Item skill properties:72558 complete source controls')==3 and log.count('Fresh property graphs:24original native items')==3
  assert log.count('4752 complete shared selector controls/510 pinned item-level suppliers')==3
  blood=load(stage/'blood-life-tap-fixtures.json');assert blood['Generation']==2 and blood['Curse']['Source']==blood['Source'] and blood['Curse']['Rank']==5
  saved=load(stage/'item-execution-metadata-fixtures.json');assert saved['Generation']==2 and len(saved['Targets'])==8 and len({t['Actor'] for t in saved['Targets']})==8
  assert {t['Curse']['Ability'] for t in saved['Targets']}==abilities
  assert all(t['Curse']['Source']==saved['Source'] and t['Curse']['Rank']==5 for t in saved['Targets'])
  assert sum(t['Blind'] is not None for t in saved['Targets'])==1 and sum(t['Curse']['Terror'] is not None for t in saved['Targets'])==1
 for name in ('content-build-1.log','integration-build-2.log'):assert all(x in (STAGE/name).read_text() for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 for name,kind in (('Sanctuary.Content.dll','content'),('Sanctuary.Integration.dll','integration')):assert sha(STAGE/kind/name)==born['artifacts']['Assemblies/'+name]
 source=load(ROOT/'tools/reference/item-skill-execution-metadata.json');assert source['rows']==429 and source['missiles']==742 and source['missingAnimationRows']==43 and source['allFieldsRetained'] and source['do30Curses']==[66,71,72,76,77,82,87,91]
 for group in ('inputs','artifacts'):
  for p,h in source[group].items():assert sha(ROOT/p)==h,p
 catalogue=load(ROOT/'data/Sanctuary/Balance/item-skill-execution.json');assert len(catalogue['Skills'])==429 and catalogue['Skills'][362]['Name']=='' and len(catalogue['UnresolvedAnimationIds'])==43
 fingerprints={}
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  current=load(STAGE/name);assert current==load(ROOT/'tools/reference'/name);baseline=old('tools/reference/'+name);omit={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in current.items() if k not in omit}=={k:v for k,v in baseline.items() if k not in omit};fingerprints[name]=len(current['InputHashes'])
  for p,h in current['InputHashes'].items():assert sha(ROOT/p)==h,p
 assert len(load(STAGE/'named-item-runtime-audit.json')['Definitions'])==573 and markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
 status=load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses'] and sum(r['implementation']=='handler-present' for r in status['skills'])==194 and sum(r['characterClass']=='Assassin' and r['implementation']=='handler-present' for r in status['skills'])==24
 assert not load(ROOT/'tools/reference/shadow-summons-preflight.json')['handlerEnabled']
 for name,needle in (('metadata-check.log','Item execution metadata:429complete current rows'),('executor-source-check.log','Skill execution:37680complete source'),('python-tests.log','Ran 71 tests'),('npc-source-check.log','NPC source controls:22432magic/14472rare'),('npc-catalogue-check.log','NPC items: 692 current bases'),('valkyrie-catalogue-check.log','Valkyrie: 18 current bases'),('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):assert needle in (STAGE/name).read_text(),name
 assert '\nOK\n' in (STAGE/'python-tests.log').read_text() and all(c['exitCode']==0 for c in load(STAGE/'verification-commands.json'))
 installation=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installation['installedArtifacts'])==106
 for p,h in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/p)==h,p
 protection=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
 for kind,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
  folder=Path(installation['runtimeRoot'])/'.local/server'/relative;assert len(protection[kind])==count and {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protection[kind]
 client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
 for kind in ('patchHashes','sharedInputs'):
  for p,h in client[kind].items():assert sha(ROOT/p)==h,p
 files=[Path(__file__).resolve(),ROOT/'tools/build_item_skill_execution.py',ROOT/'tests/check_item_execution_metadata.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/reference/item-skill-execution-metadata.json',ROOT/'tools/reference/shadow-summons-preflight.json']
 files.extend(ROOT/p for p in changed('sourceInputs'));files.extend(ROOT/p for p in source['artifacts'])
 files.extend(ROOT/'docs'/n for n in ('item-execution-metadata-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md'))
 for stage in (STAGE,WIDE):files.extend(p for p in stage.iterdir() if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json')
 files.extend(p for p in (STAGE/'failed-focused-1').rglob('*') if p.is_file())
 e=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=194,assassinHandlers=24,completedClasses=[],generatedNpcItems=False,genericNpcProcAdmission=False,
  scope='All current skill execution metadata and eight actual NPC do30 curse bodies through shared item/common-executor services;other NPC contexts/event admission remain gated.',artifacts=born['artifacts'],copiedData=born['copiedData'],focusedRun=2,broaderRun=1,contentBuild=1,integrationBuild=2,focusedMarkers=128,broaderMarkers=344,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
  currentSkillRows=429,currentMissileLinks=742,blankSkill=362,unresolvedAnimationRows=43,rawFieldsRetained=True,actualNpcCursesPerCohort=8,originalSourceRemovedCurseVictimsPerCohort=8,heldBlindStatesPerCohort=1,heldFearStatesPerCohort=1,actualConnectedBloodChecksPerCohort=3,retainedExecutorControlsPerBoot=37680,retainedCastControlsPerBoot=18912,retainedCallbackControlsPerBoot=25920,
  pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=fingerprints['named-item-runtime-audit.json'],affixFingerprints=fingerprints['first-loop-affix-audit.json'],protection=dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40),
  diagnostics=['Focused1passed all metadata assertions but could not find independent ground at fixture row3900. Inputs/log/binaries/producer preserved;focused2uses established row3160. No production-code change for this fixture correction.'],
  limits=['Primary source tables retain every raw field and all direct missile links. Known animation codes use the pinned older enum;43missing modes stay explicit null. Current separate delay fields are not silently mapped to the older single-delay model. Ordinary player finish rejects unresolved animation/delay projections.',
   'Eight current do30 curses supply actual existing area/replacement/physical/elemental/damage/slow/heal/return/blind/fear services with NPC creator and rank. The protected synchronous native item frame has no selected ordinary D2 skill entry;original D2 used-skill/town/global scheduler/current executable/client parity remain unverified.',
   'Persistence fixtures extend expiry/AI deadlines and internalize original victims,then remove the caster. Both actual reloads preserve those held original curse/AI/source states;this is not a natural-clock AI movement certificate. Generic event registration/order/lifecycle and all other NPC spell/stat/skill-list/room/missile/resource contexts remain unfinished.',
   'Confuse/do61,Attract,charge actions,full generated item initialization/fallback and Shadow/Valk actor/AI/casting/lifecycle/input remain separate. No new learned handler or completed class;no deployment/native/client/protocol/persistence-schema change. Separate effects/audio/tree/hotkey artwork remain identified placeholders.'],files={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))})
 out=ROOT/'tools/reference/item-execution-metadata-native-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Item execution metadata native receipt stale'
 else:out.write_text(encoded)
 print('Item execution metadata:128focused/344broader markers;429rows/742missiles/43explicit modes;8actual NPC curses and original source-removed held states through2reloads;71Python/audits/protected pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
