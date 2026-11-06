#!/usr/bin/env python3
"""Bind all thirty Barbarian scoped fixtures without claiming class completion."""
import argparse
from collections import Counter
from pathlib import Path
import sys
import skill_checkpoint_evidence as e
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'barbarian-current';FOCUS=BASE/'barbarian-current-potion-live'
PARENT='3cce1eb5ea5756ed1b4415892c567959bf11d1f2'
sys.path.insert(0,str(ROOT/'tests'))
from check_barbarian_current import configuration

def expected():
 c=configuration();counts=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
  'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
 counts.update({n:3 for n in c['extra_checks']})
 for family in c['extra_families']:counts.update({family+'-check':1,family+'-reload-check':2})
 for command in c['extra_target_input_commands']:counts.update({command+'-check':3,command+'-result':3})
 for command,result in c['extra_wait_results'].items():counts[result]+=counts[command]
 counts.update({'find-item-snapshot-check':3,'find-potion-snapshot-check':3})
 return counts

def record(check=False):
 # One class cohort and one distinct natural Potion cohort,not two retained suites.
 born=e.inventory(STAGE,FOCUS,STAGE/'source-snapshot-1.json')
 parent=e.load(ROOT/'tools/reference/leap-attack-native-evidence.json')
 oldpath=BASE/'leap-attack/birth-inputs.json';assert e.sha(oldpath)==parent['files'][str(oldpath.relative_to(ROOT))]
 old=e.load(oldpath)
 oldcs={p:h for p,h in old['sourceInputs'].items()if p.endswith('.cs')}
 newcs={p:h for p,h in born['sourceInputs'].items()if p.endswith('.cs')}
 assert set(oldcs)==set(newcs)and {p for p in oldcs if oldcs[p]!=newcs[p]}=={'tests/Sanctuary.Integration/FindPotionChecks.cs'}
 assert old['copiedData']==born['copiedData']
 for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll','Assemblies/Sanctuary.Content.dll']:
  assert old['artifacts'][name]==born['artifacts'][name]
 for kind in ['content','integration']:
  path=BASE/'leap-attack'/kind/f'Sanctuary.{kind.title()}.dll'
  assert e.sha(path)==old['artifacts']['Assemblies/'+path.name]
 assert sum(expected().values())==224
 log=e.cohort(STAGE,expected(),224)
 focus=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
  'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'find-potion-check':1,'find-potion-reload-check':2,
  'find-potion-live-check':3,'find-potion-live-result':3})
 e.cohort(FOCUS,focus,18)
 for stage,run in [(STAGE,5),(FOCUS,2)]:
  assert 'pass across3boots/2reloads'in(stage/f'native-run-{run}.log').read_text()
  text=(stage/'birth-native-checks.log').read_text()
  assert "denied by connection filter 'auto-denylist'"not in text
  assert text.count('Find Potion policy:5004complete legacy-source cases')==3
  assert text.count('Find Potion monster tiers:144real boundary/facet/type casts')==1
  potion=e.load(stage/'find-potion-fixtures.json');assert potion['Owner']and potion['Used']and potion['Pending']and potion['Loot']
 assert log.count('Leap landing native damage notification:')==9
 assert log.count('Leap Attack SQ14:1620initial and1080absolute')==3
 for family in ['frenzy','double-swing','barbarian-binding']:
  assert e.load(STAGE/(family+'-fixtures.json'))['Generation']==2
 assert all(s['Generation']==2 for s in e.load(STAGE/'leap-attack-fixtures.json'))
 build=(STAGE/'integration-build-1.log').read_text();assert all(s in build for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 for folder in ['failed-1','failed-2']:
  assert 'Sequence contains more than one matching element'in(STAGE/folder/'birth-native-checks.log').read_text()
 assert "denied by connection filter 'auto-denylist'"in(STAGE/'failed-3/birth-native-checks.log').read_text()
 assert 'No Find Potion corridor'in(STAGE/'failed-4/birth-native-checks.log').read_text()
 assert sum(markers(FOCUS/'prepared-pass-1/birth-native-checks.log').values())==18
 prepared=(FOCUS/'prepared-pass-1/check_barbarian_potion_live.py').read_text()
 assert prepared.replace("  extra_checks=('find-potion-snapshot-check',) if False else (),\n",'')==(ROOT/'tests/check_barbarian_potion_live.py').read_text()
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in tests and '\nOK\n'in tests
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');skills=[s for s in status['skills']if s['characterClass']=='Barbarian']
 assert len(skills)==30 and all(s['implementation']=='handler-present'and not s['complete']for s in skills)
 assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 matrix=(ROOT/'docs/barbarian-coverage-matrix.md').read_text()
 for s in skills:assert matrix.count('| '+s['name']+' |')==1,s['talentId']
 decisions=[ROOT/'tools/reference'/n for n in ['double-swing-start-decision.json','frenzy-start-decision.json','grim-ward-context-decision.json','leap-attack-landing-decision.json']]
 assert all(e.load(p)['approved']for p in decisions)
 files=e.stage_files(STAGE,FOCUS)+[Path(__file__).resolve(),ROOT/'tests/check_barbarian_current.py',ROOT/'tests/check_barbarian_potion_live.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tools/reference/leap-attack-native-evidence.json',oldpath,*decisions]
 for folder in [*(STAGE/f'failed-{n}'for n in range(1,5)),FOCUS/'prepared-pass-1']:
  files.extend(p for p in folder.rglob('*')if p.is_file())
 files += [ROOT/'docs'/p for p in ['barbarian-current-acceptance-checkpoint.md','barbarian-coverage-matrix.md','barbarian-class-roadmap-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,barbarianHandlers=30,completedClasses=[],
  nativeCohorts=2,currentClassMarkers=224,potionLiveMarkers=18,currentClassRun=5,potionLiveRun=2,
  contentBuild='unchanged leap-attack Content2 (production1)',integrationBuild=1,sourceSnapshot=1,auditRun=1,
  classFixtureFamilies=24,perBootChecks=9,wireScenariosPerBoot=11,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,
  currentExecutableParity=False,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,
  protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],
  skills=[s['talentId']for s in skills],
  scope=['All30existing Barbarian talents have scoped fixtures in one combined current-class cohort.11actual mouse/quick scenarios per boot cover DoubleSwing/Frenzy/Ward/LeapAttack;other families retain controlled casts/natural loops and original saved-state checks.',
   'Only C#change is the FindPotion fixture arena. Production content/shared/balance and native kernel match the accepted parent byte-for-byte. Potion isolated cohort additionally checks natural40ms release after start payment and loss of release mana,one attempt,monster-level tiers,loot and original saves.',
   'Current source/native controls,original item/resource regressions,approval records,573named decisions and installed protection remain explicit. Some older fixtures re-seed after validating prior saves;no universal same-identity-through-all-boots claim.'],
  limits=['Exact modern timing/IAS/WSM/ownership/RNG/snapshots,legacy Bash flat-field health total,FindItem chance/special treasure contexts,terrain/control/gear/recipient boundaries,PvP/multiplayer and rendered client remain unfinished. No class certificate or new handler.',
   'Failed1/2expose retained TCP sockets until drain-thread shutdown;failed3exposes an unnecessary silent fixture connection and disposable auto-denylist;failed4exhausts the narrow shared FindPotion arena. Tests now shut down sockets,use an opt-in settling delay and the shared independently fitted arena. No production filter or configuration was weakened.',
   'Parent317cross-class markers remain historical to that package,not freshly rerun. No deployment or final effects/audio/tree/hotkey icons;identified placeholders remain.'])
 e.write('barbarian-current-native-evidence.json',result,files,check)
 print('Barbarian current acceptance:30mapped talents,224combined/18naturalPotion markers,two reloads each;71Python,unchanged production/573decisions;no class complete.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
