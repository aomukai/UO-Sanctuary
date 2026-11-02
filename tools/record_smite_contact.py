#!/usr/bin/env python3
"""Bind Smite shield delivery and retained consumers to the tested native build."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/smite-contact'
PARENT='7438c1882deac214423388f4c77d8d0fcc29daba'
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
 log,counts=passed(STAGE/'birth-native-checks.log',830,3)
 assert 'pass across3boots/2reloads' in (STAGE/'full-run-2.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_smite_contact import configuration
 cfg=configuration(True);assert len(cfg['extra_families'])==69 and len(cfg['extra_checks'])==13 and len(cfg['extra_target_input_commands'])==72
 for name in cfg['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in cfg['extra_checks']:assert counts[name]==3,name
 for name in cfg['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 phrase='Smite contact:pre-HP notification/credit/hitclass0x65;source/native deferred lethal return;forced hit/miss/block and RNG order;real item weapon/leech exclusions;fractional Life Tap;real recovery/RIP entitlement;3pre-roll refusals;champion89/90stun'
 assert log.count(phrase)==3
 assert counts['smite-result']==1 and counts['poison-dagger-contact-check']==counts['sacrifice-contact-check']==3
 focus=STAGE/'focused-accepted-2';fi=inventory.validate_inventory(focus/'birth-inputs.json');passed(focus/'birth-native-checks.log',64,3)
 assert fi['artifacts']==tested['artifacts'] and fi['copiedData']==tested['copiedData']
 for part in ('content','integration'):
  path=part+'/Sanctuary.'+part.title()+'.dll';assert sha(STAGE/path)==sha(focus/path)
  text=(STAGE/(part+'-build-2.log')).read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
 negative=STAGE/'negative-native-death';ni=json.loads((negative/'birth-inputs.json').read_text());text=(negative/'birth-native-checks.log').read_text()
 assert 'smite-contact-check: failed' in text and 'Smite must commit target HP before retaliation and defer target death until return' in text
 for name in ('PaladinSmite.cs','PlayerWeaponContact.cs','PaladinCombat.cs'):
  assert (negative/name).read_bytes()==historical('src/Sanctuary.Content/'+name)
  assert sha(negative/name)==ni['sourceInputs']['src/Sanctuary.Content/'+name]
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 assert sha(negative/'SmiteContactChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/SmiteContactChecks.cs']
 prior=json.loads((ROOT/'.local/class-skills/sacrifice-contact/birth-inputs.json').read_text())
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][name]==tested['artifacts'][name]
 assert prior['copiedData'].keys()==tested['copiedData'].keys()
 assert {p for p in tested['copiedData'] if tested['copiedData'][p]!=prior['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'));changed=[]
 for a,b in zip(now['skills'],old['skills'],strict=True):
  if a!=b:assert a['talentId']=='paladin.smite';changed.append(a['talentId']);b['remaining']=a['remaining']
 assert changed==['paladin.smite'] and now==old and not now['completedClasses']
 parent=ROOT/'tools/reference/sacrifice-contact-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 source=ROOT/'tools/reference/smite-engine-evidence.json';assert source.read_bytes()==historical(str(source.relative_to(ROOT)))
 probe=ROOT/'.local/class-skills/smite-source-probe.json';source_record=json.loads(source.read_text());assert json.loads(probe.read_text())==source_record['probe']
 assert {n:len(v) for n,v in source_record['probe'].items()}==dict(damageCases=64,guards=5,stunCases=256,timingCases=72)
 for path in ('.local/class-skills/SkillPal.cpp.reference','.local/research/d2moo-SUnitDmg.cpp','.local/research/d2moo-CommonUnits.cpp'):
  assert sha(ROOT/path)==source_record['sourceHashes'][path],path
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
 files=[Path(__file__).resolve(),ROOT/'tests/check_smite_contact.py',parent,source,probe,ROOT/'tools/probe_smite_reference.py',
  *(ROOT/'docs'/name for name in ('paladin-smite-contact-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md')),
  *(STAGE/name for name in ('pre-build-2.json','birth-inputs.json','birth-native-checks.log','full-run-2.log','smite-checks.json','sacrifice-checks.json','audit-inputs.json','audit-native-checks.log','audit-run-2.log','python-tests.log','catalogue-checks.log','content-build-2.log','integration-build-2.log')),
  *(focus/name for name in ('birth-inputs.json','birth-native-checks.log','focused-run-2.log','smite-checks.json','sacrifice-checks.json')),
  *(negative/name for name in ('birth-inputs.json','birth-native-checks.log','PaladinSmite.cs','PlayerWeaponContact.cs','PaladinCombat.cs','SmiteContactChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(ROOT/path for path in ('.local/class-skills/SkillPal.cpp.reference','.local/research/d2moo-SUnitDmg.cpp','.local/research/d2moo-CommonUnits.cpp')),
  *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Smite ordered shield contact with forced source results and distinct effect policy',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=830,focusedMarkers=64,boots=3,reloads=2,saves=3,newServerWireActions=9,retainedServerWireActions=207,sourceShieldCases=64,sourceStunCases=256,sourceTimingCases=72,sourceGuards=5,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['Previous content fails Smite death-before-retaliation regression. Smite now uses the shared ordered player contact/notification/credit/HP/stun/kill-recovery/attempt/return pipeline while retaining shield/HS/item_normaldamage arithmetic,S1 timing and2-mana event payment.',
   'Do150 shield damage/result RNG order,forced success after miss/block,hitclass0x65 and prepared timed knockback are distinct from ordinary attacks. Ordinary critical/weapon damage/elements/poison/freeze/leech remain excluded. Actual fractional Life Tap and recovery/RIP entitlement pass alongside mapped/native lethal credit,pre-HP notification before stun,champion89/90stun and three pre-roll refusals.',
   'Nine new actual left/right/quick wire actions confirm unpaid/unrolled windup and once-only damage/contact/payment even with a forced ordinary miss.207predecessor wire actions,original gear/resources/HS/stun saves and Poison Dagger/Sacrifice contact checks pass across both reloads.',
   'The unchanged supplementary source probe reproduces64shield,256stun,72timing and five guard cases. Four fresh audits preserve decisions;71Python/573named/240catalogue/generators pass;106installed hashes remain unchanged.'],
  remaining=['Older engine/source-service comparisons do not certify exact modern mode/RNG/geometry or primary client behavior. Full item/kill-proc/durability registration order,modern direct setup/Prevent Monster Heal behavior,unclassified UO species conversion,special/PvP/charged/oskill,pending-action saves and old-content migration remain separate.',
   'Unknown native control metadata supplies no invented stun or D2 identity. Unsupported native modifiers/Paragon/uninterruptible/death-delay contexts refuse before RNG/payment;region/callback mutation remains fail-fast and nontransactional.',
   'Continue the Paladin review with Vengeance,retaining its distinct elemental roll/synergies while reusing allocation/contact. Final shield-bash/S1/stun/block/return/audio and talent/hotkey assets remain separate placeholders. No class completion,deployment or save reset.'])
 output=ROOT/'tools/reference/smite-contact-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Smite contact:830combined/64focused markers,9new/207retained wire actions,2reloads,4audits/71Python;106installed unchanged;no class completion.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
