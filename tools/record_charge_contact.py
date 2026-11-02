#!/usr/bin/env python3
"""Bind Charge impact allocation and ordered contact to the exact tested build."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/charge-contact'
PARENT='b424355f8c51c7a472ffc297d851e54066f88351'
sha=inventory.sha

def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def passed(path,total,boots):
 text=path.read_text();counts=markers(path)
 assert sum(counts.values())==total and ': failed' not in text and ' ERR]' not in text,path
 assert text.count('Shutting down')==text.count('Writing world save snapshot done')==boots,path
 return text,counts

def record(check=False):
 inventory.STAGE=STAGE
 tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/'pre-build-5.json',STAGE/'birth-inputs.json')
 log,counts=passed(STAGE/'birth-native-checks.log',950,3)
 assert 'pass across3boots/2reloads' in (STAGE/'full-run-5.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_charge_contact import configuration
 cfg=configuration(True);assert len(cfg['extra_families'])==69 and len(cfg['extra_checks'])==17 and len(cfg['extra_target_input_commands'])==90
 for name in cfg['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in cfg['extra_checks']:assert counts[name]==3,name
 for name in cfg['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 phrase='Charge contact:ordered mapped/native lethal return/credit;396FP live allocation;hit/miss/block forced knockback and frozen pause/get-hit;real Sapphire/Wormskull;870FP leech/Life Tap;5pre-roll refusals'
 assert log.count(phrase)==3
 assert counts['charge-check']==1 and counts['charge-reload-check']==2
 focus=STAGE/'focused-accepted-5';fi=inventory.validate_inventory(focus/'birth-inputs.json');passed(focus/'birth-native-checks.log',197,3)
 assert fi['artifacts']==tested['artifacts'] and fi['copiedData']==tested['copiedData']
 for part in ('content','integration'):
  path=part+'/Sanctuary.'+part.title()+'.dll';assert sha(STAGE/path)==sha(focus/path)
  text=(STAGE/(part+'-build-5.log')).read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
 negative=STAGE/'negative-native-death';ni=json.loads((negative/'birth-inputs.json').read_text());text=(negative/'birth-native-checks.log').read_text()
 assert 'charge-contact-check: failed' in text and 'Charge must commit target HP before retaliation and defer target death until return' in text
 parent=ROOT/'tools/reference/conversion-contact-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 prior=json.loads((ROOT/'.local/class-skills/conversion-contact/birth-inputs.json').read_text())
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']==prior['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 for name in ('PaladinCharge.cs','PlayerWeaponContact.cs'):assert (negative/name).read_bytes()==historical('src/Sanctuary.Content/'+name)
 # The negative's inventory enumerates current workspace files, not parent DLL
 # sources. Parent DLL identity and separately archived parent sources prove
 # this binary substitution; do not mislabel it a parent source-build receipt.
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][name]==tested['artifacts'][name]
 assert prior['copiedData'].keys()==tested['copiedData'].keys()
 assert {p for p in tested['copiedData'] if tested['copiedData'][p]!=prior['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'));changed=[]
 for a,b in zip(now['skills'],old['skills'],strict=True):
  if a!=b:assert a['talentId']=='paladin.charge';changed.append(a['talentId']);b['remaining']=a['remaining']
 assert changed==['paladin.charge'] and now==old and not now['completedClasses']
 source=ROOT/'tools/reference/charge-engine-evidence.json';assert source.read_bytes()==historical(str(source.relative_to(ROOT)))
 sr=json.loads(source.read_text());probe=STAGE/'source-probe/charge-source-probe.json';reproduced=STAGE/'source-probe/source-evidence-reproduced.json'
 assert json.loads(probe.read_text())==sr['probe'] and json.loads(reproduced.read_text())==sr
 assert {k:len(v) for k,v in sr['probe'].items()}==dict(startCases=42,phaseCases=24,impactCases=3,gateCases=12,rewindCases=12,velocityCases=54,legacyManaCases=6,flowCases=15)
 for path,digest in sr['sourceHashes'].items():assert sha(ROOT/path)==digest,path
 assert sr['sourceCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and sr['referenceVersion']=='3.3.93847'
 audit=json.loads((STAGE/'audit-inputs.json').read_text());assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
 _,audit_counts=passed_audit(STAGE/'audit-native-checks.log');assert sum(audit_counts.values())==4
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
 for value in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert value in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
 files=[Path(__file__).resolve(),ROOT/'tests/check_charge_contact.py',parent,source,probe,reproduced,ROOT/'tools/probe_charge_reference.py',ROOT/'tools/probe_holy_bolt_hit_reference.py',ROOT/'tools/reference/holy-bolt-hit-engine-evidence.json',
  *(ROOT/'docs'/name for name in ('paladin-charge-contact-checkpoint.md','paladin-charge-contact-preflight.md','paladin-holy-bolt-contact-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md')),
  *(STAGE/name for name in ('pre-build-5.json','birth-inputs.json','birth-native-checks.log','full-run-5.log','charge-checks.json','conversion-checks.json','zeal-checks.json','vengeance-checks.json','smite-checks.json','sacrifice-checks.json','audit-inputs.json','audit-native-checks.log','audit-run-5.log','python-tests.log','catalogue-checks.log','content-build-5.log','integration-build-5.log')),
  *(focus/name for name in ('birth-inputs.json','birth-native-checks.log','focused-run-5.log','charge-checks.json','conversion-checks.json','zeal-checks.json','vengeance-checks.json','smite-checks.json','sacrifice-checks.json','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(negative/name for name in ('birth-inputs.json','birth-native-checks.log','run.log','PaladinCharge.cs','PlayerWeaponContact.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Charge live impact allocation and ordered ordinary contact with independent forced knockback',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=950,focusedMarkers=197,boots=3,reloads=2,saves=3,newServerWireActions=18,retainedServerWireActions=252,sourceCases=168,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['The parent content DLL fails the retained HP/retaliation/deferred-death regression. Charge now allocates ordinary typed weapon contact at impact,not movement start. Live rank/hard Might synergy/resistance produce396FP256 after additive ED and resistance;there are no travel damage draws or second mana payments.',
   'Hit/miss/block all request source flag8;ordinary miss/block skip damage rolls but retain flat attacker-takes-damage and mapped knockback. Prepared movement pauses while frozen and finishes in get-hit. Source hitclass0x70 combines with unarmed1 or dagger6. Forced knockback is independent from Smite:ordinary cold,poison,critical,item damage and leech remain allowed,while duplicate item knockback is suppressed.',
   'Mapped and unclassified lethal credit/death order,real Sapphire/Wormskull cold37frames/poison102per frame,and870FP256 physical Greyform leech/Life Tap pass. Five unsupported/unpaid/late contexts refuse before hit rolls;late refusal retains the paid nine-mana start.',
   'Eighteen actual target/ground left/right/quick actions and252retained wire actions pass across two reloads. Existing travel,obstruction,late-frame,tracking,stamina,close basic fallback,equipment/owner transitions and original saved wounded/resource actors remain covered. Fresh audits preserve readiness decisions;71Python/573named/240catalogue/generator checks pass and106installed hashes are unchanged.',
   'All168older-source cases reproduce with declared services and restored source aliases verified against pinned hashes/Git blobs. These are bounded reconstruction comparisons,not current-executable or full-class certification.'],
  remaining=['Modern source movement/animation/event/seed/body/collision/range/FP projection,uninterruptible targeting policy and broken reconstructed path initializer remain distinct from native adapter tests. Unclassified UO actors have no invented D2 identity or forced source movement.',
   'Full item registration/RNG/durability order,critical result bits,PvP/charged/oskill/special contexts,pending-action persistence,old-content migration,equipment round-trip/immediate cancellation and graphical-client parity remain open. Existing adjacent basic fallback retains nine-mana payment and ordinary damage without Charge ED/AR/knockback.',
   'Source0x70/overlay147,travel/impact/knockback and audio use identified placeholders. Talent/hotkey icons are tracked separately. No class completion,deployment or save reset;continue Paladin spell acceptance.'])
 output=ROOT/'tools/reference/charge-contact-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Charge contact:950combined/197focused markers,18new/252retained wire actions,2reloads,4audits/71Python;106installed unchanged;no class completion.')
def passed_audit(path):
 text=path.read_text();counts=markers(path);assert ': failed' not in text and ' ERR]' not in text;return text,counts
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
