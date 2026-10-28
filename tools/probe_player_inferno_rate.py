#!/usr/bin/env python3
"""Execute ordinary SQ6/SQ18 rate routing with current transition flags."""
import argparse,hashlib,json,re,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'arctic-modes'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());hashes={};routines={}
    def body(relative,name):
        p=BASE/'d2moo-pinned-source'/relative;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=sha(p);b=function(raw.decode(),name);routines[name]=hashlib.sha256(b.encode()).hexdigest();return b
    cpp=(STAGE/'rate-abi-base.cpp').read_text().split('int main()',1)[0]
    parent=json.loads((ROOT/'tools/reference/blade-fury-rate-source-evidence.json').read_text());assert tree['sha']==parent['legacyCommit']
    for name,digest in parent['compiledRoutineHashes'].items():
        blocks=[]
        for rel in parent['sourceHashes']:
            try:b=body(rel,name)
            except (AssertionError,ValueError):continue
            if hashlib.sha256(b.encode()).hexdigest()==digest:blocks.append(b)
        assert len(blocks)==1 and blocks[0] in cpp,name
    def replace(a,b):
        nonlocal cpp
        assert cpp.count(a)==1,a;cpp=cpp.replace(a,b,1)
    replace('D2AnimSeqTxt gPlayerSequenceBladeFury[19]', 'extern D2AnimSeqTxt gPlayerSequenceInferno[15],gPlayerSequenceArcticBlast[15];\nD2AnimSeqTxt gPlayerSequenceBladeFury[19]')
    replace('constexpr int PLRMODE_CAST=10,','constexpr int PLRMODE_SPECIAL1=11,PLRMODE_CAST=10,')
    seqpath=BASE/'d2moo-pinned-source/source/D2Common/src/DataTbls/SequenceTbls.cpp';seq=seqpath.read_text()
    for name in ['gPlayerSequenceInferno','gPlayerSequenceArcticBlast']:
        m=re.search(r'D2AnimSeqTxt '+name+r'\[15\]\s*=\s*\{',seq);assert m,name
        end=seq.index('};',m.start())+2;cpp+=seq[m.start():end]+'\n'
    replace('struct D2SkillsTxt{int nSkillId=266,nSeqTrans=7;', 'int skillId=41,classId=1,fcr=0,fcrQueries=0;struct D2SkillsTxt{int nSeqInput=10,nSkillId=41,nSeqTrans=18;')
    replace('assert(id==266);','assert(id==skillId);')
    assert cpp.count('assert(type==0&&id==6&&mode==18);')==2
    cpp=cpp.replace('assert(type==0&&id==6&&mode==18);','assert(type==0&&id==classId&&mode==18);')
    replace('return gPlayerSequenceBladeFury;','return skillId==41?gPlayerSequenceInferno:gPlayerSequenceArcticBlast;')
    replace('return 19*256;','return 15*256;');replace('return 19;','return 15;')
    replace('int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int,int){assert(false);return 0;}', 'int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int,int){fcrQueries++;return fcr;}')
    # Complete permission wrapper: seqinput is a nonzero gate here,not an
    # elapsed-frame threshold. Full packet/First-mode acceptance stays separate.
    cpp+='\nconstexpr int PLRMODE_DEATH=0,PLRMODE_NEUTRAL=1,PLRMODE_TOWNNEUTRAL=5,PLRMODE_DEAD=17,PLRMODE_THROW=12,PLRMODE_KICK=13,PCLASS_AMAZON=0,PCLASS_DRUID=5;\n'
    cpp+='bool cursor=false;void* INVENTORY_GetCursorItem(void*){return cursor?&owner:nullptr;}\nint EVENT_GetEventFrame(D2GameStrc*,D2UnitStrc*,int){return 1000;}\nD2SkillsTxt* SKILLS_GetSkillsTxtRecordFromSkill(D2SkillStrc*){return &row;}\n'
    cpp+=body('source/D2Game/src/PLAYER/PlrModes.cpp','sub_6FC81890')+'\n'
    cpp+=r'''int main(){bool first=true;printf("[");
for(int kind:{0,1})for(int base:{128,256,512,1024})for(int modifier:{0,15,50,100,175,1000})for(int castRate:{0,75,200}){
 skillId=kind?230:41;classId=kind?5:1;owner={};owner.dwClassId=classId;row={};row.nSkillId=skillId;skill={};anim={};anim.dwAnimSpeed=base;other=modifier;queries=fcrQueries=0;fcr=castRate;
 UNITS_InitializeSequence(&owner);UNITS_UpdateAnimRateAndVelocity(&owner,__FILE__,__LINE__);events.clear();sub_6FCBCE70(&game,&owner);int action=0,end=0;
 for(auto e:events){if(e.type==1)action=e.frame-1000;if(e.type==2)end=e.frame-1000;}
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",kind,base,modifier,castRate,owner.dwSeqSpeed,owner.wAnimSpeed,owner.dwSeqMode,owner.dwSeqFrameCount,action,end,queries,fcrQueries);first=false;
}printf("]\n");}'''
    p=STAGE/'player-inferno-rate.cpp';p.write_text(cpp);compiler=STAGE/'rate-compiler.log';sanitizer=STAGE/'rate-sanitizer.log'
    with compiler.open('w') as out:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(p.with_suffix(''))],stdout=out,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(p.with_suffix(''))],capture_output=True);sanitizer.write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()[-2000:]
    cases=json.loads(run.stdout);assert len(cases)==144
    for kind,base,modifier,fcr,speed,anim,mode,count,action,end,queries,fcrQueries in cases:
        assert [speed,anim,mode,count,action,end,queries,fcrQueries]==[256,base*max(15,min(175,modifier))//100,11 if kind else 10,3840,10,15,1,0]
    pin=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';rows=json.loads(pin.read_text());manifest=pin.parent.parent/'sources.json';assert sha(pin)==json.loads(manifest.read_text())['files']['json/skills.json']['sha256']
    for ident in ['41','230']:assert rows[ident]['seqtrans']=='SQ' and not rows[ident].get('UseAttackRate') and not rows[ident].get('useAttackRate') and rows[ident]['seqinput']==10
    out=BASE/'player-inferno-rate-source-probe.json';out.write_bytes(run.stdout)
    paths=[Path(__file__).resolve(),STAGE/'rate-abi-base.cpp',p,compiler,sanitizer,out,pin,manifest]
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],currentExecutableParity=False,learnedHandlerEnabled=False,contexts=144,sourceHashes=hashes,compiledRoutineHashes=routines,
        artifacts={str(x.relative_to(ROOT)):sha(x) for x in paths},sanitizers='ASan/UBSan clean',
        established=['Complete ordinary SQ initialization,rate router/selected predicates/other writer and source scheduler retain sequence256/action10/end15for both SQ6andSQ18 across ordinary animation metadata/other-rate and0/75/200FCR controls. FCR item-stat service is never queried. Current seqtransSQ/useAttackRate0 select that route; other-rate changes pose speed,not sequence clock.',
            'The full sub_6FC81890permission body is hash-bound but not executed by this rate matrix; its nonzero seqinput branch is not a threshold on elapsed sequence frames. Full packet/First-mode accepted reentry remains outside scope.'],
        remaining=['Identity ConvertMode,ordinary player animation lookup and metadata are declared services. Morphs,full modern executable rate/input/payment cadence and native controller need separate acceptance. Nonselected rate writers reject execution.'])
    target=ROOT/'tools/reference/player-inferno-rate-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==text
    else:target.write_text(text)
    print('144ordinary SQ6/SQ18rate controls retain sequence256/action10/end15; FCR queries0; modern executable/input cadence remains separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
