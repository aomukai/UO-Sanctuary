#!/usr/bin/env python3
"""Record current room-list/monster-hook comparisons and retained actual native placement."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/pet-room-links'
PARENT='636571e0b6fe5d389bc5444eef48f01cecd55c8a';sha=inventory.sha
def record(compare=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ('birth','audit'):snapshot(STAGE/'pre-build-1.json',STAGE/(name+'-inputs.json'))
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {n:h for n,h in native['sourceInputs'].items() if n.endswith('.cs')}==audit['sourceInputs']
    prior=json.loads((ROOT/'.local/class-skills/pet-native-placement/birth-inputs.json').read_text());assert native['copiedData']==prior['copiedData']
    assert {n:h for n,h in native['sourceInputs'].items() if n.startswith(('src/','shared/'))}=={n:h for n,h in prior['sourceInputs'].items() if n.startswith(('src/','shared/'))}
    assert {n for n,h in native['sourceInputs'].items() if n.startswith('tests/') and n.endswith('.cs') and prior['sourceInputs'].get(n)!=h}=={'tests/Sanctuary.Integration/PetRoomLinkChecks.cs','tests/Sanctuary.Integration/PetMonsterPlacementHookChecks.cs'}
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    sourcepaths=[ROOT/'tools/reference/pet-room-links-source-evidence.json',ROOT/'tools/reference/pet-monster-placement-hooks-source-evidence.json']
    for path,cases,unsupported in zip(sourcepaths,[5952,9780],[3222,12]):
        source=json.loads(path.read_text());assert source['referenceVersion']=='3.3.93847' and source['cases']==cases and source['unsupportedCases']==unsupported and source['offlineMachineCodeExecuted']
        assert not any(source[k] for k in ('processMemoryReadDuringProbe','targetProcessCodeExecuted','worldMovementVerified','handlerEnabled'))
        for name,h in source['artifacts'].items():assert sha(ROOT/name)==h,name
        fixture=source['fixture'];assert native['sourceInputs'][fixture['path']]==fixture['sha256']==sha(ROOT/fixture['path'])
        assert len(json.loads((ROOT/fixture['path']).read_text()))==cases
    assert '5952 cases' in (STAGE/'source-check.log').read_text() and '9780 cases' in (STAGE/'hooks-source-check.log').read_text()
    baseline=ROOT/'.local/class-skills/pet-native-placement/birth-native-checks.log'
    oldReceipt=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/pet-native-placement-native-evidence.json'],cwd=ROOT));assert sha(baseline)==oldReceipt['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);expected['pet-room-links-check']+=3;expected['pet-monster-placement-hooks-check']+=3
    assert sum(expected.values())==140 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert all(log.count(f'Native pet placement:generation{g},two original actors;')==1 for g in range(3))
    assert log.count('Pet room links:5952complete current-code cases,3222unsupported controls;')==3
    assert log.count('Pet monster placement hooks:9780complete current-code cases,12unsupported controls;')==3
    originals=json.loads((STAGE/'goatman-body-fixtures.json').read_text());placements=json.loads((STAGE/'pet-native-placement-fixtures.json').read_text())
    assert len(originals)==len(placements)==2 and {p['Generation'] for p in placements}=={2}
    assert {p['Body'] for p in placements}=={p['Body'] for p in originals}
    for p in placements:
        o=next(o for o in originals if o['Body']==p['Body']);assert all(p[k]==o[k] for k in ('Owner','Body','Life','Mana','Order'))
        assert p['LastLanding']['X']>0 and p['LastLanding']['Y']>0
    saved=json.loads((STAGE/'pet-reconnect-fixture.json').read_text());obstacle=json.loads((STAGE/'pet-obstacle-fixture.json').read_text())
    assert saved['Generation']==obstacle['Generation']==2 and saved['Body']==obstacle['Body'] and saved['Owner']==obstacle['Owner'] and saved['Seed']==obstacle['Seed']
    assert saved['TotalDraws']==10 and saved['Life']==69247 and saved['Mana']==54272
    for name,count in (('named-item-runtime-audit.json',944),('first-loop-affix-audit.json',21)):
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,h in current['InputHashes'].items():assert sha(ROOT/path)==h,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    a=(STAGE/'audit-native-checks.log').read_text();assert a.count(': passed')==2 and ': failed' not in a and ' ERR]' not in a
    for kind in ('content','integration'):assert all(t in (STAGE/(kind+'-build-1.log')).read_text() for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    for name,text in (('named-readiness.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in (STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==h,name
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    diagnostic=STAGE/'diagnostic-marker-site-1'
    original=(diagnostic/'probe_pet_room_links.py').read_text();current=(ROOT/'tools/probe_pet_room_links.py').read_text()
    assert 'UC_ERR_WRITE_UNMAPPED' in (diagnostic/'source-run.log').read_text()
    fixed=original.replace('BASE+0x2a3bd4','BASE+0x2a3b6b').replace('  self.map(STACK,0x40000);',"  assert bytes(self.uc.mem_read(BASE+0x2a3b6b,6))==b'\\xc7\\x00\\x01\\x00\\x00\\x00'\n  self.map(STACK,0x40000);")
    assert fixed==current
    files=[Path(__file__).resolve(),*sourcepaths,ROOT/'tests/check_pet_room_links.py',baseline,
        *(ROOT/'docs'/n for n in ('pet-room-links-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-1.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-1.log','audit-native-checks.log','audit-run-1.log','source-run-2.log','source-check.log','hooks-source-run-1.log','hooks-source-check.log','python-tests.log','named-readiness.log','catalogue-check.log','roadmap-check.log','content-build-1.log','integration-build-1.log','pet-native-placement-fixtures.json','goatman-body-fixtures.json','pet-reconnect-fixture.json','pet-obstacle-fixture.json')),
        *[p for p in diagnostic.iterdir() if p.is_file()]]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,goalComplete=False,completedClasses=[],component='Current room-list and forced monster-hook differential acceptance with retained native placement',
        verified=dict(currentCodeCasesPerBoot=15732,roomLinkCasesPerBoot=5952,monsterHookCasesPerBoot=9780,unsupportedControlsPerBoot=3234,nativeMarkers=140,retainedNativeMarkers=134,boots=3,reloads=2,saves=3,originalActorsPerBoot=2,nativeScenesPerBoot=8,pythonTests=71,namedDecisions=573,auditFingerprints=[944,21],handlers=191,protectedInstalledArtifactsUnchanged=106),
        limits=['Complete add/remove/update-removal/touch routines match all exposed kind1 unit/room fields,alignment/fatal ordering and partial writes. Cyclic lists/other unit kinds and global native room/crowd ownership remain unsupported or separate.',
            'Complete forced remove/restore bodies,actual current mask/size getters and composed footprint/write kernels match final buffers and resolver/fatal order. Conditional force0,other unit kinds and SIMD/direct-cell callback grouping are not certified.',
            'Both prior reviewed monster-hook/room-link comparison gaps are resolved for the actual typed callers. Retained native scenes use supplied prepared paths;birth/controller/combat,global membership/crowd/exact visibility and learned casting remain open. No production-source/data/deployment,new handler,class completion or final-art claim.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    target=ROOT/'tools/reference/pet-room-links-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if compare:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Current pet room links/hooks:15732cases per boot;140markers/3boots/2reloads;actual native/original actors retain;71Python/106installed unchanged;global/controller scope remains open.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
