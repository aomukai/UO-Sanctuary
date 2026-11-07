#!/usr/bin/env python3
"""Execute complete older transformed Valkyrie startup/mode/rate bodies."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,sys
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'));from probe_current_item_affixes import function
def main():
 inputs={}
 def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
 def read(p):inputs[str(p.relative_to(ROOT))]=sha(p);return p.read_text()
 B=ROOT/'.local/class-skills';S=B/'valkyrie-startup-source';S.mkdir(exist_ok=True);parent=json.loads(read(ROOT/'tools/reference/valkyrie-animation-source-evidence.json'));p=B/'valkyrie-animation-source/source.cpp';assert hashlib.sha256(p.read_bytes()).hexdigest()==parent['artifacts'][str(p.relative_to(ROOT))]
 cpp=read(p).split('int main(){')[0]
 cpp=cpp.replace('assert(id==67||id==68)', 'assert(id==67||id==68||id==69)').replace('return id==67?move:attack;', 'return id==67?move:id==68?attack:100;')
 cpp=cpp.replace('UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_TYPES_COUNT=6','UNIT_PLAYER=0,UNIT_MONSTER=1,UNIT_OBJECT=2,UNIT_MISSILE=3,UNIT_ITEM=4,UNIT_TILE=5,UNIT_TYPES_COUNT=6')
 cpp=cpp.replace('struct Anim{int dwAnimSpeed=256;','struct Anim{int dwFrames=18,dwAnimSpeed=256;')
 cpp=cpp.replace('struct D2UnitStrc{int dwUnitType','struct D2ObjectData{int unused;};struct D2UnitStrc{int nActionFrame=0,dwUnitId=7,dwFlags=0;void*pMemoryPool=nullptr;D2ObjectData*pObjectData=nullptr;int dwUnitType')
 cpp=cpp.replace('int nWalkSpeed=6;','int nWalkSpeed=6,nRunSpeed=9;')
 cpp=cpp.replace('constexpr int MONMODE_RUN=15,','constexpr int STATLIST_TEMPONLY=1,UNITFLAG_DOUPDATE=1,IMODE_ONGROUND=0,IMODE_DROPPING=1;\nconstexpr int ')
 # Complete setter branches remain in the TU, with unreachable services refusing.
 cpp+=r'''
 struct D2StatListStrc{int value=0;}temp;int extra=0,baseMove=75,refreshes=0,freed=0,lookups=0,allocations=0,removals=0;
 struct D2ObjectsTxt{int dwFrameCnt[8]={},nStart[8]={},wFrameDelta[8]={},nSync=0;};
 struct D2MissilesTxt{int wAnimRate=0,nAnimLen=0;};
 D2ObjectsTxt*DATATBLS_GetObjectsTxtRecord(int){assert(false);return nullptr;}
 D2MissilesTxt*DATATBLS_GetMissilesTxtRecord(int){assert(false);return nullptr;}
 bool STATES_IsUnitShapeShifted(D2UnitStrc*){assert(false);return false;}
 int SEED_RollLimitedRandomNumber(void*,int){assert(false);return 0;}
 void UNITS_InitializeSequence(D2UnitStrc*){assert(false);}
 D2AnimSeqTxt*DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*){assert(false);return nullptr;}
 D2StatListStrc*STATLIST_AllocStatList(void*,int flags,int expires,int type,int id){assert(flags==1&&expires==0&&type==1&&id==7);allocations++;temp={};return &temp;}
 void STATLIST_SetStat(D2StatListStrc*s,int id,int value,int layer){assert(s==&temp&&id==67&&layer==0);s->value=value;}
 void D2COMMON_10475_PostStatToStatList(D2UnitStrc*,D2StatListStrc*s,int yes){assert(s==&temp&&yes==1);extra=s->value;move=baseMove+extra;}
 void STATLIST_DeactivateTemporaryStates(D2UnitStrc*){if(extra!=0)removals++;extra=0;move=baseMove;}
 void UNITROOM_RefreshUnit(D2UnitStrc*){refreshes++;}
 void SUNITDMG_FreeAttackerCombatList(D2GameStrc*,D2UnitStrc*){freed++;}
 void UNITS_UpdateAnimRateAndVelocity(D2UnitStrc*,const char*,int);
 void UNITS_SetAnimData(D2UnitStrc*,int,int,int);
 void UNITS_SetAnimStartFrame(D2UnitStrc*);
 '''
 tree=json.loads((B/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 def pinned(rel):
  p=B/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(t['sha']for t in tree['tree']if t['path']==rel);inputs[str(p.relative_to(ROOT))]=sha(p);return raw.decode()
 units=pinned('source/D2Common/src/Units/Units.cpp');game=pinned('source/D2Game/src/UNIT/SUnit.cpp')
 names=[(units,'UNITS_CharacterStartRunningOrKnockback'),(units,'UNITS_SetAnimStartFrame'),(units,'UNITS_ChangeAnimMode'),(game,'SUNIT_SetCombatMode'),(game,'sub_6FCBC4D0')];bodies={n:function(t,n)for t,n in names};cpp+='\n'.join(bodies.values())
 cpp=cpp.replace('D2ObjectData*pObjectData=nullptr;','D2ObjectData*pObjectData=nullptr;void*pSeed=nullptr;')

 modulators=units[units.index('struct D2AnimUnkStrc'):units.index('struct D2UnitAnimModeStatBasePercentageStrc')]
 def enum(rel,name):return re.search(r'enum '+name+r'\s*\{.*?\};',pinned(rel),re.S).group()
 monmodes=enum('source/D2Common/include/D2Monsters.h','D2C_MonModes')
 monids=enum('source/D2Common/include/DataTbls/MonsterIds.h','D2C_MonsterIds')
 def enum_values(raw):
  raw=re.sub(r'//[^\n]*','',raw.split('{',1)[1].rsplit('}',1)[0]);values={};value=-1
  for bit in raw.split(','):
   bit=bit.strip()
   if not bit:continue
   if '='in bit:
    name,number=map(str.strip,bit.split('=',1));value=int(number,0)
   else:name=bit;value+=1
   values[name]=value
  return values
 assert enum_values(enum('source/D2Common/include/D2StatList.h','D2C_ItemStats'))['STAT_OTHER_ANIMRATE']==69
 assert enum_values(enum('source/D2Common/include/DataTbls/MonsterTbls.h','D2C_MonStatsTxtFlagIndices'))['MONSTATSFLAGINDEX_NPC']==8
 assert enum_values(enum('source/D2Common/include/DataTbls/SkillsTbls.h','D2C_SkillsTxtFlagIndices'))['SKILLSFLAGINDEX_USEATTACKRATE']==19
 flags=enum_values(enum('source/D2Common/include/D2Skills.h','D2C_SkillFlags'));assert flags['SKFLAG_PASSIVE']==1 and flags['SKFLAG_STUN']==0x1000

 cpp=cpp.replace('struct Anim{',monmodes+'\n'+monids+'\nstruct Anim{')
 cpp=cpp.replace('int dwFrames=18,dwAnimSpeed=256;', 'int dwFrames=18,dwAnimSpeed=256;')
 cpp=cpp.replace('struct D2MonStatsTxt{int ', 'struct D2MonStatsTxt{unsigned dwMonStatsFlags=0;int ')
 cpp=cpp.replace('const D2MonStatsTxt*DATATBLS_GetMonStatsTxtRecord', 'D2MonStatsTxt*DATATBLS_GetMonStatsTxtRecord')
 # Neutral-rate stat is read by a full helper. Source numeric value is bound
 # by the original stat enum at publication,not a new table inference.
 cpp=cpp.replace('int STATLIST_UnitGetStatValue(', 'constexpr int OTHER_ANIM_STAT=69;\nint STATLIST_UnitGetStatValue(')
 extra=r"""
 constexpr int MONSTATSFLAG_NPC=1<<8,SKILLSFLAG_USEATTACKRATE=1<<19,SKFLAG_PASSIVE=1,SKFLAG_STUN=0x1000,STAT_OTHER_ANIMRATE=69;
 struct D2SkillsTxt{int nSkillId=0,nSeqTrans=0;unsigned dwFlags[2]={};};struct D2SkillStrc{D2SkillsTxt*pSkillsTxt=nullptr;unsigned dwFlags=0;};
 D2SkillStrc*UNITS_GetUsedSkill(D2UnitStrc*){return nullptr;}
 D2SkillsTxt*DATATBLS_GetSkillsTxtRecord(int){assert(false);return nullptr;}
 void UNITS_UpdateCastAnimRateAndVelocity(D2UnitStrc*){assert(false);}
 void UNITS_UpdateBlockAnimRateAndVelocity(D2UnitStrc*){assert(false);}
 void UNITS_UpdateGetHitAnimRateAndVelocity(D2UnitStrc*){assert(false);}
 void UNITS_UpdateKnockbackAnimRateAndVelocity(D2UnitStrc*,int,int,int){assert(false);}
 Anim*DATATBLS_GetAnimDataRecord(D2UnitStrc*a,int cls,int mode,int kind,void*){lookups++;assert(kind==0&&cls==0);assert(mode==1||mode==2||mode==3||mode==7);anim.dwFrames=mode==7?18:8;anim.dwAnimSpeed=mode==1?128:256;std::fill(std::begin(anim.pFrameFlags),std::end(anim.pFrameFlags),0);if(mode==7)anim.pFrameFlags[11]=1;return &anim;}
 """
 newnames=['UNITS_IsSeqAnimSpeedModulatedByFCR','UNITS_IsAnimModeBlocking','UNITS_IsAnimModeGetHit','UNITS_IsAnimModeKnockBack','UNITS_CanAnimModeUseVelocityModifier','UNITS_CanAnimModeUseAttackRate','UNITS_AnimModeAllowsAnimSpeed','UNITS_UpdateOtherAnimRateAndVelocity','UNITS_SetAnimData','UNITS_UpdateAnimRateAndVelocity']
 newbodies={n:function(units,n)for n in newnames}
 # New dependencies precede the setter bodies that call the dispatcher.
 cpp=cpp[:cpp.index(bodies['UNITS_CharacterStartRunningOrKnockback'])]+modulators+extra+'\n'+'\n'.join(newbodies.values())+'\n'+'\n'.join(bodies.values())
 bodies.update(newbodies)
 cpp+=r"""
 int main(){int count;assert(scanf("%d",&count)==1);printf("[");bool sep=false;
 for(int c=0;c<count;c++){int baseAttack,itemMove,initialMove,variant;assert(scanf("%d%d%d%d%d%d%d",&ias,&attack,&fasterMove,&itemMove,&baseAttack,&initialMove,&variant)==7);attack+=baseAttack;D2StatesTxt states[94];tables.pStatesTxt=states;D2UnitStrc a;a.dwAnimMode=1;D2DynamicPathStrc path;a.pDynamicPath=&path;D2GameStrc g;extra=0;move=baseMove=initialMove+itemMove;refreshes=freed=lookups=allocations=removals=0;
 int step=0;for(int m:{15,15,4,4,2,2,1,1,15,2,4}){int old=a.dwAnimMode;SUNIT_SetCombatMode(&g,&a,m);assert(a.dwUnitType==1&&extra==(m==15?50:0));int start=a.nSeqCurrentFramePrecise;sub_6FCBC4D0(&a);assert(a.nSeqCurrentFramePrecise==0&&extra==(m==15?50:0));events.clear();if(m==4)sub_6FCBCE70(&g,&a);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",sep?",":"",c,variant,step++,old,m,extra,move,start,a.wAnimSpeed,path.velocity,a.dwFrameCountPrecise,refreshes,freed,allocations,removals);sep=true;bool se=false;for(auto e:events){printf("%s[%d,%d,%d,%d]",se?",":"",e.type,e.frame,e.kind,e.param);se=true;}printf("]]");}
 assert(!UNITS_ChangeAnimMode(nullptr,2));a.dwUnitType=UNIT_TILE;assert(!UNITS_ChangeAnimMode(&a,2));}
 printf("]\n");}
 """
 for n,b in bodies.items():assert function(cpp,n)==b,n
 for n,h in parent['completeRoutineHashes'].items():assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==h,n
 source=S/'source.cpp';source.write_text(cpp)
 r=subprocess.run(['g++','-std=c++20','-O1','-g1','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(source),'-o',str(S/'probe')],capture_output=True);(S/'compiler.log').write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stderr.decode()[-6000:]
 cases=json.loads(read(B/'valkyrie-animation-source/cases.json'));selected=[c for c in cases if c['Mode']==4 and c['BaseAttack']==100 and c['BaseMove']==100];assert len(selected)==57
 contexts=[dict(Case=c['Case'],Rank=c['Rank'],Difficulty=c['Difficulty'],Stats=c['Stats'],BaseAttack=atk,BaseMove=mov,Variant=v)for c in selected for v,(atk,mov)in enumerate([(100,75),(100,0),(-200,75),(-200,0),(300,75),(300,0),(50,75),(50,0)])]
 request=str(len(contexts))+' '+' '.join(str(x)for c in contexts for x in [c['Stats'][0],c['Stats'][1],c['Stats'][3],c['Stats'][2],c['BaseAttack'],c['BaseMove'],c['Variant']])+'\n';(S/'input.txt').write_text(request)
 r=subprocess.run([str(S/'probe')],input=request.encode(),capture_output=True);(S/'sanitizer.log').write_bytes(r.stderr);(S/'output.json').write_bytes(r.stdout);assert r.returncode==0 and not r.stderr,r.stderr.decode()[-2000:];rows=json.loads(r.stdout);assert len(rows)==5016
 for i,c in enumerate(contexts):c['Steps']=rows[i*11:(i+1)*11]
 (S/'cases.json').write_text(json.dumps(contexts,separators=(',',':'))+'\n')

 for i,c in enumerate(contexts):
  for step,r in enumerate(c['Steps']):
   assert r[0]==i and r[1]==c['Variant']and r[2]==step
   assert r[6]==c['BaseMove']+c['Stats'][2]+(50 if r[4]==15 else 0)
   assert r[10]==(18 if r[4]==4 else 8)*256
   assert r[12]==step+1 and r[13]==[1,1,1,1,1,1,1,1,2,2,2][step]
   assert r[14]==[0,0,1,1,1,1,1,1,1,2,2][step]
   if r[4]==4:assert len(r[15])==2 and r[15][0][0]==1 and r[15][0][2]==1 and r[15][1][0]==2
 runtime=ROOT/'data/Sanctuary/Balance/valkyrie-animation.json';animation=json.loads(read(runtime));assert animation['WalkVelocity']==6 and animation['RunVelocity']==9
 assert {row['Code']:row for row in animation['Rows']}=={row['Code']:row for row in parent['records']if row['Code']in ['AMA12HT','AMWL2HT','AMRN2HT','AMNU2HT']}
 initializer=ROOT/'tools/reference/valkyrie-initializer-source-evidence.json';initial=json.loads(read(initializer));assert initial['contexts']==840
 initcode=pinned('source/D2Game/src/MONSTER/Monster.cpp');initbody=function(initcode,'MONSTER_InitializeStatsAndSkills')
 assert 'STAT_ATTACKRATE, 100' in initbody and 'STAT_VELOCITYPERCENT, 75'in initbody
 receipt=dict(referenceVersion='3.3.93847',currentExecutableParity=False,outerMonsterModeDispatcherExecuted=False,completeRateDispatcherExecuted=True,contexts=456,transitions=5016,originalLoadouts=57,canonicalContexts=57,nullAndTileGuards=912,completeRoutineHashes={**parent['completeRoutineHashes'],**{n:hashlib.sha256(b.encode()).hexdigest()for n,b in bodies.items()}},initialStatsRoutineHash=hashlib.sha256(initbody.encode()).hexdigest(),rates=dict(attack=100,walk=75,run=125,temporaryRunBonus=50),inputs=inputs,services=['Exact current four transformed rows and Amazon walk6/run9 are supplied to the animation lookup. Stat/temporary-list/player-graphics services preserve current original-item values;actual monster identity1 is retained while converted graphics type0/class0 choose player predicates.', 'The complete rate dispatcher and its six predicates execute unchanged. Cast/block/gethit/knockback/used-skill services assert if selected;those states are outside the supplied idle/walk/run/ordinary A1 domain. The outer monster mode-change dispatcher, room/path generation, damage and event-loop callbacks remain separate.'],established=['The actual combat-mode setter calls complete ChangeAnimMode, which deactivates temporary states on a changed mode and enters the complete start-frame getter. Converted Amazon run allocates/posts exactly one +50velocity list. Repeating run does not stack it;leaving run removes it.', 'The subsequent complete game reset helper sets sequence frame0 and updates rates without removing the running list. Full initial scheduler still uses spear frame bonus2 and the action marker11/end plan. Item attack/movement/faster-movement values remain separate from initial100/75 and temporary50.', 'All57 original loadouts execute eight supplied base-rate controls and11 transitions,including repeated run/A1/walk/neutral. Null and tile mode-change guards refuse without entering unsupported animation services. Current default rates100/75/125follow the accepted initial stats boundary plus current Amazon charstats.'],limits=['Complete older bodies with pinned current table/animation inputs supplement the reference;current executable equivalence is not claimed. The full outer monster mode dispatcher is not executed here.', 'Actors without a dynamic path, sequences and other animation modes retain explicit unsupported services. This does not certify learned summon/payment/replacement, town/parking, sunder, external elemental stat lists or target-type contact adapters.'],artifacts={str(p.relative_to(ROOT)):sha(p)for p in [source,S/'probe',S/'input.txt',S/'output.json',S/'cases.json',S/'compiler.log',S/'sanitizer.log',Path(__file__).resolve()]})
 (ROOT/'tools/reference/valkyrie-startup-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n')
 print('Mode-entry probe passes5016 source transitions across57loadouts/456contexts;15added complete bodies+prior9;full rate dispatcher;ASan/UBSan clean.')

def check():
 p=ROOT/"tools/reference/valkyrie-startup-source-evidence.json";d=json.loads(p.read_text())
 for group in ["inputs","artifacts"]:
  for name,h in d[group].items():assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==h,name
 assert len(d["completeRoutineHashes"])==24 and d["transitions"]==5016
 print("Valkyrie startup source/artifacts:24complete bodies,5016transitions;scope limits retained.")
if __name__=="__main__":
 p=argparse.ArgumentParser();p.add_argument("--check",action="store_true");a=p.parse_args()
 if a.check:check()
 else:main()
