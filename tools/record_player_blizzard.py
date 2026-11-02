#!/usr/bin/env python3
"""Bind prepared Blizzard to the selected source/native/save/audit checkpoint."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from record_blaze import proof
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/sorceress-player-blizzard';BUILD=3;PARENT='a39d29e'
sha=inventory.sha

def historical(name):return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)
def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text();assert sum(counts.values())==612 and ': failed' not in log and ' ERR]' not in log
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_player_blizzard import configuration
 c=configuration(True);assert len(c['extra_families'])==44 and len(c['extra_checks'])==10 and len(c['extra_target_input_commands'])==58
 for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in c['extra_checks']:assert counts[name]==3,name
 for name in c['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 assert counts['mage-meteor-live-result']==3
 for text in ('Player Blizzard:12240native source endpoints/24384scatter comparisons','Player Meteor:55080source endpoint comparisons','Mage Meteor:320 complete normal-center source expiry/hit14/area/field contexts',
 'Fire Wall:13770source field endpoints,2400accepted source cast plans/5760cast contexts,840shared maker cases,18native rate41/MDR mitigation comparisons',
 'Blaze:765rank/hard goldens,23040complete-source rolls/120native HP-return comparisons','TK source/native:rolls=7680 deliveries=2560 deaths=864 pushes=848',
 'Ordered monster release:1616native complete-source compositions','NPC return:60488complete-source policies/exact traces'):assert text in log,text
 saved=json.loads((STAGE/'player-blizzard-fixtures.json').read_text());assert saved['Generation']==2 and saved['Mana']>0 and saved['Life']>0 and saved['FirstLife']>0 and saved['SecondLife']>0
 assert len(saved['Storms'])==1 and len(saved['Shards'])>=2
 s=saved['Storms'][0];assert s['Source'] and s['Owner']==saved['Owner'] and s['Rank']>20 and 1<s['Frame']<100
 assert all(x['Owner']==saved['Owner'] and x['Rank']==s['Rank'] and x['Modifiers'] is not None and 0<x['Remaining']<=9 for x in saved['Shards'])
 assert any(x['LastCollision'] in (saved['First'],saved['Second']) for x in saved['Shards'])
 focused=inventory.validate_inventory(STAGE/f'diagnostic-{BUILD}/birth-inputs.json');assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==tested['copiedData']
 flog=(STAGE/f'diagnostic-{BUILD}/birth-native-checks.log').read_text();assert sum(markers(STAGE/f'diagnostic-{BUILD}/birth-native-checks.log').values())==70 and ' ERR]' not in flog and ': failed' not in flog
 assert flog.count('Shutting down')==flog.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'diagnostic-run-{BUILD}.log').read_text()
 for kind in ('content','integration'):
  text=(STAGE/f'{kind}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
  path=f'{kind}/Sanctuary.{kind.title()}.dll';assert sha(STAGE/path)==sha(STAGE/f'focused-accepted-{BUILD}'/path)
 proofs=[proof('blizzard-'+n+'-source-evidence.json',cases,routines) for n,cases,routines in [('creation',5184,4),('center',130996,8),('damage',24480,16),('collision',3840,6)]]
 for p in proofs:assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
 parent=ROOT/'tools/reference/sorceress-player-meteor-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
 new=next(s for s in now['skills'] if s['talentId']=='sorceress.blizzard');old=next(s for s in before['skills'] if s['talentId']=='sorceress.blizzard')
 assert new['verification']==old['verification']=='partial-native-coverage';old['remaining']=new['remaining'];assert now==before and not now['completedClasses']
 audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
 assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
 assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text();fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  audit=json.loads((STAGE/name).read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text());assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for p,d in audit['InputHashes'].items():assert sha(ROOT/p)==d,p
  old=json.loads(historical('tools/reference/'+name));ignore=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
  assert {k:v for k,v in audit.items() if k not in ignore}=={k:v for k,v in old.items() if k not in ignore};fingerprints.append(len(audit['InputHashes']))
 assert fingerprints==[900,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/f'checks-{BUILD}.log').read_text()
 for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for p,d in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==d,p
 files=[Path(__file__).resolve(),ROOT/'tests/check_player_blizzard.py',*(ROOT/('tools/probe_blizzard_'+n+'.py') for n in ('creation','center','damage','collision')),*proofs,parent,ROOT/'tools/build_skill_roadmap.py',
 *(ROOT/'docs'/n for n in ('sorceress-player-blizzard-checkpoint.md','sorceress-blizzard-preflight.md','sorceress-frozen-orb-preflight.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
 *(STAGE/n for n in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','player-blizzard-fixtures.json',f'diagnostic-{BUILD}/birth-inputs.json',f'diagnostic-{BUILD}/birth-native-checks.log',f'diagnostic-run-{BUILD}.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
 *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Prepared Blizzard center and typed saved cold shards',installed=False,graphicalClientVerified=False,completedClasses=[],
 verified=dict(nativeMarkers=612,focusedMarkers=70,boots=3,reloads=2,saves=3,olderCreationCompositions=5184,olderCenterCompositions=130996,olderDamageCaptureCompositions=24480,olderCollisionCompositions=3840,nativeEndpointComparisons=12240,nativeScatterComparisons=24384,newServerWireActions=6,retainedServerWireActions=168,fixtureFamilies=44,sharedChecks=10,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
 established=['Prepared right-only release resolves live aim/rank before once-only mana and45frame local delay. A stationary100frame center emits25shards at elapsed0,4,...96 before lifetime decrement,using the existing Fissure/Blizzard sampler. Child skill rank is retained;hard points/gear/cold damage mastery are current at creation. Cold length100and endpoint rounding follow complete source capture.',
 'Shared first-recipient lookup filters LastCollide before selection. Each9frame shard hits once per eligible update after decrement,expires before the zero-frame hit,and preserves most-recent history. Overlapping targets alternate;an isolated target receives one hit. Direct cold payloads use existing retaliation/avoidance and ordered NPC mitigation/HP/cold/death/return services. Live typed potion lanes cross cold immunity and stop on removal.',
 'Source centers/shards interleave chronologically. Offline and missed-load frames age without emission/damage/RNG. Atomic restore preserves legacy unextended payloads;no seed/modifier history is reconstructed. Original saved center/frame/shards/seeds/history,caster,targets,orb,wounds and mana naturally continue across two authenticated reloads and produce an additional shard. Inspection holds preserve remaining budgets. Six new wire actions retain168prior actions.',
 'Four fresh audits preserve item/affix decisions;71Python tests,573named definitions,240catalogue skills and generators pass. All106protected installed artifacts remain unchanged;no deployment or class certificate.'],
 remaining=['Older source compositions use declared services,not modern executable proof. Full modern outer cast/flag64/factory/stat/room/grid/filter/0x40 occupancy/global seed/animation/lifecycle correspondence remains open;native terrain/ordered sector enumeration and seed creation are explicit adapters.',
 'Full damage-fill/block/event subscriber equivalence and regeneration/source negative-MDR intermediate versus native clamp remain shared gaps. Unmapped/PvP/hireling/sand-leaper/uninterruptible/death-delay/incoming-Damage-override actor contexts are flagged rather than assigned invented identities. Charged/oskill,old-DLL/unheld concurrent full-world saves and graphical-client acceptance remain separate.',
 'Falling ice,cold impact and cast/loop/hit audio are identified placeholders. Effects/audio and talent/right/quick icons remain separate ledgers. Inferno is individually deferred while unaffected skills continue.'])
 out=ROOT/'tools/reference/sorceress-player-blizzard-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Player Blizzard:612combined/70focused markers,5184creation/130996center/24480capture/3840collision source cases,12240native endpoints/24384scatter,6new/168retained wire actions,two original natural center/shard resumes;four audits/71Python;106installed hashes unchanged;modern/client/assets gaps explicit.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
