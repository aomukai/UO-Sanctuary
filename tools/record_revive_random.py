#!/usr/bin/env python3
"""Bind original Revive source-stream continuation and lifecycle acceptance."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/revive-random'
FOCUS=ROOT/'.local/class-skills/revive-random-focus'
PARENT='78cc26e441de84b869de2b3828d57fcd76fcc666'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    tested=inventory.validate_inventory(STAGE/'birth-inputs.json');focus=inventory.validate_inventory(FOCUS/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for folder,name in ((STAGE,'birth-inputs.json'),(FOCUS,'birth-inputs.json'),(STAGE,'audit-inputs.json')):snapshot(STAGE/'pre-build-2.json',folder/name)
    assert tested['artifacts']==focus['artifacts']==audit['artifacts']and tested['copiedData']==focus['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/revive-equipment-offense';prior=json.loads((previous/'birth-inputs.json').read_text())
    assert tested['copiedData']==prior['copiedData']
    changed={p for p in set(tested['sourceInputs'])|set(prior['sourceInputs'])if p.endswith('.cs')and tested['sourceInputs'].get(p)!=prior['sourceInputs'].get(p)}
    sources={'src/Sanctuary.Content/'+p+'.cs'for p in ('NpcEquipmentRandom','ReviveEquipmentLife','ReviveEquipmentOffense','ReviveNativeActivation')}|{'tests/Sanctuary.Integration/ReviveRandomChecks.cs'}
    assert changed==sources
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/revive-equipment-offense-native-evidence.json'],cwd=ROOT))
    baseline=previous/'birth-native-checks.log';assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);assert sum(expected.values())==499
    expected['revive-random-check']+=1;expected['revive-random-reload-check']+=2
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==502
    old_focus=previous.with_name('revive-equipment-offense-focus')/'birth-native-checks.log'
    assert sha(old_focus)==parent['evidence'][str(old_focus.relative_to(ROOT))]
    expected_focus=markers(old_focus);assert sum(expected_focus.values())==24
    expected_focus['revive-random-check']+=1;expected_focus['revive-random-reload-check']+=2
    assert markers(FOCUS/'birth-native-checks.log')==expected_focus and sum(expected_focus.values())==27
    for folder in (STAGE,FOCUS):
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'revive-random-fixtures.json').read_text());assert saved['Generation']==2 and all(len(saved[k])==3 for k in ('Actors','Streams','Next','After'))
        scenes=json.loads((folder/'revive-equipment-offense-fixtures.json').read_text())['Scenes'];originals={r['Actor']:r['State']['Original']for r in scenes}
        assert saved['Actors']==[r['Actor']for r in scenes]
        for actor,row,next_value,after in zip(saved['Actors'],saved['Streams'],saved['Next'],saved['After']):
            original=originals[actor]
            assert row['Actor']==actor and row['Owner']==scenes[0]['State']['Owner']and row['Item']==0 and not row.get('Unarmed',False)
            assert row['ReviveCorpse']==original['Corpse']and row['ReviveBinding']==hashlib.sha256(original['BeforeDeathJson'].encode()).hexdigest()
            expected_after=0x6AC690C5*(row['Seed']&0xffffffff)+(row['Seed']>>32)
            assert after==expected_after and next_value==expected_after&0xffffffff
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-2.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',998),('first-loop-affix-audit.json',21)):
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
        *(ROOT/'tests'/name for name in ('check_revive_random.py','check_revive_random_focus.py')),
        *(ROOT/'docs'/name for name in ('revive-random-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md')),
        *(STAGE/name for name in ('pre-build-1.json','pre-build-2.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','content-build-1.log','content-build-2.log','integration-build-2.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','revive-random-fixtures.json','revive-equipment-offense-fixtures.json')),
        *(FOCUS/name for name in ('birth-inputs.json','birth-native-checks.log','native-run-1.log','revive-random-fixtures.json','revive-equipment-offense-fixtures.json'))]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Persist original/corpse/death-snapshot source RNG binding;default offensive roll;loot-independent continuation and cleanup;native swing/event/public equipped delivery remains gated',
        verified=dict(nativeMarkers=502,focusedMarkers=27,parentMarkersRetained=499,bootsPerCohort=3,actualReloadsPerCohort=2,
            savedOriginalStreamsPerCohort=3,rawComparisonsPerOriginal=256,continuedGenerations=2,pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=[998,21],
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=tested['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['No original D2 world/AI seed or full random chronology is recovered;existing source-algorithm initialization policy is explicit.',
            'Native original-body swings,qualified impact/mitigation/events,skill/stat attachment and public equipped casting remain unfinished.',
            'Existing attack2/elemental-body/affix/projectile/hand-selection and full executable/world/PvP/client/old-binary/unheld snapshot limits remain.',
            'No new handler,completed class,deployment or assets;effects/audio and talent/hotkey icons remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/revive-random-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Original Revive streams:502 broader/27 focused markers;499 parent retained;three streams/two exact continuation reloads;loot/facets/parking/replacement/death;delivery gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
