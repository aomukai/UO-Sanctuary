#!/usr/bin/env python3
"""Bind original Valkyrie external endpoints,full source packets and live contact."""
import argparse,json,sys
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'valkyrie-buffs';WIDE=BASE/'valkyrie-buffs-retained';ISO=BASE/'valkyrie-buffs-isolate';SHARED=BASE/'valkyrie-buffs-shared';PARENT='adf572650ce008668ca174083677c4dfd9de0f9e'
def record(check=False):
 born=e.inventory(STAGE,WIDE,STAGE/'source-snapshot-1.json');sys.path.insert(0,str(ROOT/'tests'));from check_valkyrie_buffs import configuration
 parent=e.load(ROOT/'tools/reference/valkyrie-town-native-evidence.json');extra=Counter({'valkyrie-buffs-check':1,'valkyrie-buffs-reload-check':2})
 for previous,stage,count in [(BASE/'valkyrie-town',STAGE,120),(BASE/'valkyrie-town-retained',WIDE,428)]:
  p=previous/'birth-native-checks.log';assert e.sha(p)==parent['files'][str(p.relative_to(ROOT))];e.cohort(stage,markers(p)+extra,count)
 e.cohort(ISO,expected(configuration(isolate=True)),27);e.cohort(SHARED,expected(configuration(shared=True)),255)
 phrase='Valkyrie buffs:57unfiltered original loadouts/330items,1824complete-source full packets per boot with8external contexts and4shares;whole-point Enchant,combined own mastery,exact actor RNG and immutable graph;actual NPC Enchant/buffed cached HP contact,live leases and original wounds/items/resources/RNG through two reloads;monster-type/sunder/learned cast remain separate'
 items={}
 for stage in [STAGE,WIDE,ISO,SHARED]:
  assert 'pass across3boots/2reloads'in(stage/'native-run-1.log').read_text();snapshot(STAGE/'source-snapshot-1.json',stage/'birth-inputs.json');other=e.load(stage/'birth-inputs.json');assert other['artifacts']==born['artifacts']and other['copiedData']==born['copiedData']
  for p,h in other['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
  if stage==SHARED:continue
  assert(stage/'birth-native-checks.log').read_text().count(phrase)==3
  saved=e.load(stage/'valkyrie-buffs-fixtures.json');assert saved['Generation']==2 and len(saved['Matrix'])==57
  assert sum(len(h['Items'])for h in saved['Matrix'])==330
  for h in saved['Matrix']:
   b=json.loads(h['Text']);assert b['Birth']['Critical']==1 and h['Life']>=256 and h['Random']['Actor']==h['Body']
  pending=saved['Live'];h=pending['Body'];b=json.loads(h['Text']);r=json.loads(h['Resources']);effects=json.loads(h['Effects'])
  assert len(h['Items'])==7 and b['Initialization']is not None and b['EquipmentContactProfile']=='valkyrie-original-proc-contact-v1' and r['Actor']==h['Body']==h['Random']['Actor']
  assert effects['Enchant']['Rank']==1 and effects['Enchant']['Min256']==1001 and effects['Enchant']['Max256']==2001 and len(effects['Auras'])==3
  assert pending['Life']>pending['Contact']['Impact']['Packet']['Total256']>0 and pending['Contact']['Impact']['Packet']['Fire256']>0 and h['Life']>=256
  items[stage.name]=337
 for n in ['content-build-1.log','integration-build-1.log']:assert all(x in(STAGE/n).read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 commands=e.load(STAGE/'verification-commands.json');assert len(commands)==9 and all(c['exitCode']==0 for c in commands)
 assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and'\nOK\n'in(STAGE/'python-tests.log').read_text()
 source=ROOT/'tools/reference/valkyrie-buffs-source-evidence.json';s=e.load(source);assert s['packets']==5472 and s['originalLoadouts']==57 and len(s['completeRoutineHashes'])==12
 for g in ['inputs','artifacts']:
  for p,h in s[g].items():assert e.sha(ROOT/p)==h,p
 preflight=e.load(ROOT/'tools/reference/valkyrie-montype-preflight.json');assert preflight['currentCatalogProducers']==0 and not preflight['blocksCurrentValkyrieGeneratedEquipment'] and preflight['nonzeroGuardRetained']
 for p,h in preflight['inputs'].items():assert e.sha(ROOT/p)==h,p
 assert 'ASan/UBSan clean.'in(STAGE/'source-check-1.log').read_text()
 fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
 client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
 for g in ['patchHashes','sharedInputs']:
  for p,h in client[g].items():assert e.sha(ROOT/p)==h,p
 status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert sum(s['implementation']=='handler-present'for s in status['skills'])==202 and not status['completedClasses']
 files=[p for p in e.stage_files(STAGE,WIDE,ISO,SHARED)if not p.name.startswith('receipt-run-')]
 files += list((STAGE/'frozen-c1-i1').iterdir())+[Path(__file__).resolve(),ROOT/'tools/probe_valkyrie_buffs.py',ROOT/'tests/check_valkyrie_buffs.py',source,STAGE/'content/Sanctuary.Content.dll',STAGE/'integration/Sanctuary.Integration.dll']
 files += [ROOT/'tools/record_valkyrie_montype_preflight.py',ROOT/'tools/reference/valkyrie-montype-preflight.json',ROOT/'docs/valkyrie-montype-preflight.md']
 files += [ROOT/'docs'/n for n in ['valkyrie-buffs-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=202,amazonHandlers=28,completedClasses=[],learnedValkyrie=False,currentExecutableParity=False,sourceSnapshot=1,contentBuild=1,integrationBuild=1,focusedMarkers=120,retainedMarkers=428,isolatedMarkers=27,sharedMarkers=255,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,sourcePackets=5472,sourceRoutines=12,originalMatrixBodies=57,originalMatrixItems=330,originalItemsByCohort=items,nativePacketComparisonsPerBoot=1824,externalContexts=8,damageShares=[0,64,128,255],pythonTests=71,verificationCommands=9,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=41,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=['Existing WithBonuses supplies external endpoints;reviewed current enma/exma49/50and older getter/do25 establish whole-point original stat-list projection. Original graph remains immutable;complete fill handles own mastery and one fire/lightning/cold/magic roll sequence.','All57original loadouts compare full packets/armor/exact actor continuation for no,individual,combined,subpoint and expired effects at four shares through two reloads. No owner gear/mastery or new buff RNG.','Actual existing NPC Enchant writer reaches original contact. Buff removal/caster removal and target resistance changes after allocation do not rewrite cached HP. Live leases,original items/resources/wounds/RNG and genuine pending contact persist across two actual reloads.'],limits=['The twelve unchanged older fill/innate/RNG bodies execute;external stat publishing is an explicit service. Current executable/full buff publisher chronology is not claimed. The stale original symbol-table path failed before compilation;the surviving owned copy has the identical accepted alias fingerprint.','Prepared existing aura leases do not invent ally inheritance of Paladin self-only elemental auras. The legacy single-item NPC adapter retains its prepared FP256 contract pending a separate producer/unit-rounding audit;uniform original-stat-list behavior for all NPCs is not claimed.','Nonzero sunder and learned input/payment/placement/replacement/client acceptance remain. Monster-type stat180 is guarded;the pinned current catalogue has no producers,so no invented native taxonomy blocks learned work. No class completion/deployment;effects/audio/body/equipment and talent/hotkey icons remain separate placeholders.'])
 e.write('valkyrie-buffs-native-evidence.json',result,files,check);print('Valkyrie buffs:5472source packets/12complete bodies;120focused/428retained/27isolated/255shared;71Python/nine checks;202handlers/no deployment.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
