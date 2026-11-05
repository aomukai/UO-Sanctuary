#!/usr/bin/env python3
"""Bind explicit live generated-item resources to native persistence evidence."""
import argparse,json
from pathlib import Path
from collections import Counter
from record_generated_npc_resources import expected as previous_expected
from snapshot_skill_build import check as snapshot
import skill_checkpoint_evidence as e
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/generated-npc-live-resources';COMPACT=ROOT/'.local/class-skills/generated-npc-live-resources-compact';REGRESSION=ROOT/'.local/class-skills/generated-npc-live-resources-regression';BASELINE=ROOT/'.local/class-skills/generated-npc-live-resources-regression-baseline';PARENT_CORE=ROOT/'.local/class-skills/generated-npc-live-resources-regression-parent-core';PARENT='287ea7e2447e7db1c598978e01de176f90982b78'
CHANGED={'src/Sanctuary.Content/GeneratedNpcStatGraph.cs','src/Sanctuary.Content/GeneratedNpcLiveResources.cs','src/Sanctuary.Content/NpcEquipmentResources.cs','src/Sanctuary.Content/NpcEquipmentRegeneration.cs','src/Sanctuary.Content/BattleOrders.cs','src/Sanctuary.Content/PaladinVigor.cs','tests/Sanctuary.Integration/GeneratedNpcLiveResourceChecks.cs'}
def build(check=False):
 born=e.load(COMPACT/'birth-inputs.json');audit=e.load(STAGE/'audit-inputs.json');reg=e.load(REGRESSION/'birth-inputs.json')
 assert born['artifacts']==audit['artifacts']==reg['artifacts']and born['copiedData']==audit['copiedData']==reg['copiedData']
 for path in (COMPACT/'birth-inputs.json',STAGE/'audit-inputs.json',REGRESSION/'birth-inputs.json'):
  snapshot(STAGE/'pre-build-2.json',path)
  for p,h in e.load(path)['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
 for kind in ('Content','Integration'):assert e.sha(STAGE/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
 old=e.load(ROOT/'.local/class-skills/generated-npc-resources/pre-build-2.json')['inputs'];now=e.load(STAGE/'pre-build-2.json')['inputs']
 assert {p for p in old['sourceInputs'].keys()|now['sourceInputs'].keys()if old['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}==CHANGED
 assert old['projectInputs']==now['projectInputs']and old['copiedData']==now['copiedData']
 parent=e.load(ROOT/'tools/reference/generated-npc-resources-native-evidence.json')
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==parent['artifacts'][name]
 expected=previous_expected();expected['generated-npc-live-resources-check']+=1;expected['generated-npc-live-resources-reload-check']+=2
 log=e.cohort(COMPACT,expected,51);assert log.count('Generated NPC live resources:5original recipients;source-matched birth;')==3
 controls=Counter({'iron-golem-birth-check':3})
 for family in ('iron-golem-items','npc-resource-callbacks','battle-orders','oak-sage-body','paladin-aura'):
  controls[family+'-check']+=1;controls[family+'-reload-check']+=2
 controls['battle-orders-result']=3;controls['paladin-aura-result']=1;controls['paladin-aura-reload-result']=2
 reglog=e.cohort(REGRESSION,controls,24)
 # Parent binary comparison is explicitly an artifact baseline,not a claim
 # that its older assemblies were built from today's source inventory.
 baseline=e.load(BASELINE/'birth-inputs.json');parentcore=e.load(PARENT_CORE/'birth-inputs.json')
 assert baseline['artifacts']==parentcore['artifacts']==parent['artifacts']and baseline['copiedData']==born['copiedData']==parentcore['copiedData']
 parentlog=e.cohort(PARENT_CORE,controls,24)
 failure='System.InvalidOperationException: Mage buff learned summon'
 assert failure in(BASELINE/'birth-native-checks.log').read_text()and failure in(STAGE/'failed-regression-1/birth-native-checks.log').read_text()
 # Wardrobe cleanup warnings belong to these older consumer fixtures;record
 # their occurrence in both builds instead of hiding them as fresh success.
 warning='during world save.';assert warning in reglog and warning in parentlog
 saved=e.load(COMPACT/'generated-npc-live-resources-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==5
 assert [r['Case']for r in saved['Actors']]==[8,587,576,578,602]and len({r['Actor']for r in saved['Actors']})==5
 for r in saved['Actors']:
  state=json.loads(r['State']);assert state['Version']==0 and state['Actor']==r['Actor']and state['Owner']==saved['Owner']and state['ActiveItems']==r['Items']and len(r['Items'])==len(r['Payloads'])==1
  assert 0<r['Life']<r['MaximumLife']and 0<r['Mana']<r['MaximumMana']and 0<r['Stamina']<r['MaximumStamina']
  assert r['Life']%256!=0 and r['Mana']%256!=0 and r['Stamina']%256!=0
  item=json.loads(r['Payloads'][0]);assert item['Equipment']['Actor']==r['Actor']and item['Equipment']['Owner']==saved['Owner']
 for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-2.log')).read_text()for t in('Build succeeded.','0 Warning(s)','0 Error(s)'))
 audits=e.audits(STAGE,PARENT,born['artifacts']);commands=e.load(STAGE/'verification-commands.json');assert len(commands)==4 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text();protected=e.protection()
 files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_generated_npc_live_resources.py',ROOT/'tests/check_generated_npc_resource_regression.py']+[ROOT/p for p in CHANGED]
 files += [ROOT/'tools/reference/generated-npc-resources-source-evidence.json',ROOT/'tools/reference/generated-npc-resources-native-evidence.json']
 files += [ROOT/'docs'/n for n in('generated-npc-live-resources-checkpoint.md','skill-mechanical-reference.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
 files += e.stage_files(STAGE,COMPACT,REGRESSION,BASELINE,PARENT_CORE)
 for folder in STAGE.glob('failed-*'):files += [p for p in folder.rglob('*')if p.is_file()]
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
 receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=2,integrationBuild=2,sourceSnapshot=2,compactRun=2,compactMarkers=51,regressionRun=3,regressionMarkers=24,parentRegressionMarkers=24,boots=3,actualReloads=2,saves=3,liveOriginalRecipients=5,liveOriginalItems=5,nativePayloadRestoredFromManifest=False,pythonTests=71,verificationCommands=4,auditFingerprints=audits,protection=protected,
  scope='Explicit source-resource profiles bind original generated items to native fractional maxima/current pools and persistent source recipes/regeneration/last applied skill percentages. Derived load/getter reconstruction disables resource callbacks;existing native ledgers retain wounds. Source-matched birth,base/logical-item/Orders/Vigor/Oak transitions,tiny/empty pools,trial refusal,recipe aliasing and source regeneration pass on five original recipients through two reloads. Existing numeric item/resource regressions and independent resource source proof remain retained.',
  limits=['Resource-only prepared recipient admission is not a learned actor,full combat/item-skill/event/aura/state/charge/rate/resistance consumer or mutable/physical item-transfer path. Unsupported time and outer-callback dependencies reject without filtering source generation pools. Source bases are explicit,not UO attribute conversions.',
   'Five live fixtures have one original item each;further multi-item active-order combinations and active buff expiry across saves remain unverified. Stamina recovery is frozen only for the inspection roster;D2 NPC mana/stamina recovery cadence remains a separate consumer. Safe-area parking of these new recipients is not claimed by the older failing test.',
   'The legacy safe-area suite fails Mage buff learned summon identically with current and pushed parent assemblies. Full failure and artifact baseline are retained;the cause remains a nonblocking Necromancer follow-up. The scoped BO/Oak/Paladin suites pass two reloads on both current and parent binaries;both retain older WardrobeSet cleanup warnings during disposable saves.',
   'Native1caught changed source raw-construction/merge callback interleaving;corrected before accepted Content2/Integration2/native2. Regression1stops at the reproduced legacy Mage failure;regression2passes initial assertions but misses its Battle Orders harness manifest mapping;neither is accepted reload evidence.',
   'No new handler/completed class,deployment,installed build/save/config/client change,new skill effect/audio or icon. Current executable/world/allocation seed parity remains unproved.'])
 e.write('generated-npc-live-resources-native-evidence.json',receipt,files,check)
 print('Generated NPC live resources:5original recipients/two reloads;51compact+24resource-consumer markers;24parent controls;71Python/audits/protection pass;legacy Mage safe-area gap retained.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
