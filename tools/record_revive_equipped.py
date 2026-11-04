#!/usr/bin/env python3
"""Bind learned equipped Revive admission, refusals, real inputs and reload evidence."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/revive-equipped'
FOCUS=ROOT/'.local/class-skills/revive-equipped-focus'
PARENT='af1215db9785b12f9c612599fb017d1512ffc2c2'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    paths=[STAGE/'birth-inputs.json',FOCUS/'birth-inputs.json',STAGE/'audit-inputs.json']
    tested,focused,audit=[inventory.validate_inventory(p)for p in paths]
    for p in paths:snapshot(STAGE/'pre-build-5.json',p)
    assert tested['artifacts']==focused['artifacts']==audit['artifacts']and tested['copiedData']==focused['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/revive-native-swing';prior=json.loads((previous/'birth-inputs.json').read_text())
    assert tested['copiedData']==prior['copiedData']
    sources={'src/Sanctuary.Content/'+name+'.cs'for name in ('FrozenEquipmentEffects','ReviveCasts','ReviveEquipmentLife','ReviveEquipmentOffense')}
    sources|={'tests/Sanctuary.Integration/'+name+'.cs'for name in ('ReviveCastChecks','ReviveEquipmentLifeChecks','ReviveEquippedChecks','ReviveNativeSwingChecks')}
    assert {p for p in set(tested['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and tested['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}==sources
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][n]==prior['artifacts'][n]==sha(STAGE/'native/Distribution'/n)
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/revive-native-swing-evidence.json'],cwd=ROOT))
    for folder,old_folder,total,retained in ((STAGE,previous,547,532),(FOCUS,previous.with_name('revive-native-swing-focus'),72,57)):
        old=old_folder/'birth-native-checks.log';assert sha(old)==parent['evidence'][str(old.relative_to(ROOT))]
        expected=markers(old);assert sum(expected.values())==retained
        expected['revive-equipped-check']+=1;expected['revive-equipped-reload-check']+=2
        for mode in ('right','quick'):
            expected['revive-equipped-'+mode+'-check']+=3;expected['revive-equipped-'+mode+'-result']+=3
        assert markers(folder/'birth-native-checks.log')==expected and sum(expected.values())==total
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'revive-equipped-fixtures.json').read_text());assert saved['Generation']==2 and all(saved[k]>0 for k in ('Owner','Pending','Original'))
        assert len(saved['Actors'])==2 and len({r['Actor']for r in saved['Actors']})==2
        assert all(r['Life']>0 and r['Binding']and r['Corpse']>0 for r in saved['Actors'])
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-5.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',1012),('first-loop-affix-audit.json',21)):
        fresh=json.loads((STAGE/name).read_text());assert fresh==json.loads((ROOT/'tools/reference'/name).read_text())and len(fresh['InputHashes'])==count
        assert fresh['ContentAssemblySha256']==tested['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in fresh['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in fresh.items()if k not in skip}=={k:v for k,v in old.items()if k not in skip}
    assert(STAGE/'audit-native-checks.log').read_text().count(': passed')==2
    assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
    for name,text in (('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in(STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key,relative,count in (('saves','.local/server/Saves',38),('configuration','.local/server/Configuration',20)):
        folder=Path(installed['runtimeRoot'])/relative;assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not status['completedClasses']and sum(s['implementation']=='handler-present'for s in status['skills'])==191
    files=[Path(__file__).resolve(),*(ROOT/p for p in sorted(sources)),ROOT/'tests/check_iron_golem_birth.py',
        *(ROOT/'tests'/n for n in ('check_revive_equipped.py','check_revive_equipped_focus.py')),
        *(ROOT/'docs'/n for n in ('revive-equipped-checkpoint.md','defiler-body-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-5.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','audit-inputs.json','audit-native-checks.log','audit-run-2.log','content-build-5.log','integration-build-5.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','revive-equipped-fixtures.json','failed-1/birth-native-checks.log','failed-1/birth-inputs.json')),
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-5.log','revive-equipped-fixtures.json','failed-1/birth-native-checks.log','failed-2/birth-native-checks.log','failed-3/birth-native-checks.log'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Learned Revive admits qualified equipped originals through shared frozen graph, defenses, incoming events, offense and native A1; unsupported inputs refuse before payment',
        verified=dict(nativeMarkers=547,focusedMarkers=72,parentMarkersRetained=532,bootsPerCohort=3,actualReloadsPerCohort=2,connectedWorldSavesPerCohort=3,
            equippedOriginalKinds=['Skeleton','Zombie'],unsupportedScenes=7,realRightAndQuickInputsPerBoot=True,preflightNoRngOrActivation=True,
            unpaidWindup=True,releasePaysOnce=True,independentLootDeletion=True,underfundedAndMovementCancellation=True,
            pendingCorpseAvailabilityReloaded=True,originalSerialAiWoundsAndBindingsReloaded=True,pythonTests=71,auditFingerprints=[1012,21],namedDecisionsUnchanged=573,
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),artifacts=tested['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['The first broader run failed the inherited pending-packet reload assertion; the detailed rerun passed both reloads without gameplay changes. Its intermittent cause remains unresolved and the failed run is retained.',
            'Admission is qualified; unsupported native bodies, item properties, hands, missiles, elements, callbacks and historical missing inputs remain explicit refusals.',
            'Current native AI/ability/cadence, animation lookup, registration chronology and recipient/world/PvP/client paths are not universally certified.',
            'No class complete, deployment or final artwork; effects/audio and talent/hotkey icons remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/revive-equipped-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Learned equipped Revive:547 broader/72 focused;532 parent markers retained;qualified originals,refusals,right/quick inputs and two reloads;installation preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
