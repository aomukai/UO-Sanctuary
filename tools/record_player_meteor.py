#!/usr/bin/env python3
"""Bind prepared player Meteor to the selected source/native/save/audit checkpoint."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from record_blaze import proof
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/sorceress-player-meteor';BUILD=4;PARENT='ea8d6bc'
sha=inventory.sha

def historical(name):return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)
def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text();assert sum(counts.values())==595 and ': failed' not in log and ' ERR]' not in log
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_player_meteor import configuration
 c=configuration(True);assert len(c['extra_families'])==43 and len(c['extra_checks'])==10 and len(c['extra_target_input_commands'])==56
 for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in c['extra_checks']:assert counts[name]==3,name
 for name in c['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 assert counts['mage-meteor-live-result']==3
 for text in ('Player Meteor:55080source endpoint comparisons','Mage Meteor:320 complete normal-center source expiry/hit14/area/field contexts',
 'Fire Wall:13770source field endpoints,2400accepted source cast plans/5760cast contexts,840shared maker cases,18native rate41/MDR mitigation comparisons',
 'Blaze:765rank/hard goldens,23040complete-source rolls/120native HP-return comparisons','TK source/native:rolls=7680 deliveries=2560 deaths=864 pushes=848',
 'Ordered monster release:1616native complete-source compositions','NPC return:60488complete-source policies/exact traces'):assert text in log,text
 saved=json.loads((STAGE/'player-meteor-fixtures.json').read_text());assert saved['Generation']==2 and saved['Mana']>0 and saved['Life']>0 and saved['TargetLife']>0
 assert saved['Warnings']==[] and len(set(saved['Completed']))==2 and len(saved['Fields'])==54
 assert all(f['Owner']==saved['Owner'] and f['SkillRank']>20 and f['Size']==2 and f['DamageRate']==41 and f['MeteorModifiers'] is not None for f in saved['Fields'])
 focused=inventory.validate_inventory(STAGE/f'diagnostic-{BUILD}/birth-inputs.json');assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==tested['copiedData']
 flog=(STAGE/f'diagnostic-{BUILD}/birth-native-checks.log').read_text();assert sum(markers(STAGE/f'diagnostic-{BUILD}/birth-native-checks.log').values())==59 and ' ERR]' not in flog and ': failed' not in flog
 assert flog.count('Shutting down')==flog.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'diagnostic-run-{BUILD}.log').read_text()
 for kind in ('content','integration'):
  text=(STAGE/f'{kind}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
  path=f'{kind}/Sanctuary.{kind.title()}.dll';assert sha(STAGE/path)==sha(STAGE/f'focused-accepted-{BUILD}'/path)
 creation=proof('meteor-creation-source-evidence.json',5184,4);damage=proof('meteor-damage-source-evidence.json',55080,16)
 for p in (creation,damage):assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
 mage=ROOT/'tools/reference/mage-meteor-source-evidence.json';assert mage.read_bytes()==historical(str(mage.relative_to(ROOT)));assert json.loads(mage.read_text())['fixture']['cases']==320
 parent=ROOT/'tools/reference/fire-wall-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
 new=next(s for s in now['skills'] if s['talentId']=='sorceress.meteor');old=next(s for s in before['skills'] if s['talentId']=='sorceress.meteor')
 assert new['verification']==old['verification']=='partial-native-coverage';old['remaining']=new['remaining'];assert now==before and not now['completedClasses']
 audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
 assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
 assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text();fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  audit=json.loads((STAGE/name).read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text());assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for p,d in audit['InputHashes'].items():assert sha(ROOT/p)==d,p
  old=json.loads(historical('tools/reference/'+name));ignore=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
  assert {k:v for k,v in audit.items() if k not in ignore}=={k:v for k,v in old.items() if k not in ignore};fingerprints.append(len(audit['InputHashes']))
 assert fingerprints==[899,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/f'checks-{BUILD}.log').read_text()
 for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for p,d in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==d,p
 files=[Path(__file__).resolve(),ROOT/'tests/check_player_meteor.py',ROOT/'tools/probe_meteor_creation.py',ROOT/'tools/probe_meteor_damage.py',creation,damage,mage,parent,ROOT/'tools/build_skill_roadmap.py',
 *(ROOT/'docs'/n for n in ('sorceress-player-meteor-checkpoint.md','sorceress-blizzard-preflight.md','sorceress-meteor-preflight.md','sorceress-fire-ground-preflight.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
 *(STAGE/n for n in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','player-meteor-fixtures.json','mage-meteor-fixtures.json',f'diagnostic-{BUILD}/birth-inputs.json',f'diagnostic-{BUILD}/birth-native-checks.log',f'diagnostic-run-{BUILD}.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
 *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Prepared player Meteor warning,ordered area impact and typed saved fields',installed=False,graphicalClientVerified=False,completedClasses=[],
 verified=dict(nativeMarkers=595,focusedMarkers=59,boots=3,reloads=2,saves=3,olderCreationCompositions=5184,olderDamageCaptureCompositions=55080,nativeEndpointComparisons=55080,reusedCenterCompositions=320,newServerWireActions=6,retainedServerWireActions=162,fixtureFamilies=43,sharedChecks=10,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
 established=['Prepared right-only release resolves live selected aim/current rank before once-only fractional mana and30frame local delay. A captured stationary60frame warning does not follow the target. Impact endpoints/mastery are retained;one raw packet is copied across recipients,with flags5/hit2 through existing ordered HP/death/return services. Actual native get-hit/death and field no-get-hit controls pass.',
 'Eighteen source producer offsets reuse the existing Mage/Phoenix controller. Size2/rate41 source03 fields retain rank but capture current child hard points/gear. Skill synergy follows HitShift8;field Inferno synergy precedes HitShift3;mastery follows once. Typed live potion lanes cross fire immunity and stop on removal. Saved independent field seeds and original payloads persist.',
 'Offline/missed-load warnings expire harmlessly;fields do not replay missed damage or RNG. Two original saved warnings naturally impact across two reloads while original fields/caster/target/gear/wounds/mana continue. Inspection deadline holds preserve field remaining lifetimes. Legacy unextended payloads keep their historical route. Six new wire actions retain162prior actions;Mage Meteor natural and saved regression remains included.',
 'Source compositions cover declared older routines/services,not modern executable proof. Four fresh audits,71Python tests,573named definitions and240skills retain item decisions;106installed artifacts remain unchanged.'],
 remaining=['No class certificate/deployment/graphical-client verification. Modern outer cast/flag64/factory/room/terrain/grid/filter/global seed,full damage fill and animation RNG/lifecycle correspondence remain explicit. Native terrain/allocation/recipient metadata and current per-missile entropy are declared adapters.',
 'Unmapped/PvP/hireling/sand-leaper/death-delay/incoming-Damage-override actor contexts require separate adapters;they are flagged rather than assigned invented D2 identities. Shared source/native negative MDR intermediate/clamp and regeneration/event parity gaps remain. Charged/oskill,old-DLL/unheld concurrent full-world saves and final presentation remain open.',
 'Warning/fall/impact/burning ground and cast sound are identified placeholders;effects/audio and tree/right/quick icons remain in separate ledgers. Inferno is individually deferred while unaffected skills continue.'])
 out=ROOT/'tools/reference/sorceress-player-meteor-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Player Meteor:595combined/59focused markers,5184creation/55080capture source and native endpoints,320reused center cases,6new/162retained wire actions,two original natural warning/field resumes;four audits/71Python;106installed hashes unchanged;explicit modern/client/assets gaps.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
