#!/usr/bin/env python3
"""Bind Hammer curved source motion,RNG,lifecycle and natural saved-flight proof."""
import argparse,hashlib,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/blessed-hammer-flight'
PARENT='6e2ad8d19c1c59cd29279f641bebebf07d4913eb';META='Data/Sanctuary/Balance/class-skill-status.json';sha=inventory.sha
BUILD=7;FOCUS=6

def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def passed(path,total,boots):
 text=path.read_text();counts=markers(path)
 assert sum(counts.values())==total and ': failed' not in text and ' ERR]' not in text,path
 assert text.count('Shutting down')==text.count('Writing world save snapshot done')==boots,path
 return text,counts

def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 log,counts=passed(STAGE/'birth-native-checks.log',1043,3);assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_blessed_hammer_flight import configuration
 cfg=configuration(True);assert (len(cfg['extra_families']),len(cfg['extra_checks']),len(cfg['extra_target_input_commands']))==(73,18,102)
 for name in cfg['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in cfg['extra_checks']:assert counts[name]==3,name
 for name in cfg['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 assert counts['blessed-hammer-flight-result']==1 and counts['blessed-hammer-flight-reload-result']==2
 phrase='Hammer flight:12960complete source frames;6795FP first seeded hit/7098FP next;captured curved path/last collider/deep copy;10lifecycle/wall/slow controls;final119/120;atomic/legacy restore;original paid moving flight staged'
 assert log.count(phrase)==1
 saved=json.loads((STAGE/'blessed-hammer-flight-checks.json').read_text());f=saved['Flight']
 assert saved['Generation']==2 and saved['Mana']==17 and saved['DeletedCollider']==f['LastCollision']!=0
 assert f['Motion']['Seed']==7129137990492200882 and f['Frame']>2 and f['Point']>1 and f['Motion']['Path']['SavedSteps']
 assert 'Hammer cleared absent saved collider:'+str(f['Id'])+'/' in log
 focus=STAGE/f'focused-accepted-{FOCUS}';fi=json.loads((focus/'birth-inputs.json').read_text());passed(focus/'birth-native-checks.log',308,3)
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
 parent=ROOT/'tools/reference/blessed-hammer-damage-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 prior=json.loads((ROOT/'.local/class-skills/blessed-hammer-contact/birth-inputs.json').read_text())
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][name]==tested['artifacts'][name]
 negative=STAGE/'negative-offline-replay';ni=json.loads((negative/'birth-inputs.json').read_text());text=(negative/'birth-native-checks.log').read_text()
 assert 'blessed-hammer-flight-result: failed' in text and 'Disconnected Hammer replayed combat instead of harmless aging' in text
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']==prior['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 assert (negative/'BlessedHammers.cs').read_bytes()==historical('src/Sanctuary.Content/BlessedHammers.cs')
 assert sha(negative/'BlessedHammerFlightChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/BlessedHammerFlightChecks.cs']
 diagnostic=STAGE/'focused-diagnostic-4';di=json.loads((diagnostic/'birth-inputs.json').read_text());text=(diagnostic/'birth-native-checks.log').read_text()
 assert 'System.ArgumentException: An item with the same key has already been added.' in text and 'at Sanctuary.CharacterProfiles.Assign' in text
 assert sha(diagnostic/'BlessedHammerFlightChecks.cs')==di['sourceInputs']['tests/Sanctuary.Integration/BlessedHammerFlightChecks.cs']
 source=ROOT/'tools/reference/blessed-hammer-motion-engine-evidence.json';sr=json.loads(source.read_text())
 assert sr['sourceCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and sr['contexts']==108 and sr['movementFrames']==12960 and sr['legacyPathType']==14
 assert sr['movementProfile']=='unmodified1.10branch' and sr['guarded113Profile']=='108contexts/allmovementrefused'
 for path,digest in sr['artifacts'].items():assert sha(ROOT/path)==digest,path
 for path,digest in sr['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/path)==digest,path
 guarded=json.loads((STAGE/'motion-source/guarded-113-cases.json').read_text());assert len(guarded)==108 and all(not f[0] for row in guarded for f in row['frames'])
 kernel=STAGE/'kernel-diagnostic-1';ki=json.loads((kernel/'birth-inputs.json').read_text());text=(kernel/'birth-native-checks.log').read_text()
 assert 'Hammer source context0 frame0 field0:actual1 expected0' in text
 assert sha(kernel/'BlessedHammerMotionChecks.cs')==ki['sourceInputs']['tests/Sanctuary.Integration/BlessedHammerMotionChecks.cs']
 assert sha(kernel/'SourceActorMotion.cs')==ki['sourceInputs']['src/Sanctuary.Content/SourceActorMotion.cs']
 kr=json.loads((kernel/'blessed-hammer-motion-engine-evidence.json').read_text())
 assert sha(kernel/'probe_blessed_hammer_motion.py')==kr['artifacts']['tools/probe_blessed_hammer_motion.py']
 for n in ('hammer-motion.cpp','cases.json'):assert sha(kernel/'motion-source'/n)==kr['artifacts'][str((STAGE/'motion-source'/n).relative_to(ROOT))]

 actor=ROOT/'tools/reference/actor-motion-engine-evidence.json';assert actor.read_bytes()==historical(str(actor.relative_to(ROOT)))
 ar=json.loads(actor.read_text());assert ar['fixture']['contexts']==3240 and ar['fixture']['frameComparisons']==64800 and sha(ROOT/ar['fixture']['path'])==ar['fixture']['sha256']
 oldcurve=ROOT/'tools/reference/blessed-hammer-engine-evidence.json';oldDamage=ROOT/'tools/reference/blessed-hammer-damage-engine-evidence.json'
 for p in (oldcurve,oldDamage):assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
 preparation=ROOT/'tools/reference/heaven-fist-start-engine-evidence.json';pr=json.loads(preparation.read_text())
 assert pr['sourceCommit']==sr['sourceCommit'] and pr['counts']==dict(start=20,callback=72) and len(pr['completeFunctions'])==2
 for path,digest in (pr['sourceHashes']|pr['artifacts']).items():assert sha(ROOT/path)==digest,path
 audit=json.loads((STAGE/'audit-inputs.json').read_text());assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
 text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and sum(markers(STAGE/'audit-native-checks.log').values())==4
 fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  value=json.loads((STAGE/name).read_text());assert value==json.loads((ROOT/'tools/reference'/name).read_text()) and value['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for path,digest in value['InputHashes'].items():assert sha(ROOT/path)==digest,path
  previous=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in value.items() if k not in ignore}=={k:v for k,v in previous.items() if k not in ignore};fingerprints.append(len(value['InputHashes']))
 assert fingerprints==[910,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/'catalogue-checks.log').read_text()
 for value in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert value in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
 files=[Path(__file__).resolve(),ROOT/'tests/check_blessed_hammer_flight.py',parent,source,actor,oldcurve,oldDamage,preparation,ROOT/'docs/paladin-heaven-fist-contact-preflight.md',
  *(ROOT/'docs'/n for n in ('paladin-blessed-hammer-flight-checkpoint.md','paladin-blessed-hammer-flight-preflight.md','class-skill-reuse-notes.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md')),
  *(STAGE/n for n in (f'pre-build-{FOCUS}.json',f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','blessed-hammer-flight-checks.json','blessed-hammer-damage-checks.json','blessed-hammer-checks.json','holy-bolt-flight-checks.json','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log','catalogue-checks.log',*(f'{part}-build-{b}.log' for b in (FOCUS,BUILD) for part in ('content','integration')))),
  *(focus/n for n in ('birth-inputs.json','birth-native-checks.log',f'focused-run-{FOCUS}.log','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(negative/n for n in ('birth-inputs.json','birth-native-checks.log','run.log','BlessedHammers.cs','BlessedHammerFlightChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(diagnostic/n for n in ('birth-inputs.json','birth-native-checks.log','BlessedHammerFlightChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(kernel/n for n in ('birth-inputs.json','birth-native-checks.log','SourceActorMotion.cs','BlessedHammerMotionChecks.cs','probe_blessed_hammer_motion.py','blessed-hammer-motion-engine-evidence.json','motion-source/hammer-motion.cpp','motion-source/cases.json','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json')),
  *(ROOT/p for p in sr['artifacts']),*(ROOT/p for p in pr['artifacts'])]
 result=dict(referenceVersion='3.3.93847',component='Blessed Hammer saved curved source motion,RNG and original-flight lifecycle',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=1043,focusedMarkers=308,boots=3,reloads=2,saves=3,newServerWireActions=9,retainedServerWireActions=297,legacyMotionContexts=108,legacyMotionFrames=12960,nextFoHPreparationCases=92,actorRegressionContexts=3240,actorRegressionFramesPerBoot=64800,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['New ordinary casts snapshot typed damage and prepare source curve before one payment. Optional version0Motion stores original77point curve,integer position/direction/vector,waypoint,velocity/acceleration/counter,cells and per-missile RNG. Shared SourceActorMotion supports missile path14while retaining actor collision rules;BasicMissileUpdate/SourceMissileSteps/native cell-wall/source footprints are reused.',
   'First undead contact deals6795FP and the next7098FP from two positive-bound draws per hit,advancing seeds to1193071124610and7129137990492200882. Piercing/last collider,slow damage without velocity change,10owner/lifecycle/wall controls,final119/120,deep copies,atomic/legacy loading and natural paid moving original-flight reloads pass. The same original curve/seed/wounds/mana persists;no missile copy replaces it.',
   'The parent DLL fails offline harmless-aging regression. Historical motionless flights retain their compatibility controller;original trajectory/saved receiver fixtures explicitly retain legacy cohorts. Retained damage inputs now use source seeds. Wardrobe diagnostic was creation-only fixture API misuse,corrected with the existing transaction.',
   'Complete unchanged older1.10movement composition passes108contexts/12960frames and sanitizers. The optional1.13guard refuses all missile contexts and is preserved as a conflict;modern outer dispatch is not certified. Fog trig/room/missile-grid services remain declared. Unchanged3240context/64800frame actor regression passes on every boot.',
   'Focused6and full7share exact compiled sources/projects/DLLs with one remaining-note metadata bridge. Four audits preserve item/affix decisions;71Python/573named/240catalogue/generators pass;106installed hashes unchanged.'],
  remaining=['Current executable creator RNG/outer dispatch/Fog LUT/factory InitSteps6/initial paths,room/sector/footprints/candidate order/global mixed-flight chronology/active lag catch-up,events/items/PvP/charged/oskill/client equivalence remain explicit. The1.10motion proof is supplementary and its1.13guard conflict is not hidden.',
   'Harmless offline/load aging and native terrain/body adapters are existing host policies. Owner/town-boundary/client presentation and old-content readers require their own broader acceptance. Related absent-collider reference families remain separately recorded.',
   'Final hammer/trail/contact/wall/expiry/audio and talent/hotkey artwork remain separate identified placeholders. No whole-class completion,deployment or save reset.'])
 output=ROOT/'tools/reference/blessed-hammer-flight-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Hammer flight:1043combined/308focused,9new/297retained wires,12960source/64800actor frames,2natural original-flight reloads,4audits/71Python;106installed unchanged;no class certificate.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
