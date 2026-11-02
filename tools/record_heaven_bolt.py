#!/usr/bin/env python3
"""Bind FoH secondary typed receiver,shared flight and qualified persistence."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/heaven-bolt';sha=inventory.sha
PARENT='1a95747c9ce5c47ada5b15ecae603b1689ea40c8';FOCUS=4;BUILD=5;META='Data/Sanctuary/Balance/class-skill-status.json'
def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def passed(path,total):
 s=path.read_text();m=markers(path)
 assert sum(m.values())==total and ': failed' not in s and ' ERR]' not in s,path
 assert s.count('Shutting down')==s.count('Writing world save snapshot done')==3,path
 return s,m

def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 log,counts=passed(STAGE/'birth-native-checks.log',1089);assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_heaven_bolt import configuration
 c=configuration(True);assert (len(c['extra_families']),len(c['extra_checks']),len(c['extra_target_input_commands']))==(75,18,108)
 for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in c['extra_checks']:assert counts[name]==3,name
 for name in c['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3 and log.count(' '+name+': windup')==3,name
 assert counts['heaven-bolt-reload-result']==2 and log.count('FoH child:typed impact T29;Holy3 original magic only;')==1
 saved=json.loads((STAGE/'heaven-bolt-checks.json').read_text());f=saved['Flight']
 assert saved['Generation']==2 and saved['Mana']==17 and saved['DeletedCollider']==f['LastCollision']
 assert f['Ability']==121 and (f['Min256'],f['Max256'])==(11776,14720) and f['Captured']['Modifiers']['Potion']==0
 assert f['Motion']['VelocityFp8']==2304 and f['Motion']['Seed']==1193071124610 and f['Frame']>2 and not f['Motion']['Fault']
 focus=STAGE/f'focused-accepted-{FOCUS}';fi=json.loads((focus/'birth-inputs.json').read_text());passed(focus/'birth-native-checks.log',354)
 assert fi['artifacts']==tested['artifacts'] and fi['sourceInputs'].items()<=tested['sourceInputs'].items()
 for part in ('content','integration'):
  p=part+'/Sanctuary.'+part.title()+'.dll';assert sha(focus/p)==sha(STAGE/p)
  for build in (FOCUS,BUILD):
   s=(STAGE/f'{part}-build-{build}.log').read_text();assert all(x in s for x in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 a=json.loads((STAGE/f'pre-build-{FOCUS}.json').read_text())['inputs'];b=json.loads((STAGE/f'pre-build-{BUILD}.json').read_text())['inputs']
 assert a['sourceInputs']==b['sourceInputs'] and a['projectInputs']==b['projectInputs']
 assert a['copiedData']==fi['copiedData'] and b['copiedData']==tested['copiedData']
 assert {p for p in fi['copiedData'] if fi['copiedData'][p]!=tested['copiedData'][p]}=={META}
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'));changed=[]
 for x,y in zip(now['skills'],old['skills'],strict=True):
  if x!=y:assert x['talentId']=='paladin.fistoftheheavens';changed.append(x['talentId']);y['remaining']=x['remaining']
 assert changed==['paladin.fistoftheheavens'] and now==old and not now['completedClasses']
 parent=ROOT/'tools/reference/heaven-fist-damage-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 prior=json.loads((ROOT/'.local/class-skills/heaven-fist-contact/birth-inputs.json').read_text())
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][p]==tested['artifacts'][p]
 negative=STAGE/'negative-potion-launch';ni=json.loads((negative/'birth-inputs.json').read_text());s=(negative/'birth-native-checks.log').read_text()
 assert 'heaven-bolt-check: failed' in s and 'FoH child incorrectly snapshots potion into original magic' in s
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']==prior['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 for name in ('HolyBolts.cs','HolyBoltDamage.cs','HolyBoltFlights.cs'):assert (negative/name).read_bytes()==historical('src/Sanctuary.Content/'+name)
 assert sha(negative/'HeavenBoltChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/HeavenBoltChecks.cs']
 diagnostic=STAGE/'focused-diagnostic-2';di=json.loads((diagnostic/'birth-inputs.json').read_text());s=(diagnostic/'birth-native-checks.log').read_text()
 assert 'Child host control/expiry unsupported loss=-8960000' in s and 'Paragon damage/recovery needs its ordered actor adapter' in s
 assert sha(diagnostic/'HeavenBoltChecks.cs')==di['sourceInputs']['tests/Sanctuary.Integration/HeavenBoltChecks.cs']
 assert sha(diagnostic/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
 source=[]
 for name in ('holy-bolt-hit-engine-evidence.json','heaven-fist-start-engine-evidence.json','heaven-fist-delay-engine-evidence.json','heaven-fist-evidence.json','heaven-fist-user-observations.json'):
  p=ROOT/'tools/reference'/name;assert p.read_bytes()==historical(str(p.relative_to(ROOT)));source.append(p)
 hit=json.loads(source[0].read_text());assert hit['counts']==dict(eligibility=768,healing=16,guards=4)
 for name in source[:3]:
  r=json.loads(name.read_text())
  for p,h in (r.get('sourceHashes',{})|r['artifacts']).items():assert sha(ROOT/p)==h,p
 prep=ROOT/'tools/reference/holy-shield-cast-source-evidence.json';pr=json.loads(prep.read_text());assert pr['cases']==480 and len(pr['routineHashes'])==7
 for p,h in (pr['sourceHashes']|pr['currentTables']|pr['artifacts']).items():assert sha(ROOT/p)==h,p
 audit=json.loads((STAGE/'audit-inputs.json').read_text());assert audit['artifacts']==tested['artifacts'] and audit['copiedData']==tested['copiedData']
 s=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in s and ' ERR]' not in s and sum(markers(STAGE/'audit-native-checks.log').values())==4
 fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  n=json.loads((STAGE/name).read_text());assert n==json.loads((ROOT/'tools/reference'/name).read_text()) and n['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for p,h in n['InputHashes'].items():assert sha(ROOT/p)==h,p
  o=json.loads(historical('tools/reference/'+name));ignore={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in n.items() if k not in ignore}=={k:v for k,v in o.items() if k not in ignore};fingerprints.append(len(n['InputHashes']))
 assert fingerprints==[911,21]
 s=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in s and '\nOK\n' in s and 'FAILED' not in s
 s=(STAGE/'catalogue-checks.log').read_text()
 for text in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert text in s
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
 docs=[ROOT/'docs'/n for n in ('paladin-heaven-bolt-checkpoint.md','paladin-heaven-fist-contact-preflight.md','paladin-holy-shield-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md','t29-combat-potions.md')]
 files=[Path(__file__).resolve(),ROOT/'tests/check_heaven_bolt.py',parent,*source,*docs,prep,*(ROOT/p for p in pr['artifacts']),STAGE/'holy-shield-source-preparation.log',
  *(STAGE/n for n in ('birth-inputs.json','birth-native-checks.log','heaven-bolt-checks.json','heaven-fist-damage-checks.json','holy-bolt-flight-checks.json','blessed-hammer-flight-checks.json',f'pre-build-{FOCUS}.json',f'pre-build-{BUILD}.json',f'full-run-{BUILD}.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log','catalogue-checks.log',*(f'{part}-build-{b}.log' for part in ('content','integration') for b in (FOCUS,BUILD)))),
  *(focus/n for n in ('birth-inputs.json','birth-native-checks.log',f'focused-run-{FOCUS}.log','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(negative/n for n in ('birth-inputs.json','birth-native-checks.log','HeavenBoltChecks.cs','HolyBolts.cs','HolyBoltDamage.cs','HolyBoltFlights.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(diagnostic/n for n in ('birth-inputs.json','birth-native-checks.log','focused-run-2.log','HeavenBoltChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='FoH secondary shared typed damage and source flight',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=1089,focusedMarkers=354,boots=3,reloads=2,saves=3,newServerWireActions=9,retainedServerWireActions=315,hostControls=10,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['New captured FoH parents create shared HolyBoltCapture/Motion flights with velocity2304FP8,Activate2,Range50,per-child RNG,cells and last collider. Endpoints use original FoH rank and factory-time Holy Bolt hard points,without mastery or launch potion scalar. Captured slow changes damage and velocity separately;ordinary Holy Bolt remains distinct.',
   'Shared typed receiver applies impact-time T29four lanes. Holy3 bypasses positive original magic defenses for undead and demons only;added elements retain separate defenses. Allies/pets do not heal,beasts take no secondary damage and these collisions consume no damage draw. Piercing delivers12442then12034FP with saved seed666then1193071124610;ordered notification/fractional HP/credit/death excludes weapon leech/Thorns.',
   'One paid parent-created original moving child retains identity,path,cells,RNG,binding,wounds and17mana across two actual reloads and timer-driven resumed hits. The fixture explicitly completes the original primary under controlled invocation and holds/rearms inspection clocks;it does not prove an unmodified two-second lifetime across downtime. Nine real left/right/quick casts separately exercise normal windup,primary delay,child travel and once payment.',
   'Atomic restore,legacy omission,reference normalization,activation/final frames and ten host controls pass. The parent DLL fails launch-potion regression;preserved diagnostic2 is a fixture Paragon-life baseline error corrected with byte-identical production DLL. Focused4/final5compiled sources/projects/DLLs match,with one remaining-note metadata bridge. Fresh audits/71Python/573named/240catalogue/generators pass;106installed artifacts unchanged.'],
  remaining=['Next Holy Shield source preparation adds480compiled start36/do18/outer/list cases using existing scaffolds;no Holy Shield runtime behavior changes in this package. Modern damage15 is not present in the pinned older hit7 source;current table/species/piercing contracts and shared services do not certify its entire executable body. Modern full factory/seed ownership/event/room/candidate/search/PvP/item-only/client parity remains bounded.',
   'Held/rearmed inspection clocks do not certify uninterrupted absolute lifetime through long downtime. Loaded/offline aging and owner/death/respec/facet cleanup verify existing host policies;modern dead-caster continuation remains unverified. Old uncaptured parent/ray compatibility paths are preserved.',
   'Confirmed targeting remains unchanged;own-tile/ties/perpendicular boundaries remain uncertain. Final effects/audio and talent/hotkey icons are separate placeholders. No class completion,deployment or save reset.'])
 output=ROOT/'tools/reference/heaven-bolt-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('FoH child:1089combined/354focused,9new/315retained wire actions,2held/rearmed original-flight reloads,4audits/71Python;106installed unchanged.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
