#!/usr/bin/env python3
"""Accept the full current callback, exact service traces and inline cleanup path."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_build_snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-search-callback'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_build_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('d36234e','tools/reference/generic-pet-metadata-native-evidence.json');assert prior['verified']['passMarkers']==257
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==260
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/generic-pet-metadata/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==257 and not before-after and after-before==Counter({'generic-pet-search-check':1,'generic-pet-search-reload-check':2})
 assert log.count('GenericPet search:5824captured-current callbacks,540explicit null-AI boundaries,36owned prep-cleanup compositions;targets,counters,grid,float32,swarm,state lists and exact service traces pass;world helper bindings/caller/actor remain gated')==3
 assert log.count('GenericPet metadata:429skills/232states;1287buff+1287search current-code compositions,explicit selector/missing-row guards and Map362identity pass;world selector/search/actor remain gated')==3
 sourcePath=ROOT/'tools/reference/generic-pet-search-callback-source-evidence.json';source=json.loads(sourcePath.read_text());artifacts.append(sourcePath)
 assert source['verified']==dict(contexts=5824,unsupportedAiNullBoundaries=540,callbackBytes=3027,distanceBytes=138,completeCallbackFragments=5,inlinePreparationCleanupCompositions=36,maxOwnedStateGroupSize=7)
 assert not source['processMemoryRead'] and not source['targetProcessCodeExecuted'] and source['sourceCodeExecutedInOfflineEmulator'] and not source['handlerEnabled'] and not source['fullAiVerified']
 for n,h in source['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 lib=ROOT/source['emulatorLibrary']['path'];assert sha(lib)==source['emulatorLibrary']['sha256'];artifacts.append(lib)
 states=json.loads((ROOT/'data/Sanctuary/Balance/generic-pet-states.json').read_text());sizes=Counter(r['group'] for r in states if r['group']);assert len(states)==232 and max(sizes.values())==7
 for name in ['generic-pet-metadata-data-evidence.json','generic-pet-metadata-source-evidence.json']:
  p=ROOT/'tools/reference'/name;proof=json.loads(p.read_text());artifacts.append(p)
  for n,h in proof['artifacts'].items():assert sha(ROOT/n)==h,n
 code=(ROOT/'src/Sanctuary.Content/GenericPetSearch.cs').read_text();assert all(s in code for s in ['internal static int Visit','group.States.Count>10','prepared.Groups.Count>10','GenericPetSearch','Truncate','Squared'])
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','d36234e:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[747,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['class-skill-status.json','skill-effects.json','skill-icons.json','find-potion-level-bands.json','generic-pet-skills.json','generic-pet-states.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','d36234e:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 artifacts += [mainPath]+[STAGE/n for n in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','python-tests.log']]
 artifacts += [ROOT/n for n in ['tools/record_generic_pet_search.py','tests/check_generic_pet_search.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]
 result=dict(referenceVersion='3.3.93847',component='Complete current GenericPet target-search callback with explicit world services',installed=False,handlerEnabled=False,fullAiVerified=False,realWorldHelpersAndTraversalAccepted=False,heapBackedCallerConsumptionAccepted=False,newActorPersistenceAccepted=False,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=260,retainedMarkers=257,worldBoots=3,actualWorldReloads=2,callbackContextsPerBoot=5824,unsupportedAiNullBoundaries=540,currentCallbackBytes=3027,currentDistanceBytes=138,callbackUnwindFragments=5,inlinePreparationCleanupCompositionsPerBoot=36,maxOwnedStateGroupCardinality=7,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=[747,21],unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='d36234e',receipt='tools/reference/generic-pet-metadata-native-evidence.json'),
  established=['The complete3027-byte/five-fragment current callback and138-byte squared-distance leaf execute offline. Native results match5824contexts per boot,including540explicit null-monster-AI source boundaries. Exact targets,counters,grid cells,descriptor fields and service traces match;unexpected source memory faults fail the oracle. No live process access is needed in this increment.',
   'The callback preserves nullable/self/type/validity guards,signed distance cutoffs,death/mode/flag paths,room shortcuts,ordered collision/level/special calls,corpse-nearest selection,owner-nearest counters,priority category/monster bytes,bias/sweet-spot/signed swarm replacement and strict tie handling. Every callback returns zero;mutations form the result. Underlying world predicates are explicitly supplied,not replaced with guessed actor allegiance/geometry.',
   'The8x8grid uses raw dynamic Q16float32positions versus zero-extended static coordinates. Signed float truncation,CVTTSS2SIout-of-range sentinel,integer division by8/clamping and y+x*8indexing are retained. Count increments wrap and bias weights twice;closest target uses live squared distance rather than stale stored distance. The actual138-byte leaf supplies every reference distance with signed32wrapping.',
   'Prepared state groups keep live candidate slots,ordered membership tests and strict-nearer replacement. Thirty-six compositions execute actual846-byte preparation,253-byte append,152-byte cleanup and3027-byte callback using owned metadata. Embedded headers/groups remain byte-identical through cleanup. The pinned232states have maximum group cardinality7,within10inline IDs. Managed caller binding rejects more than10groups/IDs instead of silently preserving heap-backed source buffers;those consumption paths remain unverified.',
   'All257predecessor markers and retained metadata/buff/search/Find Potion/save fixtures pass. Both clean builds zero warnings/errors;71Python tests,573unchanged named decisions and106unchanged installed artifacts pass. Source/data inventories match native inputs. This detached callback introduces no actor persistence;no full-class suite or installation is claimed.'],
  remaining=['World validity/room/level/collision/special/priority/monster/state helpers remain declared services;their captured bodies are available for separate comparison/binding. Immutable snapshots and supplied traversal do not certify arbitrary service mutation/reentrancy. Source null-AI and heap-backed caller paths remain explicit unsupported boundaries.',
   'Port/compose the current search-wrapper traversal,grid initialization/sort and selection payload with this callback,then bind real world metadata/ownership/geometry and downstream ordering/actions. Ordering4b7100remains unreadable. Full actor/facet/logout/death/respec/save and extended passive/NPC skill dependencies still gate learned demons. All class acceptance and final effects/audio/talent/hotkey icons remain separately pending.'])
 out=ROOT/'tools/reference/generic-pet-search-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Current search callback:260native/71Python;5824source callbacks and36prep-cleanup compositions per boot;world helpers/caller/actor remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
