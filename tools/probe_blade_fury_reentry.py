#!/usr/bin/env python3
"""Compose complete fresh Fury mode entry over a released timed state.

Only inactive-bit and ready/equal-deadline contexts execute. Other fresh
rewinds can index before the supplementary sequence array and stay unsupported.
Accepted packet cadence and modern executable equivalence are not inferred.
"""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    parent=json.loads((ROOT/'tools/reference/blade-fury-sequence-source-evidence.json').read_text())
    source=BASE/'blade-fury-sequence-source-probe.cpp';cpp=source.read_text().split('int main()',1)[0]
    for name,digest in parent['sourceHashes'].items():assert sha(BASE/'d2moo-pinned-source'/name)==digest
    # These exact complete routines come from the existing verified composition.
    from review_weapon_event_order import function
    for name,digest in parent['compiledRoutineHashes'].items():
        assert hashlib.sha256(function(cpp,name).encode()).hexdigest()==digest,name
    cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;
gpSkillSrvStartFnTable_6FD408B0[26]=SKILLS_SrvSt26_BladeFury;gpSkillSrvDoFnTable_6FD40A20[48]=SKILLS_SrvDo048_BladeFury;
printf("[");bool first=true;
for(int level:{1,3,20})for(int amount:{0,255,256,1535,1536,4096})for(int lease:{0,1})for(int deadline:{999,1000})for(int id:{0,1})for(int target:{0,1})for(int make:{0,1}){
owner={};owner.pStatListEx=&ex;owner.dwFlags=192;owner.dwAnimMode=1;owner.pAnimSeq=gPlayerSequenceBladeFury;
ex={};state={};row={};skill={};events.clear();used=true;selected=nullptr;rank=level;mana=amount;game.dwGameFrame=1000;actionOrdinal=0;
record=learned=allocation=true;aim=target;factory=make;calc=5;emits=made=paid=rewinds=allocs=0;order.clear();trace.clear();
row.dwFlags[1]=gdwBitMasks[SKILLSFLAGINDEX2_USEMANAONDO];skill.param=deadline;state.until=1005;
if(lease)ex.pMyStats=&state;ex.StatFlags[0]=1u<<15;
events.push_back({EVENTTYPE_REMOVESTATE,1005,0,0});events.push_back({EVENTTYPE_MODECHANGE,1001,4,8});events.push_back({EVENTTYPE_ENDANIM,1002,0,0});
targetUnit={};targetUnit.dwUnitType=1;targetUnit.dwAnimMode=1;targetUnit.dwFlags=UNITFLAG_TARGETABLE;
if(id)PLRMODE_StartID_AttackCastThrowKickSpecialSequence(&game,&owner,18,&targetUnit);else PLRMODE_StartXY_AttackCastThrowKickSpecialSequence(&game,&owner,18,110,120);
printf("%s[%d,%d,%d,%d,%d,%d,%d,[",first?"":",",level,amount,lease,deadline,id,target,make);first=false;bool sep=false;
for(int frame=1000;frame<=1023;frame++){
game.dwGameFrame=frame;
while(true){int index=-1,best=2147483647;for(int i=0;i<(int)events.size();i++)if(events[i].frame<=frame&&events[i].frame<best){index=i;best=events[i].frame;}if(index<0)break;auto event=events[index];events.erase(events.begin()+index);
if(event.type==EVENTTYPE_REMOVESTATE){if(STATLIST_GetStatListFromUnitAndState(&owner,STATE_INFERNO)&&state.until<=frame){ex.pMyLastList=ex.pMyStats=nullptr;SKILLS_StatRemoveCallback_RemoveState(&owner,STATE_INFERNO,&state);}}
else if(event.type==EVENTTYPE_ENDANIM)sub_6FC81250(&game,&owner,0,0);
else if(event.type==EVENTTYPE_MODECHANGE){int result=sub_6FC80EE0(&game,&owner,event.kind,event.param);if(result==2)sub_6FC81250(&game,&owner,event.kind,event.param);}
}
printf("%s",sep?",":"");PrintState();sep=true;
}printf("]]");}printf("]\n");}'''
    p=BASE/'blade-fury-reentry-source-probe.cpp';p.write_text(cpp)
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(p.with_suffix(''))],check=True)
    run=subprocess.run([str(p.with_suffix(''))],capture_output=True);(BASE/'blade-fury-reentry-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    rows=json.loads(run.stdout);assert len(rows)==576 and all(len(r[7])==24 for r in rows)
    fixture=BASE/'blade-fury-reentry-source-probe.json';fixture.write_bytes(run.stdout)
    data=dict(referenceVersion='3.3.93847',legacyCommit=parent['legacyCommit'],sourceHashes=parent['sourceHashes'],compiledRoutineHashes=parent['compiledRoutineHashes'],
        contexts=576,frames=13824,baseCompositionSha256=sha(source),probeSha256=sha(p),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture)),
        established=['Full XY/ID mode entry clears flag64, rebuilds per-frame sequence events and retains independent timed-state callbacks. Released inactive state and ready/equal shot deadlines execute without invented pre-sequence padding.',
            'A ready retained list can emit/pay immediately without the fresh six-mana threshold or state reactivation. Equality renews the list but emits nothing and returns failure/neutral. Original stale expiry callbacks consult the new deadline.',
            'The four predecessor probes were reconstructed and checked before this composition. ASan/UBSan exit0 with empty runtime stderr is required.'],
        limits=['Future-deadline or active-bit fresh entry can invoke a rewind at a newly reset epoch. The old array getter has no negative-index guard; these contexts are excluded rather than padded. Modern behavior and the accepting outer/client gate remain unresolved.',
            'Per-owner FIFO event storage, normal SQ23 init/rate, target/path/room/allocation services and ordinary-player context retain the predecessor composition boundaries. Modern packet cadence is not inferred. Rank1 legacy zero debit remains a separate comparison control.'])
    out=ROOT/'tools/reference/blade-fury-reentry-source-evidence.json';text=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==text
    else:out.write_text(text)
    print('Fury released-state fresh entry:576complete contexts/13824frames; ready/equal and stale expiry; sanitizers clean; unsupported fresh rewinds remain explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
