#!/usr/bin/env python3
"""Bind the isolated poison-spell scheduler foundation; no learned-skill claim."""
import argparse,json
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/spell-poison-ticks'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={k:inventory.validate_inventory(STAGE/(k+'-inputs.json')) for k in ['birth','audit']}
    for kind,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(kind+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts']
        assert value['copiedData']==inputs['birth']['copiedData']
    previous=ROOT/'tools/reference/bone-spirit-native-evidence.json'
    predecessor=json.loads(previous.read_text())
    ref=predecessor['inputInventories']['birth']
    assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    assert set(before)<=set(after)
    assert set(after)-set(before)=={'src/Sanctuary.Content/SpellPoisonTicks.cs','tests/Sanctuary.Integration/SpellPoisonTickChecks.cs'}
    changed={p for p in before if before[p]!=after[p]}
    assert changed=={'src/Sanctuary.Content/TimedEffects.cs','src/Sanctuary.Content/PoisonStates.cs'}
    assert inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    baseline=ROOT/'.local/class-skills/bone-spirit/birth-native-checks.log'
    assert sha(baseline)==predecessor['artifacts'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)+Counter({'spell-poison-ticks-check':1,'spell-poison-ticks-reload-check':2})
    assert sum(expected.values())==232 and markers(STAGE/'birth-native-checks.log')==expected
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    saved=json.loads((STAGE/'spell-poison-ticks-fixtures.json').read_text())
    assert saved['Generation']==2 and len(saved['Targets'])==2
    for i,row in enumerate(saved['Targets']):
        state=row['State'];exposure=state['SpellPoison']
        assert row['Balance']==50000*256-2*(768 if i==0 else 3584)
        assert exposure['Source']==saved['Source'] and exposure['RawRate256']==2560
        assert exposure['Rate256']==(0 if i==0 else 2560) and exposure['ElapsedMs']==17 and exposure['RemainingMs']==1903
        if i==0:assert state['PoisonMs']==0 and state['PoisonClock'] is None
        else:assert state['PoisonMs']==1903 and state['PoisonClock']=={'Rate256':2560,'ElapsedMs':17}
    for kind in ['content','integration']:
        log=(STAGE/f'{kind}-build-{build}.log').read_text()
        assert all(x in log for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,size in [('named-item-runtime-audit.json',828),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/bone-spirit'/name).read_text())
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
    paths=[previous,ROOT/'tools/reference/poison-spell-potion-deviation.json',Path(__file__).resolve()]
    paths += [ROOT/p for p in sorted(changed|set(after)-set(before))]
    paths += [ROOT/p for p in ['tests/check_spell_poison_ticks.py','docs/spell-poison-ticks-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/poison-nova-source-preparation.md','docs/t29-combat-potions.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','spell-poison-ticks-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Shared pure-poison spell tick foundation',installed=False,learnedPoisonNovaAccepted=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=232,retainedPredecessorMarkers=229,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[828,21],unchangedInstalledArtifacts=106,savedExposureKinds=['poison immune','ordinary poison'],savedPhaseMs=17,finalSavedGeneration=2,handlers=186,necromancerHandlers=30),
        inputInventories={k:dict(path=str((STAGE/(k+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(k+'-inputs.json'))) for k in inputs},
        changedSourceHashes={p:after[p] for p in sorted(changed|set(after)-set(before))},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['The existing40ms effect scheduler carries separately saved spell exposure without a positive poison rate on immune targets. Such an exposure leaves poison status and natural regeneration unmasked.',
            'Tick-time potion eligibility,late drinking/removal/expiry,independent elemental resistance/masteries,combined original+added poison rounding,cure/cleansing,strongest poison replacement,nonspell replacement and deleted-source bonus cessation pass.',
            'Base poison and elemental additions share one health payment. Native kernel controls verify Protection once for8base+6bonus and NPC item replenishment before a lethal aggregate. Combined tick death/loot credit bypasses ordinary damage callbacks.',
            'Actual two-reload saves preserve both ordinary and zero-rate exposures,including17ms phase,original rate,remaining duration,source and exact health. Missing legacy JSON fields default to no exposure;malformed state restores atomically.'],
        remaining=['This helper is not yet connected to learned Poison Nova or Poison Explosion. Their provisional handlers are unchanged;mechanical completion and deployment are not claimed.',
            'Next:connect Nova64saved source rays,poison packets and this tick lane;verify actual learned right/quick inputs,payment,collision/NHD,initial health delivery and lifecycle. Explosion cloud allocation follows.',
            'Full PvP/player poison-floor versus lethal elemental bonus composition,extreme combined-rate domains,dynamic poison defenses,already-removed buff history,serial retirement and every cure producer remain unverified. Existing friendly-player policy is retained. Immune exposure keeps normal regeneration ordering;no new simultaneous-regeneration rule is invented.',
            'No new broad1180-marker run is claimed for this foundation. The prior full-class regression belongs to the Bone Spirit checkpoint. All229focused predecessor markers are retained. Final effects/audio and talent/hotkey icons remain separate pending assets.'])
    destination=ROOT/'tools/reference/spell-poison-ticks-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert destination.read_text()==text
    else:destination.write_text(text)
    print('Spell poison tick foundation:232markers/3boots/2reloads,4audits/71Python;learned Nova pending;installed unchanged.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--build',type=int,required=True);parser.add_argument('--run',type=int,required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();record(args.build,args.run,args.check)
