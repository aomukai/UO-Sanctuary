#!/usr/bin/env python3
"""Bind shared fire/cold supplied-intent modes and retained spell/save/input scopes."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/player-inferno-modes'
PARENT='8591dbc0627be41633bff9d8fe4a66f148c1f124'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE;native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-1.json',STAGE/'birth-inputs.json')
    sourcepath=ROOT/'tools/reference/inferno-stream-source-evidence.json';source=json.loads(sourcepath.read_text())
    assert source['verified']==dict(damageContexts=4590,factoryContexts=14280,sanitizers='ASan/UBSan exit0; empty runtime stderr')
    assert source['referenceVersion']=='3.3.93847' and not source['learnedHandlerEnabled'] and not source['currentExecutableParity']
    for name,digest in source['artifacts'].items():assert sha(ROOT/name)==digest,name
    for name,digest in source['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/name)==digest,name
    for name,digest in source['pinnedInputHashes'].items():assert sha(ROOT/'tools/reference/d2r-named-equipment/json'/(name+'.json'))==digest,name
    for name,count in [('inferno-stream-damage',4590),('inferno-stream-factory',14280)]:
        assert len(json.loads((ROOT/'.local/class-skills'/(name+'-source-probe.json')).read_text()))==count
    appearancepath=ROOT/'tools/reference/warlock-demon-appearance-reference.json';appearance=json.loads(appearancepath.read_text())
    assert [(x['body'],x['sound'],x['implemented']) for x in appearance['mappings']]==[(4,372,True),(22,377,False),(149,1200,False)]
    for path,digest in appearance['nativeSourceHashes'].items():assert sha(Path(path))==digest,path
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==182
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for name in ('inferno-stream','inferno-primitives','goatman-body','fire-bolt','ice-bolt','ice-blast','glacial-spike','fire-ball','nova','frost-nova','poison-nova','inferno-mode','arctic-mode'):
        assert log.count(' INF] '+name+'-check: passed')==1 and log.count(' INF] '+name+'-reload-check: passed')==2,name
    for name in ('player-inferno','player-inferno-sequence','arctic-mode-rate'):assert log.count(' INF] '+name+'-check: passed')==3,name
    assert log.count('Inferno:4590fire source packets/14280complete-factory controls pass')==3
    for name in ('inferno-mode','arctic-mode'):
        assert log.count(' INF] '+name+'-live-check: passed')==3
        assert log.count(' INF] '+name+'-live-result: passed')==3
        mode=json.loads((STAGE/(name+'-fixture.json')).read_text());assert mode['Owner'] and mode['Mana256']==1808
    diagnostic=STAGE/'diagnostic-live-command-1'
    old=json.loads((diagnostic/'birth-inputs.json').read_text())
    snapshot(diagnostic/'pre-build-1.json',diagnostic/'birth-inputs.json')
    assert old['artifacts']==native['artifacts'] and old['copiedData']==native['copiedData']
    assert {p for p,h in old['sourceInputs'].items() if native['sourceInputs'].get(p)!=h}=={'tests/check_iron_golem_birth.py','tests/check_player_inferno_modes.py'}
    for name in ('check_iron_golem_birth.py','check_player_inferno_modes.py'):
        assert sha(diagnostic/name)==old['sourceInputs']['tests/'+name]
    assert "Server did not reach 'inferno-mode-live: ready'" in (diagnostic/'native-run-1.log').read_text()
    for kind in ('content','integration'):
        name='Sanctuary.'+kind.title()+'.dll';assert sha(diagnostic/kind/name)==sha(STAGE/kind/name)
    for name in ('fire-bolt','ice-bolt','ice-blast','glacial-spike','fire-ball','nova','frost-nova'):
        for slot in ('right','quick'):assert log.count(' INF] '+name+'-'+slot+'-result: passed')==3
    goat=json.loads((STAGE/'goatman-body-fixtures.json').read_text());assert len(goat)==2 and {x['Female'] for x in goat}=={True,False}
    assert all(x['Life']==x['Maximum']-257 and x['State'] and x['Owner'] and x['Body'] and x['Helm'] for x in goat)
    flight=json.loads((STAGE/'inferno-stream-fixtures.json').read_text());assert flight['Generation']==2 and flight['Flight']['Owner']==flight['Owner']
    assert '    Inferno = 56,' in (ROOT/'shared/ClassAbilities.cs').read_text() and flight['Flight']['Ability']==56
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {p:h for p,h in native['sourceInputs'].items() if p.endswith('.cs')}==audit['sourceInputs']
    a=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in a and ' ERR]' not in a and a.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',932),('first-loop-affix-audit.json',21)]:
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    for kind in ('content','integration'):
        assert all(s in (STAGE/(kind+'-build-1.log')).read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    py=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in py and '\nOK\n' in py
    catalog=(STAGE/'catalogue-checks.log').read_text()
    for s in ('573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert s in catalog
    previous=json.loads((ROOT/'.local/class-skills/inferno-stream/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==previous['artifacts'][name]
    assert {p for p,h in native['copiedData'].items() if previous['copiedData'].get(p)!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    assert {p for p,h in native['sourceInputs'].items() if p.startswith(('src/','shared/')) and previous['sourceInputs'].get(p)!=h}=={
        *('src/Sanctuary.Content/'+p for p in ('ClassCombat.cs','ClassMovement.cs','WardrobeTransaction.cs','DualEquip.cs','HitRecovery.cs','CharacterProfiles.cs','ClassDeath.cs','DruidArcticModes.cs','PlayerInfernoModes.cs'))}
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(x['implementation']=='handler-present' for x in status['skills'])==191
    files=[Path(__file__).resolve(),ROOT/'tests/check_inferno_stream.py',ROOT/'tests/check_player_inferno_modes.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tools/probe_inferno_stream_reference.py',ROOT/'tools/build_skill_roadmap.py',sourcepath,appearancepath,
        *(ROOT/'docs'/name for name in ('player-inferno-modes-checkpoint.md','sorceress-inferno-preflight.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-1.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','content-build-1.log','integration-build-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','python-tests.log','catalogue-checks.log','goatman-body-fixtures.json','inferno-stream-fixtures.json','inferno-primitives.json','inferno-mode-fixture.json','arctic-mode-fixture.json')),*[p for p in sorted(diagnostic.rglob('*')) if p.is_file()]]
    data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Shared supplied-intent Inferno/Arctic mode controller verified with actual timed emissions and cancellation;modern input/payment remains gated.',
        verified=dict(sourceFireContextsPerBoot=4590,sourceFactoryContextsPerBoot=14280,nativeMarkers=182,boots=3,reloads=2,saves=3,pythonTests=71,namedDecisions=573,handlers=191,protectedInstalledArtifactsUnchanged=106),
        limits=['No learned Inferno/Arctic raw input/payment cadence,current executable parity or complete-class claim. Actual supplied-intent mode integration is verified.',
            'Slow Missiles velocity is source-checked;unknown modern damage scaling is explicitly refused. Native geometry/order/creator seed and full owner lifecycle remain adapters. Potion contact reuses the existing spell consumer without a dedicated new potion scene.',
            'Legacy119and shared226flights remain independent of transient mode cancellation. Full owner death/facet/reentrant and class-switch projectile chronology remain separate;existing class cleanup policies are retained.',
            'No deployment,save reset,fresh full-class run,final custom effect/audio or final icon is claimed. D2R follow-path capture is deferred because no owned reference process was running.'],
        diagnostic=dict(path=str(diagnostic.relative_to(ROOT)),acceptedBuild=1,acceptedRun=2,sameProductionAndIntegrationBinaries=True,reason='The inherited live runner sent an unsuffixed command;checked_live_commands now sends and verifies the registered -check command. Existing unsuffixed fixtures retain their old path.'),tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/player-inferno-modes-native-evidence.json';encoded=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Shared fire/cold modes:182markers/3boots/2reloads,6natural connected scenes;retained source/flight/item/input scopes;71Python;191handlers;106installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
