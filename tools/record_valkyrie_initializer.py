#!/usr/bin/env python3
"""Bind source-initialized Valkyrie world/own/HP/item provenance and reloads."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-initializer';WIDE=BASE/'valkyrie-initializer-retained';ISO=BASE/'valkyrie-initializer-isolate';SHARED=BASE/'valkyrie-initializer-shared'
PARENT='6ac7a6aa35551106755bd56ae4397cca004fc18b'
def roll(seed):
 seed=(seed&0xffffffff)*0x6ac690c5+(seed>>32);return seed,seed&0xffffffff
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-2.json');parent=e.load(ROOT/'tools/reference/npc-world-allocation-native-evidence.json');extra=Counter({'valkyrie-initializer-check':1,'valkyrie-initializer-reload-check':2,'valkyrie-initializer-seal':3})
 for previous,stage,count in [(BASE/'npc-world-allocation',STAGE,108),(BASE/'npc-world-allocation-retained',WIDE,416)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
 e.cohort(ISO,expected({'extra_families':['valkyrie-initializer'],'extra_pre_save_commands':['valkyrie-initializer-seal']}),15)
 sys.path.insert(0,str(ROOT/'tests'));from check_valkyrie_initializer import configuration
 e.cohort(SHARED,expected(configuration(shared=True)),255)
 phrase='Valkyrie initializer:840source contexts/336zero-choice native comparisons;12actual rank/difficulty world creations,allocated own HP roll then unfiltered equipment,derived zero resource bases/regen,exact world/own/item streams,atomic preflight/witness guards;original initialized bodies/items/resources/wounds through two reloads;controller/contact/cast and outer current callbacks remain separate'
 itemcounts={}
 for stage in [STAGE,WIDE,ISO,SHARED]:
  assert 'pass across3boots/2reloads'in(stage/'native-run-2.log').read_text()
  if stage in [ISO,SHARED]:
   snapshot(STAGE/'source-snapshot-2.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts']and other['copiedData']==born['copiedData']
   for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage==SHARED:continue
  assert(stage/'birth-native-checks.log').read_text().count(phrase)==3
  s=e.load(stage/'valkyrie-initializer-fixtures.json');assert s['Generation']==2 and s['WorldAtSave']==s['World'] and len(s['Bodies'])==12 and len({h['Owner']for h in s['Bodies']})==12
  if stage!=ISO:
   world=e.load(stage/'npc-world-allocation-fixtures.json');assert world['WorldAtSave']==s['WorldAtSave']
  ranks=Counter();diffs=Counter();items=0
  for h in s['Bodies']:
   b=json.loads(h['State']);i=b['Initialization'];eq=b['Equipment'];r=json.loads(h['Resources']);before=i['WorldBefore'];after,low=roll(before);unit=(666<<32)|low;own,life=roll(unit)
   assert i['Profile']=='source-valkyrie-initialization-v1'and i['WorldAfter']==after and i['UnitSeed']==unit and i['AfterInitialization']==own
   assert i['SpawnLife256']==b['Birth']['SpawnLife256']==(400+life%81)*256 and i['RegenerationBase']==i['SpawnLife256']*12>>12
   assert eq['PetBefore']==own and eq['GameBefore']==after and eq['PetAfter']==h['Random']['Seed']and eq['Owner']==h['Owner']==b['Owner']==r['Owner']
   assert h['Body']==r['Actor']==h['Random']['Actor']and h['Life']==h['Maximum']-257 and r['Valkyrie']['RegenerationBase']==i['RegenerationBase']
   assert all(r['Valkyrie'][k]==0 for k in ['Mana256','ManaMaximum256','Stamina256','StaminaMaximum256'])and r['ItemEvents']is not None
   assert b['EquipmentStatProfile']=='valkyrie-equipment-numeric-v1' and b['EquipmentDefenseProfile']=='valkyrie-inherited-defense-v1'
   assert not b.get('GroundMotion')and not b.get('EquipmentContactProfile')
   ranks[b['Birth']['Rank']]+=1;diffs[i['Difficulty']]+=1
   assert len(h['Items'])==sum(a['Success']for a in eq['Attempts']);items+=len(h['Items'])
   for raw in h['Items']:
    item=json.loads(raw);assert item['Equipment']['Actor']==h['Body']and item['Equipment']['Owner']==h['Owner']and item['Allocation']is not None
  assert ranks==Counter({1:3,5:3,10:3,20:3})and diffs==Counter({0:4,1:4,2:4});itemcounts[stage.name]=items
 for n in ['content-build-1.log','integration-build-1.log','integration-build-2.log']:assert all(x in(STAGE/n).read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 assert 'Ran 71 tests'in(STAGE/'python-tests-I2.log').read_text()and'\nOK\n'in(STAGE/'python-tests-I2.log').read_text()
 commands=e.load(STAGE/'verification-commands-I2.json');assert len(commands)==7 and all(c['exitCode']==0 for c in commands)
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check-I2.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for group in ['patchHashes','sharedInputs']:
  for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
 sourcepath=ROOT/'tools/reference/valkyrie-initializer-source-evidence.json';datapath=ROOT/'tools/reference/valkyrie-initializer-data-evidence.json';source=e.load(sourcepath);data=e.load(datapath)
 assert source['contexts']==840 and len(source['completeRoutineHashes'])==10 and source['currentDataEvidenceSha256']==e.sha(datapath)
 for doc in [source,data]:
  for group in ['inputs','artifacts']:
   for p,h in doc.get(group,{}).items():assert e.sha(ROOT/p)==h,p
 assert data['compiledTextMatches']==dict(primaryRows=751,propertyIndexOffset=78,alignmentOffset=135,secondaryRows=623,componentCountsOffset=21,totalPiecesOffset=236)
 assert e.load(ROOT/'data/Sanctuary/Balance/valkyrie-initializer.json')==[data['valkyrie']]
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]
 files += e.stage_files(BASE/'valkyrie-initializer-source')+[BASE/'valkyrie-initializer-source/failed-data-1.py']
 files += [Path(__file__).resolve(),ROOT/'tests/check_valkyrie_initializer.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/Sanctuary.Integration/NpcWorldAllocationChecks.cs',ROOT/'tools/record_npc_item_spell_flights.py',ROOT/'tools/probe_valkyrie_initializer.py',ROOT/'tools/corroborate_valkyrie_initializer.py',sourcepath,datapath,STAGE/'content/Sanctuary.Content.dll',STAGE/'integration/Sanctuary.Integration.dll']
 for stage in [STAGE,WIDE]:files+=list((stage/'failed-native-1').iterdir())
 files+=list((ISO/'accepted-run-1').iterdir())+list((STAGE/'checks-before-I2').iterdir())
 files += [ROOT/'docs'/n for n in ['valkyrie-initializer-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,currentExecutableParity=False,sourceInitializedBody=True,sourceSnapshot=2,contentBuild=1,integrationBuild=2,focusedRun=2,retainedRun=2,isolatedRun=2,sharedRun=2,auditRun=2,focusedMarkers=108,retainedMarkers=416,isolatedMarkers=15,sharedMarkers=255,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,preSaveSealsPerCohort=3,sourceContexts=840,nativeSourceComparisons=336,initializedBodies=12,rankContexts=[1,5,10,20],difficultyContexts=[0,1,2],originalItemsByCohort=itemcounts,pythonTests=71,verificationCommands=7,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=['Current owned data is corroborated across751primary/623secondary compiled/text rows. Friendly/noRatio357haszero component choices,no initial skills/properties/onInit equipment and400-480HP.840complete-source stat contexts independently establish336zero-choice HP/seed/regen/rate baselines and non-current component draw controls.', 'Native body allocation precedes shared world unit draw. Its own seeded HP draw continues through unfiltered MonEquip selection;original items use the separate shared world stream. The resulting own seed becomes the bound actor/weapon stream. Derived zero mana/stamina/raw-HP regen enter existing resource/event/defense recipients.', 'Optional initialization witness validates world/unit/HP/equipment handoff,initial difficulty and birth life without changing legacy null JSON/native layout. Twelve actual rank/difficulty creations retain original items,ledgers,RNG,maxima and exact257FP wounds through two reloads;bad UTC and malformed witness refuse atomically.'],limits=['This is the source stats-initializer and prepared creation boundary,not complete current outer game/AI/quest/animation initialization. Sanctuary supplies no D2 region component cache for this two-piece summon;the verified zero-variant path is explicit. Initial100/75source rate stats do not certify or silently supply post-transform running callbacks.', 'Learned payment/placement/replacement,town/safe-area route,live NecroPet/A1/contact,nonzero inherited sunder and client acceptance remain separate;202handlers/no class complete. Original generated appearances/equipment/effects/icons remain placeholders. No deployment.', 'Metadata data-run1used a case-sensitive onInit header;archived script/log retain that failure and the corrected lookup changes no values. The first obsolete reader path failed before extracting files. The full source body compiles unchanged with a declared reservedui8literal shim warning;ASan/UBSan runtime stderr is empty. Both initial native broad runs failed an older per-family world-state assertion after126legitimatecreation/itemdraws. Archived manifests independently reproduce all126draws;old actual loaded state was not logged. I2keeps an immutable loaded-world witness,checked family endpoints and a final pre-save seal for both manifests. This preserves one shared world stream rather than isolating fixtures;the original connected Revive save remains. Source/failed inputs/logs/I1DLLs and accepted initial isolation remain archived. C1/I1/I2and final native/71Python checks pass.'])
 e.write('valkyrie-initializer-native-evidence.json',result,files,check)
 print('Valkyrie initialization:12actual rank/difficulty creations,source840/336native comparisons;108focused/416retained/15isolated/255shared;71Python/sevenchecks;202handlers,AI/contact/cast next.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
