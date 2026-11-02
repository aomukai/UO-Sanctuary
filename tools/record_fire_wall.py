#!/usr/bin/env python3
"""Bind the selected Fire Wall runtime build to source,native,save and audit evidence."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from record_blaze import proof
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sorceress-fire-wall'
BUILD=8
PARENT='58486ecc379583a843d5468b838a33f2b2131114'
sha=inventory.sha

def historical(name):return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)
def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
 assert sum(counts.values())==572 and ': failed' not in log and ' ERR]' not in log
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'))
 from check_fire_wall import configuration
 c=configuration(True);assert len(c['extra_families'])==41 and len(c['extra_checks'])==10 and len(c['extra_target_input_commands'])==54
 for n in c['extra_families']:assert counts[n+'-check']==1 and counts[n+'-reload-check']==2,n
 for n in c['extra_checks']:assert counts[n]==3,n
 for n in c['extra_target_input_commands']:
  assert counts[n+'-check']==counts[n+'-result']==3,n
  assert log.count(' '+n+': windup')==3,n
 for text in ('Fire Wall:13770source field endpoints,2400accepted source cast plans/5760cast contexts,840shared maker cases,18native rate41/MDR mitigation comparisons',
              'Blaze:765rank/hard goldens,23040complete-source rolls/120native HP-return comparisons',
              'Energy Shield:765rank/hard goldens,25344complete-source kernel/792native absorption comparisons',
              'Buff payment:45native self/ally/fallback comparisons with complete generic wrapper',
              'TK source/native:rolls=7680 deliveries=2560 deaths=864 pushes=848',
              'Ordered monster release:1616native complete-source compositions','NPC return:60488complete-source policies/exact traces'):
  assert text in log,text
 saved=json.loads((STAGE/'fire-wall-fixtures.json').read_text())
 assert saved['Generation']==2 and saved['Mana']>0 and saved['Life']>0 and saved['TargetLife']>0
 assert len(saved['Makers'])==2 and len(saved['Fields'])>1
 rank=saved['Makers'][0]['Rank'];assert rank>20
 for m in saved['Makers']:
  assert m['Owner']==saved['Owner'] and m['Rank']==rank and m['Min256']==m['Max256']==0
  assert 0<m['Motion']['Frame']<m['Motion']['Frames']==7+2*rank and m['Motion']['VelocityFp8']==2304
 assert all(f['Owner']==saved['Owner'] and f['SkillRank']==rank and f['FireWallModifiers'] is not None and f['DamageRate']==41 for f in saved['Fields'])
 focused=inventory.validate_inventory(STAGE/f'diagnostic-{BUILD}/birth-inputs.json')
 assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==tested['copiedData']
 flog=(STAGE/f'diagnostic-{BUILD}/birth-native-checks.log').read_text()
 assert sum(markers(STAGE/f'diagnostic-{BUILD}/birth-native-checks.log').values())==61 and ' ERR]' not in flog and ': failed' not in flog
 assert flog.count('Shutting down')==flog.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'diagnostic-run-{BUILD}.log').read_text()
 for kind in ('content','integration'):
  text=(STAGE/f'{kind}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
  n=f'{kind}/Sanctuary.{kind.title()}.dll';assert sha(STAGE/n)==sha(STAGE/f'focused-accepted-{BUILD}'/n)
 assert sha(STAGE/'content/Sanctuary.Content.dll')==sha(STAGE/'attempt-7/content/Sanctuary.Content.dll'),'Scene isolation must not change production combat code'
 failed=json.loads((STAGE/'attempt-7/birth-inputs.json').read_text())
 assert failed['artifacts']['Assemblies/Sanctuary.Content.dll']==tested['artifacts']['Assemblies/Sanctuary.Content.dll']
 storm=json.loads((STAGE/'attempt-7/storm-pulse-fixtures.json').read_text());wall=json.loads((STAGE/'attempt-7/fire-wall-fixtures.json').read_text())
 center=wall['Fields'][0]
 assert wall['Map']==storm['Map'] and wall['Target']>storm['Storm']['LastTarget'] and wall['TargetLife']>0
 assert (center['X']-storm['Point']['X'])**2+(center['Y']-storm['Point']['Y'])**2<(storm['Storm']['RadiusSubtiles']/2)**2
 assert 'storm-pulse-reload-result: failed' in (STAGE/'attempt-7/birth-native-checks.log').read_text()

 old=json.loads((ROOT/'.local/class-skills/sorceress-blaze/birth-inputs.json').read_text())
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert old['artifacts'][n]==tested['artifacts'][n]
 assert old['copiedData'].keys()==tested['copiedData'].keys()
 assert {p for p in tested['copiedData'] if old['copiedData'][p]!=tested['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
 creation=proof('fire-wall-creation-source-evidence.json',6600,5);damage=proof('fire-wall-damage-source-evidence.json',27540,15);returns=proof('fire-wall-return-source-evidence.json',15360,9)
 for p in (creation,damage):assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
 parent=ROOT/'tools/reference/blaze-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
 new=next(s for s in now['skills'] if s['talentId']=='sorceress.firewall');old=next(s for s in before['skills'] if s['talentId']=='sorceress.firewall')
 assert new['verification']==old['verification']=='partial-native-coverage';old['remaining']=new['remaining'];assert now==before and not now['completedClasses']
 audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
 assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
 assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text();fingerprints=[]
 for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  audit=json.loads((STAGE/n).read_text());assert audit==json.loads((ROOT/'tools/reference'/n).read_text())
  assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for p,d in audit['InputHashes'].items():assert sha(ROOT/p)==d,p
  old=json.loads(historical('tools/reference/'+n));ignore=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
  assert {k:v for k,v in audit.items() if k not in ignore}=={k:v for k,v in old.items() if k not in ignore};fingerprints.append(len(audit['InputHashes']))
 assert fingerprints==[898,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/f'checks-{BUILD}.log').read_text()
 for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in text
 assert 'Traceback' not in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for p,d in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==d,p
 files=[Path(__file__).resolve(),ROOT/'tests/check_fire_wall.py',ROOT/'tools/probe_fire_wall_return.py',ROOT/'tools/probe_fire_wall_creation.py',ROOT/'tools/probe_fire_wall_damage.py',creation,damage,returns,parent,ROOT/'tools/build_skill_roadmap.py',
  *(ROOT/'docs'/n for n in ('sorceress-fire-wall-native-checkpoint.md','sorceress-fire-wall-creation-checkpoint.md','sorceress-fire-ground-preflight.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
  *(STAGE/n for n in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','fire-wall-fixtures.json',f'diagnostic-{BUILD}/birth-inputs.json',f'diagnostic-{BUILD}/birth-native-checks.log',f'diagnostic-run-{BUILD}.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
  *(STAGE/'attempt-7'/n for n in ('birth-inputs.json','birth-native-checks.log','storm-pulse-fixtures.json','fire-wall-fixtures.json')),
  *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Fire Wall prepared cast,maker and typed saved field',installed=False,graphicalClientVerified=False,completedClasses=[],
  verified=dict(nativeMarkers=572,focusedMarkers=61,boots=3,reloads=2,saves=3,olderCreationCompositions=6600,olderDamageCaptureCompositions=27540,olderRate41ReturnCompositions=15360,nativeEndpointComparisons=13770,nativeAcceptedRequestPlans=2400,sharedTrailComparisons=840,nativeRealItemMdrMitigationComparisons=18,newServerWireActions=6,retainedServerWireActions=156,fixtureFamilies=41,sharedChecks=10,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),
  markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
  established=['Prepared right-only player casts resolve live selected coordinates,refuse invalid/cancelled/insufficient releases before once-only current-rank payment,and apply localdelay35. Two fixed-point makers precede the center field;the existing Molten Boulder trail kernel emits from the previous cell/flag before movement/lifetime,including a qualifying final-frame child.',
   'Typed stationary Fire Wall fields share Blaze point lookup,roll/soft-hit and ordered spell mitigation/HP-return services with their own rate41. Maker zero damage is not inherited weapon damage. Child rank is retained while hard/gear values are live at child creation;existing fields retain captured payloads. Live potion components cross fire immunity and stop on removal. Legacy rankless fields/makers retain old data/route.',
   'Missed/offline maker movement/emission and field damage/RNG do not replay;real disk loads age missed updates. Same original partially travelled makers/fields,caster,target,orb,wounds and mana survive two reloads and naturally advance,emit children and damage. Fixture inspection holds preserve remaining time budget;original field RNG advances without recapture. Six new wire actions retain156prior actions.',
   'Nonzero-MDR comparison uses an actual eligible metal original with18MDR through shared numeric mitigation,not a fabricated property or unsupported HP adapter. Source comparisons cover the declared complete older routines and services;four fresh audits/71Python/573named/240catalogue/generators preserve item decisions and106installed artifacts.'],
  remaining=['No class certificate/deployment/graphical-client verification. Modern self-aim,full outer LOS/grid/terrain/path/room/factory/stat/global seed,animation RNG correlation and lifecycle equivalence remain open. Older source compositions use declared services,not modern binary proof.',
   'Maker mode8 LastCollide is retained,but zero-damage hit-by-missile event subscribers explicitly warn as unsupported. Native incoming-damage overrides,nonzero-MDR actor HP adapters,passive avoidance/PvP/special/death-delay actors,charged/oskill,old-DLL/unheld full-world saves and final presentation remain open. Older negative MDR intermediate versus native clamp remains a shared documented gap.',
   'Shared ground fire/cast sound are identified placeholders;effects/audio and tree/hotkey icons remain separate.'])
 out=ROOT/'tools/reference/fire-wall-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Fire Wall:572combined/61focused markers,6600creation/27540damage/15360return source cases,13770endpoints/2400plans/840trail/18MDR numeric comparisons,6new/156retained wire actions,two original maker/field resumes;four audits/71Python;106installed hashes preserved;explicit modern/event/client/assets gaps.')
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
