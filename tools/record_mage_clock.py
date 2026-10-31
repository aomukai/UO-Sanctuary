#!/usr/bin/env python3
"""Bind skeletal mage A1 skill-event clock and retained native mechanics."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/mage-clock'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/skeleton-contact-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)==set(before)
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/DruidWolfGeometry.cs','src/Sanctuary.Content/NecromancerSummons.cs','tests/Sanctuary.Integration/MageMissileChecks.cs'}
    assert set(inputs['birth']['copiedData'])==set(old['copiedData'])
    assert {p for p,h in old['copiedData'].items() if inputs['birth']['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/necromancer-summons.json'}
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/skeleton-contact/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)
    assert sum(expected.values())==300 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'skeleton-contact-fixtures.json').read_text());seed=(666<<32)|173
    assert saved['Generation']==2 and saved['Seed']==seed
    mage=json.loads((STAGE/'mage-missile-fixtures.json').read_text());assert mage['Generation']==2
    source_proofs={}
    for name in ['skeleton-birth-source-evidence.json','npc-equipment-damage-source-evidence.json','npc-equipment-critical-source-evidence.json','mage-missile-source-evidence.json']:
        path=ROOT/'tools/reference'/name;proof=json.loads(path.read_text())
        for field in ['inputs','artifacts']:
            for p,h in proof.get(field,{}).items():assert sha(ROOT/p)==h,p
        source_proofs[name]=dict(path=str(path.relative_to(ROOT)),sha256=sha(path))
    proof=json.loads((ROOT/'tools/reference/skeleton-birth-source-evidence.json').read_text())
    assert proof['cases']==11475 and proof['unsafePercentageCases']==1542 and proof['nonpositiveWrappedLifeCases']==795 and proof['inspectedCallerLevelArgument']==0
    iron=json.loads((STAGE/'iron-golem-cast-fixtures.json').read_text())
    assert iron['RawMana'] is not None and 0<=iron['RawMana']-iron['Mana']<=255
    assert iron['RawPendingMana'] is not None and 0<=iron['RawPendingMana']-iron['PendingMana']<=255
    for kind in ['content','integration']:
        log=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(x in log for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,size in [('named-item-runtime-audit.json',832),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/skeleton-contact'/name).read_text())
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==size and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    log=(STAGE/'audit-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log and log.count('Shutting down')==1
    python=(STAGE/'python-tests.log').read_text()
    assert 'Ran 71 tests' in python and '\nOK\n' in python
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    paths=[previous,ROOT/'tools/reference/poison-spell-potion-deviation.json',ROOT/'tools/build_class_skills.py',ROOT/'tools/prepare_skeleton_world_reference.py',ROOT/'tools/reference/skeleton-world-preflight.json',Path(__file__).resolve()]
    paths += [ROOT/p for p in sorted(changed|set(after)-set(before))]
    paths += [ROOT/p for p in ['tests/check_mage_clock.py','docs/mage-clock-checkpoint.md','docs/skeletal-mage-upgrade-plan.md','docs/skeleton-components-source-review.md','tools/reference/skeleton-shield-observations.json','data/Sanctuary/Balance/necromancer-summons.json','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','skeleton-contact-fixtures.json','mage-missile-fixtures.json','reference-check.log','iron-golem-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Skeletal Mage source A1 skill-event clock',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=300,retainedPredecessorMarkers=300,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[832,21],unchangedInstalledArtifacts=106,actualMageCycles=3,baselineActionMs=400,baselineCycleMs=800,halfRateActionMs=800,halfRateCycleMs=1600,skillEventKind=2,finalSavedGeneration=2,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},sourceProofs=source_proofs,
        changedSourceHashes={p:after[p] for p in sorted(changed)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Pinned SKA1HTH16frames/speed208/skill event2 at index8 now feeds the existing source animation planner: action10/end20 gives400ms launch/800ms cycle. A50percent slow gives20/40frames;the175percent final rate cap gives6/12. Owner equipment IAS is excluded and other golem clocks retain their existing branch.',
            'The shared clock reads optional action-kind metadata,defaulting existing consumers to1. Mage uses kind2 without relabeling its projectile skill event as a melee event. Current local data corroborates302table fields and25SK animation rows;the only runtime change is the Mage animation row.',
            'Three connected native attacks require no projectile/noHP at360ms,launch after400ms and before800ms,and delayed damage only through traced projectile collision. Two actual reloads preserve advanced projectile state/stream and restore the computed clock.',
            'All300predecessor focused markers,Iron and regular skeleton timed controls,4audits/71Python,573named decisions and106installed artifacts remain.'],
        remaining=['Initial S1/AI67 targeting/movement,final component/shield generation and initializer seed order,global-frame/current-executable/native geometry/client parity remain unverified. An attack captures its clock at start;live mid-animation rescheduling and pending-action persistence are not certified.',
            'Existing projectile source lifetime/poison aggregation/full-event/special-recipient/PvP gaps remain. The user rank12all-visible-shields observation is recorded without inferring a distribution or block rate. Other golem and curse acceptance continue.',
            'No complete class,deployment or new1180-marker broad run is claimed. Final mage emergence/attack/elemental projectile/hit/death effects/audio and talent/hotkey icons remain separate pending ledgers.'])
    destination=ROOT/'tools/reference/mage-clock-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Mage clock:400ms skill launch/800ms cycle,300markers/3boots/2reloads,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
