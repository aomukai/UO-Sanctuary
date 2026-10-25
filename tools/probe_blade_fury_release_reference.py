#!/usr/bin/env python3
"""Separate the Inferno bit from its lease using complete pinned routines.

The legacy stop packet and state/list helpers compose with the unchanged
ordinary resource wrappers. This does not establish when the modern client
sends stop/reentry packets, or production mode/event ordering.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import subprocess

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills'


def build(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text())
    hashes,routines={},{}
    def source(rel):
        raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        hashes[rel]=hashlib.sha256(raw).hexdigest()
        return raw.decode()
    def extract(rel,name):
        text=source(rel)
        m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
        end=text.index('{',m.start())+1;depth=1
        while depth:
            depth+=(text[end]=='{')-(text[end]=='}');end+=1
        body=text[m.start():end]
        routines[name]=hashlib.sha256(body.encode()).hexdigest()
        return body+'\n'
    base=(STAGE/'blade-fury-resource-source-probe.cpp').read_text().split('int main()',1)[0]
    old=json.loads((ROOT/'tools/reference/blade-fury-resource-source-evidence.json').read_text())
    for name,h in old['compiledRoutineHashes'].items():
        rel='source/D2Game/src/SKILLS/'+('SkillAss.cpp' if name in {'SKILLS_GetProgressiveSkillMissileId','SKILLS_EvaluateProgressiveSkillCalc','SKILLS_SrvDo048_BladeFury','SKILLS_SrvSt26_BladeFury','SKILLS_StatRemoveCallback_RemoveState'} else 'Skills.cpp')
        assert extract(rel,name).strip() in base and routines[name]==h,name
    def replace(a,b):
        nonlocal base
        assert a in base,a
        base=base.replace(a,b,1)
    replace('#define __fastcall','#define __stdcall\n#define __fastcall')
    replace('struct D2UnitStrc{','struct D2StatListExStrc;\nstruct D2UnitStrc{')
    replace('void* pDynamicPath=nullptr;}owner,created;',
            'void* pDynamicPath=nullptr;unsigned dwFlagEx=0;D2StatListExStrc* pStatListEx=nullptr;}owner,created;')
    replace('struct D2StatListStrc{int until=0;}state;',
            'struct D2StatListStrc{int until=0,dwStateNo=1;D2StatListStrc* pPrevLink=nullptr;}state;\n'
            'struct D2StatListExStrc{uint32_t StatFlags[32]={};D2StatListStrc* pMyLastList=nullptr;D2StatListStrc* pMyStats=nullptr;}ex;\n'
            'using BOOL=int;constexpr int FALSE=0,TRUE=1,UNIT_MISSILE=3,STATEMASK_DISGUISE=3,UNITFLAGEX_ISSHAPESHIFTED=1;\n'
            'struct D2StatesTxt{unsigned dwStateFlags=0;}stateRow;\n'
            'bool STATLIST_IsExtended(D2StatListExStrc* p){return p==&ex;}\n'
            'D2StatesTxt* DATATBLS_GetStatesTxtRecord(int id){assert(id==STATE_INFERNO);return &stateRow;}\n'
            'bool STATES_CheckStateMaskOnUnit(D2UnitStrc*,int){assert(false);return false;}\n'
            'void UNITROOM_RefreshUnit(D2UnitStrc*){}\n')
    replace('D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int s){assert(s==STATE_INFERNO);return active?&state:nullptr;}',
            'D2StatListStrc* STATLIST_GetStatListFromUnitAndState(D2UnitStrc*,int);')
    replace('bool STATES_CheckState(D2UnitStrc*,int s){if(s==2){return false;}assert(s==STATE_INFERNO);return active;}',
            'BOOL STATES_CheckState(D2UnitStrc*,int);')
    replace('void STATES_ToggleState(D2UnitStrc*,int s,int on){assert(s==STATE_INFERNO);active=on;}',
            'void STATES_ToggleState(D2UnitStrc*,int,int);')
    # Append the unmodified bit helper, state toggler/checker and both list
    # walkers. Extended-list identity and room refresh are declared ABI services.
    bit='source/Fog/include/D2BitManip.h'
    base+='template<typename T>\n'+extract(bit,'BITMANIP_SetBitsValueForMask')
    stat='source/D2Common/src/D2StatList.cpp';states='source/D2Common/src/D2States.cpp'
    for rel,name in [(stat,'D2Common_STATES_ToggleState_6FDB8900'),(states,'STATES_ToggleState'),
                     (states,'STATES_CheckState'),(stat,'D2Common_GetStateFromStatListEx_6FDB8190'),
                     (stat,'STATLIST_GetStatListFromUnitAndState'),
                     ('source/D2Game/src/PLAYER/PlrMsg.cpp','D2GAME_PACKETCALLBACK_Rcv0x12_6FC84670')]:
        base+=extract(rel,name)
    base+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;
gpSkillSrvStartFnTable_6FD408B0[26]=SKILLS_SrvSt26_BladeFury;gpSkillSrvDoFnTable_6FD40A20[48]=SKILLS_SrvDo048_BladeFury;
printf("[");bool first=true;
for(int level:{1,3,20})for(int before:{0,1,2})for(int next:{999,1000,1001})
for(int amount:{0,255,256,1535,1536})for(int later:{0,255,256,1535,1536})
for(int target:{0,1})for(int make:{0,1})for(int size:{1,2}){
 owner={};ex={};state={};owner.pStatListEx=&ex;row={};skill={};rank=level;skill.param=next;mana=amount;state.until=1020;owner.dwFlags=192;
 D2StatListStrc unrelated;unrelated.dwStateNo=7;unrelated.pPrevLink=&state;
 if(before==1){ex.pMyLastList=&unrelated;}if(before==2){ex.pMyStats=&unrelated;}
 ex.StatFlags[0]=1u<<15;if(before==1){ex.StatFlags[0]|=gdwBitMasks[STATE_INFERNO];}
 record=learned=allocation=true;aim=target;factory=make;calc=5;emits=made=paid=rewinds=allocs=0;order.clear();
 row.dwFlags[1]=gdwBitMasks[SKILLSFLAGINDEX2_USEMANAONDO];
 int released=D2GAME_PACKETCALLBACK_Rcv0x12_6FC84670(&game,&owner,nullptr,size);
 assert(ex.StatFlags[0]&(1u<<15));
 int releaseBit=STATES_CheckState(&owner,STATE_INFERNO)!=0,releaseList=STATLIST_GetStatListFromUnitAndState(&owner,STATE_INFERNO)!=nullptr;
 // Fresh allocation installs a real link in the declared post service below.
 int start=sub_6FD12950(&game,&owner,266,level,0),startMana=mana,startEmit=emits,startPaid=paid;
 mana=later;int result=start?D2GAME_SKILLS_Handler_6FD12BA0(&game,&owner,266,level,1,0,0):-1;
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,\"%s\"]",first?"":",",level,before,next,amount,later,target,make,size,released,releaseBit,releaseList,start,startMana,startEmit,startPaid,result,mana,emits,made,paid,STATES_CheckState(&owner,STATE_INFERNO)!=0,STATLIST_GetStatListFromUnitAndState(&owner,STATE_INFERNO)!=nullptr,state.until,skill.param,rewinds,allocs,owner.dwFlags,order.c_str());first=false;
}printf("]\n");}
'''
    replace('void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*,int one){assert(one==1);}',
            'void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc* p,int one){assert(one==1);ex.pMyLastList=p;}')
    p=STAGE/'blade-fury-release-source-probe.cpp';p.write_text(base);binary=p.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True)
    (STAGE/'blade-fury-release-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    rows=json.loads(run.stdout);assert len(rows)==5400
    assert any(r[1]==1 and r[7]==1 and r[9]==0 and r[10]==1 and r[11]==1 and r[3]<1536 for r in rows)
    fixture=STAGE/'blade-fury-release-source-probe.json';fixture.write_bytes(run.stdout)
    out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],nativeVerifiedByThisSourceProbe=False,
             sourceHashes=hashes,compiledRoutineHashes=routines,sharedAbiEvidence='tools/reference/blade-fury-resource-source-evidence.json',
             fixture=dict(path=str(fixture.relative_to(ROOT)),cases=len(rows),sha256=hashlib.sha256(run.stdout).hexdigest()),
             established=['Complete stop0x12, state-bit helpers and list walkers compose with unchanged st26/do48 and ordinary resource wrappers. Valid one-byte stop clears only the Inferno bit; two-byte packets return3 without changing it. The existing timed list remains independently discoverable.',
                          'A released-but-retained list renews its deadline through st26 without recreating the state bit or imposing the fresh six-mana threshold. Generic per-shot eligibility still applies. Direct do48 may emit after release, but does not rewind without the state bit. Later stat removal is distinct and sets server-do flag64.',
                          'Native bridge must preserve this distinction before input/mode integration. Current-table rank1 debit overrides the separately recorded old consumer zero-payment quirk.'],
             limits=['Extended-list ABI identity, room refresh, stat posting, animation rewind and missile allocation are explicit services; disguise states, charges, Blood Mana and a full production event queue/client are outside this fixture.',
                     'These are older server rules. Exact current client stop/reentry timing, release tail, warmup and native graphical input acceptance remain open. No learned handler is enabled by this source probe.'],
             sanitizers='ASan/UBSan; exit0 and empty runtime stderr')
    path=ROOT/'tools/reference/blade-fury-release-source-evidence.json';text=json.dumps(out,indent=2)+'\n'
    if check:assert path.read_text()==text,'Stale Blade Fury release source evidence'
    else:path.write_text(text)
    print('5400 complete release/state-list/resource compositions verified; current client/input timing remains separate.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
