#!/usr/bin/env python3
"""Accept nested current search functions,secondary data and composed search."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_build_snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-search-nested'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_build_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('626778c','tools/reference/generic-pet-search-services-native-evidence.json');assert prior['verified']['passMarkers']==272
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==275
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/generic-pet-search-services/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==272 and not before-after and after-before==Counter({'generic-pet-search-nested-check':1,'generic-pet-search-nested-reload-check':2})
 assert log.count('GenericPet traversal:3348current-code contexts,1776explicit source boundaries;registry,stale path/proxy caches,all dispatch paths,owner closure,64bit exits and live link order pass;real world graph/helper/lifecycle bindings remain gated')==3
 assert log.count('GenericPet search wrapper:2026current-code contexts,432tile-owner boundaries,2637exact heap permutations;real callback/prep/cleanup/grid,owner refresh,traversal,payload and borrowed slots pass;world helpers/actor remain gated')==3
 assert log.count('GenericPet bound search:1484current-code contexts,66explicit source boundaries;full wrapper/callback/prep/grid with actual refresh/registry/traversal,shared mutable path caches and live links pass;real world helpers/graph/actor remain gated')==3
 assert log.count('GenericPet search services:9392current helper contexts,848explicit source boundaries and216full search compositions;actual room,owned tables,state bitmasks,priority,corpse,ancestry,collision cache and special scans pass;world/nested predicates/actor remain gated')==3
 assert log.count('GenericPet nested search:10423current helper/composition contexts,408explicit boundaries and216full search compositions;actual allegiance/stat nodes,relations,secondary metadata,mask loops,signed sizes,ray/point/index geometry pass;stat/mask/terminal world producers and actor remain gated')==3
 sourcePath=ROOT/'tools/reference/generic-pet-search-nested-source-evidence.json';source=json.loads(sourcePath.read_text());artifacts.append(sourcePath)
 assert source['verified']==dict(nestedContexts=10423,nestedUnsupported=408,pipelineContexts=216,pipelineUnsupported=0,completeFunctions=10,capturedFunctionBytes=1976,instructionAndPaddingBytes=1928,inlineSwitchBytes=48)
 assert not source['processMemoryRead'] and source['sourceCodeExecutedInOfflineEmulator'] and not source['handlerEnabled'] and not source['fullAiVerified']
 for n,h in source['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 lib=ROOT/source['emulatorLibrary']['path'];assert sha(lib)==source['emulatorLibrary']['sha256'];artifacts.append(lib)
 dataPath=ROOT/'tools/reference/generic-pet-secondary-data-evidence.json';data=json.loads(dataPath.read_text());artifacts.append(dataPath)
 assert data['verified']==dict(secondaryRows=623,secondaryStride=296,monsterLinks=751,compiledTextFieldMatches=10719,compiledRowIdChecks=623,reservedUpperFlagChecks=623,textIdentityDifferences=19)
 assert len(data['identityDifferences'])==19 and not data['processMemoryRead'] and not data['worldSelectorBindingVerified']
 for n,h in data['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 assert [(r['rva'],r['codeAndPaddingBytes'],r['inlineSwitchBytes'],r['rawInvalidMarkers'],r['codeInvalidMarkers']) for r in source['switchTables']]==[('0x277db0',152,24,1,0),('0x277e60',184,24,0,0)]
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','626778c:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[755,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['class-skill-status.json','skill-effects.json','skill-icons.json','find-potion-level-bands.json','generic-pet-skills.json','generic-pet-states.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','626778c:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 artifacts += [mainPath]+[STAGE/n for n in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','python-tests.log']]
 artifacts += [ROOT/n for n in ['tools/record_generic_pet_search_nested.py','tests/check_generic_pet_search_nested.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]
 result=dict(referenceVersion='3.3.93847',component='Nested current search functions,secondary metadata and complete search composition',installed=False,handlerEnabled=False,fullAiVerified=False,realWorldRegistryRoomGraphAccepted=False,fullSearchBoundCompositionAccepted=True,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=275,retainedMarkers=272,worldBoots=3,actualWorldReloads=2,nestedContextsPerBoot=10423,explicitNestedBoundaries=408,fullSearchContextsPerBoot=216,fullSearchUnsupported=0,completeNestedFunctions=10,capturedFunctionBytes=1976,instructionAndPaddingBytes=1928,inlineSwitchBytes=48,ownedSecondaryRows=623,ownedMonsterLinks=751,compiledTextFieldMatches=10719,textIdentityDifferences=19,newProcessReadBytes=0,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=[755,21],unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='626778c',receipt='tools/reference/generic-pet-search-services-native-evidence.json'),
  established=['Ten complete current functions execute unchanged:allegiance,alignment,player relation,secondary metadata,state-mask scan,both size getters,ray,point and special index. Native return values,mutable size caches and exact nested service traces match10423helper/composed-call contexts per boot,including408explicit assertion/missing-data boundaries. All216complete source search compositions finish without unsupported boundaries.',
   'Compiled secondary metadata has623rows/296-byte stride and751signed primary links.10719selected text fields match,plus623compiled row IDs and623reserved-bit checks. Nineteen legacy text hcIdx values differ from compiled ordinals;the actual getter uses the compiled links/ordinals. Every primary link and every secondary row compare through three explicit fixture views,including null/negative/out-of-range links and signed-size controls.',
   'Alignment follows primary then secondary stat-list nodes for state105,then requires stat172metadata before calling the supplied node decoder. Allegiance preserves null/self and numeric0/2rules. Player relations preserve source-kind/data guards,self suppression,null-target GUID-1,first-match identity and linked order. State masks preserve signed active-statlist gates,raw mask intersection,early exit and repeated wide-count reads with source overflow/narrowing.',
   'Both size getters preserve six-kind dispatch,signed monster bytes,object dimensions,unsigned missile size and their different null-unit paths. Ray adjustment preserves clamped radii,wrapped absolute/manhattan arithmetic,dominant-axis/diagonal endpoint changes and raw terminal results. Point/index helpers share the existing mutable cache updater;the metric retains source wrap/clamp/division behavior. Actual outer validity,corpse,collision and special callers are included,along with the complete wrapper/callback/metadata/grid/refresh/registry/traversal stack.',
   'Both152/184-byte executable size prefixes have zero invalid markers;each has a24-byte six-entry inline switch table whose targets are actual instruction boundaries. The original capture is unchanged. This supersedes the prior SizeX-only1952/24byte classification with1928instruction/padding bytes and48switch bytes across1976captured bytes.',
   'All272predecessor checks pass across3boots/2actual reloads. Both clean builds have zero warnings/errors;pre-build/native inputs match.71Python tests,573unchanged named decisions and106unchanged installed artifacts pass. No new learned handler,actor persistence,full-class suite,installation or process-memory read is claimed.'],
  remaining=['Stat172metadata/node stat decoding,missile size metadata,the actual raw mask410producer and terminal room trace2a6c40remain supplied services. The mask meaning and actual Britannia geometry/room/registry/ownership membership are not inferred from the legacy layout. Explicit fixture selector views do not certify real aliases or a difficulty mapping.',
   'General ordering4b7100,heap-backed caller storage,remaining producers,extended passives,NPC skills and complete actor/facet/logout/death/respec/save lifecycle still gate learned demons. Negative outer special indices and cyclic invalid lists remain outside acceptance. No class is complete;effects/audio and talent/hotkey icons remain separate placeholders.'])

 out=ROOT/'tools/reference/generic-pet-search-nested-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Nested search:275native/71Python;10423helper/composed/216full search cases per boot;remaining stat/mask/trace/world/actor unfinished.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
