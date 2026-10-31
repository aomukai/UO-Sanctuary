#!/usr/bin/env python3
"""Bind Blood Golem prepared result/contact/block/HP/return and retained mechanics."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/blood-golem-combat'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/blood-golem-birth-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)-set(before)=={'src/Sanctuary.Content/BloodGolemContact.cs','src/Sanctuary.Content/BloodGolemHealing.cs','tests/Sanctuary.Integration/BloodGolemCombatChecks.cs','tests/Sanctuary.Integration/BloodGolemContactChecks.cs'}
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/'+name+'.cs' for name in ['BloodGolemBody','NecromancerBloodBirth','IronGolemContact','RaisedSkeletonContact']}
    assert set(inputs['birth']['copiedData'])==set(old['copiedData'])
    assert {p for p,h in old['copiedData'].items() if inputs['birth']['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/necromancer-summons.json'}
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/blood-golem-birth/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'blood-golem-combat-check':1,'blood-golem-combat-reload-check':2,'blood-golem-combat-live-result':3,'blood-golem-contact-check':1,'blood-golem-contact-reload-check':2})
    assert sum(expected.values())==339 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'blood-golem-contact-fixtures.json').read_text());seed=(666<<32)|173
    assert saved['Generation']==2 and saved['Seed']==seed
    source_proofs={}
    for name in ['skeleton-birth-source-evidence.json','npc-equipment-damage-source-evidence.json','npc-equipment-critical-source-evidence.json','blood-golem-healing-source-evidence.json']:
        path=ROOT/'tools/reference'/name;proof=json.loads(path.read_text())
        for field in ['inputs','artifacts']:
            for p,h in proof.get(field,{}).items():assert sha(ROOT/p)==h,p
        source_proofs[name]=dict(path=str(path.relative_to(ROOT)),sha256=sha(path))
    proof=json.loads((ROOT/'tools/reference/skeleton-birth-source-evidence.json').read_text())
    assert proof['cases']==11475 and proof['unsafePercentageCases']==1542 and proof['nonpositiveWrappedLifeCases']==795 and proof['inspectedCallerLevelArgument']==0
    heal=json.loads((ROOT/'tools/reference/blood-golem-healing-source-evidence.json').read_text());assert heal['cases']==dict(healing=15559,incoming=3456) and heal['currentParameters']['incomingShare']==0
    combat=json.loads((STAGE/'blood-golem-combat-fixtures.json').read_text());stream=(666<<32)|173
    for _ in range(3):stream=(0x6AC690C5*(stream&0xffffffff)+(stream>>32))&0xffffffffffffffff
    assert combat['Generation']==2 and combat['Seed']==stream
    iron=json.loads((STAGE/'iron-golem-cast-fixtures.json').read_text())
    assert iron['RawMana'] is not None and 0<=iron['RawMana']-iron['Mana']<=255
    assert iron['RawPendingMana'] is not None and 0<=iron['RawPendingMana']-iron['PendingMana']<=255
    for kind in ['content','integration']:
        log=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(x in log for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,size in [('named-item-runtime-audit.json',840),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/blood-golem-birth'/name).read_text())
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
    paths += [ROOT/p for p in ['tests/check_blood_golem_combat.py','docs/blood-golem-combat-checkpoint.md','tools/prepare_blood_golem_world_reference.py','tools/reference/blood-golem-world-preflight.json','tools/probe_blood_golem_healing.py','data/Sanctuary/Balance/necromancer-summons.json','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','blood-golem-contact-fixtures.json','blood-golem-combat-fixtures.json','iron-golem-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Blood Golem unarmed damage,critical,G2 clock,prepared contact and FP healing',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=339,retainedPredecessorMarkers=330,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[840,21],unchangedInstalledArtifacts=106,actualBloodMeleeCycles=3,baselineActionMs=400,baselineCycleMs=640,intrinsicCriticalPercent=5,normalRank4Minimum256=4748,normalRank4ExclusiveMaximum256=13568,healingSourceCases=15559,incomingSourceCases=3456,currentIncomingShare=0,finalSavedGeneration=2,finalSavedSeed=seed,finalCombatSeed=stream,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},sourceProofs=source_proofs,
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after)-set(before))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Learned Blood Golems now reuse the shared unarmed FP damage/intrinsic critical/typed Enchant/poison-input and saved actor-stream services. Rank4/Normal/no Fire synergy gives4748..13567FP256,exclusive13568,intrinsic5percent. Owner gear/poison/IAS/later ranks do not leak;deletion clears the stream.',
            'Owned G2 A1is16frames/speed256/action1index10. Actual attacks capture hit/block/FP damage at start and release at400ms within640ms. Three connected cycles require exact start packet/stream,no damage at380ms and exact loss by520ms. Slow50percent gives20/32frames;final175percent rate gives6/10.',
            'Complete unchanged healing callbacks23/26,heal helper,11033/11034and percentage bodies execute15559healing/3456incoming contexts. Native policy matches final HP,overlays and ordered owner/pet/overflow writes. Drain scales before target-life cap;current Param5zero transfers no incoming damage to the owner.',
            'Actual mapped undead contacts verify FP owner/pet/overflow shares,near-maximum fractions,no early healing,no healing on block,zero-share native melee/ranged reception,Life Tap before Thorns,immutable mitigation,range/free/replay,cancellation,exact-zero death and owner credit. Two reloads preserve parked subtype/owner/streams and no pending replay.',
            'All330predecessor markers,6Blood right/quick casts,4audits/71Python,573named decisions and106installed artifacts remain. Only the6-mode G2animation row changes runtime data.'],
        remaining=['Legacy callback11034rounding differs from table calc2dm at some ranks (rank12:130versus129). The complete old callback is implemented and the discrepancy is explicit;current3.3.93847executable rounding is not certified.',
            'Recipient alignment without a reviewed mapping uses the reported existing native bridge before allocation;later allocation/release failures never replay through that bridge. Historical plain minions and explicit legacy virtual Strike retain the old native/integer healing path. Full special/dynamic recipient/PvP/event/notification graph remains open.',
            'Initial S1/AI/movement,initializer/equipment/seed/global-frame/client collision parity,alternate modes and live animation-rate rescheduling remain unverified. Other Necromancer summons/curses continue. No complete class,deployment or new broad1180-marker run is claimed. Final effects/audio and tree/right/quick icons remain separate.'])
    destination=ROOT/'tools/reference/blood-golem-combat-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Blood combat:339markers/3boots/2reloads,3connected400/640ms attacks,15559healing/3456incoming source contexts,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
