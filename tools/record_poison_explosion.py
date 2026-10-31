#!/usr/bin/env python3
"""Bind learned Poison Explosion saved clouds and retained shared mechanics."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/poison-explosion'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/poison-nova-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)-set(before)=={'src/Sanctuary.Content/PoisonCloudGeometry.cs','tests/Sanctuary.Integration/PoisonExplosionChecks.cs'}
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/NecromancerCombat.cs','src/Sanctuary.Content/NecromancerSpellMissiles.cs','src/Sanctuary.Content/MissileFootprints.cs','src/Sanctuary.Content/PlagueJavelins.cs','src/Sanctuary.Content/SkillBindings.cs','tests/Sanctuary.Integration/IronGolemCastChecks.cs'}
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/poison-nova/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'poison-explosion-check':1,'poison-explosion-reload-check':2,'poison-explosion-right-check':3,'poison-explosion-right-result':3,'poison-explosion-quick-check':3,'poison-explosion-quick-result':3,'plague-javelin-check':3})
    assert sum(expected.values())==265 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'poison-explosion-fixtures.json').read_text());flights=saved['Clouds']
    assert saved['Generation']==2 and len(flights)==8 and saved['Corpse']>0 and saved['Gold']>0
    assert {x['Cast'] for x in flights}=={0} and [x['RadialIndex'] for x in flights]==list(range(8))
    for x in flights:
        assert x['Owner']==saved['Owner'] and x['Frame']==19 and x['Frames']==60 and x['ElementFrames']==50
        assert x['Flags']==23 and x['VelocityFp8']==192 and x['Fault'] is None and x['Guided'] is None
    source_proofs={}
    for name in ['poison-explosion-emitter-source-evidence.json','poison-explosion-damage-source-evidence.json']:
        path=ROOT/'tools/reference'/name;proof=json.loads(path.read_text())
        for field in ['inputs','artifacts']:
            for p,h in proof.get(field,{}).items():assert sha(ROOT/p)==h,p
        source_proofs[name]=dict(path=str(path.relative_to(ROOT)),sha256=sha(path))
    assert json.loads((ROOT/'tools/reference/poison-explosion-emitter-source-evidence.json').read_text())['emissionCases']==432
    assert json.loads((ROOT/'tools/reference/poison-explosion-damage-source-evidence.json').read_text())['cases']==12240
    iron=json.loads((STAGE/'iron-golem-cast-fixtures.json').read_text())
    assert iron['RawMana'] is not None and 0<=iron['RawMana']-iron['Mana']<=255
    assert iron['RawPendingMana'] is not None and 0<=iron['RawPendingMana']-iron['PendingMana']<=255
    for kind in ['content','integration']:
        log=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(x in log for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,size in [('named-item-runtime-audit.json',829),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/poison-nova'/name).read_text())
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
    paths=[previous,ROOT/'tools/reference/poison-spell-potion-deviation.json',ROOT/'tools/build_class_skills.py',ROOT/'tools/probe_poison_explosion_damage.py',ROOT/'tools/probe_poison_explosion_emitter.py',Path(__file__).resolve()]
    paths += [ROOT/p for p in sorted(changed|set(after)-set(before))]
    paths += [ROOT/p for p in ['tests/check_poison_explosion.py','docs/poison-explosion-checkpoint.md','data/Sanctuary/Balance/necromancer-summons.json','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/poison-nova-source-preparation.md','docs/t29-combat-potions.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','poison-explosion-fixtures.json','iron-golem-cast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Learned Poison Explosion saved moving clouds',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=265,retainedPredecessorMarkers=247,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[829,21],unchangedInstalledArtifacts=106,sourcePacketContextsPerBoot=12240,sourceEmitterContexts=432,sourceDo3Contexts=6,sourceAllocationProjectionsPerBoot=144,clouds=8,plagueJavelinChecks=3,retainedIronGolemRawManaSaveLoad=True,actualRightQuickCasts=6,finalSavedGeneration=2,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},sourceProofs=source_proofs,
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after)-set(before))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Learned Poison Explosion creates8saved corpse-origin clouds,using the same16-offset emission geometry as Plague Javelin with mainStep2/subStep0/loops0. It replaces the stationary radius2field. Flags0x17 supply Param1<<7=256raw velocity,resulting192FP8 or96under50percent Slow Missiles. Clouds live60frames independently of rank-scaled poison duration.',
            'Complete source do63/emitter/factory execute432contexts,144allocation attempts and72created clouds,including all-failed/alternating allocation and corpse consumption guards. Six complete do3wrapper controls establish its conditional0x40mask and collision delegation. Complete source poison packet fixtures compare12240contexts per native boot,including50+10*(rank-1)duration,15percent hard synergies,mastery and item/critical exclusion.',
            'Shared saved flights supply Size2contact,non-killing collision,no NextHit and only the latest collider identity. Actual travel precedes distant contact;A/B/Arecipient changes are permitted. Cloud Collision1footprints project from saved moving actors. Very slow cloud movement reuses Plague tile-fit plus existing LOS instead of passing a blocking endpoint tile.',
            'Actual learned casting pays8mana once,consumes/reserves the corpse once and leaves273gold lootable. Right-only/quick inputs,unpaid/RIP refusal,soft-rank/duration capture,8lifecycle/terrain/slow controls,immune/late-potion/cure ticks and atomic malformed saves pass. Existing older-source initial-rate bridge is retained explicitly.',
            'The retained Iron Golem fixture now compares raw serialized mana at WorldLoad,where the temporary65000resource ceiling intentionally precedes final caps,then keeps the usable post-load bounds. Ownership/gear/life/seed/pending/binding guards remain;production resource math is unchanged.',
            'Two actual reloads preserve8clouds advanced17to18to19frames,their vectors,source,rate,poison length,seed,latest collision,deadlines,corpse NoSelect/Consumed/Animated flags,loot and bindings. Six connected right/quick casts use actual corpse packets. All247predecessor markers and3fresh Plague Javelin source/emission/resource suites pass.'],
        remaining=['Complete current path/dispatcher/events,terrain/candidate/footprint/global-frame/FCR/graphical-client parity and corpse eligibility/destruction/animation remain declared shared services. Legacy do63missing-skill-record typo is outside the compiled domain;the native route requires pinned data.',
            'Modern initial poison-rate health delivery,full PvP/player floor versus lethal elemental bonus,cross-owner/retired-source credit,dynamic poison defenses,removed buff history,extreme domains and every cure producer remain shared gaps. Source wrapper collision delegation is not a new complete generic dispatcher proof.',
            'No complete class,deployment,old-content binary upgrade or new broad1180-marker run is claimed. Next:remaining Necromancer summon/curse acceptance. Final poison corpse/cloud/puff/state effects/audio and talent/right/quick icons remain separately pending.'])
    destination=ROOT/'tools/reference/poison-explosion-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Poison Explosion:265markers/3boots/2reloads,12240source packets,8saved clouds,6real inputs,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
