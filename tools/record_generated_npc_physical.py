#!/usr/bin/env python3
"""Bind selected-hand physical rolls to source controls and original native items."""
import argparse
from pathlib import Path
from record_generated_npc_resources import expected as previous_expected
from snapshot_skill_build import check as snapshot
import skill_checkpoint_evidence as e
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/generated-npc-physical';COMPACT=ROOT/'.local/class-skills/generated-npc-physical-compact';PARENT='2e1f47be93c37ae7f2cba21f320407c349efaf31'
CHANGED={'src/Sanctuary.Content/'+n+'.cs'for n in('CurrentItemInitialization','GeneratedNpcEquipment','GeneratedNpcWeaponFrame','NpcItemAffixes','SourceEquipPlacement','SourceWeaponMastery')}|{'tests/Sanctuary.Integration/GeneratedNpcPhysicalChecks.cs'}
def build(check=False):
 born=e.load(COMPACT/'birth-inputs.json');audit=e.load(STAGE/'audit-inputs.json');assert born['artifacts']==audit['artifacts']and born['copiedData']==audit['copiedData']
 for path in(COMPACT/'birth-inputs.json',STAGE/'audit-inputs.json'):
  snapshot(STAGE/'pre-build-2.json',path)
  for p,h in e.load(path)['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
 for kind in('Content','Integration'):assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 old=e.load(ROOT/'.local/class-skills/generated-npc-weapons/pre-build-1.json')['inputs'];now=e.load(STAGE/'pre-build-2.json')['inputs']
 assert {p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}==CHANGED
 assert old['projectInputs']==now['projectInputs']and old['copiedData']==now['copiedData']
 parent=e.load(ROOT/'tools/reference/generated-npc-weapons-native-evidence.json')
 for name in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==parent['artifacts'][name]
 sourcepath=ROOT/'tools/reference/generated-npc-physical-source-evidence.json';source=e.load(sourcepath);cases=e.load(ROOT/'.local/class-skills/generated-npc-physical-source/cases.json')
 assert len(source['completeRoutineHashes'])==42 and source['compositions']==len(cases['hands'])==len(cases['numericOutputs'])==3276
 assert source['physicalRollComparisons']==3276*5*18==294840 and source['masteryControls']==len(cases['mastery'])==160
 assert sum(a['Case']>=1548 for a in cases['hands'])==1728
 prior=e.load(ROOT/'tools/reference/generated-npc-weapons-source-evidence.json')
 for name,h in prior['completeRoutineHashes'].items():assert source['completeRoutineHashes'][name]==h,name
 assert all(r[4]==0 for r in cases['mastery']if r[3]==2),'Positive layer40 must not bypass the source first32 copy limit'
 wanted=previous_expected()
 for family in('generated-npc-live-resources','generated-npc-weapons','generated-npc-physical'):wanted[family+'-check']+=1;wanted[family+'-reload-check']+=2
 log=e.cohort(COMPACT,wanted,57);assert log.count('Generated NPC physical:3276source item/hand compositions;294840physical rolls and RNG chronology;160layered mastery controls;')==3
 saved=e.load(COMPACT/'generated-npc-physical-fixtures.json');original=e.load(COMPACT/'generated-npc-stats-fixtures.json');assert saved==original and saved['Generation']==2 and len(saved['Holders'])==1566
 assert len({r['Actor']for r in saved['Holders']})==1566 and sum(len(r['Items'])for r in saved['Holders'])==1674
 live=e.load(COMPACT/'generated-npc-live-resources-fixtures.json');assert live['Generation']==2 and len(live['Actors'])==5
 for kind in('content','integration'):assert all(t in(STAGE/(kind+'-build-2.log')).read_text()for t in('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=e.audits(STAGE,PARENT,born['artifacts']);commands=e.load(STAGE/'verification-commands.json');assert len(commands)==5 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text();protected=e.protection()
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_generated_npc_physical.py',sourcepath]+[ROOT/p for p in CHANGED]
 for group in('inputs','artifacts'):
  for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
  files += [ROOT/p for p in source[group]]
 files += [ROOT/'docs'/n for n in('generated-npc-physical-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md','pet-adjustment-review.md')]
 files += e.stage_files(STAGE,COMPACT)+[p for d in('source-attempt-1','native-attempt-1')for p in(STAGE/d).rglob('*')if p.is_file()]
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=2,integrationBuild=2,sourceSnapshot=2,compactRun=2,compactMarkers=57,boots=3,actualReloads=2,saves=3,sourceCompositions=3276,multiItemCompositions=1728,physicalRollsPerBoot=294840,completeOriginalRoutines=42,layeredMasteryControls=160,originalNativeHolders=1566,originalNativeItems=1674,nativePayloadRestoredFromManifest=False,pythonTests=71,verificationCommands=5,auditFingerprints=audits,protection=protected,
  scope='Original selected native item graphs now supply cached physical endpoints,normal damage,actor/additional/mastery percent,source signed-short weapon coefficients,strength/dexterity and endpoint percentages to existing NpcEquipmentDamage.Roll256. Full source mastery reads retain melee/throw choice,positive maximum and first32-before-type-match. All arrays,selected identities,callbacks,physical results and exact bounded RNG calls compare through two reloads;resource pools and original item payloads remain unchanged.',
  limits=['Ordinary getStats1/initialDamage0 only;explicit caller primary/secondary,used-skill/row/first-item-type/range/phase/flags/additional percent/source share. This is not persistent primary or actual NPC controller/combat admission.',
   'Source comprises42complete older routines with current item facts,not current executable/global RNG/client parity. Valid inventory/record/type services are explicit. Player/modern Warlock hand policy is not certified.',
   'Elemental/poison/critical/recipient mitigation/HP/event composition and full skill/rate/resistance consumers,mutable item flags and actual learned summons remain separate. Current Sunder300encoding and unreadable Warlock callbacks remain nonblocking gaps.',
   'Native attempt1exceeded the60second command deadline without a completed comparison. Frame-local immutable metadata caching removes repeated decoding while retaining every entry freshness guard;five transferred-item refusals are checked. Accepted Content2/Integration2/source snapshot2are frozen.',
   'Source attempt1passed;attempt2strengthened the first32 truncation control by keeping40nonzero entries. Later positive layer40cannot be considered. Both original routine bodies are unchanged.',
   'No new handler/completed class,deployment,installed build/save/config/client change,new skill effect/audio or icon identity.'])
 e.write('generated-npc-physical-native-evidence.json',receipt,files,check)
 print('Generated NPC physical:294840rolls/160mastery controls;42complete routines;57native markers/two reloads;71Python/audits/protection pass;no learned actor admission.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
