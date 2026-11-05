#!/usr/bin/env python3
"""Record numeric resource callbacks on original generated equipped items."""
import argparse,json
from pathlib import Path
from record_generated_npc_stats import expected as previous_expected
from snapshot_skill_build import check as snapshot
import skill_checkpoint_evidence as evidence
ROOT=evidence.ROOT;STAGE=ROOT/'.local/class-skills/generated-npc-resources';COMPACT=ROOT/'.local/class-skills/generated-npc-resources-compact';PARENT='f371add1b1846746048f8ac16013727f370e4988'
CHANGED={'src/Sanctuary.Content/GeneratedNpcStatGraph.cs','src/Sanctuary.Content/NpcItemResourceCallbacks.cs','tests/Sanctuary.Integration/GeneratedNpcStatChecks.cs','tests/Sanctuary.Integration/GeneratedNpcResourceChecks.cs'}
def expected():
 r=previous_expected(False,True);r['generated-npc-resources-check']+=1;r['generated-npc-resources-reload-check']+=2;return r

def build(check=False):
 born=evidence.load(COMPACT/'birth-inputs.json');audit=evidence.load(STAGE/'audit-inputs.json');assert born['artifacts']==audit['artifacts']and born['copiedData']==audit['copiedData']
 for path in (COMPACT/'birth-inputs.json',STAGE/'audit-inputs.json'):
  snapshot(STAGE/'pre-build-2.json',path)
  for p,h in evidence.load(path)['sourceInputs'].items():assert evidence.sha(ROOT/p)==h,p
 for kind in ('Content','Integration'):assert evidence.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 old=evidence.load(ROOT/'.local/class-skills/generated-npc-stats/pre-build-1.json')['inputs'];now=evidence.load(STAGE/'pre-build-2.json')['inputs']
 assert {p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}==CHANGED
 assert old['projectInputs']==now['projectInputs']and old['copiedData']==now['copiedData']
 parent=evidence.load(ROOT/'tools/reference/generated-npc-stats-native-evidence.json')
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==parent['artifacts'][name]
 sourcepath=ROOT/'tools/reference/generated-npc-resources-source-evidence.json';source=evidence.load(sourcepath);cases=evidence.load(ROOT/'.local/class-skills/generated-npc-resources-source/cases.json')
 assert source['numericCompositions']==len(cases['numericOutputs'])==6264 and source['originalItemShapes']==len(cases['numericInputs'])==1566
 assert len(source['completeRoutineHashes'])==30 and not source['currentExecutableParity']
 log=evidence.cohort(COMPACT,expected(),48);assert log.count('Generated NPC resources:6264source item/resource compositions;540NPC branch controls;')==3
 saved=evidence.load(COMPACT/'generated-npc-resources-fixtures.json');original=evidence.load(COMPACT/'generated-npc-stats-fixtures.json');assert saved==original and saved['Generation']==2 and len(saved['Holders'])==1566
 assert len({r['Actor']for r in saved['Holders']})==1566 and sum(len(r['Items'])for r in saved['Holders'])==1674
 for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-2.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=evidence.audits(STAGE,PARENT,born['artifacts']);commands=evidence.load(STAGE/'verification-commands.json');assert len(commands)==5 and all(c['exitCode']==0 for c in commands)
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in text and '\nOK\n'in text;protected=evidence.protection()
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_generated_npc_resources.py',sourcepath]+[ROOT/p for p in CHANGED]
 for group in ('inputs','artifacts'):
  for p,h in source[group].items():assert evidence.sha(ROOT/p)==h,p
  files += [ROOT/p for p in source[group]]
 files += [ROOT/'docs'/n for n in ('generated-npc-resources-checkpoint.md','skill-mechanical-reference.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files += evidence.stage_files(STAGE,COMPACT)
 for folder in STAGE.glob('failed-*'):files += [p for p in folder.rglob('*')if p.is_file()]
 status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=2,integrationBuild=2,sourceSnapshot=2,compactRun=2,compactMarkers=48,boots=3,actualReloads=2,saves=3,sourceCompositions=6264,npcBranchControls=540,completeOriginalRoutines=30,unchangedResourceBranches=3,originalNativeHolders=1566,originalNativeItems=1674,nativePayloadRestoredFromManifest=False,pythonTests=71,verificationCommands=5,auditFingerprints=audits,protection=protected,
  scope='Opt-in source MAXHP/MAXMANA/MAXSTAMINA callbacks on original generated NPC item graphs preserve positive FP256 pool percentage,empty pools and source monster regeneration gates. Four resource variants and construction/repeat/level/removal match exact source flags,base/full arrays and callback order;original items retain identity/payload through two reloads. Existing unopted numeric graph callers retain prior behavior.',
  limits=['The three complete switch branches are not the complete outer server callback. Native live HP/mana/stamina maximum/setter synchronization,skill/event/aura/state/charge consumers and persistent mutable generated item state remain open. No learned actor or handler was enabled.',
   'Explicit fixture bases,day angle91 and monster regeneration coefficient0or3 do not convert UO attributes,establish world time or prove current executable/world/allocation RNG parity. Nonpositive next maxima with positive old/current and out-of-domain resource/regeneration writes reject before pool writes.',
   'Reload tests reconstruct numeric frames against original native items,not live saved resource profiles or historical D2 callback registration order. The family manifest is only an equality/identity witness;it never restores item payloads.',
   'Native run1passed the resource command but its harness then missed the required family witness file;preserved and excluded. Verification1failed stale parent audit fingerprints before refreshed audits were installed;preserved and excluded. Source1compiler diagnostics for missing retained read/write wrappers are preserved;source2uses four additional complete unchanged routines.',
   'No installed build/save/config/client change,deployment,new skill effect/audio or icon identity.'])
 evidence.write('generated-npc-resources-native-evidence.json',receipt,files,check)
 print('Generated NPC resources:6264compositions/30complete routines/3resource branches;540NPC controls;48compact markers/two reloads;71Python/5checks/audits/protection pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
