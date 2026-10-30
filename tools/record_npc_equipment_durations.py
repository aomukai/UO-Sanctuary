#!/usr/bin/env python3
"""Bind NPC item duration defenses to the actual frozen native test scopes."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_revive_regeneration import checked_log, markers
from snapshot_skill_build import check as check_snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-equipment-durations'
PREDECESSOR='abe07f0'
sha=inventory.sha


def historical(name):
    return json.loads(subprocess.check_output(['git','show',PREDECESSOR+':'+name],cwd=ROOT))


def record(check=False):
    inventory.STAGE=STAGE
    snapshot=STAGE/'pre-build-attempt-4.json'
    paths=[Path(__file__).resolve(),snapshot];scopes=[]
    for name in ['durations-inputs.json','stats-inputs.json','audit-inputs.json']:
        p=STAGE/name;scopes.append(inventory.validate_inventory(p));check_snapshot(snapshot,p);paths.append(p)
    focused,retained,audited=scopes
    assert all(x['artifacts']==focused['artifacts'] and x['copiedData']==focused['copiedData'] for x in scopes)
    prior=historical('tools/reference/npc-equipment-resources-evidence.json');old=prior['inputInventory']
    assert old['copiedData']==focused['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert old['artifacts'][name]==focused['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    new_cs={p:h for p,h in focused['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new_cs)-set(old_cs)=={'src/Sanctuary.Content/ItemDurationDefenses.cs','tests/Sanctuary.Integration/NpcEquipmentDurationChecks.cs'}
    assert {p for p in old_cs if old_cs[p]!=new_cs[p]}=={
        'src/Sanctuary.Content/BladeSentinelMissiles.cs','src/Sanctuary.Content/ColdStates.cs',
        'src/Sanctuary.Content/DruidCreeperPoison.cs','src/Sanctuary.Content/FreezeStates.cs',
        'src/Sanctuary.Content/GearEffects.cs','src/Sanctuary.Content/IronGolemItems.cs',
        'src/Sanctuary.Content/NamedElements.cs','src/Sanctuary.Content/NpcEquipmentPoison.cs','src/Sanctuary.Content/PoisonJavelins.cs'}
    scope=checked_log(STAGE/'durations-native-checks.log',3)
    assert scope==Counter({'npc-equipment-durations-check':1,'npc-equipment-durations-reload-check':2})
    complete=checked_log(STAGE/'stats-native-checks.log',395)
    previous=ROOT/'.local/class-skills/npc-equipment-resources/stats-native-checks.log'
    assert sha(previous)==prior['artifacts'][str(previous.relative_to(ROOT))]
    assert complete==markers(previous.read_text())+scope
    for family,count,key in [('poison-mitigation',72000,'cases'),('freeze-state',224,'lengthCases')]:
        p=ROOT/'tools/reference'/(family+'-engine-evidence.json');proof=json.loads(p.read_text())
        assert proof==historical(str(p.relative_to(ROOT)))
        assert proof['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and proof['probe'][key]==count
        case=ROOT/proof['probe']['path'];assert sha(case)==proof['probe']['sha256']
        assert focused['sourceInputs'][str(case.relative_to(ROOT))]==retained['sourceInputs'][str(case.relative_to(ROOT))]==sha(case)
        paths.extend([p,case])
    audit_log=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count(': passed')==2 and audit_log.count('Shutting down')==1
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());audits.append(a)
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert a['ContentAssemblySha256']==focused['artifacts']['Assemblies/Sanctuary.Content.dll']
        for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
        paths.extend([p,ROOT/'tools/reference'/name])
    assert [len(a['InputHashes']) for a in audits]==[797,21]
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==historical('tools/reference/named-item-runtime-audit.json')['Definitions']
    old_affixes=historical('tools/reference/first-loop-affix-audit.json')
    for field in ['ImplementedCodes','SpawnableCodes','Items','Pools']:assert audits[1][field]==old_affixes[field],field
    python=STAGE/'python-tests.log';assert 'Ran 71 tests' in python.read_text() and '\nOK\n' in python.read_text();paths.append(python)
    installed=historical('tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for n,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/n)==h,n
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build-attempt-4.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        paths.extend([p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')])
    for name in ['durations-native-checks.log','stats-native-checks.log','audit-native-checks.log',
                 'isolated-npc-equipment-durations-fixtures.json','npc-equipment-durations-fixtures.json','run-attempt-4.log','retained-run-attempt-2.log']:
        paths.append(STAGE/name)
    for name in ['isolated-npc-equipment-durations-fixtures.json','npc-equipment-durations-fixtures.json']:
        saved=json.loads((STAGE/name).read_text());assert saved['Generation']==2 and len(saved['Actors'])==5
        rows=saved['Actors'];assert [x['PoisonLength'] for x in rows]==[50,50,75,0,0]
        assert [x['Half'] for x in rows]==[False,False,False,True,False]
        assert [x['Cannot'] for x in rows]==[False,False,False,False,True]
    for n in ['tests/check_npc_equipment_durations.py','tests/check_npc_equipment_durations_regression.py','docs/skill-effects.md','docs/skill-icons.md']:
        paths.append(ROOT/n)
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert len([x for x in status['skills'] if x['implementation']=='handler-present'])==185 and status['completedClasses']==[]
    result=dict(referenceVersion='3.3.93847',component='Original bound NPC equipment poison and cold/freeze duration defenses',installed=False,
        goalComplete=False,completedClasses=[],verified=dict(reusedPoisonMitigationContexts=72000,reusedColdFreezeLengthContexts=224,
        focusedMarkers=3,retainedMarkers=395,separateAuditMarkers=2,bootsPerNativeScope=3,actualReloadsPerNativeScope=2,
        pythonTests=71,inputFingerprints=[797,21],unchangedNamedDefinitions=573,unchangedInstalledArtifacts=106),
        inputInventory=retained,focusedInputInventory=focused,auditInputInventory=audited,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=[
            'Shared recipient item reader supplies poison-length resistance, Cannot Be Frozen and half freeze duration from the actual original, never controller equipment. Existing source mitigation and cold/freeze services remain the numeric engines; player caps and global penalties are not applied to an NPC poison-length pool.',
            'Five actual metal originals exercise50/75PLR, half freeze and Cannot Be Frozen with actual packet/aggregate poison, unchanged first-frame rate, cold/freeze and legacy chill. Identified/broken/repair/removal, positive owner equipment exclusion and exact serial/parent/full-roll persistence pass across two reloads.',
            'Prepared poison NPC/Javelin/Creeper/Blade consumers now supply recipient PLR. Legacy aggregate bound-NPC poison retains its existing rate projection while duration/total use the source-frame length helper. Bound-NPC legacy chill delegates to the existing source cold-state routine.',
            'Native item removal callbacks previously read a binding pointing at a deleted item. DestroyBoundItem now validates ownership and removes the binding before native deletion callbacks execute. Removal and retained resource/poison/critical/item lifecycles pass. No new actor fields or persistence schema.'],
        remaining=[
            'Offensive Freezes Target and other item-hit/skill events/procs, complete NPC regeneration consumers, independently frozen Revive equipment and complete learned Iron Golem remain unfinished.',
            'Reused full older-source contexts are not a current-executable or item-birth certificate. Player aggregate poison and existing negative passive-pierce projections retain their prior scope; complete modern parity is not inferred. No new old-content upgrade or full-class run is claimed.',
            'Revive inherits living abilities; global monster spell conversion remains separate. No new handler, class completion or deployment. Generic UO cold/freeze/poison markers and text-label icons remain identified placeholders in separate effects/audio and icon registers.'])
    target=ROOT/'tools/reference/npc-equipment-durations-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==rendered
    else:target.write_text(rendered)
    print('NPC duration acceptance:72000poison/224cold-length/3focused/395retained/2audits/71Python;remaining scope explicit.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
