#!/usr/bin/env python3
"""Record exact prepared Raven body/component acceptance without enabling learned casting."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'raven'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    inputs=json.loads((STAGE/'stats-inputs.json').read_text())
    for name,digest in inputs['sourceInputs'].items():assert sha(ROOT/name)==digest,name
    for name,digest in inputs['copiedData'].items():assert sha(ROOT/'data/Sanctuary'/name.removeprefix('Data/Sanctuary/'))==digest,name
    for kind in ['content','integration']:
        name='Sanctuary.'+kind.title()+'.dll'
        assert sha(STAGE/kind/name)==inputs['artifacts']['Assemblies/'+name]
    log=(STAGE/'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    for name in ['raven-stats-check','raven-flight-check','toward-path-check','valkyrie-creation-check','named-gear-check']:
        assert log.count(name+': passed')==1,name
    assert '13770source damage/flatAR,4590explicit-level births,12800signed blind cases and actual NPC blind controls pass' in log
    assert '6480complete source contexts and61722ordered collision queries match' in log
    for name in ['raven-body', 'summon-difficulty', 'class-history', 'full-wardrobe']:
        assert log.count(name+'-check: passed')==1,name
        assert log.count(name+'-reload-check: passed')==2,name
    assert log.count('raven-body-live-result: passed')==3
    assert log.count('Writing world save snapshot done')==3
    assert log.count('Shutting down')==3
    assert 'installed Armageddon untouched'  in (BASE/'raven-stats-run.log').read_text()
    source=json.loads((ROOT/'tools/reference/raven-components-source-evidence.json').read_text())
    assert source['fixture']['damage']==13770 and source['fixture']['birth']==4590 and source['fixture']['blind']==12800
    assert sha(ROOT/source['fixture']['path'])==source['fixture']['sha256']
    for name,digest in source['generatedSourceHashes'].items():assert sha(ROOT/name)==digest,name
    for name,digest in source['sourceHashes'].items():assert sha(BASE/'d2moo-pinned-source'/name)==digest,name
    circle=json.loads((ROOT/'tools/reference/raven-circle-source-evidence.json').read_text())
    assert circle['fixture']['contexts']==6480 and sha(ROOT/circle['fixture']['path'])==circle['fixture']['sha256']
    assert sha(BASE/'raven-circle-source-probe.cpp')==circle['generatedSourceSha256']
    assert sha(BASE/'toward-path-source-probe.cpp')==circle['parentCppSha256']
    for name,digest in circle['sourceHashes'].items():assert sha(BASE/'d2moo-pinned-source'/name)==digest,name
    coverage=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    present=[x for x in coverage['skills'] if x['implementation']=='handler-present']
    assert len(present)==165 and sum(x['characterClass']=='Druid' for x in present)==9
    assert not coverage['completedClasses']
    builds={}
    for name in ['content','integration']:
        text=(BASE/('raven-'+name+'-build.log')).read_text()
        assert 'Build succeeded.' in text and '0 Error(s)' in text
        builds[name]=int(re.findall(r'(\d+) Warning\(s\)',text)[-1]);assert builds[name]==0
    paths=[ROOT/p for p in ['src/Sanctuary.Content/DruidRavenStats.cs','src/Sanctuary.Content/DruidRavenBody.cs','src/Sanctuary.Content/DruidRavenMotion.cs',
        'src/Sanctuary.Content/PersistentMinions.cs','src/Sanctuary.Content/SkeletonPets.cs','src/Sanctuary.Content/NecromancerSummons.cs',
        'tests/Sanctuary.Integration/RavenBodyChecks.cs','tests/Sanctuary.Integration/ClassHistoryChecks.cs','tests/Sanctuary.Integration/FullWardrobeChecks.cs','src/Sanctuary.Content/ValkyrieCreation.cs',
        'src/Sanctuary.Content/NamedControl.cs','src/Sanctuary.Content/SourceActorMotion.cs','src/Sanctuary.Content/SourceActorPath.cs',
        'src/Sanctuary.Content/SourceTowardPath.cs','tests/Sanctuary.Integration/RavenStatsChecks.cs','tests/Sanctuary.Integration/RavenFlightChecks.cs','tests/check_raven_stats.py',
        'tools/build_class_skills.py','tools/prepare_raven_reference.py','tools/probe_raven_components_reference.py',
        'tools/record_raven_stats.py','tools/probe_raven_circle_reference.py','tools/reference/raven-circle-source-evidence.json',
        'tools/reference/raven-preflight.json','tools/reference/raven-components-source-evidence.json',
        'data/Sanctuary/Balance/necromancer-summons.json','data/Sanctuary/Balance/summon-level-stats.json']]
    paths += [STAGE/p for p in ['content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll','stats-inputs.json','stats-native-checks.log','body-fixtures.json']]
    paths += [BASE/p for p in ['raven-content-build.log','raven-integration-build.log','raven-stats-run.log','raven-components-source-probe.json']]
    for name in ['raven-components','raven-base']:
        paths += [BASE/(name+s) for s in ['-source-probe.cpp','-source-probe','-sanitizer.log']]
    paths += [BASE/p for p in ['raven-circle-source-probe.cpp','raven-circle-source-probe','raven-circle-source-probe.json',
        'raven-circle-sanitizer.log','toward-path-source-probe.cpp','toward-path-source-probe.json']]
    evidence=dict(referenceVersion='3.3.93847',handlerEnabled=False,installed=False,handlers=165,druidHandlers=9,
        completedClasses=[],goalComplete=False,scope='Prepared actual Raven body, numeric/blind/path and lifecycle consumers under an explicitly unverified legacy AI timing profile; learned casting, full current D2R parity and installation remain pending.',
        timingProfile='legacy-npc-clock-current-hk-metadata-v1',boots=3,actualReloads=2,connectedNaturalLifetimes=3,buildWarnings=builds,sourceAndNativeDamageCases=13770,sourceAndNativeBirthCases=4590,
        sourceAndNativeBlindCases=12800,sourceAndNativeCircleCases=6480,circleCollisionQueries=61722,
        retainedTowardCases=6480,retainedTowardCollisionQueries=62880,hashes={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=[
            'Actual DruidRavenStats endpoints and flat attack-mode AR match13770complete physical/toht source captures/all255ranks, independent hard wolf/bear synergies and unrelated item-stat controls. Birth stats match4590complete explicit-level base captures across caster/rank/difficulty/game contexts; level remains above99 while only monlvl lookup clamps.',
            'Shared ValkyrieCreation.BaseAtLevel serves both default and explicit-level summons without duplicating its111-row data. Retained native Valkyrie creation comparisons and real gear/snapshot controls pass.',
            'NamedControl.BlindLevel matches12800complete signed event09 cases, including-1. Actual controlled NPC proxy blind disables special attacks without lowering health; zero stat draws none and downstream protected-pet refusal still consumes its source draw. Existing player positive-only behavior and retained named-item controls pass.',
            'Actual Druid owner and Tarnhelm change prepared effective rank/pet level while prior immutable birth stats stay captured. Nonzero inherited physical sunder is explicitly refused pending its missing consumer. Current Raven monster/property data are exported from pinned tables and the recorded local monprop fact.',
            'Existing SourceTowardPath now supports source+2/-2circle offsets for path5/6. All6480complete source contexts and61722ordered collision queries match, including exact failures/duplicate points/vector/flags/target state and JSON identity. Completed circle paths do not request a target repath. Existing6480toward contexts/62880queries and old path15 snapshots remain unchanged.'
            ,'Actual DruidRavenBody suppresses native skeleton attacks/regeneration, preserves source first-think/attempt budget and separate birth/live attack packet, applies real damage and signed blind, follows/circles and retires after finite attempts in the declared timing profile. Tests include whole-flock explicit focus, five-pet FIFO, Tarnhelm mode snapshot/live-next-mode updates, offline pending-attack cancellation, wardrobe parking, facet callbacks, orphan cleanup, malformed atomic restore and array isolation.',
            'Three disposable boots/two actual world reloads retain owner/body/birth/seed/budget/clocks/parking and resume without offline attacks. Three connected real-time bodies exhaust12rank1attempts and damage actual mapped NPCs. Retained summon difficulty/class history/full wardrobe/Valkyrie/named gear tests pass. Two obsolete fixture constants now derive the class count from the eight-class enum; production history format is unchanged.'
        ],limits=[
            'Learned Raven casting remains disabled. The prepared factory is tested on a real Raven subclass, but this is not a whole-skill or class parity certificate. Historical2.4AI changes prevent treating the inspected legacy schedule plus current HK animation data as proven current timing.',
            'Legacy source services, scoped RNG, current-executable event/target/animation behavior and exact modern blind/first-think budget semantics remain bounded or unverified. Source callback records requested blind rank; native tests separately cover supported living NPC eligibility.',
            'Circle proof inherits the reconstruction marked BROKEN for the general initializer and its bounded room/grid/zero-footprint point-target services. Two compiler pointer-width warnings occur in unused unit-target branches; those branches, unit-target repathing and full room recache are not established by these captures.',
            'Fresh full-class regression, current audit exports/70Python suite, learned casting/payment/right/quick actual input acceptance and installation remain pending. Current actor scope includes three connected natural lifetimes and two actual reloads; the existing main Armageddon installation is unchanged.',
            'Native map/LOS/nearest mapped-NPC targeting, point-target zero-footprint flight/warp, scoped RNG and million-step hit roll/whole-life damage projections are explicit adapters. Moving-unit path repair, legacy owner-vision scan side effects, PvP/unmapped source-size targets and nonzero inherited physical sunder remain unsupported or unverified.',
            'Final summon/flight/attack/blind/retire/audio effects and separate talent/right/quick icons remain identified inventory placeholders.'
        ],next='Connect once-only6mana learned Raven SC/right/quick casting and confirmed whole-flock focus; verify actual input, cancellation, resource/cap and lifecycle edges. Extend point-path repair and retain explicit current-build timing/target/geometry uncertainties; refresh audits/full regression before installing.')
    out=ROOT/'tools/reference/raven-stats-native-evidence.json';text=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print('Prepared Raven body/components:37640source captures,3boots/2reloads/3natural connected lifetimes and retained summon/wardrobe/item consumers; learned handler and installation still pending.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
