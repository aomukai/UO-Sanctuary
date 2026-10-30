#!/usr/bin/env python3
"""Record the shared non-armor defense fix without claiming full NPC support."""
import argparse
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_revive_regeneration import checked_log, markers
from snapshot_skill_build import check as check_snapshot

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills/npc-equipment-defense'
PREDECESSOR = '1ec4cb1'
sha = inventory.sha


def historical(name):
    return subprocess.check_output(['git','show',PREDECESSOR+':'+name],cwd=ROOT)


def record(check=False):
    inventory.STAGE=STAGE
    focused=inventory.validate_inventory(STAGE/'defense-inputs.json')
    retained=inventory.validate_inventory(STAGE/'stats-inputs.json')
    for name in ['defense-inputs.json','stats-inputs.json']:
        check_snapshot(STAGE/'pre-build-attempt-1.json',STAGE/name)
    assert focused['artifacts']==retained['artifacts'] and focused['copiedData']==retained['copiedData']
    prior=json.loads(historical('tools/reference/revive-facets-evidence.json'))
    assert prior['verified']['retainedMarkers']==366 and prior['verified']['fullClassMarkers']==1184
    old=prior['inputInventory']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert old['artifacts'][name]==retained['artifacts'][name]
        assert sha(STAGE/'native/Distribution'/name)==retained['artifacts'][name]
    assert old['copiedData']==retained['copiedData']
    previous_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    current_cs={p:h for p,h in retained['sourceInputs'].items() if p.endswith('.cs')}
    assert set(current_cs)-set(previous_cs)=={'tests/Sanctuary.Integration/NpcEquipmentDefenseChecks.cs'}
    assert {p for p in previous_cs if previous_cs[p]!=current_cs[p]}=={'src/Sanctuary.Content/ClassGear.cs'}
    old_gear=historical('src/Sanctuary.Content/ClassGear.cs').decode()
    assert old_gear.count('if (basis.MaxDefense <= 0) { return 0; }')==1
    assert (ROOT/'src/Sanctuary.Content/ClassGear.cs').read_text()==old_gear.replace('if (basis.MaxDefense <= 0) { return 0; }','if (basis.MaxDefense <= 0) { return Bonus(item, "ac", recipientLevel); }')
    scope=checked_log(STAGE/'defense-native-checks.log',3)
    complete=checked_log(STAGE/'stats-native-checks.log',369)
    old_log=ROOT/'.local/class-skills/revive-facets/stats-native-checks.log'
    assert sha(old_log)==prior['artifacts'][str(old_log.relative_to(ROOT))]
    assert complete==markers(old_log.read_text())+scope
    source_path=ROOT/'tools/reference/npc-equipment-defense-source-evidence.json'
    source=json.loads(source_path.read_text());assert source['cases']==648
    for name,value in source['artifacts'].items():assert sha(ROOT/name)==value
    cases=ROOT/'.local/class-skills/npc-equipment-defense-source/cases.json'
    assert sha(cases)==sha(ROOT/'.local/class-skills/npc-equipment-defense-source-cases.json')
    assert len(json.loads(cases.read_text()))==648
    paths=[Path(__file__).resolve(),source_path,cases,ROOT/'tests/check_npc_equipment_defense.py',ROOT/'tests/check_npc_equipment_defense_regression.py',ROOT/'tools/probe_npc_equipment_defense.py']
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build-attempt-1.log')
        assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        paths.extend([p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')])
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text())
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert a['ContentAssemblySha256']==retained['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,value in a['InputHashes'].items():assert sha(ROOT/path)==value
        audits.append(a);paths.extend([p,ROOT/'tools/reference'/name])
    assert [len(a['InputHashes']) for a in audits]==[785,21]
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==json.loads(historical('tools/reference/named-item-runtime-audit.json'))['Definitions']
    python=STAGE/'python-tests-attempt-1.log'
    assert 'Ran 71 tests' in python.read_text() and '\nOK\n' in python.read_text() and 'FAILED' not in python.read_text()
    installed_path=ROOT/'tools/reference/creeper-frame-installation-evidence.json';installed=json.loads(installed_path.read_text())
    assert len(installed['installedArtifacts'])==106
    for name,value in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==value
    paths.extend([python,installed_path])
    paths.extend(STAGE/n for n in ['pre-build-attempt-1.json','defense-inputs.json','stats-inputs.json','defense-native-checks.log','stats-native-checks.log','isolated-npc-equipment-defense-fixtures.json','npc-equipment-defense-fixtures.json','retained-run-attempt-1.log'])
    result=dict(referenceVersion='3.3.93847',component='Shared non-armor flat defense supplier',installed=False,goalComplete=False,completedClasses=[],
        verified=dict(sourceCases=648,focusedMarkers=3,retainedMarkers=369,bootsPerScope=3,actualReloadsPerScope=2,pythonTests=71,inputFingerprints=[785,21],unchangedNamedDefinitions=573,unchangedInstalledArtifacts=106),
        frozenPredecessor=dict(commit=PREDECESSOR,receipt='tools/reference/revive-facets-evidence.json',fullClassMarkers=1184,scope='Historical full acceptance only; no new full-class run is claimed for this one-line reader fix.'),
        inputInventory=retained,focusedInputInventory=focused,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Only the no-intrinsic-armor branch changes: it returns the existing recipient-level flat ac supplier. Actual Ribcracker now supplies100through equipped player defense and original/frozen numeric readers.',
                     '648complete older-source stat-list/defense contexts compose for player/monster kinds. Actual broken/unidentified/unequipped cases and original identity/full rolls/frozen payload survive two native reloads.'],
        remaining=['No full NPC stat or item-event/proc certificate and no learned Iron Golem handler. Revive continues to explicitly reject unsupported equipped effects.',
                   'Older source composition is not modern executable parity or a complete item-birth proof. Existing armor/ED rounding is unchanged; no new global percentage behavior is inferred.',
                   'Complete NPC equipment integration, Sunder/source-resistance gaps and separate final effects/audio/icons remain unfinished.'])
    p=ROOT/'tools/reference/npc-equipment-defense-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==text
    else:p.write_text(text)
    print('Non-armor defense:648source/3focused/369retained/71Python;scoped supplier acceptance only.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
