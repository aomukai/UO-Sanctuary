#!/usr/bin/env python3
"""Bind frozen original offensive rolls, current source metadata and Extra Strong pooling."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/revive-equipment-offense'
FOCUS=ROOT/'.local/class-skills/revive-equipment-offense-focus'
PARENT='46b80371e9528db1ed5623f3bd9e9a6415919150'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');focus=inventory.validate_inventory(FOCUS/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for folder,name in ((STAGE,'birth-inputs.json'),(FOCUS,'birth-inputs.json'),(STAGE,'audit-inputs.json')):snapshot(STAGE/'pre-build-4.json',folder/name)
    assert native['artifacts']==focus['artifacts']==audit['artifacts']and native['copiedData']==focus['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/revive-equipment-combat';prior=json.loads((previous/'birth-inputs.json').read_text())
    added={'Data/Sanctuary/Balance/revive-offense-sources.json'}
    assert set(native['copiedData'])==set(prior['copiedData'])|added and all(native['copiedData'][p]==h for p,h in prior['copiedData'].items())
    changed={n for n in set(prior['sourceInputs'])|set(native['sourceInputs'])if n.endswith('.cs')and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}
    assert changed=={'src/Sanctuary.Content/'+n+'.cs'for n in ('ReviveEquipmentCombat','ReviveEquipmentOffense','NpcEquipmentDamage','NpcEquipmentElements')}|{'tests/Sanctuary.Integration/'+n+'.cs'for n in ('ReviveEquipmentCombatChecks','ReviveEquipmentOffenseChecks')}
    assert native['sourceInputs']['src/Sanctuary.Content/ReviveCasts.cs']==prior['sourceInputs']['src/Sanctuary.Content/ReviveCasts.cs']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][n]==prior['artifacts'][n]
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/revive-equipment-combat-native-evidence.json'],cwd=ROOT));baseline=previous/'birth-native-checks.log'
    assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))];expected=markers(baseline);assert sum(expected.values())==496
    expected['revive-equipment-offense-check']+=1;expected['revive-equipment-offense-reload-check']+=2
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==499
    focused={'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
             'revive-equipment-life-check':1,'revive-equipment-life-reload-check':2,'revive-equipment-pools-check':1,'revive-equipment-pools-reload-check':2,'frozen-equipment-combat-check':1,'frozen-equipment-combat-reload-check':2,'revive-equipment-combat-check':1,'revive-equipment-combat-reload-check':2,'revive-equipment-offense-check':1,'revive-equipment-offense-reload-check':2}
    assert markers(FOCUS/'birth-native-checks.log')==focused
    for folder in (STAGE,FOCUS):
        text=(folder/'birth-native-checks.log').read_text();assert ': failed'not in text and ' ERR]'not in text and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
        assert text.count('Revive frozen numeric admission: malformed original restore refused atomically')==9
        assert text.count('Frozen numeric admission: unknown stat IDs refused')==3
        saved=json.loads((folder/'revive-equipment-offense-fixtures.json').read_text());assert len(saved['Scenes'])==3
        assert [r['Snapshot']['Physical']['Weapon']for r in saved['Scenes']]==[True,True,False]
        assert [r['Snapshot']['Physical']['StrengthCoefficient']for r in saved['Scenes']]==[100,110,0]
        for scene in saved['Scenes']:
            row=scene['State'];capture=scene['Snapshot']
            assert row['Owner']==saved['Owner']and row['Actor']==scene['Actor']and row['Difficulty']==2 and row['Setup']['EquipmentLifeProfile']=='frozen-equipment-combat-v1'
            assert capture['Actor']==scene['Actor']and capture['Target']==saved['Target']and capture['Source']=='skeleton1'and capture['AttackSlot']==1
            assert capture['Critical']['Deadly']>0 and capture['Critical']['Intrinsic']>0 and len(scene['Items'])==4 and all(len(item['Levels'])==99 for item in scene['Items'])
        assert [r['Snapshot']['Poison']['Stats']['PoisonCount']for r in saved['Scenes']]==[2,2,1]
        assert [r['Snapshot']['Poison']['Stats']['PoisonImpactFrames']for r in saved['Scenes']]==[87,87,75]
    diagnostics={'diagnostic-parent-extra-strong':'Revive base included frozen equipment or wrong difficulty','diagnostic-summon-only-catalogue':"The given key 'skeleton1' was not present in the dictionary."}
    for name,message in diagnostics.items():
        diagnostic=STAGE/name;old=json.loads((diagnostic/'birth-inputs.json').read_text())
        assert message in(diagnostic/'birth-native-checks.log').read_text()
        for kind in ('Content','Integration'):assert sha(diagnostic/('Sanctuary.'+kind+'.dll'))==old['artifacts']['Assemblies/Sanctuary.'+kind+'.dll']
    baseline=STAGE/'diagnostic-parent-extra-strong';old=json.loads((baseline/'birth-inputs.json').read_text())
    assert old['artifacts']['Assemblies/Sanctuary.Content.dll']==prior['artifacts']['Assemblies/Sanctuary.Content.dll']
    for name,path in (('ReviveEquipmentCombat.cs','src/Sanctuary.Content/ReviveEquipmentCombat.cs'),('ReviveEquipmentCombatChecks.cs','tests/Sanctuary.Integration/ReviveEquipmentCombatChecks.cs')):assert sha(baseline/name)==old['sourceInputs'][path]
    broken=STAGE/'diagnostic-summon-only-catalogue';old=json.loads((broken/'birth-inputs.json').read_text())
    assert sha(broken/'ReviveEquipmentOffense.cs')==old['sourceInputs']['src/Sanctuary.Content/ReviveEquipmentOffense.cs']
    import csv
    from build_revive_offense_sources import build
    build(True)
    observation=json.loads((ROOT/'tools/reference/revive-offense-source-observation.json').read_text())
    assert observation['referenceVersion']=='3.3.93847'and observation['comparedRows']==751
    owned=Path(observation['ownedPath']);assert sha(owned)==observation['ownedSha256']and sha(ROOT/observation['currentPath'])==observation['currentSha256']and sha(ROOT/observation['runtimePath'])==observation['runtimeSha256']
    raw={r['Id']:r for r in csv.DictReader(owned.read_text(encoding=observation['encoding']).splitlines(),delimiter='\t')if r.get('Id')}
    for row in json.loads((ROOT/observation['runtimePath']).read_text()):
        original=raw[row['source']];assert row['critical']==int(original.get('Crit')or 0)
        assert row['elements']==[dict(mode=original.get(f'El{i}Mode')or'',type=original.get(f'El{i}Type')or'')for i in range(1,4)]
    unique=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/MONSTER/MonsterUnique.cpp'
    strong=unique.read_text().split('void __fastcall MONSTERUNIQUE_UMod5_Strong(',1)[1].split('//D2Game.',1)[0]
    assert all('STATLIST_AddUnitStat(pUnit, '+stat in strong for stat in ('STAT_DAMAGEPERCENT','STAT_ITEM_TOHIT_PERCENT'))
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-4.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',998),('first-loop-affix-audit.json',21)):
        new=json.loads((STAGE/name).read_text());assert new==json.loads((ROOT/'tools/reference'/name).read_text())and len(new['InputHashes'])==count
        assert new['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in new['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in old.items()if k not in skip}=={k:v for k,v in new.items()if k not in skip}
    assert(STAGE/'audit-native-checks.log').read_text().count(': passed')==2
    assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
    for name,text in (('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in(STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key,relative,count in (('saves','.local/server/Saves',38),('configuration','.local/server/Configuration',20)):
        folder=Path(installed['runtimeRoot'])/relative;assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    state=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not state['completedClasses']and sum(r['implementation']=='handler-present'for r in state['skills'])==191
    files=[Path(__file__).resolve(),*(ROOT/'src/Sanctuary.Content'/(n+'.cs')for n in ('ReviveEquipmentCombat','ReviveEquipmentOffense','NpcEquipmentDamage','NpcEquipmentElements')),
        *(ROOT/'tests/Sanctuary.Integration'/(n+'.cs')for n in ('ReviveEquipmentCombatChecks','ReviveEquipmentOffenseChecks')),
        *(ROOT/'tests'/n for n in ('check_revive_equipment_offense.py','check_revive_equipment_offense_focus.py')),
        *(ROOT/'docs'/n for n in ('revive-equipment-offense-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md')),
        *(STAGE/n for n in ('pre-build-1.json','pre-build-2.json','pre-build-3.json','pre-build-4.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log',
            'integration-build-1.log','content-build-2.log','content-build-3.log','integration-build-3.log','content-build-4.log','integration-build-4.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','revive-equipment-offense-fixtures.json','frozen-equipment-combat-fixtures.json')),
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-3.log','revive-equipment-offense-fixtures.json','frozen-equipment-combat-fixtures.json')),
        *(p for name in diagnostics for p in sorted((STAGE/name).rglob('*'))if p.is_file()),ROOT/'tools/build_revive_offense_sources.py',ROOT/'tools/reference/revive-offense-source-observation.json',ROOT/observation['runtimePath'],unique]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Correct Extra Strong/item percentage pooling;capture and roll original-body attack1 frozen physical/elemental/poison/critical inputs;original swing delivery and public equipped admission remain gated',
        verified=dict(nativeMarkers=499,focusedMarkers=24,parentMarkersRetained=496,bootsPerCohort=3,actualReloadsPerCohort=2,
            savedOffensiveOriginalsPerCohort=3,oneTwoHandedAndUnarmed=True,sourceRowsCompared=751,liveItemLevelsPerItem=99,seedReplaysPerSceneCheck=32,extraStrongParentFailureReproduced=True,pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=[998,21],
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=native['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['This offensive snapshot/roll is not yet wired into original-body swings, packet/contact events, generic stream persistence or full mitigation;no public equipped gate is lifted.',
            'Attack2, mixed/innate elemental bodies, elemental affixes, multiple weapons and projectile routing need explicit adapters;skill-derived critical/mastery and stat attachment remain unfinished.',
            'This reuses accepted NPC source damage arithmetic;full current executable/world/PvP/client and actual old-binary/unheld snapshot cases remain separate.',
            'No new handler,completed class,deployment or assets;effects/audio and talent/hotkey icons remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/revive-equipment-offense-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Revive frozen offense:499 broader/24 focused markers;496 parent retained;751 owned rows;one/two-hand/unarmed snapshots,crit/poison/Enchant/reloads;Extra Strong corrected;delivery gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
