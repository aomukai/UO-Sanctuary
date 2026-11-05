#!/usr/bin/env python3
"""Bind qualified NPC incoming events and original carrier persistence."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
from record_raven_snapshot import expected as previous_expected
import skill_checkpoint_evidence as evidence

ROOT=evidence.ROOT
STAGE=ROOT/'.local/class-skills/npc-incoming-procs'
WIDE=ROOT/'.local/class-skills/npc-incoming-procs-broader'
COMPACT=ROOT/'.local/class-skills/npc-incoming-procs-compact'
PARENT='cdf4ea4e868cc483a9cc091c961f3ba07f5d6a19'
def expected(broader=False,compact=False):
    result=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})if compact else previous_expected(broader)
    result['npc-incoming-proc-check']+=1;result['npc-incoming-proc-reload-check']+=2
    return result

def build(check=False):
    born=evidence.inventory(STAGE,WIDE,STAGE/'pre-build-4.json')
    before=evidence.load(ROOT/'.local/class-skills/raven-snapshot/pre-build-1.json')['inputs'];now=evidence.load(STAGE/'pre-build-4.json')['inputs']
    production={'NpcCombatEvents','NpcItemProcGraph','NpcItemProcs','PreparedMonsterDamage'}
    changed={p for p in before['sourceInputs'].keys()|now['sourceInputs'].keys()if before['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}
    assert changed=={f'src/Sanctuary.Content/{n}.cs'for n in production}|{'tests/Sanctuary.Integration/NpcIncomingProcChecks.cs'}
    assert before['projectInputs']==now['projectInputs']
    delta={p for p in before['copiedData'].keys()|now['copiedData'].keys()if before['copiedData'].get(p)!=now['copiedData'].get(p)}
    assert delta=={'Data/Sanctuary/Balance/class-skill-status.json'}
    status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    prior=json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    assert {k:v for k,v in prior.items()if k!='skills'}=={k:v for k,v in status.items()if k!='skills'}
    notes=[]
    for a,b in zip(prior['skills'],status['skills'],strict=True):
        if a!=b:
            assert {k:v for k,v in a.items()if k!='remaining'}=={k:v for k,v in b.items()if k!='remaining'}
            assert 'qualified incoming201' in b['remaining'];notes.append(b['talentId'])
    assert set(notes)=={'amazon.valkyrie','assassin.shadowwarrior','assassin.shadowmaster'}
    assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
    content=evidence.load(STAGE/'pre-build-4.json')['inputs'];integration=content
    assert {p:h for p,h in content['sourceInputs'].items()if not p.startswith('tests/')}=={p:h for p,h in now['sourceInputs'].items()if not p.startswith('tests/')}
    assert integration['sourceInputs']==now['sourceInputs']
    for stage,broader,compact,count in ((STAGE,False,False,383),(WIDE,True,False,599),(COMPACT,False,True,12)):
        log=evidence.cohort(stage,expected(broader,compact),count);assert 'NPC incoming procs:actual stat201/function21' in log
        saved=evidence.load(stage/'npc-incoming-proc-fixtures.json');assert saved['Generation']== 2 and len(saved['Items'])==2
        graph=saved['Graph'];assert graph['Actor']==saved['Body']and graph['Owner']==saved['Owner']and {i['Item']for i in graph['Items']}==set(saved['Items'])
        assert len(graph['Events']['Entries'])== 2 and all(e['Function']== 21 and e['Event']in (1,2)for e in graph['Events']['Entries'])
        assert saved['Curse']['Source']==saved['Body']and saved['Curse']['Rank']== 5 and saved['Life']> 0 and saved['Mana']==0
        current=evidence.load(stage/'birth-inputs.json');assert current['artifacts']==born['artifacts']and current['copiedData']==born['copiedData']
        for path,digest in current['sourceInputs'].items():assert evidence.sha(ROOT/path)==digest,path
    for name in ('content-build-2.log','integration-build-3.log'):
        assert all(t in (STAGE/name).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    audits=evidence.audits(STAGE,PARENT,born['artifacts']);evidence.verification(STAGE);protected=evidence.protection()
    prior=evidence.load(ROOT/'tools/reference/raven-snapshot-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==prior['artifacts'][name]
    files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tests/check_npc_incoming_procs.py',ROOT/'tools/build_skill_roadmap.py',ROOT/'data/Sanctuary/Balance/class-skill-status.json']+[ROOT/p for p in changed]
    files += [ROOT/'docs'/name for name in ('npc-incoming-procs-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
    files+=evidence.stage_files(STAGE,WIDE,COMPACT)
    for name in ('failed-compact-1','pre-status-compact-pass'):
        folder=STAGE/name;files += [p for p in folder.rglob('*')if p.is_file()]
        old=evidence.load(folder/'birth-inputs.json')
        for kind in ('Content','Integration'):assert evidence.sha(folder/kind.lower()/f'Sanctuary.{kind}.dll')==old['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    for stage in (STAGE,WIDE,COMPACT):
        folder=stage/'pre-deleted-carrier-pass';files += [p for p in folder.rglob('*')if p.is_file()]
        old=evidence.load(folder/'birth-inputs.json')
        for kind in ('Content','Integration'):assert evidence.sha(folder/kind.lower()/f'Sanctuary.{kind}.dll')==old['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],
        artifacts=born['artifacts'],copiedData=born['copiedData'],contentBuild=2,integrationBuild=3,sourceSnapshot=4,focusedRun=2,broaderRun=2,compactRun=4,
        focusedMarkers=383,broaderMarkers=599,compactMarkers=12,bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,
        originalSavedActorAndTwoCarriersPerCohort=True,callback=21,stat=201,pythonTests=71,auditFingerprints=audits,protection=protected,
        scope='Shared qualified ordered incoming packet producer and event-only stat201curse carriers;pre-health delivery,packet gates,defender RNG/order,actual HP and original state through2reloads.',
        limits=['New incoming cases supply explicit post-defense packets;no native scalar UO damage flags,automatic monster AI or new mitigation parity is inferred.',
            'Carrier scope requires the existing original-item/unarmed/Tainted/frozen-Revive RNG binding and eight admitted do30curses. Player/removed-target contexts remain unsupported.',
            'Lethal receiver/caster-retirement,reflected attacker death and reentrant removal need dedicated acceptance. Full generated NPC equipment and Valkyrie/Shadow actor/cast/lifecycle,other event/spell/charge services and class roadmap/assets remain unfinished. No learned handler,class completion, deployment,native/client/protocol/persistence ID change.'])
    evidence.write('npc-incoming-procs-native-evidence.json',receipt,files,check)
    print('NPC incoming procs:383focused/599broader/12compact markers;original actor,two carriers and incoming recasts through2reloads;71Python/audits/protection pass.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
