#!/usr/bin/env python3
"""Bind complete legacy Frenzy release/charge/packet control to current native checks."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/frenzy-release'
PARENT='4c88e60740cb49b95fa6b7ac3157d23665903cbf'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE;native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-1.json',STAGE/'birth-inputs.json')
    sourcepath=ROOT/'tools/reference/frenzy-release-source-evidence.json';source=json.loads(sourcepath.read_text())
    assert source['cases']==15024 and source['byFamily']=={'stats':1188,'roll':12099,'phase':1737}
    assert not source['currentExecutableParity'] and not source['handlerEnabled'] and len(source['completeRoutineHashes'])==6
    for name,digest in source['artifacts'].items():assert sha(ROOT/name)==digest,name
    for name,digest in source['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/name)==digest,name
    fixture=source['fixture'];assert sha(ROOT/fixture['path'])==fixture['sha256'] and len(json.loads((ROOT/fixture['path']).read_text()))==15024
    assert native['sourceInputs'][fixture['path']]==fixture['sha256']
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==191
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count(' INF] double-swing-phase-check: passed')==3 and log.count(' INF] frenzy-release-check: passed')==3
    assert log.count('Frenzy:15024complete legacy contexts')==3
    for name in ('weapon-hands','inferno-stream','inferno-primitives','goatman-body','fire-bolt','ice-bolt','ice-blast','glacial-spike','fire-ball','nova','frost-nova','poison-nova','inferno-mode','arctic-mode'):
        assert log.count(' INF] '+name+'-check: passed')==1 and log.count(' INF] '+name+'-reload-check: passed')==2,name
    for name in ('inferno-mode','arctic-mode'):assert log.count(' INF] '+name+'-live-result: passed')==3
    for name in ('fire-bolt','ice-bolt','ice-blast','glacial-spike','fire-ball','nova','frost-nova'):
        for slot in ('right','quick'):assert log.count(' INF] '+name+'-'+slot+'-result: passed')==3
    hands=json.loads((STAGE/'weapon-hands.json').read_text());assert hands['Owner'] and hands['Primary']!=hands['Secondary'] and hands['Helm'] and hands['Gloves']
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {p:h for p,h in native['sourceInputs'].items() if p.endswith('.cs')}==audit['sourceInputs']
    a=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in a and ' ERR]' not in a and a.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',935),('first-loop-affix-audit.json',21)]:
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    for kind in ('content','integration'):assert all(s in (STAGE/(kind+'-build-1.log')).read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    py=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in py and '\nOK\n' in py
    catalog=(STAGE/'catalogue-checks.log').read_text()
    for s in ('573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert s in catalog
    previous=json.loads((ROOT/'.local/class-skills/double-swing-phase/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==previous['artifacts'][name]
    assert {p for p,h in native['copiedData'].items() if previous['copiedData'].get(p)!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    assert {p for p,h in native['sourceInputs'].items() if p.startswith(('src/','shared/')) and previous['sourceInputs'].get(p)!=h}=={'src/Sanctuary.Content/BarbarianFrenzyRelease.cs'}
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses'] and sum(x['implementation']=='handler-present' for x in status['skills'])==191
    files=[Path(__file__).resolve(),sourcepath,ROOT/'tools/probe_frenzy_release.py',ROOT/'tests/check_frenzy_release.py',ROOT/'tests/check_double_swing_phase.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/reference/barbarian-frenzy-preflight.json',
        *(ROOT/'docs'/name for name in ('frenzy-release-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-1.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','content-build-1.log','integration-build-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','source-check.log','python-tests.log','catalogue-checks.log','weapon-hands.json','goatman-body-fixtures.json','inferno-stream-fixtures.json','inferno-primitives.json','inferno-mode-fixture.json','arctic-mode-fixture.json')),*(p for p in sorted((STAGE/'diagnostic-json-framing-1').rglob('*')) if p.is_file())]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,status='Complete legacy Frenzy do9/ApplyFrenzyStats/RollFrenzyDamage prepared and source/native checked;modern start67/world/stat/packet integration and learned Frenzy remain unfinished.',
        verified=dict(sourceCasesPerBoot=15024,chargeCases=1188,packetCases=12099,composedReleaseCases=1737,nativeMarkers=191,boots=3,reloads=2,saves=3,retainedNaturalChannelScenes=6,pythonTests=71,namedDecisions=573,handlers=191,protectedInstalledArtifactsUnchanged=106),
        limits=['Complete legacy Frenzy bodies,shared aura writer and Param1helpers execute with existing GUID selection. Formula,stat,packet,equipment and target services are explicit;modern start67,first-hit/payment/hand/world/input remain unverified.',
            'Synthetic aura/formula values do not certify modern speed curves,physical damage,real timed stat consumers or persistence. Used-skill replacement,narrow-field and frame-wrap controls include synthetic contexts.',
            'The earlier188marker scope and saved original hand/flight/item/Goatman identities are retained. No new automatic player attack,buff timer or RNG stream is enabled.',
            'No deployment,save reset,new learned handler,complete class,final effect/audio or final icon is claimed.'],
        sourceDiagnostic='diagnostic-json-framing-1 preserves a preparation-only JSON envelope failure;the compiled reference exited0with clean sanitizers. No accepted fixture or native run used that incomplete output.',
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/frenzy-release-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Frenzy release:15024source cases per boot;191markers/3boots/2reloads;earlier GUID/hands/channel/input/item scopes retained;71Python;191handlers;106installed unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
