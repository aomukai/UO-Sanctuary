#!/usr/bin/env python3
"""Bind prepared Hydra components to the selected source/native/save/audit checkpoint."""
import argparse,json,subprocess,sys
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from record_blaze import proof
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/hydra-prepared';BUILD=4;PARENT='af51168'
sha=inventory.sha

def historical(name):return subprocess.check_output(['git','show',PARENT+':'+name],cwd=ROOT)
def record(check=False):
 inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json');snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
 counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text();assert sum(counts.values())==638 and ': failed' not in log and ' ERR]' not in log
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
 sys.path.insert(0,str(ROOT/'tests'));from check_hydra_prepared import configuration
 c=configuration(True);assert len(c['extra_families'])==46 and len(c['extra_checks'])==10 and len(c['extra_target_input_commands'])==61
 for name in c['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2,name
 for name in c['extra_checks']:assert counts[name]==3,name
 for name in c['extra_target_input_commands']:
  assert counts[name+'-check']==counts[name+'-result']==3,name
  assert log.count(' '+name+': windup')==3,name
 assert counts['mage-meteor-live-result']==3
 for text in ('Hydra prepared:2880birth suppliers/41310endpoints/576clocks/7776AI decisions','Player Frozen Orb:9180native endpoints/9216shared source update comparisons','Player Meteor:55080source endpoint comparisons','Mage Meteor:320 complete normal-center source expiry/hit14/area/field contexts',
 'Fire Wall:13770source field endpoints,2400accepted source cast plans/5760cast contexts,840shared maker cases,18native rate41/MDR mitigation comparisons',
 'Blaze:765rank/hard goldens,23040complete-source rolls/120native HP-return comparisons','TK source/native:rolls=7680 deliveries=2560 deaths=864 pushes=848',
 'Ordered monster release:1616native complete-source compositions','NPC return:60488complete-source policies/exact traces'):assert text in log,text
 saved=json.loads((STAGE/'hydra-prepared-fixtures.json').read_text());assert saved['Generation']==2 and saved['Mana']>0 and saved['Life']>0
 assert saved['Birth']['Rank']>1 and saved['Birth']['DurationFrames']==250 and saved['Birth']['MaximumHeads']==18
 assert saved['Control']['Frame']==62 and saved['Control']['Dispatches']==2 and saved['Control']['Thinks']==2
 focused=inventory.validate_inventory(STAGE/f'diagnostic-{BUILD}/birth-inputs.json');assert focused['artifacts']==tested['artifacts'] and focused['copiedData']==tested['copiedData']
 flog=(STAGE/f'diagnostic-{BUILD}/birth-native-checks.log').read_text();assert sum(markers(STAGE/f'diagnostic-{BUILD}/birth-native-checks.log').values())==18 and ' ERR]' not in flog and ': failed' not in flog
 assert flog.count('Shutting down')==flog.count('Writing world save snapshot done')==3
 assert 'pass across3boots/2reloads' in (STAGE/f'diagnostic-run-{BUILD}.log').read_text()
 for kind in ('content','integration'):
  text=(STAGE/f'{kind}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in text and '0 Warning(s)' in text and '0 Error(s)' in text
  path=f'{kind}/Sanctuary.{kind.title()}.dll';assert sha(STAGE/path)==sha(STAGE/f'focused-accepted-{BUILD}'/path)
 proofs=[proof('hydra-'+n+'-source-evidence.json',cases,routines) for n,cases,routines in [('creation',12800,9),('ai',15552,8),('clock',576,8),('damage',41310,16),('bolt-creation',5760,10)]]
 for p in proofs:assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
 retained_owner=proof('hydra-retained-owner-damage-source-evidence.json',23040,14)
 parent=ROOT/'tools/reference/sorceress-player-frozen-orb-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
 now=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());before=json.loads(historical('data/Sanctuary/Balance/class-skill-status.json'))
 new=next(s for s in now['skills'] if s['talentId']=='sorceress.hydra');old=next(s for s in before['skills'] if s['talentId']=='sorceress.hydra')
 assert new['verification']==old['verification']=='partial-native-coverage';old['remaining']=new['remaining'];assert now==before and not now['completedClasses']
 audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
 assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
 assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text();fingerprints=[]
 for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
  audit=json.loads((STAGE/name).read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text());assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
  for p,d in audit['InputHashes'].items():assert sha(ROOT/p)==d,p
  old=json.loads(historical('tools/reference/'+name));ignore=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
  assert {k:v for k,v in audit.items() if k not in ignore}=={k:v for k,v in old.items() if k not in ignore};fingerprints.append(len(audit['InputHashes']))
 assert fingerprints==[903,21]
 text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text and 'FAILED' not in text
 text=(STAGE/f'checks-{BUILD}.log').read_text()
 for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in text
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for p,d in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==d,p
 negative=STAGE/'reproduce-pre-fix/birth-native-checks.log';assert 'Inactive expired Hydra rewound its frame or reset history' in negative.read_text() and 'hydra-prepared-check: failed' in negative.read_text()
 files=[negative,Path(__file__).resolve(),ROOT/'tests/check_hydra_prepared.py',*(ROOT/('tools/probe_hydra_'+n.replace('-','_')+'.py') for n in ('creation','ai','clock','damage','bolt-creation')),*proofs,retained_owner,ROOT/'tools/probe_hydra_retained_owner_damage.py',parent,ROOT/'tools/build_skill_roadmap.py',
 *(ROOT/'docs'/n for n in ('hydra-prepared-checkpoint.md','sorceress-hydra-preflight.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
 *(STAGE/n for n in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','hydra-prepared-fixtures.json',f'diagnostic-{BUILD}/birth-inputs.json',f'diagnostic-{BUILD}/birth-native-checks.log',f'diagnostic-run-{BUILD}.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
 *(ROOT/'tools/reference'/n for n in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
 result=dict(referenceVersion='3.3.93847',component='Hydra per-head cap/64bit chance corrections and captured birth/frame components',installed=False,graphicalClientVerified=False,completedClasses=[],
 verified=dict(nativeMarkers=638,focusedMarkers=18,boots=3,reloads=2,saves=3,sourceCases=75998,olderRetainedOwnerFireCases=23040,nativeBirthComparisons=2880,nativeEndpointComparisons=41310,nativeClockComparisons=576,nativeAiDecisionComparisons=7776,newServerWireActions=0,retainedServerWireActions=183,fixtureFamilies=46,sharedChecks=10,auditMarkers=4,auditFingerprints=fingerprints,pythonTests=71,unchangedInstalledArtifacts=106),markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
 established=['Existing learned Hydra now registers each successful head independently at18and uses canonical full64bit percentage. A native partial placement starts with17heads,blocks the first requested head and creates two,evicting exactly one oldest head while preserving the other16originals.',
 'HydraCreation captures three accrued fields and positive HydraMissile/current rank plus hard Fire Bolt/Fire Ball. Sorceress Fire Mastery and real equipment participate at birth. Shared endpoints retain the capture after caster gear/hard-point changes;41310native endpoints match complete source capture.',
 'HydraControl reuses shared ordinary event planning/neutral handoff with explicit acquisition,mode admission,dispatch and retirement services. Source/native576clock/7776AI and2880birth supplier controls pass. JSON retains frame/seed/deadlines/counters;offline advancement suppresses combat/RNG without extending expiry.',
 'A saved original native caster/orb/wounds/mana and captured birth/frame component continue pending model actions across two process reloads. This is prepared model continuation,not natural production Hydra head/bolt persistence. Zero new wire actions retain183existing actions across three boots. Four audits preserve item/affix decisions;71Python/573named/240catalogue checks pass;106installed artifacts unchanged.'],
 remaining=['Full production world controller/actual NPC emitting identity versus player ownership,ordered typed damage,target acquisition,collision/path/InitSteps/Slow Missiles/interception and final head/creator GUID retirement remain unfinished. Current virtual-head/bolt persistence remains on its earlier route. No full Hydra/class completion is claimed.',
 'Older source,current tables and owned animation metadata are bounded evidence,not modern executable proof. New controller actual input/resources/head/bolt original natural reloads and approved short-lived trap facet/offline integration remain next. Native source comparisons retain declared service limits;PvP/special actors/client/old-DLL/unheld saves remain separate.',
 'Head/bolt/death effects/audio and talent/right/quick icons remain independent identified placeholders. No installation or save reset.'])
 out=ROOT/'tools/reference/hydra-prepared-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Hydra prepared:638combined/18focused markers,2880birth/41310endpoints/576clocks/7776AI comparisons,partial per-head cap and64bit chance;two prepared-model/caster/gear reloads,183retained wire actions,four audits/71Python;106installed hashes unchanged;full world controller remains next.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
