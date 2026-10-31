#!/usr/bin/env python3
"""Bind exact learned Blood Golem birth/resources and retained native mechanics."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/blood-golem-birth'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/clay-golem-contact-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)-set(before)=={'src/Sanctuary.Content/BloodGolemBody.cs','src/Sanctuary.Content/NecromancerBloodBirth.cs','tests/Sanctuary.Integration/BloodGolemBirthChecks.cs'}
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/NecromancerSummons.cs','src/Sanctuary.Content/SkeletonPets.cs'}
    assert set(inputs['birth']['copiedData'])==set(old['copiedData'])
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/clay-golem-contact/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'blood-golem-birth-check':1,'blood-golem-birth-reload-check':2,'blood-golem-birth-right-check':3,'blood-golem-birth-right-result':3,'blood-golem-birth-quick-check':3,'blood-golem-birth-quick-result':3})
    assert sum(expected.values())==330 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'skeleton-contact-fixtures.json').read_text());seed=(666<<32)|173
    assert saved['Generation']==2 and saved['Seed']==seed
    mage=json.loads((STAGE/'mage-missile-fixtures.json').read_text());assert mage['Generation']==2
    blood=json.loads((STAGE/'blood-golem-birth-fixtures.json').read_text());assert blood['Generation']==2 and blood['Maximum']==240814 and blood['Life']==240814-257
    source_proofs={}
    for name in ['skeleton-birth-source-evidence.json','npc-equipment-damage-source-evidence.json','npc-equipment-critical-source-evidence.json','mage-missile-source-evidence.json','blood-golem-birth-source-evidence.json','blood-golem-birth-independent-source-evidence.json']:
        path=ROOT/'tools/reference'/name;proof=json.loads(path.read_text())
        for field in ['inputs','artifacts']:
            for p,h in proof.get(field,{}).items():assert sha(ROOT/p)==h,p
        source_proofs[name]=dict(path=str(path.relative_to(ROOT)),sha256=sha(path))
    proof=json.loads((ROOT/'tools/reference/skeleton-birth-source-evidence.json').read_text())
    assert proof['cases']==11475 and proof['unsafePercentageCases']==1542 and proof['nonpositiveWrappedLifeCases']==795 and proof['inspectedCallerLevelArgument']==0
    bloodproof=json.loads((ROOT/'tools/reference/blood-golem-birth-source-evidence.json').read_text());assert bloodproof['cases']==11475 and bloodproof['unsafePercentageCases']==1620 and bloodproof['nonpositiveWrappedLifeCases']==1299 and sum(x['corroboratedFields'] for x in bloodproof['localTables'].values())==148
    independent=json.loads((ROOT/'tools/reference/blood-golem-birth-independent-source-evidence.json').read_text());assert independent['cases']==16065 and independent['unsafePercentageCases']==1632 and independent['nonpositiveWrappedLifeCases']==1311 and independent['independentSynergyCases']==4590 and independent['functions']==bloodproof['functions']
    iron=json.loads((STAGE/'iron-golem-cast-fixtures.json').read_text())
    assert iron['RawMana'] is not None and 0<=iron['RawMana']-iron['Mana']<=255
    assert iron['RawPendingMana'] is not None and 0<=iron['RawPendingMana']-iron['PendingMana']<=255
    for kind in ['content','integration']:
        log=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(x in log for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,size in [('named-item-runtime-audit.json',838),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/clay-golem-contact'/name).read_text())
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
    paths += [ROOT/p for p in ['tests/check_blood_golem_birth.py','docs/blood-golem-birth-checkpoint.md','tools/probe_blood_golem_birth.py','tools/probe_blood_golem_birth_independent.py','docs/blood-golem-birth-source-preparation.md','docs/skeletal-mage-upgrade-plan.md','docs/skeleton-components-source-review.md','tools/reference/skeleton-shield-observations.json','data/Sanctuary/Balance/necromancer-summons.json','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','skeleton-contact-fixtures.json','mage-missile-fixtures.json','blood-golem-birth-fixtures.json','iron-golem-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Learned Blood Golem exact birth/resources',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=330,retainedPredecessorMarkers=315,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[838,21],unchangedInstalledArtifacts=106,sourceContexts=16065,independentSynergyCases=4590,safeSourceMatches=14433,unsafeSourceRefusals=1632,nonpositiveWrappedLifeCases=1311,normalRank4Life256=82329,nightmareRank4Life256=158924,nightmareRank4Regeneration256=116,gearSynergyLife256=240814,gearSynergyRegeneration256=176,actualRightQuickCasts=6,finalSavedGeneration=2,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},sourceProofs=source_proofs,
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after)-set(before))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['New learned Blood Golems use a saved exact-resource subtype with the shared noRatio summon base/passive/MAXHP and native resource/facet/buff services. All3difficulties are preflighted before creating or replacing a golem;physical Sunder explicitly refuses.',
            'Complete unchanged source helper bodies execute16065contexts,retaining11475previous rows and4590independent hard-synergy controls. Native matches14433safe rows and refuses1632signed-percentage hazards,including1311nonpositive wrapped results.148current fields remain corroborated against the owned3.3.93847tables.',
            'Rank4/Mastery0 Normal life82329FP256 and60FP regeneration,Nightmare158924FP256 and116FP regeneration survive the actual learned factory. Real Wormskull/effective Mastery8/hard Clay4/Iron3/Fire4 gives240814FP256 life,176FP regeneration,armor699,attack1188,damagepercent244 and velocity24. Later owner ranks do not rewrite existing captures.',
            'Native checks cover once-only payment,replacement,insufficient mana retaining the old pet,40ms recovery/PMH,facet-relative fractional life,Battle Orders and historical plain-minion type persistence. Six actual right/quick casts create the exact-resource subtype with no creation/payment during windup. Two reloads preserve injured parked resources and captured owner/type.',
            'All315predecessor markers,4audits/71Python,573named decisions,runtime data,staged kernel and106installed artifacts remain unchanged.'],
        remaining=['Full event23/26health/damage-sharing behavior is not certified;the inherited existing native damage/healing bridge remains explicit. Fractional healing/event semantics,unarmed damage/critical/prepared contact,initial S1/AI/animation/movement and initializer/seed/current-executable/client parity need separate acceptance.',
            'Historical plain minions retain their existing saved type/path. The native65000HPceiling remains an identified adapter. Complete other Necromancer summon/curse acceptance before marking the class complete.',
            'No complete class,deployment or new1180-marker broad run is claimed. Blood summon/body/attack/healing/share/hit/death effects/audio and tree/right/quick icons remain separate pending ledgers.'])
    destination=ROOT/'tools/reference/blood-golem-birth-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Blood birth:330native markers/3boots/2reloads,16065source contexts/4590independent synergy rows,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
