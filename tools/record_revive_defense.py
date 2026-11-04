#!/usr/bin/env python3
"""Bind frozen original Revive recipient defenses and actual reload checks."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/revive-defense'
FOCUS=ROOT/'.local/class-skills/revive-defense-focus'
PARENT='a256cbaf425cd898c39ef58e1be6391f67aaf74f'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json');focus=inventory.validate_inventory(FOCUS/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for folder,name in ((STAGE,'birth-inputs.json'),(FOCUS,'birth-inputs.json'),(STAGE,'audit-inputs.json')):snapshot(STAGE/'pre-build-4.json',folder/name)
    assert tested['artifacts']==focus['artifacts']==audit['artifacts']and tested['copiedData']==focus['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/revive-impact';prior=json.loads((previous/'birth-inputs.json').read_text())
    assert tested['copiedData']==prior['copiedData']
    changed={p for p in set(tested['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and tested['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}
    sources={'src/Sanctuary.Content/'+p+'.cs'for p in ('ReviveEquipmentDefenses','ReviveNativeActivation','ReviveFacets','NpcEquipmentCombat','NamedCombat','NamedElements','GemCombat','GearEffects','ItemDurationDefenses','ReviveEquipmentImpact')}|{'tests/Sanctuary.Integration/ReviveDefenseChecks.cs'}
    assert changed==sources
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/revive-impact-native-evidence.json'],cwd=ROOT))
    baseline=previous/'birth-native-checks.log';assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);assert sum(expected.values())==505
    expected['revive-defense-check']+=1;expected['revive-defense-reload-check']+=2
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==508
    old_focus=previous.with_name('revive-impact-focus')/'birth-native-checks.log'
    assert sha(old_focus)==parent['evidence'][str(old_focus.relative_to(ROOT))]
    expected_focus=markers(old_focus);assert sum(expected_focus.values())==30
    expected_focus['revive-defense-check']+=1;expected_focus['revive-defense-reload-check']+=2
    assert markers(FOCUS/'birth-native-checks.log')==expected_focus and sum(expected_focus.values())==33
    for folder in (STAGE,FOCUS):
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'revive-defense-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Actors'])==4
        assert [f['Enabled']for f in saved['Actors']]==[True,True,True,False]
        for f in saved['Actors']:
            assert f['Actor']>0 and f['Corpse']>0 and len(f['Bonuses'])==99 and all(len(row)==20 for row in f['Bonuses'])
        a,b,c,_=saved['Actors'];assert all(row[1]>=70 and row[12]>0 and row[16]>0 for row in a['Bonuses'])
        assert all(row[10]>0 and row[15]>0 and row[17]>0 for row in b['Bonuses'])
        assert all(row[5]>0 and row[7]>0 and row[13]>0 and row[18]>0 for row in c['Bonuses'])
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-4.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',1000),('first-loop-affix-audit.json',21)):
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
        *(ROOT/'tests'/name for name in ('check_revive_defense.py','check_revive_defense_focus.py')),
        *(ROOT/'docs'/name for name in ('revive-defense-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md')),
        *(STAGE/name for name in ('pre-build-4.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','content-build-4.log','integration-build-4.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','revive-defense-fixtures.json')),
        *(FOCUS/name for name in ('birth-inputs.json','birth-native-checks.log','native-run-3.log','revive-defense-fixtures.json'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Frozen original Revive recipient resistance/duration/reduction/absorption and conditional defense component;native actor lifecycle and qualified allocation;public equipped cast/events/swing remain gated',
        verified=dict(nativeMarkers=508,focusedMarkers=33,parentMarkersRetained=505,bootsPerCohort=3,actualReloadsPerCohort=2,
            savedOriginalsPerCohort=4,defensiveProfilesPerCohort=3,legacyCombatControlsPerCohort=1,liveSupplierLevels=99,
            uncappedNpcResistance=True,sourcePoolsAndUnrelatedMods=True,durationAndMitigation=True,facetRescaling=True,profileRemoval=True,
            atomicUnknownAndAggregateOverflowRefusal=True,qualifiedAllocation=True,pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=[1000,21],
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=tested['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Custom native resistance getters/computation/caps are explicitly refused;positive item magic resistance/lightning-percent/flat-fire/flat-cold absorption are not new native fixture certificates.',
            'No frozen source pierce/stat/regen/event blanket activation;original-body native swing,registered events and public equipped cast remain unfinished.',
            'Existing source action/element/affix/projectile/hand-selection,full executable/AI/world/PvP/client and pending native action persistence limits remain.',
            'Optional Defiler tooltip observation unavailable;formula discrepancy remains uncertain and nonblocking.',
            'No new handler,completed class,deployment or assets;effects/audio and talent/hotkey icons remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/revive-defense-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Original Revive recipient defenses:508 broader/33 focused markers;505 parent retained;native pools/duration/mitigation/facets/reloads;public swing/events gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
