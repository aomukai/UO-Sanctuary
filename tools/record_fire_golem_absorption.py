#!/usr/bin/env python3
"""Bind exact learned Fire Golem absorption/resources and retained native mechanics."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/fire-golem-absorption'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/fire-golem-birth-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)-set(before)=={'tests/Sanctuary.Integration/FireGolemAbsorptionChecks.cs'}
    changed={p for p in before if before[p]!=after[p]};assert changed=={'src/Sanctuary.Content/NamedElements.cs'}
    assert set(inputs['birth']['copiedData'])==set(old['copiedData'])
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/fire-golem-birth/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'fire-golem-absorption-check':1,'fire-golem-absorption-reload-check':2,'damage-rounding-check':1,'damage-rounding-reload-check':2})
    assert sum(expected.values())==360 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'skeleton-contact-fixtures.json').read_text());seed=(666<<32)|173
    assert saved['Generation']==2 and saved['Seed']==seed
    mage=json.loads((STAGE/'mage-missile-fixtures.json').read_text());assert mage['Generation']==2
    fire=json.loads((STAGE/'fire-golem-absorption-fixtures.json').read_text());assert fire['Generation']==2 and fire['Maximum']==84134 and fire['Life']==84134-10000-208
    source_proofs={}
    for name in ['skeleton-birth-source-evidence.json','npc-equipment-damage-source-evidence.json','npc-equipment-critical-source-evidence.json','mage-missile-source-evidence.json','fire-golem-birth-source-evidence.json','fire-golem-absorption-source-evidence.json']:
        path=ROOT/'tools/reference'/name;proof=json.loads(path.read_text())
        for field in ['inputs','artifacts']:
            for p,h in proof.get(field,{}).items():assert sha(ROOT/p)==h,p
        source_proofs[name]=dict(path=str(path.relative_to(ROOT)),sha256=sha(path))
    proof=json.loads((ROOT/'tools/reference/skeleton-birth-source-evidence.json').read_text())
    assert proof['cases']==11475 and proof['unsafePercentageCases']==1542 and proof['nonpositiveWrappedLifeCases']==795 and proof['inspectedCallerLevelArgument']==0
    absorption=json.loads((ROOT/'tools/reference/fire-golem-absorption-source-evidence.json').read_text());assert absorption['cases']==2916
    iron=json.loads((STAGE/'iron-golem-cast-fixtures.json').read_text())
    assert iron['RawMana'] is not None and 0<=iron['RawMana']-iron['Mana']<=255
    assert iron['RawPendingMana'] is not None and 0<=iron['RawPendingMana']-iron['PendingMana']<=255
    for kind in ['content','integration']:
        log=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(x in log for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,size in [('named-item-runtime-audit.json',842),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/fire-golem-birth'/name).read_text())
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
    paths += [ROOT/p for p in ['tests/check_fire_golem_absorption.py','docs/fire-golem-absorption-checkpoint.md','tools/probe_fire_golem_absorption.py','tools/reference/damage-rounding-engine-evidence.json','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','skeleton-contact-fixtures.json','mage-missile-fixtures.json','fire-golem-absorption-fixtures.json','damage-rounding-checks.json','iron-golem-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Learned Fire Golem40percent absorption cap and FP health composition',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=360,retainedPredecessorMarkers=354,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[842,21],unchangedInstalledArtifacts=106,sourceContexts=2916,absorbCap=40,finalSavedGeneration=2,finalSavedLife=fire['Life'],finalSavedMaximum=fire['Maximum'],handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},sourceProofs=source_proofs,
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after)-set(before))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['New learned Fire Golems now use the ordinary accrued percentage stat pool capped40before flat absorption. Innate and own-item values compose;owner item values are not inherited. The existing shared reducer/FP health services are reused;historical plain-minion adapters retain their previous100cap.',
            'Complete unchanged resistance/absorb and percentage source bodies execute2916declared monster contexts,including raw immunity,fractional/large values,percentage pooling and flat order. Negative MDR residuals remain outside this probe under the existing zero-clamp policy.',
            'Actual rank20Fire has112raw fire resistance/88raw absorption. Applied Conviction12reduces effective fire resistance to95. Native100fire spell/weapon hits heal2then deal3;full-health entry loses3without banking healing. Qualified prepared allocation defers its512FPheal until the real health phase,then subtracts768FP.',
            'A real bound Steel Shade supplies flat fire absorption alongside rank1innate36percent;native full absorption heals once. Identify/removal controls eliminate stale item stats. This is an injected original-stat control,not a newly enabled Fire Golem equipment-casting feature.',
            'Two parked reloads preserve exact resources;controlled vulnerable1point spell/weapon packets each lose52FP net. Existing general damage-rounding/native gear regressions and all354predecessor markers pass,with4audits/71Python,573named decisions,runtime data,staged kernel and106installed artifacts unchanged.'],
        remaining=['Full current-executable/initializer/event/recipient-return and global absorption graph parity remain open. The prepared check exercises the qualified health phase,not a newly certified full contact adapter for Fire recipients.',
            'Outgoing fire damage-stat units/elemental delivery,Holy Fire pulse/weapon,death/replacement explosion,unarmed/critical/contact and S1/AI/animation/movement/client remain separate. Historical adapters require explicit migration or recasting;no silent save conversion occurs.',
            'No complete class,deployment or new broad1180-marker run is claimed. Final fire affinity/absorption/healing/pulse/hit/death effects/audio and tree/right/quick icons remain separate pending ledgers.'])
    destination=ROOT/'tools/reference/fire-golem-absorption-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Fire absorption:360markers/3boots/2reloads,2916source contexts,40percent cap/native FP order,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
