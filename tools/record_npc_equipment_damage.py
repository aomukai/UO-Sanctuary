#!/usr/bin/env python3
"""Record source-composed NPC physical damage without claiming all item effects."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_revive_regeneration import checked_log,markers
from snapshot_skill_build import check as check_snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-equipment-damage'
PREDECESSOR='ee1ff2d'
sha=inventory.sha

def historical(name):
    return json.loads(subprocess.check_output(['git','show',PREDECESSOR+':'+name],cwd=ROOT))

def record(check=False):
    inventory.STAGE=STAGE;paths=[Path(__file__).resolve(),STAGE/'pre-build-attempt-6.json']
    scopes=[]
    for name in ['damage-inputs.json','stats-inputs.json','audit-inputs.json']:
        p=STAGE/name;scopes.append(inventory.validate_inventory(p));check_snapshot(STAGE/'pre-build-attempt-6.json',p);paths.append(p)
    focused,retained,audited=scopes
    assert all(x['artifacts']==focused['artifacts'] and x['copiedData']==focused['copiedData'] for x in scopes)
    prior=historical('tools/reference/npc-equipment-combat-evidence.json');old=prior['inputInventory']
    assert old['copiedData']==focused['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert old['artifacts'][name]==focused['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};new_cs={p:h for p,h in focused['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new_cs)-set(old_cs)=={'src/Sanctuary.Content/NpcEquipmentDamage.cs','tests/Sanctuary.Integration/NpcEquipmentDamageChecks.cs'}
    assert {p for p in old_cs if old_cs[p]!=new_cs[p]}=={'src/Sanctuary.Content/ClassGear.cs','src/Sanctuary.Content/SkeletonPets.cs','src/Sanctuary.Content/NecromancerSummons.cs','src/Sanctuary.Content/NpcEquipmentCombat.cs'}
    scope=checked_log(STAGE/'damage-native-checks.log',3)
    assert scope==Counter({'npc-equipment-damage-check':1,'npc-equipment-damage-reload-check':2})
    complete=checked_log(STAGE/'stats-native-checks.log',374)
    previous=ROOT/'.local/class-skills/npc-equipment-combat/stats-native-checks.log'
    assert sha(previous)==prior['artifacts'][str(previous.relative_to(ROOT))]
    assert complete==markers(previous.read_text())+scope
    weapons=ROOT/'tools/reference/d2r-named-equipment/json/weapons.json'
    metal=[r for r in json.loads(weapons.read_text()).values() if r.get('bitfield1',0)&2]
    assert len(metal)==200 and all((r.get('2handmindam',0) if r.get('2handed',0) or r.get('1or2handed',0) else r.get('mindam',0))>0 for r in metal)
    paths.append(weapons)
    source=ROOT/'tools/reference/npc-equipment-damage-source-evidence.json';s=json.loads(source.read_text());paths.append(source)
    assert s['cases']==3888 and s['olderEngineCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    for group in ['inputs','artifacts']:
        for n,h in s[group].items():assert sha(Path(n) if Path(n).is_absolute() else ROOT/n)==h,n
    cases=ROOT/'.local/class-skills/npc-equipment-damage-source/cases.json'
    flat=ROOT/'.local/class-skills/npc-equipment-damage-source-cases.json';assert sha(cases)==sha(flat)
    assert len(json.loads(cases.read_text()))==3888
    assert focused['sourceInputs'][str(cases.relative_to(ROOT))]==sha(cases) and retained['sourceInputs'][str(flat.relative_to(ROOT))]==sha(flat)
    paths.extend([cases,flat])
    audit_log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count(': passed')==2 and audit_log.count('Shutting down')==1
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());audits.append(a)
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert a['ContentAssemblySha256']==focused['artifacts']['Assemblies/Sanctuary.Content.dll']
        for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
        paths.extend([p,ROOT/'tools/reference'/name])
    assert [len(a['InputHashes']) for a in audits]==[787,21]
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==historical('tools/reference/named-item-runtime-audit.json')['Definitions']
    old_affixes=historical('tools/reference/first-loop-affix-audit.json')
    for field in ['ImplementedCodes','SpawnableCodes','Items','Pools']:assert audits[1][field]==old_affixes[field],field
    python=STAGE/'python-tests.log';assert 'Ran 71 tests' in python.read_text() and '\nOK\n' in python.read_text();paths.append(python)
    installed=historical('tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for n,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/n)==h,n
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build-attempt-6.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        paths.extend([p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')])
    for name in ['damage-native-checks.log','stats-native-checks.log','audit-native-checks.log','isolated-npc-equipment-damage-fixtures.json','npc-equipment-damage-fixtures.json','run-attempt-6.log','retained-run-attempt-2.log']:
        paths.append(STAGE/name)
    paths.extend(ROOT/n for n in ['tests/check_npc_equipment_damage.py','tests/check_npc_equipment_damage_regression.py','tools/probe_npc_equipment_damage.py'])
    result=dict(referenceVersion='3.3.93847',component='Bound original NPC physical damage consumer',installed=False,goalComplete=False,completedClasses=[],
        verified=dict(sourceCompositions=3888,focusedMarkers=3,retainedMarkers=374,separateAuditMarkers=2,bootsPerNativeScope=3,actualReloadsPerNativeScope=2,pythonTests=71,inputFingerprints=[787,21],unchangedNamedDefinitions=573,unchangedInstalledArtifacts=106),
        inputInventory=retained,focusedInputInventory=focused,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=['Complete older physical damage, percentage and stat-list composition routines match3888typed roll/endpoint/bound contexts per native boot, including one/two-handed/unarmed selection,local/general ED,normal damage,minimum-only percentage,source64/128 and large-value rounding.',
                     'Actual Atlantian,Diggler,Bonesnap,Duriel and Scalper original items reach FP256 health loss and the summon Strike path,including melee versus thrown endpoints and blunt versus undead damage. Two reloads preserve original identity/parent/full rolls and exact first/last physical packet values. Unbound skeleton profile arithmetic is refactored without changing its formulas.'],
        remaining=['No complete NPC equipment property certificate or learned Iron Golem handler. Elemental/poison damage,critical/item events/procs,resources,resistances and independently frozen Revive effects remain unfinished.',
                   'Live acceptance covers prepared SkeletonGuardian bodies. Bound NecromancerMinion variants,full attack windup/contact/seed ownership,arbitrary native base attributes and complete raw item-stat projection require further integration. The typed roller accepts minimum/maximum percentage stats;actual exotic zero-base item projection is not certified.',
                   'Older source composition is not current executable parity. Bloodtree Stump remains unavailable for its existing skill-effect gate;the fixture uses available Bonesnap,whose canonical source key is Bonesob. No definition gate was changed.',
                   'No new full1184run,class completion,deployment or final art claim. Revive ability inheritance remains shared;global monster spell conversion remains separate.'])
    target=ROOT/'tools/reference/npc-equipment-damage-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==text
    else:target.write_text(text)
    print('NPC equipment physical acceptance:3888source/3focused/374retained/2audits/71Python;remaining equipment/skill work explicit.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
