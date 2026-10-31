#!/usr/bin/env python3
"""Bind prepared flat-return/same-state acceptance without claiming full damage-event parity."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from snapshot_skill_build import check as check_snapshot
from review_weapon_event_order import function
import build_skill_roadmap

ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/iron-golem-retaliation';sha=inventory.sha
FROZEN='d206870'
def record(check=False):
    inventory.STAGE=STAGE
    inputs=inventory.validate_inventory(STAGE/'birth-inputs.json');audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ['birth-inputs.json','audit-inputs.json']:check_snapshot(STAGE/'pre-build-3.json',STAGE/name)
    assert inputs['artifacts']==audited['artifacts'] and inputs['copiedData']==audited['copiedData']
    prior=json.loads((ROOT/'tools/reference/iron-golem-world-native-evidence.json').read_text())['inputInventory']
    old={p:h for p,h in prior['sourceInputs'].items() if p.endswith('.cs')};new={p:h for p,h in inputs['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new)-set(old)=={'src/Sanctuary.Content/NpcEquipmentRetaliation.cs','tests/Sanctuary.Integration/IronGolemRetaliationChecks.cs'}
    assert {p for p in old if old[p]!=new[p]}=={'src/Sanctuary.Content/IronGolemBody.cs','src/Sanctuary.Content/PaladinAuras.cs','src/Sanctuary.Content/PaladinThorns.cs'}
    assert inputs['copiedData']==prior['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert sha(STAGE/'native/Distribution'/name)==inputs['artifacts'][name]==prior['artifacts'][name]
    source_path=ROOT/'tools/reference/iron-golem-retaliation-source-evidence.json';source=json.loads(source_path.read_text())
    assert source['referenceVersion']=='3.3.93847' and source['fixture']['eventCases']==80 and source['fixture']['replacementCases']==12 and source['fixture']['formulaControls']==255
    for name,digest in source['sourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/name)==digest
    for name,digest in source['unchangedExecutedRoutineHashes'].items()|source['staticReviewedRoutineHashes'].items():
        found=False
        for relative in source['sourceHashes']:
            try:body=function((ROOT/'.local/class-skills/d2moo-pinned-source'/relative).read_text(),name)
            except AssertionError:continue
            found=found or hashlib.sha256(body.encode()).hexdigest()==digest
        assert found,name
    for table,digest in source['currentTableHashes'].items():assert sha(ROOT/'tools/reference/d2r-named-equipment/json'/(table+'.json'))==digest
    assert sha(ROOT/source['fixture']['path'])==source['fixture']['sha256'] and sha(ROOT/'.local/class-skills/iron-golem-retaliation-source-probe.cpp')==source['generatedSourceSha256']
    assert not (ROOT/'.local/class-skills/iron-golem-retaliation-sanitizer.log').read_text()
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    expected=Counter({'iron-golem-birth-check':3,'necromancer-pet-ai-check':3,'iron-golem-world-live-result':3,'iron-golem-retaliation-expiry':3})
    for family in ['iron-golem-items','npc-resource-callbacks','iron-golem-birth-graph','iron-golem-body','iron-golem-world','iron-golem-retaliation']:
        expected[family+'-check']=1;expected[family+'-reload-check']=2
    assert sum(expected.values())==30 and Counter(re.findall(r'\b([a-z0-9-]+): passed\b',log))==expected
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    for kind in ['content','integration']:
        text=(STAGE/(kind+'-build-3.log')).read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    audit_log=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count('Shutting down')==1
    assert Counter(re.findall(r'\b([a-z0-9-]+): passed\b',audit_log))==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        audit=json.loads((STAGE/name).read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old_audit=json.loads(subprocess.check_output(['git','show',FROZEN+':tools/reference/'+name],cwd=ROOT))
        if name.startswith('named'):assert len(audit['Definitions'])==573 and audit['Definitions']==old_audit['Definitions']
        else:
            for key,value in old_audit.items():
                if key not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes']:assert audit[key]==value,key
        audits.append(audit)
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    saved=json.loads((STAGE/'iron-golem-retaliation-fixtures.json').read_text());assert len(saved)==2
    assert [r['Flat'] for r in saved]==[23,8] and [r['Replaced'] for r in saved]==[False,True]
    for row in saved:
        state=json.loads(row['State']);gear=json.loads(row['Gear'])
        assert gear['NamedItemId']=='unique/46' and row['Life']>0 and row['Life']<row['Maximum'] and state['Recipe']['Rank']==1
        assert state.get('IntrinsicThornsReplaced',False)==row['Replaced'] and state['Recipe']['Item']==row['Item'] and state['Recipe']['Owner']==row['Owner']
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert status==build_skill_roadmap.build()
    handlers=[r for r in status['skills'] if r['implementation']=='handler-present'];assert len(handlers)==185 and sum(r['characterClass']=='Necromancer' for r in handlers)==29 and not status['completedClasses']
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
    paths=[Path(__file__).resolve(),source_path]
    paths += [ROOT/name for name in ['tools/probe_iron_golem_retaliation.py','tests/check_iron_golem_retaliation.py','tests/check_iron_golem_retaliation_audits.py',
        'tests/export_runtime_audits.py','docs/iron-golem-retaliation-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md',
        'docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/name for name in ['pre-build-3.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','run-2.log','content-build-3.log','integration-build-3.log',
        'audit-native-checks.log','audit-run-3.log','python-tests.log','iron-golem-retaliation-fixtures.json','iron-golem-world-fixtures.json','iron-golem-body-fixtures.json',
        'iron-golem-items-fixtures.json','npc-resource-callbacks-fixtures.json','iron-golem-birth-graph-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Prepared Iron Golem/NPC flat retaliation and same-state history',learnedHandlerEnabled=False,installed=False,completedClasses=[],goalComplete=False,
        verified=dict(sourceEventCases=80,sourceReplacementCases=12,nativeTableTierControlsPerBoot=255,combinedMarkers=30,combinedBoots=3,actualReloads=2,saves=3,
            naturalExpiryChecks=3,connectedWorldSessions=3,retainedPaladinAuditMarkers=4,retainedPaladinAuditBoots=1,pythonTests=71,namedDefinitions=573,
            auditFingerprints=[len(a['InputHashes']) for a in audits],unchangedInstalledArtifacts=106,handlers=185,necromancerHandlers=29),
        inputInventory=inputs,auditInputInventory=audited,artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=['Born-rank flat stat78and original-item ATD join existing Barbs/Paladin physical aggregation. Actual owner Tarnhelm and original identification changes preserve the appropriate immutable/live contributions. Generic original-equipped Skeleton receives item8without innate15.',
            'Actual Barbs/zero-mana Paladin pulses exercise recipient delivery. Paladin same-state application replaces the untagged innate list under the recorded older reference;explicit clear/natural expiry and both reloads preserve replacement history. Current-build coexistence is not verified.',
            'Native miss/hit/zero-damage attempts,ranged/spell exclusion,physical immunity and non-recursion pass. Saved Lance of Yaggai originals/full rolls,exact injured life and replaced/unreplaced state survive both reloads. Existing world/body/graph/birth/resource scopes and3natural world sessions remain covered.',
            'Retained timed Paladin checks,catalogue exports and71Python tests pass with unchanged573named decisions/affix semantics and106installed artifacts.'],
        remaining=['The existing return bridge still has incoming HP/death/kill-event chronology gaps. This is not a complete source packet/mitigation/registration/duplication/return-mode implementation or modern executable certificate. Formula controls use table tiers,not compiled formula execution.',
            'Modern same-state coexistence and restoration require reference confirmation. Full wardrobe rollback,old-active-aura migration,owner/class/facet lifecycle,remaining item/proc/sunder and complete attack entry/contact/events remain unfinished.',
            'Unified golem replacement/cap,learned targeting/payment/placement and actual-client acceptance remain next. No class completion,full-class regression,old-content-binary migration,installation or new skill/icon is claimed. Final effects/audio and talent/hotkey icons remain separate.'])
    path=ROOT/'tools/reference/iron-golem-retaliation-native-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Prepared Iron Golem retaliation:80event/12replacement source cases,255tier controls,30native markers/3boots/2reloads/3expiry checks;71Python tests;learned cast/order parity remain gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
