#!/usr/bin/env python3
"""Bind approved normal Consume corpse input and original/bound persistence."""
import argparse
from collections import Counter
import json
import re
import subprocess
import sys
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT
STAGE=ROOT/'.local/class-skills/consume-learned'
sys.path.insert(0,str(ROOT/'tests'))
from check_consume_learned import configuration

def expected_markers():
    c=configuration();r=Counter({'iron-golem-birth-check':3,'consume-aura-save-check':3})
    for f in ('iron-golem-items','npc-resource-callbacks',*c['extra_families']):
        for suffix,count in (('-check',1),('-reload-check',2)):
            n=f+suffix;r[n]+=count
            if n in c['extra_wait_results']:r[c['extra_wait_results'][n]]+=count
    for n in c['extra_target_input_commands']:r[n+'-check']+=3;r[n+'-result']+=3
    assert sum(r.values())==60
    return r

def record(check=False):
    approval=e.load(ROOT/'tools/reference/warlock-final-talents-approval.json')
    assert approval['approved'] and 'warlock.consume' in approval['approvedTalents']
    for p,h in approval['filesAtApprovalSha256'].items():assert e.sha(ROOT/p)==h,p
    born=e.load(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json')
    parent=e.load(ROOT/'.local/class-skills/blood-boil-learned/birth-inputs.json')
    changed={p for p,h in born['sourceInputs'].items() if p.endswith('.cs') and parent['sourceInputs'].get(p)!=h}
    assert changed=={'shared/ClassAbilities.cs','tests/Sanctuary.Integration/ConsumeAuraChecks.cs','src/Sanctuary.Content/ConsumeEffects.cs','src/Sanctuary.Content/ConsumeRosterGump.cs','src/Sanctuary.Content/WarlockCombat.cs','tests/Sanctuary.Integration/ConsumeWorldChecks.cs','tests/Sanctuary.Integration/ConsumeSupplierChecks.cs','tests/Sanctuary.Integration/ConsumeLearnedChecks.cs'}
    for p,h in born['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
    assert {p for p,h in born['copiedData'].items() if h!=parent['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    old=json.loads(subprocess.check_output(['git','show','91a88b54:data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT));current=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert [a['talentId'] for a,b in zip(old['skills'],current['skills'],strict=True) if a!=b]==['warlock.consume']
    assert old|{'skills':current['skills']}==current and sum(r['implementation']=='handler-present' for r in current['skills'])==240
    for p,h in born['copiedData'].items():assert e.sha(ROOT/'data'/p.removeprefix('Data/'))==h,p
    for p in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert e.sha(STAGE/'native/Distribution'/p)==born['artifacts'][p]==parent['artifacts'][p]
    for k in ('Content','Integration'):
        assert e.sha(STAGE/k.lower()/f'Sanctuary.{k}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{k}.dll']
        build=(STAGE/(k.lower()+'-build-4.log')).read_text();assert all(v in build for v in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    assert 'Approved=>true' in (ROOT/'src/Sanctuary.Content/ConsumeEffects.cs').read_text()
    log=e.cohort(STAGE,expected_markers(),60)
    assert 'pass across3boots/2reloads' in (STAGE/'native-run-4.log').read_text()
    assert log.count('Client: 127.0.0.1: Connected.')==27 and 'denied by connection filter' not in log
    for mode in ('right','quick','defiler','menu'):assert log.count('consume-learned-'+mode+': windup')==3
    files=[ROOT/'tools/record_consume_learned.py',ROOT/'docs/consume-learned-checkpoint.md',ROOT/'tools/reference/consume-learned-client-evidence.json',ROOT/'tools/build_skill_roadmap.py',ROOT/'data/Sanctuary/Balance/class-skill-status.json',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/check_britain.py',STAGE/'source-snapshot-4.json',STAGE/'birth-inputs.json',STAGE/'birth-native-checks.log',STAGE/'native-run-4.log',STAGE/'content-build-4.log',STAGE/'integration-build-4.log',STAGE/'failed-run-1.tar.gz',STAGE/'failed-run-2.tar.gz',STAGE/'failed-run-3.tar.gz',*configuration()['extra_inputs']]
    result=dict(component='Learned Consume original/bound selected retirement and town menu',referenceVersion='3.3.93847',approved=True,defaultRuleEnabled=True,nativeMarkers=60,markers=dict(sorted(expected_markers().items())),connections=27,actualAbilityWireInputs=12,inputModes=['right','quick','defiler quick','town menu quick'],boots=3,saves=3,reloads=2,globalHandlers=240,warlockHandlers=30,remainingApprovedTalents=[],contentBuild=4,integrationBuild=4,sourceSnapshot=4,nativeRun=4,artifacts=born['artifacts'],changedSources=sorted(changed),runtimeDataChange='Consume handler/verification/remaining row only',nativeRebuilt=False,persistedSchemaChanged=False,installed=False,classCertified=False,goalComplete=False,saveDeletionWarnings=re.findall(r'Warning: Attempted to delete (.+) during world save\.',log),protection=e.protection('tools/reference/consume-learned-client-evidence.json',44),limits='Normal right/quick packets and automatic timing/release qualify all three original species and a qualified native Daemon. Zero-target quick opens the actual town roster;its response is exercised through the real Gump callback,not a second socket reply. Four existing supplier/world/bound/aura families retain numeric writers,relative life,logical pool order,absolute expiry,safe/offline saves and existing provided auras. No graphical purchase or whole-class certificate;current do162/global/special/PvP/property/unknown species behavior and final effects/icons remain explicit. No charms/runewords/current-client reads/deployment.')
    e.write('consume-learned-native-evidence.json',result,files,check)
    print('Consume:60 native results/12 wire casts/27connections/two reloads;240global/30Warlock handlers.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
