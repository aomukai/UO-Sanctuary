#!/usr/bin/env python3
"""Bind Clay Golem prepared result/contact/block/HP/return and retained mechanics."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/clay-golem-contact'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/clay-golem-combat-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)-set(before)=={'src/Sanctuary.Content/ClayGolemContact.cs','tests/Sanctuary.Integration/ClayGolemContactChecks.cs'}
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/IronGolemContact.cs','src/Sanctuary.Content/RaisedSkeletonContact.cs','tests/Sanctuary.Integration/SkeletonContactChecks.cs','tests/Sanctuary.Integration/ClayGolemCombatChecks.cs'}
    assert set(inputs['birth']['copiedData'])==set(old['copiedData'])
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/clay-golem-combat/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'clay-golem-contact-check':1,'clay-golem-contact-reload-check':2})
    assert sum(expected.values())==315 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'clay-golem-contact-fixtures.json').read_text());seed=(666<<32)|173
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
    for name,size in [('named-item-runtime-audit.json',836),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/clay-golem-combat'/name).read_text())
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
    paths += [ROOT/p for p in ['tests/check_clay_golem_contact.py','docs/clay-golem-contact-checkpoint.md','tools/prepare_clay_golem_world_reference.py','tools/reference/clay-golem-world-preflight.json','data/Sanctuary/Balance/necromancer-summons.json','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','clay-golem-contact-fixtures.json','clay-golem-combat-fixtures.json','iron-golem-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Clay Golem prepared result,contact,block,HP and return',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=315,retainedPredecessorMarkers=312,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[836,21],unchangedInstalledArtifacts=106,actualClayMeleeCycles=3,baselineActionMs=400,baselineCycleMs=720,intrinsicCriticalPercent=5,normalRank4Minimum256=1049,normalRank4ExclusiveMaximum256=2624,finalSavedGeneration=2,finalSavedSeed=seed,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},sourceProofs=source_proofs,
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after)-set(before))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Learned Clay attacks now share the reviewed unarmed summon geometry/result supplier with RaisedSkeleton and the existing Iron ordered contact/HP/return services. Geometry uses the pinned source class289,size2,reach0 and existing explicit native coordinate/LOS adapters.',
            'Actual A1 captures the source hit/block result and immutable FP256 impact at start. Three connected400/720ms cycles require the exact prepared packet/stream at start,no damage at380ms and exact release by520ms without reroll.',
            'Native controls verify shields,immutable start-time mitigation,range free,double-release refusal,Life Tap before lethal return,exact-zero death/owner credit,recovery/suspension cancellation,and a reported legacy fallback for unmapped native recipients before allocation. Later allocation/release failures never replay through legacy damage.',
            'Two actual reloads retain a parked Clay body/owner,unchanged source stream and no pending packet. All312predecessor markers,4audits/71Python,573named decisions,runtime data and106installed artifacts remain. The existing positive-visible-life-loss outgoing slow is explicitly retained as an adapter,not certified item19parity.'],
        remaining=['Full modern event graph/current-executable behavior and outgoing slow/item19 remain uncertain;unsupported recipient traits explicitly report legacy use or pause only the faulted target after allocation.',
            'Initial S1/AI67/movement,initializer/seed/global-frame/client geometry,live rate rescheduling and alternate animation modes remain unverified. Historical plain minions keep their prior type/path. Other golem/curse acceptance continues.',
            'No complete class,deployment or new broad1180-marker run is claimed. Final Clay emergence/body/attack/critical/hit/slow/death effects/audio and tree/right/quick icons remain separate pending ledgers.'])
    destination=ROOT/'tools/reference/clay-golem-contact-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Clay prepared contact:315markers/3boots/2reloads,3real400/720ms attacks,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
