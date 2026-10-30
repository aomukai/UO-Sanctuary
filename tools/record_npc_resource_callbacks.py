#!/usr/bin/env python3
"""Bind NPC current-resource callbacks to their source and actual saved worlds."""
import argparse,hashlib,json,subprocess
from collections import Counter
from pathlib import Path
import record_grizzly_body as inventory
from record_revive_regeneration import checked_log,markers
from snapshot_skill_build import check as check_snapshot
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/npc-resource-callbacks'
PREDECESSOR='0351a75';sha=inventory.sha


def historical(name):
    return json.loads(subprocess.check_output(['git','show',PREDECESSOR+':'+name],cwd=ROOT))


def record(check=False):
    inventory.STAGE=STAGE;snapshot=STAGE/'pre-build-attempt-2.json';paths=[Path(__file__).resolve(),snapshot];scopes=[]
    for name in ['callbacks-inputs.json','stats-inputs.json','audit-inputs.json','upgrade-inputs.json']:
        p=STAGE/name;scopes.append(inventory.validate_inventory(p));check_snapshot(snapshot,p);paths.append(p)
    focused,retained,audited,upgrade=scopes
    assert all(x['artifacts']==focused['artifacts'] and x['copiedData']==focused['copiedData'] for x in scopes)
    prior=historical('tools/reference/npc-regen-basis-evidence.json');old=prior['inputInventory'];assert old['copiedData']==focused['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert old['artifacts'][name]==focused['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};new_cs={p:h for p,h in focused['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new_cs)-set(old_cs)=={'tests/Sanctuary.Integration/NpcResourceCallbackChecks.cs'}
    assert {p for p in old_cs if old_cs[p]!=new_cs[p]}=={
        'src/Sanctuary.Content/BattleOrders.cs','src/Sanctuary.Content/GearProfiles.cs','src/Sanctuary.Content/IronGolemItems.cs',
        'src/Sanctuary.Content/NecromancerEffects.cs','src/Sanctuary.Content/NpcEquipmentResources.cs','src/Sanctuary.Content/SkillHealth.cs',
        'tests/Sanctuary.Integration/NpcEquipmentResourceChecks.cs','tests/Sanctuary.Integration/NpcRegenBasisChecks.cs'}
    scope=checked_log(STAGE/'callbacks-native-checks.log',3)
    assert scope==Counter({'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    complete=checked_log(STAGE/'stats-native-checks.log',404);previous=ROOT/'.local/class-skills/npc-equipment-regen-basis/stats-native-checks.log'
    assert sha(previous)==prior['artifacts'][str(previous.relative_to(ROOT))] and complete==markers(previous.read_text())+scope
    source=ROOT/'tools/reference/npc-resource-callbacks-source-evidence.json';proof=json.loads(source.read_text())
    assert proof['referenceVersion']=='3.3.93847' and proof['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and proof['cases']==1080
    parent=ROOT/'tools/reference/battle-orders-engine-evidence.json';assert json.loads(parent.read_text())==historical(str(parent.relative_to(ROOT)))
    for n,h in (proof['inputs']|proof['artifacts']).items():assert sha(ROOT/n)==h,n;paths.append(ROOT/n)
    text=(ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/ITEMS/ItemMode.cpp').read_text()
    branch=text[text.index('    case STAT_MAXHP:'):text.index('    case STAT_ITEM_NONCLASSSKILL:')]
    assert hashlib.sha256(branch.encode()).hexdigest()==proof['compiledUnchangedResourceBranchesSha256']
    case=ROOT/'.local/class-skills/npc-resource-callbacks-source/cases.json';assert len(json.loads(case.read_text()))==1080
    assert focused['sourceInputs'][str(case.relative_to(ROOT))]==sha(case)
    flat=ROOT/'.local/class-skills/npc-resource-callbacks-source-cases.json';assert retained['sourceInputs'][str(flat.relative_to(ROOT))]==sha(flat)==sha(case)
    paths.extend([source,ROOT/'tools/probe_npc_resource_callbacks.py'])
    assert checked_log(STAGE/'upgrade-native-checks.log',3)==Counter({'npc-resource-callbacks-legacy-create-check':1,'npc-resource-callbacks-legacy-upgrade-check':1,'npc-resource-callbacks-legacy-reload-check':1})
    old_content=ROOT/'.local/class-skills/npc-equipment-regen-basis/content/Sanctuary.Content.dll'
    assert sha(old_content)==upgrade['oldArtifacts']['Sanctuary.Content.dll']==old['artifacts']['Assemblies/Sanctuary.Content.dll']
    receipt=subprocess.check_output(['git','show',PREDECESSOR+':tools/reference/npc-regen-basis-evidence.json'],cwd=ROOT)
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
        p=STAGE/(kind+'-build-attempt-2.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        paths.extend([p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')])
    for name in ['callbacks-native-checks.log','stats-native-checks.log','audit-native-checks.log','upgrade-native-checks.log',
        'isolated-npc-resource-callbacks-fixtures.json','npc-resource-callbacks-fixtures.json','upgrade-npc-resource-callbacks-legacy-fixtures.json',
        'run-attempt-2.log','retained-run-attempt-1.log','upgrade-run-attempt-1.log']:paths.append(STAGE/name)
    for name in ['isolated-npc-resource-callbacks-fixtures.json','npc-resource-callbacks-fixtures.json']:
        saved=json.loads((STAGE/name).read_text());assert saved['Generation']==2 and len(saved['Actors'])==4
        rows=saved['Actors'];assert [x['Life'] for x in rows]==[20864,8064,8064,23104]
        assert [x['Mana'] for x in rows]==[14080,7168,5120,5120] and [x['Stamina'] for x in rows]==[25600,25600,30720,25600]
    legacy=json.loads((STAGE/'upgrade-npc-resource-callbacks-legacy-fixtures.json').read_text());assert legacy['Generation']==2
    for n in ['tests/check_npc_resource_callbacks.py','tests/check_npc_resource_callbacks_regression.py','tests/check_npc_resource_callbacks_upgrade.py',
        'docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md']:paths.append(ROOT/n)
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert len([x for x in status['skills'] if x['implementation']=='handler-present'])==185 and status['completedClasses']==[]
    result=dict(referenceVersion='3.3.93847',component='NPC current life/mana/stamina maximum-change callbacks',installed=False,goalComplete=False,completedClasses=[],
        verified=dict(completeResourceBranchContexts=1080,focusedMarkers=3,retainedMarkers=404,oldContentUpgradeMarkers=3,separateAuditMarkers=2,
        bootsPerLifecycleScope=3,actualReloadsPerLifecycleScope=2,pythonTests=71,inputFingerprints=[798,21],unchangedNamedDefinitions=573,unchangedInstalledArtifacts=106),
        inputInventory=retained,focusedInputInventory=focused,upgradeInputInventory=upgrade,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=[
            'Actual bound equipment maximum changes preserve source current-resource percentages for all3pools with FP256 truncation/minimum1and empty/no-previous safeguards,using the existing callback helper. Attachment/removal snapshots,pre-store captures and raw late durability balances avoid premature clamping.',
            'Crown of Thieves,Frostburn,Gore Rider and Kelpie Snare verify full/partial/tiny/empty pools,identification,armor/weapon/clothing breakage/repair,unchanged stores,removal and exact resource/original persistence. A declared20mana base exercises positive transitions;source-zero mana separately excludes the native Int fallback rather than creating free mana.',
            'Optional exact NpcMaximums observations persist in the existing OrdersResources ledger. A real accepted-old-content save omits them;two new boots preserve initial saved pools/identity/full rolls before source-ratio transitions and new observation persistence. Existing resource/basis assertions now account for the verified callbacks and their fractional quantization.'],
        remaining=[
            'Only the complete resource branches are compiled unchanged,not the entire outer event callback or modern executable. Positive next maxima are certified;older zero-next clamp bounds remain excluded and the existing native zero-cap convention is retained explicitly.',
            'Full outer item event registration,multi-item/delayed ordering,old missing histories,other life-masks/native poison,material life-per-five,NPC mana/stamina/native stat-gain parity,frozen Revive equipment and learned Iron Golem remain unfinished.',
            'Revive inherits living abilities;global monster spell conversion remains separate. No new handler,class completion,full-class run or deployment. Effects/audio and talent/hotkey icons remain separate identified placeholders.'])
    target=ROOT/'tools/reference/npc-resource-callbacks-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==rendered
    else:target.write_text(rendered)
    print('NPC resource callbacks acceptance:1080source/3focused/404retained/3old-upgrade/2audits/71Python;remaining scope explicit.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
