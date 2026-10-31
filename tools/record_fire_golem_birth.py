#!/usr/bin/env python3
"""Bind exact learned Fire Golem birth/resources and retained native mechanics."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/fire-golem-birth'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/blood-golem-combat-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)-set(before)=={'src/Sanctuary.Content/FireGolemBody.cs','src/Sanctuary.Content/NecromancerFireBirth.cs','tests/Sanctuary.Integration/FireGolemBirthChecks.cs'}
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/NecromancerSummons.cs','src/Sanctuary.Content/SkeletonPets.cs'}
    assert set(inputs['birth']['copiedData'])==set(old['copiedData'])
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/blood-golem-combat/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'fire-golem-birth-check':1,'fire-golem-birth-reload-check':2,'fire-golem-birth-right-check':3,'fire-golem-birth-right-result':3,'fire-golem-birth-quick-check':3,'fire-golem-birth-quick-result':3})
    assert sum(expected.values())==354 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'skeleton-contact-fixtures.json').read_text());seed=(666<<32)|173
    assert saved['Generation']==2 and saved['Seed']==seed
    mage=json.loads((STAGE/'mage-missile-fixtures.json').read_text());assert mage['Generation']==2
    fire=json.loads((STAGE/'fire-golem-birth-fixtures.json').read_text());assert fire['Generation']==2 and fire['Maximum']==216345 and fire['Life']==216345-257
    source_proofs={}
    for name in ['skeleton-birth-source-evidence.json','npc-equipment-damage-source-evidence.json','npc-equipment-critical-source-evidence.json','mage-missile-source-evidence.json','fire-golem-birth-source-evidence.json']:
        path=ROOT/'tools/reference'/name;proof=json.loads(path.read_text())
        for field in ['inputs','artifacts']:
            for p,h in proof.get(field,{}).items():assert sha(ROOT/p)==h,p
        source_proofs[name]=dict(path=str(path.relative_to(ROOT)),sha256=sha(path))
    proof=json.loads((ROOT/'tools/reference/skeleton-birth-source-evidence.json').read_text())
    assert proof['cases']==11475 and proof['unsafePercentageCases']==1542 and proof['nonpositiveWrappedLifeCases']==795 and proof['inspectedCallerLevelArgument']==0
    fireproof=json.loads((ROOT/'tools/reference/fire-golem-birth-source-evidence.json').read_text());assert fireproof['cases']==16065 and fireproof['unsafePercentageCases']==0 and fireproof['independentSynergyCases']==4590 and sum(x['corroboratedFields'] for x in fireproof['localTables'].values())==163
    iron=json.loads((STAGE/'iron-golem-cast-fixtures.json').read_text())
    assert iron['RawMana'] is not None and 0<=iron['RawMana']-iron['Mana']<=255
    assert iron['RawPendingMana'] is not None and 0<=iron['RawPendingMana']-iron['PendingMana']<=255
    for kind in ['content','integration']:
        log=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(x in log for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,size in [('named-item-runtime-audit.json',842),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/blood-golem-combat'/name).read_text())
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
    paths += [ROOT/p for p in ['tests/check_fire_golem_birth.py','docs/fire-golem-birth-checkpoint.md','tools/probe_fire_golem_birth.py','tools/prepare_fire_golem_world_reference.py','tools/reference/fire-golem-world-preflight.json','docs/skeletal-mage-upgrade-plan.md','docs/skeleton-components-source-review.md','tools/reference/skeleton-shield-observations.json','data/Sanctuary/Balance/necromancer-summons.json','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','skeleton-contact-fixtures.json','mage-missile-fixtures.json','fire-golem-birth-fixtures.json','iron-golem-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Learned Fire Golem exact birth/resources',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=354,retainedPredecessorMarkers=339,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[842,21],unchangedInstalledArtifacts=106,sourceContexts=16065,independentSynergyCases=4590,safeSourceMatches=16065,unsafeSourceRefusals=0,normalRank4PrerequisiteLife256=84134,nightmareRank4PrerequisiteLife256=164774,nightmareRank4PrerequisiteRegeneration256=120,gearSynergyLife256=216345,gearSynergyRegeneration256=158,actualRightQuickCasts=6,baselineFireResistance=142,baselineFireAbsorb=58,finalSavedGeneration=2,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},sourceProofs=source_proofs,
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after)-set(before))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['New learned Fire Golems use a saved exact-resource subtype with shared noRatio summon base/passive/MAXHP,regeneration,facet,buff and persistence services. All difficulties are preflighted before allocation/replacement;inherited fire Sunder explicitly refuses.',
            'Complete unchanged shared helper bodies execute16065safe contexts,including4590independent hard-synergy rows.163current fields are corroborated against owned3.3.93847tables. Effective Mastery and hard Blood life,Clay attack rating and Iron armor remain separate. Four fire-stat calculations and Holy Fire rank/selection are captured through declared current scalar/stat services.',
            'Actual learned rank4includes prerequisite Blood1:Normal84134FP256life/61FPregen,Nightmare164774FP256/120FPregen. Real Wormskull/effective Mastery8/hard Blood2/Clay4/Iron3 recast gives216345FP256life/158FPregen,armor779,attack1248,velocity24,fire resistance138/absorb62 and Holy Fire rank11. Later owner ranks do not rewrite captures.',
            'The fire resistance seed follows the declared source base100plus aura delta,with no Summon Resist fire addition while absorption is positive. Native resistance modifiers are preserved. Real fire delivery verifies the resulting immunity without certifying absorption caps/ordering.',
            'Once-only payment,replacement,insufficient mana retaining the old pet,40ms recovery/PMH,facet-relative life,Battle Orders,6actual right/quick casts and2injured parked reloads pass. Historical plain minions retain their type/path. All339predecessor markers,4audits/71Python,573named decisions,runtime data,staged kernel and106installed artifacts remain.'],
        remaining=['Fire damage-stat units and full elemental delivery,absorption cap/order/mixed equipment,Holy Fire pulse/weapon behavior,death/replacement explosion,unarmed critical/prepared contact,initial S1/AI/animation/movement and full initializer/current-executable/client parity remain separate. Existing inherited combat/aura/death behavior is not certified by birth captures.',
            'Fire resistance uses declared noRatio initialization and additive stat-list services;complete monster initializer/stat aggregation execution remains unverified. Native65000HPceiling remains an explicit adapter. Continue other Necromancer acceptance before marking a class complete.',
            'No complete class,deployment or new1180-marker broad run is claimed. Fire emergence/body/attack/aura/absorb/hit/death effects/audio and tree/right/quick icons remain separately pending.'])
    destination=ROOT/'tools/reference/fire-golem-birth-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Fire birth:354native markers/3boots/2reloads,16065safe source contexts/4590independent synergies,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
