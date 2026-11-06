#!/usr/bin/env python3
"""Bind NPC item knockback packets, original actors and retained combat evidence."""
import argparse
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-item-knockback';WIDE=BASE/'npc-item-knockback-retained'
PARENT='03d5a1f6dc98cf5f36c1a20d505088bf58539155'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-8.json')
 old=e.load(BASE/'inferno-held-candidate/source-snapshot-4.json')['inputs'];current=e.load(STAGE/'source-snapshot-8.json')['inputs']
 assert current['sourceInputs'].keys()-old['sourceInputs'].keys()=={'src/Sanctuary.Content/NpcItemKnockback.cs','tests/Sanctuary.Integration/NpcItemKnockbackChecks.cs'}
 assert old['sourceInputs'].keys()<=current['sourceInputs'].keys()
 assert {p for p,h in old['sourceInputs'].items()if current['sourceInputs'][p]!=h}=={'tests/Sanctuary.Integration/ReviveEventChecks.cs',*('src/Sanctuary.Content/'+n for n in ('NpcCombatEvents.cs','NpcItemProcGraph.cs','ReviveEquipmentEvents.cs','IronGolemContact.cs'))}
 assert current['copiedData']==old['copiedData'] and current['projectInputs']==old['projectInputs']
 compiled=e.load(STAGE/'source-snapshot-6.json')['inputs']
 for prefix in ('src/','shared/'):
  assert {p:h for p,h in compiled['sourceInputs'].items()if p.startswith(prefix)}=={p:h for p,h in current['sourceInputs'].items()if p.startswith(prefix)}
 assert compiled['copiedData']==current['copiedData'] and compiled['projectInputs']==current['projectInputs']
 parent=e.load(BASE/'inferno-held-candidate/birth-inputs.json')
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]
 for kind,n in (('content',2),('integration',7)):
  assert all(s in(STAGE/f'{kind}-build-{n}.log').read_text()for s in('Build succeeded.','0 Warning(s)','0 Error(s)'))
 focused=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'npc-item-knockback-check':1,'npc-item-knockback-reload-check':2})
 prior=e.load(ROOT/'tools/reference/npc-item-fear-native-evidence.json');path=BASE/'npc-item-fear-retained/birth-native-checks.log';assert e.sha(path)==prior['files'][str(path.relative_to(ROOT))]
 retained=markers(path);assert sum(retained.values())==149;retained.update({'npc-item-knockback-check':1,'npc-item-knockback-reload-check':2})
 logs=[e.cohort(STAGE,focused,12),e.cohort(WIDE,retained,152)]
 for stage,run in ((STAGE,7),(WIDE,3)):
  assert 'pass across3boots/2reloads' in(stage/f'run-{run}.log').read_text()
  saved=e.load(stage/'npc-item-knockback-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==3 and sum(a['Revive']for a in saved['Actors'])==1
  assert all(a['Owner']and a['Source']and a['Item']and a['Target']and a['Motion']['Target']==a['Target']for a in saved['Actors'])
 assert all(log.count('NPC item knockback:24576source cases')==3 for log in logs)
 source=e.load(ROOT/'tools/reference/npc-item-knockback-source-evidence.json');assert source['contexts']==24576 and not source['currentExecutableParity']
 for group in ('inputs','artifacts'):
  for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protection=e.protection()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==5 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(r['implementation']=='handler-present'for r in status['skills'])==197 and not status['completedClasses']
 files=e.stage_files(STAGE,WIDE)
 for n,message in ((1,'Iceblink learned start refused'),(2,'That original item is not accessible within casting range.'),(3,'see=True los=False'),(4,'Invalid saved NPC proc graph')):
  folder=STAGE/f'failed-native-{n}';inputs=e.load(folder/'birth-inputs.json');assert message in(folder/'birth-native-checks.log').read_text()
  for path in folder.glob('*.cs'):
   prefix='tests/Sanctuary.Integration/'if path.name.endswith('Checks.cs')else'src/Sanctuary.Content/'
   assert e.sha(path)==inputs['sourceInputs'][prefix+path.name]
  for kind in ('Content','Integration'):
   path=folder/kind.lower()/f'Sanctuary.{kind}.dll';assert e.sha(path)==inputs['artifacts'][f'Assemblies/Sanctuary.{kind}.dll'];files.append(path)
  files+=e.stage_files(folder)+list(folder.glob('*.cs'))
 for folder,count in ((STAGE/'prior-focused-5',12),(STAGE/'prior-focused-6',12)):
  inputs=e.load(folder/'birth-inputs.json');log=(folder/'birth-native-checks.log').read_text()
  assert sum(markers(folder/'birth-native-checks.log').values())==count and ': failed'not in log and ' ERR]'not in log
  for path in folder.glob('*.cs'):
   assert e.sha(path)==inputs['sourceInputs']['tests/Sanctuary.Integration/'+path.name]
  for kind in ('Content','Integration'):
   path=folder/kind.lower()/f'Sanctuary.{kind}.dll';assert e.sha(path)==inputs['artifacts'][f'Assemblies/Sanctuary.{kind}.dll'];files.append(path)
  files+=e.stage_files(folder)+list(folder.glob('*.cs'))
 for folder,message in ((WIDE/'failed-retained-1','Unsupported registration was silently skipped: 81'),(WIDE/'failed-retained-2','npc-item-fear-live: failed')):
  inputs=e.load(folder/'birth-inputs.json');assert message in(folder/'birth-native-checks.log').read_text()
  for path in folder.glob('*.cs'):
   assert e.sha(path)==inputs['sourceInputs']['tests/Sanctuary.Integration/'+path.name]
  for kind in ('Content','Integration'):
   path=folder/kind.lower()/f'Sanctuary.{kind}.dll';assert e.sha(path)==inputs['artifacts'][f'Assemblies/Sanctuary.{kind}.dll'];files.append(path)
  files+=e.stage_files(folder)+list(folder.glob('*.cs'))+list(folder.glob('*.py'))
 files+=e.stage_files(STAGE/'stale-audit-verification-1')
 files+=[Path(__file__).resolve(),ROOT/'tools/probe_npc_item_knockback.py',ROOT/'tests/check_npc_item_knockback.py',ROOT/'tools/reference/npc-item-knockback-source-evidence.json',ROOT/'tools/reference/npc-item-fear-native-evidence.json']
 files +=[ROOT/'docs'/n for n in ('npc-item-knockback-checkpoint.md','necromancer-coverage-matrix.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,productionChanged=True,handlers=197,completedClasses=[],contentBuild=2,integrationBuild=7,sourceSnapshot=8,focusedRun=7,retainedRun=3,auditRun=4,sourceContexts=24576,focusedMarkers=12,retainedMarkers=152,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,originalActors=3,originalIronActors=2,originalRevives=1,pythonTests=71,auditFingerprints=fingerprints,protection=protection,artifacts=born['artifacts'],copiedData=born['copiedData'],
 scope=['Original Griswolds Edge learned Iron and frozen original Revive admit stat81/function07 with one raw low7draw and pinned size flags. Legacy and graph callbacks return the changed packet;the later ordinary return owns movement and immune-mode fallback.',
        'Actual ordered original-item contacts preserve one HP notification,source RNG,owner mana and bit8 through the shared return. Graph serialization admits its new innate event without changing the save layout. Original gear,wounds,seed and movement state survive two reloads;loaded movement advances without replaying damage.',
        'All149parent retained markers remain,including curse/input,lightning/freeze/fear and proc/contact scopes. No learned handler count or class-completion claim is added.'],
 limits=['Complete unchanged supplementary older callback is not current-executable/global event/RNG/geometry/PvP/client equivalence. Unknown native size metadata remains an explicit pre-contact refusal.',
         'Outgoing original-monster projectile producers and full retired-source/owner contexts remain separately bounded. A registered missile callback is not a certificate for every producer.',
         'Original sword floor LOS failed in earlier native fixture locations;production admission is unchanged. The later positive native site does not retroactively establish the individual cause of every earlier grouped refusal.',
         'Howltusk still has separate unsupported damage-to-mana;no item-property filtering is used. Existing movement/get-hit effects remain identified placeholders;no new skill/icon identity or deployment.'])
 e.write('npc-item-knockback-native-evidence.json',result,files,check)
 print('NPC item knockback:12focused/152retained,2reloads each,24576source contexts;original item/wounds/motion preserved;installed build unchanged.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
