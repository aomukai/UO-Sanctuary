#!/usr/bin/env python3
"""Accept targeted Arctic potion delivery without claiming learned casting."""
import argparse
from pathlib import Path
import sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_npc_item_spell_flights import expected

ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'arctic-potion';ISO=BASE/'arctic-potion-isolate'
PARENT='ea328567f6ded3ab08b1602eef87a045b3915dc1'


def record(check=False):
    sys.path.insert(0,str(ROOT/'tests'));from check_arctic_potion import configuration
    born=e.load(ISO/'birth-inputs.json');audit=e.load(STAGE/'audit-inputs.json')
    for path in [ISO/'birth-inputs.json',STAGE/'audit-inputs.json']:
        snapshot(STAGE/'source-snapshot-6.json',path)
        inputs=e.load(path)
        assert inputs['artifacts']==born['artifacts'] and inputs['copiedData']==born['copiedData']
        for p,h in inputs['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
    e.cohort(ISO,expected(configuration(isolate=True)),24)
    assert 'pass across3boots/2reloads'in(STAGE/'isolate-driver-5.log').read_text()
    saved=e.load(ISO/'arctic-potion-fixtures.json');assert saved['Generation']==2 and saved['Life']>0
    assert saved['Shot']['Modifiers']['ColdMastery']>0 and saved['Shot']['Modifiers']['Potion']==0
    for name in ['content-build-2.log','integration-build-6.log']:
        assert all(t in(STAGE/name).read_text()for t in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==14 and all(c['exitCode']==0 for c in commands)
    log=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in log and '\nOK\n'in log
    proof=e.load(ROOT/'tools/reference/arctic-potion-review-evidence.json')
    assert proof['arcticTimingRuleApproved'] and not proof['arcticTimingApprovalInferred'] and not proof['handlerEnabled']
    assert not proof['sourceProbeRerun'] and len(proof['unavailableHistoricalArtifacts'])==8
    for p,h in proof['inputs'].items():assert e.sha(ROOT/p)==h,p
    # All native corrections changed the fixture,not the production receiver.
    failed=['failed-reload-c2-i2','failed-reload-c2-i3','failed-packet-c2-i4','failed-packet-c2-i5']
    for name in failed:
        folder=STAGE/name
        assert ': failed'in(folder/'birth-native-checks.log').read_text()
        assert e.sha(folder/'Sanctuary.Content.dll')==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    assert 'Backpack'in(STAGE/'integration-build-1.log').read_text() and 'error CS0246'in(STAGE/'integration-build-1.log').read_text()
    frozen=STAGE/'frozen-c2-i6'
    for name,h in e.load(frozen/'hashes.json').items():assert e.sha(frozen/name)==h,name
    for kind in ['Content','Integration']:
        assert e.sha(frozen/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
    client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json');assert len(client['patchHashes'])==41
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert sum(r['implementation']=='handler-present'for r in status['skills'])==203 and not status['completedClasses']
    files=e.stage_files(STAGE,ISO)+list(frozen.iterdir())+[Path(__file__).resolve()]
    files=[p for p in files if not p.name.startswith('receipt-')]
    files+=[p for name in failed for p in(STAGE/name).iterdir()if p.is_file()]
    files+=[ROOT/p for p in proof['inputs']if not p.startswith('.local/')]
    files+=[ROOT/'tools/reference/arctic-potion-review-evidence.json']
    files+=[ROOT/'docs'/name for name in ['arctic-potion-checkpoint.md','arctic-approved-input-rule.md','shadow-world-admission-blocker.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlerEnabled=False,handlers=203,completedClasses=[],
        targetedChecks=24,boots=3,reloads=2,saves=3,contentBuild=2,integrationBuild=6,sourceSnapshot=6,
        reusedColdContexts=4590,reusedFactoryContexts=14280,sourceProbeRerun=False,unavailableHistoricalArtifacts=proof['unavailableHistoricalArtifacts'],
        pythonTests=71,verificationCommands=14,arcticTimingRuleApproved=True,learnedArcticEnabled=False,
        auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=proof['scope'],limits=proof['limits'])
    e.write('arctic-potion-native-evidence.json',result,files,check)
    print('Arctic potion:24targeted checks/3boots/2reloads/3saves;mastery/items/wounds and live element defenses verified;approved held input next,no deployment.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
