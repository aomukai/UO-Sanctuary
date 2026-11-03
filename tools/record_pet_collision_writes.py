#!/usr/bin/env python3
"""Bind current collision writes and footprint wrapper composition evidence."""
import argparse,json,subprocess,struct
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/pet-collision-writes'
FRESH=ROOT/'.local/class-skills/pet-collision-write-reference-20261103'
PARENT='6e1fe8d0d57de2f4ca0d4dec89b13ddc21c6522e';sha=inventory.sha
def record(check=False):
 inventory.STAGE=STAGE
 native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
 for name in ('birth','audit'):snapshot(STAGE/'pre-build-1.json',STAGE/(name+'-inputs.json'))
 assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
 assert {p:h for p,h in native['sourceInputs'].items() if p.endswith('.cs')}==audit['sourceInputs']
 prior=json.loads((ROOT/'.local/class-skills/pet-collision-reads/birth-inputs.json').read_text());assert native['copiedData']==prior['copiedData']
 assert {p for p,h in native['sourceInputs'].items() if p.startswith(('src/','shared/')) and prior['sourceInputs'].get(p)!=h}=={'src/Sanctuary.Content/GenericPetCollisionWrites.cs'}
 assert {p for p,h in native['sourceInputs'].items() if p.startswith('tests/') and p.endswith('.cs') and prior['sourceInputs'].get(p)!=h}=={'tests/Sanctuary.Integration/PetCollisionWriteChecks.cs'}
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
 captureFiles=[];codeBytes=readBytes=0;identity=None
 for name in ('callees-pet-collision-writes-20261103.json',):
  path=FRESH/name;captureFiles.append(path);proof=json.loads(path.read_text())
  assert proof['referenceVersion']=='3.3.93847' and proof['directCallTargetsVerified'] and proof['processMemoryRead'] and proof['bytesRead']<=8192
  assert not any(proof[k] for k in ('processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'))
  if identity is None:identity=proof['process']
  assert proof['process']==identity
  codeBytes+=sum(r['bytes'] for r in proof['records']);readBytes+=proof['bytesRead']
  for r in proof['records']:
   assert r['allUnwindFragmentsCaptured'] and all(f['captured'] and f['invalidInstructionMarkers']==0 for f in r['fragments'])
  for n,h in proof['artifacts'].items():assert sha(ROOT/n)==h,n
 assert codeBytes==1725 and readBytes==4010
 callers=FRESH/'write-callers.json';captureFiles.append(callers);index=json.loads(callers.read_text())
 assert {r['rva'] for r in index['records']}=={'0x2a5d30','0x2a5fd0'}
 for name,h in index['artifacts'].items():assert sha(ROOT/name)==h,name
 sourcepath=ROOT/'tools/reference/pet-collision-writes-source-evidence.json';source=json.loads(sourcepath.read_text())
 assert source['cases']==39984 and source['unsupportedCases']==576 and source['byFamily']=={'addAdjacent':7680,'removeAdjacent':7680,'addBox':7704,'removeBox':7704,'addPattern':2304,'removePattern':2304,'addSize':2304,'removeSize':2304}
 assert source['offlineMachineCodeExecuted'] and not any(source[k] for k in ('processMemoryReadDuringProbe','targetProcessCodeExecuted','worldMovementVerified','handlerEnabled'))
 for name,h in source['artifacts'].items():assert sha(ROOT/name)==h,name
 fixture=source['fixture'];assert native['sourceInputs'][fixture['path']]==fixture['sha256']==sha(ROOT/fixture['path'])
 assert len(json.loads((ROOT/fixture['path']).read_text()))==39984
 previousReceipt=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/pet-collision-reads-native-evidence.json'],cwd=ROOT))
 baseline=ROOT/'.local/class-skills/pet-collision-reads/birth-native-checks.log';assert sha(baseline)==previousReceipt['evidence'][str(baseline.relative_to(ROOT))]
 expected=markers(baseline)
 expected['pet-collision-writes-check']+=3
 assert sum(expected.values())==131 and markers(STAGE/'birth-native-checks.log')==expected
 log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert log.count('collision writes:39984complete current-code cases,576unsupported controls,30336direct bindings,9216complete footprint compositions')==3
 saved=json.loads((STAGE/'pet-reconnect-fixture.json').read_text());obstacle=json.loads((STAGE/'pet-obstacle-fixture.json').read_text())
 assert saved['Generation']==obstacle['Generation']==2 and saved['Body']==obstacle['Body'] and saved['Owner']==obstacle['Owner'] and saved['Seed']==obstacle['Seed']
 assert saved['TotalDraws']==10 and saved['Life']==69247 and saved['Mana']==54272
 seed=saved['OriginalSeed']
 for _ in range(saved['TotalDraws']):seed=(0x6AC690C5*(seed&0xffffffff)+(seed>>32))&0xffffffffffffffff
 assert seed==saved['Seed']
 goats=json.loads((STAGE/'goatman-body-fixtures.json').read_text());assert len(goats)==2 and {g['Female'] for g in goats}=={False,True}
 assert all(g['Life']==91903 and g['Maximum']==92160 and g['Body']!=g['Owner'] and g['Helm'] for g in goats)
 for name,count in (('named-item-runtime-audit.json',941),('first-loop-affix-audit.json',21)):
  current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
  assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
  for path,h in current['InputHashes'].items():assert sha(ROOT/path)==h,path
  old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
  assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
 a=(STAGE/'audit-native-checks.log').read_text();assert a.count(': passed')==2 and ': failed' not in a and ' ERR]' not in a
 for kind in ('content','integration'):assert all(s in (STAGE/(kind+'-build-1.log')).read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
 assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
 for name,text in (('named-readiness.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in (STAGE/name).read_text()
 installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
 for path,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==h,path
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
 files=[Path(__file__).resolve(),sourcepath,ROOT/'tests/check_pet_collision_writes.py',baseline,*captureFiles,
  *(ROOT/'docs'/n for n in ('pet-collision-writes-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
  *(STAGE/n for n in ('pre-build-1.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-1.log','audit-native-checks.log','audit-run-1.log','source-check.log','python-tests.log','named-readiness.log','catalogue-check.log','roadmap-check.log','content-build-1.log','integration-build-1.log','skeleton-world-fixtures.json','iron-golem-world-fixtures.json','pet-obstacle-fixture.json','pet-reconnect-fixture.json','goatman-body-fixtures.json','goatman-foundation-fixtures.json'))]
 diagnostic=ROOT/'.local/class-skills/pet-collision-writes-capture-preparation-1'
 old=(diagnostic/'capture_pet_collision_writes.py').read_text();assert '0x2a49f0' in old and '0x2a49f0' not in (ROOT/'tools/capture_pet_collision_writes.py').read_text()
 preparation=json.loads((diagnostic/'note.json').read_text());assert sha(diagnostic/'capture_pet_collision_writes.py')==preparation['originalScriptSha256']
 files.extend([diagnostic/'capture_pet_collision_writes.py',diagnostic/'note.json',diagnostic/'capture.log',ROOT/'.local/class-skills/pet-collision-writes-capture-2.log',ROOT/'.local/class-skills/pet-collision-writes-source-run-1.log'])
 result=dict(referenceVersion='3.3.93847',parent=PARENT,goalComplete=False,completedClasses=[],component='Current collision writes and complete footprint wrapper compositions',
  verified=dict(currentCodeCasesPerBoot=39984,unsupportedControlsPerBoot=576,directBindingsPerBoot=30336,completeFootprintCompositionsPerBoot=9216,nativeMarkers=131,retainedNativeMarkers=128,retainedOriginalSkeletonMarkers=80,boots=3,reloads=2,saves=3,pythonTests=71,namedDecisions=573,auditFingerprints=[941,21],handlers=191,protectedInstalledArtifactsUnchanged=106),
  captures=dict(process=identity,writeRoutineRecordBytes=1725,totalReadBytes=4010,maximumBytesPerCapture=4010,deferred='Four complete current write kernels reuse the captured current resolver/box split and prior footprint wrappers. Actual native terrain/grid ownership,room links and controller remain unfinished.'),
  limits=['Direct write cases compare ordered resolver entries,normalized per-word stores and complete final buffers,including wide SIMD boxes. SIMD load grouping is not claimed. Missing adjacent metadata skips each affected point;missing box cell buffers are unsupported with earlier writes retained.',
   'Four complete footprint wrappers compose actual write kernels with shared original buffers;their comparison covers lookup order/final cells,not individual direct-cell callbacks. Actual native terrain/grid ownership,room links and controller remain unfinished.',
   'Original skeleton reconnect/obstacle and prepared Warlock/Witch body retention remain distinct scopes. No automatic Goatman AI,learned cast,new handler,class completion,deployment or final-art claim.'],
  tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
 out=ROOT/'tools/reference/pet-collision-writes-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Current collision writes:39984cases/30336bindings/9216compositions per boot;131markers/3boots/2reloads;71Python;106installed unchanged;191handlers/no completed class.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
