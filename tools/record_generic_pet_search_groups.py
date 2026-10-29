#!/usr/bin/env python3
"""Accept current search-state preparation with retained buff/corpse behavior."""
import argparse,hashlib,json,re,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_shape_restrictions import frozen_receipt
from snapshot_skill_build import check as check_build_snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-search-groups'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 inventory.STAGE=STAGE;inputs=inventory.validate_inventory(STAGE/'stats-inputs.json');check_build_snapshot(STAGE/'pre-build-inputs.json',STAGE/'stats-inputs.json');artifacts=[]
 _,prior=frozen_receipt('937f83f','tools/reference/find-potion-level-native-evidence.json');assert prior['verified']['passMarkers']==251
 for kind in ['content','integration']:
  p=STAGE/(kind+'-build.log');assert all(s in p.read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)']);artifacts += [p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')]
 log=(STAGE/'stats-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==254
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 pattern=r'\] ([A-Za-z0-9-]+): passed';before=Counter(re.findall(pattern,(ROOT/'.local/class-skills/find-potion-levels/stats-native-checks.log').read_text()));after=Counter(re.findall(pattern,log))
 assert sum(before.values())==251 and not before-after and after-before==Counter({'generic-pet-search-groups-check':1,'generic-pet-search-groups-reload-check':2})
 assert log.count('GenericPet search groups:819captured-current contexts,15fatal boundaries;A2,state expansion,prior clearing,live candidate slots and metadata order pass;post-cleanup callback/world actor remain gated')==3
 assert log.count('GenericPet preparation:4682captured-current contexts,200fatal boundaries;exact descriptor targets,state groups,recipient callbacks and service order pass;real world iterators/state/geometry remain gated')==3
 assert log.count('Find Potion monster tiers:144real boundary/facet/type casts;recorded death levels,legacy fallback/conflict,low-level owner,no area tags,payment,cancel,loot and pending-save fixtures pass')==1
 sourcePath=ROOT/'tools/reference/generic-pet-search-groups-source-evidence.json';source=json.loads(sourcePath.read_text());artifacts.append(sourcePath)
 assert source['verified']==dict(contexts=819,fatalBoundaries=15,preparationBytes=846,appendBytes=253,completeFunctionsExecuted=2,stateVectorGrowths=1206,previousOwnedStateFrees=649,maxOutputGroups=9,maxStateIds=24)
 assert not source['processMemoryRead'] and source['sourceCodeExecutedInOfflineEmulator'] and not source['handlerEnabled'] and not source['fullAiVerified']
 for n,h in source['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 library=ROOT/source['emulatorLibrary']['path'];assert sha(library)==source['emulatorLibrary']['sha256'];artifacts.append(library)
 buffPath=ROOT/'tools/reference/generic-pet-preparation-source-evidence.json';buff=json.loads(buffPath.read_text());assert buff['verified']['contexts']==4682
 for n,h in buff['artifacts'].items():assert sha(ROOT/n)==h,n
 artifacts.append(buffPath)
 # Bind the caller's observed order separately from prepared state values.
 caller=ROOT/'.local/class-skills/warlock-runtime-reference/callees-live-actions-targets-20261029.json';capture=json.loads(caller.read_text());artifacts.append(caller)
 assert capture['executableSha256']=='e1f5436e3d9687f644ef16938b1b183d1fdef434f18cf66d852cf68f48cc8936'
 for n,h in capture['artifacts'].items():assert sha(ROOT/n)==h,n;artifacts.append(ROOT/n)
 asm=(ROOT/'.local/class-skills/warlock-runtime-reference/callees-live-actions-targets-20261029-4b3df0-part0.asm').read_text()
 assert asm.index('call   0x1404b2cb0')<asm.index('call   0x1404b6c30')<asm.index('call   0x1404b3210')
 audits=[]
 for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
  p=STAGE/name;a=json.loads(p.read_text());assert a==json.loads((ROOT/'tools/reference'/name).read_text());assert a['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
  for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
  audits.append(a);artifacts += [p,ROOT/'tools/reference'/name]
 old=json.loads(subprocess.check_output(['git','show','937f83f:tools/reference/named-item-runtime-audit.json'],cwd=ROOT));assert audits[0]['Definitions']==old['Definitions'] and len(audits[0]['Definitions'])==573 and [len(a['InputHashes']) for a in audits]==[743,21]
 tests=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in tests and '\nOK\n' in tests and 'FAILED' not in tests
 mainPath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainPath.read_text());assert len(main['installedArtifacts'])==106
 for n,h in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/n)==h,n
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(s['implementation']=='handler-present' for s in status['skills'])==184
 for name in ['class-skill-status.json','skill-effects.json','skill-icons.json','find-potion-level-bands.json']:
  p=ROOT/'data/Sanctuary/Balance'/name;assert p.read_bytes()==subprocess.check_output(['git','show','937f83f:'+str(p.relative_to(ROOT))],cwd=ROOT);artifacts.append(p)
 artifacts += [mainPath]+[STAGE/n for n in ['pre-build-inputs.json','stats-inputs.json','stats-native-checks.log','run.log','source-run.log','source-recheck.log','python-tests.log']]
 artifacts += [p for p in (STAGE/'diagnostics').rglob('*') if p.is_file()]
 artifacts += [ROOT/n for n in ['tools/record_generic_pet_search_groups.py','tests/check_generic_pet_search_groups.py','tools/build_skill_roadmap.py','tools/snapshot_skill_build.py','tools/build_property_integration_backlog.py','tools/reference/item-property-integration-backlog.json']]
 result=dict(referenceVersion='3.3.93847',component='Current GenericPet search-state preparation and shared state-group expansion',installed=False,handlerEnabled=False,fullAiVerified=False,postCleanupCallbackAccepted=False,realMetadataAndWorldSearchAccepted=False,newActorPersistenceAccepted=False,goalComplete=False,handlers=184,warlockHandlers=1,completedClasses=[],
  verified=dict(passMarkers=254,retainedMarkers=251,worldBoots=3,actualWorldReloads=2,contextsPerBoot=819,fatalBoundaries=15,completeFunctions=2,completeFunctionBytes=1099,stateVectorGrowthRequestsInSourceOracle=1206,previousOwnedStateFreesInSourceOracle=649,retainedBuffContextsPerBoot=4682,retainedBuffFatalBoundaries=200,preBuildAndNativeSourceDataMatch=True,pythonTests=71,unchangedNamedDefinitions=573,inputFingerprints=[743,21],unchangedMainInstallationArtifacts=106,newFullClassRun=False),
  inputInventory=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(artifacts))},frozenPredecessor=dict(commit='937f83f',receipt='tools/reference/find-potion-level-native-evidence.json'),
  established=['The cached846-byte search preparation and253-byte group append execute offline. Native managed values match819contexts per boot,including15explicit invalid-selector boundaries. No running process access,input,attach,security changes or game-memory writes occur in this increment.',
   'Eligible type2/non-base candidates clear their target before skill lookup. Signed skill wordA2 is distinct from buff wordA0 and flags39. Missing/negative/out-of-range states and missing lookup rows produce no group. Group-zero states retain their exact state ID;nonzero signed groups expand related IDs in table order,including negative/duplicate groups. Group identity retains the live candidate slot rather than copying a descriptor.',
   'Destination preparation clears prior groups before lookup,including empty and fatal cases. Actual source code frees649old owned state arrays and requests1206state-ID growths across the oracle. Managed containers match prepared values and order;they do not claim native allocator/free parity. Captured append stays inside ten embedded group slots;group-vector growth and arbitrary mutable metadata remain unverified.',
   'Existing buff preparation now reuses the same grouping kernel. Its original4682source contexts and200fatal boundaries still match exact descriptors and service traces on every boot. Buff recipient selection and flags remain unchanged. No additional skill handler or demon actor is enabled.',
   'All251previous native markers remain,including approved Find Potion144casts and saved corpse levels. Three boots/two reloads retain saved fixtures;this detached preparation adds no actor persistence claim. Both clean builds zero warnings/errors;71Python tests,573unchanged named eligibility decisions and106unchanged installed artifacts pass. Source/data snapshots match native inputs. No new full-class suite or installation is claimed.'],
  remaining=['The captured caller calls846-byte preparation,then group cleanup,then its unavailable search callback. This acceptance stops at preparation return;post-cleanup ownership/lifetime and callback consumption are explicitly unverified. Do not infer use-after-free or invent a selector from the observed call order.',
   'Real NPC skill/state metadata,actual world search/validity,ordering,mode/path/event/seed ownership,owner callbacks,extended passives and summon lifecycle still gate learned Goatman/Tainted/Defiler. Continue available reference components/class roadmap while individual sources are unavailable. All classes retain their documented remaining checks;effects/audio and talent/hotkey icons stay separate placeholders.'])
 out=ROOT/'tools/reference/generic-pet-search-groups-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Search-state preparation:254native/71Python;819current cases and4682retained buff cases per boot;world search/lifetime remain gated.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
