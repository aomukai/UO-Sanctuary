#!/usr/bin/env python3
"""Complete ordinary SQ23 initialization/rate route with declared metadata."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
import probe_blade_fury_timing_reference as basis
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'

def build(check=False):
    basis.build(check=True)
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());hashes={};routines={}
    def source(rel):
        raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel)
        hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
    def extract(text,name):
        m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
        end=text.index('{',m.start())+1;depth=1
        while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
        body=text[m.start():end];routines[name]=hashlib.sha256(body.encode()).hexdigest();return body+'\n'
    units=source('source/D2Common/src/Units/Units.cpp');seq=source('source/D2Common/src/DataTbls/SequenceTbls.cpp')
    cpp=(STAGE/'blade-fury-timing-source-probe.cpp').read_text().split('int main()',1)[0]
    def replace(old,new):
        nonlocal cpp
        assert cpp.count(old)==1,old;cpp=cpp.replace(old,new)
    cpp='#include <limits>\n#define __forceinline inline\nusing BOOL=int;\nusing D2AnimSeqEvent=int;\n'+cpp
    replace('struct Anim{uint8_t pFrameFlags[32]={};}', 'struct Anim{int dwAnimSpeed=256;uint8_t pFrameFlags[32]={};}')
    replace('int dwUnitType=0,dwClassId=6,dwAnimMode=18,', 'unsigned dwSeqFrame=0,dwSeqMode=0,dwFlags=0;int nActionFrame=0;void* pInventory=nullptr;int dwUnitType=0,dwClassId=6,dwAnimMode=18,')
    cpp+=r'''
constexpr int TRUE=1,FALSE=0,NUMBER_OF_PLRMODES=20,NUMBER_OF_MONMODES=16,UNIT_MONSTER=1,UNIT_TILE=5,UNIT_ITEM=4,MONSTER_FIRST_EXPANSION=500;
constexpr int PLRMODE_BLOCK=9,PLRMODE_GETHIT=4,PLRMODE_KNOCKBACK=19,PLRMODE_SEQUENCE=18;
constexpr int MONMODE_CAST=10,MONMODE_BLOCK=11,MONMODE_GETHIT=6,MONMODE_KNOCKBACK=13,MONMODE_WALK=2,MONMODE_RUN=15;
constexpr int SKILLSFLAG_USEATTACKRATE=1,SKFLAG_PASSIVE=1,SKFLAG_STUN=2,MONSTATSFLAG_NPC=1,STAT_OTHER_ANIMRATE=0,UNITFLAG_SQGFXCHANGE=1;
template<class T>T D2Clamp(T value,T low,T high){return std::clamp(value,low,high);}
struct D2AnimUnkStrc{BOOL bCanUseVelocityModifierByPassiveSkill,bCanUseVelocityModifier,bCanUseSkillAttackRate,bCanUseAttackRate,bAllowAnimSpeed;};
struct D2SkillsTxt{int nSkillId=266,nSeqTrans=7;unsigned dwFlags[2]={};}row;
struct D2SkillStrc{D2SkillsTxt* pSkillsTxt=&row;unsigned dwFlags=0;}skill;
struct D2MonStatsTxt{unsigned dwMonStatsFlags=0;};int other=100,queries=0;
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return &skill;}
D2SkillsTxt* DATATBLS_GetSkillsTxtRecord(int id){assert(id==266);return &row;}
D2MonStatsTxt* DATATBLS_GetMonStatsTxtRecord(int){assert(false);return nullptr;}
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){assert(stat==STAT_OTHER_ANIMRATE);queries++;return other;}
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*,int,int){assert(false);return 0;}
void UNITS_SetAnimData(D2UnitStrc*,int type,int id,int mode){assert(type==0&&id==6&&mode==18);}
Anim* DATATBLS_GetAnimDataRecord(D2UnitStrc*,int id,int mode,int type,void*){assert(type==0&&id==6&&mode==18);return &anim;}
void FOG_Trace(const char*,...){assert(false);}
D2AnimSeqTxt* DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*){return gPlayerSequenceBladeFury;}
int DATATBLS_GetSeqFramePointsCount(D2UnitStrc*){return 19*256;}
int DATATBLS_GetSeqFrameCount(D2UnitStrc*){return 19;}
'''
    for name in ['gaPlayerModesAnimModulators','gaMonModesAnimModulators','gaMonModesAnimModulatorsExpansion']:
        m=re.search(r'^(?:static )?D2AnimUnkStrc '+name+r'\[[^]]+\]\s*=\s*\{',units,re.M);assert m,name
        end=units.index('};',m.start())+2;cpp+=units[m.start():end]+'\n'
    # All nonselected rate writers are explicit rejecting services. The full
    # router and every predicate on the selected ordinary-player route execute.
    for name in ['UNITS_UpdateCastAnimRateAndVelocity','UNITS_UpdateBlockAnimRateAndVelocity',
                 'UNITS_UpdateGetHitAnimRateAndVelocity','UNITS_UpdateKnockbackAnimRateAndVelocity',
                 'UNITS_UpdateRunWalkAnimRateAndVelocity','UNITS_UpdateAttackAnimRateAndVelocity']:
        body=extract(units,name);header=body[:body.index('{')];cpp+=header+'{assert(false);}\n'
    for text,name in [(seq,'DATATBLS_ComputeSequenceAnimation'),(units,'UNITS_InitializeSequence'),
                     (units,'UNITS_IsSeqAnimSpeedModulatedByFCR'),(units,'UNITS_IsAnimModeBlocking'),
                     (units,'UNITS_IsAnimModeGetHit'),(units,'UNITS_IsAnimModeKnockBack'),
                     (units,'UNITS_CanAnimModeUseVelocityModifier'),(units,'UNITS_CanAnimModeUseAttackRate'),
                     (units,'UNITS_AnimModeAllowsAnimSpeed'),(units,'UNITS_SetAnimationSpeed'),
                     (units,'UNITS_UpdateOtherAnimRateAndVelocity'),(units,'UNITS_UpdateAnimRateAndVelocity')]:
        cpp+=extract(text,name)
    cpp+=r'''
int main(){printf("[");bool first=true;
for(int base:{128,256,512,1024})for(int modifier:{0,15,50,100,175,1000}){
 owner={};row={};skill={};anim={};anim.dwAnimSpeed=base;other=modifier;queries=0;
 UNITS_InitializeSequence(&owner);UNITS_UpdateAnimRateAndVelocity(&owner,__FILE__,__LINE__);
 events.clear();sub_6FCBCE70(&game,&owner);int action=0,end=0;
 for(auto e:events){if(e.type==1)action=e.frame-1000;if(e.type==2)end=e.frame-1000;}
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",base,modifier,owner.dwSeqSpeed,owner.wAnimSpeed,owner.dwSeqMode,owner.dwSeqFrameCount,action,end,queries);first=false;
}printf("]\n");}
'''
    path=STAGE/'blade-fury-rate-source-probe.cpp';path.write_text(cpp);binary=path.with_suffix('')
    subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(binary)],check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'blade-fury-rate-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    rows=json.loads(run.stdout);assert len(rows)==24
    for base,modifier,speed,anim,mode,count,action,end,queries in rows:
        assert (speed,anim,mode,count,action,end,queries)==(256,base*max(15,min(175,modifier))//100,10,4864,12,19,1)
    ref=ROOT/'tools/reference/d2r-named-equipment';raw=(ref/'json/skills.json').read_bytes();meta=json.loads((ref/'sources.json').read_text())
    assert hashlib.sha256(raw).hexdigest()==meta['files']['json/skills.json']['sha256']
    pinned=json.loads(raw)['266'];assert pinned['seqnum']==23 and pinned['seqtrans']=='A1' and not pinned.get('useAttackRate')
    out=STAGE/'blade-fury-rate-source-probe.json';out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    writers=[n for n in routines if n.startswith('UNITS_Update') and n not in ['UNITS_UpdateOtherAnimRateAndVelocity','UNITS_UpdateAnimRateAndVelocity']]
    e=dict(referenceVersion='3.3.93847',nativeVerifiedByThisSourceProbe=False,legacyCommit=tree['sha'],sourceHashes=hashes,
           compiledRoutineHashes={k:v for k,v in routines.items() if k not in writers},reviewedUnexecutedWriterHashes={k:routines[k] for k in writers},
           sharedTimingEvidence='tools/reference/blade-fury-timing-source-evidence.json',
           fixture=dict(path=str(out.relative_to(ROOT)),sha256=hashlib.sha256(out.read_bytes()).hexdigest(),cases=24),
           established=['Complete ordinary SQ initialization, router, all selected-route predicates, other-rate writer, speed setter and source scheduler compose. Current Fury seqtransA1/useAttackRate0 selects neither FCR nor attack-rate modulation. Faster item-stat queries assert if reached.',
                        'Initialization leaves sequence speed256 across24 declared animation-base/other-rate controls. Other-rate changes wAnimSpeed but does not rewrite dwSeqSpeed. The scheduler uses the latter, retaining source action12/end19. Do not route Fury through generic IAS/FCR timing.'],
           limits=['Normal unmorphed ordinary learned-player SQ context; identity ConvertMode, sequence record19/23, skill lookup and animation metadata are declared services. Nonselected cast/block/recovery/movement/attack writers reject execution; their bodies are reviewed, not executed.',
                   'This composes the older rate/initial scheduler, not player mode entry or client reentry/event-queue/state-expiry dispatch. Current executable warmup/cadence/release observations remain separate. Native rate binding is still pending.'],
           sanitizers='ASan/UBSan; exit0 and empty runtime stderr')
    target=ROOT/'tools/reference/blade-fury-rate-source-evidence.json';render=json.dumps(e,indent=2)+'\n'
    if check:assert target.read_text()==render
    else:target.write_text(render)
    print('24 complete Blade Fury SQ initialization/rate/router/scheduler controls pass; sequence256/action12/end19 independent of declared ordinary animation-rate metadata; native mode/input remain pending.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
