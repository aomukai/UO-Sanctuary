#!/usr/bin/env python3
"""Execute complete player st11/do19 and ordinary resource wrappers."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'player-inferno-channel'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    preflight=json.loads((ROOT/'tools/reference/arctic-blast-preflight.json').read_text())
    for name,increment in [('Arctic Blast','2'),('Inferno','3')]:
        row=preflight['currentRows']['skills'][name]
        assert [row[k] for k in ['Param1','Param2','calc1','mana','lvlmana','manashift']]==['35',increment,'ln12/2','24','1','2']
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, routines = {}, {}
    def extract(relative, name):
        p = BASE / 'd2moo-pinned-source' / relative
        raw = p.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = sha(p)
        text = function(raw.decode(), name)
        routines[name] = hashlib.sha256(text.encode()).hexdigest()
        return text
    abi = STAGE / 'abi-base.cpp'
    base = abi.read_text().split('int main()', 1)[0]
    previous = json.loads((ROOT / 'tools/reference/blade-fury-release-source-evidence.json').read_text())
    for name, digest in previous['compiledRoutineHashes'].items():
        candidates = []
        for relative in previous['sourceHashes']:
            text = (BASE / 'd2moo-pinned-source' / relative).read_text()
            if name + '(' in text:
                try:
                    block = extract(relative, name)
                except (AssertionError, ValueError):
                    continue
                if hashlib.sha256(block.encode()).hexdigest() == digest:
                    candidates.append(block)
        assert len(candidates) == 1 and candidates[0] in base, name
    def replace(old, new):
        nonlocal base
        assert old in base, old
        base = base.replace(old, new, 1)
    def line(prefix, value):
        matches = [s for s in base.splitlines() if s.startswith(prefix)]
        assert len(matches) == 1, prefix
        replace(matches[0], value)
    replace('int rank=3;', 'int skillId=41,lastRange=0;int rank=3;')
    replace('unsigned dwFlags=64,dwSeqFrame=0;', 'int dwClassId=0,nSeqCurrentFramePrecise=0;unsigned dwFlags=64,dwSeqFrame=0;')
    replace('int nSkill=0,nMissile=0,', 'int nRange=0,nSkill=0,nMissile=0,')
    replace('int nAuraState=-1,', 'int dwCalc[2]={0,1},nAuraState=-1,')
    base = base.replace('assert(id==266', 'assert(id==skillId').replace('return 266;', 'return skillId;')
    line('D2UnitStrc* MISSILES_CreateMissileFromParams(',
         'D2UnitStrc* MISSILES_CreateMissileFromParams(D2GameStrc*,D2MissileStrc*p){assert(p->dwFlags==0x8020&&p->pOwner==&owner&&p->pOrigin==&owner&&p->nMissile==row.wSrvMissileA&&p->nSkill==skillId&&p->nSkillLevel==rank);lastRange=p->nRange;emits++;order+=\'E\';if(factory){made++;return &created;}return nullptr;}')
    base += '\nconstexpr int EVENTTYPE_ENDANIM=3,EVENTTYPE_MODECHANGE=2;\nstruct D2MonStats2Txt{int nInfernoLen=1;};\nD2MonStats2Txt* MONSTERREGION_GetMonStats2TxtRecord(int){assert(false);return nullptr;}\n'
    sor = 'source/D2Game/src/SKILLS/SkillSor.cpp'
    for name in ['SKILLS_DoInferno', 'SKILLS_StartInferno', 'SKILLS_SrvSt11_Inferno_ArcticBlast', 'SKILLS_SrvDo019_Inferno_ArcticBlast']:
        base += extract(sor, name) + '\n'
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;
gpSkillSrvStartFnTable_6FD408B0[11]=SKILLS_SrvSt11_Inferno_ArcticBlast;
gpSkillSrvDoFnTable_6FD40A20[19]=SKILLS_SrvDo019_Inferno_ArcticBlast;
printf("[");bool first=true;
for(int kind:{0,1})for(int level:{1,3,20,28,255})for(int amount:{0,95,96,1535,1536,4096})
for(int bits=0;bits<4;bits++)for(int param:{0,1})for(int alloc:{0,1})for(int target:{0,1})for(int make:{0,1})for(int action=0;action<5;action++){
owner={};owner.pStatListEx=&ex;owner.dwFlags=64;ex={};state={};row={};skill={};rank=level;skillId=kind?230:41;game.dwGameFrame=1000;
row.wSrvStartFunc=11;row.wSrvDoFunc=19;row.wSrvMissileA=kind?459:60;row.wMana=24;row.wLevelMana=1;row.nManaShift=2;row.dwFlags[1]=0;
mana=amount;calc=std::max(1,(35+(kind?2:3)*(level-1))/2);record=learned=true;allocation=alloc;aim=target;factory=make;
emits=made=paid=payCalls=rewinds=allocs=lastRange=0;order.clear();state.until=bits&2?1010:0;ex.pMyLastList=bits&2?&state:nullptr;
STATES_ToggleState(&owner,STATE_INFERNO,bits&1);skill.param=param;int start=-1,result=-1;
if(action==0)start=SKILLS_SrvSt11_Inferno_ArcticBlast(&game,&owner,skillId,rank);
if(action==1)result=SKILLS_SrvDo019_Inferno_ArcticBlast(&game,&owner,skillId,rank);
if(action==2||action==4)start=sub_6FD12950(&game,&owner,skillId,rank,0);
if(action==3||(action==4&&start))result=D2GAME_SKILLS_Handler_6FD12BA0(&game,&owner,skillId,rank,1,0,0);
bool lease=STATLIST_GetStatListFromUnitAndState(&owner,STATE_INFERNO)!=nullptr;
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",kind,level,amount,bits,param,alloc,target,make,action,
start,result,STATES_CheckState(&owner,STATE_INFERNO)!=0,lease,lease?state.until:0,skill.param,owner.dwFlags,mana,emits,made,paid,rewinds,allocs,lastRange);first=false;
}printf("]\n");}
'''
    generated = STAGE / 'player-inferno-source-probe.cpp'
    generated.write_text(base)
    binary = generated.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(generated), '-o', str(binary)], check=True)
    run = subprocess.run([str(binary)], capture_output=True)
    sanitizer = STAGE / 'source-sanitizer.log'
    sanitizer.write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-2000:]
    cases = json.loads(run.stdout)
    assert len(cases) == 19200, len(cases)
    output = BASE / 'player-inferno-channel-source-probe.json'
    output.write_bytes(run.stdout)
    paths = [Path(__file__).resolve(), abi, generated, output, sanitizer, ROOT / 'tools/reference/arctic-blast-preflight.json']
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], contexts=len(cases), sanitizerClean=True,
        currentExecutableParity=False, nativeAccepted=False, compiledRoutineHashes=routines, sourceHashes=hashes,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in paths},
        established=['Complete st11/StartInferno/do19/DoInferno compose with the existing compiled generic start/do/mana and state/list helpers. Both player skill identities, threshold/state/list separation, first emission suppression, reentry renewals and factory failures execute.',
            'Arctic lifetime uses35+2per extra rank,whereas Inferno uses35+3per extra rank,both divided by2and clamped to1. Their shared handlers do not imply matching range formulas.',
            'The legacy consumer still writes zero debit at rank1. Current table per-invocation costs are separate controls; this source fixture does not measure modern payment frequency or infer four displayed mana as an exact fractional drain.'],
        declaredServices=['Player-only used skill, constant target coordinates, rewind, stat allocation/posting and missile allocation. Monster branch, Blood Mana, charges, world room/LOS and real scheduling are excluded or asserted.'],
        remaining=['Sequence event composition, modern held-input cadence/release, current resource drain and cold/flight/world delivery remain separate acceptance.'])
    path = ROOT / 'tools/reference/player-inferno-channel-source-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Player Inferno/Arctic:19200 complete start/do/resource contexts execute; native/current channel cadence remains separate.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--check', action='store_true')
    build(p.parse_args().check)
