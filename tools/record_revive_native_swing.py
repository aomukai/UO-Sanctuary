#!/usr/bin/env python3
"""Bind original native OnSwing/AI, pending packets and connected-save evidence."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/revive-native-swing'
FOCUS=ROOT/'.local/class-skills/revive-native-swing-focus'
PARENT='b169ff52ab365cee9a1472163a4a9d694d38e6a0'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    paths=[STAGE/'birth-inputs.json',FOCUS/'birth-inputs.json',STAGE/'audit-inputs.json']
    tested,focused,audit=[inventory.validate_inventory(p)for p in paths]
    for p in paths:snapshot(STAGE/'pre-build-3.json',p)
    assert tested['artifacts']==focused['artifacts']==audit['artifacts']and tested['copiedData']==focused['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/revive-swing-clock';prior=json.loads((previous/'birth-inputs.json').read_text())
    assert tested['copiedData']==prior['copiedData']
    sources={'src/Sanctuary.Content/'+name+'.cs'for name in ('CombatStuns','CreatureHitRecovery','IronGolemContact','NpcCombatEvents','ReviveNativeActivation','ReviveSwings')}
    sources|={'tests/Sanctuary.Integration/HolyShieldCastChecks.cs','tests/Sanctuary.Integration/ReviveNativeSwingChecks.cs'}
    assert {p for p in set(tested['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and tested['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}==sources
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][n]==prior['artifacts'][n]==sha(STAGE/'native/Distribution'/n)
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/revive-swing-clock-native-evidence.json'],cwd=ROOT))
    for folder,old_folder,total,retained in ((STAGE,previous,532,523),(FOCUS,previous.with_name('revive-swing-clock-focus'),57,48)):
        old=old_folder/'birth-native-checks.log';assert sha(old)==parent['evidence'][str(old.relative_to(ROOT))]
        expected=markers(old);assert sum(expected.values())==retained
        expected['revive-native-swing-check']+=1;expected['revive-native-swing-reload-check']+=2;expected['revive-native-swing-result']+=3;expected['revive-native-swing-save-result']+=3
        assert markers(folder/'birth-native-checks.log')==expected and sum(expected.values())==total
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'revive-native-swing-fixtures.json').read_text());assert saved['Generation']==2 and all(saved[k]>0 for k in ('Owner','Actor','Target','TargetLife','Life','Seed'))
        assert len({saved['Owner'],saved['Actor'],saved['Target']})==3 and saved['EndUtc'].endswith('Z')
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-3.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
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
        *(ROOT/'tests'/n for n in ('check_revive_native_swing.py','check_revive_native_swing_focus.py')),
        *(ROOT/'docs'/n for n in ('revive-native-swing-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-3.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','audit-inputs.json','audit-native-checks.log','audit-run-2.log','content-build-3.log','integration-build-3.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','revive-native-swing-fixtures.json','failed-1/birth-native-checks.log')),
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-3.log','revive-native-swing-fixtures.json','failed-1/birth-native-checks.log'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Opt-in original native A1 entry,shared packet/contact,AI hold and persisted windup/busy state;public equipped casting unchanged',
        verified=dict(nativeMarkers=532,focusedMarkers=57,parentMarkersRetained=523,bootsPerCohort=3,actualReloadsPerCohort=2,connectedWorldSavesPerCohort=3,
            actualAiPursuitAndContactPerCohort=3,deterministicScenesPerBoot=7,actualNativeOnSwing=True,noEarlyHp=True,noBusyReroll=True,releaseOnce=True,
            escapeMoveFreezeGetHitParkCancellation=True,parkingPreservesDeadline=True,noMovementFlagMutation=True,originalSerialAndAiRetained=True,
            atomicMalformedPacketRefusal=True,packetJsonRoundtrip=True,actualSavedPendingPacketDeserialized=True,offlineReloadNoHitOrReroll=True,
            remainingDeadlineAndWoundsReloaded=True,unmanagedNpcExclusion=True,inheritedHolyShieldSaveSetupRetained=True,pythonTests=71,auditFingerprints=[1012,21],namedDecisionsUnchanged=573,
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),artifacts=tested['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Public equipped-corpse preflight/admission across source bodies remains unfinished.',
            'Native AI cadence,all interruption/re-entry and existing asynchronous ability combinations are not a universal current-D2R controller certificate.',
            'Current animation lookup/mode/event registration history and unsupported source/item/hand/element/callback/recipient/world/PvP/client paths remain explicit.',
            'The save-only fixture uses a future UTC epoch to retain a deadline through load;this is not a gameplay delay.',
            'No class complete,deployment or final artwork;effects/audio and talent/hotkey icons remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/revive-native-swing-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Revive native A1:532 broader/57 focused,523 parent retained;actual OnSwing/AI/contact and connected pending-packet saves/two reloads;installation preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
