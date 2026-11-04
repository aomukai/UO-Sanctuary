#!/usr/bin/env python3
"""Bind shared Iron Maiden admission and reflection-safe original NPC contact."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-iron-maiden'
FOCUS=STAGE.with_name('npc-iron-maiden-focus')
PARENT='1a2d489441710b830f23bfb5818680f9e1fa8adb'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    paths=[STAGE/'birth-inputs.json',FOCUS/'birth-inputs.json',STAGE/'audit-inputs.json']
    tested,focused,audit=[inventory.validate_inventory(p)for p in paths]
    for p in paths:snapshot(STAGE/'pre-build-13.json',p)
    assert tested['artifacts']==focused['artifacts']==audit['artifacts']and tested['copiedData']==focused['copiedData']==audit['copiedData']
    previous=STAGE.with_name('npc-armor-setup');prior=json.loads((previous/'birth-inputs.json').read_text())
    assert tested['copiedData']==prior['copiedData']
    sources={'src/Sanctuary.Content/'+n+'.cs'for n in ('IronGolemContact','NativeMonsterReturn')}
    sources|={'tests/Sanctuary.Integration/'+n+'.cs'for n in ('IronGolemOrderedContactChecks','NpcIronMaidenChecks','ReviveNativeSwingChecks')}
    assert {p for p in set(tested['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and tested['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}==sources
    # Reuse the established event, fractional mitigation, owner and HP phases;
    # this change must not quietly replace them with a separate return path.
    for n in ('IronMaiden','PreparedMonsterDamage','PreparedMonsterCredit','PaladinThorns','NecromancerEffects'):
        p='src/Sanctuary.Content/'+n+'.cs';assert tested['sourceInputs'][p]==prior['sourceInputs'][p]
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][n]==prior['artifacts'][n]==sha(STAGE/'native/Distribution'/n)
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/npc-armor-setup-native-evidence.json'],cwd=ROOT))
    for folder,old_folder,total,retained in ((STAGE,previous,565,559),(FOCUS,previous.with_name('npc-armor-setup-focus'),90,84)):
        old=old_folder/'birth-native-checks.log';assert sha(old)==parent['evidence'][str(old.relative_to(ROOT))]
        expected=markers(old);assert sum(expected.values())==retained
        expected['npc-iron-maiden-check']+=1;expected['npc-iron-maiden-reload-check']+=2;expected['npc-iron-maiden-live-result']+=3
        assert markers(folder/'birth-native-checks.log')==expected and sum(expected.values())==total
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'npc-iron-maiden-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Rows'])==2
        assert all(saved[k]>0 for k in ('Owner','Revive','Golem','Material'))and len(saved['Binding'])==64
        assert {r['Source']for r in saved['Rows']}=={saved['Revive'],saved['Golem']}
        for row in saved['Rows']:
            assert row['Life']>0 and row['Seed']>0 and row['Curse']['Source']==saved['Owner']and row['Curse']['Rank']==3
        for kind in ('Skeleton','IronGolemBody'):
            for mode in ('ordinary','fractional-tap','suppressed','miss','block','no-fill','removed','late'):
                assert f'{kind} mode={mode} 'in log
    smoke=STAGE.with_name('npc-iron-maiden-smoke');smoked=inventory.validate_inventory(smoke/'birth-inputs.json')
    snapshot(STAGE/'pre-build-13.json',smoke/'birth-inputs.json');assert smoked['artifacts']==tested['artifacts']and smoked['copiedData']==tested['copiedData']
    assert sum(markers(smoke/'birth-native-checks.log').values())==15 and ': failed'not in(smoke/'birth-native-checks.log').read_text()
    proof_path=ROOT/'tools/reference/iron-maiden-source-evidence.json';proof=json.loads(proof_path.read_text());assert proof['contexts']==dict(event=8640,legacyBloodGolemTail=15552)
    for section in ('inputs','artifacts'):
        for path,digest in proof[section].items():assert sha(ROOT/path)==digest,path
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-13.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',1013),('first-loop-affix-audit.json',21)):
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
    files=[Path(__file__).resolve(),*(ROOT/p for p in sorted(sources)),proof_path,ROOT/'tools/reference/bone-prison-user-targeting-decision.json',
        *(ROOT/'tests'/n for n in ('check_npc_iron_maiden.py','check_npc_iron_maiden_focus.py','check_npc_iron_maiden_smoke.py')),
        *(ROOT/'docs'/n for n in ('npc-iron-maiden-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')),
        *(STAGE/n for n in ('pre-build-13.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','audit-inputs.json','audit-native-checks.log','audit-run-2.log','content-build-13.log','integration-build-13.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','npc-iron-maiden-fixtures.json')),
        *(STAGE/n for n in ('native-run-1.log','failed-1/birth-inputs.json','failed-1/birth-native-checks.log','failed-1/ReviveNativeSwingChecks.cs','python-tests-before-audit-refresh.log','integration-build-9.log','integration-build-10.log')),
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-2.log','npc-iron-maiden-fixtures.json')),
        *(smoke/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-11.log','npc-iron-maiden-fixtures.json','native-run-8.log',*(f'failed-{i}/birth-native-checks.log'for i in (1,2,3,4,5,6,7,9))))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Admit cursed original NPC melee through existing Iron Maiden event;retain synchronous source identity through reflected retirement',
        verified=dict(nativeMarkers=565,focusedMarkers=90,smokeMarkers=15,parentMarkersRetained=559,bootsPerCohort=3,actualReloadsPerCohort=2,
            actualLearnedOriginalReviveAndIron=True,independentLootAndExactMaterial=True,originalNativeDefensesRetained=True,
            ordinaryFractionalAndLifeTap=True,noReflectionOnSuppressedMissBlockNoFillCancel=True,curseReadAtContact=True,
            actualReflectedMutualDeathAndOwnerCredit=True,sourceDeathBeforeReleaseStillSkips=True,actualCursedNativeA1PerCohort=3,
            savedCurseSourceRankDeadlineWoundsItemsBindingAndStream=True,noNewSaveSchema=True,pendingSaveRecoveryWindowFixedWithoutWeakeningAssertions=True,
            pythonTests=71,auditFingerprints=[1013,21],namedDecisionsUnchanged=573,installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=tested['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Existing older-source Iron Maiden event/host review,physical mitigation,stat/world/current-executable and native return limits remain. No new full engine certificate.',
            'The captured source identity is synchronous only;it is neither persisted nor reconstructed from unrelated history. Existing owner-credit capture is reused unchanged.',
            'Nonlethal arithmetic/live controls use physically resistant targets;separate actual weapon packets exercise lethal reflected retirement. Fractional arithmetic uses an explicitly supplied packet.',
            'One reproduced pending-swing reload failure was a3FP256 recovery frame between fixture manifest and composed serialization. This fixture now blocks regeneration during save preparation without changing its pending swing or exact assertions;other historical asynchronous failures are not retroactively explained.',
            'Unowned enemy Blood Golem tail,unadapted NPC events,positive/layered120 and raw66 remain unsupported. No class complete,deployment or final artwork.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/npc-iron-maiden-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('NPC Iron Maiden:565broader/90focused/15smoke;existing fractional event,reflected original retirement/owner credit,native A1 and saved wounds/curse across two reloads;installation preserved.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
