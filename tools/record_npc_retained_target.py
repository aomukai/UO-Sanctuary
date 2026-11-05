#!/usr/bin/env python3
"""Bind reflected source death and original logical-target bridge acceptance."""
import argparse
from pathlib import Path
from record_npc_incoming_lethal import expected as previous_expected
import skill_checkpoint_evidence as evidence
from review_npc_retained_target import build as review

ROOT=evidence.ROOT
STAGE=ROOT/'.local/class-skills/npc-retained-target'
WIDE=ROOT/'.local/class-skills/npc-retained-target-broader'
COMPACT=ROOT/'.local/class-skills/npc-retained-target-compact'
PARENT='3a61d72e0c74d68123a9a37ca4bcda0f93a2501b'
CHANGED={'src/Sanctuary.Content/'+name+'.cs'for name in ('NecromancerCurseCasts','NpcItemProcGraph','NpcItemProcs','NpcItemSkillExecution','NpcItemSpellCasts','NpcItemTarget','PreparedMonsterDamage')}
CHANGED.add('tests/Sanctuary.Integration/NpcRetainedTargetChecks.cs')

def expected(broader=False,compact=False):
    result=previous_expected(broader,compact)
    result['npc-retained-target-check']+=1;result['npc-retained-target-reload-check']+=2
    return result

def build(check=False):
    born=evidence.inventory(STAGE,WIDE,STAGE/'pre-build-1.json')
    before=evidence.load(ROOT/'.local/class-skills/npc-incoming-lethal/pre-build-1.json')['inputs'];now=evidence.load(STAGE/'pre-build-1.json')['inputs']
    changed={p for p in before['sourceInputs'].keys()|now['sourceInputs'].keys()if before['sourceInputs'].get(p)!=now['sourceInputs'].get(p)}
    assert changed==CHANGED,changed
    assert before['copiedData']==now['copiedData']and before['projectInputs']==now['projectInputs']
    parent=evidence.load(ROOT/'tools/reference/npc-incoming-lethal-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][name]==parent['artifacts'][name]
    for stage,broader,compact,count in ((STAGE,False,False,389),(WIDE,True,False,605),(COMPACT,False,True,18)):
        log=evidence.cohort(stage,expected(broader,compact),count)
        assert 'NPC retained target:2actual Iron Maiden deaths,retained/deleted originals reuse source corpse identity' in log
        saved=evidence.load(stage/'npc-retained-target-fixtures.json');assert saved['Generation']==2 and len(saved['Actors'])==2
        assert {row['Retained']for row in saved['Actors']}=={True,False}
        for name in ('Owner','Body','Original','Corpse','Ally','Item'):assert len({r[name]for r in saved['Actors']})==2
        for row in saved['Actors']:
            assert row['Curse']['Source']==row['Body']and row['Curse']['Rank']==5 and row['Life']>0 and row['Mana']==0
            assert row['Graph']['Actor']==row['Body']and row['Graph']['Owner']==row['Owner']
            assert [r['Item']for r in row['Graph']['Items']]==[row['Item']]
        for fixture in ('npc-incoming-proc','npc-incoming-lethal'):assert evidence.load(stage/(fixture+'-fixtures.json'))['Generation']==2
        inventory=evidence.load(stage/'birth-inputs.json');assert inventory['artifacts']==born['artifacts']and inventory['copiedData']==born['copiedData']
        for path,digest in inventory['sourceInputs'].items():assert evidence.sha(ROOT/path)==digest,path
    for kind in ('content','integration'):
        assert all(t in (STAGE/(kind+'-build-1.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    audits=evidence.audits(STAGE,PARENT,born['artifacts']);assert audits=={'named-item-runtime-audit.json':1051,'first-loop-affix-audit.json':21}
    evidence.verification(STAGE);protected=evidence.protection();review(True)
    status=evidence.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json');assert not status['completedClasses']and sum(r['implementation']=='handler-present'for r in status['skills'])==197
    files=[Path(__file__).resolve(),ROOT/'tools/skill_checkpoint_evidence.py',ROOT/'tools/review_npc_retained_target.py',ROOT/'tools/reference/npc-retained-target-preflight.json',ROOT/'tests/check_npc_retained_target.py']+[ROOT/p for p in changed]
    files += [ROOT/'docs'/name for name in ('npc-retained-target-checkpoint.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','class-skill-reuse-notes.md','skill-effects.md','skill-icons.md')]
    files+=evidence.stage_files(STAGE,WIDE,COMPACT)
    inspection=evidence.load(ROOT/'tools/reference/npc-retained-target-preflight.json');assert inspection['retainedTargetBridgeImplemented']and not inspection['modernDispatcherParityEstablished']
    for path,digest in inspection['inputHashes'].items():assert evidence.sha(ROOT/path)==digest,path
    receipt=dict(referenceVersion='3.3.93847',parent=PARENT,installed=False,handlers=197,completedClasses=[],artifacts=born['artifacts'],copiedData=born['copiedData'],
        contentBuild=1,integrationBuild=1,sourceSnapshot=1,focusedRun=1,broaderRun=1,compactRun=1,focusedMarkers=389,broaderMarkers=605,compactMarkers=18,
        bootsPerCohort=3,actualReloadsPerCohort=2,savesPerCohort=3,originalSavedReflectedCasesPerCohort=2,pythonTests=71,auditFingerprints=audits,protection=protected,
        scope='Synchronous original monster frame captured before retaliation;actual Iron Maiden source deaths with retained/deleted originals reuse existing SourceCorpseIdentity and live corpse position through item/common-executor/do30;one defender RNG/pre-HP curse,one HP/free;original graphs/resources/item/dead identities/target curses through2reloads.',
        limits=['Qualified post-defense receiver fixtures do not infer automatic UO scalar damage/AI/mitigation admission.',
            'Pinned older dependency inspection is separate from native acceptance;modern dispatcher parity and exact room/path traversal remain unestablished.',
            'Removed identity/facet mismatch is refused before incoming proc RNG/receiving HP,without rollback of earlier outgoing/retaliation effects;no fabricated fallback.',
            'Player attackers,ground/no-target services,reentrant recipient removal,other proc spells and complete NPC equipment/Valkyrie/Shadow actors remain unfinished.',
            'No balance data/native/client/protocol/persistence-layout change,new learned handler,completed class or deployment;effects/icons remain separate identified placeholders.'])
    evidence.write('npc-retained-target-native-evidence.json',receipt,files,check)
    print('NPC retained target:389focused/605broader/18compact markers;2actual reflected deaths/original graph/item/corpse cases through2reloads;71Python/audits/protection pass.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
