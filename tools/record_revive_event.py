#!/usr/bin/env python3
"""Bind frozen original Revive registered callbacks and shared contact checks."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/revive-event'
FOCUS=ROOT/'.local/class-skills/revive-event-focus'
PARENT='475a6b8976581545f2228064474ec33284f4a447'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json');focus=inventory.validate_inventory(FOCUS/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for folder,name in ((STAGE,'birth-inputs.json'),(FOCUS,'birth-inputs.json'),(STAGE,'audit-inputs.json')):snapshot(STAGE/'pre-build-3.json',folder/name)
    assert tested['artifacts']==focus['artifacts']==audit['artifacts']and tested['copiedData']==focus['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/revive-defense';prior=json.loads((previous/'birth-inputs.json').read_text())
    assert tested['copiedData']==prior['copiedData']
    changed={p for p in set(tested['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and tested['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}
    sources={'src/Sanctuary.Content/'+p+'.cs'for p in ('ReviveEquipmentEvents','ReviveNativeActivation','NpcCombatEvents','NpcOpenWounds','NpcCrushing','NpcEquipmentCombat','NpcEquipmentRetaliation','NpcSpectralHit','NativeMonsterReturn','IronGolemContact')}|{'tests/Sanctuary.Integration/ReviveEventChecks.cs'}
    assert changed==sources
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/revive-defense-native-evidence.json'],cwd=ROOT))
    baseline=previous/'birth-native-checks.log';assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);assert sum(expected.values())==508
    expected['revive-event-check']+=1;expected['revive-event-reload-check']+=2
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==511
    old_focus=previous.with_name('revive-defense-focus')/'birth-native-checks.log'
    assert sha(old_focus)==parent['evidence'][str(old_focus.relative_to(ROOT))]
    expected_focus=markers(old_focus);assert sum(expected_focus.values())==33
    expected_focus['revive-event-check']+=1;expected_focus['revive-event-reload-check']+=2
    assert markers(FOCUS/'birth-native-checks.log')==expected_focus and sum(expected_focus.values())==36
    for folder in (STAGE,FOCUS):
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'revive-event-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Actors'])==4
        assert [f['Kind']for f in saved['Actors']]==['events','legacy','registration','numeric']
        assert all(f['Actor']>0 for f in saved['Actors']) and saved['Owner']>0
        assert saved['Actors'][0]['Issue']==saved['Actors'][1]['Issue']==None
        assert 'event' in saved['Actors'][2]['Issue'].lower()and 'numeric' in saved['Actors'][3]['Issue'].lower()
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-3.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',1001),('first-loop-affix-audit.json',21)):
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
    files=[Path(__file__).resolve(),*(ROOT/p for p in sorted(sources)),ROOT/'src/Sanctuary.Content/SourceRandom.cs',
        *(ROOT/'tests'/name for name in ('check_revive_event.py','check_revive_event_focus.py')),
        *(ROOT/'docs'/name for name in ('revive-event-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-3.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','audit-inputs.json','audit-native-checks.log','audit-run-2.log','content-build-3.log','integration-build-3.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','revive-event-fixtures.json')),
        *(FOCUS/name for name in ('birth-inputs.json','birth-native-checks.log','native-run-2.log','revive-event-fixtures.json'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Explicit frozen Revive registered-event admission and original-source use of shared qualified contact/damage/return;current subscription chronology/native swing/full incoming remain gated',
        verified=dict(nativeMarkers=511,focusedMarkers=36,parentMarkersRetained=508,bootsPerCohort=3,actualReloadsPerCohort=2,
            savedOriginalsPerCohort=4,workingEventProfilesPerCohort=1,legacyControlsPerCohort=1,unsupportedControlsPerCohort=2,
            registeredTableRows=21,workingItemTotals=dict(openWounds=100,crushingBlow=45,flatRetaliation=10),
            deduplicatedCallbacks=True,sharedContactCasesPerBoot=6,healthAndCallbackOrder=True,lethalReturn=True,sharedComponentStream=True,atomicProfileRefusal=True,
            pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=[1001,21],installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        olderEngineReference={name:sha(ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src'/name)for name in ('ITEMS/ItemMode.cpp','UNIT/SUnitEvent.cpp')},
        artifacts=tested['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Shared canonical registry ordering is component behavior;current D2R runtime attachment/subscription history and exact executable RNG chronology are not certified.',
            'Controlled nonsummoned native incoming random multiplier remains gated;full Revive recipients and flat-return HP into them need that adapter.',
            'Qualified contact fixtures supply explicit result/range services;actual original geometry/result/controller/AI/action scheduling and pending persistence remain unfinished.',
            'Unconsumed registrations,raw numeric/derived supplier issues,skill/stat attachment,attack-slot/element/affix/projectile/hand-selection and full world/PvP/client cases remain explicit.',
            'No new handler,completed class,deployment or new assets;existing effect placeholders and talent/hotkey icon ledgers remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/revive-event-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Original Revive registered events/shared contact:511 broader/36 focused markers;508 parent retained;callbacks,HP,death,refusals and reloads;current subscription chronology/native controller gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
