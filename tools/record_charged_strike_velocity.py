#!/usr/bin/env python3
"""Bind captured Charged Strike speed, mixed flight saves and retained regressions."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/charged-strike-velocity'
PARENT='bd9e687b51a3a2cf27453724601ece3529861e93'
sha=inventory.sha


def load(path):return json.loads(path.read_text())


def historical(path):return json.loads(subprocess.check_output(['git','show',PARENT+':'+path],cwd=ROOT))


def record(check=False):
    inventory.STAGE=STAGE
    native,audit=[inventory.validate_inventory(STAGE/name)for name in('birth-inputs.json','audit-inputs.json')]
    for name in('birth-inputs.json','audit-inputs.json'):snapshot(STAGE/'pre-build-2.json',STAGE/name)
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    before_compile=load(STAGE/'pre-build-1.json')['inputs'];after_compile=load(STAGE/'pre-build-2.json')['inputs']
    for key in('projectInputs','copiedData'):assert before_compile[key]==after_compile[key]
    assert {p:h for p,h in before_compile['sourceInputs'].items()if not p.startswith('tests/')}=={p:h for p,h in after_compile['sourceInputs'].items()if not p.startswith('tests/')}
    assert {p for p in before_compile['sourceInputs']if before_compile['sourceInputs'][p]!=after_compile['sourceInputs'][p]}=={'tests/Sanctuary.Integration/ChargedStrikeVelocityChecks.cs'}
    old_path=ROOT/'tools/reference/necro-legacy-army-evidence.json';old=load(old_path)
    parent_snapshot=ROOT/'.local/class-skills/necro-legacy-army/pre-build-5.json'
    assert sha(parent_snapshot)==old['evidence'][str(parent_snapshot.relative_to(ROOT))]
    baseline=load(parent_snapshot)['inputs']
    sources={'src/Sanctuary.Content/AmazonMelee.cs','src/Sanctuary.Content/ChargedStrikeBolts.cs',
        'tests/Sanctuary.Integration/ChargedStrikeChecks.cs','tests/Sanctuary.Integration/ChargedStrikeVelocityChecks.cs'}
    assert {p for p in set(baseline['sourceInputs'])|set(after_compile['sourceInputs'])if baseline['sourceInputs'].get(p)!=after_compile['sourceInputs'].get(p)}==sources
    assert baseline['projectInputs']==after_compile['projectInputs']
    assert {p for p in baseline['copiedData']if baseline['copiedData'][p]!=after_compile['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for name in('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert native['artifacts'][name]==old['currentArtifacts'][name]==sha(STAGE/'native/Distribution'/name)
    status_path='data/Sanctuary/Balance/class-skill-status.json';a=historical(status_path);b=load(ROOT/status_path)
    assert {k:v for k,v in a.items()if k!='skills'}=={k:v for k,v in b.items()if k!='skills'}
    assert {x['talentId']:[key for key in x if x[key]!=y[key]]for x,y in zip(a['skills'],b['skills'],strict=True)if x!=y}=={'amazon.chargedstrike':['remaining']}
    assert not b['completedClasses']and sum(r['implementation']=='handler-present'for r in b['skills'])==191
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'weapon-sequence-rate-check':3,
        'jab-check':1,'jab-reload-check':2,'power-strike-check':1,'power-strike-reload-check':2,
        'charged-strike-check':1,'charged-strike-reload-check':2,'charged-strike-native-bolts':1,'charged-strike-expiry-start':1,
        'charged-strike-expiry':1,'charged-strike-result':1,'charged-strike-reload-result':2,
        'slow-missiles-component-check':1,'slow-missiles-component-reload-check':2,'slow-missiles-component-result':1,
        'charged-strike-velocity-check':1,'charged-strike-velocity-reload-check':2})
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==34
    focused=STAGE.with_name('charged-strike-velocity-smoke')
    checked=inventory.validate_inventory(focused/'birth-inputs.json');snapshot(STAGE/'pre-build-2.json',focused/'birth-inputs.json')
    assert checked['artifacts']==native['artifacts']and checked['copiedData']==native['copiedData']
    assert sum(markers(focused/'birth-native-checks.log').values())==12
    for folder in(STAGE,focused):
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and' ERR]'not in log
        assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
        saved=load(folder/'charged-strike-velocity-fixtures.json')
        assert saved['Generation']==2 and saved['Owner']>0 and saved['Weapon']>0 and saved['Life256']%256!=0 and saved['Mana256']>0
        assert len(saved['Flights'])==2
        modern=[f for f in saved['Flights']if f.get('FactoryMotion')is not None]
        legacy=[f for f in saved['Flights']if f.get('FactoryMotion')is None]
        assert len(modern)==len(legacy)==1 and modern[0]['FactoryMotion']==dict(VelocityFp8=1152,SlowVelocityPercent=50)
        assert 'FactoryMotion'not in legacy[0]and all(f['Owner']==saved['Owner']and f['Frame']>=3 for f in saved['Flights'])
    for name in('content-build-1.log','integration-build-2.log'):
        assert all(word in(STAGE/name).read_text()for word in('Build succeeded.','0 Warning(s)','0 Error(s)'))
    source_path=ROOT/'tools/reference/charged-strike-velocity-source-evidence.json';source=load(source_path)
    assert source['cases']==72 and source['compiledRoutines']==['MISSILES_CreateMissileFromParams','SKILLS_MissileInit_ChargedBolt']
    for path,digest in source['evidence'].items():assert sha(ROOT/path)==digest,path
    cases=load(ROOT/'.local/class-skills/charged-strike-velocity-source/cases.json')
    assert load(STAGE/'source-sanitized-cases.json')==cases and not(STAGE/'source-sanitizer.log').read_text()
    assert '72 complete factory/initializer compositions'in(STAGE/'source-probe.log').read_text()
    audits=[]
    for name,count in(('named-item-runtime-audit.json',1020),('first-loop-affix-audit.json',21)):
        fresh=load(STAGE/name);previous=historical('tools/reference/'+name)
        assert fresh==load(ROOT/'tools/reference'/name)and len(fresh['InputHashes'])==count
        assert fresh['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in fresh['InputHashes'].items():assert sha(ROOT/path)==digest,path
        skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in fresh.items()if k not in skip}=={k:v for k,v in previous.items()if k not in skip}
        audits.append(fresh)
    assert len(audits[0]['Definitions'])==573 and markers(STAGE/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    for name,word in(('python-tests.log','Ran 71 tests'),('readiness-check.log','573 definitions accounted for'),
        ('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','recorded separately')):
        assert word in(STAGE/name).read_text()
    assert '\nOK\n'in(STAGE/'python-tests.log').read_text()
    client_path=ROOT/'tools/reference/poison-corpse-aim-client-evidence.json';client=load(client_path);assert client['patches']==40
    for section in('patchHashes','sharedInputs'):
        for path,digest in client[section].items():assert sha(ROOT/path)==digest,path
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for key,relative,count in(('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installed['runtimeRoot'])/'.local/server'/relative
        assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    files=[Path(__file__).resolve(),old_path,parent_snapshot,source_path,client_path,
        *(ROOT/path for path in sources),ROOT/status_path,ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/probe_charged_strike_velocity.py',
        *(ROOT/'tests'/name for name in('check_iron_golem_birth.py','check_charged_strike_velocity.py','check_charged_strike_velocity_smoke.py')),
        *(ROOT/'docs'/name for name in('charged-strike-velocity-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in('pre-build-1.json','pre-build-2.json','content-build-1.log','integration-build-2.log','birth-inputs.json',
            'birth-native-checks.log','native-run-2.log','charged-strike-velocity-fixtures.json','audit-inputs.json','audit-native-checks.log','audit-run-1.log',
            'named-item-runtime-audit.json','first-loop-affix-audit.json','source-probe.log','source-sanitizer-build.log','source-sanitized-cases.json','source-sanitizer.log',
            'python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log')),
        *(focused/name for name in('birth-inputs.json','birth-native-checks.log','native-run-2.log','charged-strike-velocity-fixtures.json'))]
    for attempt in('attempt-1','attempt-2'):files +=[p for p in(STAGE/attempt).rglob('*')if p.is_file()]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Charged Strike released factory velocity capture with explicit missing-field flight compatibility',installed=False,goalComplete=False,completedClasses=[],
        builds=dict(content=1,integration=2),mainRun=2,focusedRun=2,auditRun=1,
        verified=dict(sourceFactoryInitializerCases=72,sourceSanitizersClean=True,affectedSystemMarkers=34,focusedMarkers=12,bootsPerCohort=3,
            actualReloadsPerCohort=2,worldSavesPerCohort=3,actualLearnedSpearEmission=True,slowAcquiredDuringWindup=True,normalSpeedUnchanged=True,
            normalVelocityFp8=2304,halfVelocityFp8=1152,zeroSpeedExpiry=True,capturedVelocitySurvivesCasterStateExpiry=True,
            missingFieldLegacyVelocityPreserved=True,mixedOriginalFlightsWoundsWeaponManaReloads=True,invalidMotionRestoreAtomic=True,
            pythonTests=71,auditFingerprints=[1020,21],namedDecisionsUnchanged=573,installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,
            clientPatchesUnchanged=40,handlers=191),artifacts=native['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in sorted(set(files))},
        limitations=['Source composition and declared factory/path/damage/stat services,not current D2R executable/full path or class parity.',
            'Prepared Slow Missiles receiver controls do not enable its unfinished learned skill or prove modern damage scaling/order. Only velocity is changed.',
            'Original host collision/activation/shotgun and legacy calc1 melee-ED overlap remain unverified for current D2R;normal paths,damage and payment stay unchanged.',
            'Held inspection flight deadlines plus ordinary lifetime controls,not unheld active-flight or actual old-writer Charged Strike migration. Missing motion fields explicitly retain old speed.',
            '34 affected-system/12 focused markers,not a fresh full-Amazon/all-class certificate. Native/client/installed build preserved;effects/audio and icons stay separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/charged-strike-velocity-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Charged Strike velocity:72 source compositions,34 affected-system/12 focused markers,two reloads each;installed build and saves preserved.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
