#!/usr/bin/env python3
"""Accept full current search composed with refresh,registry and traversal."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_build_snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-search-bound'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_build_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('50259df','tools/reference/generic-pet-traversal-native-evidence.json');assert prior['verified']['passMarkers']==266
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==269
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/generic-pet-traversal/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==266 and not before-after and after-before==Counter({'generic-pet-search-bound-check':1,'generic-pet-search-bound-reload-check':2})
 assert log.count('GenericPet traversal:3348current-code contexts,1776explicit source boundaries;registry,stale path/proxy caches,all dispatch paths,owner closure,64bit exits and live link order pass;real world graph/helper/lifecycle bindings remain gated')==3
 assert log.count('GenericPet search wrapper:2026current-code contexts,432tile-owner boundaries,2637exact heap permutations;real callback/prep/cleanup/grid,owner refresh,traversal,payload and borrowed slots pass;world helpers/actor remain gated')==3
 assert log.count('GenericPet bound search:1484current-code contexts,66explicit source boundaries;full wrapper/callback/prep/grid with actual refresh/registry/traversal,shared mutable path caches and live links pass;real world helpers/graph/actor remain gated')==3
 sourcePath=ROOT/'tools/reference/generic-pet-search-bound-source-evidence.json';source=json.loads(sourcePath.read_text());artifacts.append(sourcePath)
 assert source['verified']==dict(contexts=1484,unsupportedBoundaries=66,addedCompleteSourceBytes=1049,addedFunctions=3,searchIndex=1)
 assert not source['processMemoryRead'] and source['sourceCodeExecutedInOfflineEmulator'] and not source['handlerEnabled'] and not source['fullAiVerified']
 for n,h in source['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 lib=ROOT/source['emulatorLibrary']['path'];assert sha(lib)==source['emulatorLibrary']['sha256'];artifacts.append(lib)
 capturePath=ROOT/'.local/class-skills/warlock-runtime-reference/traversal-services-20261030.json';capture=json.loads(capturePath.read_text());artifacts.append(capturePath)
 assert capture['bytesRead']==3288<=8192 and capture['sourceReferencesVerified'] and capture['executableSha256']=='e1f5436e3d9687f644ef16938b1b183d1fdef434f18cf66d852cf68f48cc8936'
 assert [(r['rva'],r['bytes']) for r in capture['records']]==[('0x370680',104),('0x333550',366),('0x40d660',395),('0x40d7f0',442),('0x40d9b0',57)]
 assert all(r['allUnwindFragmentsCaptured'] for r in capture['records']) and all(f['invalidInstructionMarkers']==0 for r in capture['records'] for f in r['fragments'])
 assert not any(capture[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
 for n,h in capture['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','50259df:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[749,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['class-skill-status.json','skill-effects.json','skill-icons.json','find-potion-level-bands.json','generic-pet-skills.json','generic-pet-states.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','50259df:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 artifacts += [mainPath]+[STAGE/n for n in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','python-tests.log']]
 artifacts += [ROOT/n for n in ['tools/record_generic_pet_search_bound.py','tests/check_generic_pet_search_bound.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]
 result=dict(referenceVersion='3.3.93847',component='Full current search composed with concrete refresh,registry and traversal',installed=False,handlerEnabled=False,fullAiVerified=False,realWorldRegistryRoomGraphAccepted=False,fullSearchBoundCompositionAccepted=True,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=269,retainedMarkers=266,worldBoots=3,actualWorldReloads=2,contextsPerBoot=1484,explicitSourceBoundaries=66,addedCompleteSourceFunctions=3,addedCompleteFunctionBytes=1049,pinnedSearchDescriptorIndex=1,pinnedSearchDescriptorKind=0,binderRejectsIncoherentSnapshots=True,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=[749,21],unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='50259df',receipt='tools/reference/generic-pet-traversal-native-evidence.json'),
  established=['The complete search wrapper,callback,preparation,cleanup,row initializer and grid heap now execute together with actual230-byte refresh,104-byte registry and715-byte room traversal in the offline current-build oracle. Native payloads,borrowed candidate mutations,path caches and exact ordered service traces match1484contexts per boot,including66explicit source boundaries.',
   'Index1uses its immutable default descriptor. Actual room adjacency and next160chains drive callback enumeration;no preselected visit list substitutes for traversal. Duplicate/reordered rooms,missing rooms,explicit targets,registry bucket/type5lookup,stale cache identities,item modes and callback-side next-link changes are preserved.',
   'The binder now reads the shared live traversal path after refresh rather than reusing an earlier wrapper path snapshot. Managed controls deliberately retain the stale original snapshot and separately refuse conflicting core unit metadata. Both direct Run and the existing combat Provider preserve borrowed mutable slots.',
   'All266predecessor markers survive across three boots and two actual reloads. Both clean builds have zero warnings/errors;pre-build/native source-data inventories match.71Python tests,573unchanged named decisions and106unchanged installed artifacts pass. No new learned handler,actor persistence,full-class suite or installation is claimed.',
   'No new process read or input occurs. The oracle reuses cached owned current-build code;hashes bind all fragments and the emulator library. World predicates,registry bucket choice and room metadata remain explicit supplied services.'],
  remaining=['Bind actual Britannia registry/room membership,ownership and callback validity/collision/state/priority helpers. The accepted fixture graph is not actual world acceptance. General ordering4b7100and heap-backed caller storage remain separately unfinished.',
   'Real selector/ownership/geometry,extended passives,NPC skills and complete actor/facet/logout/death/respec/save lifecycle still gate learned demons. No class is complete;effects/audio and talent/hotkey icons remain separate placeholders.'])

 out=ROOT/'tools/reference/generic-pet-search-bound-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Bound search:269native/71Python;1484source controls per boot;actual world graph/helpers/actor remain unfinished.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
