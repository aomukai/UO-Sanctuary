#!/usr/bin/env python3
"""Bind combined player slowing to the exact tested builds and saved regressions."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/player-attack-slow'
PARENT='f49e4175af85848724daeda6128f40733eeca45f'
sha=inventory.sha

def record(check=False,checkpoint=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    full=None if checkpoint else inventory.validate_inventory(STAGE/'class-inputs.json')
    connections=ROOT/'.local/class-skills/player-attack-slow-connections'
    inventory.STAGE=connections;smoke=inventory.validate_inventory(connections/'class-inputs.json');inventory.STAGE=STAGE
    snapshot(STAGE/'pre-build-2.json',STAGE/'birth-inputs.json')
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    for other in (smoke,audit,*(() if checkpoint else (full,))):
        assert native['artifacts']==other['artifacts'] and native['copiedData']==other['copiedData']
        assert {p:h for p,h in native['sourceInputs'].items() if p.endswith('.cs')}=={p:h for p,h in other['sourceInputs'].items() if p.endswith('.cs')}
    counts={'class':None}
    for name in (('birth',) if checkpoint else ('birth','class')):
        log=(STAGE/(name+'-native-checks.log')).read_text()
        assert ': failed' not in log and ' ERR]' not in log
        assert log.count('Shutting down')==3
        assert log.count('Writing world save snapshot done')>=3
        counts[name]=log.count(': passed')
    assert counts['birth']==234
    if not checkpoint:assert counts['class']>=1184
    log=(STAGE/'birth-native-checks.log').read_text()
    assert log.count('Writing world save snapshot done')==3
    assert log.count('player-attack-slow-check: passed')==log.count('weapon-sequence-rate-check: passed')==3
    if not checkpoint:
        full_log=(STAGE/'class-native-checks.log').read_text()
        assert full_log.count('Fists claw:3072 legacy controls')==1
        assert full_log.count('fists-claw-reload-check: passed')==2
    smoke_log=(connections/'class-native-checks.log').read_text()
    assert ': failed' not in smoke_log and ' ERR]' not in smoke_log and smoke_log.count(': passed')==12
    assert smoke_log.count('Writing world save snapshot done')==smoke_log.count('Shutting down')==3
    assert 'Legacy connected fixture smoke passed' in (connections/'run-2.log').read_text()
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in old.items() if k not in skip}=={k:v for k,v in current.items() if k not in skip}
    text=(STAGE/'audit-native-checks.log').read_text();assert text.count(': passed')==2 and ': failed' not in text and ' ERR]' not in text
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-2.log')).read_text()
        assert all(word in text for word in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for word in ('573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):
        assert word in text
    source=json.loads((ROOT/'tools/reference/player-attack-slow-source-evidence.json').read_text())
    assert source['engineContexts']==1296 and source['currentFamilyContexts']==18432 and source['prematureClampDisagreements']==546
    assert sha(ROOT/source['fixture']['path'])==source['fixture']['sha256']
    assert sha(ROOT/'.local/class-skills/player-attack-slow-probe.cpp')==source['probeSha256']
    assert sha(ROOT/'.local/class-skills/player-attack-slow-modern-probe.js')==source['scriptSha256']
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    previous=json.loads((ROOT/'.local/class-skills/blade-fury-reentry/birth-inputs.json').read_text())
    for path in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert previous['artifacts'][path]==native['artifacts'][path]
    assert native['copiedData']==previous['copiedData']
    assert {p for p,h in native['sourceInputs'].items() if p.startswith(('src/','shared/')) and previous['sourceInputs'].get(p)!=h}=={
        'src/Sanctuary.Content/'+name+'.cs' for name in ('ActionTiming','AssassinClaws','DruidFormAttacks','GearEffects','LobProjectiles','PaladinZeal','TigerStrikes')}
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    files=[Path(__file__).resolve(),ROOT/'tools/probe_player_attack_slow.py',ROOT/'tools/reference/player-attack-slow-source-evidence.json',ROOT/'tests/check_player_attack_slow.py',
        *(ROOT/'docs'/name for name in ('player-attack-slow-checkpoint.md','dragon-finisher-reference-review.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-2.json','birth-inputs.json','audit-inputs.json','content-build-2.log','integration-build-2.log','birth-native-checks.log','audit-native-checks.log','native-run-1.log','audit-run-1.log','python-tests.log','python-before-audit.log','catalogue-checks.log','source-check.log')),
        *(connections/name for name in ('class-inputs.json','class-native-checks.log','run-2.log','thunderstorm-checks.json','whirlwind-fixtures.json')),
        *(ROOT/'.local/class-skills'/name for name in ('player-attack-slow-source-2.log','player-attack-slow-probe.cpp','player-attack-slow-modern-probe.js'))]
    diagnostic=STAGE/'diagnostic-full-orb-connection-1'
    for kind in ('content','integration'):
        name='Sanctuary.'+kind.title()+'.dll';assert sha(diagnostic/kind/name)==sha(STAGE/kind/name)
    assert 'Expected one fixture connection; found 0' in (diagnostic/'class-native-checks.log').read_text()
    files.extend(p for p in diagnostic.rglob('*') if p.is_file())
    diagnostic=STAGE/'diagnostic-full-storm-teardown-3'
    for kind in ('content','integration'):
        name='Sanctuary.'+kind.title()+'.dll';assert sha(diagnostic/kind/name)==sha(STAGE/kind/name)
    text=(diagnostic/'class-native-checks.log').read_text()
    assert text.count(': passed')==735 and 'Sequence contains more than one matching element' in text
    assert 'thunderstorm-reload-result: passed' in text
    files.extend(p for p in diagnostic.rglob('*') if p.is_file())
    diagnostic=connections/'diagnostic-empty-accounts-1'
    assert 'This server has no accounts.' in (diagnostic/'class-native-checks.log').read_text()
    files.extend(p for p in diagnostic.rglob('*') if p.is_file())
    if not checkpoint:files.extend(STAGE/name for name in ('class-inputs.json','class-native-checks.log','class-run-4.log'))
    diagnostic=STAGE/'diagnostic-full-storm-connection-2'
    for kind in ('content','integration'):
        name='Sanctuary.'+kind.title()+'.dll';assert sha(diagnostic/kind/name)==sha(STAGE/kind/name)
    text=(diagnostic/'class-native-checks.log').read_text()
    assert 'frozen-orb-result: passed' in text and 'Expected one fixture connection; found 0' in text
    files.extend(p for p in diagnostic.rglob('*') if p.is_file())
    files.append(ROOT/'tests/check_class_skills.py')
    data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Targeted player slow correction and connection reload smoke verified; final broad regression pending.' if checkpoint else 'Shared player aggregate slow correction verified; no new handler or class certificate.',finalBroadRegressionPending=checkpoint,
        verified=dict(nativeMarkers=counts,connectionSmokeMarkers=12,bootsPerNativeRun=3,reloadsPerNativeRun=2,sourceContexts=1296,currentFamilyContexts=18432,currentFamilyNativeFistsContexts=9216,pythonTests=71,handlerCount=191,unchangedInstalledArtifacts=106),
        limits=['Legacy engine/current-family original calculator are supplementary evidence, not pinned modern executable equivalence.',
            'Individual slow producers and attack-entry capture are retained. Modern mid-animation event rescheduling, dual-hand stat details, NPC rollout and full client acceptance remain separate.',
            'Native NPC bounded projections are preserved. No learned Dragon/Wake/Fury gates, item gates, deployed build, saves or final assets are changed.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference'/('player-attack-slow-checkpoint-evidence.json' if checkpoint else 'player-attack-slow-native-evidence.json');encoded=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    broad='pending' if checkpoint else str(counts['class'])
    print(f'Player aggregate slows:{counts["birth"]}retained;full class markers:{broad};12connection smoke;1296source/9216native current-family contexts;191handlers;106installed hashes unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');p.add_argument('--checkpoint',action='store_true');a=p.parse_args();record(a.check,a.checkpoint)
