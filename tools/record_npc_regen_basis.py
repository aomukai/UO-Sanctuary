#!/usr/bin/env python3
"""Bind exact NPC maximum-life regeneration observation to frozen native scopes."""
import argparse,hashlib,json,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_revive_regeneration import checked_log,markers
from snapshot_skill_build import check as check_snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/npc-equipment-regen-basis'
PREDECESSOR='385fa98';sha=inventory.sha


def historical(name):
    return json.loads(subprocess.check_output(['git','show',PREDECESSOR+':'+name],cwd=ROOT))


def record(check=False):
    inventory.STAGE=STAGE;snapshot=STAGE/'pre-build-attempt-1.json';paths=[Path(__file__).resolve(),snapshot];scopes=[]
    for name in ['basis-inputs.json','stats-inputs.json','audit-inputs.json','upgrade-inputs.json']:
        p=STAGE/name;scopes.append(inventory.validate_inventory(p));check_snapshot(snapshot,p);paths.append(p)
    focused,retained,audited,upgrade=scopes
    assert all(x['artifacts']==focused['artifacts'] and x['copiedData']==focused['copiedData'] for x in scopes)
    prior=historical('tools/reference/npc-equipment-regeneration-evidence.json');old=prior['inputInventory'];assert old['copiedData']==focused['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert old['artifacts'][name]==focused['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};new_cs={p:h for p,h in focused['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new_cs)-set(old_cs)=={'tests/Sanctuary.Integration/NpcRegenBasisChecks.cs'}
    assert {p for p in old_cs if old_cs[p]!=new_cs[p]}=={'src/Sanctuary.Content/BattleOrders.cs','src/Sanctuary.Content/GearProfiles.cs','src/Sanctuary.Content/NpcEquipmentResources.cs'}
    scope=checked_log(STAGE/'basis-native-checks.log',3)
    assert scope==Counter({'npc-regen-basis-check':1,'npc-regen-basis-reload-check':2})
    complete=checked_log(STAGE/'stats-native-checks.log',401)
    previous=ROOT/'.local/class-skills/npc-equipment-regeneration/stats-native-checks.log'
    assert sha(previous)==prior['artifacts'][str(previous.relative_to(ROOT))] and complete==markers(previous.read_text())+scope
    source=ROOT/'tools/reference/npc-equipment-regen-basis-source-evidence.json';proof=json.loads(source.read_text())
    assert proof['referenceVersion']=='3.3.93847' and proof['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and proof['cases']==3072
    parent=ROOT/'tools/reference/battle-orders-engine-evidence.json';assert json.loads(parent.read_text())==historical(str(parent.relative_to(ROOT)))
    for n,h in (proof['inputs']|proof['artifacts']).items():assert sha(ROOT/n)==h,n;paths.append(ROOT/n)
    callback=(ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/ITEMS/ItemMode.cpp').read_text()
    branch=callback[callback.index('    case STAT_MAXHP:'):callback.index('    case STAT_MAXMANA:')]
    assert hashlib.sha256(branch.encode()).hexdigest()==proof['compiledUnchangedMaxHpBranchSha256']
    case=ROOT/'.local/class-skills/npc-equipment-regen-basis-source/cases.json';assert len(json.loads(case.read_text()))==3072
    assert focused['sourceInputs'][str(case.relative_to(ROOT))]==sha(case)
    flat=ROOT/'.local/class-skills/npc-regen-basis-source-cases.json';assert retained['sourceInputs'][str(flat.relative_to(ROOT))]==sha(flat)==sha(case)
    paths.extend([source,ROOT/'tools/probe_npc_regen_basis.py'])
    assert checked_log(STAGE/'upgrade-native-checks.log',3)==Counter({'npc-regen-basis-legacy-create-check':1,'npc-regen-basis-legacy-upgrade-check':1,'npc-regen-basis-legacy-reload-check':1})
    old_content=ROOT/'.local/class-skills/npc-equipment-regeneration/content/Sanctuary.Content.dll'
    assert sha(old_content)==upgrade['oldArtifacts']['Sanctuary.Content.dll']==old['artifacts']['Assemblies/Sanctuary.Content.dll']
    receipt=subprocess.check_output(['git','show',PREDECESSOR+':tools/reference/npc-equipment-regeneration-evidence.json'],cwd=ROOT)
    assert upgrade['oldReceipt']['commit']==PREDECESSOR and upgrade['oldReceipt']['sha256']==hashlib.sha256(receipt).hexdigest()
    assert len(upgrade['boots'])==3
    for i,boot in enumerate(upgrade['boots']):
        expected=dict(focused['artifacts'])
        if i==0:expected['Assemblies/Sanctuary.Content.dll']=sha(old_content)
        assert boot['index']==i and boot['artifacts']==expected and boot['savedFiles']
    paths.append(old_content)
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2 and text.count('Shutting down')==1
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        p=STAGE/name;a=json.loads(p.read_text());audits.append(a);assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert a['ContentAssemblySha256']==focused['artifacts']['Assemblies/Sanctuary.Content.dll']
        for n,h in a['InputHashes'].items():assert sha(ROOT/n)==h,n
        paths.extend([p,ROOT/'tools/reference'/name])
    assert [len(a['InputHashes']) for a in audits]==[798,21]
    assert len(audits[0]['Definitions'])==573 and audits[0]['Definitions']==historical('tools/reference/named-item-runtime-audit.json')['Definitions']
    old_affixes=historical('tools/reference/first-loop-affix-audit.json')
    for field in ['ImplementedCodes','SpawnableCodes','Items','Pools']:assert audits[1][field]==old_affixes[field],field
    python=STAGE/'python-tests.log';assert 'Ran 71 tests' in python.read_text() and '\nOK\n' in python.read_text();paths.append(python)
    installed=historical('tools/reference/creeper-frame-installation-evidence.json');assert len(installed['installedArtifacts'])==106
    for n,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/n)==h,n
    for kind in ['content','integration']:
        p=STAGE/(kind+'-build-attempt-1.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        paths.extend([p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')])
    for name in ['basis-native-checks.log','stats-native-checks.log','audit-native-checks.log','upgrade-native-checks.log',
        'isolated-npc-regen-basis-fixtures.json','npc-regen-basis-fixtures.json','upgrade-npc-regen-basis-legacy-fixtures.json',
        'run-attempt-1.log','retained-run-attempt-1.log','upgrade-run-attempt-1.log']:paths.append(STAGE/name)
    for name in ['isolated-npc-regen-basis-fixtures.json','npc-regen-basis-fixtures.json']:
        saved=json.loads((STAGE/name).read_text());assert saved['Generation']==2 and len(saved['Actors'])==3
        assert [x['Rate'] for x in saved['Actors']]==[12,22,5] and [x['Maximum'] for x in saved['Actors']]==[13184,23424,8064]
    legacy=json.loads((STAGE/'upgrade-npc-regen-basis-legacy-fixtures.json').read_text());assert legacy['Generation']==2 and legacy['Rate']==12 and legacy['Balance']==20*256+17
    for n in ['tests/check_npc_regen_basis.py','tests/check_npc_regen_basis_regression.py','tests/check_npc_regen_basis_upgrade.py',
        'docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md']:paths.append(ROOT/n)
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert len([x for x in status['skills'] if x['implementation']=='handler-present'])==185 and status['completedClasses']==[]
    result=dict(referenceVersion='3.3.93847',component='NPC exact maximum-life natural regeneration basis callbacks',installed=False,goalComplete=False,completedClasses=[],
        verified=dict(completeMaxHpBranchContexts=3072,focusedMarkers=3,retainedMarkers=401,oldContentUpgradeMarkers=3,separateAuditMarkers=2,
        bootsPerLifecycleScope=3,actualReloadsPerLifecycleScope=2,pythonTests=71,inputFingerprints=[798,21],unchangedNamedDefinitions=573,unchangedInstalledArtifacts=106),
        inputInventory=retained,focusedInputInventory=focused,upgradeInputInventory=upgrade,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=[
            'NPC resource ledgers observe exact source maximum life. Actual max-life transitions recalculate the natural HPRegen basis from whole HP rounded down,including changes hidden by the same native display. Zero coefficient/unchanged or nonpositive previous/current-life cases preserve the previous rate.',
            'Actual Umbral Disk51.5life/52display and Shaftstop91.5/92 produce12and22rather than native-ceiling13and23. Blacktongue disabled HP preserves its original5basis. Identification,breakage,repair,unchanged stores,actual Barbarian BO,removal and prepared1FP/same-display controls pass through two world reloads with original identities/full rolls.',
            'An optional exact observation field persists in OrdersResources. Old omitted fields establish the present baseline without inventing callback history or changing the old saved rate. A real accepted-old-content save then performs verified transitions and persists12regen/exact observation with unchanged20life+17FP and original full rolls.'],
        remaining=[
            'The complete MAXHP branch is compiled unchanged,not the complete outer callback or modern executable. It also rescales current HP. The native NPC item bridge still clamps absolute balances;that is an implementation gap,not a user-approved deviation. Full HP/mana balance callback integration remains required before complete affected skills.',
            'Old missing callback histories are not reconstructed retroactively. Other life-masks/native poison,material life-per-five,NPC mana/stamina/native stat-gain parity,item events/procs,independently frozen Revive equipment and learned Iron Golem remain unfinished.',
            'Revive retains living abilities;global monster spell conversion remains separate. No new handler,class completion,full-class run or deployment. Effects/audio and talent/hotkey icons remain separate identified placeholders.'])
    target=ROOT/'tools/reference/npc-regen-basis-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==rendered
    else:target.write_text(rendered)
    print('NPC regen basis acceptance:3072source-branch/3focused/401retained/3old-upgrade/2audits/71Python;balance callback gap explicit.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
