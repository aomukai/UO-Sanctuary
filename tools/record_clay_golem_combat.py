#!/usr/bin/env python3
"""Bind Clay Golem shared unarmed damage, intrinsic critical, G1 action clock and saved stream."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/clay-golem-combat'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/clay-golem-slow-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)-set(before)=={'tests/Sanctuary.Integration/ClayGolemCombatChecks.cs'}
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/'+name+'.cs' for name in ['ClayGolemBody','NpcEquipmentCritical','NpcEquipmentDamage','NpcEquipmentElements','NpcEquipmentImpact','NpcEquipmentPoison','NpcEquipmentRandom','RaisedSkeleton']}
    assert set(inputs['birth']['copiedData'])==set(old['copiedData'])
    assert {p for p,h in old['copiedData'].items() if inputs['birth']['copiedData'][p]!=h}=={'Data/Sanctuary/Balance/necromancer-summons.json'}
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/clay-golem-slow/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'clay-golem-combat-check':1,'clay-golem-combat-reload-check':2,'clay-golem-combat-live-result':3})
    assert sum(expected.values())==312 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'clay-golem-combat-fixtures.json').read_text());seed=(666<<32)|173
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
    for name,size in [('named-item-runtime-audit.json',835),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/clay-golem-slow'/name).read_text())
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
    paths += [ROOT/p for p in ['tests/check_clay_golem_combat.py','docs/clay-golem-combat-checkpoint.md','tools/prepare_clay_golem_world_reference.py','tools/reference/clay-golem-world-preflight.json','data/Sanctuary/Balance/necromancer-summons.json','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','clay-golem-combat-fixtures.json','iron-golem-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Clay Golem unarmed damage,critical and G1 A1 clock',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=312,retainedPredecessorMarkers=306,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[835,21],unchangedInstalledArtifacts=106,actualClayMeleeCycles=3,baselineActionMs=400,baselineCycleMs=720,intrinsicCriticalPercent=5,normalRank4Minimum256=1049,normalRank4ExclusiveMaximum256=2624,finalSavedGeneration=2,finalSavedSeed=seed,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},sourceProofs=source_proofs,
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after)-set(before))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Clay Golem now joins RaisedSkeleton through an explicit shared unarmed-NPC interface. Existing original-item branches retain their context. Fractional physical rolls,intrinsic5percent critical,actual Enchant/critical and empty own-poison inputs use the existing source services;owner weapon/poison/IAS/skill changes are not inherited.',
            'Rank4/Normal/no Fire synergy physical range is1049..2623FP256,exclusive maximum2624. Source hit/physical/intrinsic draws use the same saved actor stream;ordinary actual Strike and preparation controls verify the shared packet. No fake original item is fabricated and deletion clears the Clay stream.',
            'Owned G1A1has16frames/speed232/action1at index9. The shared planner gives10/18frames:400ms release/720ms cycle. Slow50percent gives20/36;175percent final rate cap gives6/11. Three connected native attacks require no hit at380ms and the exact fractional hit/source stream by520ms.',
            'Two actual reloads preserve parked owner/subtype/tagged stream and resume exactly one explicit draw. All306predecessor markers,original-item matrices,4audits/71Python,573named decisions and106installed artifacts remain. The only runtime change is the6-mode G1animation row.'],
        remaining=['This increment still delivers Clay strikes through the existing native health/event bridge. NPC blocking and complete start-time prepared result/contact/HP/return/event composition remain to be connected;unarmed allocation components alone are not full ordered-contact acceptance.',
            'Existing outgoing slow/item19uncertainty,initial S1/AI67/movement,source initializer/global-frame/seed/current-executable/client parity and live mid-animation rescheduling remain unverified. Historical plain minions keep their prior path. Other golem and curse acceptance continue.',
            'No complete class,deployment or new1180-marker broad run is claimed. Final Clay emergence/body/attack/critical/hit/slow/death effects/audio and tree/right/quick icons remain separate pending ledgers.'])
    destination=ROOT/'tools/reference/clay-golem-combat-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Clay combat:312markers/3boots/2reloads,3real400/720ms attacks,5percent critical,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
