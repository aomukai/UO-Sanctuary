#!/usr/bin/env python3
"""Pin complete reviewed Confuse/Attract bodies; no source execution claim."""
import argparse,hashlib,json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def review(check=False):
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    blobs={r['path']:r['sha'] for r in tree['tree'] if r['type']=='blob'};inputs={};bodies={}
    def routine(rel,name):
        p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blobs[rel]
        inputs[str(p.relative_to(ROOT))]=sha(p);text=raw.decode();m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
        i=text.index('{',m.start())+1;depth=1
        while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
        body=text[m.start():i];bodies[name]=body;return body
    nec='source/D2Game/src/SKILLS/SkillNec.cpp'
    cast=routine(nec,'SKILLS_SrvDo061_Confuse');receiver=routine(nec,'sub_6FD0C060');remove=routine(nec,'sub_6FD0C2B0')
    attract=routine(nec,'SKILLS_SrvDo059_Attract');neighbors=routine(nec,'sub_6FD0BDA0')
    target=routine('source/D2Game/src/AI/AiUtil.cpp','sub_6FCF2920')
    mindblast=routine('source/D2Game/src/SKILLS/SkillAss.cpp','SKILLS_AuraCallback_MindBlast')
    mindblast_remove=routine('source/D2Game/src/SKILLS/SkillAss.cpp','SKILLS_StatRemoveCallback_MindBlast')
    assert 'UNIT_ALIGNMENT_EVIL' in mindblast and 'STATE_CONVERSION' in mindblast and 'sub_6FCBDD30(pUnit, 2u, 1)' in mindblast
    aligned=routine('source/D2Common/src/D2StatList.cpp','STATLIST_AreUnitsAligned')
    do30=routine(nec,'sub_6FD0B450')
    assert 'dwAiCurseDiv' in cast and 'sub_6FD0C060' in cast
    assert receiver.index('sub_6FD10EC0(&curse)')<receiver.index('sub_6FCBDD30(pUnit, 1u, 1)')<receiver.index('sub_6FC61E30(pUnit, 3, 0)')<receiver.index('EVENT_SetEvent')
    assert 'pParam->nDuration + pParam->pGame->dwGameFrame' in receiver and '!= UNIT_ALIGNMENT_EVIL' in receiver
    assert 'sub_6FCBDD30(pUnit, 0, 1)' in remove and 'sub_6FC61F00' not in remove
    assert 'STATLIST_GetUnitAlignment(pUnit) == UNIT_ALIGNMENT_NEUTRAL' in do30
    assert 'case 3:' in target and 'ITEMS_RollRandomNumber(&pUnit->pSeed) & 1' in target and 'sub_6FCBDD30(pUnit, nAlignment, 1)' in target
    assert 'UNIT_ALIGNMENT_NEUTRAL' not in aligned
    assert attract.index('sub_6FCBDD30(pTargetUnit, 1u, 1)')<attract.index('sub_6FD10360')<attract.index('sub_6FD10EC0(&curse)')
    assert 'sub_6FC61E30(pUnit, nParam, pParam->nUnitGUID)' in neighbors
    data=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';manifest=json.loads((data.parent.parent/'sources.json').read_text());assert sha(data)==manifest['files']['json/skills.json']['sha256'];inputs[str(data.relative_to(ROOT))]=sha(data)
    skills=json.loads(data.read_text());conf=skills['81'];attr=skills['86']
    assert [conf[k] for k in ['srvdofunc','aurafilter','mana','Param1','Param2','Param3','Param4']]==[61,16514,13,6,1,250,50]
    assert [attr[k] for k in ['srvdofunc','aurafilter','mana','Param1','Param2','Param3','Param4']]==[59,2,17,9,0,300,90]
    inputs[str(Path(__file__).relative_to(ROOT))]=sha(Path(__file__))
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],scope='Complete static body review;none of these routines are executed by this producer.',
        inputs=inputs,reviewedNotExecuted={k:hashlib.sha256(v.encode()).hexdigest() for k,v in bodies.items()},
        established=['Confuse callback checksEvil alignment;onlyaccepted factory result setsNeutral,node9,override3 and independentAIRESET usingpre-resistance duration. Removingvisiblecurse restoresalignment/node but doesnotclearoverride.',
        'Neutral alignswithnobodyexceptself. Override3temporarily selectsopposing alignment withonerawlowbit,scans,restoresalignment andclearsoverride onemptyresult. Nativeworldscan/cadence/RNGownershipremainadapters.',
        'Do30AI castsrejectNeutral. Earliernativefixtures permittingDimVision/Terror toreplaceactiveConfuse lackeditsruntimeNeutralstate;this correction is notanapprovedSanctuarydeviation.',
        'Mind Blast usesSTATE_CONVERSION andGoodalignment2 afteranEvil-alignment gate;Confuse usesNeutralalignment1. Sharedeligibilitymustnotconvert anactiveNeutralConfuse recipient.',
        'Attract mutatesselectedNeutral/node/minionlinks,setsneighbor targetoverrides withindependentreset,thenattemptscursefactory;this reviewdoesnotimplementAttract orcertifyitscurrentexecutablebehavior.'],
        remaining=['Currentexecutablecorroboration,fullcallback/VM/factory/node/room/threat/target/event/AIexecution and nativealignment/credit/persistenceintegration mustbeverifiedseparately. No sourceexecution or completeclassclaim.'])
    p=ROOT/'tools/reference/confuse-source-review.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==text
    else:p.write_text(text)
    print('Confuse/Attract/Mind Blast:10complete bodies statically reviewed and current table rows pinned;no source execution claim.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
