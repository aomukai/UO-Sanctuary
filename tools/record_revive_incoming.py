#!/usr/bin/env python3
"""Bind original Revive Kind1 incoming FP/AOS/retaliation and reload evidence."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/revive-incoming'
FOCUS=ROOT/'.local/class-skills/revive-incoming-focus'
PARENT='ba41db467a4f53f454a874360571d40047dc910c'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json');focus=inventory.validate_inventory(FOCUS/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for folder,name in ((STAGE,'birth-inputs.json'),(FOCUS,'birth-inputs.json'),(STAGE,'audit-inputs.json')):snapshot(STAGE/'pre-build-5.json',folder/name)
    assert tested['artifacts']==focus['artifacts']==audit['artifacts']and tested['copiedData']==focus['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/revive-event';prior=json.loads((previous/'birth-inputs.json').read_text())
    assert tested['copiedData']==prior['copiedData']
    changed={p for p in set(tested['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and tested['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}
    sources={'src/Sanctuary.Content/'+p+'.cs'for p in ('ReviveEquipmentIncoming','ReviveNativeActivation','PreparedMonsterNativeDamage','SkillHealth','IronGolemContact')}|{'tests/Sanctuary.Integration/ReviveIncomingChecks.cs'}
    assert changed==sources
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/revive-event-native-evidence.json'],cwd=ROOT))
    baseline=previous/'birth-native-checks.log';assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);assert sum(expected.values())==511
    expected['revive-incoming-check']+=1;expected['revive-incoming-reload-check']+=2
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==514
    old_focus=previous.with_name('revive-event-focus')/'birth-native-checks.log'
    assert sha(old_focus)==parent['evidence'][str(old_focus.relative_to(ROOT))]
    expected_focus=markers(old_focus);assert sum(expected_focus.values())==36
    expected_focus['revive-incoming-check']+=1;expected_focus['revive-incoming-reload-check']+=2
    assert markers(FOCUS/'birth-native-checks.log')==expected_focus and sum(expected_focus.values())==39
    for folder in (STAGE,FOCUS):
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'revive-incoming-fixtures.json').read_text());assert saved['Generation']==2 and saved['Life']>0
        assert all(saved[k]>0 for k in ('Owner','Incoming','Legacy'))and saved['Incoming']!=saved['Legacy']
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-5.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',1002),('first-loop-affix-audit.json',21)):
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
        *(ROOT/'tests'/name for name in ('check_revive_incoming.py','check_revive_incoming_focus.py')),
        *(ROOT/'docs'/name for name in ('revive-incoming-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-5.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','content-build-5.log','integration-build-5.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','revive-incoming-fixtures.json')),
        *(FOCUS/name for name in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','revive-incoming-fixtures.json'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Opt-in original Kind1 recipient for post-mitigation SkillHealth/native fixed-point AOS/retaliation,shared incoming guards and notification credit;native kernel/public cast unchanged',
        verified=dict(nativeMarkers=514,focusedMarkers=39,parentMarkersRetained=511,bootsPerCohort=3,actualReloadsPerCohort=2,
            savedOriginalsPerCohort=2,incomingProfilesPerCohort=1,legacyEventControlsPerCohort=1,directFractionCasesPerBoot=14,nativeAosCasesPerBoot=10,
            contactReturnCasesPerBoot=2,initialLethalCases=2,positivePetScalar=8,petScalarReads=0,anonymousDamage=True,wholeDisplayCredit=True,partialLifeReloads=True,
            unknownProfileAtomicRefusal=True,customDamageRefusal=True,qualifiedGuardAdmission=True,pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=[1002,21],
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        nativeKernelReference={name:sha(STAGE/'native'/name)for name in ('Projects/Server/Mobiles/Mobile.cs','Projects/UOContent/Mobiles/BaseCreature.cs','Projects/UOContent/Misc/AOS.cs')},
        artifacts=tested['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Direct arbitrary native Damage calls and AOS branches bypassing the fixed-point hook are not newly certified;unknown native Damage/OnDamage overrides and unsupported modifiers still refuse.',
            'Allied/hostility policy is unchanged;prepared player guard admission is not a player/pet PvP certificate.',
            'Current stat attachment/event subscription chronology,original geometry/result/controller scheduling and pending action persistence remain unfinished;public equipped casting remains gated.',
            'Existing raw numeric/property/event/hand/element/AI/world/client and wider PvP cases remain explicit.',
            'No new handler,completed class,deployment or artwork;shared effect placeholders and talent/hotkey icons remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/revive-incoming-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Original Revive Kind1 incoming:514 broader/39 focused markers;511 parent retained;fractional/AOS/anonymous/return/death/reload checks;zero native pet-multiplier reads.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
