#!/usr/bin/env python3
"""Bind ordinary current item drain compatibility to complete older fill and native deliveries."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from snapshot_skill_build import check as check_snapshot
from review_weapon_event_order import function
import build_skill_roadmap

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/npc-item-drain'
FROZEN='76a55df'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE
    inputs=inventory.validate_inventory(STAGE/'birth-inputs.json')
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ['birth-inputs.json','audit-inputs.json']:
        check_snapshot(STAGE/'pre-build-2.json',STAGE/name)
    assert inputs['artifacts']==audited['artifacts'] and inputs['copiedData']==audited['copiedData']
    prior=json.loads((ROOT/'tools/reference/npc-impact-allocation-native-evidence.json').read_text())['inputInventory']
    old={p:h for p,h in prior['sourceInputs'].items() if p.endswith('.cs')}
    new={p:h for p,h in inputs['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new)==set(old)
    assert {p for p in old if old[p]!=new[p]}=={'src/Sanctuary.Content/ItemStatContext.cs','src/Sanctuary.Content/NpcEquipmentImpact.cs','tests/Sanctuary.Integration/NpcImpactAllocationChecks.cs'}
    assert inputs['copiedData']==prior['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['artifacts'][name]==prior['artifacts'][name]
    source_path=ROOT/'tools/reference/golem-roster-source-evidence.json'
    source=json.loads(source_path.read_text())
    assert source['referenceVersion']=='3.3.93847' and source['fixture']['cases']==48
    source_file=ROOT/'.local/class-skills/d2moo-pinned-source'/source['sourceFile']
    assert sha(source_file)==source['sourceSha256']
    import hashlib
    assert hashlib.sha256(function(source_file.read_text(),'sub_6FC7DA40').encode()).hexdigest()==source['executedRoutineSha256']
    for name,digest in source['staticReviewedRoutineHashes'].items():
        assert hashlib.sha256(function(source_file.read_text(),name).encode()).hexdigest()==digest
    for name,digest in source['currentTableHashes'].items():
        assert sha(ROOT/'tools/reference/d2r-named-equipment/json'/(name+'.json'))==digest
    assert sha(ROOT/source['fixture']['path'])==source['fixture']['sha256']
    assert sha(ROOT/'.local/class-skills/golem-roster-source-probe.cpp')==source['generatedSourceSha256']
    assert not (ROOT/'.local/class-skills/golem-roster-sanitizer.log').read_text()
    melee_path=ROOT/'tools/reference/melee-result-source-evidence.json'
    melee=json.loads(melee_path.read_text())
    assert melee['cases']==16384 and melee['legacyCommit']==source['legacyCommit']
    raw_path=ROOT/'.local/class-skills/d2moo-pinned-source'/melee['sourceFile']
    assert sha(raw_path)==melee['sourceSha256']
    assert hashlib.sha256(function(raw_path.read_text(),melee['routine']).encode()).hexdigest()==melee['unchangedRoutineSha256']
    assert sha(ROOT/melee['fixture']['path'])==melee['fixture']['sha256']
    assert sha(ROOT/'.local/class-skills/melee-result-source-probe.cpp')==melee['generatedSourceSha256']
    assert not (ROOT/'.local/class-skills/melee-result-sanitizer.log').read_text()
    phase_path=ROOT/'tools/reference/monster-weapon-phases-source-evidence.json'
    phase=json.loads(phase_path.read_text())
    assert phase['cases']==5184 and phase['legacyCommit']==source['legacyCommit']
    for relative,digest in phase['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/relative)==digest
    for name,digest in phase['unchangedRoutineHashes'].items():
        found=False
        for relative in phase['sourceHashes']:
            try:body=function((ROOT/'.local/class-skills/d2moo-pinned-source'/relative).read_text(),name)
            except AssertionError:continue
            found=found or hashlib.sha256(body.encode()).hexdigest()==digest
        assert found,name
    assert sha(Path(phase['parentFixture']['path']))==phase['parentFixture']['sha256']
    assert sha(ROOT/phase['parentFixture']['receipt'])==phase['parentFixture']['receiptSha256']
    assert sha(ROOT/phase['fixture']['path'])==phase['fixture']['sha256']
    assert sha(ROOT/'.local/class-skills/monster-weapon-phases-source-probe.cpp')==phase['generatedSourceSha256']
    assert not (ROOT/'.local/class-skills/monster-weapon-phases-sanitizer.log').read_text()
    allocation_path=ROOT/'tools/reference/npc-impact-allocation-source-evidence.json'
    allocation=json.loads(allocation_path.read_text())
    assert allocation['cases']==27000 and allocation['legacyCommit']==source['legacyCommit']
    allocation_file=ROOT/'.local/class-skills/d2moo-pinned-source'/allocation['sourceFile']
    assert sha(allocation_file)==allocation['sourceSha256']
    for name,digest in allocation['unchangedRoutineHashes'].items():
        assert hashlib.sha256(function(allocation_file.read_text(),name).encode()).hexdigest()==digest
    for name in ['path','receipt']:
        digest='sha256' if name=='path' else 'receiptSha256'
        assert sha(ROOT/allocation['parent'][name])==allocation['parent'][digest]
    assert sha(ROOT/allocation['separatePeriodicReference']['path'])==allocation['separatePeriodicReference']['sha256']
    assert sha(ROOT/allocation['fixture']['path'])==allocation['fixture']['sha256']
    assert sha(ROOT/'.local/class-skills/npc-impact-allocation-source-probe.cpp')==allocation['generatedSourceSha256']
    assert not (ROOT/'.local/class-skills/npc-impact-allocation-sanitizer.log').read_text()
    drain_path=ROOT/'tools/reference/npc-item-drain-source-evidence.json'
    drain=json.loads(drain_path.read_text())
    assert drain['cases']==144 and drain['legacyCommit']==source['legacyCommit'] and drain['currentPropertiesWritingDrainMaxima']==[]
    drain_file=ROOT/'.local/class-skills/d2moo-pinned-source'/drain['sourceFile']
    assert sha(drain_file)==drain['sourceSha256']
    for name,digest in drain['unchangedRoutineHashes'].items():
        assert hashlib.sha256(function(drain_file.read_text(),name).encode()).hexdigest()==digest
    for name,digest in drain['currentTableHashes'].items():assert sha(ROOT/'tools/reference/d2r-named-equipment/json'/(name+'.json'))==digest
    assert {code:row['id'] for code,row in drain['currentWriterRules'].items()}=={'lifesteal':60,'manasteal':62,'stamdrain':154}
    for name in ['path','receipt']:
        digest='sha256' if name=='path' else 'receiptSha256'
        assert sha(ROOT/drain['parent'][name])==drain['parent'][digest]
    assert sha(ROOT/drain['fixture']['path'])==drain['fixture']['sha256']
    assert sha(ROOT/'.local/class-skills/npc-item-drain-source-probe.cpp')==drain['generatedSourceSha256']
    assert not (ROOT/'.local/class-skills/npc-item-drain-sanitizer.log').read_text()
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    expected=Counter({'iron-golem-birth-check':3,'necromancer-pet-ai-check':3,'iron-golem-world-live-result':3,'iron-golem-retaliation-expiry':3,'melee-result-check':3,'monster-weapon-phases-check':3,'monster-contact-check':3,'monster-return-result':1})
    families=['iron-golem-items','npc-resource-callbacks','iron-golem-birth-graph','iron-golem-body','iron-golem-world','iron-golem-retaliation','golem-roster','monster-ordered-release','monster-return','prepared-weapon-event','npc-weapon-preparation','npc-equipment-damage','npc-equipment-elements','npc-equipment-poison','npc-equipment-critical','npc-impact-allocation']
    for family in families:
        expected[family+'-check']=1;expected[family+'-reload-check']=2
    assert sum(expected.values())==70 and Counter(re.findall(r'\b([a-z0-9-]+): passed\b',log))==expected
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'Golem roster:16four-type replacements' in log
    assert log.count('Monster weapon phases:5184complete-source/native compositions')==3
    assert log.count('NPC contact:3840complete-source releases/1616health commits')==3
    assert 'Ordered monster release:1616native complete-source compositions' in log
    assert 'NPC allocation:27000complete-source mitigation/total/prediction comparisons,21actual original' in log
    assert '3minimum-only item deliveries and3miss-NoFill controls pass' in log
    assert '60488' in log
    assert 'NPC preparation:12offensive snapshot/activation/stream controls,64native damage-ledger/master comparisons' in log
    for kind in ['content','integration']:
        text=(STAGE/(kind+'-build-2.log')).read_text()
        assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    audit_log=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count('Shutting down')==1
    assert Counter(re.findall(r'\b([a-z0-9-]+): passed\b',audit_log))==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        audit=json.loads((STAGE/name).read_text())
        assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old_audit=json.loads(subprocess.check_output(['git','show',FROZEN+':tools/reference/'+name],cwd=ROOT))
        if name.startswith('named'):
            assert len(audit['Definitions'])==573 and audit['Definitions']==old_audit['Definitions']
        else:
            for key,value in old_audit.items():
                if key not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes']:assert audit[key]==value,key
        audits.append(audit)
    python=(STAGE/'python-tests.log').read_text()
    assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    saved=json.loads((STAGE/'npc-impact-allocation-fixtures.json').read_text())
    impact=saved['Impact'];packet=impact['Packet']
    assert impact['Actor']==saved['Actor'] and impact['Target']==saved['Target'] and impact['CreditOwner']==saved['Owner']
    assert packet['Poison256']==impact['Poison']['Rate256']==64 and impact['Poison']['Frames']==75
    assert packet['Total256']==sum(packet.get(key,0) for key in ['Physical256','Fire256','Cold256','Lightning256','Magic256','Poison256'])
    assert saved['Life']==20000*256 and json.loads(saved['Gear'])['NamedItemId']=='unique/4'
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert status==build_skill_roadmap.build()
    handlers=[r for r in status['skills'] if r['implementation']=='handler-present']
    assert len(handlers)==185 and sum(r['characterClass']=='Necromancer' for r in handlers)==29 and not status['completedClasses']
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
    paths=[Path(__file__).resolve(),source_path,melee_path,phase_path,allocation_path,drain_path,ROOT/'tools/probe_npc_item_drain.py',ROOT/'tools/probe_npc_impact_allocation.py',ROOT/'tools/probe_melee_result.py',ROOT/'tools/probe_monster_weapon_phases.py']
    paths += [ROOT/name for name in ['tools/probe_golem_roster.py','tests/check_npc_item_drain.py','tests/check_npc_item_drain_audits.py',
        'docs/npc-item-drain-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/skill-mechanical-reference.md',
        'docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/name for name in ['pre-build-2.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','run-1.log',
        'content-build-2.log','integration-build-2.log','audit-native-checks.log','audit-run-1.log','python-tests.log',
        *[family+'-fixtures.json' for family in families]]]
    result=dict(referenceVersion='3.3.93847',component='Ordinary minimum-only NPC item drain compatibility',learnedIronGolemEnabled=False,
        installed=False,completedClasses=[],goalComplete=False,
        verified=dict(sourceItemDrainContexts=144,nativeMinimumOnlyItemDeliveries=3,sourceAllocationContexts=27000,nativeAllocationContexts=27000,nativeOriginalAllocationDeliveries=21,nativeMissNoFillControls=3,nativeSnapshotControls=12,nativeCreditComparisons=64,nativeRewardControls=1,sourcePhaseContexts=5184,nativePhaseCompositionsPerBoot=5184,retainedContactContextsPerBoot=3840,retainedOrderedHealthContexts=1616,retainedReturnContexts=60488,retainedSourceResultContexts=16384,nativeWorldHitBlockControlsPerBoot=4,retainedSourceAppendContexts=48,nativeReplacementPairs=16,combinedMarkers=70,boots=3,actualReloads=2,saves=3,
            naturalExpiryChecks=3,connectedWorldSessions=3,retainedPaladinAuditMarkers=4,pythonTests=71,namedDefinitions=573,
            auditFingerprints=[len(a['InputHashes']) for a in audits],unchangedInstalledArtifacts=106,handlers=185,necromancerHandlers=29),
        inputInventory=inputs,auditInputInventory=audited,artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=['Current property rules write life/mana minima60/62 and stamina-consumption stat154,with no property writing attack-drain maxima61/63/65.144complete unchanged older fill/range/total contexts separate ordinary minimum-only/zero-initial item fields from artificial maxima and skill-supplied initial fields.',
            'The allocator no longer rejects ordinary current item steal/stamina-consumption values. Runtime writer-plan guards retain the expected current minima and reject any positive maximum-writing property. Native Hellplague,Battlebranch and Soulfeast Tine allocations/releases retain injured source life and victim mana/stamina,without fabricating player leech or extra drain damage.',
            '70combined markers/3boots/2reloads retain27000allocation contexts,21other actual item deliveries,3miss/NoFill controls,offensive/credit/reward,equipped numeric and ordered/world scopes. Paladin/audits and71Python pass;573named decisions and106installed artifacts are unchanged.'],
        remaining=['Modern ordinary Iron Golem item-leech behavior remains unobserved;the minimum-only rule is an explicitly pinned older-reference interpretation. Positive attack maxima and skill-supplied drain fields/resource events remain unsupported and must not be inferred from this ordinary item scope.',
            'The live Iron route still needs real contact/event-registry/native notification/return/durable-credit adapters before migration and learned casting. Procs/state priority,burn/freeze,sunder,modern Thorns coexistence and full client acceptance remain unfinished.',
            'No class completion,full-class regression,old-content-binary migration,deployment or final art is claimed. Effects/audio and talent/hotkey icons remain separate.'])
    path=ROOT/'tools/reference/npc-item-drain-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('NPC ordinary item drain:144source/3actual item controls;70markers/3boots/2reloads;71Python;positive-max/skill drains and live actor migration remain separate.')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
