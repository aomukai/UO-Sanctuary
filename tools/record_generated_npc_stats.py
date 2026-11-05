#!/usr/bin/env python3
"""Bind source item-stat dispatch and numeric graphs to original native items."""
import argparse,json
from pathlib import Path
from record_npc_equip_placement import expected as previous_expected
from snapshot_skill_build import check as snapshot
import skill_checkpoint_evidence as evidence

ROOT=evidence.ROOT;STAGE=ROOT/'.local/class-skills/generated-npc-stats';COMPACT=ROOT/'.local/class-skills/generated-npc-stats-compact';PARENT='e6a0d1587fcda25c874208edd46c332dbc6899c5'
CHANGED={'src/Sanctuary.Content/GeneratedNpcStatGraph.cs','src/Sanctuary.Content/SourceItemStatUpdates.cs','tests/Sanctuary.Integration/GeneratedNpcStatChecks.cs'}
def expected(broader=False,compact=False):
 r=previous_expected(broader,compact);r['generated-npc-stats-check']+=1;r['generated-npc-stats-reload-check']+=2;return r
def build(check=False):
 born=evidence.load(COMPACT/'birth-inputs.json');audit=evidence.load(STAGE/'audit-inputs.json');assert born['artifacts']==audit['artifacts']and born['copiedData']==audit['copiedData']
 for path in (COMPACT/'birth-inputs.json',STAGE/'audit-inputs.json'):
  snapshot(STAGE/'pre-build-1.json',path)
  for p,h in evidence.load(path)['sourceInputs'].items():assert evidence.sha(ROOT/p)==h,p
 for kind in ('Content','Integration'):assert evidence.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 old=evidence.load(ROOT/'.local/class-skills/npc-equip-placement/pre-build-3.json')['inputs'];now=evidence.load(STAGE/'pre-build-1.json')['inputs']
 assert {p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}==CHANGED
 assert old['projectInputs']==now['projectInputs']and old['copiedData']==now['copiedData']
 parent=evidence.load(ROOT/'tools/reference/npc-equip-placement-native-evidence.json')
 for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]
 sourcepath=ROOT/'tools/reference/generated-npc-stats-source-evidence.json';source=evidence.load(sourcepath);controls=evidence.load(ROOT/'.local/class-skills/generated-npc-stats-source/cases.json')
 assert source['gateControls']==len(controls['inputs'])==len(controls['outputs'])==46800
 assert source['numericCompositions']==len(controls['numericInputs'])==len(controls['numericOutputs'])==1566 and source['multiItemCompositions']==18
 assert len(source['completeRoutineHashes'])==3 and len(source['inheritedRoutineHashes'])==23 and not source['currentExecutableParity']
 log=evidence.cohort(COMPACT,expected(False,True),45);assert log.count('Generated NPC stats:46800complete dispatcher controls;1566source numeric compositions including18multi-item orders')==3
 saved=evidence.load(COMPACT/'generated-npc-stats-fixtures.json');assert saved['Generation']==2 and len(saved['Holders'])==1566 and len({r['Actor']for r in saved['Holders']})==1566
 assert [r['Case']for r in saved['Holders']]==list(range(1566));allitems=[]
 for r,a in zip(saved['Holders'],controls['numericInputs']):
  assert len(r['Items'])==len(r['Payloads'])==len(a['Items']);allitems+=r['Items']
  for text,index in zip(r['Payloads'],a['Items']):
   item=json.loads(text);row=controls['items'][index];assert item['Version']==1 and item['Equipment']['Actor']==r['Actor']and item['Equipment']['Owner']==saved['Owner']and item['Equipment']['Kind']==a['Kind']and item['Equipment']['Body']==row['Body']
 assert len(allitems)==len(set(allitems))==1674
 for fixture,count in (('npc-equip-placement',1548),('generated-npc-item',2580),('item-quality-current',2624),('current-item-affixes',1680)):
  d=evidence.load(COMPACT/(fixture+'-fixtures.json'));assert d['Generation']==2 and len(d['Items'])==count
 for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-1.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=evidence.audits(STAGE,PARENT,born['artifacts']);assert audits=={'named-item-runtime-audit.json':1073,'first-loop-affix-audit.json':21}
 commands=evidence.load(STAGE/'verification-commands.json');assert len(commands)==5 and all(c['exitCode']==0 for c in commands)
 python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in python and '\nOK\n'in python;protected=evidence.protection()
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_generated_npc_stats.py',sourcepath]+[ROOT/p for p in CHANGED]
 for group in ('inputs','artifacts'):
  for p,h in source[group].items():assert evidence.sha(ROOT/p)==h,p
  files += [ROOT/p for p in source[group]]
 # The accepted claw presentation uses the existing mapping,not a new item
 # family or an unapproved reassignment of Kama's axe classification.
 gear=ROOT/'data/Sanctuary/Balance/uo-equipment.json';tekagi=next(r for r in evidence.load(gear)if r['type']=='Tekagi');assert (tekagi['category'],tekagi['affixType'],tekagi['baseCode'])==('Claws','h2h','clw');files.append(gear)
 files += [ROOT/'docs'/n for n in ('generated-npc-stats-checkpoint.md','skill-mechanical-reference.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files += evidence.stage_files(STAGE,COMPACT)
 for folder in [*STAGE.glob('failed-*'),STAGE/'discarded-source-2-3',STAGE/'before-exact-item-context']:files += [p for p in folder.rglob('*')if p.is_file()]
 status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=1,integrationBuild=1,sourceSnapshot=1,compactRun=1,compactMarkers=45,
  boots=3,actualReloads=2,saves=3,completeDispatcherControls=46800,numericCompositions=1566,multiItemCompositions=18,completeOriginalRoutines=26,originalNativeHolders=1566,originalNativeItems=1674,nativePayloadRestoredFromManifest=False,
  pythonTests=71,verificationCommands=5,auditFingerprints=audits,protection=protected,
  scope='Complete older add/remove item-stat callers and dual-wield policy compose with23retained complete stat/query/attachment functions. GeneratedNpcStatGraph restores actual original native raw items in inventory order and uses the source merge/removal path with explicit bases,clock,value-change,rate and vital services. Source/native flags,raw arrays,ownership,repeat,level changes,reverse removal and ordered value callbacks agree across1566compositions and two reloads.',
  limits=['The numeric recipient graph is a prerequisite,not global live Mobile setter/animation/item-skill/event admission or a learned Valkyrie/Shadow actor. Native tests supply explicit source bases and day angle91;they do not convert UO attributes or establish a Sanctuary world-time mapping. Missing time service rejects active timed fields;current NPC affix pools have no time property codes.',
   'Fresh native generated metadata has no BROKEN flag. Mutable durability/flags,item RNG/charges and stat mutation persistence need separate admission. Logical stat removal precedes physical transfer;changing original inventory/owner/payload invalidates the numeric frame instead of silently rebuilding.',
   'Gem/set/player book/main-weapon/world effects are explicit gate-trace services,not their complete bodies. Old player policy does not establish modern Warlock player rules. Source callback trace services do not execute the original live MAXHP branch or native HP synchronization.',
   'Native payloads/identities persist directly;fresh derived graph construction and callback replay are explicit fixture/caller operations,not proof of historical D2 save/load registration order. Current executable/world/allocation seed parity remains unproved.',
   'Tekagi remains the accepted existing claw asset/mapping;both-hand appearance/animation preview remains pending. No new art,handler,completed class,installed build/save/config/client change or deployment.'])
 evidence.write('generated-npc-stats-native-evidence.json',receipt,files,check)
 print('Generated NPC stats:46800gates/1566numeric compositions/26complete routines;1674original items/two reloads;45compact markers;71Python/5checks/audits/protection pass.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
