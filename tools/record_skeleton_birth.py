#!/usr/bin/env python3
"""Bind learned regular skeleton exact birth/resources and retained mechanics."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/skeleton-birth'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/poison-explosion-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)-set(before)=={'src/Sanctuary.Content/NecromancerSkeletonBirth.cs','src/Sanctuary.Content/RaisedSkeleton.cs','tests/Sanctuary.Integration/SkeletonBirthChecks.cs'}
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/'+name+'.cs' for name in ['NecromancerMageBirth','NecromancerSkeletons','NpcEquipmentCritical','NpcEquipmentResources','PersistentMinions','SkeletonPets']}|{'tests/Sanctuary.Integration/BattleCommandChecks.cs'}
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/poison-explosion/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'skeleton-birth-check':1,'skeleton-birth-reload-check':2,'skeleton-birth-right-check':3,'skeleton-birth-right-result':3,'skeleton-birth-quick-check':3,'skeleton-birth-quick-result':3})
    assert sum(expected.values())==280 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'skeleton-birth-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Life256']==4027 and saved['Maximum256']==8064 and saved['Rate']==7
    source_proofs={}
    for name in ['skeleton-birth-source-evidence.json']:
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
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/poison-explosion'/name).read_text())
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
    paths=[previous,ROOT/'tools/reference/poison-spell-potion-deviation.json',ROOT/'tools/build_class_skills.py',ROOT/'tools/probe_skeleton_birth.py',Path(__file__).resolve()]
    paths += [ROOT/p for p in sorted(changed|set(after)-set(before))]
    paths += [ROOT/p for p in ['tests/check_skeleton_birth.py','docs/skeleton-birth-checkpoint.md','data/Sanctuary/Balance/necromancer-summons.json','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/poison-nova-source-preparation.md','docs/t29-combat-potions.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','skeleton-birth-fixtures.json','iron-golem-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Learned regular skeleton exact birth/resources',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=280,retainedPredecessorMarkers=265,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[831,21],unchangedInstalledArtifacts=106,sourceContextsPerBoot=11475,matchedPositiveContexts=9933,explicitOverflowRefusals=1542,actualRightQuickCasts=6,finalSavedGeneration=2,life256=8064,regeneration256=7,petLevel=78,armor=533,attack=902,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},sourceProofs=source_proofs,
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after)-set(before))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['New learned skeletons reuse the existing actor/AI but opt into a typed RaisedSkeleton with exact life and the existing NPC resource clock. Historical SkeletonGuardian actors and prepared NPC/item holders retain their existing path. The saved roster/count/cap/class-history predicates recognize both regular types.',
            'Complete source base/passive/resistance/MAXHPcallback helpers compare11475contexts:9933positive source cases match and1542extreme signed-product domains explicitly refuse. The inspected complete do31caller supplies level argument0. Rank4/Mastery0/owner99 Normal is8064FP(31.5HP),rate7,pet level78,armor533,AR902;fixed noRatio spawn bases21/30/42 are retained.',
            'Regular skeleton and Mage now share passive/MAXHP resource composition. Learned casting consumes the corpse and9mana once using the existing exact mana helper;fractional damage/healing/full cap,PMH,Battle Orders,facet life ratios,captured ranks,legacy isolation and6actual right/quick casts pass.',
            'Two actual reloads preserve the new subtype,owner,parked state,captured ranks,exact injured life,maximum and regeneration;recovery resumes once without offline replay. All265predecessor markers,including Mage/FIFO/Battle Command and item/poison/field saves,remain.'],
        remaining=['Full source do31initializer/component/shield/equipment choice,AI/animation/attack/critical,modern executable and incoming/event/terrain/global-frame/FCR/client parity remain unverified. The existing attack and movement timing adapters are retained.',
            'Physical Sunder delivery and extreme signed-product birth inputs explicitly refuse rather than invent stats. Source right-only binding policy versus the existing legacy left allowance remains a known class-wide binding follow-up;this resource increment verifies right/quick inputs. Historical body migration and old-content binary upgrade are not claimed.',
            'No complete class,deployment or new broad1180-marker run is claimed. Next:remaining Necromancer summon combat and curse acceptance. Final skeleton body/summon/combat effects/audio and talent/hotkey icons remain separately pending.'])
    destination=ROOT/'tools/reference/skeleton-birth-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Skeleton birth:280markers/3boots/2reloads,11475source contexts,8064FP life/rate7,6real inputs,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
