#!/usr/bin/env python3
"""Bind lethal incoming events and retired-caster persistence acceptance."""
import argparse
from pathlib import Path
from record_npc_incoming_procs import expected as previous_expected
import skill_checkpoint_evidence as evidence
from review_npc_retained_target import build as review

ROOT=evidence.ROOT
STAGE=ROOT/'.local/class-skills/npc-incoming-lethal'
WIDE=ROOT/'.local/class-skills/npc-incoming-lethal-broader'
COMPACT=ROOT/'.local/class-skills/npc-incoming-lethal-compact'
PARENT='ecc503448a8e5f5d14b71ff8d5263b0d2156d855'
def expected(broader=False,compact=False):
    result=previous_expected(broader,compact)
    result['npc-incoming-lethal-check']+=1;result['npc-incoming-lethal-reload-check']+=2
    return result

def build(check=False):
    born=evidence.inventory(STAGE,WIDE,STAGE/'pre-build-1.json')
    before=evidence.load(ROOT/'.local/class-skills/npc-incoming-procs/pre-build-4.json')['inputs'];now=evidence.load(STAGE/'pre-build-1.json')['inputs']
    changed={p for p in before['sourceInputs'].keys()|now['sourceInputs'].keys()if before['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}
    assert changed=={'tests/Sanctuary.Integration/NpcIncomingProcChecks.cs','tests/Sanctuary.Integration/NpcIncomingLethalChecks.cs'}
    assert before['copiedData']==now['copiedData']and before['projectInputs']==now['projectInputs']
    parent=evidence.load(ROOT/'tools/reference/npc-incoming-procs-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll','Assemblies/Sanctuary.Content.dll'):assert born['artifacts'][name]==parent['artifacts'][name]
    for stage,broader,compact,count in ((STAGE,False,False,386),(WIDE,True,False,602),(COMPACT,False,True,15)):
        log=evidence.cohort(stage,expected(broader,compact),count);assert 'NPC incoming lethal:2actual melee/missile fatal receivers' in log
        saved=evidence.load(stage/'npc-incoming-lethal-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==2
        for name in ('Owner','Retired','Enemy','Item'):assert len({r[name]for r in saved['Actors']})==2
        for row in saved['Actors']:
            assert row['Curse']['Source']==row['Retired']and row['Curse']['Rank']==5 and row['Penalty']<0 and row['Life']>0 and row['Mana']==0
        assert evidence.load(stage/'npc-incoming-proc-fixtures.json')['Generation']==2
        inventory=evidence.load(stage/'birth-inputs.json');assert inventory['artifacts']==born['artifacts']and inventory['copiedData']==born['copiedData']
        for path,digest in inventory['sourceInputs'].items():assert evidence.sha(ROOT/path)==digest,path
    assert all(t in (STAGE/'integration-build-1.log').read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    audits=evidence.audits(STAGE,PARENT,born['artifacts']);evidence.verification(STAGE);protected=evidence.protection();review(True)
    status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
    files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tools/review_npc_retained_target.py',ROOT/'tools/reference/npc-retained-target-preflight.json',ROOT/'tests/check_npc_incoming_lethal.py']+[ROOT/p for p in changed]
    files += [ROOT/'docs'/name for name in ('npc-incoming-lethal-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
    files+=evidence.stage_files(STAGE,WIDE,COMPACT)
    inspection=evidence.load(ROOT/'tools/reference/npc-retained-target-preflight.json')
    for path,digest in inspection['inputHashes'].items():assert evidence.sha(ROOT/path)==digest,path
    receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],productionAssemblyReused=True,
        integrationBuild=1,sourceSnapshot=1,focusedRun=1,broaderRun=1,compactRun=1,focusedMarkers=386,broaderMarkers=602,compactMarkers=15,
        bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,originalSavedRetiredCasterCasesPerCohort=2,pythonTests=71,auditFingerprints=audits,protection=protected,
        scope='Fatal qualified melee/missile receiving casts before HP;one death/free;explicit completed retirement removes graph/RNG while original owner/item/attacker curse and actual damage penalty persist through2reloads.',
        limits=['Tests supply qualified post-defense packets and complete retirement explicitly if needed;automatic native scalar attack/AI/mitigation admission is not inferred.',
            'The retained-target source dependency record is inspection only;modern callback/registration parity,reflected attacker death,removed-target fallback and reentrant removal remain unfinished.',
            'No production code/data or native/client/protocol/layout change. Generated NPC equipment,Valkyrie/Shadow actors,other class skills and effects/icons remain unfinished;no class is complete or deployed.'])
    evidence.write('npc-incoming-lethal-native-evidence.json',receipt,files,check)
    print('NPC incoming lethal:386focused/602broader/15compact markers;2retired caster/original item/curse cases through2reloads;reused production,71Python/audits/protection pass.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
