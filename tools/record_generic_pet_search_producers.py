#!/usr/bin/env python3
"""Accept current stat/missile producers and exact room trace with shared stepping."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_build_snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-search-producers'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_build_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('8de2433','tools/reference/generic-pet-search-nested-native-evidence.json');assert prior['verified']['passMarkers']==275
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==278
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/generic-pet-search-nested/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==275 and not before-after and after-before==Counter({'generic-pet-search-producers-check':1,'generic-pet-search-producers-reload-check':2})
 assert log.count('GenericPet traversal:3348current-code contexts,1776explicit source boundaries;registry,stale path/proxy caches,all dispatch paths,owner closure,64bit exits and live link order pass;real world graph/helper/lifecycle bindings remain gated')==3
 assert log.count('GenericPet search wrapper:2026current-code contexts,432tile-owner boundaries,2637exact heap permutations;real callback/prep/cleanup/grid,owner refresh,traversal,payload and borrowed slots pass;world helpers/actor remain gated')==3
 assert log.count('GenericPet bound search:1484current-code contexts,66explicit source boundaries;full wrapper/callback/prep/grid with actual refresh/registry/traversal,shared mutable path caches and live links pass;real world helpers/graph/actor remain gated')==3
 assert log.count('GenericPet search services:9392current helper contexts,848explicit source boundaries and216full search compositions;actual room,owned tables,state bitmasks,priority,corpse,ancestry,collision cache and special scans pass;world/nested predicates/actor remain gated')==3
 assert log.count('GenericPet nested search:10423current helper/composition contexts,408explicit boundaries and216full search compositions;actual allegiance/stat nodes,relations,secondary metadata,mask loops,signed sizes,ray/point/index geometry pass;stat/mask/terminal world producers and actor remain gated')==3
 assert log.count('GenericPet search producers:9058current producer contexts,28explicit boundaries and216full search compositions;current stat lookup/clamping,missile sizes and complete grid trace match;13200retained wall-follow paths share the stepper;actual mask/world/ordering/actor remain gated')==3
 assert log.count('Wall-follow: 13200 full source paths and 259590 exact collision calls match; empty and partial paths retained')==3
 sourcePath=ROOT/'tools/reference/generic-pet-search-producers-source-evidence.json';source=json.loads(sourcePath.read_text());artifacts.append(sourcePath)
 assert source['verified']==dict(producerContexts=9058,producerUnsupported=28,pipelineContexts=216,pipelineUnsupported=0,completeFunctions=5,completeFunctionBytes=2025,boundedLeafBytes=128,leafCodeAndNopBytes=113,leafPaddingBytes=15)
 assert not source['processMemoryRead'] and source['sourceCodeExecutedInOfflineEmulator'] and not source['handlerEnabled'] and not source['fullAiVerified']
 for n,h in source['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 lib=ROOT/source['emulatorLibrary']['path'];assert sha(lib)==source['emulatorLibrary']['sha256'];artifacts.append(lib)
 for name in ['generic-pet-producer-data-evidence.json','generic-pet-producer-reference-evidence.json']:
  path=ROOT/'tools/reference'/name;r=json.loads(path.read_text());artifacts.append(path)
  for n,h in r['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
  if name.endswith('data-evidence.json'):
   assert r['verified']==dict(statRows=368,statStride=324,missileRows=742,missileStride=460,compiledTextFieldMatches=1846,existingStatSemanticMatches=1104,compiledIdChecks=1110,textIdDifferences=1)
   assert r['flags']['currentCompiledMinimumMask']==512 and r['flags']['existingSemanticCatalogMinimumMask']==1024
  else:
   assert r['verified']==dict(completeFunctions=5,completeFunctionBytes=2025,boundedLeafBytes=128,leafCodeAndNopBytes=113,leafPaddingBytes=15,successfulCaptureReadBytes=7666,failedAttemptAnchorReadBytesInferred=560,totalReadBytesIncludingFailedAnchors=8226,maximumReadBytesPerCall=3570)
   assert not any(r[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','8de2433:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[758,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['class-skill-status.json','skill-effects.json','skill-icons.json','find-potion-level-bands.json','generic-pet-skills.json','generic-pet-states.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','8de2433:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 artifacts += [p for p in (STAGE/'diagnostics').iterdir() if p.is_file()]
 artifacts += [mainPath]+[STAGE/n for n in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','python-tests.log']]
 artifacts += [ROOT/n for n in ['tools/record_generic_pet_search_producers.py','tests/check_generic_pet_search_producers.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]
 result=dict(referenceVersion='3.3.93847',component='Current stat/missile producers,room trace and shared source stepping',installed=False,handlerEnabled=False,fullAiVerified=False,realWorldRegistryRoomGraphAccepted=False,fullSearchBoundCompositionAccepted=True,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=278,retainedMarkers=275,worldBoots=3,actualWorldReloads=2,producerContextsPerBoot=9058,explicitProducerBoundaries=28,fullSearchContextsPerBoot=216,fullSearchUnsupported=0,completeFunctions=5,completeFunctionBytes=2025,boundedLeafBytes=128,leafCodeAndNopBytes=113,leafPaddingBytes=15,sharedWallFollowPathsPerBoot=13200,exactWallFollowCollisionQueriesPerBoot=259590,reusedStatDefinitions=368,ownedMissileRows=742,compiledTextFieldMatches=1846,textIdDifferences=1,successfulCaptureReadBytes=7666,failedAnchorReadBytesInferred=560,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=[758,21],unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='8de2433',receipt='tools/reference/generic-pet-search-nested-native-evidence.json'),
  established=['Five complete current functions (2025bytes) and a bounded128-byte no-unwind lookup execute unchanged:stat/missile metadata getters,node value reader,adjacent-room resolver and1505-byte grid trace. Native producer values,first duplicate lookup,clamping,caches,origin/destination records and exact cell-read order match9058contexts per boot,including28explicit source/adapter boundaries. All216full source search compositions finish without unsupported boundaries.',
   'Current stat semantics match all368existing EquipmentStatDefinition rows for fMin,unshifted minimum and ValShift. The compiled minimum flag is0x200;the existing semantic catalog uses legacy0x400. This field mapping is explicit,not raw-layout equivalence. Text row212labels213,but compiled row212and the existing catalog retain the correct identity. All742missile sizes match current compiled byte434and owned text.',
   'The current stat reader reuses EquipmentStatNode/EquipmentStatValue,its extended/base vector choice,and the shared minimum clamp. Signed lower-bound lookup preserves first duplicates and literal supplied vector order. Alignment now passes its metadata result directly into the reader,without re-reading metadata or relying on a mutable selector side channel.',
   'Complete tracing reads origin and destination cells,clips the destination on blockage,preserves width-versus-pitch pointer stepping,all octants,mask-zero reads,room crossing/null adjacency,missing rooms and raw coordinate wrap. The origin remains unchanged. SourceLineCursor is shared with wall-follow,and all13200retained source paths/259590ordered collision queries per boot still match. Only the path kernel is reused;the old enclosing historical trap-handler gate is not claimed as a new full-class suite.',
   'The first managed run caught clipping the origin instead of the destination. Its snapshot,DLLs,fixture,harness and native log are preserved. The production port was fixed from the unchanged captured stack layout,and the oracle comparison was expanded to include both point records. The source code bytes were never changed. The corrected full run passes all275predecessor checks plus3new markers across3boots/2actual reloads.',
   'Both clean builds have zero warnings/errors;pre-build/native inputs match.71Python tests,573unchanged named decisions and106unchanged installed artifacts pass. No new learned handler,actor persistence,full-class suite or installation is claimed. The stat lookup leaf has113instruction/NOP bytes and15INT3padding before the next known unwind start;no own unwind completeness is claimed.',
   'Six bounded read-only captures preserve these references with7666recorded read bytes. An earlier no-unwind attempt read only the fixed560anchor bytes,inferred from preserved tool control flow;its process identity/read receipt was not persisted. Two pre-reader tool-hash validation rejections caused no process read. No input,attach,security change or game-memory write occurs.'],
  remaining=['Actual mask410production/meaning and Britannia grid/room/registry/ownership/stat-node producers remain unverified. Explicit table views do not establish real selector aliases or difficulty mapping. The source kernels now replace the earlier stat/missile/trace stubs,but fixture grids do not certify actual world collision bits or event ordering.',
   'General ordering4b7100,heap-backed caller storage,extended passives,NPC skills and complete actor/facet/logout/death/respec/save lifecycle still gate learned demons. Negative outer special indices,cyclic invalid graphs and out-of-range buffer views remain explicit gaps. No class is complete;effects/audio and talent/hotkey icons remain separate placeholders.'])

 out=ROOT/'tools/reference/generic-pet-search-producers-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Search producers:278native/71Python;9058producer/216full search cases and13200shared wall-follow paths per boot;actual mask/world/ordering/actor unfinished.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
