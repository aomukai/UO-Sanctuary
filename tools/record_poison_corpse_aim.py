#!/usr/bin/env python3
"""Bind shared explosion corpse targeting, original poison behavior and client routing."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/poison-corpse-aim'
PARENT='a8b6b0443fe517d3c3c6e579ee59eb0584cf6941'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE
    paths=[STAGE/name for name in ('birth-inputs.json','audit-inputs.json')]
    native,audit=[inventory.validate_inventory(path)for path in paths]
    for path in paths:snapshot(STAGE/'pre-build-1.json',path)
    assert native['artifacts']==audit['artifacts']and native['copiedData']==audit['copiedData']
    previous=STAGE.with_name('necro-town');prior=json.loads((previous/'birth-inputs.json').read_text())
    sources={'src/Sanctuary.Content/'+name+'.cs'for name in ('CorpseCastTargets','CorpseExplosionCasts','NecromancerCombat')}
    sources.add('tests/Sanctuary.Integration/PoisonCorpseAimChecks.cs')
    assert {path for path in set(native['sourceInputs'])|set(prior['sourceInputs'])if path.endswith('.cs')and native['sourceInputs'].get(path)!=prior['sourceInputs'].get(path)}==sources
    assert native['copiedData']==prior['copiedData']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/necro-town-native-evidence.json'],cwd=ROOT))
    oldlog=previous/'birth-native-checks.log';assert sha(oldlog)==parent['evidence'][str(oldlog.relative_to(ROOT))]
    expected=markers(oldlog);assert sum(expected.values())==99
    for family in ('poison-explosion','poison-corpse-aim'):expected[family+'-check']+=1;expected[family+'-reload-check']+=2
    for command in ('poison-explosion-right','poison-explosion-quick','poison-ground-right','poison-ground-quick'):
        expected[command+'-check']+=3;expected[command+'-result']+=3
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==129
    smoke=STAGE.with_name('poison-corpse-aim-smoke');smoked=inventory.validate_inventory(smoke/'birth-inputs.json');snapshot(STAGE/'pre-build-1.json',smoke/'birth-inputs.json')
    assert smoked['artifacts']==native['artifacts']and smoked['copiedData']==native['copiedData']and sum(markers(smoke/'birth-native-checks.log').values())==39
    for folder in (STAGE,smoke):
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'poison-corpse-aim-fixtures.json').read_text());assert saved['Generation']==2 and saved['Owner']>0 and saved['Corpse']>0 and saved['Mana']>0
        assert saved['Row']['Corpse']==saved['Corpse']and saved['Row']['BaseLife']['Seed']>0 and not saved['Row']['NoSelect']
        clouds=json.loads((folder/'poison-explosion-fixtures.json').read_text());assert clouds['Generation']==2 and len(clouds['Clouds'])==8
        for command in ('poison-explosion-right','poison-explosion-quick','poison-ground-right','poison-ground-quick'):assert log.count(command+'-result: passed')==3
    source=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';manifest=ROOT/'tools/reference/d2r-named-equipment/sources.json'
    assert sha(source)==json.loads(manifest.read_text())['files']['json/skills.json']['sha256']
    raw=json.loads(source.read_text());fields={'SearchEnemyNear':1,'SelectProc':1,'TargetCorpse':1,'srvstfunc':17,'cltstfunc':21}
    for source_id in (74,83):assert all(raw[str(source_id)].get(key)==value for key,value in fields.items())
    catalog=json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text())
    for row in catalog:
        if row['sourceId']in(74,83):assert all(row['sourceFields'].get(key)==value for key,value in fields.items())
    legacy=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/SKILLS/SkillNec.cpp'
    assert 'SKILLS_SrvSt17_Poison_CorpseExplosion'in legacy.read_text()
    client_path=ROOT/'tools/reference/poison-corpse-aim-client-evidence.json';client=json.loads(client_path.read_text())
    assert client['patches']==40 and client['affectedPaths']==41 and client['buildErrors']==0 and client['buildWarnings']==20 and not client['graphicalInputVerified']
    for section in ('patchHashes','sharedInputs','artifacts'):
        for path,digest in client[section].items():assert sha(ROOT/path)==digest,path
    for path,digest in client['clientInputs'].items():assert sha(STAGE/'client-root/upstream/ClassicUO'/path)==digest,path
    for kind in ('content','integration'):assert all(text in(STAGE/(kind+'-build-1.log')).read_text()for text in('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',1018),('first-loop-affix-audit.json',21)):
        fresh=json.loads((STAGE/name).read_text());assert fresh==json.loads((ROOT/'tools/reference'/name).read_text())and len(fresh['InputHashes'])==count
        assert fresh['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in fresh['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {key:value for key,value in fresh.items()if key not in skip}=={key:value for key,value in old.items()if key not in skip}
    assert(STAGE/'audit-native-checks.log').read_text().count(': passed')==2
    python=(STAGE/'python-tests-2.log').read_text();assert 'Ran 71 tests'in python and '\nOK\n'in python and 'FAILED'not in python
    for name,text in (('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in(STAGE/name).read_text()
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses']and sum(row['implementation']=='handler-present'for row in status['skills'])==191
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key,relative,count in (('saves','.local/server/Saves',38),('configuration','.local/server/Configuration',20)):
        folder=Path(installed['runtimeRoot'])/relative;assert len(protected[key])==count and {str(path.relative_to(folder)):sha(path)for path in folder.rglob('*')if path.is_file()}==protected[key]
    files=[Path(__file__).resolve(),*(ROOT/path for path in sorted(sources)),source,manifest,legacy,client_path,
        *(ROOT/path for path in ('tests/check_poison_corpse_aim.py','tests/check_poison_corpse_aim_smoke.py','tests/check_necro_town.py','tests/check_poison_explosion.py','tools/review_poison_corpse_aim_client.py','patches/ClassicUO/0040-poison-explosion-ground-targeting.patch','data/Sanctuary/Balance/class-skill-status.json')),
        *(ROOT/'docs'/name for name in ('poison-corpse-aim-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-1.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','audit-run-2.log','content-build-1.log','integration-build-1.log','python-tests.log','python-tests-2.log','readiness-check.log','catalogue-check.log','roadmap-check.log','poison-corpse-aim-fixtures.json','poison-explosion-fixtures.json','client-build-1.log','client-routing-before.cs')),
        *(smoke/name for name in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','poison-corpse-aim-fixtures.json','poison-explosion-fixtures.json'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Poison Explosion ground targeting through the shared cursor-nearest corpse picker,retaining distinct poison eligibility and cloud delivery',sourceSelection=dict(sourceIds=[74,83],sharedFields=fields,staticLegacyStart='SKILLS_SrvSt17_Poison_CorpseExplosion',nearClickTiles=2),
        verified=dict(affectedSystemNativeMarkers=129,parentMarkersRetained=99,smokeMarkers=39,bootsPerCohort=3,actualReloadsPerCohort=2,actualWorldSavesPerCohort=3,
            actualGroundRightCastsPerCohort=3,actualGroundQuickCastsPerCohort=3,retainedExplicitPoisonInputsPerCohort=6,
            poisonEligibilityIndependentOfCorpseLifeRng=True,noAcquisitionSeedLootManaMutation=True,lockedPendingCorpse=True,lostPrimaryNoReacquisitionOrPayment=True,
            onceOnlyEightManaAndEightClouds=True,originalOrphanCorpseLootMetadataReloads=True,retainedPoisonContactCureImmunityPotionAndFlightReloads=True,
            clientPatchStack=40,clientBuildWarnings=20,clientBuildErrors=0,clientGraphicalInputVerified=False,
            pythonTests=71,auditFingerprints=[1018,21],namedDecisionsUnchanged=573,installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=native['artifacts'],evidence={str(path.relative_to(ROOT)):sha(path)for path in files},
        limitations=['Shared selection is supported by owned current fields and static older shared-start review. Direct runtime observation established CE ground aim only. Extending the two-tile Euclidean/serial-tie policy to Poison Explosion is an explicit Sanctuary adaptation,not measured current client behavior.',
            'Existing corpse eligibility,eight-tile range,poison packet/geometry/event/owner/recipient and current-executable limits remain. The separate corpse-in-town start guard is next work;caster-town coverage does not prove it. No inference about Raise Skeleton,Mages or Revive selection modes.',
            'Full40-patch client build/round trip and actual server inputs are separate from rendered cursor playtesting. Installed client/server/saves/configuration unchanged.',
            'Initial Python run preceded the fresh audit and rejected stale evidence;after unchanged573decision audit refresh all71pass. Initial audit CLI invocation omitted--stage and exited before starting a world;run2succeeds.',
            '129affected-system markers retain99parent checks;not a new full-class or565-marker original-summon certificate. Modern barrier death/hidedead and shared explosive wall-center projection remain deferred. No class complete,deployment or final artwork.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/poison-corpse-aim-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Poison corpse aim:129affected-system/39smoke markers,six ground inputs/two reloads per cohort;40client patches;installed build and saves preserved.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
