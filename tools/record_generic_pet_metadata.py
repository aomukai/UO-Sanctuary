#!/usr/bin/env python3
"""Accept owned metadata adapters, source compositions and bounded new references."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_build_snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-metadata'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_build_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('020989d','tools/reference/generic-pet-search-groups-native-evidence.json');assert prior['verified']['passMarkers']==254
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==257
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/generic-pet-search-groups/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==254 and not before-after and after-before==Counter({'generic-pet-metadata-check':1,'generic-pet-metadata-reload-check':2})
 assert log.count('GenericPet metadata:429skills/232states;1287buff+1287search current-code compositions,explicit selector/missing-row guards and Map362identity pass;world selector/search/actor remain gated')==3
 assert log.count('GenericPet search groups:819captured-current contexts,15fatal boundaries;A2,state expansion,prior clearing,live candidate slots and metadata order pass;post-cleanup callback/world actor remain gated')==3
 assert log.count('GenericPet preparation:4682captured-current contexts,200fatal boundaries;exact descriptor targets,state groups,recipient callbacks and service order pass;real world iterators/state/geometry remain gated')==3
 for name in ['generic-pet-metadata-data-evidence.json','generic-pet-metadata-source-evidence.json']:
  p=ROOT/'tools/reference'/name;proof=json.loads(p.read_text());artifacts.append(p)
  assert not proof['processMemoryRead'] and not proof['handlerEnabled'] and not proof['worldSelectorBindingVerified']
  for n,h in proof['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
  if name.endswith('-data-evidence.json'):
   assert proof['verified']==dict(skillRows=429,stateRows=232,skillStride=748,stateStride=68,skillsBinaryBytes=320896,statesBinaryBytes=15780,compiledFieldMatches=1948,ownedTextFieldMatches=dict(skills=16809,states=995))
   assert len(proof['textRepresentationDifferences'])==75 and proof['publicUnnamedSkillRows']==[362]
  else:
   assert proof['verified']==dict(skillRows=429,stateRows=232,explicitSelectorViews=3,buffCompositions=1287,searchCompositions=1287,totalCompositions=2574,unsupportedBoundaries=0)
   lib=ROOT/proof['emulatorLibrary']['path'];assert sha(lib)==proof['emulatorLibrary']['sha256'];artifacts.append(lib)
 # User left the owned game idling outside town. Capture only readable direct
 # code callees, never force protected pages; no semantic acceptance follows.
 captures=[];capturedFunctions=capturedBytes=readBytes=0
 for label,count,code,reads in [('live-search-idle-20261030',1,3027,6614),('live-search-services-idle-20261030',10,2310,5180),('live-follow-services-idle-20261030',6,1364,3288),('live-search-vector-idle-20261030',2,369,1298),('live-ordering-idle-20261030',0,0,560)]:
  p=ROOT/'.local/class-skills/warlock-runtime-reference'/('callees-'+label+'.json');c=json.loads(p.read_text());artifacts.append(p)
  assert c['executableSha256']=='e1f5436e3d9687f644ef16938b1b183d1fdef434f18cf66d852cf68f48cc8936' and c['bytesRead']==reads<=8192 and c['directCallTargetsVerified']
  assert not any(c[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
  complete=[r for r in c['records'] if r['allUnwindFragmentsCaptured']];assert len(complete)==count and sum(r['bytes'] for r in complete)==code
  assert all(f['captured'] and f['invalidInstructionMarkers']==0 for r in complete for f in r['fragments'])
  if count==0:assert len(c['records'])==1 and c['records'][0]['rva']=='0x4b7100' and c['records'][0]['bytes']==1226
  for n,h in c['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
  captures.append(dict(path=str(p.relative_to(ROOT)),completeFunctions=count,capturedBytes=code,bytesRead=reads));capturedFunctions+=count;capturedBytes+=code;readBytes+=reads
 assert (capturedFunctions,capturedBytes,readBytes)==(19,7070,16940)
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','020989d:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[746,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['class-skill-status.json','skill-effects.json','skill-icons.json','find-potion-level-bands.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','020989d:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 artifacts += [mainPath]+[STAGE/n for n in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','data-build.log','python-tests.log','callback-availability.log','callback-services-availability.log','follow-services-availability.log','search-vector-availability.log','ordering-availability.log']]
 artifacts += [p for p in (STAGE/'diagnostics').rglob('*') if p.is_file()]
 artifacts += [ROOT/n for n in ['tools/record_generic_pet_metadata.py','tests/check_generic_pet_metadata.py','tools/build_skill_roadmap.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]
 result=dict(referenceVersion='3.3.93847',component='Owned compiled GenericPet metadata and explicit preparation adapters',installed=False,handlerEnabled=False,fullAiVerified=False,worldSelectorBindingVerified=False,worldSearchAndActorAccepted=False,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=257,retainedMarkers=254,worldBoots=3,actualWorldReloads=2,skillRows=429,stateRows=232,compiledFieldMatches=1948,ownedTextFieldMatches=17804,documentedRepresentationDifferences=75,buffCompositionsPerBoot=1287,searchCompositionsPerBoot=1287,retainedBuffContextsPerBoot=4682,retainedSearchContextsPerBoot=819,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=[746,21],unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  newReferenceAvailability=dict(captures=captures,completeFunctions=19,capturedCodeBytes=7070,totalReadBytes=16940,captureReadBudgetPerCall=8192,maxActualReadBytesPerCall=6614,unavailableOrderingRva='0x4b7100',newBodiesMechanicallyAccepted=False),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='020989d',receipt='tools/reference/generic-pet-search-groups-native-evidence.json'),
  established=['Strict owned CASC extraction matches pinned3.3.93847 executable/build metadata. Compiled headers/counts/strides and1948selected skill/state fields match owned text and pinned public data;17804populated text fields agree after75explicit annotation/quote/whitespace representations. Map362exists in owned data despite a missing public name;its slot and name are retained.',
   'Immutable runtime catalogs retain all429skill and232state rows in ID order,raw flags36/39,AI type,self/target signed states and signed groups. Adapter views require an explicit source selector and reject cross-view requests;no default facet/difficulty mapping or world aliasing is inferred. Missing/out-of-range rows return null;raw-state requests outside a bound row remain unsupported.',
   'Each skill is supplied as both a buff and search candidate in all three explicitly bound fixture views:1287buff and1287search complete-current-code compositions per boot match native targets/groups/service traces. These supplied categories isolate preparation contracts,not actual gathered actor categories. Real state membership and owner traversal remain supplied services. Catalogs load successfully on all three boots.',
   'All254previous markers,4682buff controls,819search controls and approved Find Potion/save fixtures remain. Both clean builds zero warnings/errors;71Python tests,573unchanged named decisions and106unchanged installed artifacts pass. Source/data snapshots match native inputs;no new full-class suite or actor persistence claim.',
   'After the user left the owned game idling outside town,the previously unavailable3027-byte search callback,ten direct helpers,six follow helpers and two vector helpers became readable. Five bounded read-only calls preserve19complete coherent bodies/7070code bytes,with16940total reads including repeated anchors. No input,attach,security change or game-memory write occurs. Ordering remains unreadable and is not forced. These bodies are references awaiting analysis/source/native comparison,not new gameplay claims.'],
  remaining=['Analyze the newly available target-search callback with its direct helpers,then compose it with existing preparation/scoring services. Resolve actual cleanup-before-callback storage consumption rather than inferring ownership semantics. Follow helpers are available for the existing executor;ordering and owner callbacks remain separate.',
   'Real world selector/table binding,skill ownership/levels,state membership,iterators,mode/path/event/seed ownership,extended passives and full summon lifecycle still gate learned demons. All classes retain outstanding acceptance;effects/audio and talent/hotkey icons stay separately recorded placeholders.'])
 out=ROOT/'tools/reference/generic-pet-metadata-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Owned pet metadata:257native/71Python;2574source compositions per boot;19new coherent reference bodies cached,not yet mechanically accepted.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
