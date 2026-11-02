#!/usr/bin/env python3
"""Bind FoH typed delayed primary,potion and original strike persistence."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/heaven-fist-contact'
PARENT='dca967aa3b48940e0eda818ba74e6728b5f9e283';META='Data/Sanctuary/Balance/class-skill-status.json';sha=inventory.sha
BUILD=6;FOCUS=5

def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def passed(path,total,boots):
 text=path.read_text();counts=markers(path)
 assert sum(counts.values())==total and ': failed' not in text and ' ERR]' not in text,path
 assert text.count('Shutting down')==text.count('Writing world save snapshot done')==boots,path
 return text,counts

def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 log,counts=passed(STAGE/'birth-native-checks.log',1066,3);assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_heaven_fist_damage import configuration
 cfg=configuration(True);assert (len(cfg['extra_families']),len(cfg['extra_checks']),len(cfg['extra_target_input_commands']))==(74,18,105)
 for name in cfg['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in cfg['extra_checks']:assert counts[name]==3,name
 for name in cfg['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 assert counts['heaven-fist-damage-reload-result']==2
 phrase='FoH damage:impact-time four lanes;captured lightning/slow/rank;ordered native notification/HP/credit/death;no weapon leech/Thorns;atomic/legacy restore;3pre-roll refusals;original paid delayed strike staged'
 assert log.count(phrase)==1
 saved=json.loads((STAGE/'heaven-fist-damage-checks.json').read_text());assert saved['Generation']==2 and saved['Mana']==17 and saved['TargetLife']==30000*256-41754
 f=saved['Strike'];assert f['Min256']==41088 and f['Max256']==54784 and f['Captured']['Modifiers']['Potion']==0 and f['Captured']['Seed']==666<<32
 focus=STAGE/f'focused-accepted-{FOCUS}';fi=json.loads((focus/'birth-inputs.json').read_text());passed(focus/'birth-native-checks.log',331,3)
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
  if a!=b:assert a['talentId']=='paladin.fistoftheheavens';changed.append(a['talentId']);b['remaining']=a['remaining']
 assert changed==['paladin.fistoftheheavens'] and now==old and not now['completedClasses']
 parent=ROOT/'tools/reference/blessed-hammer-flight-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 prior=json.loads((ROOT/'.local/class-skills/blessed-hammer-flight/birth-inputs.json').read_text())
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][name]==tested['artifacts'][name]
 negative=STAGE/'negative-potion-launch';ni=json.loads((negative/'birth-inputs.json').read_text());text=(negative/'birth-native-checks.log').read_text()
 assert 'heaven-fist-damage-check: failed' in text and 'FoH incorrectly snapshots potion as extra original lightning at launch' in text
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']==prior['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 assert (negative/'HeavenFists.cs').read_bytes()==historical('src/Sanctuary.Content/HeavenFists.cs')
 assert sha(negative/'HeavenFistDamageChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/HeavenFistDamageChecks.cs']
 diagnostic=STAGE/'focused-diagnostic-4';di=json.loads((diagnostic/'birth-inputs.json').read_text());text=(diagnostic/'birth-native-checks.log').read_text()
 assert "denied by connection filter 'auto-denylist'" in text and 'Expected one fixture connection; found 0' in text and 'heaven-fist-damage-reload-result: passed' in text
 assert sha(diagnostic/'HeavenFistDamageChecks.cs')==di['sourceInputs']['tests/Sanctuary.Integration/HeavenFistDamageChecks.cs']
 assert sha(diagnostic/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
 source=ROOT/'tools/reference/heaven-fist-delay-engine-evidence.json';sr=json.loads(source.read_text());assert sr['cases']==1512 and len(sr['completeFunctions'])==2 and sr['sourceCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 for path,digest in (sr['sourceHashes']|sr['artifacts']).items():assert sha(ROOT/path)==digest,path
 startup=ROOT/'tools/reference/heaven-fist-start-engine-evidence.json';assert startup.read_bytes()==historical(str(startup.relative_to(ROOT))) and json.loads(startup.read_text())['counts']==dict(start=20,callback=72)
 legacy=ROOT/'tools/reference/heaven-fist-evidence.json';observations=ROOT/'tools/reference/heaven-fist-user-observations.json'
 for p in (legacy,observations):assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
 audit=json.loads((STAGE/'audit-inputs.json').read_text());assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
 text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and sum(markers(STAGE/'audit-native-checks.log').values())==4
 fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  value=json.loads((STAGE/name).read_text());assert value==json.loads((ROOT/'tools/reference'/name).read_text()) and value['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for path,digest in value['InputHashes'].items():assert sha(ROOT/path)==digest,path
  previous=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in value.items() if k not in ignore}=={k:v for k,v in previous.items() if k not in ignore};fingerprints.append(len(value['InputHashes']))
 assert fingerprints==[911,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/'catalogue-checks.log').read_text()
 for value in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert value in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
 files=[Path(__file__).resolve(),ROOT/'tests/check_heaven_fist_damage.py',parent,source,startup,legacy,observations,
  *(ROOT/'docs'/n for n in ('paladin-heaven-fist-damage-checkpoint.md','paladin-heaven-fist-contact-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md','t29-combat-potions.md')),
  *(STAGE/n for n in (f'pre-build-{FOCUS}.json',f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','heaven-fist-damage-checks.json','heaven-fist-checks.json','blessed-hammer-flight-checks.json','holy-bolt-flight-checks.json','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log','catalogue-checks.log',*(f'{part}-build-{b}.log' for b in (FOCUS,BUILD) for part in ('content','integration')))),
  *(focus/n for n in ('birth-inputs.json','birth-native-checks.log',f'focused-run-{FOCUS}.log','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(negative/n for n in ('birth-inputs.json','birth-native-checks.log','run.log','HeavenFists.cs','HeavenFistDamageChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(diagnostic/n for n in ('birth-inputs.json','birth-native-checks.log','HeavenFistDamageChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json')),
  *(ROOT/p for p in sr['artifacts'])]
 result=dict(referenceVersion='3.3.93847',component='Fist of the Heavens typed delayed primary and original source persistence',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=1066,focusedMarkers=331,boots=3,reloads=2,saves=3,newServerWireActions=9,retainedServerWireActions=306,legacyHit22Cases=1512,priorStartupCases=92,hostLifecycleControls=7,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['New casts prepare optional capture before one25mana payment/ten-frame delay. Primary lightning/rank/slow/mastery/seed and original target/burst coordinates persist;impact T29four lanes replace launch scalar potion enhancement. Lightning immunity and mixed component defenses retain the independent additions without poison/chill statuses.',
   'Shared typed notification/fractional HP/credit/death/NPC return excludes weapon leech/Thorns/stun. Atomic malformed/duplicate/legacy restore,missing/dead target identity handling,three capability refusals and seven existing host lifecycle controls pass. Friendly/dead primaries preserve original-origin burst without hitting a reused unrelated identity.',
   'The persistence fixture holds one original paid strike deadline through two real reloads,then rearms it for400ms and observes its single production-timer41754FP impact and secondary factory,with unchanged wounds/mana. This proves capture/controller persistence and timer delivery,not survival of an unmodified400ms deadline through two restarts. Loaded-overdue cleanup is separate. Nine real left/right/quick actions verify delayed damage/once payment. Historical delayed/secondary cohorts remain explicitly legacy. A fixture-only authentication omission caused the preserved auto-denylist failure;the corrected run uses the exact same content DLL/security settings.',
   'Complete older hit22/callback wrapper passes1512bounded cases with declared nested damage/search services;prior92startup/callback cases are unchanged. Focused5/full6share compiled sources/projects/DLLs;only one reviewed remaining-note metadata field differs. Audits preserve decisions;71Python/573named/240catalogue/generators pass;106installed hashes remain unchanged.'],
  remaining=['Secondary bolts still use their legacy controller/receiver and launch scalar potion enhancement;continue typed child damage,impact T29,source activation/movement/per-child RNG and natural trajectory saves. No secondary completion is claimed by the primary migration.',
   'The persistence test deliberately holds/rearms the impact deadline;unchanged short-delay clock behavior across restart is not certified. Modern full factory/stat/creator-seed/search/room/event/damage15/PvP/charged/oskill/client and dead-caster continuation remain explicit. Seven lifecycle checks establish existing host policies,not D2R owner-death equivalence. Missing/no-corpse dead-unit continuation is individually unsupported.',
   'Confirmed cursor-nearest click-facing acquisition is unchanged;own-tile,ties and perpendicular boundary remain uncertain. Final warning/strike/burst/secondary travel/contact/audio and talent/hotkey art remain separate placeholders. No class completion,deployment or save reset.'])
 output=ROOT/'tools/reference/heaven-fist-damage-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('FoH primary:1066combined/331focused,9new/306retained wires,1512source cases,held/rearmed original strike through2reloads,4audits/71Python;106installed unchanged;secondary remains next.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
