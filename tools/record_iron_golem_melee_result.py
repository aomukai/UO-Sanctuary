#!/usr/bin/env python3
"""Bind source melee results and real Iron world shield-block acceptance."""
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
STAGE=ROOT/'.local/class-skills/iron-golem-melee-result'
FROZEN='d933cc5'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE
    inputs=inventory.validate_inventory(STAGE/'birth-inputs.json')
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ['birth-inputs.json','audit-inputs.json']:
        check_snapshot(STAGE/'pre-build-1.json',STAGE/name)
    assert inputs['artifacts']==audited['artifacts'] and inputs['copiedData']==audited['copiedData']
    prior=json.loads((ROOT/'tools/reference/golem-roster-native-evidence.json').read_text())['inputInventory']
    old={p:h for p,h in prior['sourceInputs'].items() if p.endswith('.cs')}
    new={p:h for p,h in inputs['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new)-set(old)=={'src/Sanctuary.Content/SourceMeleeResult.cs','tests/Sanctuary.Integration/MeleeResultChecks.cs'}
    assert {p for p in old if old[p]!=new[p]}=={'src/Sanctuary.Content/IronGolemWorld.cs'}
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
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log
    expected=Counter({'iron-golem-birth-check':3,'necromancer-pet-ai-check':3,'iron-golem-world-live-result':3,'iron-golem-retaliation-expiry':3,'melee-result-check':3})
    families=['iron-golem-items','npc-resource-callbacks','iron-golem-birth-graph','iron-golem-body','iron-golem-world','iron-golem-retaliation','golem-roster']
    for family in families:
        expected[family+'-check']=1;expected[family+'-reload-check']=2
    assert sum(expected.values())==36 and Counter(re.findall(r'\b([a-z0-9-]+): passed\b',log))==expected
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'Golem roster:16four-type replacements' in log
    for kind in ['content','integration']:
        text=(STAGE/(kind+'-build-1.log')).read_text()
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
    paths=[Path(__file__).resolve(),source_path,melee_path,ROOT/'tools/probe_melee_result.py']
    paths += [ROOT/name for name in ['tools/probe_golem_roster.py','tests/check_iron_golem_melee_result.py','tests/check_iron_golem_melee_result_audits.py',
        'docs/iron-golem-melee-result-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/skill-mechanical-reference.md',
        'docs/skill-effects.md','docs/skill-icons.md','tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json']]
    paths += [STAGE/name for name in ['pre-build-1.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','run-1.log',
        'content-build-1.log','integration-build-1.log','audit-native-checks.log','audit-run-1.log','python-tests.log',
        *[family+'-fixtures.json' for family in families]]]
    result=dict(referenceVersion='3.3.93847',component='Iron Golem source melee results and recipient block delivery',learnedIronGolemEnabled=False,
        installed=False,completedClasses=[],goalComplete=False,
        verified=dict(sourceResultContexts=16384,nativeWorldHitBlockControlsPerBoot=4,retainedSourceAppendContexts=48,nativeReplacementPairs=16,combinedMarkers=36,boots=3,actualReloads=2,saves=3,
            naturalExpiryChecks=3,connectedWorldSessions=3,retainedPaladinAuditMarkers=4,pythonTests=71,namedDefinitions=573,
            auditFingerprints=[len(a['InputHashes']) for a in audits],unchangedInstalledArtifacts=106,handlers=185,necromancerHandlers=29),
        inputInventory=inputs,auditInputInventory=audited,artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=['Complete unchanged older GetResultFlags matches16384native results and service traces per boot. Player/NPC/null/unsupported types,hostility/range,offsets,running,hit/miss,all avoidance bits and uninterruptible state are covered with declared callbacks.',
            'The actual Iron world action now consults the existing recipient block supplier after a successful hit. Four deterministic world controls per boot verify hit/miss and shield block/no-block:misses skip defender block RNG,misses/blocks skip weapon damage/critical RNG and health,unblocked hits damage the target.',
            'Shared recipient shield selection/block RNG and projected geometry/stat services are explicit adapters. Unsupported block recipients fault instead of silently taking an unblockable attack. Learned Iron remains disabled.',
            'Prior shared roster,body/world/retaliation checks,timed Paladin,catalogue audits and71Python tests remain covered;573named decisions and106installed artifacts are unchanged.'],
        remaining=['HP-before-retaliation,deferred death/kill-event dispatch,complete damage allocation/registration/return,remaining item procs/sunder,modern Thorns coexistence and learned paid casting remain unfinished. Result flags alone do not certify these phases.',
            'The full hit arithmetic and block/dodge internals are supplied services in the source oracle;the native actor uses existing shared services,whose component/RNG scope is not current executable parity. Modern full AI/event timing and actual client acceptance remain separate.',
            'No class completion,full-class regression,old-content-binary migration,deployment or new art is claimed. Effects/audio and talent/hotkey icons remain separate.'])
    path=ROOT/'tools/reference/iron-golem-melee-result-native-evidence.json'
    text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Iron melee result:16384source contexts/4world block controls per boot,36markers/3boots/2reloads;71Python;HP/death/learned Iron remain gated.')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
