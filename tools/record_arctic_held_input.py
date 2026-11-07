#!/usr/bin/env python3
"""Accept learned Arctic input with retained Inferno and honest scope limits."""
import argparse
from collections import Counter
from pathlib import Path
import sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
from record_poison_dagger_contact import markers
from record_npc_item_spell_flights import expected

ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'arctic-held-input'
PARENT='fe0eafedb1bdb4c323e58269d9e0f2cf83a21294'


def wire_markers():
    out=Counter()
    for prefix in ['inferno','arctic']:
        for source in [0,1,9]:out[f'{prefix}-held-wire-{source}-result']=3
        out[prefix+'-held-input-save-stage-check']=3
    return out


def record(check=False):
    wide,iso,shared=[BASE/('arctic-held-input'+s)for s in ['-retained','-isolate','-shared']]
    born=e.inventory(STAGE,wide,STAGE/'source-snapshot-3.json')
    sys.path.insert(0,str(ROOT/'tests'));from check_arctic_held_input import configuration
    ancestor=e.load(ROOT/'tools/reference/skill-startup-native-evidence.json')
    for old,stage,count in [('skill-startup',STAGE,195),('skill-startup-retained',wide,503),('skill-startup-shared',shared,285)]:
        path=BASE/old/'birth-native-checks.log';assert e.sha(path)==ancestor['files'][str(path.relative_to(ROOT))]
        config=configuration(retained=stage==wide,shared=stage==shared)
        actual=expected(config)+wire_markers()
        if config.get('save_result'):actual[config['save_result']]+=3
        assert not(markers(path)-actual),'A retained parent marker was dropped'
        e.cohort(stage,actual,count)
    e.cohort(iso,expected(configuration(isolate=True))+wire_markers(),54)
    fixture_counts=[]
    for stage,driver in [(STAGE,'focused-driver-2.log'),(wide,'retained-driver-2.log'),(iso,'isolate-driver-3.log'),(shared,'shared-driver-2.log')]:
        assert 'pass across3boots/2reloads'in(STAGE/driver).read_text()
        data=e.load(stage/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',stage/'birth-inputs.json')
        assert data['artifacts']==born['artifacts'] and data['copiedData']==born['copiedData']
        for p,h in data['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
        sizes={}
        for prefix,field in [('inferno','Flights'),('arctic','Missiles')]:
            saved=e.load(stage/(prefix+'-held-input-fixtures.json'))
            assert saved['Generation']==2 and saved['UnresumedSaves']==2 and saved['HeldAtFirstSave']
            assert saved['Mana256']==80*256-23*96 and saved['Life256']>0 and len(saved[field])==20
            assert all(s['Owner']==saved['Owner']and s['Rank']==1 for s in saved[field])
            if prefix=='arctic':
                assert all(s['Remaining']==17 and s['Damage']['Frames']==17 and s['Damage']['ColdFrames']==100
                           and s['Damage']['Minimum256']==256 and s['Damage']['Maximum256']==512 for s in saved[field])
                assert all(s['Modifiers']==dict(Potion=0,FireMastery=0,ColdMastery=0,LightningMastery=0,PoisonMastery=0)for s in saved[field])
            sizes[prefix]=len(saved[field])
        fixture_counts.append(sizes)
    for name in ['content-build-1.log','integration-build-2.log']:
        assert all(t in(STAGE/name).read_text()for t in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==14 and all(c['exitCode']==0 for c in commands)
    log=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in log and '\nOK\n'in log
    proof=e.load(ROOT/'tools/reference/arctic-held-input-review-evidence.json')
    assert proof['arcticInputExplicitlyApproved']and proof['learnedArcticHandler']and proof['handlers']==204 and proof['druidHandlers']==30
    assert not proof['currentD2RInputTimingVerified']and not proof['graphicalClientInputVerified']
    for p,h in proof['inputs'].items():assert e.sha(ROOT/p)==h,p
    client=e.load(ROOT/'tools/reference/arctic-held-input-client-evidence.json')
    assert client['patches']==42 and client['affectedPaths']==42 and client['buildWarnings']==44 and client['buildErrors']==0
    for group in ['patchHashes','sharedInputs','artifacts']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    old_client=e.load(ROOT/'tools/reference/inferno-held-input-client-evidence.json')
    assert len(old_client['patchHashes'])==41
    for p,h in old_client['patchHashes'].items():assert e.sha(ROOT/p)==h,p
    for name in ['failed-shared-idle-c1-i2','failed-retained-idle-c1-i2']:
        folder=STAGE/name;text=(folder/'birth-native-checks.log').read_text()
        assert ': failed'in text and ('inactivity'in text or 'stuck socket'in text)
        assert e.sha(folder/'Sanctuary.Content.dll')==born['artifacts']['Assemblies/Sanctuary.Content.dll']
        assert e.sha(folder/'Sanctuary.Integration.dll')==born['artifacts']['Assemblies/Sanctuary.Integration.dll']
    frozen=STAGE/'frozen-c1-i2'
    for n,h in e.load(frozen/'hashes.json').items():assert e.sha(frozen/n)==h,n
    for kind in ['Content','Integration']:assert e.sha(frozen/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation();assert protected==e.load(STAGE/'protection-check.json')
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert sum(r['implementation']=='handler-present'for r in status['skills'])==204 and not status['completedClasses']
    assert all(r['implementation']=='handler-present'for r in status['skills']if r['characterClass']=='Druid')
    files=[p for p in e.stage_files(STAGE,wide,iso,shared)if not p.name.startswith('receipt-')]+list(frozen.iterdir())+[Path(__file__).resolve()]
    for sub in ['failed-shared-idle-c1-i2','failed-retained-idle-c1-i2','passed-initial-c1-i1','passed-focused-initial-c1-i2']:
        files+=[p for p in(STAGE/sub).iterdir()if p.is_file()]
    files+=[ROOT/p for p in proof['inputs']if not p.startswith('.local/')]
    files+=[ROOT/'tools/reference/arctic-held-input-review-evidence.json',ROOT/'tools/review_arctic_held_client.py']
    files+=[ROOT/'docs'/n for n in ['arctic-held-input-checkpoint.md','arctic-approved-input-rule.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,learnedArcticHandler=True,arcticInputExplicitlyApproved=True,
        currentD2RInputTimingVerified=False,graphicalInputVerified=False,handlers=204,druidHandlers=30,completedClasses=[],
        contentBuild=1,integrationBuild=2,sourceSnapshot=3,focusedChecks=195,retainedChecks=503,isolatedChecks=54,sharedChecks=285,
        bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,rawSources=[0,1,9],twoConnectedSaveOwners=True,savedEmissionCounts=fixture_counts,
        clientPatches=42,clientWarnings=44,clientErrors=0,pythonTests=71,verificationCommands=14,
        auditFingerprints=fingerprints,protection=protected,artifacts=born['artifacts'],copiedData=born['copiedData'],scope=proof['scope'],limits=proof['limits'])
    e.write('arctic-held-input-native-evidence.json',result,files,check)
    print('Learned Arctic:195focused/503retained/54isolated/285shared;both channels/raw0,1,9/two connected save owners;204handlers/Druid30,no class certificate/deployment.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
