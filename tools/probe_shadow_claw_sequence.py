#!/usr/bin/env python3
"""Owned Shadow monster sequence plus complete NPC callback/do35 composition."""
import csv,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-claw-sequence-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 bodies={};sources={}
 def source(rel,name):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel);sources[rel]=sha(p);body=function(raw.decode(),name);bodies[name]=hashlib.sha256(body.encode()).hexdigest();return body
 pin=json.loads((ROOT/'tools/reference/d2r-local-installation-evidence.json').read_text());install=Path(pin['installationPath']);assert sha(install/'.build.info')==pin['buildInfo']['sha256'] and sha(install/'D2R.exe')==pin['executable']['sha256']
 local=ROOT/'.local/research/d2r-3.3.93847';mp=local/'monseq.txt';stats=local/'monstats.txt';assert sha(mp)=='a48a884a654cab7cafe0be2764fa8a77f428a9b48855d27461d133a1f553898c'and sha(stats)=='8831fce35fc549a4849b13a1bd467eb34913f58b8269c0c1bbfa76aeabb6d32e'
 rows=[r for r in csv.DictReader(mp.read_text().splitlines(),delimiter='\t')if r['sequence']=='seq_swtigerfist'];assert len(rows)==16
 data=[[{'A2':5,'S4':11}[r['mode']],int(r['frame']),int(r['dir']or 0),int(r['event']or 0)]for r in rows];assert [i for i,r in enumerate(data)if r[3]]==[6,10]
 monsters={r['Id']:r for r in csv.DictReader(stats.read_text().splitlines(),delimiter='\t')};links={}
 for name in ['shadowwarrior','shadowmaster']:
  links[name]={i:monsters[name]['Sk'+str(i)+'mode']for i in [1,3,6]};assert all(v=='seq_swtigerfist'for v in links[name].values())
 anim=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text());ap=ROOT/anim['localRawPath'];assert sha(ap)==anim['sha256'];raw=ap.read_bytes();offset=0;gh=[]
 for _ in range(256):
  n=int.from_bytes(raw[offset:offset+4],'little');offset+=4
  for _ in range(n):
   r=raw[offset:offset+160];offset+=160;code=r[:8].split(b'\0')[0].decode('ascii')
   if code in ['AIGHHT1','AIGHHT2','AIGHHTH']:gh.append(dict(Code=code,Frames=int.from_bytes(r[8:12],'little'),Speed=int.from_bytes(r[12:16],'little')))
 assert len(gh)==3 and all(r['Speed']==256 for r in gh)
 balance=ROOT/'data/Sanctuary/Balance/shadow-claw-sequence.json';balance.write_text(json.dumps(dict(ReferenceVersion='3.3.93847',Code='seq_swtigerfist',Mode=14,GraphicsMode=18,Points=16,Frames=16,BaseSpeed=256,Skills=[259,269,274],Rows=data),indent=2)+'\n')
 # Existing full builder fixture services are reused; NPC callback is new.
 template=BASE/'assassin-claw-sequence-source-probe.cpp';cpp=template.read_text().split('D2AnimSeqTxt gPlayerSequenceDragonClaw_HTH_HT1[')[0]
 for name in ['SKILLS_SrvDo034_TigerStrike_CobraStrike_RoyalStrike','SKILLS_SrvDo035_FistsOfFire_ClawsOfThunder_BladesOfIce']:
  assert function(cpp,name)==source('source/D2Game/src/SKILLS/SkillAss.cpp',name)
 cpp=cpp.replace('dwClassId=6,dwAnimMode=18','dwClassId=417,dwAnimMode=14').replace('int dwUnitType=0','int dwUnitType=1').replace('unsigned dwSeqFrame=0,dwFlags=0;','unsigned dwSeqFrame=0,dwFlags=0,dwSeqMode=0;int nActionFrame=0;')
 cpp=cpp.replace('bool UNITS_CanDualWield(D2UnitStrc*){return true;}','')
 cpp+='\nusing BOOL=int;using D2AnimSeqEvent=int;constexpr int TRUE=1,FALSE=0,UNIT_PLAYER=0,UNIT_MONSTER=1,PCLASS_BARBARIAN=4,PCLASS_ASSASSIN=6,MONSTER_SHADOWWARRIOR=417,MONSTER_SHADOWMASTER=418,UNITFLAG_SQGFXCHANGE=32;\n'
 cpp+=source('source/D2Common/src/Units/Units.cpp','UNITS_CanDualWield')+'\n'
 cpp+='D2AnimSeqTxt npcRows[16]={'+','.join('{0,'+','.join(map(str,r))+'}'for r in data)+'};\n'
 cpp+='D2AnimSeqTxt*DATATBLS_GetMonSeqTxtRecordFromUnit(D2UnitStrc*){return npcRows;}int DATATBLS_GetSeqFramePointsCount(D2UnitStrc*){return 4096;}int DATATBLS_GetSeqFrameCount(D2UnitStrc*){return 16;}void UNITS_SetAnimActionFrame(D2UnitStrc*,int){std::abort();}\n'
 cpp+=source('source/D2Common/src/DataTbls/SequenceTbls.cpp','DATATBLS_ComputeSequenceAnimation')+'\n'
 for name in ['UNITS_InitializeSequence','UNITS_StopSequence','UNITS_IsAtEndOfFrameCycle']:cpp+=source('source/D2Common/src/Units/Units.cpp',name)+'\n'
 cpp+=source('source/D2Game/src/PLAYER/Player.cpp','sub_6FC7C7B0')+'\n'
 cpp+='int chosenSkill=259,ordinal=0,lastHand=0;void sub_6FC63680(D2GameStrc*,D2UnitStrc*){}\nvoid sub_6FD13410(D2GameStrc*g,D2UnitStrc*p){lastHand=sub_6FC7C7B0(p)==&rightWeapon?1:2;hit=(hitMask&(1<<ordinal++))!=0;p->dwFlags|=UNITFLAG_SKSRVDOFUNC;SKILLS_SrvDo035_FistsOfFire_ClawsOfThunder_BladesOfIce(g,p,chosenSkill,rankNow);}\n'
 cpp+=source('source/D2Game/src/MONSTER/MonsterMode.cpp','sub_6FC64E90')+'\n'
 cpp+=r'''
int main(){printf("[");bool comma=false;for(int skill:{259,269,274})for(int speed:{38,128,175,256,333,448})for(int initial:{0,2})for(int mask:{0,1,2,3}){
owner={};owner.dwClassId=417+(mask&1);UNITS_InitializeSequence(&owner);owner.dwSeqSpeed=speed;row={};list={initial,initial*50,3,skill,321};exists=initial>0;enabled=exists;chosenSkill=skill;rankNow=3;hitMask=mask;ordinal=lastHand=0;drains=allocs=0;saved={};selected=record=allocation=true;handMode=3;
printf("%s{\"Skill\":%d,\"Speed\":%d,\"Initial\":%d,\"Mask\":%d,\"Trace\":[",comma?",":"",skill,speed,initial,mask);comma=true;
for(int tick=1;tick<200;tick++){unsigned point=owner.dwSeqFrame;int remaining=owner.dwFrameCountPrecise,action=owner.nActionFrame,flags=bool(owner.dwFlags&64);lastHand=0;int result=sub_6FC64E90(&game,&owner);
printf("%s[%d,%u,%d,%d,%d,%d,%u,%d,%d,%d,%d,%d]",tick>1?",":"",tick,point,remaining,action,flags,result,owner.dwSeqFrame,owner.dwFrameCountPrecise,owner.nActionFrame,bool(owner.dwFlags&64),list.count,lastHand);if(result==2)break;}printf("]}");}printf("]\n");}
'''
 cpppath=STAGE/'source.cpp';cpppath.write_text(cpp);binary=STAGE/'probe';r=subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-omit-frame-pointer','-no-pie',str(cpppath),'-o',str(binary)],capture_output=True,text=True);(STAGE/'compiler.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stderr[-2200:]
 r=subprocess.run([str(binary)],capture_output=True,text=True);(STAGE/'sanitizer.log').write_text(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr;cases=json.loads(r.stdout);assert len(cases)==144
 fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 # Reuse the existing authenticated attack-rate fixture for twelve new NPC mode14 contexts.
 rate_template=BASE/'shadow-animation-source/source.cpp';parent=json.loads((ROOT/'tools/reference/shadow-animation-source-evidence.json').read_text());assert sha(rate_template)==parent['artifacts'][str(rate_template.relative_to(ROOT))]
 rate=rate_template.read_text().split('int main(){')[0]
 for rel,name in [('source/D2Common/src/D2Skills.cpp','D2COMMON_11013_ConvertMode'),('source/D2Common/src/Units/Units.cpp','UNITS_UpdateAttackAnimRateAndVelocity')]:assert function(rate,name)==source(rel,name)
 rate+=r"""
int main(){D2StatesTxt states[120];tables.pStatesTxt=states;printf("[");bool comma=false;for(int cls:{417,418})for(auto r:std::vector<std::vector<int>>{{0,100,0,0},{40,100,20,-30},{300,200,-30,20},{0,15,-30,20},{10,30,20,-30},{120,150,20,20}}){D2UnitStrc actor;actor.dwClassId=cls;actor.dwAnimMode=14;ias=r[0];attack=r[1];rightRate=r[2];leftRate=r[3];anim.dwAnimSpeed=256;int type=1,c=cls,mode=14;D2COMMON_11013_ConvertMode(&actor,&type,&c,&mode,__FILE__,__LINE__);merges=0;UNITS_UpdateAttackAnimRateAndVelocity(&actor);printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",comma?",":"",cls,ias,attack,rightRate,leftRate,type,c,mode,actor.dwSeqSpeed,merges);comma=true;}printf("]\n");}
"""
 rate_path=STAGE/'rate.cpp';rate_path.write_text(rate);rate_binary=STAGE/'rate';r=subprocess.run(['g++','-std=c++20','-O1','-fsanitize=address,undefined','-fno-omit-frame-pointer','-no-pie',str(rate_path),'-o',str(rate_binary)],capture_output=True,text=True);(STAGE/'rate-compiler.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stderr[-1800:]
 r=subprocess.run([str(rate_binary)],capture_output=True,text=True);(STAGE/'rate-sanitizer.log').write_text(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr
 rates=json.loads(r.stdout);assert len(rates)==12 and all(r[5:8]==[0,6,18]and r[9]==1 for r in rates)
 rate_fixture=STAGE/'rates.json';rate_fixture.write_text(json.dumps(rates,separators=(',',':'))+'\n')
 # Lookup/composite/rate supplier contracts are reviewed,not compiled here.
 for rel,name in [('source/D2Common/src/D2Skills.cpp','SKILLS_GetSeqNumFromSkill'),('source/D2Common/src/DataTbls/SequenceTbls.cpp','DATATBLS_GetSeqRecordFromUnit'),('source/D2Common/src/D2Composit.cpp','D2Common_10884_COMPOSIT_unk')]:source(rel,name)
 evidence=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=sources,routineHashes=bodies,contexts=144,rateContexts=12,installation=dict(path=pin['installationPath'],buildInfoSha256=pin['buildInfo']['sha256'],executableSha256=pin['executable']['sha256']),ownedInputs={str(p.relative_to(ROOT)):sha(p)for p in [mp,stats,ap]},slotLinks=links,sequence=data,gh=gh,artifacts={str(p.relative_to(ROOT)):sha(p)for p in [Path(__file__).resolve(),template,balance,cpppath,binary,fixture,rate_template,rate_path,rate_binary,rate_fixture]},scope='Full normal NPC mode14 callback+initializer/step/end+do35/do34+selector+actual NPC dual eligibility with owned16row table. Skillflags0 and valid target/record/inventory/stat-list/allocation are declared fixture services. Monster-slot sequence lookup/composite GH override reviewed separately;supplied speeds are not a current executable rate certificate. Current monotonic point parity/bit64 is preserved,not player action ordinals;modern parity stays unverified.')
 assert sha(install/'.build.info')==pin['buildInfo']['sha256']and sha(install/'D2R.exe')==pin['executable']['sha256'];(ROOT/'tools/reference/shadow-claw-sequence-source-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n');print('Shadow own sequence:144 whole NPC callback compositions,owned tables and ASan/UBSan clean.')
if __name__=='__main__':main()
