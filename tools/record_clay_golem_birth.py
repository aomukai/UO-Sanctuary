#!/usr/bin/env python3
"""Bind exact learned Clay Golem birth/resources and retained native mechanics."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/clay-golem-birth'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/mage-clock-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)-set(before)=={'src/Sanctuary.Content/ClayGolemBody.cs','src/Sanctuary.Content/NecromancerClayBirth.cs','tests/Sanctuary.Integration/ClayGolemBirthChecks.cs'}
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/NecromancerSummons.cs','src/Sanctuary.Content/SkeletonPets.cs'}
    assert set(inputs['birth']['copiedData'])==set(old['copiedData'])
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/mage-clock/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'clay-golem-birth-check':1,'clay-golem-birth-reload-check':2})
    assert sum(expected.values())==303 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'skeleton-contact-fixtures.json').read_text());seed=(666<<32)|173
    assert saved['Generation']==2 and saved['Seed']==seed
    mage=json.loads((STAGE/'mage-missile-fixtures.json').read_text());assert mage['Generation']==2
    clay=json.loads((STAGE/'clay-golem-birth-fixtures.json').read_text());assert clay['Generation']==2 and clay['Maximum']==648*256 and clay['Life']==648*256-257
    source_proofs={}
    for name in ['skeleton-birth-source-evidence.json','npc-equipment-damage-source-evidence.json','npc-equipment-critical-source-evidence.json','mage-missile-source-evidence.json','clay-golem-birth-source-evidence.json']:
        path=ROOT/'tools/reference'/name;proof=json.loads(path.read_text())
        for field in ['inputs','artifacts']:
            for p,h in proof.get(field,{}).items():assert sha(ROOT/p)==h,p
        source_proofs[name]=dict(path=str(path.relative_to(ROOT)),sha256=sha(path))
    proof=json.loads((ROOT/'tools/reference/skeleton-birth-source-evidence.json').read_text())
    assert proof['cases']==11475 and proof['unsafePercentageCases']==1542 and proof['nonpositiveWrappedLifeCases']==795 and proof['inspectedCallerLevelArgument']==0
    clayproof=json.loads((ROOT/'tools/reference/clay-golem-birth-source-evidence.json').read_text());assert clayproof['cases']==11475 and clayproof['unsafePercentageCases']==1098 and clayproof['nonpositiveWrappedLifeCases']==1041 and sum(x['corroboratedFields'] for x in clayproof['localTables'].values())==132
    iron=json.loads((STAGE/'iron-golem-cast-fixtures.json').read_text())
    assert iron['RawMana'] is not None and 0<=iron['RawMana']-iron['Mana']<=255
    assert iron['RawPendingMana'] is not None and 0<=iron['RawPendingMana']-iron['PendingMana']<=255
    for kind in ['content','integration']:
        log=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(x in log for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,size in [('named-item-runtime-audit.json',834),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/mage-clock'/name).read_text())
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
    paths += [ROOT/p for p in ['tests/check_clay_golem_birth.py','docs/clay-golem-birth-checkpoint.md','tools/probe_clay_golem_birth.py','docs/skeletal-mage-upgrade-plan.md','docs/skeleton-components-source-review.md','tools/reference/skeleton-shield-observations.json','data/Sanctuary/Balance/necromancer-summons.json','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','skeleton-contact-fixtures.json','mage-missile-fixtures.json','clay-golem-birth-fixtures.json','iron-golem-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Learned Clay Golem exact birth/resources',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=303,retainedPredecessorMarkers=300,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[834,21],unchangedInstalledArtifacts=106,sourceContexts=11475,safeSourceMatches=10377,unsafeSourceRefusals=1098,normalRank4Life256=52480,nightmareRank4Life256=91840,nightmareRank4Regeneration256=67,gearSynergyLife256=165888,gearSynergyRegeneration256=121,finalSavedGeneration=2,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},sourceProofs=source_proofs,
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after)-set(before))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['New learned Clay Golems use an explicit subclass with exactFP256 life and shared source MAXHP regeneration. Rank4/Mastery0 Normal is205HP/38FP recovery;Nightmare is358.75HP/67FP. Native40ms/PMH,relative facet life,Battle Orders and two actual injured parked reloads pass.',
            'Complete source base/passive/resistance/MAXHP callback composition covers11475contexts:10377safe matches and1098explicit arithmetic refusals. All three difficulties are preflighted before creation,including a Normal-safe/Hell-unsafe domain. Owner physical Sunder remains explicitly refused.',
            'Real learned recasting pays once and replaces the prior golem. Actual Wormskull plus hard skill allocation yields Clay5/Mastery8,hardBlood2/Iron3/Fire4:648HP/121FP recovery,164damagepercent,24velocitypercent and54SummonResist. Later owner bonuses do not rewrite captured birth stats.',
            'Historical plain NecromancerMinion Clay actors retain their saved type and prior resource branch under a separate owner. The staged kernel/runtime data and106installed artifacts remain unchanged;all300predecessor native markers,4audits/71Python and573named decisions pass.'],
        remaining=['Clay incoming-melee slow callback27has source review and registration evidence but is not newly integrated here. Existing outgoing slow/integer weapon damage and generic720ms action/movement remain unfinished consumers;full source unarmed damage/critical/contact/return and AI67/S1 still need integration.',
            'Full modern initializer/component/event/seed/geometry/global-frame/client and PvP/special-recipient parity remain unverified. Historical actor upgrade/binary migration is not claimed;new actors retain the existing65000native life ceiling. Other golem and curse acceptance continues.',
            'No complete class,deployment or new1180-marker broad run is claimed. Final Clay emergence/body/hit/slow/death effects/audio and talent/hotkey icons remain separate pending ledgers. Approved later pet hard-point ability unlocks await the post-baseline upgrade pass.'])
    destination=ROOT/'tools/reference/clay-golem-birth-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Clay birth:303markers/3boots/2reloads,10377safe source matches/1098refusals,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
