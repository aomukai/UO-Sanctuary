#!/usr/bin/env python3
"""Accept complete current search composition and exact grid heap semantics."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_build_snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-search-wrapper'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_build_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('644b987','tools/reference/generic-pet-search-native-evidence.json');assert prior['verified']['passMarkers']==260
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==263
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/generic-pet-search-callback/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==260 and not before-after and after-before==Counter({'generic-pet-search-wrapper-check':1,'generic-pet-search-wrapper-reload-check':2})
 assert log.count('GenericPet search wrapper:2026current-code contexts,432tile-owner boundaries,2637exact heap permutations;real callback/prep/cleanup/grid,owner refresh,traversal,payload and borrowed slots pass;world helpers/actor remain gated')==3
 assert log.count('GenericPet search:5824captured-current callbacks,540explicit null-AI boundaries,36owned prep-cleanup compositions;targets,counters,grid,float32,swarm,state lists and exact service traces pass;world helper bindings/caller/actor remain gated')==3
 sourcePath=ROOT/'tools/reference/generic-pet-search-wrapper-source-evidence.json';source=json.loads(sourcePath.read_text());artifacts.append(sourcePath)
 assert source['verified']==dict(wrapperContexts=2026,unsupportedBoundaries=432,heapPermutations=2637,wrapperBytes=1187,rowInitializerBytes=69,heapBytes=300,boundedLeafBytes=16,leafInstructionBytes=13,leafPaddingBytes=3)
 assert not source['processMemoryRead'] and source['sourceCodeExecutedInOfflineEmulator'] and not source['handlerEnabled'] and not source['fullAiVerified']
 for n,h in source['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 lib=ROOT/source['emulatorLibrary']['path'];assert sha(lib)==source['emulatorLibrary']['sha256'];artifacts.append(lib)
 leafPath=ROOT/'.local/class-skills/warlock-runtime-reference/grid-cell-leaf-idle-20261030.json';leaf=json.loads(leafPath.read_text());artifacts.append(leafPath)
 assert leaf['referenceVersion']=='3.3.93847' and leaf['executableSha256']=='e1f5436e3d9687f644ef16938b1b183d1fdef434f18cf66d852cf68f48cc8936' and leaf['bytesRead']==592 and leaf['directCallTargetVerified']
 assert leaf['record']['boundedLeafReference'] and not leaf['record']['allUnwindFragmentsCaptured'] and leaf['record']['nextKnownUnwindStart']=='0xaeeb0'
 assert not any(leaf[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
 assert (ROOT/'.local/class-skills/warlock-runtime-reference/grid-cell-leaf-idle-20261030.bin').read_bytes()==bytes.fromhex('33c048890148894108488bc1c3cccccc')
 traversalPath=ROOT/'.local/class-skills/warlock-runtime-reference/callees-live-search-traversal-20261030.json';traversal=json.loads(traversalPath.read_text());artifacts.append(traversalPath)
 assert traversal['bytesRead']==2450<=8192 and traversal['directCallTargetsVerified'] and traversal['executableSha256']==leaf['executableSha256']
 assert [(r['rva'],r['bytes']) for r in traversal['records']]==[('0x371b10',230),('0x40d9f0',715)]
 assert all(r['allUnwindFragmentsCaptured'] for r in traversal['records']) and all(f['invalidInstructionMarkers']==0 for r in traversal['records'] for f in r['fragments'])
 assert not any(traversal[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
 for capture in [leaf,traversal]:
  for n,h in capture['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','644b987:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[748,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['class-skill-status.json','skill-effects.json','skill-icons.json','find-potion-level-bands.json','generic-pet-skills.json','generic-pet-states.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','644b987:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 artifacts += [mainPath]+[STAGE/n for n in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','python-tests.log','traversal-availability.log']]
 artifacts += [p for p in (STAGE/'diagnostics').rglob('*') if p.is_file()]
 artifacts += [ROOT/n for n in ['tools/record_generic_pet_search_wrapper.py','tests/check_generic_pet_search_wrapper.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]
 result=dict(referenceVersion='3.3.93847',component='Full current GenericPet search wrapper and grid heap composition',installed=False,handlerEnabled=False,fullAiVerified=False,realWorldRefreshTraversalHelpersAccepted=False,heapBackedCallerConsumptionAccepted=False,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=263,retainedMarkers=260,worldBoots=3,actualWorldReloads=2,wrapperContextsPerBoot=2026,explicitTileOwnerBoundaries=432,heapPermutationsPerBoot=2637,wrapperBytes=1187,rowInitializerBytes=69,heapBytes=300,boundedLeafBytes=16,leafInstructionBytes=13,leafPaddingBytes=3,realCallbackAndInlineLifecycleComposition=True,borrowedMutableProvider=True,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=[748,21],unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  newReferenceAvailability=dict(refreshBytes=230,traversalBytes=715,completeFunctions=2,mechanicallyAccepted=False,newProcessReadBytes=3042,maxReadBudgetPerCall=8192),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='644b987',receipt='tools/reference/generic-pet-search-native-evidence.json'),
  established=['The complete1187-byte search wrapper composes actual callback3027,distance138,preparation846,append253,cleanup152,row initializer69 and grid heap300. A separate bounded16-byte leaf has13instruction bytes/one return plus3INT3padding before the next known unwind start. Its16-byte zeroing/return behavior executes;no own unwind completeness is claimed. Native values/traces match2026contexts per boot,including432explicit tile-owner assertion boundaries.',
   'Owner refresh precedes path-target/status reads;the current source writes owner identity into callback bias after validating the path target. This literal behavior is retained,not reinterpreted. Explicit target performs one callback;otherwise declared traversal receives byte1 and invokes the actual callback in supplied order. Mask0 still collects counters;null configuration still runs wrapper setup/traversal while callback guards remain in force.',
   'All64grid cells are initialized. Positive signed counts select contiguous cell pointers. The300-byte helper heapifies by signed distance ascending,then count descending,then source cell address ascending;it is not a generic complete sort. All2637full heap permutations match,including n0..6exhaustive input permutations and8..64cell extreme signed values. Its uninitialized R8scratch byte is unread;controlled stack fills preserve that distinction.',
   'Returned target slots are callback20,28,30,38,then grid root target;callback priority40is intermediate and not returned directly. Word40packs counters58/5c andWord48packs60/64. The provider fits existing combat Search and preserves borrowed mutable descriptor slots,refusing an immutable-only view instead of copying target mutations away. Embedded capacities remain bounded;world helper behavior is not invented.',
   'All260predecessor markers and saved fixtures pass;both clean builds zero warnings/errors,71Python tests,573unchanged named decisions and106unchanged installed artifacts pass. Source/data snapshots match native inputs. No new actor persistence,full-class suite,handler or installation is claimed.',
   'A bounded read-only leaf capture reads592bytes including anchors. A second2450-byte call preserves complete refresh230/traversal715bodies with zero invalid markers. No input,attach,security change or game-memory write occurs. Those two helper bodies await mechanical comparison;the source oracle continues to declare their services explicitly.'],
  remaining=['Port/compare the now-cached refresh371b10 and traversal40d9f0 bodies and callback validity/room/level/collision/state/priority helpers. Real actor ownership/metadata/geometry,ordering and full lifecycle remain required;General ordering4b7100stillhas its prior unreadable reference gap.',
   'Heap-backed caller storage,arbitrary service mutation/reentrancy,extended passives and NPC skill dependencies remain unsupported/unverified. Learned demons remain gated. All classes retain outstanding mechanical/client tests;effects/audio and talent/hotkey icons remain separately recorded placeholders.'])
 out=ROOT/'tools/reference/generic-pet-search-wrapper-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Current search wrapper:263native/71Python;2026source compositions/2637exact heap permutations per boot;world helpers/actor remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
