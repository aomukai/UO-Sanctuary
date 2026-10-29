#!/usr/bin/env python3
"""Accept complete current refresh,registry and traversal policies."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_build_snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-traversal'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_build_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('f58c2b5','tools/reference/generic-pet-search-wrapper-native-evidence.json');assert prior['verified']['passMarkers']==263
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==266
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/generic-pet-search-wrapper/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==263 and not before-after and after-before==Counter({'generic-pet-traversal-check':1,'generic-pet-traversal-reload-check':2})
 assert log.count('GenericPet traversal:3348current-code contexts,1776explicit source boundaries;registry,stale path/proxy caches,all dispatch paths,owner closure,64bit exits and live link order pass;real world graph/helper/lifecycle bindings remain gated')==3
 assert log.count('GenericPet search wrapper:2026current-code contexts,432tile-owner boundaries,2637exact heap permutations;real callback/prep/cleanup/grid,owner refresh,traversal,payload and borrowed slots pass;world helpers/actor remain gated')==3
 sourcePath=ROOT/'tools/reference/generic-pet-traversal-source-evidence.json';source=json.loads(sourcePath.read_text());artifacts.append(sourcePath)
 assert source['verified']==dict(contexts=3348,unsupportedBoundaries=1776,capturedFunctionBytes=2309,completeFunctions=7,searchDescriptorIndex=1,searchDescriptorKind=0,searchDescriptorOverride=False)
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
 old=json.loads(subprocess.check_output(['git','show','f58c2b5:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[749,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['class-skill-status.json','skill-effects.json','skill-icons.json','find-potion-level-bands.json','generic-pet-skills.json','generic-pet-states.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','f58c2b5:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 artifacts += [mainPath]+[STAGE/n for n in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','python-tests.log']]
 artifacts += [ROOT/n for n in ['tools/record_generic_pet_traversal.py','tests/check_generic_pet_traversal.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]
 result=dict(referenceVersion='3.3.93847',component='Current path refresh,registry and all traversal dispatch policies',installed=False,handlerEnabled=False,fullAiVerified=False,realWorldRegistryRoomGraphAccepted=False,fullSearchBoundCompositionAccepted=False,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=266,retainedMarkers=263,worldBoots=3,actualWorldReloads=2,contextsPerBoot=3348,explicitSourceBoundaries=1776,completeSourceFunctions=7,completeFunctionBytes=2309,pinnedSearchDescriptorIndex=1,pinnedSearchDescriptorKind=0,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=[749,21],unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='f58c2b5',receipt='tools/reference/generic-pet-search-wrapper-native-evidence.json'),
  established=['Seven complete current functions (230refresh,104registry,715dispatch,395/442alternate room scans,366owned enumeration,57closure) execute offline;native values and exact service traces match3348contexts per boot with1776explicit source assertion/null boundaries. The actual wrapper index1is pinned to kind0/no override from immutable16-byte descriptors.',
   'Refresh clears stale pointer identity and items in modes1/2 while preserving target type/key. Registry searches type-specific bucket chains and the special type5head. Default traversal preserves adjacent-room/list order,duplicates,live next160reads after callbacks and exact64-bit early returns. Null room/proxy entries remain unsupported source boundaries.',
   'Alternate type1scan validates room proxy entries before processing,retains direct cached references,clears failed resolution but does not replace a mismatched nonzero cache. Type2requires a living player/monster,skips the five source town IDs before proxy validation,and requires nonempty proxy arrays before linked-unit traversal. These literal conditions are retained without guessing Britannia room mapping.',
   'Type3resolves the owner twice through the captured dispatcher/enumerator,then follows saved owner GUID nodes. Next owner links are cached before callbacks. The captured57-byte closure stores the first nonzero64-bit result and suppresses later underlying callbacks while registry enumeration continues. Type-erasure invocation/allocation/disposal are declared ABI services;managed closure ownership is not certified native allocator equivalence.',
   'A wrapper-service binder now supplies these managed refresh/traversal policies while requiring explicit graph/registry providers. Full search-wrapper composition with this binder remains a separate required check. All263predecessor markers and saved fixtures pass;both clean builds zero warnings/errors,71Python tests,573unchanged named decisions and106unchanged installed artifacts pass. No additional learned handler/full-class suite/installation is claimed.',
   'One bounded read-only call preserves five additional referenced bodies/1364code bytes with3288total reads including anchors. Direct call,tail jump and LEA references are validated against the complete cached parent. No input,attach,security change or game-memory write occurs.'],
  remaining=['Compose the concrete refresh/registry/traversal services through the full search wrapper/callback using source-backed room graphs and live reference mutation controls;current retained wrapper checks still use its earlier declared traversal/refresh services. Bind actual world registry/room membership and callback validity/collision/state/priority helpers separately.',
   'Real descriptor semantics beyond pinned search index1,arbitrary cyclic/mutating invalid graphs,heap-backed caller state,ordering,extended passives,NPC skills and full actor lifecycle remain unfinished. Learned demons remain gated;all class tests/effects/audio/talent/hotkey icons remain separately pending.'])
 out=ROOT/'tools/reference/generic-pet-traversal-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Current traversal:266native/71Python;3348source controls per boot;real graph/full search bound composition remain unfinished.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
