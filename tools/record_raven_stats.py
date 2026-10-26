#!/usr/bin/env python3
"""Record exact prepared Raven component acceptance without enabling a skill."""
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
    assert 'installed Armageddon build untouched' in (BASE/'raven-stats-run.log').read_text()
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
    paths=[ROOT/p for p in ['src/Sanctuary.Content/DruidRavenStats.cs','src/Sanctuary.Content/ValkyrieCreation.cs',
        'src/Sanctuary.Content/NamedControl.cs','src/Sanctuary.Content/SourceActorMotion.cs','src/Sanctuary.Content/SourceActorPath.cs',
        'src/Sanctuary.Content/SourceTowardPath.cs','tests/Sanctuary.Integration/RavenStatsChecks.cs','tests/Sanctuary.Integration/RavenFlightChecks.cs','tests/check_raven_stats.py',
        'tools/build_class_skills.py','tools/prepare_raven_reference.py','tools/probe_raven_components_reference.py',
        'tools/record_raven_stats.py','tools/probe_raven_circle_reference.py','tools/reference/raven-circle-source-evidence.json',
        'tools/reference/raven-preflight.json','tools/reference/raven-components-source-evidence.json',
        'data/Sanctuary/Balance/necromancer-summons.json','data/Sanctuary/Balance/summon-level-stats.json']]
    paths += [STAGE/p for p in ['content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll','stats-inputs.json','stats-native-checks.log']]
    paths += [BASE/p for p in ['raven-content-build.log','raven-integration-build.log','raven-stats-run.log','raven-components-source-probe.json']]
    for name in ['raven-components','raven-base']:
        paths += [BASE/(name+s) for s in ['-source-probe.cpp','-source-probe','-sanitizer.log']]
    paths += [BASE/p for p in ['raven-circle-source-probe.cpp','raven-circle-source-probe','raven-circle-source-probe.json',
        'raven-circle-sanitizer.log','toward-path-source-probe.cpp','toward-path-source-probe.json']]
    evidence=dict(referenceVersion='3.3.93847',handlerEnabled=False,installed=False,handlers=165,druidHandlers=9,
        completedClasses=[],goalComplete=False,scope='Prepared Raven numeric, signed-blind and circle-path consumers only; no Raven actor, learned casting, AI or installation is claimed.',
        boots=1,actualReloads=0,buildWarnings=builds,sourceAndNativeDamageCases=13770,sourceAndNativeBirthCases=4590,
        sourceAndNativeBlindCases=12800,sourceAndNativeCircleCases=6480,circleCollisionQueries=61722,
        retainedTowardCases=6480,retainedTowardCollisionQueries=62880,hashes={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=[
            'Actual DruidRavenStats endpoints and flat attack-mode AR match13770complete physical/toht source captures/all255ranks, independent hard wolf/bear synergies and unrelated item-stat controls. Birth stats match4590complete explicit-level base captures across caster/rank/difficulty/game contexts; level remains above99 while only monlvl lookup clamps.',
            'Shared ValkyrieCreation.BaseAtLevel serves both default and explicit-level summons without duplicating its111-row data. Retained native Valkyrie creation comparisons and real gear/snapshot controls pass.',
            'NamedControl.BlindLevel matches12800complete signed event09 cases, including-1. Actual controlled NPC proxy blind disables special attacks without lowering health; zero stat draws none and downstream protected-pet refusal still consumes its source draw. Existing player positive-only behavior and retained named-item controls pass.',
            'Actual Druid owner and Tarnhelm change prepared effective rank/pet level while prior immutable birth stats stay captured. Nonzero inherited physical sunder is explicitly refused pending its missing consumer. Current Raven monster/property data are exported from pinned tables and the recorded local monprop fact.',
            'Existing SourceTowardPath now supports source+2/-2circle offsets for path5/6. All6480complete source contexts and61722ordered collision queries match, including exact failures/duplicate points/vector/flags/target state and JSON identity. Completed circle paths do not request a target repath. Existing6480toward contexts/62880queries and old path15 snapshots remain unchanged.'
        ],limits=[
            'The controlled SkeletonGuardian used for blind is an explicitly named component proxy, not a playable Raven. This one-boot check does not establish Raven flight, AI/casting, natural expiry/exhaustion or persisted actor state.',
            'Legacy source services, scoped RNG, current-executable event/target/animation behavior and exact modern blind/first-think budget semantics remain bounded or unverified. Source callback records requested blind rank; native tests separately cover supported living NPC eligibility.',
            'Circle proof inherits the reconstruction marked BROKEN for the general initializer and its bounded room/grid/zero-footprint point-target services. Two compiler pointer-width warnings occur in unused unit-target branches; those branches, unit-target repathing and full room recache are not established by these captures.',
            'Fresh full-class regression, current audit exports/70Python suite, actual input/timers/two-reload actor acceptance and installation remain pending for the future Raven candidate. The existing main Armageddon installation is unchanged.',
            'Final summon/flight/attack/blind/retire/audio effects and separate talent/right/quick icons remain identified inventory placeholders.'
        ],next='Connect saved Raven ownership/cap/flight/AI/attack mode and learned casting using these components; preserve explicit current-build uncertainties and approved facet/minion policies.')
    out=ROOT/'tools/reference/raven-stats-native-evidence.json';text=json.dumps(evidence,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print('Prepared Raven components verified against37640source captures plus real gear/NPC blind/circle paths and retained consumers; no learned handler or installation.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
