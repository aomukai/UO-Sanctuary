#!/usr/bin/env python3
"""Bind Hammer typed damage/capture evidence without certifying its flight."""
import argparse,hashlib,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/blessed-hammer-contact'
PARENT='e052269a8f6511d5489d7b243296c166b5055a7a';META='Data/Sanctuary/Balance/class-skill-status.json';sha=inventory.sha
BUILD=7;FOCUS=6

def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def passed(path,total,boots):
 text=path.read_text();counts=markers(path)
 assert sum(counts.values())==total and ': failed' not in text and ' ERR]' not in text,path
 assert text.count('Shutting down')==text.count('Writing world save snapshot done')==boots,path
 return text,counts

def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 log,counts=passed(STAGE/'birth-native-checks.log',1016,3);assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_blessed_hammer_damage import configuration
 cfg=configuration(True);assert (len(cfg['extra_families']),len(cfg['extra_checks']),len(cfg['extra_target_input_commands']))==(72,17,99)
 for name in cfg['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in cfg['extra_checks']:assert counts[name]==3,name
 for name in cfg['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 phrase='Blessed Hammer damage:384complete source cases;two magic draws/species;captured Concentration/hard synergy;impact-time four-lane potion;ordinary immunity/demon/beast defenses;ordered notification/death/credit;no weapon leech/Thorns;CanSlow0damage capture;atomic/legacy restore;3pre-roll refusals'
 assert log.count(phrase)==1 and log.count('Blessed Hammer saved context before fixture placement:')==2
 focus=STAGE/f'focused-accepted-{FOCUS}';fi=json.loads((focus/'birth-inputs.json').read_text());passed(focus/'birth-native-checks.log',281,3)
 assert fi['artifacts']==tested['artifacts'] and fi['sourceInputs'].items()<=tested['sourceInputs'].items()
 for part in ('content','integration'):
  path=part+'/Sanctuary.'+part.title()+'.dll';assert sha(STAGE/path)==sha(focus/path)
  for build in (FOCUS,BUILD):
   text=(STAGE/f'{part}-build-{build}.log').read_text();assert all(x in text for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 before=json.loads((STAGE/f'pre-build-{FOCUS}.json').read_text())['inputs'];after=json.loads((STAGE/f'pre-build-{BUILD}.json').read_text())['inputs']
 assert before['sourceInputs']==after['sourceInputs'] and before['projectInputs']==after['projectInputs']
 assert before['copiedData']==fi['copiedData'] and after['copiedData']==tested['copiedData']
 assert {p for p in fi['copiedData'] if fi['copiedData'][p]!=tested['copiedData'][p]}=={META}
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'));changed=[]
 for a,b in zip(now['skills'],old['skills'],strict=True):
  if a!=b:assert a['talentId']=='paladin.blessedhammer';changed.append(a['talentId']);b['remaining']=a['remaining']
 assert changed==['paladin.blessedhammer'] and now==old and not now['completedClasses']
 parent=ROOT/'tools/reference/holy-bolt-flight-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 prior=json.loads((ROOT/'.local/class-skills/holy-bolt-flight/birth-inputs.json').read_text())
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][name]==tested['artifacts'][name]
 negative=STAGE/'negative-potion-launch';ni=json.loads((negative/'birth-inputs.json').read_text());text=(negative/'birth-native-checks.log').read_text()
 assert 'blessed-hammer-damage-check: failed' in text and 'Blessed Hammer incorrectly snapshots potion as extra original magic at launch' in text
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']==prior['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 assert (negative/'BlessedHammers.cs').read_bytes()==historical('src/Sanctuary.Content/BlessedHammers.cs')
 assert sha(negative/'BlessedHammerDamageChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/BlessedHammerDamageChecks.cs']
 diagnostic=STAGE/'focused-diagnostic-2';di=json.loads((diagnostic/'birth-inputs.json').read_text());text=(diagnostic/'birth-native-checks.log').read_text()
 assert 'Actual Holy Bolt damage input timeout:sequence=1,pending=None,held=None,running=True,alive=True,frozen=False,mana=529,valid=False,overlap=True' in text
 assert sha(diagnostic/'HolyBoltDamageChecks.cs')==di['sourceInputs']['tests/Sanctuary.Integration/HolyBoltDamageChecks.cs']
 assert sha(diagnostic/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
 collision=STAGE/'focused-diagnostic-5';ci=json.loads((collision/'birth-inputs.json').read_text());text=(collision/'birth-native-checks.log').read_text()
 assert 'generation=1,loss=3359,notifications=1,mana=17/17,seed=6028383488958571807' in text
 hs=json.loads((collision/'blessed-hammer-damage-checks.json').read_text())['Flight'];bs=json.loads((collision/'holy-bolt-flight-checks.json').read_text())['Flight']
 assert hs['OriginX']<bs['X']<hs['OriginX']+6 and hs['OriginY']==bs['Y'] and bs['Motion']['Seed']==666
 assert sha(collision/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
 for name in ('BlessedHammerDamageChecks.cs','HolyBoltFlightChecks.cs','ZealChecks.cs'):assert sha(collision/name)==ci['sourceInputs']['tests/Sanctuary.Integration/'+name]
 source=ROOT/'tools/reference/blessed-hammer-damage-engine-evidence.json';sr=json.loads(source.read_text())
 assert sr['sourceCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and sr['counts']==dict(damage=336,concentration=28,start=20) and len(sr['completeFunctions'])==8
 for path,digest in (sr['sourceHashes']|sr['artifacts']).items():assert sha(ROOT/path)==digest,path
 curve=ROOT/'tools/reference/blessed-hammer-engine-evidence.json';assert curve.read_bytes()==historical(str(curve.relative_to(ROOT)))
 cr=json.loads(curve.read_text());assert json.loads((STAGE/'source-probe/hammer-source-probe.json').read_text())==cr['path']['probe']
 assert len(cr['path']['probe']['points'])==77 and len(cr['path']['probe']['velocities'])==120
 for path,digest in cr['sourceHashes'].items():assert sha(ROOT/path)==digest,path
 audit=json.loads((STAGE/'audit-inputs.json').read_text());assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
 text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and sum(markers(STAGE/'audit-native-checks.log').values())==4
 fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  value=json.loads((STAGE/name).read_text());assert value==json.loads((ROOT/'tools/reference'/name).read_text()) and value['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for path,digest in value['InputHashes'].items():assert sha(ROOT/path)==digest,path
  previous=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in value.items() if k not in ignore}=={k:v for k,v in previous.items() if k not in ignore};fingerprints.append(len(value['InputHashes']))
 assert fingerprints==[909,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/'catalogue-checks.log').read_text()
 for value in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert value in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
 files=[Path(__file__).resolve(),ROOT/'tests/check_blessed_hammer_damage.py',parent,source,curve,
  *(ROOT/'docs'/n for n in ('paladin-blessed-hammer-damage-checkpoint.md','paladin-blessed-hammer-contact-preflight.md','paladin-blessed-hammer-flight-preflight.md','class-skill-reuse-notes.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md')),
  *(STAGE/n for n in (f'pre-build-{FOCUS}.json',f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','blessed-hammer-damage-checks.json','blessed-hammer-checks.json','holy-bolt-flight-checks.json','holy-bolt-damage-checks.json','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log','catalogue-checks.log',*(f'{part}-build-{b}.log' for b in (FOCUS,BUILD) for part in ('content','integration')))),
  *(focus/n for n in ('birth-inputs.json','birth-native-checks.log',f'focused-run-{FOCUS}.log','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(negative/n for n in ('birth-inputs.json','birth-native-checks.log','run.log','BlessedHammers.cs','BlessedHammerDamageChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(diagnostic/n for n in ('birth-inputs.json','birth-native-checks.log','HolyBoltDamageChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(collision/n for n in ('birth-inputs.json','birth-native-checks.log','blessed-hammer-damage-checks.json','holy-bolt-flight-checks.json','BlessedHammerDamageChecks.cs','HolyBoltFlightChecks.cs','ZealChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json')),
  *(ROOT/p for p in sr['artifacts'])]
 result=dict(referenceVersion='3.3.93847',component='Blessed Hammer ordered typed magic and launch capture with impact-time potion',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=1016,focusedMarkers=281,boots=3,reloads=2,saves=3,newServerWireActions=9,retainedServerWireActions=288,legacySourceCases=384,nativeSourceComparisons=368,sourceOnlyFactoryGuards=16,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['The parent DLL fails the launch-potion regression. Ordinary casts snapshot Concentration,hard synergies,rank,item modifiers and slow damage without launch potion. Impact T29four lanes use separate defenses. Magic immunity remains ordinary;undead/demon independently add50percent of the rerolled base. Ordered notification/fractional HP/credit/death/return excludes weapon leech/Thorns.',
   'CanSlow0preserves velocity while captured slow damage changes endpoints. Atomic malformed/duplicate restore preserves the live collection;uncaptured legacy flights retain their receiver. Original capture/resources survive two reloads with synchronous receiver checks. Nine natural left/right/quick casts are separate from saved curved-flight acceptance.',
   'Eight complete unchanged older routines pass384cases with declared nested services. Existing77waypoints/120velocity source evidence is unchanged and explicitly uses reconstructed Fog trig. Retained Holy Bolt wire failure was an invalid fixture target outside its checked corridor;production content DLL is identical before and after the fixture correction.',
   'Focused6and full7have identical source/projects/DLLs;only one reviewed remaining-note metadata field differs. Four audits preserve decisions,71Python/573named/240catalogue/generators pass,and106installed hashes remain unchanged.'],
  remaining=['Curved source-flight/per-missile RNG,natural original-flight reloads,absent-collider cleanup,offline/facet/death/respec behavior remain next. Existing global-RNG/double-vector/catch-up controller is not certified as source-equivalent.',
   'Modern executable,Fog LUT,room/sector/body/candidate/initial dispatch/critical/events/complete item/PvP/charged/oskill/client fidelity remain explicit. Controlled older-source services do not certify modern behavior.',
   'Final hammer/trail/contact/wall/expiry/audio and talent/hotkey artwork remain separate identified placeholders. No whole-class completion,deployment or save reset.'])
 output=ROOT/'tools/reference/blessed-hammer-damage-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Blessed Hammer damage:1016combined/281focused,9new/288retained wires,384source cases,4audits/71Python;106installed unchanged;flight/class acceptance remains open.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
