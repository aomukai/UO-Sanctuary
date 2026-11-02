#!/usr/bin/env python3
"""Record shared player contact and Sacrifice target phase evidence without class completion."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sacrifice-contact'
PARENT='4654010fd8ec6cf31549b5d54b8380701ac988de'
sha=inventory.sha

def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def passed(path,total,boots):
 text=path.read_text();counts=markers(path)
 assert sum(counts.values())==total and ': failed' not in text and ' ERR]' not in text
 assert text.count('Shutting down')==text.count('Writing world save snapshot done')==boots
 return text,counts

def record(check=False):
 inventory.STAGE=STAGE
 tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-4.json',STAGE/'birth-inputs.json')
 log,counts=passed(STAGE/'birth-native-checks.log',809,3)
 assert 'pass across3boots/2reloads' in (STAGE/'full-run-4.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_sacrifice_contact import configuration
 cfg=configuration(True);assert len(cfg['extra_families'])==69 and len(cfg['extra_checks'])==12 and len(cfg['extra_target_input_commands'])==69
 for name in cfg['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in cfg['extra_checks']:assert counts[name]==3,name
 for name in cfg['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 for phrase in ('Sacrifice contact:pre-HP notification after self-cost;source and unclassified-native lethal retaliation before target death;real recovery/RIP entitlement;miss/block flat return;captured Sapphire cold/Wormskull poison;3pre-roll refusals',
   'Poison Dagger contact:real pre-HP notification/direct player credit/knife hit class'):
  assert log.count(phrase)==3,phrase
 focus=STAGE/'focused-accepted-4';fi=inventory.validate_inventory(focus/'birth-inputs.json');passed(focus/'birth-native-checks.log',39,3)
 assert fi['artifacts']==tested['artifacts'] and fi['copiedData']==tested['copiedData']
 for part in ('content','integration'):
  path=part+'/Sanctuary.'+part.title()+'.dll';assert sha(STAGE/path)==sha(focus/path)
  text=(STAGE/(part+'-build-4.log')).read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
 negative=STAGE/'negative-native-death';ni=json.loads((negative/'birth-inputs.json').read_text());text=(negative/'birth-native-checks.log').read_text()
 assert 'sacrifice-contact-check: failed' in text and 'Sacrifice must commit target HP before retaliation and defer target death until return' in text
 for name in ('PaladinSacrifice.cs','PoisonDaggerContact.cs'):
  assert (negative/name).read_bytes()==historical('src/Sanctuary.Content/'+name)
  assert sha(negative/name)==ni['sourceInputs']['src/Sanctuary.Content/'+name]
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 assert sha(negative/'SacrificeContactChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/SacrificeContactChecks.cs']
 assert 'Source hit-class longsword' in (STAGE/'failed-longsword-fixture/birth-native-checks.log').read_text()
 # The frozen native kernel is the predecessor's exact kernel;only the
 # content/integration assemblies and one status description changed here.
 prior=json.loads((ROOT/'.local/class-skills/sacrifice-allocation/birth-inputs.json').read_text())
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][name]==tested['artifacts'][name]
 assert prior['copiedData'].keys()==tested['copiedData'].keys()
 assert {p for p in tested['copiedData'] if tested['copiedData'][p]!=prior['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'));changed=[]
 for a,b in zip(now['skills'],old['skills'],strict=True):
  if a!=b:assert a['talentId']=='paladin.sacrifice';changed.append(a['talentId']);b['remaining']=a['remaining']
 assert changed==['paladin.sacrifice'] and now==old and not now['completedClasses']
 parent=ROOT/'tools/reference/sacrifice-allocation-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 from probe_player_hit_classes import build as hit_classes
 source=ROOT/'tools/reference/player-hit-class-source-evidence.json';assert json.loads(source.read_text())==hit_classes()
 audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
 assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
 assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text()
 fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  value=json.loads((STAGE/name).read_text());assert value==json.loads((ROOT/'tools/reference'/name).read_text())
  assert value['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for path,digest in value['InputHashes'].items():assert sha(ROOT/path)==digest,path
  previous=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in value.items() if k not in ignore}=={k:v for k,v in previous.items() if k not in ignore};fingerprints.append(len(value['InputHashes']))
 assert fingerprints==[906,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/'catalogue-checks.log').read_text()
 for value in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):
  assert value in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
 files=[Path(__file__).resolve(),ROOT/'tools/probe_player_hit_classes.py',ROOT/'tests/check_sacrifice_contact.py',parent,source,
  *(ROOT/'docs'/name for name in ('paladin-sacrifice-contact-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md')),
  *(STAGE/name for name in ('pre-build-4.json','birth-inputs.json','birth-native-checks.log','full-run-4.log','sacrifice-checks.json','audit-inputs.json','audit-native-checks.log','audit-run-4.log','python-tests.log','catalogue-checks.log','content-build-4.log','integration-build-4.log')),
  *(focus/name for name in ('birth-inputs.json','birth-native-checks.log','focused-run-4.log','sacrifice-checks.json')),
  *(negative/name for name in ('birth-inputs.json','birth-native-checks.log','PaladinSacrifice.cs','PoisonDaggerContact.cs','SacrificeContactChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(STAGE/'source'/name for name in ('tree.json','Units.cpp','Items.cpp','headers/source/D2Common/include/D2DataTbls.h','player-hit-class-probe.cpp')),
  *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Sacrifice ordered player-to-NPC target contact and shared Poison Dagger actor adapter',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=809,focusedMarkers=39,boots=3,reloads=2,saves=3,newServerWireActions=0,retainedServerWireActions=207,compiledHitClassGetterCases=112,pinnedWeaponClassRows=306,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['The prior content DLL fails target-death-before-retaliation regression. Shared PlayerWeaponContact now handles Sacrifice and Poison Dagger through ordered notification/credit/typed HP,kill recovery,attempt/retaliation and deferred death/return. Sacrifice self-cost remains before this target phase.',
   'Both mapped source and unclassified-native lethal exchanges preserve direct player credit and consume once. Unclassified UO recipients retain an explicit native reaction adapter without invented source class/base/mode/shield identity. Real recovery/RIP gear,miss/block flat return,captured Sapphire cold and Wormskull poison,pre-HP notification after self-cost and three pre-roll refusals pass.',
   'All306existing player weapon hit-class rows match the pinned owned tables;112unchanged legacy getter cases declare inventory/table services. Unarmed1,longsword3 and retained Poison Dagger knife6 native checks pass. These do not certify modern animation/primary selection.',
   'The same immutable build retains the full predecessor input/resource/save cohort,including207actual wire actions,three boots/two reloads/three saves. Four audits preserve decisions;71Python/573named/240catalogue/generators pass;106installed artifacts remain unchanged.'],
  remaining=['Supplementary older engine functions are not a modern executable certificate. Source geometry/RNG,player critical/deadly result bits/overlay,full item-event/kill-proc registration order,unclassified UO-to-D2 species conversion,converted throwing,PvP/special/charged/oskill,pending-action saves,old-content migration and graphical client parity remain separate.',
   'Unsupported native modifiers/Paragon/uninterruptible/death-delay recipients refuse before start RNG/cost. Region/callback mutation remains fail-fast and nontransactional;no silent rollback or missing subscriber is invented. Player-recipient damage retains its previous compatibility fixture path.',
   'Continue the Paladin review with Smite while retaining these individual Sacrifice gaps. Final blood/contact/block/return/audio and talent/hotkey art remain independent placeholders. No class completion,deployment or save reset.'])
 output=ROOT/'tools/reference/sacrifice-contact-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Sacrifice contact:809combined/39focused markers,207retained wire actions,2reloads,4audits/71Python,306pinned rows/112getter cases;106installed unchanged;no class completion.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
