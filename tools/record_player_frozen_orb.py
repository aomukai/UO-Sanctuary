#!/usr/bin/env python3
"""Bind prepared Frozen Orb to the selected source/native/save/audit checkpoint."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from record_blaze import proof
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/sorceress-player-frozen-orb';BUILD=5;PARENT='2d772a6'
sha=inventory.sha

def historical(name):return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)
def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text();assert sum(counts.values())==635 and ': failed' not in log and ' ERR]' not in log
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_player_frozen_orb import configuration
 c=configuration(True);assert len(c['extra_families'])==45 and len(c['extra_checks'])==10 and len(c['extra_target_input_commands'])==61
 for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in c['extra_checks']:assert counts[name]==3,name
 for name in c['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 assert counts['mage-meteor-live-result']==3
 for text in ('Player Frozen Orb:9180native endpoints/9216shared source update comparisons','Player Meteor:55080source endpoint comparisons','Mage Meteor:320 complete normal-center source expiry/hit14/area/field contexts',
 'Fire Wall:13770source field endpoints,2400accepted source cast plans/5760cast contexts,840shared maker cases,18native rate41/MDR mitigation comparisons',
 'Blaze:765rank/hard goldens,23040complete-source rolls/120native HP-return comparisons','TK source/native:rolls=7680 deliveries=2560 deaths=864 pushes=848',
 'Ordered monster release:1616native complete-source compositions','NPC return:60488complete-source policies/exact traces'):assert text in log,text
 saved=json.loads((STAGE/'player-frozen-orb-fixtures.json').read_text());assert saved['Generation']==2 and saved['Mana']>0 and saved['Life']>0 and saved['FirstLife']>0 and saved['SecondLife']>0
 original=saved['Missiles'];assert any(x['Kind']==0 and x['Frame']>1 for x in original) and any(x['Kind']==2 and x['Frame']>1 for x in original)
 assert all(x['Owner']==saved['Owner'] and x['Rank']>20 and x['Source'] is not None for x in original)
 focused=inventory.validate_inventory(STAGE/f'diagnostic-{BUILD}/birth-inputs.json');assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==tested['copiedData']
 flog=(STAGE/f'diagnostic-{BUILD}/birth-native-checks.log').read_text();assert sum(markers(STAGE/f'diagnostic-{BUILD}/birth-native-checks.log').values())==87 and ' ERR]' not in flog and ': failed' not in flog
 assert flog.count('Shutting down')==flog.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'diagnostic-run-{BUILD}.log').read_text()
 for kind in ('content','integration'):
  text=(STAGE/f'{kind}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
  path=f'{kind}/Sanctuary.{kind.title()}.dll';assert sha(STAGE/path)==sha(STAGE/f'focused-accepted-{BUILD}'/path)
 proofs=[proof('frozen-orb-'+n+'-source-evidence.json',cases,routines) for n,cases,routines in [('creation',5760,9),('damage',13770,16),('emission',7878,4),('collision',9246,7)]]
 for p in proofs:
  if 'collision' not in p.name:assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
 parent=ROOT/'tools/reference/sorceress-player-blizzard-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
 new=next(s for s in now['skills'] if s['talentId']=='sorceress.frozenorb');old=next(s for s in before['skills'] if s['talentId']=='sorceress.frozenorb')
 assert new['verification']==old['verification']=='partial-native-coverage';old['remaining']=new['remaining'];assert now==before and not now['completedClasses']
 audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
 assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
 assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text();fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  audit=json.loads((STAGE/name).read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text());assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for p,d in audit['InputHashes'].items():assert sha(ROOT/p)==d,p
  old=json.loads(historical('tools/reference/'+name));ignore=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
  assert {k:v for k,v in audit.items() if k not in ignore}=={k:v for k,v in old.items() if k not in ignore};fingerprints.append(len(audit['InputHashes']))
 assert fingerprints==[901,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/f'checks-{BUILD}.log').read_text()
 for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for p,d in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==d,p
 files=[Path(__file__).resolve(),ROOT/'tests/check_player_frozen_orb.py',*(ROOT/('tools/probe_frozen_orb_'+n+'.py') for n in ('creation','damage','emission','collision')),*proofs,parent,ROOT/'tools/build_skill_roadmap.py',
 *(ROOT/'docs'/n for n in ('sorceress-player-frozen-orb-checkpoint.md','sorceress-frozen-orb-preflight.md','sorceress-hydra-preflight.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
 *(STAGE/n for n in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','player-frozen-orb-fixtures.json',f'diagnostic-{BUILD}/birth-inputs.json',f'diagnostic-{BUILD}/birth-native-checks.log',f'diagnostic-run-{BUILD}.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
 *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Prepared Frozen Orb root,trail and curved terminal cold shards',installed=False,graphicalClientVerified=False,completedClasses=[],
 verified=dict(nativeMarkers=635,focusedMarkers=87,boots=3,reloads=2,saves=3,olderCreationCompositions=5760,olderEmissionCompositions=7878,olderDamageCaptureCompositions=13770,olderCollisionContexts=9216,olderCollisionLifecycleCalls=30,nativeEndpointComparisons=9180,nativeUpdateComparisons=9216,newServerWireActions=9,retainedServerWireActions=174,fixtureFamilies=45,sharedChecks=10,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
 established=['Prepared left/right/quick release resolves live selected aim/rank before once-only fractional mana and25frame local delay. The source root emits30trail children before movement and16terminal children only at exact-zero remaining from the final moved position. Children retain rank and capture current hard synergy/gear;terminal vectors turn at0/2/4.',
 'Typed cold damage uses shared ordered HP/death/return and cold duration services. Root contacts retain history without invented damage/cold. Child LastCollide/contact/removal,immunity/live potion lanes,offline/load aging and atomic historical restore have native controls. Source/native comparisons remain bounded to their declared services.',
 'Original root and terminal shard,gear,caster/targets,wounds/mana and saved frame/payload/seed naturally continue across two authenticated reloads and produce another child. Nine new wire actions retain174prior actions across three boots. The Meteor fixture explicitly exercises ordinary and SOFTHIT notification paths without inventing a get-hit animation.',
 'Four fresh audits preserve item/affix decisions;71Python tests,573named definitions,240catalogue skills and generators pass. All106protected installed artifacts remain unchanged;no deployment or class certificate.'],
 remaining=['Older-engine compositions/current tables are not modern executable proof. Modern outer activation/InitSteps,room/grid/path quantization/candidate order,global/animation RNG,factory/stat and lifecycle correspondence remain open;native geometry and scheduling/entropy are explicit adapters.',
 'Full damage-fill/block/event subscribers,root zero-packet event callbacks,regeneration and source negative-MDR/native clamp remain shared gaps. Unmapped/PvP/hireling/uninterruptible/death-delay/incoming-Damage-override actors are flagged. Charged/oskill,old-DLL/unheld concurrent saves and graphical-client acceptance remain separate.',
 'Orb/ice sprites and cast/travel/hit audio are identified placeholders. Effects/audio and talent/left/right/quick icons remain separate ledgers. Inferno stays individually deferred;Hydra is next.'])
 out=ROOT/'tools/reference/sorceress-player-frozen-orb-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Player Frozen Orb:635combined/87focused markers,source capture/emission/collision controls,9180native endpoints/9216update traces,9new/174retained wire actions,two original natural root/curve resumes;four audits/71Python;106installed hashes unchanged;modern/client/assets gaps explicit.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
