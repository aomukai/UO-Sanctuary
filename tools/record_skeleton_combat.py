#!/usr/bin/env python3
"""Bind learned skeleton unarmed damage, intrinsic critical, source clock and saved stream."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/skeleton-combat'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/necromancer-binding-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)-set(before)=={'tests/Sanctuary.Integration/SkeletonCombatChecks.cs'}
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/'+name+'.cs' for name in ['NecromancerSkeletonBirth','NpcEquipmentCritical','NpcEquipmentDamage','NpcEquipmentElements','NpcEquipmentImpact','NpcEquipmentPoison','NpcEquipmentRandom','RaisedSkeleton','SkeletonPets']}
    assert set(inputs['birth']['copiedData'])==set(old['copiedData'])
    assert {p for p,h in old['copiedData'].items() if inputs['birth']['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/necromancer-summons.json'}
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/necromancer-binding/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'skeleton-combat-check':1,'skeleton-combat-reload-check':2,'skeleton-combat-live-result':3})
    assert sum(expected.values())==297 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'skeleton-combat-fixtures.json').read_text());seed=(666<<32)|173
    for _ in range(3):seed=(0x6AC690C5*(seed&0xffffffff)+(seed>>32))&0xffffffffffffffff
    assert saved['Generation']==2 and saved['Seed']==seed
    source_proofs={}
    for name in ['skeleton-birth-source-evidence.json','npc-equipment-damage-source-evidence.json','npc-equipment-critical-source-evidence.json']:
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
    for name,size in [('named-item-runtime-audit.json',831),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/necromancer-binding'/name).read_text())
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
    paths += [ROOT/p for p in ['tests/check_skeleton_combat.py','docs/skeleton-combat-checkpoint.md','data/Sanctuary/Balance/necromancer-summons.json','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','skeleton-combat-fixtures.json','iron-golem-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Learned skeleton unarmed damage,critical and A1 action clock',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=297,retainedPredecessorMarkers=291,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[831,21],unchangedInstalledArtifacts=106,actualMeleeCycles=3,baselineActionMs=480,baselineCycleMs=760,intrinsicCriticalPercent=5,normalRank4Minimum256=273,normalRank4ExclusiveMaximum256=547,finalSavedGeneration=2,finalSavedSeed=seed,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},sourceProofs=source_proofs,
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after)-set(before))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['New learned RaisedSkeleton attacks use shared source FP256 unarmed rolls with captured flat/percentage skill stats and the current monster intrinsic5percent critical. Equipped NPC consumers retain their original branch;owner weapon/poison/IAS bonuses are not inherited and later owner gear does not rewrite captured skill ranks. Real Enchant fire remains available to an unequipped pet and intrinsic critical doubles its damage.',
            'A tagged unarmed stream reuses the existing source random algorithm/store without fabricating an original item. Old equipped rows omit the new default flag and retain their saved format. Exact draws,two reloads,offline non-replay and atomic malformed restore pass;deletion cleanup is implemented.',
            'Pinned SKA11HS16frames/speed224/action10 feeds the shared source event planner. Baseline action12/end19 gives480/760ms;slow50percent gives23/37frames. Three connected native A1 cycles require no damage at400ms,then the exact fractional hit and expected source stream. Historical actors and Mage/golem timing remain on their existing paths.',
            'New learned creation preflights all three difficulties,refusing the Normal-safe/rank84/Mastery255 domain that would overflow after entering Hell. All291predecessor focused markers,4audits/71Python,573named decisions and106installed artifacts remain.'],
        remaining=['This is an ordinary damage/clock increment through the existing native health/event bridge. Complete source prepared start/contact/result/block/HP/return/event composition,initial S1/AI67movement and global-frame/seed/current-executable/client parity remain unverified;the bridge is not a complete source event certificate.',
            'Final shield/component/equipment distribution and owner/pet initialization seed order remain unresolved. Exact live mid-animation rate changes,pending action persistence,and historical body/binary migration remain unverified. No class completion,deployment or new1180-marker broad run is claimed.',
            'Regular Mage,other golem and curse acceptance continues. Final effects/audio and talent/hotkey icons remain separate pending ledgers.'])
    destination=ROOT/'tools/reference/skeleton-combat-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Skeleton combat:297markers/3boots/2reloads,3real480/760ms attacks,5percent critical,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
