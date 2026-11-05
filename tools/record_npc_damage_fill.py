#!/usr/bin/env python3
"""Strict acceptance of full NPC fill orchestration and retained original-item consumers."""
import argparse
from pathlib import Path
from collections import Counter
import sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/npc-damage-fill';COMPACT=ROOT/'.local/class-skills/npc-damage-fill-compact';SHARED=ROOT/'.local/class-skills/npc-damage-fill-shared';PARENT='6c12fdd84dd0eb3477294d64c8513b350e30fad3'
sys.path.insert(0,str(ROOT/'tests'))
from check_npc_damage_fill import configuration
CHANGED=['src/Sanctuary.Content/'+n for n in('NpcDamageFill.cs','GeneratedNpcFillServices.cs','GeneratedNpcWeaponFrame.cs','NpcEquipmentDamage.cs','NpcEquipmentElements.cs','WeaponPoisonPackets.cs','NamedCombat.cs')]+['tests/Sanctuary.Integration/NpcDamageFillChecks.cs']
def build(check=False):
 born=e.load(COMPACT/'birth-inputs.json');audit=e.load(STAGE/'audit-inputs.json');other=e.load(SHARED/'birth-inputs.json')
 assert born['artifacts']==audit['artifacts']==other['artifacts'] and born['copiedData']==audit['copiedData']==other['copiedData']
 for path in(COMPACT/'birth-inputs.json',SHARED/'birth-inputs.json',STAGE/'audit-inputs.json'):
  snapshot(STAGE/'pre-build-4.json',path)
  for p,h in e.load(path)['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
 for kind in('Content','Integration'):assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 for kind in('content','integration'):assert all(t in(STAGE/(kind+'-build-4.log')).read_text()for t in('Build succeeded.','0 Warning(s)','0 Error(s)'))
 source=e.load(ROOT/'tools/reference/npc-damage-fill-source-evidence.json');assert source['cases']==5120 and len(source['completeRoutineHashes'])==6 and len(source['statBindings'])==36 and not source['currentExecutableParity']
 assert source['serviceBindings']==dict(goodAlignment=2,attackedInMelee=3,blunt=57,criticalResult=8192,eventSkipResult=32)
 for group in('inputs','artifacts'):
  for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
 sourcecases=e.load(ROOT/'.local/class-skills/npc-damage-fill-source/cases.json');assert len(sourcecases)==5120
 bridge=e.load(COMPACT/'bridge-evidence.json');assert bridge['contexts']==96 and bridge['generation']==2 and bridge['completeRoutineHashes']==source['completeRoutineHashes']
 for p,h in bridge['artifacts'].items():assert e.sha(ROOT/p)==h,p
 config=configuration(compact=True);families=('iron-golem-items','npc-resource-callbacks',*config['extra_families'])
 wanted=Counter({'iron-golem-birth-check':3})
 for name in families:wanted[name+'-check']+=1;wanted[name+'-reload-check']+=2
 compact=e.cohort(COMPACT,wanted,60);assert compact.count('NPC full damage fill:5120source contexts;96original-item bridge exports;')==3
 wanted=Counter({'iron-golem-birth-check':3,'venom-result':1,'venom-reload-result':2})
 for name in('iron-golem-items','npc-resource-callbacks','npc-equipment-elements','npc-equipment-poison','npc-equipment-critical','venom'):wanted[name+'-check']+=1;wanted[name+'-reload-check']+=2
 e.cohort(SHARED,wanted,24)
 saved=e.load(COMPACT/'npc-damage-fill-fixtures.json');original=e.load(COMPACT/'generated-npc-stats-fixtures.json');assert saved['Original']==original and original['Generation']==2
 assert len(original['Holders'])==1566 and len({r['Actor']for r in original['Holders']})==1566 and sum(len(r['Items'])for r in original['Holders'])==1674
 assert len(saved['Bridge'])==96
 live=e.load(COMPACT/'generated-npc-live-resources-fixtures.json');assert live['Generation']==2 and len(live['Actors'])==5
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==5 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
 audits=e.audits(STAGE,PARENT,born['artifacts']);protected=e.protection()
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tools/check_npc_damage_fill_bridge.py',ROOT/'tests/check_npc_damage_fill.py',ROOT/'tests/check_npc_damage_fill_shared.py',ROOT/'tools/reference/npc-damage-fill-source-evidence.json']+[ROOT/p for p in CHANGED]
 for group in('inputs','artifacts'):files += [ROOT/p for p in source[group]]
 files += [ROOT/p for p in bridge['artifacts']]
 files += [ROOT/'docs'/n for n in('npc-damage-fill-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-mechanical-reference.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md','pet-adjustment-review.md')]
 files += e.stage_files(STAGE,COMPACT,SHARED)
 record=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=4,integrationBuild=4,sourceSnapshot=4,compactRun=4,sharedRun=4,fullSourceContexts=5120,completeOriginalRoutines=6,headerBoundStatIds=36,packetFields=21,originalItemBridgeContexts=96,originalNativeHolders=1566,originalNativeItems=1674,nativePayloadRestoredFromManifest=False,compactMarkers=60,sharedMarkers=24,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,retainedPhysicalRollsPerBoot=294840,retainedMasteryControls=160,pythonTests=71,verificationCommands=5,auditFingerprints=audits,protection=protected,
 scope='Complete older ordinary FillDamageValues orchestration reuses existing physical/element/poison/critical helpers and original generated-item selected-hand graph. Six unchanged routines with independently header-bound stats/flags/elements and declared stat,metadata,target,event,RNG services match5120contexts:all21packet fields,armor and RNG/entry/event/overlay/restore traces.96native original-item contexts after two actual reloads match a separate full-source replay,using previously qualified selected endpoints/coefficients/mastery/type copies as explicit services. Original identities,raw item payloads and resource pools retained. Actual elemental,poison,critical and player Venom consumers pass separate24marker cohort.',
 limits=['Allocation and explicit suppliers only:recipient mitigation,HP/drain/status delivery and real attacked-event consumers,actual learned actor/controller/primary identity and mutable item flags remain separate. No class completed.',
 'Older complete-source caller/math plus pinned current data is not current executable/global RNG parity or a full stat-engine executable model. Valid domains are checked;unknown mechanics are not inferred from appearance.',
 'Bridge compares overlay metadata,not rendered assets;entry/restore topology is covered by native and retained42routine hand graph checks. No unproved sunder300delivery or unreadable Warlock callback behavior.',
 'The pinned older body comments out burn-stat collection. Keep that gap explicit;initial burn/conversion metadata does not establish burn-stat collection or timed damage delivery.',
 'Prior native3passed60+24markers;final4adds zero-cold source/ordinary packet regression without changing production. Failed native1:fixture falsely declared unarmed actor blunt;failed native2:fixture omitted required graph change callback. Production unchanged between builds1/2/3/4;preview graph explicitly restores before topology comparison.',
 'No deployment,installed build/save/config/client change,new effect/audio or icon identity. Existing placeholders and paired Tekagi visual preview remain separately tracked.'])
 e.write('npc-damage-fill-native-evidence.json',record,files,check)
 print('Full NPC fill:5120source contexts/96original-item source replay;60+24native markers/two reloads;71Python/audits/protection pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
