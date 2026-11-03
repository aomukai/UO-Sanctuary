#!/usr/bin/env python3
"""Bind captured current relocation state to source/native and retained actors."""
import argparse,json,subprocess,struct
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/generic-pet-path-commit'
FRESH=ROOT/'.local/class-skills/pet-path-commit-reference-20261103'
PARENT='e2092bf7fd9467d2382950e4447c267851184ba3';sha=inventory.sha
def record(check=False):
 inventory.STAGE=STAGE
 native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
 for name in ('birth','audit'):snapshot(STAGE/'pre-build-1.json',STAGE/(name+'-inputs.json'))
 assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
 assert {p:h for p,h in native['sourceInputs'].items() if p.endswith('.cs')}==audit['sourceInputs']
 prior=json.loads((ROOT/'.local/class-skills/pet-reconnect/birth-inputs.json').read_text());assert native['copiedData']==prior['copiedData']
 assert {p for p,h in native['sourceInputs'].items() if p.startswith(('src/','shared/')) and prior['sourceInputs'].get(p)!=h}=={'src/Sanctuary.Content/GenericPetPathCommit.cs'}
 assert {p for p,h in native['sourceInputs'].items() if p.startswith('tests/') and p.endswith('.cs') and prior['sourceInputs'].get(p)!=h}=={'tests/Sanctuary.Integration/GenericPetPathCommitChecks.cs'}
 for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
 captureFiles=[];codeBytes=readBytes=0;identity=None
 for name in ('callees-path-commit-nearby-20261103.json','callees-path-commit-leaves-20261103.json','callees-path-commit-footprints-20261103.json'):
  path=FRESH/name;captureFiles.append(path);proof=json.loads(path.read_text())
  assert proof['referenceVersion']=='3.3.93847' and proof['directCallTargetsVerified'] and proof['processMemoryRead'] and proof['bytesRead']<=8192
  assert not any(proof[k] for k in ('processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'))
  if identity is None:identity=proof['process']
  assert proof['process']==identity
  codeBytes+=sum(r['bytes'] for r in proof['records']);readBytes+=proof['bytesRead']
  for r in proof['records']:
   assert r['allUnwindFragmentsCaptured'] and all(f['captured'] for f in r['fragments'])
   if r['rva']!='0x2a5fd0':assert all(f['invalidInstructionMarkers']==0 for f in r['fragments'])
   else:
    # This deferred wrapper contains a six-entry RVA dispatch table after
    # its return, not executable instructions. Preserve the raw capture and
    # check its exact indexed-load/jump contract without weakening capture
    # guards or admitting this wrapper into the executed oracle.
    assert [f['invalidInstructionMarkers'] for f in r['fragments']]==[0,0,2]
    raw=[(FRESH/f'callees-path-commit-footprints-20261103-2a5fd0-part{i}.bin').read_bytes() for i in range(3)]
    assert b'\x41\x83\xf9\x05' in raw[0]
    assert b'\x44\x8b\x8c\x81\x88\x61\x2a\x00' in raw[1] and b'\x4c\x03\xc9\x41\xff\xe1' in raw[1]
    assert raw[2][15:16]==b'\xc3' and int(r['fragments'][2]['rva'],16)+19==0x2a6188
    assert struct.unpack('<6I',raw[2][19:43])==(0x2a6170,0x2a6017,0x2a607e,0x2a60c8,0x2a6120,0x2a6163)
  for n,h in proof['artifacts'].items():assert sha(ROOT/n)==h,n
 assert codeBytes==4015 and readBytes==9710
 sourcepath=ROOT/'tools/reference/generic-pet-path-commit-source-evidence.json';source=json.loads(sourcepath.read_text())
 assert source['cases']==8184 and source['byFamily']=={'commit':7200,'coordinates':216,'reset':768} and source['reset96']==0
 assert source['offlineMachineCodeExecuted'] and not any(source[k] for k in ('processMemoryReadDuringProbe','targetProcessCodeExecuted','worldMovementVerified','handlerEnabled'))
 for name,h in source['artifacts'].items():assert sha(ROOT/name)==h,name
 fixture=source['fixture'];assert native['sourceInputs'][fixture['path']]==fixture['sha256']==sha(ROOT/fixture['path'])
 assert len(json.loads((ROOT/fixture['path']).read_text()))==8184
 previousReceipt=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/pet-reconnect-native-evidence.json'],cwd=ROOT))
 baseline=ROOT/'.local/class-skills/pet-reconnect/birth-native-checks.log';assert sha(baseline)==previousReceipt['evidence'][str(baseline.relative_to(ROOT))]
 expected=markers(baseline)
 for name in ('generic-pet-warp-candidate','generic-pet-monster-placement','generic-pet-warp-primitives','generic-pet-warp','generic-pet-motion-services','generic-pet-follow-movement','generic-pet-follow-executor','generic-pet-decisions','generic-pet-movement','generic-pet-parameters','generic-pet-path-commit'):expected[name+'-check']+=3
 for name in ('goatman-body','goatman-foundation'):expected[name+'-check']+=1;expected[name+'-reload-check']+=2
 assert sum(expected.values())==119 and markers(STAGE/'birth-native-checks.log')==expected
 log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
 assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
 assert log.count('path commitment:8184complete current-code cases,3183unsupported controls,684placement bindings')==3
 saved=json.loads((STAGE/'pet-reconnect-fixture.json').read_text());obstacle=json.loads((STAGE/'pet-obstacle-fixture.json').read_text())
 assert saved['Generation']==obstacle['Generation']==2 and saved['Body']==obstacle['Body'] and saved['Owner']==obstacle['Owner'] and saved['Seed']==obstacle['Seed']
 assert saved['TotalDraws']==10 and saved['Life']==69247 and saved['Mana']==54272
 seed=saved['OriginalSeed']
 for _ in range(saved['TotalDraws']):seed=(0x6AC690C5*(seed&0xffffffff)+(seed>>32))&0xffffffffffffffff
 assert seed==saved['Seed']
 goats=json.loads((STAGE/'goatman-body-fixtures.json').read_text());assert len(goats)==2 and {g['Female'] for g in goats}=={False,True}
 assert all(g['Life']==91903 and g['Maximum']==92160 and g['Body']!=g['Owner'] and g['Helm'] for g in goats)
 for name,count in (('named-item-runtime-audit.json',937),('first-loop-affix-audit.json',21)):
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
 files=[Path(__file__).resolve(),sourcepath,ROOT/'tests/check_generic_pet_path_commit.py',baseline,*captureFiles,
  *(ROOT/'docs'/n for n in ('generic-pet-path-commit-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
  *(STAGE/n for n in ('pre-build-1.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-1.log','audit-native-checks.log','audit-run-1.log','source-check.log','python-tests.log','named-readiness.log','catalogue-check.log','roadmap-check.log','content-build-1.log','integration-build-1.log','skeleton-world-fixtures.json','iron-golem-world-fixtures.json','pet-obstacle-fixture.json','pet-reconnect-fixture.json','goatman-body-fixtures.json','goatman-foundation-fixtures.json','receipt-validation-diagnostic-1.log'))]
 result=dict(referenceVersion='3.3.93847',parent=PARENT,goalComplete=False,completedClasses=[],component='Current shared path commitment/coordinate/reset state preparation',
  verified=dict(currentCodeCasesPerBoot=8184,commitCases=7200,coordinateCases=216,resetCases=768,unsupportedControlsPerBoot=3183,placementBindingsPerBoot=684,nativeMarkers=119,retainedOriginalSkeletonMarkers=80,boots=3,reloads=2,saves=3,pythonTests=71,namedDecisions=573,auditFingerprints=[937,21],handlers=191,protectedInstalledArtifactsUnchanged=106),
  captures=dict(process=identity,routineRecordBytes=4015,totalReadBytes=9710,maximumBytesPerCapture=4754,executedCodeBytes=1057,deferred='1540-byte nearby routine and1418bytes in five footprint wrappers are preserved,not implemented/certified here. The size-removal wrapper includes a24-byte six-entry dispatch table after its return;two linear-disassembly bad markers lie inside this table. Its indexed load/targets are checked separately and it is not executed by this oracle.'),
  limits=['Commit/coordinate/reset bodies execute completely with explicit collision footprint,room geometry/lookup/link and target-metadata services. No actual native world relocation or complete controller/learned skill is certified.',
   'The placement binding composes the checked helper with existing prepared policy. This is not a complete current-engine room/collision/owner/timing/AI/global-seed/client certificate.',
   'Original skeleton reconnect/obstacle and prepared Warlock/Witch body retention remain distinct scopes. No automatic Goatman AI,learned cast,new handler,class completion,deployment or final-art claim.'],
  tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
 out=ROOT/'tools/reference/generic-pet-path-commit-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Current path commitment:8184cases/684placement bindings per boot;119markers/3boots/2reloads;71Python;106installed unchanged;191handlers/no completed class.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
