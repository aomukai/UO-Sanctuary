#!/usr/bin/env python3
"""Bind opt-in original Revive frozen equipment life consumers."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/revive-equipment-life'
FOCUS=ROOT/'.local/class-skills/revive-equipment-life-focus'
PARENT='11ef2781cee395c141912ac84a23841968e51f9f'
sha=inventory.sha
def record(check=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');focus=inventory.validate_inventory(FOCUS/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for folder,name in ((STAGE,'birth-inputs.json'),(FOCUS,'birth-inputs.json'),(STAGE,'audit-inputs.json')):snapshot(STAGE/'pre-build-3.json',folder/name)
    assert native['artifacts']==focus['artifacts']==audit['artifacts']and native['copiedData']==focus['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/frozen-equipment-combat';prior=json.loads((previous/'birth-inputs.json').read_text());assert native['copiedData']==prior['copiedData']
    changed={n for n in set(prior['sourceInputs'])|set(native['sourceInputs'])if n.endswith('.cs')and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}
    assert changed=={'src/Sanctuary.Content/'+n+'.cs'for n in ('ReviveEquipmentLife','ReviveNativeActivation','ReviveLife')}|{'tests/Sanctuary.Integration/'+n+'.cs'for n in ('ReviveEquipmentLifeChecks','FrozenEquipmentCombatChecks')}
    assert native['sourceInputs']['src/Sanctuary.Content/ReviveCasts.cs']==prior['sourceInputs']['src/Sanctuary.Content/ReviveCasts.cs']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][n]==prior['artifacts'][n]
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/frozen-equipment-combat-native-evidence.json'],cwd=ROOT));baseline=previous/'birth-native-checks.log'
    assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))];expected=markers(baseline);assert sum(expected.values())==487
    expected['revive-equipment-life-check']+=1;expected['revive-equipment-life-reload-check']+=2
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==490
    focused={'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
             'revive-equipment-life-check':1,'revive-equipment-life-reload-check':2,'frozen-equipment-combat-check':1,'frozen-equipment-combat-reload-check':2}
    assert markers(FOCUS/'birth-native-checks.log')==focused
    for folder in (STAGE,FOCUS):
        text=(folder/'birth-native-checks.log').read_text();assert ': failed'not in text and ' ERR]'not in text and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
        saved=json.loads((folder/'revive-equipment-life-fixtures.json').read_text());assert len(saved['Actors'])==len(saved['Life'])==len(saved['Corpses'])==3 and len(saved['DeletedItems'])==6
        assert [r['Setup'].get('EquipmentLifeProfile')for r in saved['Actors']]==['frozen-equipment-life-v1','frozen-equipment-life-v1',None]
        assert all(r['Owner']==saved['Owner']and r['Difficulty']==2 and life>0 for r,life in zip(saved['Actors'],saved['Life']))
    diagnostics={'unavailable-named-fixture':'That named definition is not available.','serial-reuse-diagnostic':'Deleted original loot reappeared'}
    for name,message in diagnostics.items():
        folder=STAGE/name;old=json.loads((folder/'birth-inputs.json').read_text());assert message in(folder/'birth-native-checks.log').read_text()
        for kind in ('content','integration'):
            dll='Sanctuary.'+kind.title()+'.dll';assert sha(folder/kind/dll)==old['artifacts']['Assemblies/'+dll]
        assert old['artifacts']['Assemblies/Sanctuary.Content.dll']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
    for kind in ('content','integration'):assert all(t in(STAGE/(kind+'-build-3.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',994),('first-loop-affix-audit.json',21)):
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
    files=[Path(__file__).resolve(),*(ROOT/'src/Sanctuary.Content'/(n+'.cs')for n in ('ReviveEquipmentLife','ReviveNativeActivation','ReviveLife')),
        *(ROOT/'tests/Sanctuary.Integration'/(n+'.cs')for n in ('ReviveEquipmentLifeChecks','FrozenEquipmentCombatChecks')),
        *(ROOT/'tests'/n for n in ('check_revive_equipment_life.py','check_revive_equipment_life_focus.py')),
        *(ROOT/'docs'/n for n in ('revive-equipment-life-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md')),
        *(STAGE/n for n in ('pre-build-1.json','pre-build-2.json','pre-build-3.json','birth-inputs.json','birth-native-checks.log','native-run-1.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log',
            'content-build-3.log','integration-build-3.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','revive-equipment-life-fixtures.json')),
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-3.log','revive-equipment-life-fixtures.json')),
        *(p for name in diagnostics for p in sorted((STAGE/name).rglob('*'))if p.is_file())]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Opt-in prepared original Revive frozen item life;shared existing NPC resource graph;public equipment admission remains gated',
        verified=dict(nativeMarkers=490,focusedMarkers=15,parentMarkersRetained=487,bootsPerCohort=3,actualReloadsPerCohort=2,
            savedOriginalsPerCohort=3,optInOriginals=2,legacyDefaultOriginals=1,pythonTests=71,namedDecisionsUnchanged=573,auditFingerprints=[994,21],
            installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=native['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['Mana/stamina,offense/defense/resistance/event/stat attachment and full equipped Revive remain unfinished;no public gate is lifted.',
            'No positive live item-HP-percentage original was tested:available named HP-percent items remain blocked by their existing unrelated skill properties;BO percentage is positive.',
            'This reuses accepted NPC source resource arithmetic;full current executable/world/PvP/client and actual old-binary/unheld snapshot cases remain separate.',
            'Original gear deletion is observed before saving;native serial numbers may be reused,so reload proves frozen values and no item creation rather than global serial absence.',
            'No new handler,completed class,deployment or assets;effects/audio and talent/hotkey icons remain separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/revive-equipment-life-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Equipped Revive life:490broader/15focused markers;487parent retained;opt-in/legacy originals,BO/facets/loot/reloads;public gate retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
