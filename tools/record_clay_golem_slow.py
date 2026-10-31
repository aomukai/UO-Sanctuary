#!/usr/bin/env python3
"""Bind Clay Golem incoming slow and split saved movement/attack rates."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/clay-golem-slow'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit','class']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/clay-golem-birth-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)-set(before)=={'src/Sanctuary.Content/ClayGolemSlow.cs','tests/Sanctuary.Integration/ClayGolemSlowChecks.cs'}
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/'+n+'.cs' for n in ['ClayGolemBody','GearEffects','PreparedMonsterDamage','PreparedWeaponEvents','SourceAttackRate','TimedEffects']}
    assert set(inputs['birth']['copiedData'])==set(old['copiedData'])
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/clay-golem-birth/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'clay-golem-slow-check':1,'clay-golem-slow-reload-check':2})
    assert sum(expected.values())==306 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'skeleton-contact-fixtures.json').read_text());seed=(666<<32)|173
    assert saved['Generation']==2 and saved['Seed']==seed
    mage=json.loads((STAGE/'mage-missile-fixtures.json').read_text());assert mage['Generation']==2
    clay=json.loads((STAGE/'clay-golem-birth-fixtures.json').read_text());assert clay['Generation']==2 and clay['Maximum']==648*256 and clay['Life']==648*256-257
    slow=json.loads((STAGE/'clay-golem-slow-fixtures.json').read_text());assert slow['Generation']==2
    source_proofs={}
    for name in ['skeleton-birth-source-evidence.json','npc-equipment-damage-source-evidence.json','npc-equipment-critical-source-evidence.json','mage-missile-source-evidence.json','clay-golem-birth-source-evidence.json','clay-golem-slow-source-evidence.json']:
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
    for name,size in [('named-item-runtime-audit.json',835),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/clay-golem-birth'/name).read_text())
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
    paths=[previous,ROOT/'tools/prepare_clay_golem_world_reference.py',ROOT/'tools/reference/clay-golem-world-preflight.json',ROOT/'tools/reference/poison-spell-potion-deviation.json',ROOT/'tools/build_class_skills.py',ROOT/'tools/prepare_skeleton_world_reference.py',ROOT/'tools/reference/skeleton-world-preflight.json',Path(__file__).resolve()]
    paths += [ROOT/p for p in sorted(changed|set(after)-set(before))]
    paths += [ROOT/p for p in ['tests/check_clay_golem_slow.py','docs/clay-golem-slow-checkpoint.md','tools/probe_clay_golem_slow.py','docs/skeletal-mage-upgrade-plan.md','docs/skeleton-components-source-review.md','tools/reference/skeleton-shield-observations.json','data/Sanctuary/Balance/necromancer-summons.json','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','skeleton-contact-fixtures.json','mage-missile-fixtures.json','clay-golem-birth-fixtures.json','clay-golem-slow-fixtures.json','iron-golem-cast-fixtures.json',f'full-run-{run}.log','class-inputs.json','class-native-checks.log']]
    broad=ROOT/'.local/class-skills/bone-spirit/class-native-checks.log'
    broadproof=json.loads((ROOT/'tools/reference/bone-spirit-native-evidence.json').read_text())
    assert sha(broad)==broadproof['artifacts'][str(broad.relative_to(ROOT))]
    assert markers(STAGE/'class-native-checks.log')==markers(broad) and sum(markers(broad).values())==1180
    log=(STAGE/'class-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==3 and log.count('Writing world save snapshot done')==35
    paths += [broad,ROOT/'tools/reference/bone-spirit-native-evidence.json']
    result=dict(referenceVersion='3.3.93847',component='Clay Golem incoming melee slow and saved split rates',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=306,retainedPredecessorMarkers=303,boots=3,actualReloads=2,saves=3,fullRegressionMarkers=1180,fullRegressionBoots=3,fullRegressionReloads=2,fullRegressionSaves=35,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[835,21],unchangedInstalledArtifacts=106,sourceCallbackCases=1280,baselineDurationFrames=750,savedMovementSlow=11,savedAttackSlow=37,finalSavedGeneration=2,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},sourceProofs=source_proofs,
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after)-set(before))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Complete callback27executes1280source presence/type/slow/monster-flag/allocation contexts. New learned Clay Golems react to successful classified melee before health,including full absorption and lethal incoming hits. Miss/attempt,ranged and spell controls do not react.',
            'Existing native AOS/explicit weapon consumers use the shared before-return hook;qualified ordered NPC damage uses the matching before-health event phase. The central phase order test uses explicit test services and does not claim every actor producer is complete.',
            'The existing saved TimedEffects store now has an optional attack-rate value. Source same-state reuse retains the first velocity penalty while refreshing30seconds and replacing attack rate. Production movement and attack consumers read their respective values;legacy rows omit the new null field and retain their shared value.',
            'Native checks cover11movement/37attack,750frame expiry,noncurse/Cleansing,atomic malformed restore,old JSON and two reloads after deleting the source. All303predecessor markers,1180broader class markers/35saves,4audits/71Python,573named decisions and106installed artifacts remain.'],
        remaining=['Native encounter grades supply explicit unique/champion flag mappings;ungraded boss/Paragon flag ambiguity reports and skips only that reaction while other combat continues. Full current monster/state-allocator/event/geometry/global-frame/PvP/client equivalence is unverified.',
            'The outgoing item19source body has an inverted nonzero guard;modern applicability remains unresolved. Existing outgoing Clay slow is retained explicitly. Remaining OTHER_ANIMRATE/alternate-mode consumers,live mid-animation rescheduling,Clay source weapon/critical/contact/AI/S1 and other golem/curse work continue.',
            'No complete class or deployment is claimed. Historical plain Clay minions retain their prior branch. Final reaction/slow/body/cast/hit/death effects/audio and tree/right/quick icons remain separate pending ledgers.'])
    destination=ROOT/'tools/reference/clay-golem-slow-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Clay incoming slow:306focused/1180broader markers,2reloads,1280source contexts,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
