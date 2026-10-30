#!/usr/bin/env python3
"""Accept native identity/ownership producers with retained source search checks."""
import argparse,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_snapshot
from probe_warlock_runtime import ROOT,digest
STAGE=ROOT/'.local/class-skills/generic-pet-world-identity'
def sha(p):return digest(p.read_bytes())
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json')
 _,prior=frozen_receipt('ccd661c','tools/reference/generic-pet-state-mask-native-evidence.json');assert prior['verified']['passMarkers']==281
 artifacts=[Path(__file__).resolve()]
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==284
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/generic-pet-state-mask/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==281 and not before-after and after-before==Counter({'generic-pet-world-identity-check':1,'generic-pet-world-identity-reload-check':2})
 assert log.count('GenericPet world identity:36live role/context lookups,actual player/control/summon ownership,shared search/traversal binding,stale/deleted/equipped target refresh and explicit unsupported registries pass;native geometry,corpse identities,stat lists,ordering and complete actors remain gated')==3
 assert log.count('GenericPet traversal:3348current-code contexts,1776explicit source boundaries;registry,stale path/proxy caches,all dispatch paths,owner closure,64bit exits and live link order pass;real world graph/helper/lifecycle bindings remain gated')==3
 assert log.count('GenericPet state masks:2784current mask/corpse contexts,216full search compositions and9selector boundaries;all232states in3explicit profiles match;real world bindings,ordering and actors remain gated')==3
 nativeApi=STAGE/'native-owner-api.json';api=json.loads(nativeApi.read_text());assert sha(Path(api['path']))==api['sha256'] and not api['compilerSourceIdentityClaimed'];artifacts.append(nativeApi)
 fixture=STAGE/'generic-pet-world-identity-fixtures.json';saved=json.loads(fixture.read_text());gear=json.loads(saved['Gear'])
 bases=ROOT/'data/Sanctuary/Balance/iron-golem-items.json';base=next(r for r in json.loads(bases.read_text()) if r['Code']==gear['BaseCode']);artifacts.append(bases)
 assert saved['Owner']!=saved['Pet'] and saved['Binding']==dict(Pet=saved['Pet'],Owner=saved['Owner'],Item=saved['Item'],BaseCode=gear['BaseCode'],BodyLocation=base['Body1'])
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','ccd661c:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573
 fingerprints=[len(a['InputHashes']) for a in audits];assert fingerprints==[761,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['class-skill-status.json','skill-effects.json','skill-icons.json','generic-pet-state-masks.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','ccd661c:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 diagnostic=STAGE/'diagnostics/attempt1';assert 'Conflicting native owner routes silently preferred one owner' in (diagnostic/'stats-native-checks.log').read_text()
 artifacts += [p for p in diagnostic.iterdir() if p.is_file()]
 for name in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','python-tests.log']:artifacts.append(STAGE/name)
 artifacts += [fixture,mainPath,ROOT/'tests/check_generic_pet_world_identity.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/build_property_integration_backlog.py',ROOT/'tools/reference/item-property-integration-backlog.json']
 result=dict(referenceVersion='3.3.93847',component='Native stable entity identity and live player ownership bound to existing source lookup/refresh',installed=False,handlerEnabled=False,fullAiVerified=False,realWorldRegistryIdentityAccepted=True,realWorldRoomGraphAccepted=False,realWorldStateListsAccepted=False,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],processMemoryRead=False,
  verified=dict(passMarkers=284,retainedMarkers=281,worldBoots=3,actualWorldReloads=2,liveRoleAndContextQueriesPerBoot=36,retainedTraversalSourceContextsPerBoot=3348,retainedTraversalSourceBoundariesPerBoot=1776,retainedMaskCorpseContextsPerBoot=2784,retainedCompleteMaskSearchContextsPerBoot=216,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=fingerprints,unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  nativeOwnerApi=api,inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='ccd661c',receipt='tools/reference/generic-pet-state-mask-native-evidence.json'),
  established=['The bound identity context uses the authoritative native World registry and stable serials for players,creatures and mapped equipment. Source kind-qualified lookup rejects role collisions and missing/deleted entities. Context IDs are explicit server-world bindings,not facets or difficulty. Source object/missile/tile registries,unmapped items and native corpses remain explicit unsupported boundaries. Native serial uniqueness permits a direct exact candidate;D2bucket-link ordering is not claimed.',
   'Actual BaseCreature.GetMaster supplies player ownership to both search and traversal,reading the owner anew rather than copying a stale snapshot. Existing path/cache fields and supplied owned-list heads remain intact. Unowned actors with nonempty unsupplied owned lists and NPC-owned actors are explicitly unsupported. Changing control owners,shared native Master aliases,unowned states and summon-only states are checked through real bodies.',
   'Existing current-source path refresh uses the real native registry to retain valid targets,clear mismatched/deleted identities and reject an equipped original item. Player,creature and private original equipment retain identity across Internal placement and both actual world reloads. The same original serial,full gear rolls and IronGolemItems binding survive;the holder is a persistent skeleton fixture,not an Iron Golem actor.',
   'The first fixture incorrectly assumed independent ControlMaster and SummonMaster fields. The current native API uses one Master with flag-gated getters. Its failed log,snapshot,sources and DLLs are preserved. The adapter now reuses GetMaster;native alias checks corroborate the inspected API without claiming compiler/source identity for the external UOContent DLL.',
   'All281predecessor checks are retained across3boots/2reloads;clean builds,matching source/data inventories,71Python tests,573unchanged named-item decisions and106unchanged installed artifacts pass. No new learned handler,complete actor persistence,full-class suite or installation is claimed. No new D2R memory access was needed.'],
  remaining=['Actual mode/flags/AI metadata,room/grid/target-enumeration and stat/state-list producers,source selectors,general ordering4b7100,heap-backed caller storage,extended passives,NPC skill dispatch and complete actor/facet/logout/death/respec/save lifecycle remain required. Native corpse Item identities must be projected explicitly as source monsters;they are not loot items. Unsupported world registries and item types are not silently returned as valid source units.',
   'The native identity/ownership projection is not a proof of D2 room membership,collision bits,allegiance,timing or event order. No class is complete;effects/audio and talent/hotkey icons remain separate placeholders.'])
 out=ROOT/'tools/reference/generic-pet-world-identity-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('World identity:284native/71Python;36actual role/context queries per boot;2reloads preserve player/pet/original item;rooms/stats/corpses/ordering/actors unfinished.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
