#!/usr/bin/env python3
"""Accept nine current search helpers,owned tables and full search composition."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_build_snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-search-services'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_build_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('62952c0','tools/reference/generic-pet-search-bound-native-evidence.json');assert prior['verified']['passMarkers']==269
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==272
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/generic-pet-search-bound/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==269 and not before-after and after-before==Counter({'generic-pet-search-services-check':1,'generic-pet-search-services-reload-check':2})
 assert log.count('GenericPet traversal:3348current-code contexts,1776explicit source boundaries;registry,stale path/proxy caches,all dispatch paths,owner closure,64bit exits and live link order pass;real world graph/helper/lifecycle bindings remain gated')==3
 assert log.count('GenericPet search wrapper:2026current-code contexts,432tile-owner boundaries,2637exact heap permutations;real callback/prep/cleanup/grid,owner refresh,traversal,payload and borrowed slots pass;world helpers/actor remain gated')==3
 assert log.count('GenericPet bound search:1484current-code contexts,66explicit source boundaries;full wrapper/callback/prep/grid with actual refresh/registry/traversal,shared mutable path caches and live links pass;real world helpers/graph/actor remain gated')==3
 assert log.count('GenericPet search services:9392current helper contexts,848explicit source boundaries and216full search compositions;actual room,owned tables,state bitmasks,priority,corpse,ancestry,collision cache and special scans pass;world/nested predicates/actor remain gated')==3
 sourcePath=ROOT/'tools/reference/generic-pet-search-services-source-evidence.json';source=json.loads(sourcePath.read_text());artifacts.append(sourcePath)
 assert source['verified']==dict(helperContexts=9392,helperUnsupported=848,pipelineContexts=216,pipelineUnsupported=0,completeFunctions=9,completeBytes=2172)
 assert not source['processMemoryRead'] and source['sourceCodeExecutedInOfflineEmulator'] and not source['handlerEnabled'] and not source['fullAiVerified']
 for n,h in source['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 lib=ROOT/source['emulatorLibrary']['path'];assert sha(lib)==source['emulatorLibrary']['sha256'];artifacts.append(lib)
 for name in ['generic-pet-search-tables-data-evidence.json','generic-pet-search-nested-reference-evidence.json']:
  p=ROOT/'tools/reference'/name;r=json.loads(p.read_text());artifacts.append(p)
  for n,h in r['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
  if name.endswith('data-evidence.json'):
   assert r['verified']==dict(tables={'monstats':{'rows':751,'stride':508},'levels':{'rows':138,'stride':404}},compiledToTextFieldMatches=6897)
   assert not r['processMemoryRead'] and not r['worldSelectorBindingVerified']
  else:
   assert r['verified']==dict(completeFunctions=10,completeFunctionBytes=1976,instructionAndPaddingBytes=1952,inlineSwitchDataBytes=24,totalReadBytes=6192,maximumReadBytesPerCall=3108)
   assert not r['nativeMechanicsAccepted'] and not any(r[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','62952c0:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[752,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['class-skill-status.json','skill-effects.json','skill-icons.json','find-potion-level-bands.json','generic-pet-skills.json','generic-pet-states.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','62952c0:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 artifacts += list((STAGE/'diagnostics').iterdir())
 artifacts += [mainPath]+[STAGE/n for n in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','python-tests.log']]
 artifacts += [ROOT/n for n in ['tools/record_generic_pet_search_services.py','tests/check_generic_pet_search_services.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]
 result=dict(referenceVersion='3.3.93847',component='Nine current search helpers,owned tables and complete search composition',installed=False,handlerEnabled=False,fullAiVerified=False,realWorldRegistryRoomGraphAccepted=False,fullSearchBoundCompositionAccepted=True,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=272,retainedMarkers=269,worldBoots=3,actualWorldReloads=2,helperContextsPerBoot=9392,explicitHelperBoundaries=848,fullSearchContextsPerBoot=216,fullSearchUnsupported=0,completeHelperFunctions=9,completeHelperBytes=2172,ownedMonsterRows=751,ownedLevelRows=138,compiledTextFieldMatches=6897,newCachedReferenceFunctions=10,newCachedReferenceBytes=1976,boundedLiveReferenceReadBytes=6192,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=[752,21],unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='62952c0',receipt='tools/reference/generic-pet-search-bound-native-evidence.json'),
  established=['Nine complete current bodies (2172bytes) execute unchanged in the source oracle:validity,room,collision,level,special,corpse,priority,monster lookup and state membership. Native results,metadata fields,raw state masks,mutable size caches and exact nested service traces match9392contexts per boot,including848explicit assertion/null boundaries.',
   'All216full search compositions complete without unsupported boundaries. The actual helper bodies execute together with retained wrapper,callback,prep/cleanup,grid,refresh,registry and traversal. Native payloads,borrowed candidate targets,path caches,size caches and ordered service traces match. Direct Run and existing combat Provider alternate without copying candidate slots.',
   'Owned compiled monstats751x508 and levels138x404 match6897selected text fields:all eight monster flags at byte60,threat137 and MonSpcWalk63. Immutable catalogs expose explicit selector views with source bounds,including exclusion of level0. Native getter controls compare every owned row under each of three explicit fixture views;real selector aliases,difficulty and area mapping remain unverified.',
   'Validity preserves one origin owner lookup,recursive monster target ancestry,self rejection,missile owner handling,raw player hostility bit8 and the nested allegiance inversion. Priority retains repeated monster lookups and flag order. State membership returns the raw bitmask for supported kinds0/1/3,with signed statlist flags and state511exclusion. Corpse eligibility retains its secondary metadata and state-mask gates.',
   'Collision caches type/class and both size values in source order,then passes the stored first dimensions to the ray helper. Room lookup retains distinct static/dynamic path handling. Special scan retains the bounded25-entry offset table,wrapped coordinates and three ordered point checks. Negative special indices and arbitrary cyclic invalid ownership/registry graphs remain outside acceptance.',
   'All269predecessor checks survive across3boots/2actual reloads. Both clean builds have zero warnings/errors;pre-build/native inputs match.71Python tests,573unchanged named decisions and106unchanged installed artifacts pass. No new learned handler,actor persistence,full-class suite or installation is claimed.',
   'Four bounded read-only calls preserve10additional nested references/1976function bytes with6192total reads including anchors. A24-byte inline switch table explains one raw disassembly marker in SizeX;the original capture remains untouched and a separate152-byte instruction disassembly has no invalid markers. These are next-step references,not accepted nested behavior. No input,attach,security change or memory write occurs.'],
  remaining=['Compare and bind newly cached nested allegiance/alignment,player relation,secondary monster lookup,corpse mask,ray/point collision,size and special-index bodies. Actual Britannia room/registry/ownership membership and nested geometry/state producers remain unverified.',
   'General ordering4b7100,heap-backed caller storage,real selector binding,extended passives,NPC skills and complete actor/facet/logout/death/respec/save lifecycle still gate learned demons. No class is complete;effects/audio and talent/hotkey icons remain separate placeholders.'])

 out=ROOT/'tools/reference/generic-pet-search-services-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Search helpers:272native/71Python;9392helper/216full search cases per boot;nested/world/actor remain unfinished.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
