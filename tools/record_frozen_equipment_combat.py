#!/usr/bin/env python3
"""Bind independent frozen combat suppliers and retained actual Revive scopes."""
import argparse,hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/frozen-equipment-combat'
FOCUS=ROOT/'.local/class-skills/frozen-equipment-combat-focus'
PARENT='6e5e00608d97e11bf22974568a77932e67213b58'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json');focus=inventory.validate_inventory(FOCUS/'birth-inputs.json');audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for folder,name in ((STAGE,'birth-inputs.json'),(FOCUS,'birth-inputs.json'),(STAGE,'audit-inputs.json')):snapshot(STAGE/'pre-build-6.json',folder/name)
    assert native['artifacts']==focus['artifacts']==audit['artifacts']and native['copiedData']==focus['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/revive-encounter-facet';prior=json.loads((previous/'birth-inputs.json').read_text())
    assert native['copiedData']==prior['copiedData']
    changed={n for n in set(prior['sourceInputs'])|set(native['sourceInputs'])if n.endswith('.cs')and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}
    assert changed=={'src/Sanctuary.Content/FrozenEquipmentEffects.cs','src/Sanctuary.Content/FrozenEquipmentCombat.cs','tests/Sanctuary.Integration/FrozenEquipmentCombatChecks.cs'}
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][n]==prior['artifacts'][n]
    # Public equipped Revive and live Iron item consumers have not been
    # silently enabled or rewritten by a capture/transport component.
    for name in ('ReviveCasts','NpcEquipmentCombat','ItemStatContext'):
        path='src/Sanctuary.Content/'+name+'.cs';assert sha(ROOT/path)==prior['sourceInputs'][path]
    parent=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/revive-encounter-facet-native-evidence.json'],cwd=ROOT))
    baseline=previous/'birth-native-checks.log';assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline);assert sum(expected.values())==484
    expected['frozen-equipment-combat-check']+=1;expected['frozen-equipment-combat-reload-check']+=2
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==487
    focused={'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
             'frozen-equipment-combat-check':1,'frozen-equipment-combat-reload-check':2,'revive-before-death-check':1,'revive-before-death-reload-check':2}
    assert markers(FOCUS/'birth-native-checks.log')==focused
    for folder in (STAGE,FOCUS):
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        rows=json.loads((folder/'frozen-equipment-combat-fixtures.json').read_text());assert len(rows)==6
        assert {r['Name']for r in rows}=={'Crown of Thieves','Harlequin Crest','Frostburn',"Baranar's Star",'Blacktongue','Ripsaw'}
        assert len({r['Snapshot']['OriginalItem']for r in rows})==6
        assert any(r['NumericIssue']for r in rows)and any(r['NumericIssue']is None and r['Numeric']for r in rows)
        for row in rows:
            data=json.loads(row['Snapshot']['CombatJson']);assert data['Version']==0 and data['Issue']is None and len(data['Levels'])==99
            assert data['Basis']==row['Basis']and data['Levels']==row['Levels']and data['Numeric']==row['Numeric']and data['NumericIssue']==row['NumericIssue']
            assert [r['Level']for r in data['Levels']]==list(range(1,100))
        # These original accepted-death records carry the optional new field
        # through the ordinary world persistence,not just a standalone fixture.
        before=json.loads((folder/'revive-before-death-fixtures.json').read_text());assert len(before)==3
        for row in before:
            assert len(row['Equipment'])==2 and all(e.get('CombatJson')is not None for e in row['Equipment'])
    diagnostics={'fireskill-diagnostic':'unsupported original properties: fireskill','cold-raw-diagnostic':'unsupported original properties: dmg-cold',
                 'invalid-material-fixture':'Invalid converted equipment identity or level.','native-item-fixture':'Unconverted native equipment invented combat suppliers'}
    for name,message in diagnostics.items():
        folder=STAGE/name;inputs=json.loads((folder/'birth-inputs.json').read_text());assert message in(folder/'birth-native-checks.log').read_text()
        for kind in ('content','integration'):
            dll='Sanctuary.'+kind.title()+'.dll';assert sha(folder/kind/dll)==inputs['artifacts']['Assemblies/'+dll]
        assert sha(folder/'FrozenEquipmentCombatChecks.cs')==inputs['sourceInputs']['tests/Sanctuary.Integration/FrozenEquipmentCombatChecks.cs']
    for name in ('invalid-material-fixture','native-item-fixture'):
        assert sha(STAGE/name/'content/Sanctuary.Content.dll')==native['artifacts']['Assemblies/Sanctuary.Content.dll']
    for kind in ('content','integration'):
        assert all(s in(STAGE/(kind+'-build-6.log')).read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    for name,count in (('named-item-runtime-audit.json',993),('first-loop-affix-audit.json',21)):
        new=json.loads((STAGE/name).read_text());assert new==json.loads((ROOT/'tools/reference'/name).read_text())and len(new['InputHashes'])==count
        assert new['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,digest in new['InputHashes'].items():assert sha(ROOT/p)==digest,p
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in old.items()if k not in skip}=={k:v for k,v in new.items()if k not in skip}
    assert(STAGE/'audit-native-checks.log').read_text().count(': passed')==2
    assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
    for name,text in (('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert text in(STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==digest,p
    protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key,relative,count in (('saves','.local/server/Saves',38),('configuration','.local/server/Configuration',20)):
        folder=Path(installed['runtimeRoot'])/relative;assert len(protected[key])==count and {str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    state=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not state['completedClasses']and sum(r['implementation']=='handler-present'for r in state['skills'])==191
    files=[Path(__file__).resolve(),ROOT/'src/Sanctuary.Content/FrozenEquipmentEffects.cs',ROOT/'src/Sanctuary.Content/FrozenEquipmentCombat.cs',ROOT/'tests/Sanctuary.Integration/FrozenEquipmentCombatChecks.cs',
        *(ROOT/'tests'/n for n in ('check_frozen_equipment_combat.py','check_frozen_equipment_combat_focus.py')),
        *(ROOT/'docs'/n for n in ('frozen-equipment-combat-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md')),
        *(STAGE/n for n in ('pre-build-1.json','pre-build-2.json','pre-build-3.json','pre-build-4.json','pre-build-5.json','pre-build-6.json','birth-inputs.json','birth-native-checks.log','native-run-1.log',
            'audit-inputs.json','audit-native-checks.log','audit-run-1.log','content-build-6.log','integration-build-6.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log',
            'frozen-equipment-combat-fixtures.json','revive-before-death-fixtures.json')),
        *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-5.log','frozen-equipment-combat-fixtures.json','revive-before-death-fixtures.json')),
        *(p for name in diagnostics for p in sorted((STAGE/name).rglob('*'))if p.is_file())]
    result=dict(schemaVersion=1,referenceVersion='D2R Reign of the Warlock 3.3.93847',parent=PARENT,
        scope='Immutable transport of existing live NPC item suppliers;optional V0 JSON field;typed/raw capabilities remain distinct',
        verified=dict(nativeMarkers=487,focusedMarkers=15,parentMarkersRetained=484,bootsPerCohort=3,actualReloadsPerCohort=2,
            sourceItems=6,recipientLevelsPerItem=99,acceptedDeathRecordsPerCohort=3,pythonTests=71,namedDecisionsUnchanged=573,
            auditFingerprints=[993,21],installedArtifactsUnchanged=106,savesUnchanged=38,configurationsUnchanged=20,handlers=191),
        artifacts=native['artifacts'],evidence={str(p.relative_to(ROOT)):sha(p)for p in files},
        limitations=['No equipped Revive activation,stat attachment or new NPC event/AI/weapon consumer is enabled;its existing public gate remains.',
            'Raw fireskill/dmg-cold omissions remain explicit;typed suppliers can exist independently without inventing a complete raw stat list.',
            'Historical snapshots retain basic suppliers but do not infer missing combat data;unconverted source-less items remain unavailable.',
            'This transports existing getter values,not a new current-executable/whole-source arithmetic,client or actual old-binary upgrade certificate.',
            'No new handler,completed class,deployment or assets;effects/audio and talent/hotkey icons stay separate.'])
    encoded=json.dumps(result,indent=2)+'\n';target=ROOT/'tools/reference/frozen-equipment-combat-native-evidence.json'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Frozen combat suppliers:487broader/15focused markers;484parent retained;99levels and original-death reloads;public equipment gates remain.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
