#!/usr/bin/env python3
"""Bind Corpse Explosion near-click mechanics, client routing and conserved corpse state."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/corpse-aim'
PARENT='f73af732e8c06241931330fe93bff71fcc3e0d95'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    paths=[STAGE/'birth-inputs.json',STAGE/'audit-inputs.json']
    native,audit=[inventory.validate_inventory(p)for p in paths]
    for p in paths:snapshot(STAGE/'pre-build-1.json',p)
    assert native['artifacts']==audit['artifacts']and native['copiedData']==audit['copiedData']
    previous=STAGE.with_name('bone-prison-aim');prior=json.loads((previous/'birth-inputs.json').read_text())
    sources={'src/Sanctuary.Content/'+n+'.cs'for n in ('NearClickTargets','CorpseExplosionCasts','NecromancerCombat')}
    sources|={'tests/Sanctuary.Integration/'+n+'.cs'for n in ('CorpseAimChecks',)}
    assert {p for p in set(native['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and native['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}==sources
    assert {p for p in native['copiedData']if native['copiedData'][p]!=prior['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][n]==prior['artifacts'][n]==sha(STAGE/'native/Distribution'/n)
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/bone-prison-aim-native-evidence.json'],cwd=ROOT))
    old_log=previous/'birth-native-checks.log';assert sha(old_log)==parent['evidence'][str(old_log.relative_to(ROOT))]
    expected=markers(old_log);assert sum(expected.values())==60
    for family in ('corpse-cast','corpse-aim'):expected[family+'-check']+=1;expected[family+'-reload-check']+=2
    for command in ('corpse-ground-right','corpse-ground-quick'):expected[command+'-check']+=3;expected[command+'-result']+=3
    log=(STAGE/'birth-native-checks.log').read_text();assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==78
    assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    smoke=STAGE.with_name('corpse-aim-smoke');smoked=inventory.validate_inventory(smoke/'birth-inputs.json');snapshot(STAGE/'pre-build-1.json',smoke/'birth-inputs.json')
    assert smoked['artifacts']==native['artifacts']and smoked['copiedData']==native['copiedData']and sum(markers(smoke/'birth-native-checks.log').values())==24
    assert ': failed'not in(smoke/'birth-native-checks.log').read_text()
    for folder in (STAGE,smoke):
        saved=json.loads((folder/'corpse-aim-fixtures.json').read_text());assert saved['Generation']==2 and saved['Owner']>0 and saved['Corpse']>0 and saved['Mana']>0
        assert saved['Row']['Corpse']==saved['Corpse']and saved['Row']['BaseLife']['Seed']>0 and not saved['Row']['NoSelect']and not saved['Row']['Owned']
    client_path=ROOT/'tools/reference/corpse-aim-client-evidence.json';client=json.loads(client_path.read_text())
    assert client['patches']==39 and client['affectedPaths']==41 and client['buildErrors']==0 and client['buildWarnings']==20 and not client['graphicalInputVerified']
    for section in ('patchHashes','sharedInputs','artifacts'):
        for path,digest in client[section].items():assert sha(ROOT/path)==digest,path
    for path,digest in client['clientInputs'].items():assert sha(STAGE/'client-root/upstream/ClassicUO'/path)==digest,path
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-1.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',1015),('first-loop-affix-audit.json',21)):
        fresh=json.loads((STAGE/name).read_text());assert fresh==json.loads((ROOT/'tools/reference'/name).read_text())and len(fresh['InputHashes'])==count
        assert fresh['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in fresh['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in fresh.items()if k not in skip}=={k:v for k,v in old.items()if k not in skip}
    assert(STAGE/'audit-native-checks.log').read_text().count(': passed')==2
    assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
    for name,text in (('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in(STAGE/name).read_text()
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());old_status=json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    assert not status['completedClasses']and sum(s['implementation']=='handler-present'for s in status['skills'])==191
    changed=[(a,b)for a,b in zip(old_status['skills'],status['skills'])if a!=b];assert len(changed)==1
    a,b=changed[0];assert b['talentId']=='necromancer.corpseexplosion'and {k for k in a if a[k]!=b[k]}=={'remaining'}
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key,relative,count in (('saves','.local/server/Saves',38),('configuration','.local/server/Configuration',20)):
        folder=Path(installed['runtimeRoot'])/relative;assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    files=[Path(__file__).resolve(),*(ROOT/p for p in sorted(sources)),ROOT/'tests/check_corpse_aim.py',ROOT/'tests/check_corpse_aim_smoke.py',ROOT/'tests/check_bone_prison_aim.py',ROOT/'tools/review_corpse_aim_client.py',client_path,ROOT/'patches/ClassicUO/0039-corpse-explosion-ground-targeting.patch',ROOT/'tools/build_skill_roadmap.py',ROOT/'tools/reference/corpse-explosion-user-targeting-decision.json',ROOT/'data/Sanctuary/Balance/class-skill-status.json',
        *(ROOT/'docs'/n for n in ('corpse-aim-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-1.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-3.log','content-build-1.log','integration-build-1.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','corpse-aim-fixtures.json','client-build-1.log','client-build-2.log','client-input-1.cs')),
        *(smoke/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','corpse-aim-fixtures.json'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='User-observed Corpse Explosion ground acquisition with approved two-tile policy;shared mobile/corpse ranker and client point routing',
        verified=dict(affectedSystemNativeMarkers=78,parentMarkersRetained=60,smokeMarkers=24,bootsPerCohort=3,actualReloadsPerCohort=2,actualWorldSavesPerCohort=3,
            actualGroundRightCastsPerCohort=3,actualGroundQuickCastsPerCohort=3,existingCorpsePacketsAndAreaChecks=True,sharedPsychicHammerAndBonePrisonRetained=True,
            cursorNearestNotCasterNearest=True,inclusiveCircleAndExplicitSerialTies=True,existingEligibilityAndEightTileCorpseRange=True,
            unchangedAcquisitionLootSeedAndPayment=True,pendingIdentityAndTimingRetained=True,onceReleaseConsumptionLootSeedPaymentAndDamage=True,
            lostReservedTargetNoReacquisitionOrPayment=True,orphanCorpseMetadataAndLootAfterReload=True,
            clientPatchStack=39,clientBuildErrors=0,clientBuildWarnings=20,clientGraphicalInputVerified=False,
            pythonTests=71,auditFingerprints=[1015,21],namedDecisionsUnchanged=573,installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=native['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Ground acquisition is user observed. Two-tile Euclidean boundary,serial ties and preserved existing Sanctuary eight-tile caster range are explicit adaptations,not a complete current-D2R client boundary certificate.',
            'Client build/39-patch round trip and actual server wire fixtures are separate evidence. Rendered mouse/cursor behavior is unverified;installed client/server are unchanged.',
            'Existing source/current-engine geometry,region,event,owner/item/PvP and historical data limits remain;modern barrier death/hidedead remains deferred.',
            'This affected-system cohort retains60parent markers;it is not a fresh565-marker original-summon regression. No class complete,deployment or final artwork.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/corpse-aim-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Corpse aim:78affected-system/24smoke markers,6ground inputs per cohort,two reloads;39client patches/build verified;installed artifacts and saves preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
