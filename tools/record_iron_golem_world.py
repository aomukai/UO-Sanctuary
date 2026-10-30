#!/usr/bin/env python3
"""Bind prepared Iron Golem world acceptance to the exact tested source/data/assemblies."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import subprocess
import record_grizzly_body as inventory
from snapshot_skill_build import check as check_snapshot
import build_skill_roadmap
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/iron-golem-world'
sha=inventory.sha
FROZEN='882b52d'

def record(check=False):
    inventory.STAGE=STAGE
    inputs=inventory.validate_inventory(STAGE/'birth-inputs.json')
    audit_inputs=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for name in ['birth-inputs.json','audit-inputs.json']:check_snapshot(STAGE/'pre-build-7.json',STAGE/name)
    assert inputs['artifacts']==audit_inputs['artifacts'] and inputs['copiedData']==audit_inputs['copiedData']
    prior=json.loads((ROOT/'tools/reference/necromancer-pet-ai-native-evidence.json').read_text())['inputInventory']
    old={p:h for p,h in prior['sourceInputs'].items() if p.endswith('.cs')};new={p:h for p,h in inputs['sourceInputs'].items() if p.endswith('.cs')}
    assert set(new)-set(old)=={'src/Sanctuary.Content/IronGolemWorld.cs','tests/Sanctuary.Integration/IronGolemWorldChecks.cs'}
    assert {p for p in old if old[p]!=new[p]}=={'src/Sanctuary.Content/IronGolemBody.cs','src/Sanctuary.Content/CreatureProfiles.cs','src/Sanctuary.Content/NpcEquipmentCritical.cs'}
    assert {p for p in prior['copiedData'] if prior['copiedData'][p]!=inputs['copiedData'][p]}=={
        'Data/Sanctuary/Balance/necromancer-summons.json','Data/Sanctuary/Balance/class-skill-status.json','Data/Sanctuary/Balance/skill-effects.json'}
    for p in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert sha(STAGE/'native/Distribution'/p)==inputs['artifacts'][p]==prior['artifacts'][p]
    proof_path=ROOT/'tools/reference/iron-golem-world-preflight.json';proof=json.loads(proof_path.read_text())
    assert proof['referenceVersion']=='3.3.93847' and proof['corroboratedFields']==120 and len(proof['animation']['records'])==6
    for relative,digest in proof['reviewedSourceHashes'].items():assert sha(ROOT/'.local/class-skills/d2moo-pinned-source'/relative)==digest
    for name,digest in proof['reviewedRoutineHashes'].items():
        found=False
        for relative in proof['reviewedSourceHashes']:
            try:body=function((ROOT/'.local/class-skills/d2moo-pinned-source'/relative).read_text(),name)
            except AssertionError:continue
            found=found or hashlib.sha256(body.encode()).hexdigest()==digest
        assert found,name
    source=json.loads((ROOT/'tools/reference/necromancer-pet-ai-source-evidence.json').read_text())
    assert source['fixture']['cases']==1598 and sha(ROOT/source['fixture']['path'])==source['fixture']['sha256']
    log=(STAGE/'birth-native-checks.log').read_text();assert ': failed' not in log and ' ERR]' not in log
    expected=Counter({'iron-golem-birth-check':3,'necromancer-pet-ai-check':3,'iron-golem-world-live-result':3})
    for family in ['iron-golem-items','npc-resource-callbacks','iron-golem-birth-graph','iron-golem-body','iron-golem-world']:
        expected[family+'-check']=1;expected[family+'-reload-check']=2
    assert Counter(re.findall(r'\b([a-z0-9-]+): passed\b',log))==expected and sum(expected.values())==24
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert log.count('Iron Golem body recipes:1008 applicable expansion source compositions')==3
    assert log.count('1598 complete source ordered-service/state/draw comparisons')==3
    for kind in ['content','integration']:
        build=(STAGE/(kind+'-build-7.log')).read_text();assert all(x in build for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    audits=[]
    for name in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        path=STAGE/name;audit=json.loads(path.read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==inputs['artifacts']['Assemblies/Sanctuary.Content.dll']
        for source,digest in audit['InputHashes'].items():assert sha(ROOT/source)==digest,source
        historical=json.loads(subprocess.check_output(['git','show',FROZEN+':tools/reference/'+name],cwd=ROOT))
        if name.startswith('named'):assert len(audit['Definitions'])==573 and audit['Definitions']==historical['Definitions']
        else:
            assert audit.keys()==historical.keys()
            for key,value in historical.items():
                if key not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes']:assert audit[key]==value,key
        audits.append(audit)
    audit_log=(STAGE/'audit-native-checks.log').read_text()
    assert ': failed' not in audit_log and ' ERR]' not in audit_log and audit_log.count('Shutting down')==1
    assert Counter(re.findall(r'\b([a-z0-9-]+): passed\b',audit_log))==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    saved=json.loads((STAGE/'iron-golem-world-fixtures.json').read_text())
    unique=json.loads((ROOT/'tools/reference/d2r-named-equipment/json/uniqueitems.json').read_text())
    battlebranch=next(row for row in unique.values() if row.get('index')=='The Battlebranch')
    assert json.loads(saved['Gear'])['NamedItemId']=='unique/'+str(battlebranch['*ID'])
    assert saved['Seed']==24601 and saved['Life']>0 and saved['Life']<saved['Maximum'] and saved['State']['Mode']==1 and saved['State']['Attempts']==0
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert status==build_skill_roadmap.build()
    handlers=[row for row in status['skills'] if row['implementation']=='handler-present']
    assert len(handlers)==185 and sum(row['characterClass']=='Necromancer' for row in handlers)==29 and not status['completedClasses']
    assert next(row for row in status['skills'] if row['talentId']=='necromancer.irongolem')['implementation']=='blocked'
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==digest,p
    files=[Path(__file__).resolve(),proof_path]
    files += [ROOT/p for p in ['tools/prepare_iron_golem_world_reference.py','tools/build_class_skills.py','tools/build_skill_roadmap.py',
        'tests/check_iron_golem_world.py','tests/check_iron_golem_birth.py','docs/iron-golem-world-checkpoint.md','docs/class-skills-resume-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md','docs/named-item-readiness.md',
        'tools/reference/named-item-runtime-audit.json','tools/reference/first-loop-affix-audit.json','tools/reference/necromancer-pet-ai-source-evidence.json']]
    files += [STAGE/p for p in ['pre-build-7.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','run-6.log','content-build-7.log',
        'integration-build-7.log','audit-native-checks.log','audit-run-7.log','python-tests.log','iron-golem-world-fixtures.json',
        'iron-golem-body-fixtures.json','iron-golem-items-fixtures.json','npc-resource-callbacks-fixtures.json','iron-golem-birth-graph-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Prepared saved Iron Golem world controller',learnedHandlerEnabled=False,preparedMovingActorEnabled=True,
        installed=False,completedClasses=[],goalComplete=False,
        verified=dict(worldMarkers=24,worldBoots=3,actualReloads=2,saves=3,connectedNaturalSessions=3,auditMarkers=2,auditBoots=1,
            pythonTests=71,namedDefinitions=573,auditFingerprints=[len(a['InputHashes']) for a in audits],unchangedInstalledArtifacts=106,
            sourcePolicyCasesPerBoot=1598,applicableBodyRecipesPerBoot=1008,retainedBirthCasesPerBoot=36720,retainedResistanceCasesPerBoot=81,
            retainedGraphCasesPerBoot=2016,handlers=185,necromancerHandlers=29),inputInventory=inputs,auditInputInventory=audit_inputs,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in files},
        established=['Optional saved controller reuses existing ground-pet geometry,path,motion,owner routes,animation scheduling and exact-health/original-item services.Native skeleton AI/melee remain suppressed. Due actors consume frames in time/serial order.',
            'AI and item attacks share one persisted actor/original RNG. Plain G4contact35/end41,Battlebranch30IAS/base10/speed291,source physical/item contact,follow,armor weight/faster movement,exact recovery and interruption/offline cancellation pass. Three connected sessions release actual attacks on natural40mstimers.',
            'Original Battlebranch identity/full rolls,exact injured life/maxima,controller and seed survive both reloads without backlog healing/attacks. Prior stat-only bodies and original/graph/resource scopes remain covered.573named decisions,affix semantics and106installed artifacts stay unchanged.'],
        remaining=['Prepared attack uses existing release-time NPC equipment resolution;source entry-packet capture,shield/block/result flags,retaliation/procs/skill events,global event/RNG/mode chronology and current-executable parity remain unfinished.',
            'Target ordering,native fit/LOS/facet-wide rooms and neutral/busy cadence remain adapters. Complete target query services,footprint special cases,AStar/path repair,spawn/first-think overlap,current initializer/stat transport,innate attributes,resource/item/clock contexts and modern sunder are not certified.',
            'Unified golem replacement/cap,learned input/payment/placement,full owner death/logout/respec/facet and actual-client acceptance remain next. No learned handler,class completion,old-content-binary migration,full-class regression or deployment is claimed. Final effects/audio and talent/hotkey icons remain separately pending.'])
    path=ROOT/'tools/reference/iron-golem-world-native-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==rendered
    else:path.write_text(rendered)
    print('Prepared Iron Golem world:24markers/3boots/2reloads/3natural sessions;71Python tests;804/21audit fingerprints,573named decisions and106installed artifacts unchanged;learned cast still gated.')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
