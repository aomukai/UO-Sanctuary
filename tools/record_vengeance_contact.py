#!/usr/bin/env python3
"""Bind Vengeance windup allocation and ordered elemental contact to native evidence."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/vengeance-contact'
PARENT='0ce3ab4de165e7748259dd0e815905cdc47bfb91'
sha=inventory.sha

def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def passed(path,total,boots):
 text=path.read_text();counts=markers(path)
 assert sum(counts.values())==total and ': failed' not in text and ' ERR]' not in text
 assert text.count('Shutting down')==text.count('Writing world save snapshot done')==boots
 return text,counts

def record(check=False):
 inventory.STAGE=STAGE
 tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-2.json',STAGE/'birth-inputs.json')
 log,counts=passed(STAGE/'birth-native-checks.log',851,3)
 assert 'pass across3boots/2reloads' in (STAGE/'full-run-2.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_vengeance_contact import configuration
 cfg=configuration(True);assert len(cfg['extra_families'])==69 and len(cfg['extra_checks'])==14 and len(cfg['extra_target_input_commands'])==75
 for name in cfg['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in cfg['extra_checks']:assert counts[name]==3,name
 for name in cfg['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 phrase='Vengeance contact:hit/physical/element/item windup order and four-lane golden;cached mitigation/hard synergy;successful32/48/64cycle;miss/block flat return;6cancellation/target cases;source/native deferred death and credit;real Sapphire/Wormskull;physical-only leech/Life Tap;3pre-roll refusals'
 assert log.count(phrase)==3
 assert counts['vengeance-check']==1 and counts['vengeance-reload-check']==2
 for name in ('smite-contact-check','sacrifice-contact-check','poison-dagger-contact-check'):assert counts[name]==3
 focus=STAGE/'focused-accepted-2';fi=inventory.validate_inventory(focus/'birth-inputs.json');passed(focus/'birth-native-checks.log',88,3)
 assert fi['artifacts']==tested['artifacts'] and fi['copiedData']==tested['copiedData']
 for part in ('content','integration'):
  path=part+'/Sanctuary.'+part.title()+'.dll';assert sha(STAGE/path)==sha(focus/path)
  text=(STAGE/(part+'-build-2.log')).read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
 negative=STAGE/'negative-release-allocation';ni=json.loads((negative/'birth-inputs.json').read_text());text=(negative/'birth-native-checks.log').read_text()
 assert 'vengeance-contact-check: failed' in text and 'Vengeance must allocate hit and elemental roll at windup without committing resources or HP' in text
 for name in ('PaladinVengeance.cs','PaladinCombat.cs','ClassCombat.cs','GearEffects.cs'):
  assert (negative/name).read_bytes()==historical('src/Sanctuary.Content/'+name)
  assert sha(negative/name)==ni['sourceInputs']['src/Sanctuary.Content/'+name]
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 assert sha(negative/'VengeanceContactChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/VengeanceContactChecks.cs']
 prior=json.loads((ROOT/'.local/class-skills/smite-contact/birth-inputs.json').read_text())
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][name]==tested['artifacts'][name]
 assert prior['copiedData'].keys()==tested['copiedData'].keys()
 assert {p for p in tested['copiedData'] if tested['copiedData'][p]!=prior['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'));changed=[]
 for a,b in zip(now['skills'],old['skills'],strict=True):
  if a!=b:assert a['talentId']=='paladin.vengeance';changed.append(a['talentId']);b['remaining']=a['remaining']
 assert changed==['paladin.vengeance'] and now==old and not now['completedClasses']
 parent=ROOT/'tools/reference/smite-contact-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 source=ROOT/'tools/reference/vengeance-engine-evidence.json';assert source.read_bytes()==historical(str(source.relative_to(ROOT)))
 probe=ROOT/'.local/class-skills/vengeance-source-probe.json';source_record=json.loads(source.read_text());assert json.loads(probe.read_text())==source_record['probe']['data']
 assert len(source_record['probe']['data']['elementCases'])==72 and source_record['probe']['data']['guards']==[0,0]
 pal=ROOT/'.local/class-skills/SkillPal.cpp.reference';assert sha(pal)=='c421857a633b5dd1f441eded1c5f41cbd88b375ba9dd771c3fa802e39591bdf0'
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
 files=[Path(__file__).resolve(),ROOT/'tests/check_vengeance_contact.py',parent,source,probe,pal,ROOT/'tools/probe_vengeance_reference.py',
  *(ROOT/'docs'/name for name in ('paladin-vengeance-contact-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md')),
  *(STAGE/name for name in ('pre-build-2.json','birth-inputs.json','birth-native-checks.log','full-run-2.log','vengeance-checks.json','smite-checks.json','sacrifice-checks.json','audit-inputs.json','audit-native-checks.log','audit-run-2.log','python-tests.log','catalogue-checks.log','content-build-2.log','integration-build-2.log')),
  *(focus/name for name in ('birth-inputs.json','birth-native-checks.log','focused-run-2.log','vengeance-checks.json','smite-checks.json','sacrifice-checks.json')),
  *(negative/name for name in ('birth-inputs.json','birth-native-checks.log','PaladinVengeance.cs','PaladinCombat.cs','ClassCombat.cs','GearEffects.cs','VengeanceContactChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Vengeance start allocation,feedback cycle and ordered elemental contact',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=851,focusedMarkers=88,boots=3,reloads=2,saves=3,newServerWireActions=9,retainedServerWireActions=216,sourceStartCases=72,sourceGuards=2,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['The previous content DLL fails the preserved windup-allocation regression. Learned start35 captures hit/block/miss and independent physical/skill-element/item rolls,typed damage/mitigation,cold/poison and drain;do2 consumes its original target packet once via shared ordered player contact. Existing current-rank release pricing is retained.',
   'The ungeared physical320/fire209/cold209/lightning209 FP256 golden and hit/physical/element/item call order pass.32/48/64feedback advances on successful allocation,including a later cancellation,but not on miss/block. Cached resistance/hard-synergy changes,six cancellation/target cases,flat retaliation,mapped/native lethal credit and deferred death pass.',
   'Actual Sapphire/Wormskull captured cold/poison and poison resumption,physical-only ordinary leech plus distinct fractional Life Tap,and three pre-roll capability refusals pass. Nine new actual left/right/quick actions preserve17FP mana after one release;216predecessor actions and original Vengeance gear/rank/health/mana saves survive both reloads.',
   'The unchanged supplementary start routine reproduces72cases/two guards with declared services. Four audits preserve decisions;71Python/573named/240catalogue/generators pass;106installed artifacts remain unchanged.'],
  remaining=['Older source/service comparisons do not establish exact modern skill-instance/counter reset,respec/granted-skill persistence or mid-windup payment/rank semantics. The counter is an explicit process-local per-owner learned-skill projection;current release pricing is preserved rather than newly claimed as modern parity.',
   'Critical/deadly result bits and full item/kill-proc/durability registration order,source modes/RNG/geometry,unclassified UO conversion,converted throwing,special/PvP/charged/oskill,pending-action saves,old-content migration and graphical client parity remain separate. Direct player-delivery fixtures retain their compatibility path;PvP is not enabled.',
   'Unsupported native modifiers/Paragon/uninterruptible/death-delay contexts refuse before hit RNG/payment;region/callback mutation remains fail-fast and nontransactional. Continue with Zeal sequence/contact while retaining these individual gaps. Final elemental feedback/contact/cold/poison/leech/audio and talent/hotkey art remain separate placeholders. No class completion,deployment or save reset.'])
 output=ROOT/'tools/reference/vengeance-contact-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Vengeance contact:851combined/88focused markers,9new/216retained wire actions,2reloads,4audits/71Python;106installed unchanged;no class completion.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
