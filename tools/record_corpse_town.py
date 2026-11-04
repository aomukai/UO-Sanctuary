#!/usr/bin/env python3
"""Bind explosion corpse-location town checks, retained mechanics and saved state."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/corpse-town'
PARENT='a0bee14a15150ccef91dc1f905ca9d5cab79fbed'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE
    paths=[STAGE/name for name in ('birth-inputs.json','audit-inputs.json')]
    native,audit=[inventory.validate_inventory(path)for path in paths]
    for path in paths:snapshot(STAGE/'pre-build-2.json',path)
    assert native['artifacts']==audit['artifacts']and native['copiedData']==audit['copiedData']
    previous=STAGE.with_name('poison-corpse-aim');prior=json.loads((previous/'birth-inputs.json').read_text())
    sources={'src/Sanctuary.Content/'+name+'.cs'for name in ('CorpseCastTargets','CorpseExplosionCasts','NecromancerCombat','NecromancerSpellMissiles')}
    sources.add('tests/Sanctuary.Integration/CorpseTownChecks.cs')
    assert {path for path in set(native['sourceInputs'])|set(prior['sourceInputs'])if path.endswith('.cs')and native['sourceInputs'].get(path)!=prior['sourceInputs'].get(path)}==sources
    assert native['copiedData']==prior['copiedData']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/poison-corpse-aim-native-evidence.json'],cwd=ROOT))
    oldlog=previous/'birth-native-checks.log';assert sha(oldlog)==parent['evidence'][str(oldlog.relative_to(ROOT))]
    expected=markers(oldlog);assert sum(expected.values())==129
    expected['corpse-town-check']+=1;expected['corpse-town-reload-check']+=2
    commands=('corpse-town-ce-right','corpse-town-ce-quick','corpse-town-poison-right','corpse-town-poison-quick')
    for command in commands:expected[command+'-check']+=3;expected[command+'-result']+=3
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==156
    smoke=STAGE.with_name('corpse-town-smoke');smoked=inventory.validate_inventory(smoke/'birth-inputs.json');snapshot(STAGE/'pre-build-2.json',smoke/'birth-inputs.json')
    assert smoked['artifacts']==native['artifacts']and smoked['copiedData']==native['copiedData']and sum(markers(smoke/'birth-native-checks.log').values())==36
    for folder in (STAGE,smoke):
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'corpse-town-fixtures.json').read_text());assert saved['Generation']==2 and saved['Owner']>0 and saved['Corpse']>0 and saved['Mana']>0
        assert saved['Row']['Corpse']==saved['Corpse']and saved['Row']['BaseLife']['Seed']>0 and not saved['Row']['NoSelect']
        for command in commands:assert log.count(command+'-result: passed')==3
    source=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';manifest=ROOT/'tools/reference/d2r-named-equipment/sources.json'
    assert sha(source)==json.loads(manifest.read_text())['files']['json/skills.json']['sha256']
    raw=json.loads(source.read_text());assert raw['74']['srvstfunc']==raw['83']['srvstfunc']==17
    legacy=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/SKILLS/SkillNec.cpp'
    body=legacy.read_text().split('int32_t __fastcall SKILLS_SrvSt17_Poison_CorpseExplosion',1)[1].split('//D2Game.',1)[0]
    assert 'SUNIT_GetTargetUnit'in body and '!DUNGEON_IsRoomInTown(pRoom)'in body and 'D2COMMON_11021(pTarget)'in body
    town_helper=STAGE/'native/Projects/UOContent/Spells/Base/SpellHelper.cs'
    for kind in ('content','integration'):assert all(text in(STAGE/(kind+'-build-2.log')).read_text()for text in('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',1018),('first-loop-affix-audit.json',21)):
        fresh=json.loads((STAGE/name).read_text());assert fresh==json.loads((ROOT/'tools/reference'/name).read_text())and len(fresh['InputHashes'])==count
        assert fresh['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in fresh['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {key:value for key,value in fresh.items()if key not in skip}=={key:value for key,value in old.items()if key not in skip}
    assert(STAGE/'audit-native-checks.log').read_text().count(': passed')==2
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests'in python and '\nOK\n'in python and 'FAILED'not in python
    for name,text in (('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in(STAGE/name).read_text()
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses']and sum(row['implementation']=='handler-present'for row in status['skills'])==191
    client_path=ROOT/'tools/reference/poison-corpse-aim-client-evidence.json';client=json.loads(client_path.read_text());assert client['patches']==40
    for section in ('patchHashes','sharedInputs'):
        for path,digest in client[section].items():assert sha(ROOT/path)==digest,path
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key,relative,count in (('saves','.local/server/Saves',38),('configuration','.local/server/Configuration',20)):
        folder=Path(installed['runtimeRoot'])/relative;assert len(protected[key])==count and {str(path.relative_to(folder)):sha(path)for path in folder.rglob('*')if path.is_file()}==protected[key]
    failed=STAGE/'attempt-1';assert 'Ground corpse town cast started an animation'in(failed/'smoke-birth-native-checks.log').read_text()
    frozen=json.loads((failed/'smoke-birth-inputs.json').read_text())
    assert sha(failed/'CorpseTownChecks.cs')==frozen['sourceInputs']['tests/Sanctuary.Integration/CorpseTownChecks.cs']
    for kind in ('content','integration'):assert sha(failed/kind/('Sanctuary.'+kind.title()+'.dll'))==frozen['artifacts']['Assemblies/Sanctuary.'+kind.title()+'.dll']
    files=[Path(__file__).resolve(),*(ROOT/path for path in sorted(sources)),source,manifest,legacy,town_helper,client_path,
        ROOT/'tests/check_corpse_town.py',ROOT/'tests/check_corpse_town_smoke.py',ROOT/'tests/check_poison_corpse_aim.py',ROOT/'data/Sanctuary/Balance/class-skill-status.json',
        *(ROOT/'docs'/name for name in ('corpse-town-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-1.json','pre-build-2.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','audit-run-2.log','content-build-1.log','integration-build-1.log','content-build-2.log','integration-build-2.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','corpse-town-fixtures.json')),
        *(failed/name for name in ('pre-build-1.json','audit-inputs.json','audit-native-checks.log','audit-run-1.log','smoke-birth-inputs.json','smoke-birth-native-checks.log','smoke-native-run-1.log','CorpseTownChecks.cs','content/Sanctuary.Content.dll','integration/Sanctuary.Integration.dll')),
        *(smoke/name for name in ('birth-inputs.json','birth-native-checks.log','native-run-2.log','corpse-town-fixtures.json'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Separate actual-corpse town restriction for both explosions,shared ordinary validation/acquisition/release and conserved original state',
        staticSourceReview=dict(sharedStart=17,sourceIds=[74,83],targetRoomCheckedBeforeCorpsePredicate=True,newFullSourceExecution=False,releasePolicy='Existing Sanctuary revalidation also applies after dynamic native region changes;older do55/do63 do not repeat the explicit town branch.'),
        verified=dict(affectedSystemNativeMarkers=156,parentMarkersRetained=129,smokeMarkers=36,bootsPerCohort=3,actualReloadsPerCohort=2,actualWorldSavesPerCohort=3,
            actualExplicitRightRefusalsPerCohort=6,actualGroundQuickRefusalsPerCohort=6,distinctCasterAndCorpseRegions=True,spoofedPointDoesNotBypassActualCorpse=True,
            closestTownCorpseDoesNotBlockFieldAlternative=True,disabledNativeGuardProjectionRetained=True,regionAddedDuringWindupRefusesLockedPrimary=True,
            directHelpersRefuseBeforeMutation=True,originalCorpseLootSeedManaReloads=True,fieldPositiveConsumptionPayment=True,
            pythonTests=71,auditFingerprints=[1018,21],namedDecisionsUnchanged=573,clientPatchesUnchanged=40,installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=native['artifacts'],evidence={str(path.relative_to(ROOT)):sha(path)for path in files},
        limitations=['Owned current route fields and static complete older-start review,not a newly executed full source/current executable room or dispatcher certificate.',
            'Native SpellHelper.IsTown keeps its guarded-region/safe-zone/duel semantics. Exact D2R room equivalence,charged/forced/proc/NPC contexts remain unverified. Release revalidation is an explicit dynamic-world adaptation.',
            '156affected-system markers retain129parent checks;not a fresh full-class or565-marker original-summon regression. Modern barrier hidedead/death and explosive wall-center projection remain deferred.',
            'Build1smoke negative control incorrectly left an eligible field alternative. Build2isolates town-only refusal while retaining positive fallback;production mechanics are unchanged by that fixture correction.',
            'No client rebuild/deployment,save reset,whole-class completion or final artwork. Effects/audio and icons remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/corpse-town-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Explosion corpse-town guard:156affected-system/36smoke markers,12wire refusals/two reloads per cohort;installed build and saves preserved.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
