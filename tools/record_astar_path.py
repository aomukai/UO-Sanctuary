#!/usr/bin/env python3
"""Bind point-path obstacle repair to exact source/native/save evidence."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/astar-path'
PARENT='3762091f523d3c78f978fee32ed0e5e10bc22aa3'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ('birth','audit'):snapshot(STAGE/'pre-build-1.json',STAGE/(name+'-inputs.json'))
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {p:h for p,h in native['sourceInputs'].items() if p.endswith('.cs')}==audit['sourceInputs']
    prior=json.loads((ROOT/'.local/class-skills/frenzy-release/birth-inputs.json').read_text())
    assert native['copiedData']==prior['copiedData']
    changed={p for p,h in native['sourceInputs'].items() if p.startswith(('src/','shared/')) and prior['sourceInputs'].get(p)!=h}
    assert changed=={'src/Sanctuary.Content/SourceAStarPath.cs','src/Sanctuary.Content/SourceActorPath.cs'}
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    sourcepath=ROOT/'tools/reference/astar-path-source-evidence.json';source=json.loads(sourcepath.read_text())
    assert source['cases']==3360 and source['collisionQueries']==827372 and not source['currentExecutableParity']
    for name,digest in source['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/name)==digest,name
    for name,digest in source['artifacts'].items():assert sha(ROOT/name)==digest,name
    fixture=source['fixture'];assert native['sourceInputs'][fixture['path']]==fixture['sha256']==sha(ROOT/fixture['path'])
    assert len(json.loads((ROOT/fixture['path']).read_text()))==3360
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==69
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('AStar:3360 complete source contexts/827372 ordered queries;392 changed straight routes/572 partial routes')==3
    for name in ('astar-path','toward-path','necromancer-pet-ai','necromancer-pet-targets'):
        assert log.count(' INF] '+name+'-check: passed')==3,name
    for name in ('skeleton-world','skeleton-combat','iron-golem-world'):
        assert log.count(' INF] '+name+'-live-result: passed')==3,name
    for name in ('skeleton-birth-right','skeleton-birth-quick'):assert log.count(' INF] '+name+'-result: passed')==3,name
    saved=json.loads((STAGE/'skeleton-world-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Life']==69247 and saved['TargetLife']==5120000 and saved['LegacySeed']==173
    assert len({saved[n] for n in ('Owner','Body','Target','Legacy')})==4
    for name,count in (('named-item-runtime-audit.json',936),('first-loop-affix-audit.json',21)):
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT))
        skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    a=(STAGE/'audit-native-checks.log').read_text();assert a.count(': passed')==2 and ': failed' not in a and ' ERR]' not in a
    for kind in ('content','integration'):
        assert all(s in (STAGE/(kind+'-build-1.log')).read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    py=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in py and '\nOK\n' in py
    for name,expected in (('named-readiness.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):
        assert expected in (STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    files=[Path(__file__).resolve(),sourcepath,ROOT/'tests/check_astar_path.py',
        *(ROOT/'docs'/n for n in ('astar-path-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-1.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-1.log','audit-native-checks.log','audit-run-1.log','source-check.log','python-tests.log','named-readiness.log','catalogue-check.log','roadmap-check.log','content-build-1.log','integration-build-1.log','skeleton-world-fixtures.json','iron-golem-world-fixtures.json'))]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,goalComplete=False,completedClasses=[],
        component='Shared existing straight point-path AStar obstacle repair',
        verified=dict(sourceContextsPerBoot=3360,orderedCollisionQueriesPerBoot=827372,changedStraightRoutesPerBoot=392,partialStraightRoutesPerBoot=572,nativeMarkers=69,boots=3,reloads=2,saves=3,retainedNativeMarkers=63,pythonTests=71,auditFingerprints=[936,21],namedDecisions=573,handlers=191,protectedInstalledArtifactsUnchanged=106),
        limits=['Complete older point kernel/straight fallback with explicit pattern0 synthetic collision and no unit target; current executable parity remains unverified. General initializer is marked BROKEN upstream.',
            'The existing live controller consumes repaired routes, but no new actual native obstacle/automatic route-selection scene or naturally resumed repaired trajectory is certified here. Component JSON round-trips and retained original actor reloads are distinct scopes.',
            'General path1/unit-target adjustment/footprints, room crossing/recache, source global event/geometry/client parity remain unfinished. Propagation-stack overflow is explicitly refused.',
            'No deployment, installed-save change, handler/class completion, final effect/audio or final icon is claimed.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/astar-path-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Shared AStar repair:3360source contexts/827372ordered queries per boot;69markers/3boots/2reloads;71Python;106installed unchanged;191handlers/no completed class.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
