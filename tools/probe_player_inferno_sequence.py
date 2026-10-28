#!/usr/bin/env python3
"""Compose complete old player mode/action/end with st11/do19 and FIFO events."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'player-inferno-sequence'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    preflight = json.loads((ROOT / 'tools/reference/arctic-blast-preflight.json').read_text())
    assert tree['sha'] == preflight['legacyCommit'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}
    def source(relative):
        p = BASE / 'd2moo-pinned-source' / relative
        raw = p.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = sha(p)
        return raw.decode()
    def extract(relative, name):
        block = function(source(relative), name)
        routines[name] = hashlib.sha256(block.encode()).hexdigest()
        return block
    abi = STAGE / 'abi-base.cpp'
    base = abi.read_text().split('int main()', 1)[0]
    previous = json.loads((ROOT / 'tools/reference/blade-fury-sequence-source-evidence.json').read_text())
    for name, digest in previous['compiledRoutineHashes'].items():
        matches = []
        for relative in previous['sourceHashes']:
            if name + '(' not in source(relative):
                continue
            try:
                block = extract(relative, name)
            except (AssertionError, ValueError):
                continue
            if hashlib.sha256(block.encode()).hexdigest() == digest:
                matches.append(block)
        assert len(matches) == 1 and matches[0] in base, name
    def replace(old, new):
        nonlocal base
        assert old in base, old
        base = base.replace(old, new, 1)
    def line(prefix, value):
        matches = [s for s in base.splitlines() if s.startswith(prefix)]
        assert len(matches) == 1, prefix
        replace(matches[0], value)
    replace('int rank=3;', 'int skillId=41;int rank=3;')
    replace('int nSkill=0,nMissile=0,', 'int nRange=0,nSkill=0,nMissile=0,')
    replace('int nAuraState=-1,', 'int dwCalc[2]={0,1},nAuraState=-1,')
    base = base.replace('assert(id==266', 'assert(id==skillId').replace('return 266;', 'return skillId;')
    base = base.replace('19*256', '15*256').replace('dwFrames=19;', 'dwFrames=15;')
    line('D2UnitStrc* MISSILES_CreateMissileFromParams(',
         'D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc*p){assert(p->dwFlags==0x8020&&p->pOwner==&owner&&p->pOrigin==&owner&&p->nMissile==row.wSrvMissileA&&p->nSkill==skillId&&p->nSkillLevel==rank);emits++;if(factory){made++;return &created;}return nullptr;}')
    seq = source('source/D2Common/src/DataTbls/SequenceTbls.cpp')
    arrays = 'constexpr int PLRMODE_SPECIAL1=13;\n'
    for name in ['gPlayerSequenceInferno', 'gPlayerSequenceArcticBlast']:
        block = re.search(r'D2AnimSeqTxt ' + name + r'\[15\] =\s*\{.*?\n\};', seq, re.S)[0]
        assert hashlib.sha256(block.encode()).hexdigest() == preflight['legacySequences'][name]['sha256']
        arrays += block + '\n'
    replace('D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*);', arrays + 'D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*);')
    line('D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc* p)',
         'D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*p){return p->dwAnimMode==18?(skillId==230?gPlayerSequenceArcticBlast:gPlayerSequenceInferno):nullptr;}')
    base += '\nstruct D2MonStats2Txt{int nInfernoLen=1;};\nD2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){assert(false);return nullptr;}\n'
    for name in ['SKILLS_DoInferno', 'SKILLS_StartInferno', 'SKILLS_SrvSt11_Inferno_ArcticBlast', 'SKILLS_SrvDo019_Inferno_ArcticBlast']:
        base += extract('source/D2Game/src/SKILLS/SkillSor.cpp', name) + '\n'
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;
gpSkillSrvStartFnTable_6FD408B0[11]=SKILLS_SrvSt11_Inferno_ArcticBlast;
gpSkillSrvDoFnTable_6FD40A20[19]=SKILLS_SrvDo019_Inferno_ArcticBlast;
printf("[");bool first=true;
for(int kind:{0,1})for(int level:{1,3,20})for(int amount:{0,1535,1536,4096})for(int id:{0,1})
for(int stop:{-1,1001,1010,1020})for(int target:{0,1})for(int make:{0,1})for(int schedule=0;schedule<3;schedule++)for(int cancel:{-1,1011}){
owner={};owner.pStatListEx=&ex;owner.dwFlags=192;ex={};state={};row={};skill={};events.clear();used=true;selected=nullptr;
rank=level;skillId=kind?230:41;owner.dwClassId=kind?5:1;mana=amount;game.dwGameFrame=1000;actionOrdinal=0;
record=learned=allocation=true;aim=target;factory=make;calc=(35+(kind?2:3)*(level-1))/2;emits=made=paid=rewinds=allocs=0;order.clear();trace.clear();
row.wSrvStartFunc=11;row.wSrvDoFunc=19;row.wSrvMissileA=kind?459:60;row.wMana=24;row.wLevelMana=1;row.nManaShift=2;row.dwFlags[1]=0;
targetUnit={};targetUnit.dwUnitType=1;targetUnit.dwAnimMode=1;targetUnit.dwFlags=UNITFLAG_TARGETABLE;
if(id)PLRMODE_StartID_AttackCastThrowKickSpecialSequence(&game,&owner,18,&targetUnit);
else PLRMODE_StartXY_AttackCastThrowKickSpecialSequence(&game,&owner,18,110,120);
printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,[",first?"":",",kind,level,amount,id,stop,target,make,schedule,cancel);first=false;bool sep=false;
for(int frame=1000;frame<=1040;frame++){
game.dwGameFrame=frame;
if(frame==stop)D2GAME_PACKETCALLBACK_Rcv0x12_6FC84670(&game,&owner,nullptr,1);
bool callback=schedule==1?frame==1006:schedule==2&&frame>=1006&&(frame-1006)%2==0;
if(callback&&used)sub_6FD12FD0(&game,&owner);
if(frame==cancel&&used)sub_6FC81250(&game,&owner,0,0);
while(true){int index=-1,best=2147483647;for(int i=0;i<(int)events.size();i++)if(events[i].frame<=frame&&events[i].frame<best){index=i;best=events[i].frame;}
if(index<0)break;auto event=events[index];events.erase(events.begin()+index);
if(event.type==EVENTTYPE_REMOVESTATE){if(STATLIST_GetStatListFromUnitAndState(&owner,STATE_INFERNO)&&state.until<=frame){ex.pMyLastList=ex.pMyStats=nullptr;SKILLS_StatRemoveCallback_RemoveState(&owner,STATE_INFERNO,&state);}}
else if(event.type==EVENTTYPE_ENDANIM)sub_6FC81250(&game,&owner,0,0);
else if(event.type==EVENTTYPE_MODECHANGE){int result=sub_6FC80EE0(&game,&owner,event.kind,event.param);if(result==2)sub_6FC81250(&game,&owner,event.kind,event.param);}
}printf("%s",sep?",":"");PrintState();sep=true;
}printf("]]");}printf("]\n");}
'''
    generated = STAGE / 'player-inferno-sequence-source-probe.cpp'
    generated.write_text(base)
    binary = generated.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    sanitizer = STAGE / 'source-sanitizer.log'
    sanitizer.write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-2500:]
    cases = json.loads(run.stdout)
    assert len(cases) == 4608 and all(len(c[9]) == 41 for c in cases)
    output = BASE / 'player-inferno-sequence-source-probe.json'
    output.write_bytes(run.stdout)
    paths = [Path(__file__).resolve(), abi, generated, output, sanitizer, ROOT / 'tools/reference/arctic-blast-preflight.json']
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], contexts=4608, frames=188928,
        sanitizerClean=True, currentExecutableParity=False, nativeAccepted=False, compiledRoutineHashes=routines, sourceHashes=hashes,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=['Both complete15-point arrays execute with full ordinary XY/ID mode entry,initial/rewind scheduling,start/do wrappers,st11/do19,state/list/stop helpers and action/end callbacks. Source skill identities and Inferno3/Arctic2range increments stay distinct.',
            'Explicit no-reentry,single-reentry and repeated two-frame accepted-callback controls preserve per-owner FIFO insertion order,stale/duplicate removal callbacks and same-frame stop-before-start-before-events ordering. These controls do not establish when modern input accepts callbacks.'],
        declaredServices=['Per-owner FIFO queue; normal256sequence initializer/rate,used-skill/path/target/room/LOS,stat ABI and missile allocation are explicit services. Full game queue/cross-owner order and current client/FCR/rate remain outside this probe.'],
        remaining=['Native sequence bridge,JSON continuation and current-table rank1policy require separate comparison. Very early same-frame double-rewind negative-index contexts are excluded; do not invent padded pre-sequence events. No production channel or cold flight acceptance.'])
    path = ROOT / 'tools/reference/player-inferno-sequence-source-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Player Inferno/Arctic:4608complete mode/channel compositions across188928FIFO event frames; ASan/UBSan clean.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check', action='store_true')
    build(p.parse_args().check)
