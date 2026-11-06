#!/usr/bin/env python3
"""Bind original Howltusk admission and the NPC player-only damage-to-mana guard."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-damage-to-mana';WIDE=BASE/'npc-damage-to-mana-retained';PARENT='823691c1c713cc8af961500ef90caaa20861be67'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-4.json');old=e.load(BASE/'npc-item-knockback/source-snapshot-8.json')['inputs'];now=e.load(STAGE/'source-snapshot-4.json')['inputs']
 assert now['sourceInputs'].keys()-old['sourceInputs'].keys()=={'tests/Sanctuary.Integration/NpcDamageToManaChecks.cs'} and old['sourceInputs'].keys()<=now['sourceInputs'].keys()
 expected={*('src/Sanctuary.Content/'+n for n in ('NpcCombatEvents.cs','NpcItemProcGraph.cs','ReviveEquipmentEvents.cs')),*('tests/Sanctuary.Integration/'+n for n in ('NpcItemKnockbackChecks.cs','ReviveEventChecks.cs','ReviveEquippedChecks.cs','NpcItemBlindChecks.cs','IronGolemCastChecks.cs'))}
 assert {p for p,h in old['sourceInputs'].items()if now['sourceInputs'][p]!=h}==expected
 assert now['projectInputs']==old['projectInputs'] and now['copiedData'].keys()==old['copiedData'].keys()
 assert {p for p,h in old['copiedData'].items()if now['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 compiled=e.load(STAGE/'source-snapshot-2.json')['inputs']
 assert {p:h for p,h in compiled['sourceInputs'].items()if p.startswith(('src/','shared/'))}=={p:h for p,h in now['sourceInputs'].items()if p.startswith(('src/','shared/'))}
 assert compiled['copiedData']==now['copiedData'] and compiled['projectInputs']==now['projectInputs']
 baseline=e.load(BASE/'npc-item-knockback/birth-inputs.json')
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==baseline['artifacts'][n]
 for kind,n in (('content',2),('integration',4)):
  assert all(s in(STAGE/f'{kind}-build-{n}.log').read_text()for s in('Build succeeded.','0 Warning(s)','0 Error(s)'))
 focused=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'npc-damage-to-mana-check':1,'npc-damage-to-mana-reload-check':2})
 receipt=e.load(ROOT/'tools/reference/npc-item-knockback-native-evidence.json');p=BASE/'npc-item-knockback-retained/birth-native-checks.log';assert e.sha(p)==receipt['files'][str(p.relative_to(ROOT))]
 retained=markers(p);assert sum(retained.values())==152;retained.update({'npc-damage-to-mana-check':1,'npc-damage-to-mana-reload-check':2})
 logs=[e.cohort(STAGE,focused,12),e.cohort(WIDE,retained,155)]
 for stage,run in ((STAGE,4),(WIDE,3)):
  assert 'pass across3boots/2reloads'in(stage/f'run-{run}.log').read_text();saved=e.load(stage/'npc-damage-to-mana-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==3 and sum(a['Revive']for a in saved['Actors'])==1
  assert all(a['OwnerMana']==257 and a['Owner']and a['Source']and a['Item']and a['Target']==a['Motion']['Target']and a['Curse']['Curse']['Ability']==86 for a in saved['Actors'])
 assert all(log.count('NPC damage-to-mana:4032source cases/2688nonplayer guards')==3 for log in logs)
 source=e.load(ROOT/'tools/reference/npc-damage-to-mana-source-evidence.json');assert source['contexts']==4032 and len(source['completeRoutineHashes'])==2 and not source['currentExecutableParity']
 for group in ('inputs','artifacts'):
  for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
 iso=BASE/'npc-damage-to-mana-iron';extra=e.load(iso/'birth-inputs.json')
 assert extra['artifacts']==born['artifacts'] and extra['copiedData']==born['copiedData']
 e.snapshot(STAGE/'source-snapshot-4.json',iso/'birth-inputs.json')
 for p,h in extra['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
 isolate=Counter({k:v for k,v in focused.items()if not k.startswith('npc-damage-to-mana')});isolate.update({'iron-golem-cast-check':1,'iron-golem-cast-reload-check':2})
 e.cohort(iso,isolate,12);assert 'pass across3boots/2reloads'in(iso/'run-1.log').read_text()
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protection=e.protection()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==6 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(r['implementation']=='handler-present'for r in status['skills'])==197 and not status['completedClasses']
 ring=e.load(ROOT/'tools/reference/ring-of-fire-preflight.json');assert not ring['handlerEnabled']and ring['oldSourceDo154Null']and ring['oldSourceDamage17Null']and ring['rankOneMana256']==2048
 files=e.stage_files(STAGE,WIDE,iso)
 failed=STAGE/'failed-native-1';assert 'Unmodified Howltusk totals changed'in(failed/'birth-native-checks.log').read_text()
 for folder in (failed,STAGE/'prior-focused-2',WIDE/'failed-retained-1',STAGE/'prior-focused-3',WIDE/'failed-retained-2'):
  inputs=e.load(folder/'birth-inputs.json')
  for p in folder.glob('*.cs'):assert e.sha(p)==inputs['sourceInputs']['tests/Sanctuary.Integration/'+p.name]
  for kind in ('Content','Integration'):
   p=folder/kind.lower()/f'Sanctuary.{kind}.dll';assert e.sha(p)==inputs['artifacts'][f'Assemblies/Sanctuary.{kind}.dll'];files.append(p)
  files+=e.stage_files(folder)+list(folder.glob('*.cs'))
 assert 'That named definition is not available.'in(WIDE/'failed-retained-1/birth-native-checks.log').read_text()
 assert sum(markers(STAGE/'prior-focused-2/birth-native-checks.log').values())==12
 assert 'Unsupported Howltusk callback was discarded'in(WIDE/'failed-retained-2/birth-native-checks.log').read_text()
 files +=[Path(__file__).resolve(),ROOT/'tools/probe_npc_damage_to_mana.py',ROOT/'tests/check_npc_damage_to_mana.py',ROOT/'tools/reference/npc-damage-to-mana-source-evidence.json',ROOT/'tools/reference/npc-item-knockback-native-evidence.json',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/record_ring_of_fire_preflight.py',ROOT/'tools/reference/ring-of-fire-preflight.json']
 files +=[ROOT/'docs'/n for n in ('npc-damage-to-mana-checkpoint.md','warlock-ring-of-fire-preflight.md','necromancer-coverage-matrix.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,productionChanged=True,installed=False,handlers=197,completedClasses=[],contentBuild=2,integrationBuild=4,sourceSnapshot=4,focusedRun=4,retainedRun=3,auditRun=4,focusedMarkers=12,retainedMarkers=155,sourceContexts=4032,nonPlayerSourceContexts=2688,completeSourceRoutines=2,originalActors=3,originalIronActors=2,originalRevives=1,isolatedLearnedIronMarkers=12,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,auditFingerprints=fingerprints,protection=protection,artifacts=born['artifacts'],copiedData=born['copiedData'],
 scope=['Complete unchanged callback13/percentage source keeps all NPC contexts ahead of stat/resource/overlay services. Actual ordinary and graph NPC registrations retain original stat114 without forwarding mana to the owner.',
        'Unmodified Howltusk original learned Iron legacy/graph and frozen original Revive casts retain their other properties. Combined outgoing fear/knockback precedes/follows the one HP commitment in their respective phases;incoming player-only events leave pet/owner resources and RNG unchanged.',
        'Original item identity,wounds,mana,seed,curse and movement persist through two reloads. All152parent retained markers remain;the former unsupported Howltusk examples now use available Deathspade and keep its unimplemented mana-after-kill refusal.',
        'Ring of Fire primary tables and new do154/damage17 gates are recorded separately;generic Nova or item knockback are not substituted.'],
 limits=['Older source and scoped native adapters do not certify current executable/global events,full world geometry,retired-source/PvP/client or complete-class parity. Player damage-to-mana/shield order is not modified or newly certified.',
         'No new art/audio/icon identity,save format,deployment or learned handler is introduced. Inferno adaptation question remains pending;no response is not approval.'])
 e.write('npc-damage-to-mana-native-evidence.json',result,files,check)
 print('Howltusk NPC guard:12focused/155retained,2reloads each,4032source cases;original gear/resources/fear/movement preserved;installed build unchanged.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
