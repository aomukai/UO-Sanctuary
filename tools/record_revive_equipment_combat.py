#!/usr/bin/env python3
"""Bind actual original Revive frozen attack-rating/general-defense consumers."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/revive-equipment-combat'
FOCUS=ROOT/'.local/class-skills/revive-equipment-combat-focus'
PARENT='62704663ba4626bf623ed5eb287c654291142f84'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');focus=inventory.validate_inventory(FOCUS/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for folder,name in ((STAGE,'birth-inputs.json'),(FOCUS,'birth-inputs.json'),(STAGE,'audit-inputs.json')):snapshot(STAGE/'pre-build-4.json',folder/name)
    assert native['artifacts']==focus['artifacts']==audit['artifacts']and native['copiedData']==focus['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/frozen-numeric-admission';prior=json.loads((previous/'birth-inputs.json').read_text());assert native['copiedData']==prior['copiedData']
    changed={n for n in set(prior['sourceInputs'])|set(native['sourceInputs'])if n.endswith('.cs')and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}
    assert changed=={'src/Sanctuary.Content/'+n+'.cs'for n in ('ReviveEquipmentCombat','ReviveBeforeDeath','ReviveEquipmentLife','ReviveEquipmentResources','NpcEquipmentCombat','CreatureProfiles')}|{'tests/Sanctuary.Integration/'+n+'.cs'for n in ('ReviveEquipmentCombatChecks',)}
    assert native['sourceInputs']['src/Sanctuary.Content/ReviveCasts.cs']==prior['sourceInputs']['src/Sanctuary.Content/ReviveCasts.cs']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][n]==prior['artifacts'][n]
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/frozen-numeric-admission-native-evidence.json'],cwd=ROOT));baseline=previous/'birth-native-checks.log'
    assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))];expected=markers(baseline);assert sum(expected.values())==493
    expected['revive-equipment-combat-check']+=1;expected['revive-equipment-combat-reload-check']+=2
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==496
    focused={'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
             'revive-equipment-life-check':1,'revive-equipment-life-reload-check':2,'revive-equipment-pools-check':1,'revive-equipment-pools-reload-check':2,'frozen-equipment-combat-check':1,'frozen-equipment-combat-reload-check':2,'revive-equipment-combat-check':1,'revive-equipment-combat-reload-check':2}
    assert markers(FOCUS/'birth-native-checks.log')==focused
    for folder in (STAGE,FOCUS):
        text=(folder/'birth-native-checks.log').read_text();assert ': failed'not in text and ' ERR]'not in text and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
        assert text.count('Revive frozen numeric admission: malformed original restore refused atomically')==9
        assert text.count('Frozen numeric admission: unknown stat IDs refused')==3
        saved=json.loads((folder/'revive-equipment-combat-fixtures.json').read_text())
        assert all(len(saved[k])==3 for k in ('Actors','Attack','Defense','Life','Corpses'))
        assert [r['Setup'].get('EquipmentLifeProfile')for r in saved['Actors']]==['frozen-equipment-combat-v1','frozen-equipment-combat-v1','frozen-equipment-resources-v1']
        assert all(r['Owner']==saved['Owner']and r['Difficulty']==2 and life>0 for r,life in zip(saved['Actors'],saved['Life']))
        bases=[json.loads(r['Original']['BeforeDeathJson'])['AttackDefenseBases']for r in saved['Actors']]
        assert [r['Kind']for r in bases]==['native-attributes','source-body','native-attributes']
        assert all(bases[i]==dict(Version=0,Kind='native-attributes',AttackRating=122,Defense=15,Issue=None)for i in (0,2))
        assert bases[1]['Issue']is None and saved['Attack'][0]>saved['Attack'][2]and saved['Defense'][0]>saved['Defense'][2]
        gear=[json.loads(json.loads(r['Original']['BeforeDeathJson'])['EquipmentJson'])for r in saved['Actors']]
        assert all(len(items)==4 for items in gear)
    diagnostics={'diagnostic-native-offset-control':'Native equipment-offset control missing','diagnostic-direct-attack-control':'Flat/percent/pooled-Dex/armor controls were vacuous'}
    for name,message in diagnostics.items():
        diagnostic=STAGE/name;old=json.loads((diagnostic/'birth-inputs.json').read_text())
        assert message in(diagnostic/'birth-native-checks.log').read_text()
        for kind in ('Content','Integration'):assert sha(diagnostic/('Sanctuary.'+kind+'.dll'))==old['artifacts']['Assemblies/Sanctuary.'+kind+'.dll']
        assert sha(diagnostic/'ReviveEquipmentCombatChecks.cs')==old['sourceInputs']['tests/Sanctuary.Integration/ReviveEquipmentCombatChecks.cs']
        assert old['artifacts']['Assemblies/Sanctuary.Content.dll']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        assert old['copiedData']==native['copiedData']
    failed=json.loads((STAGE/'diagnostic-build-1/pre-build-1.json').read_text())
    assert sha(STAGE/'diagnostic-build-1/ReviveEquipmentCombatChecks.cs')==failed['inputs']['sourceInputs']['tests/Sanctuary.Integration/ReviveEquipmentCombatChecks.cs']
    assert 'Build FAILED.'in(STAGE/'integration-build-1.log').read_text()and all(code in(STAGE/'integration-build-1.log').read_text()for code in ('CS0019','CS0121'))
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-4.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',996),('first-loop-affix-audit.json',21)):
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
    files=[Path(__file__).resolve(),*(ROOT/'src/Sanctuary.Content'/(n+'.cs')for n in ('ReviveEquipmentCombat','ReviveBeforeDeath','ReviveEquipmentLife','ReviveEquipmentResources','NpcEquipmentCombat','CreatureProfiles')),
        *(ROOT/'tests/Sanctuary.Integration'/(n+'.cs')for n in ('ReviveEquipmentCombatChecks',)),
        *(ROOT/'tests'/n for n in ('check_revive_equipment_combat.py','check_revive_equipment_combat_focus.py')),
        *(ROOT/'docs'/n for n in ('revive-equipment-combat-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md')),
        *(STAGE/n for n in ('pre-build-1.json','pre-build-2.json','pre-build-3.json','pre-build-4.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log',
            'content-build-1.log','integration-build-1.log','content-build-2.log','integration-build-2.log','content-build-3.log','integration-build-3.log','content-build-4.log','integration-build-4.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','revive-equipment-combat-fixtures.json','frozen-equipment-combat-fixtures.json')),
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-3.log','revive-equipment-combat-fixtures.json','frozen-equipment-combat-fixtures.json')),
        *(p for name in (*diagnostics,'diagnostic-build-1')for p in sorted((STAGE/name).rglob('*'))if p.is_file())]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Prepared original Revive frozen attack rating/general defense;gear-free native bases and authored difficulty stats;public equipped admission remains gated',
        verified=dict(nativeMarkers=496,focusedMarkers=21,parentMarkersRetained=493,bootsPerCohort=3,actualReloadsPerCohort=2,
            savedOriginalsPerCohort=3,optInOriginals=2,legacyLifeOnlyOriginals=1,nativeAndEncounterOriginals=True,pooledDexterityRounding=True,actualHitBoundaries=True,shoutComposition=True,pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=[996,21],
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=native['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Weapon/elemental/poison damage, general/directional mitigation, duration defenses, stat attachment/events and full equipped Revive remain unfinished;no public gate is lifted.',
            'Missing historical gear-free bases, custom native Dexterity/armor getters and non-equipment native modifiers refuse the new profile;old default/resource-only behavior remains.',
            'This reuses accepted NPC source resource arithmetic;full current executable/world/PvP/client and actual old-binary/unheld snapshot cases remain separate.',
            'No new handler,completed class,deployment or assets;effects/audio and talent/hotkey icons remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/revive-equipment-combat-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Revive frozen attack/defense:496 broader/21 focused markers;493 parent retained;native/encounter originals,hit boundaries,Shout/facets/loot/reloads;public gate retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
