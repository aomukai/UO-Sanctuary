#!/usr/bin/env python3
"""Bind Holy Shield start payment,do18 rank rules and original save/action evidence."""
import argparse,json,subprocess,sys
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/holy-shield-cast';sha=inventory.sha
PARENT='af1275cc964a40f8e58b30fd6c2b83a214eb0881';FOCUS=4;BUILD=5;META='Data/Sanctuary/Balance/class-skill-status.json'
def historical(path):return subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT)
def passed(path,total):
 s=path.read_text();m=markers(path)
 assert sum(m.values())==total and ': failed' not in s and ' ERR]' not in s,path
 assert s.count('Shutting down')==s.count('Writing world save snapshot done')==3,path
 return s,m

def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 log,counts=passed(STAGE/'birth-native-checks.log',1104);assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_holy_shield_cast import configuration
 c=configuration(True);assert (len(c['extra_families']),len(c['extra_checks']),len(c['extra_target_input_commands']))==(76,18,110)
 assert c['save_command']==c['save_result']=='holy-shield-cast-save'
 for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in c['extra_checks']:assert counts[name]==3,name
 for name in c['extra_target_input_commands']:assert counts[name+'-check']==counts[name+'-result']==3 and log.count(' '+name+': windup')==3,name
 assert counts['holy-shield-cast-save']==3 and counts.get('player-teleport-save',0)==0
 assert log.count('Holy Shield cast:120ordinary source/native start/do components;')==1
 saved=json.loads((STAGE/'holy-shield-cast-checks.json').read_text());state=saved['State']
 assert (saved['SaveStarts'],saved['Reloads'],saved['Mana'],saved['PendingMana'])==(3,2,17,17)
 assert state['Owner']==saved['Owner'] and state['Rank']==1 and state['BlockBonus']==14 and saved['PendingOwner']!=saved['Owner']
 teleport=json.loads((STAGE/'player-teleport-fixtures.json').read_text());assert teleport['PendingStaged'] and teleport['Generation']==2
 focus=STAGE/f'focused-accepted-{FOCUS}';fi=json.loads((focus/'birth-inputs.json').read_text());passed(focus/'birth-native-checks.log',395)
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
  if x!=y:assert x['talentId']=='paladin.holyshield';changed.append(x['talentId']);y['remaining']=x['remaining']
 assert changed==['paladin.holyshield'] and now==old and not now['completedClasses']
 parent=ROOT/'tools/reference/heaven-bolt-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 prior=json.loads((ROOT/'.local/class-skills/heaven-bolt/birth-inputs.json').read_text())
 for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert prior['artifacts'][p]==tested['artifacts'][p]
 previous=json.loads(parent.read_text())['markers'];retained=dict(counts)
 for name in ('holy-shield-cast-check','holy-shield-cast-reload-check','holy-shield-cast-save','holy-shield-cast-right-check','holy-shield-cast-right-result','holy-shield-cast-quick-check','holy-shield-cast-quick-result'):retained.pop(name)
 retained['player-teleport-save']=3;assert retained==previous,'Retained marker decisions differ beyond the explicit save coordinator bridge'
 negative=STAGE/'negative-start-payment';ni=json.loads((negative/'birth-inputs.json').read_text());s=(negative/'birth-native-checks.log').read_text()
 assert 'holy-shield-cast-check: failed' in s and 'Holy Shield did not pay35mana at successful start' in s
 assert sha(negative/'content/Sanctuary.Content.dll')==ni['artifacts']['Assemblies/Sanctuary.Content.dll']==prior['artifacts']['Assemblies/Sanctuary.Content.dll']!=sha(STAGE/'content/Sanctuary.Content.dll')
 for name in ('HolyShields.cs','ClassCombat.cs','PaladinCombat.cs'):assert (negative/name).read_bytes()==historical('src/Sanctuary.Content/'+name)
 assert sha(negative/'HolyShieldCastChecks.cs')==ni['sourceInputs']['tests/Sanctuary.Integration/HolyShieldCastChecks.cs']
 diagnostic=STAGE/'focused-diagnostic-1';di=json.loads((diagnostic/'birth-inputs.json').read_text());s=(diagnostic/'birth-native-checks.log').read_text()
 assert 'Holy Shield cancellation/refund/replay death' in s
 assert sha(diagnostic/'HolyShieldCastChecks.cs')==di['sourceInputs']['tests/Sanctuary.Integration/HolyShieldCastChecks.cs']
 assert sha(diagnostic/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
 combined=STAGE/'full-diagnostic-3';ci=json.loads((combined/'birth-inputs.json').read_text());s=(combined/'birth-native-checks.log').read_text()
 assert 'Original Teleport offline logout map/point changed' in s and not json.loads((combined/'player-teleport-fixtures.json').read_text())['PendingStaged']
 assert sha(combined/'HolyShieldCastChecks.cs')==ci['sourceInputs']['tests/Sanctuary.Integration/HolyShieldCastChecks.cs']
 assert 'player-teleport-checks.json' in (combined/'HolyShieldCastChecks.cs').read_text()
 assert sha(combined/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
 source=ROOT/'tools/reference/holy-shield-cast-source-evidence.json';assert source.read_bytes()==historical(str(source.relative_to(ROOT)));sr=json.loads(source.read_text())
 assert sr['cases']==480 and len(sr['routineHashes'])==7
 for p,h in (sr['sourceHashes']|sr['currentTables']|sr['artifacts']).items():assert sha(ROOT/p)==h,p
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
 docs=[ROOT/'docs'/n for n in ('paladin-holy-shield-cast-checkpoint.md','paladin-holy-shield-preflight.md','paladin-might-aura-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files=[Path(__file__).resolve(),ROOT/'tests/check_holy_shield_cast.py',parent,source,*docs,
  *(ROOT/p for p in sr['artifacts']),*(STAGE/n for n in ('birth-inputs.json','birth-native-checks.log','holy-shield-cast-checks.json','holy-shield-checks.json','player-teleport-fixtures.json','heaven-bolt-checks.json',f'pre-build-{FOCUS}.json',f'pre-build-{BUILD}.json',f'full-run-{BUILD}.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log','catalogue-checks.log',*(f'{part}-build-{b}.log' for part in ('content','integration') for b in (FOCUS,BUILD)))),
  *(focus/n for n in ('birth-inputs.json','birth-native-checks.log',f'focused-run-{FOCUS}.log','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(negative/n for n in ('birth-inputs.json','birth-native-checks.log','HolyShieldCastChecks.cs','HolyShields.cs','ClassCombat.cs','PaladinCombat.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(diagnostic/n for n in ('birth-inputs.json','birth-native-checks.log','focused-run-1.log','HolyShieldCastChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(combined/n for n in ('birth-inputs.json','birth-native-checks.log','full-run-3.log','HolyShieldCastChecks.cs','PlayerTeleportChecks.cs','player-teleport-fixtures.json','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
  *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Holy Shield paid start,ungrouped do18 rank rules and atomic original-state persistence',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=1104,focusedMarkers=395,boots=3,reloads=2,saves=3,newServerWireActions=6,retainedServerWireActions=324,sourceCases=480,nativeComponentComparisons=120,paidStartsImmediatelyBeforeSave=3,cancellationControls=7,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['Successful learned shield-gated start36pays35mana once and creates a transient session ticket. Do18consumes the ticket without a second payment;weaker effects refuse after paid start,equal refreshes,higher replaces. Cold group1is separate. Stored rank/block,live hard Defiance and shield-dependent block/defense/Smite reuse existing services.',
   'Seven unchanged source routines compose480bounded cases;120ordinary inventory-present/no-bypass native component comparisons match admitted scope. Six actual right/quick inputs verify paid windup before effect and natural completion. Equipment/movement/Stop/incapacitation/owner controls cancel without replay;death and wardrobe resources retain existing shared policies. Shield removal/break and re-equip/repair preserve timed state and gate consumers.',
   'Version0fields remain unchanged;snapshot occurs before background writing and malformed/duplicate restore is atomic. The original completed buff/owner/shield/bindings/wounds/mana survive two real reloads with an explicit inspection deadline hold. One other original actor spends its finite three-start budget immediately before three actual saves;reload retains paid mana and restores no pending action or buff. The full save coordinator also executes inherited Teleport staging;only its outer result marker name changes.',
   'Parent DLL fails the start-payment regression. Diagnostic1misread the global death fractional-mana policy;fixture correction retains an identical production DLL. Combined3exposed a skipped Teleport stage caused by a wrong manifest name;the corrected coordinator uses the fixture-owned property and focused acceptance includes the retained Teleport branch. Focused4/final5compiled sources/projects/DLLs match with one remaining-note metadata bridge. Fresh audits/71Python/573named/240catalogue/generators pass;106installed artifacts unchanged.'],
  remaining=['Source proof uses the pinned older engine and current route tables,not modern executable/mode/animation certification. Full used-skill rank refresh,exact equipment/weapon switching,requirement-loss reactivity,item-only/granted permissions,low-mana pure-buff fallback and actual graphical client remain uncertain or unsupported.',
   'Bare do18does not recheck shields;client actions also use the declared host equipment-cancellation adapter. Inspection deadline holds do not certify unmodified thirty-second duration across long downtime;retained expiry tests are separate. Global death/wardrobe resource behavior is preserved.',
   'Final cast/persistent shield/glow/expiry/audio and talent/right/quick icons remain identified placeholders. No class completion,deployment or save reset.'])
 output=ROOT/'tools/reference/holy-shield-cast-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert output.read_text()==encoded
 else:output.write_text(encoded)
 print('Holy Shield:1104combined/395focused,6new/324retained wire actions,3paid starts at save,480source/120native components,4audits/71Python;106installed unchanged.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
