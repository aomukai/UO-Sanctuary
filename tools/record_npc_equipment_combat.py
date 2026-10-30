#!/usr/bin/env python3
"""Bind scoped NPC attack/defense consumers to tested originals and reloads."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import record_grizzly_body as inventory
from record_revive_regeneration import checked_log
from snapshot_skill_build import check as check_snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-equipment-combat'
PREDECESSOR='d793a6e'
sha=inventory.sha


def historical(name):
    return json.loads(subprocess.check_output(['git','show',PREDECESSOR+':'+name],cwd=ROOT))


def record(check=False):
    inventory.STAGE=STAGE
    focused=inventory.validate_inventory(STAGE/'combat-inputs.json')
    retained=inventory.validate_inventory(STAGE/'stats-inputs.json')
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    paths=[Path(__file__).resolve(),STAGE/'pre-build-attempt-2.json']
    for name in ['combat-inputs.json','stats-inputs.json','audit-inputs.json']:
        check_snapshot(STAGE/'pre-build-attempt-2.json',STAGE/name);paths.append(STAGE/name)
    assert all(x['artifacts']==focused['artifacts'] and x['copiedData']==focused['copiedData'] for x in [retained,audited])
    prior=historical('tools/reference/npc-equipment-defense-evidence.json')
    old=prior['inputInventory']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert old['artifacts'][name]==focused['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    assert old['copiedData']==focused['copiedData']
    old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')}
    new_cs={p:h for p,h in focused['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new_cs)-set(old_cs)=={'src/Sanctuary.Content/NpcEquipmentCombat.cs','tests/Sanctuary.Integration/NpcEquipmentCombatChecks.cs'}
    assert {p for p in old_cs if old_cs[p]!=new_cs[p]}=={'src/Sanctuary.Content/ClassStats.cs','src/Sanctuary.Content/NamedCombat.cs'}
    scope=checked_log(STAGE/'combat-native-checks.log',3)
    assert scope==Counter({'npc-equipment-combat-check':1,'npc-equipment-combat-reload-check':2})
    complete=checked_log(STAGE/'stats-native-checks.log',371)
    previous_log=ROOT/'.local/class-skills/npc-equipment-defense/stats-native-checks.log'
    assert sha(previous_log)==prior['artifacts'][str(previous_log.relative_to(ROOT))]
    previous=Counter()
    import re
    for name in re.findall(r'\] ([A-Za-z0-9-]+): passed',previous_log.read_text()):previous[name]+=1
    # Fresh catalogue audit is in its own two-command boot for this candidate.
    assert complete==previous+scope-Counter({'first-loop-catalogue-check':1})
    audit_log=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count(': passed')==2 and audit_log.count('Shutting down')==1
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());audits.append(a)
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert a['ContentAssemblySha256']==focused['artifacts']['Assemblies/Sanctuary.Content.dll']
        for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
        paths.extend([p,ROOT/'tools/reference'/name])
    assert [len(a['InputHashes']) for a in audits]==[786,21]
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==historical('tools/reference/named-item-runtime-audit.json')['Definitions']
    python=STAGE/'python-tests.log';assert 'Ran 71 tests' in python.read_text() and '\nOK\n' in python.read_text();paths.append(python)
    installed=historical('tools/reference/creeper-frame-installation-evidence.json')
    assert len(installed['installedArtifacts'])==106
    for n,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/n)==h,n
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build-attempt-2.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        paths.extend([p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')])
    for name in ['combat-native-checks.log','stats-native-checks.log','audit-native-checks.log','isolated-npc-equipment-combat-fixtures.json','npc-equipment-combat-fixtures.json','run-attempt-2.log','retained-run-attempt-1.log']:
        paths.append(STAGE/name)
    paths.extend(ROOT/n for n in ['tests/check_npc_equipment_combat.py','tests/check_npc_equipment_combat_regression.py'])
    result=dict(referenceVersion='3.3.93847',component='Bound original NPC attack rating and defense consumers',installed=False,goalComplete=False,completedClasses=[],
        verified=dict(focusedMarkers=3,retainedMarkers=371,separateAuditMarkers=2,bootsPerNativeScope=3,actualReloadsPerNativeScope=2,pythonTests=71,inputFingerprints=[786,21],unchangedNamedDefinitions=573,unchangedInstalledArtifacts=106),
        inputInventory=retained,focusedInputInventory=focused,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        reference=dict(olderEngineCommit='0c0bbd1e6bef9181a3da1c046523af622a925416',function='SUNITDMG_IsHitSuccessful',path='source/D2Game/src/UNIT/SUnitDmg.cpp',basis='Reviewed older source nonplayer flat/dex/percent attack and directional defense branches; no new compiled differential or current-executable parity claim.'),
        established=['Prepared D2 summon bodies with actual original Atlantian, Steelgoad and Diggler items consume flat/dex/percent attack and item/dex defense exactly once, including controlled hit checks. Unidentified originals return to bare baseline.',
                     'Original serial/parent/full rolls and exact combat results survive two real save/reload cycles. Existing retained classes and lifecycle checks pass.'],
        remaining=['Scope is SkeletonGuardian bodies with a valid original binding and matching owner. Native UO base attributes and independently frozen Revive equipment require separate adapters.',
                   'Existing D2 summon combat bases exclude native UO Dex. This scope treats item Dexterity as an increment to those bases; arbitrary base-dex rounding and directional-defense native item cases are not verified by these named fixtures.',
                   'Damage, resources, resistances, events/procs, complete item support and learned Iron Golem are unfinished. No class completion, new full1184 run or deployment is claimed. Revive ability inheritance remains shared; global monster spell conversion is separate.'])
    target=ROOT/'tools/reference/npc-equipment-combat-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==text
    else:target.write_text(text)
    print('NPC equipment combat scoped acceptance: 3 focused, 371 retained, 2 separate audits, 71 Python; no complete skill/class or deployment claim.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
