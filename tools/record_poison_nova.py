#!/usr/bin/env python3
"""Bind learned Poison Nova saved rays and the shared poison tick scheduler."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/poison-nova'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/spell-poison-ticks-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)-set(before)=={'tests/Sanctuary.Integration/PoisonNovaChecks.cs'}
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/NecromancerCombat.cs','src/Sanctuary.Content/NecromancerSpellMissiles.cs','src/Sanctuary.Content/SpellPoisonTicks.cs','src/Sanctuary.Content/SkillBindings.cs'}
    data='Data/Sanctuary/Balance/necromancer-summons.json'
    assert {k:v for k,v in inputs['birth']['copiedData'].items() if k!=data}=={k:v for k,v in old['copiedData'].items() if k!=data}
    import subprocess
    before_rows=json.loads(subprocess.check_output(['git','show','eff6e7a:data/Sanctuary/Balance/necromancer-summons.json'],cwd=ROOT))
    rows=json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())
    assert [x for x in rows if x['key']!='poisonnova']==before_rows and sum(x['key']=='poisonnova' for x in rows)==1
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/spell-poison-ticks/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'poison-nova-check':1,'poison-nova-reload-check':2,'poison-nova-right-check':3,'poison-nova-right-result':3,'poison-nova-quick-check':3,'poison-nova-quick-result':3})
    assert sum(expected.values())==247 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'poison-nova-fixtures.json').read_text());flights=saved['Flights']
    assert saved['Generation']==2 and len(flights)==64 and saved['Balance']==50000*256
    assert len({x['Cast'] for x in flights})==1 and [x['RadialIndex'] for x in flights]==list(range(64))
    for x in flights:
        assert x['Owner']==saved['Owner'] and x['Frame']==4 and x['Frames']==30 and x['ElementFrames']==50
        assert x['Flags']==7 and x['VelocityFp8']==2304 and x['Fault'] is None and x['Guided'] is None
    source_proofs={}
    for name in ['poison-nova-emitter-source-evidence.json','poison-nova-damage-source-evidence.json','poison-nova-routing-review.json']:
        path=ROOT/'tools/reference'/name;proof=json.loads(path.read_text())
        for field in ['inputs','artifacts']:
            for p,h in proof.get(field,{}).items():assert sha(ROOT/p)==h,p
        source_proofs[name]=dict(path=str(path.relative_to(ROOT)),sha256=sha(path))
    assert json.loads((ROOT/'tools/reference/poison-nova-emitter-source-evidence.json').read_text())['cases']==72
    assert json.loads((ROOT/'tools/reference/poison-nova-damage-source-evidence.json').read_text())['cases']==12240
    for kind in ['content','integration']:
        log=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(x in log for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,size in [('named-item-runtime-audit.json',828),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/spell-poison-ticks'/name).read_text())
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
    paths=[previous,ROOT/'tools/reference/poison-spell-potion-deviation.json',ROOT/'tools/build_class_skills.py',Path(__file__).resolve()]
    paths += [ROOT/p for p in sorted(changed|set(after)-set(before))]
    paths += [ROOT/p for p in ['tests/check_poison_nova.py','docs/poison-nova-checkpoint.md','data/Sanctuary/Balance/necromancer-summons.json','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/poison-nova-source-preparation.md','docs/t29-combat-potions.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','poison-nova-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Learned Poison Nova saved source ring and poison ticks',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=247,retainedPredecessorMarkers=232,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[828,21],unchangedInstalledArtifacts=106,sourcePacketContextsPerBoot=12240,sourceEmitterContexts=72,sourceDirectionsComparedPerBoot=384,radialRays=64,actualRightQuickCasts=6,finalSavedGeneration=2,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},sourceProofs=source_proofs,
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after)-set(before))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Poison Nova now creates64saved source-directed child missiles,using FrozenOrbs exact integer offsets and the shared factory/Q12/source-cell/do1 controller. It no longer damages a growing filled area. Each ray has30frames,2304FP8velocity,Size1,contact removal and explicit cast-local160ms NextHit adapter.',
            'Complete pinned source fixtures compare12240poison-rate/length/mastery packets and384direction/factory projections per boot. Source-zero excludes weapon/item poison and critical;50frames of poison are separate from the30-frame flight. Independent captured damage slow is retained while CanSlow0 preserves movement velocity.',
            'Learned self-origin windup pays20mana once at release. Right-only and quick bindings,unpaid/town release,real Wormskull soft rank/item-poison exclusion,captured gear,interception and offline/facet/death/respec/wardrobe/wall controls pass. Six actual connected right/quick casts reach enemies through natural flight.',
            'Actual contact applies the verified shared poison/exposure scheduler. Ordinary and immune recipients pass initial-rate bridge,no GetHit recovery,late-potion ticks and cure cleanup. The existing older-source initial health bridge is retained explicitly;modern equivalence remains open.',
            'Two actual reloads preserve all64midflight rays with shared cast identity,ordinal,vector,frame,packet,seed,source,deadline and poison length. All232predecessor markers,including positive/immune poison tick saves and legacy profiles,remain.'],
        remaining=['Exact modern NextHit cast/caster identity remains unverified;the explicit cast-local adapter is not claimed as the modern global registry. Complete current path/dispatcher/events,room/terrain/candidate/footprint/global frame/FCR/graphical-client parity and special recipients remain open.',
            'Modern initial poison-rate health delivery still needs confirmation. Full PvP/player poison-floor versus lethal elemental additions,cross-owner/retired-source credit,dynamic poison defenses,removed buff history,extreme domains and every cure producer remain shared gaps. Town release refusal is verified;full native animation/selection parity is not claimed.',
            'No class completion,deployment,old-content binary upgrade or new broad1180-marker run is claimed. Next:Poison Explosion eight moving cloud children,then remaining summon/curse acceptance. Final poisonNova effects/audio and talent/hotkey icons remain separately pending.'])
    destination=ROOT/'tools/reference/poison-nova-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Poison Nova:247markers/3boots/2reloads,12240source packets,64saved rays,6real inputs,4audits/71Python;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
