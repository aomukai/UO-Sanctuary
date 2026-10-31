#!/usr/bin/env python3
"""Bind shared golem roster acceptance without enabling learned Iron Golem."""
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
STAGE=ROOT/'.local/class-skills/golem-roster'
FROZEN='73d3712'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE
    inputs=inventory.validate_inventory(STAGE/'birth-inputs.json')
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ['birth-inputs.json','audit-inputs.json']:
        check_snapshot(STAGE/'pre-build-2.json',STAGE/name)
    assert inputs['artifacts']==audited['artifacts'] and inputs['copiedData']==audited['copiedData']
    prior=json.loads((ROOT/'tools/reference/iron-golem-retaliation-native-evidence.json').read_text())['inputInventory']
    old={p:h for p,h in prior['sourceInputs'].items() if p.endswith('.cs')}
    new={p:h for p,h in inputs['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new)-set(old)=={'src/Sanctuary.Content/NecromancerGolems.cs','tests/Sanctuary.Integration/GolemRosterChecks.cs'}
    assert {p for p in old if old[p]!=new[p]}=={'src/Sanctuary.Content/BattleCommand.cs','src/Sanctuary.Content/IronGolemBody.cs','src/Sanctuary.Content/NecromancerSummons.cs'}
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
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    expected=Counter({'iron-golem-birth-check':3,'necromancer-pet-ai-check':3,'iron-golem-world-live-result':3,'iron-golem-retaliation-expiry':3})
    families=['iron-golem-items','npc-resource-callbacks','iron-golem-birth-graph','iron-golem-body','iron-golem-world','iron-golem-retaliation','golem-roster']
    for family in families:
        expected[family+'-check']=1;expected[family+'-reload-check']=2
    assert sum(expected.values())==33 and Counter(re.findall(r'\b([a-z0-9-]+): passed\b',log))==expected
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'Golem roster:16four-type replacements' in log
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
    saved=json.loads((STAGE/'golem-roster-fixtures.json').read_text())
    assert len(saved)==2 and {r['Member'] for r in saved}=={True,False}
    assert len({r['Owner'] for r in saved})==1 and len({r['Item'] for r in saved})==2
    assert next(r for r in saved if r['Member'])['Order']>0
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert status==build_skill_roadmap.build()
    handlers=[r for r in status['skills'] if r['implementation']=='handler-present']
    assert len(handlers)==185 and sum(r['characterClass']=='Necromancer' for r in handlers)==29 and not status['completedClasses']
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
    paths=[Path(__file__).resolve(),source_path]
    paths += [ROOT/name for name in ['tools/probe_golem_roster.py','tests/check_golem_roster.py','tests/check_golem_roster_audits.py',
        'docs/golem-roster-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/skill-mechanical-reference.md',
        'docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/name for name in ['pre-build-2.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','run-2.log',
        'content-build-2.log','integration-build-2.log','audit-native-checks.log','audit-run-2.log','python-tests.log',
        *[family+'-fixtures.json' for family in families]]]
    result=dict(referenceVersion='3.3.93847',component='Shared Necromancer golem roster/replacement',learnedIronGolemEnabled=False,
        installed=False,completedClasses=[],goalComplete=False,
        verified=dict(sourceAppendContexts=48,nativeReplacementPairs=16,combinedMarkers=33,boots=3,actualReloads=2,saves=3,
            naturalExpiryChecks=3,connectedWorldSessions=3,retainedPaladinAuditMarkers=4,pythonTests=71,namedDefinitions=573,
            auditFingerprints=[len(a['InputHashes']) for a in audits],unchangedInstalledArtifacts=106,handlers=185,necromancerHandlers=29),
        inputInventory=inputs,auditInputInventory=audited,artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=['Current four golem skills share pettype=golem and cap1. Complete unchanged older append helper verifies FIFO under supplied allocation/removal/client services; cap setter and registration are static reviews.',
            'Sixteen native replacement pairs retain separate skeleton/mage/owner categories and destroy replaced Iron originals. Actual Clay/Blood/Fire factory and prepared Iron roster commit are exercised. Invalid placement and injected pre-commit Iron setup failure preserve existing pets/originals.',
            'Parked Battle Command cap refresh includes joined Iron bodies, uses recorded birth order and a disclosed order0/serial historical fallback. Storage/stat bodies remain excluded until explicitly committed. Membership,order,original identity and full rolls survive two reloads.',
            'Retained body/world/retaliation checks, timed Paladin, catalogue audits and71Python tests pass;573named decisions and106installed artifacts remain unchanged.'],
        remaining=['Learned Iron targeting/payment/placement and complete attack entry/contact/HP/death/event ordering,procs/sunder and current-state Thorns coexistence remain unfinished. This increment does not redesign mana payment or certify full creation transactions.',
            'Existing Fire replacement explosion hook is retained; refresh remains delete-only. Complete destruction/explosion event parity and current executable certification remain separate. Historical untracked FIFO cannot be reconstructed.',
            'No class completion,full-class regression,old-content-binary migration,deployment or new art is claimed. Final effects/audio and talent/hotkey icons remain separate. Revive retains living abilities; global monster spell conversion is separate.'])
    path=ROOT/'tools/reference/golem-roster-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Shared golem roster:48source contexts,16native pairs,33markers/3boots/2reloads;71Python;learned Iron remains gated.')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
