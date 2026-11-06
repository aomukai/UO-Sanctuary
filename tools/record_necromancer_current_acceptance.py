#!/usr/bin/env python3
"""Bind the current combined Necromancer cohort without claiming class completion."""
import argparse
from collections import Counter
from pathlib import Path
import sys

import skill_checkpoint_evidence as e
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot

ROOT=e.ROOT
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'necromancer-current-acceptance'
MECHANICS=BASE/'necromancer-current-mechanics'
PARENT='ed214d8291d5fb1c160c304df90d9e8e12629f5b'
BUILD=7
sys.path.insert(0,str(ROOT/'tests'))
from check_necromancer_current_acceptance import configuration


def record(check=False):
    prebuild=STAGE/f'source-snapshot-{BUILD}.json'
    tested=[]
    for stage,filename in ((STAGE,'birth-inputs.json'),(MECHANICS,'birth-inputs.json'),(STAGE,'audit-inputs.json')):
        inventory.STAGE=stage
        tested.append(inventory.validate_inventory(stage/filename))
        snapshot(prebuild,stage/filename)
    born,diagnostic,audit=tested
    assert born['artifacts']==diagnostic['artifacts']==audit['artifacts']
    assert born['copiedData']==diagnostic['copiedData']==audit['copiedData']
    prior=e.load(BASE/'npc-lightning-return/source-snapshot-14.json')['inputs']
    current=e.load(prebuild)['inputs']
    changed={p for p in prior['sourceInputs'] if prior['sourceInputs'][p]!=current['sourceInputs'].get(p)}
    assert prior['sourceInputs'].keys()==current['sourceInputs'].keys()
    assert changed=={'tests/Sanctuary.Integration/'+n for n in ('ParkingBoundaryChecks.cs','MageElementChecks.cs','IronGolemCastChecks.cs','SummonResistChecks.cs')}
    assert prior['copiedData']==current['copiedData'] and prior['projectInputs']==current['projectInputs']
    parent=e.load(BASE/'npc-lightning-return/birth-inputs.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll','Assemblies/Sanctuary.Content.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]
    assert all(s in(STAGE/f'integration-build-{BUILD}.log').read_text()for s in('Build succeeded.','0 Warning(s)','0 Error(s)'))

    old=e.load(ROOT/'tools/reference/necromancer-mechanics-native-evidence.json')
    expected=Counter(old['markers'])
    for name in ('necromancer-army','revive-native-activation'):
        expected[name+'-check']+=1
        expected[name+'-reload-check']+=2
    assert sum(expected.values())==473
    mechanics_log=e.cohort(MECHANICS,expected,473)
    full=configuration()
    assert len(full['extra_families'])==115 and len(full['extra_checks'])==21
    assert len(full['extra_live_commands'])==35 and len(full['extra_target_input_commands'])==46
    expected_full=expected.copy()
    for name in full['extra_live_commands']:expected_full[name+'-result']+=3
    for name in full['extra_target_input_commands']:
        expected_full[name+'-check']+=3
        expected_full[name+'-result']+=3
    assert sum(expected_full.values())==854
    log=e.cohort(STAGE,expected_full,854)
    assert 'pass across3boots/2reloads' in(STAGE/'run-1.log').read_text()
    assert 'pass across3boots/2reloads' in(MECHANICS/'run-5.log').read_text()
    assert log.count('Parking boundary runtime maps:Trammel=7168x4096,Felucca=7168x4096')==1
    assert mechanics_log.count('Mage elements reload:8 original actors/two poison conversions')==2
    fingerprints=e.audits(STAGE,PARENT,born['artifacts'])
    protected=e.protection()
    commands=e.load(STAGE/'verification-commands.json')
    assert len(commands)==4 and all(c['exitCode']==0 for c in commands)
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert sum(r['implementation']=='handler-present' for r in status['skills'])==197
    assert sum(r['characterClass']=='Necromancer' and r['implementation']=='handler-present' for r in status['skills'])==30
    assert not status['completedClasses']
    files=e.stage_files(STAGE,MECHANICS)
    diagnostics={}
    for n,name,message in ((1,'ParkingBoundaryChecks.cs','NullReferenceException'),(2,'MageElementChecks.cs','Saved mixed Mage roster was pruned/replaced'),(3,'IronGolemCastChecks.cs','Unsupported proc entered learned windup'),(4,'SummonResistChecks.cs','Saved recipient')):
        folder=MECHANICS/f'failed-mechanics-{n}'
        inputs=e.load(folder/'birth-inputs.json')
        assert e.sha(folder/name)==inputs['sourceInputs']['tests/Sanctuary.Integration/'+name]
        assert message in(folder/'birth-native-checks.log').read_text()
        for kind in ('Content','Integration'):
            p=folder/kind.lower()/f'Sanctuary.{kind}.dll'
            assert e.sha(p)==inputs['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
            files.append(p)
        files += e.stage_files(folder)+list(folder.glob('*.cs'))+list(folder.glob('*.txt'))
        diagnostics[str(folder.relative_to(ROOT))]=message
    failed_build=STAGE/'failed-build-4'
    assert 'error CS0136' in(failed_build/'integration-build-4.log').read_text()
    assert e.sha(failed_build/'MageElementChecks.cs')==e.load(failed_build/'source-snapshot-4.json')['inputs']['sourceInputs']['tests/Sanctuary.Integration/MageElementChecks.cs']
    files += e.stage_files(failed_build)+list(failed_build.glob('*.cs'))
    files += [Path(__file__).resolve(),ROOT/'tests/check_necromancer_current_acceptance.py',ROOT/'tools/reference/necromancer-mechanics-native-evidence.json',BASE/'npc-lightning-return/source-snapshot-14.json']
    files += [ROOT/'docs'/name for name in ('necromancer-current-acceptance-checkpoint.md','necromancer-coverage-matrix.md','npc-lightning-return-checkpoint.md','necromancer-mechanical-audit.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')]
    result=dict(referenceVersion='3.3.93847',component='Current combined Necromancer mechanics, approved army caps and actual input acceptance',
        parent=PARENT,productionChanged=False,installed=False,goalComplete=False,completedClasses=[],integrationBuild=BUILD,mechanicsRun=5,fullRun=1,auditRun=2,diagnostics=diagnostics,
        verified=dict(mechanicsMarkers=473,fullMarkers=854,fixtureFamilies=115,sharedChecks=21,liveActions=105,inputActions=138,
            bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,handlers=197,necromancerHandlers=30,auditFingerprints=fingerprints,protected=protected),
        inputInventories={str((s/f).relative_to(ROOT)):e.sha(s/f) for s,f in ((STAGE,'birth-inputs.json'),(MECHANICS,'birth-inputs.json'),(STAGE,'audit-inputs.json'))},
        mechanicsMarkers=dict(sorted(expected.items())),fullMarkers=dict(sorted(expected_full.items())),
        established=[
            'The current combined cohort retains every original 467 mechanics marker and adds approved army limits and original Revive activation. Actual live/right/quick input actions execute on the same frozen production/integration binaries through two reloads.',
            'Test-only repairs use rank10 for a two-Revive parking roster, preserve all eight old/new Mage migration cases over two legal four-Mage owners, use still-unsupported Howltusk instead of supported Goblin Toe for refusal conservation, and train rank3 for the three-Mage Summon Resist cohort. Production C#, data, native kernel and client remain unchanged.',
            'Runtime maps measure7168x4096. The earlier6144-wide boundary explanation for the failed6140corridor is withdrawn; that placement failure remains uncertain.'],
        remaining=[
            'This reruns the original combined cohort plus army limits, not every newer dedicated checkpoint. See the 30-talent coverage map and original receipts for their exact binary identities and limits.',
            'Exact modern barrier death/footprint/owner graph, wider native AI/species/item/event/geometry/PvP acceptance and individual documented shield/poison uncertainties remain. No class-completion certificate is inferred.',
            'Graphical-client testing and final effects/audio/icons remain separate; identified placeholders do not block mechanical implementation.'])
    e.write('necromancer-current-acceptance-native-evidence.json',result,files,check)
    print('Current Necromancer:473mechanics/854combined markers,115families,105live+138input actions;two reloads each;class gaps explicit.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
