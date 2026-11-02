#!/usr/bin/env python3
"""Compose complete older field roll/flags/mitigation/HP/NPC-return bodies."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/fire-wall-return-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def probe(check=False):
 STAGE.mkdir(parents=True,exist_ok=True);prior_path=ROOT/'tools/reference/telekinesis-combat-source-evidence.json';prior=json.loads(prior_path.read_text());original=ROOT/'.local/class-skills/telekinesis-combat-source/probe.cpp';retained=STAGE/'reused-telekinesis.cpp';expected=prior['artifacts'][str(original.relative_to(ROOT))]
 if not retained.exists():assert sha(original)==expected;shutil.copy2(original,retained)
 assert sha(retained)==expected;cpp=retained.read_text().split('int main()',1)[0]
 tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 blocks={};sources={}
 paths={'source/D2Game/src/MISSILES/MissMode.cpp':('MISSMODE_RollDamageValue','MISSMODE_SrvDmg03_Blaze_FireWall_ImmolationFire_MeteorFire','MISSMODE_SetDamageFlags'),
 'source/D2Game/src/UNIT/SUnitDmg.cpp':('SUNITDMG_ApplyResistancesAndAbsorb','SUNITDMG_CalculateTotalDamage','SUNITDMG_ExecuteEvents','SUNITDMG_ExecuteMissileDamage','sub_6FCC1870','SUNITDMG_GetHitClass')}
 for rel,names in paths.items():
  p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);sources[rel]=sha(p)
  for n in names:
   body=function(raw.decode(),n);blocks[n]=body
   if n in ('MISSMODE_RollDamageValue','MISSMODE_SrvDmg03_Blaze_FireWall_ImmolationFire_MeteorFire'):cpp+='\n'+body
   else:assert function(cpp,n)==body,n
 old='int MISSILE_GetHitClass(D2UnitStrc*){return 0;}';assert cpp.count(old)==1;cpp=cpp.replace(old,'int MISSILE_GetHitClass(D2UnitStrc*){return 32;}')
 cpp+=r'''
int main(){for(int n=0;n<32;n++)gdwBitMasks[n]=1u<<n;printf("[");bool first=true;
for(int rank:{1,20,255})for(uint32_t low=0;low<128;low++)for(int res:{-100,0,50,100,150})for(int mdr:{0,1,18,20})for(int rate:{41})for(int hp:{256,25600}){
actor={};target={};missile={};game={};row={};mon2={};missileRow={};actor.pGame=target.pGame=missile.pGame=&game;actor.dwUnitType=0;actor.dwUnitId=77;actor.group=1;game.pUnitList[0][77]=&actor;
target.dwUnitType=1;target.dwClassId=413;target.dwUnitId=99;target.group=2;target.dwAnimMode=1;target.pSeed.nLowSeed=low+1;target.pSeed.nHighSeed=666;target.stats[STAT_HITPOINTS]=hp;target.stats[STAT_FIRERESIST]=res;target.stats[STAT_MAGIC_DAMAGE_REDUCTION]=mdr;target.stats[STAT_LAST_SENT_HP_PCT]=128;
missile.dwUnitType=3;missile.dwClassId=69;missile.dwFlagEx=UNITFLAGEX_STOREOWNERINFO;missile.dwOwnerType=0;missile.dwOwnerGUID=77;missile.pSeed.nLowSeed=low;missile.pSeed.nHighSeed=666;missile.stats[9000]=rank*256;missile.stats[9001]=(rank+1)*256;missile.stats[STAT_DAMAGE_FRAMERATE]=rate;missileRow.dwDmgParam[0]=19;
maxLife=12800000;hasRoom=hasRow=hasModes=true;inTown=uninterruptible=frozen=stunned=false;baseId=0;blocked=0;row.dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_KILLABLE];
trace.clear();tkEvents.clear();requested=-1;kills=modes=recoveryCalls=callbackCount=0;tkRaw={};tkAfterEvents={};tkAfterReturn={};
D2DamageStrc d{};d.dwFireDamage=MISSMODE_RollDamageValue(&missile,9000,9001,-1);int raw=d.dwFireDamage;MISSMODE_SrvDmg03_Blaze_FireWall_ImmolationFire_MeteorFire(&game,&missile,&target,&d);int flags=d.wResultFlags;MISSMODE_SetDamageFlags(&game,&missile,&target,&d);
int notices=0,refreshes=0;for(auto& e:trace){notices+=e[0]==14;refreshes+=e[0]==12;}
printf("%s[[%d,%u,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%llu,%llu,%d,%d,%d],[",first?"":",",rank,low,res,mdr,rate,hp,raw,flags,tkAfterEvents.dwDmgTotal,target.stats[STAT_HITPOINTS],d.wResultFlags,d.dwHitClass,d.nHitClassActiveSet,requested,(unsigned long long)missile.pSeed.lSeed,(unsigned long long)target.pSeed.lSeed,kills,notices,refreshes);
for(size_t n=0;n<tkEvents.size();n++)printf("%s%d",n?",":"",tkEvents[n]);printf("]]");first=false;
}printf("]\n");}
'''
 p=STAGE/'probe.cpp';p.write_text(cpp);binary=STAGE/'probe';subprocess.run(['g++','-std=c++20','-O2','-DD2_VERSION_HAS_UBERS',str(p),'-o',str(binary)],check=True);cases=json.loads(subprocess.check_output([str(binary)]));assert len(cases)==15360
 for inputs,out,events in cases:
  rank,low,res,mdr,rate,hp=inputs;stream=0x6AC690C5*low+666;raw=rank*256+(stream&0xffffffff)%256;stream=0x6AC690C5*(stream&0xffffffff)+(stream>>32);soft=int((stream&127)<19)*16384
  reduction=mdr*256 if rate==0 else mdr*256*rate//1024;reduced=raw-reduction;amount=reduced if reduced<=0 else reduced*max(0,100-res)//100;after=hp if amount<=0 else hp-amount;after=0 if after<256 else after
  assert out[:9]==[raw,soft,amount,after,1|soft|(2 if after==0 else 0),32,1,-1,stream],(inputs,out)
  assert out[9]==(666<<32)|(low+1) and out[10]==int(after==0) and out[11]==int(after>0 and soft!=0) and out[12]==int(after>0 and (soft!=0 or amount>0)),(inputs,out)
  assert len(events)==(3 if after==0 else 1),(inputs,events)
 san=STAGE/'probe-san';subprocess.run(['g++','-std=c++20','-g','-DD2_VERSION_HAS_UBERS','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(p),'-o',str(san)],check=True);log=STAGE/'sanitizer.log'
 with log.open('wb') as err:assert json.loads(subprocess.check_output([str(san)],stderr=err))==cases
 assert not log.read_bytes();fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
 result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceHashes=sources,routineHashes={n:hashlib.sha256(b.encode()).hexdigest() for n,b in blocks.items()},cases=len(cases),sanitizers='ASan/UBSan clean',established=['Complete fire roll,field03,flags,event mitigation/HP and NPC return compose. One endpoint roll then one19/128SOFTHIT draw use the missile stream. Flags1or16385,class32active,hit flags0;no ordinary get-hit mode/defender RNG. Surviving SOFTHIT sends recovery notification;normal positive damage refreshes life without recovery when percentage changed. Killed/kill callbacks are not suppressed.'],declaredServices=['Older engine,not modern executable proof. Supplied captured fire endpoints,zero other missile stats,owner lookup,current row flags/hitclass/dParam1 and block=0/critical-free ordinary NPC/stat/room/event services. Older MDR can leave a negative intermediate/total;HP is unchanged and this is not a healing packet. Current native clamping and modern equivalence remain separate. Full fill-all-fields/modern stat/seed/block/PvP/event registry and native recipient/collision remain separate. Rate41is pinned Fire Wall;MDR18matches the supported original-item fixture. MDR0/1/20remain controlled comparison inputs.'],artifacts={str(q.relative_to(ROOT)):sha(q) for q in (Path(__file__).resolve(),prior_path,retained,p,binary,san,log,fixture)})
 table=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';manifest=json.loads((table.parent.parent/'sources.json').read_text());assert sha(table)==manifest['files']['json/missiles.json']['sha256']
 current=next(r for r in json.loads(table.read_text()).values() if r.get('Missile')=='firewall');assert current['HitClass']==32 and current['pSrvDmgFunc']==3 and current['dParam1']==19 and current.get('DamageRate',0)==41
 result.update(currentTable={str(table.relative_to(ROOT)):sha(table)},currentRow=current)
 out=ROOT/'tools/reference/fire-wall-return-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded
 else:out.write_text(encoded)
 print('Fire Wall returns:15360complete older roll/field03/flags/mitigation/HP/NPC-return compositions;ASan/UBSan clean;world/modern stat/event services remain explicit.')
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');probe(a.parse_args().check)
