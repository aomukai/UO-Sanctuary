#!/usr/bin/env python3
"""Bind signed NPC item regeneration, pending poison frames and old saves."""
import argparse
from collections import Counter
import hashlib,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_revive_regeneration import checked_log,markers
from snapshot_skill_build import check as check_snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-equipment-regeneration'
PREDECESSOR='e7b6da3'
sha=inventory.sha


def historical(name):
    return json.loads(subprocess.check_output(['git','show',PREDECESSOR+':'+name],cwd=ROOT))


def record(check=False):
    inventory.STAGE=STAGE;snapshot=STAGE/'pre-build-attempt-5.json';paths=[Path(__file__).resolve(),snapshot];scopes=[]
    for name in ['regeneration-inputs.json','stats-inputs.json','audit-inputs.json','upgrade-inputs.json','diagnostic-interactions/regeneration-inputs.json']:
        p=STAGE/name;scopes.append(inventory.validate_inventory(p));check_snapshot(snapshot,p);paths.append(p)
    focused,retained,audited,upgrade,stress=scopes
    assert all(x['artifacts']==focused['artifacts'] and x['copiedData']==focused['copiedData'] for x in scopes)
    prior=historical('tools/reference/npc-equipment-durations-evidence.json');old=prior['inputInventory'];assert old['copiedData']==focused['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert old['artifacts'][name]==focused['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    old_cs={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};new_cs={p:h for p,h in focused['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new_cs)-set(old_cs)=={'src/Sanctuary.Content/NpcEquipmentRegeneration.cs','tests/Sanctuary.Integration/NpcEquipmentRegenerationChecks.cs'}
    assert {p for p in old_cs if old_cs[p]!=new_cs[p]}=={
        'src/Sanctuary.Content/IronGolemItems.cs','src/Sanctuary.Content/PoisonStates.cs','src/Sanctuary.Content/SkeletonPets.cs',
        'src/Sanctuary.Content/TimedEffects.cs','tests/Sanctuary.Integration/NpcEquipmentResourceChecks.cs'}
    scope=checked_log(STAGE/'regeneration-native-checks.log',3)
    assert scope==Counter({'npc-equipment-regeneration-check':1,'npc-equipment-regeneration-reload-check':2})
    complete=checked_log(STAGE/'stats-native-checks.log',398)
    previous=ROOT/'.local/class-skills/npc-equipment-durations/stats-native-checks.log'
    assert sha(previous)==prior['artifacts'][str(previous.relative_to(ROOT))] and complete==markers(previous.read_text())+scope
    source=ROOT/'tools/reference/npc-equipment-regeneration-source-evidence.json';proof=json.loads(source.read_text())
    assert proof['referenceVersion']=='3.3.93847' and proof['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416' and proof['cases']==1920
    old_source=historical('tools/reference/poison-state-engine-evidence.json')
    assert proof['compiledUnchangedRoutineHashes']['D2GAME_MONSTER_ApplyStatRegen_6FC63440']==old_source['blockHashes']['D2GAME_MONSTER_ApplyStatRegen_6FC63440']
    for n,h in (proof['inputs']|proof['artifacts']).items():assert sha(ROOT/n)==h,n;paths.append(ROOT/n)
    case=ROOT/'.local/class-skills/npc-equipment-regeneration-source/cases.json';assert len(json.loads(case.read_text()))==1920
    assert focused['sourceInputs'][str(case.relative_to(ROOT))]==sha(case)
    flat=ROOT/'.local/class-skills/npc-equipment-regeneration-source-cases.json'
    assert retained['sourceInputs'][str(flat.relative_to(ROOT))]==sha(flat)==sha(case)
    paths.extend([source,ROOT/'tools/probe_npc_equipment_regeneration.py'])
    assert checked_log(STAGE/'upgrade-native-checks.log',3)==Counter({'npc-equipment-regeneration-legacy-create-check':1,
        'npc-equipment-regeneration-legacy-upgrade-check':1,'npc-equipment-regeneration-legacy-reload-check':1})
    old_content=ROOT/'.local/class-skills/npc-equipment-durations/content/Sanctuary.Content.dll'
    assert sha(old_content)==upgrade['oldArtifacts']['Sanctuary.Content.dll']==old['artifacts']['Assemblies/Sanctuary.Content.dll']
    receipt=subprocess.check_output(['git','show',PREDECESSOR+':tools/reference/npc-equipment-durations-evidence.json'],cwd=ROOT)
    assert upgrade['oldReceipt']['commit']==PREDECESSOR and upgrade['oldReceipt']['sha256']==hashlib.sha256(receipt).hexdigest()
    assert len(upgrade['boots'])==3
    for i,boot in enumerate(upgrade['boots']):
        expected=dict(focused['artifacts'])
        if i==0:expected['Assemblies/Sanctuary.Content.dll']=sha(old_content)
        assert boot['index']==i and boot['artifacts']==expected and boot['savedFiles']
    paths.append(old_content)
    stress_log=STAGE/'diagnostic-interactions/regeneration-native-checks.log';text=stress_log.read_text()
    assert ': failed' not in text and ' ERR]' not in text and text.count('Writing world save snapshot done')==text.count('Shutting down')==1
    assert markers(text)==Counter({'npc-equipment-regeneration-check':50,'npc-equipment-durations-check':50,'npc-equipment-resources-check':50})
    failure=STAGE/'diagnostic-initial-failure/regeneration-native-checks.log';text=failure.read_text()
    assert '"ManaMax":2' in text and '"RawInt":2' in text and '"Mana":512' in text and '"Bound":null' in text
    paths.extend([stress_log,ROOT/'tests/check_npc_equipment_regeneration_interactions.py',STAGE/'diagnostic-interactions/run.log',failure])
    audit_log=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count(': passed')==2 and audit_log.count('Shutting down')==1
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
        p=STAGE/(kind+'-build-attempt-5.log');assert all(x in p.read_text() for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
        paths.extend([p,STAGE/kind/('Sanctuary.'+kind.title()+'.dll')])
    for name in ['regeneration-native-checks.log','stats-native-checks.log','audit-native-checks.log','upgrade-native-checks.log',
        'isolated-npc-equipment-regeneration-fixtures.json','npc-equipment-regeneration-fixtures.json','upgrade-npc-equipment-regeneration-legacy-fixtures.json',
        'run-attempt-5.log','retained-run-attempt-3.log','upgrade-run-attempt-2.log']:
        paths.append(STAGE/name)
    for name in ['isolated-npc-equipment-regeneration-fixtures.json','npc-equipment-regeneration-fixtures.json']:
        saved=json.loads((STAGE/name).read_text());assert saved['Generation']==2 and len(saved['Actors'])==3
        rows=saved['Actors'];assert [x['ItemRate'] for x in rows]==[20,15,20]
        assert [x['Poison']['NpcPoisonElapsedMs'] for x in rows]==[None,39,None]
        assert [x['Poison']['PoisonClock']['ElapsedMs'] for x in [rows[0],rows[2]]]==[39,39]
    legacy=json.loads((STAGE/'upgrade-npc-equipment-regeneration-legacy-fixtures.json').read_text())
    assert legacy['Generation']==2 and json.loads(legacy['Poison'])['NpcPoisonElapsedMs']==39
    for n in ['tests/check_npc_equipment_regeneration.py','tests/check_npc_equipment_regeneration_regression.py',
        'tests/check_npc_equipment_regeneration_upgrade.py','docs/skill-effects.md','docs/skill-icons.md']:paths.append(ROOT/n)
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert len([x for x in status['skills'] if x['implementation']=='handler-present'])==185 and status['completedClasses']==[]
    result=dict(referenceVersion='3.3.93847',component='Original NPC item replenish life and combined poison HP frames',installed=False,goalComplete=False,completedClasses=[],
        verified=dict(fullOlderMonsterContexts=1920,focusedMarkers=3,retainedMarkers=398,oldContentUpgradeMarkers=3,repeatedInteractionMarkers=150,
        repeatedInteractionBoots=1,separateAuditMarkers=2,bootsPerLifecycleScope=3,actualReloadsPerLifecycleScope=2,pythonTests=71,
        inputFingerprints=[798,21],unchangedNamedDefinitions=573,unchangedInstalledArtifacts=106),
        inputInventory=retained,focusedInputInventory=focused,upgradeInputInventory=upgrade,stressInputInventory=stress,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(paths))},
        established=[
            'Existing original-item suppliers provide Replenish Life in FP256 per40ms. Healthy bound holders combine it with the existing natural regeneration basis and cap exact health. Poison removes natural regeneration but retains item replenishment,committing one signed frame before lethal resolution;prevent-heal blocks nonnegative net recovery,not negative poison.',
            'Actual Warlord Trust20,Gerke15 and Spire20 originals verify positive/zero/negative combined frames,39/40ms timing,one-life fraction/caps,prevent-heal,a near-death offset,broken/unidentified/removal and positive owner-equipment exclusion. Pending source and aggregate clocks,original identity/full rolls and exact balances survive two reloads.',
            'Optional nullable aggregate poison phase preserves old omitted-field saves and distinguishes old/unphased input from an active0..39msframe clock. The phase survives original removal and clears on cure/expiry;invalid,dual and inactive phases are rejected. A real accepted old-content save loads twice with new content,retaining exact old state before saving/resuming the new39msphase.',
            'Repeated interactions exposed an unstable old fixture expectation: native RawInt had risen to2,so removal correctly restored2mana/512FP rather than a hard-coded1. The resource fixture now captures the actual native Int fallback before removing its Crown(no item Int bonus),then still verifies missing binding,exact cap and valid life. Fifty regeneration/duration/resource cycles pass with the corrected native-baseline expectation.'],
        remaining=[
            'The source harness declares the natural HPRegen basis. Full max-life callback/basis recalculation completeness,Open Wounds/other life-mask and native UO poison composition remain separate. Material life-per-five fails explicitly until a source-rate projection is established. Negative flat live replenish suppliers are not certified by these originals.',
            'Monster and player recovery dispatch differ in the older source. Existing native NPC mana/stamina behavior and UO stat gains are retained,not certified as D2 parity;no player class regeneration coefficients were guessed for NPCs. Full modern scheduler/executable and item-birth equivalence remain open.',
            'Other NPC equipment events/procs,independently frozen Revive equipment and complete learned Iron Golem remain unfinished. Revive inherits living abilities;global monster spell conversion remains separate. No new handler,class completion,full-class run or deployment. Effects/audio and talent/hotkey icons remain separately recorded placeholders.'])
    target=ROOT/'tools/reference/npc-equipment-regeneration-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==rendered
    else:target.write_text(rendered)
    print('NPC regeneration acceptance:1920source/3focused/398retained/3old-upgrade/150interaction/2audits/71Python;remaining scope explicit.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
