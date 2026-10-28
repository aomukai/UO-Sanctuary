#!/usr/bin/env python3
"""Execute shared fan, physical endpoints and damage7 dispatch with current Shock Wave rows."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    pinpath=ROOT/'tools/reference/druid-shock-wave-preflight.json';pin=json.loads(pinpath.read_text())
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==pin['legacyCommit']
    hashes={};bodies={};artifacts={};inherited={}
    def body(relative,name):
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=sha(path);text=function(raw.decode(),name);bodies[name]=hashlib.sha256(text.encode()).hexdigest();return text
    def scaffold(name,evidence,names):
        path=BASE/name;receipt=ROOT/evidence;data=json.loads(receipt.read_text());assert data['legacyCommit']==tree['sha']
        text=path.read_text().split('int main()',1)[0]
        for relative,symbol in names:
            assert function(text,symbol)==body(relative,symbol),symbol
        inherited[evidence]=sha(receipt);return text
    def execute(name,text):
        path=BASE/f'druid-shock-wave-{name}-source-probe.cpp';path.write_text(text)
        compiler=BASE/f'druid-shock-wave-{name}-compiler.log';sanitizer=BASE/f'druid-shock-wave-{name}-sanitizer.log'
        with compiler.open('w') as log:
            subprocess.run(['g++','-std=c++20','-Wno-multichar','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(path.with_suffix(''))],stdout=log,stderr=subprocess.STDOUT,check=True)
        result=subprocess.run([str(path.with_suffix(''))],capture_output=True);sanitizer.write_bytes(result.stderr)
        assert result.returncode==0 and not result.stderr,result.stderr.decode()[-3000:]
        output=path.with_suffix('.json');data=json.loads(result.stdout);output.write_text(json.dumps(data,indent=2)+'\n')
        for p in [path,compiler,sanitizer,output]:artifacts[str(p.relative_to(ROOT))]=sha(p)
        return data
    common='source/D2Common/src/D2Skills.cpp';mode='source/D2Game/src/MISSILES/MissMode.cpp'
    names=[(common,x) for x in ['SKILLS_CalculateDamageBonusByLevel','SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage']]
    names += [(mode,x) for x in ['MISSMODE_RollDamageValue','MISSMODE_SrvDmg07_Warcry_ShockWave','MISSMODE_SetDamageFlags']]
    names += [('source/D2Common/src/Units/Units.cpp','UNITS_GetBlockRate')]
    names += [('source/D2Game/src/UNIT/SUnitDmg.cpp',x) for x in ['SUNITDMG_ApplyDodge','SUNITDMG_ApplyBlockOrDodge']]
    names += [('source/D2Game/src/MONSTER/Monster.cpp','MONSTER_SetComponents')]
    cpp=scaffold('warcry-source-probe.cpp','tools/reference/warcry-engine-evidence.json',names)
    cpp=cpp.replace('int MISSILE_GetSkill(D2UnitStrc*){return 154;}','int MISSILE_GetSkill(D2UnitStrc*){return 243;}')
    skill=pin['currentRows']['skills']['Shock Wave'];missile=pin['currentRows']['missiles']['shockwave']
    cpp+='int main(){row={};\n'
    for field,key in [('dwMinDam','MinDam'),('dwMaxDam','MaxDam'),('nToHitShift','HitShift')]:cpp+=f'row.{field}={int(skill[key])};\n'
    for field,key in [('dwMinLvlDam','MinLevDam'),('dwMaxLvlDam','MaxLevDam')]:
        for i in range(5):cpp+=f'row.{field}[{i}]={int(skill[key+str(i+1)])};\n'
    cpp+=f'row.dwParam[0]={int(skill["Param1"])};row.dwParam[1]={int(skill["Param2"])};\n'
    cpp+=r'''D2UnitStrc owner,target,shot;target.dwUnitType=UNIT_MONSTER;shot.dwUnitType=UNIT_MISSILE;shot.dwClassId=511;shot.owner=&owner;bool first=true;std::printf("{\"physicalCases\":[");
for(int rank=1;rank<=255;rank++)for(int hard:{0,1,20})for(int draw:{0,1,255,256,99999}){
owner.synergy=hard*10;int lo=SKILLS_GetMinPhysDamage(&owner,243,rank,1),hi=SKILLS_GetMaxPhysDamage(&owner,243,rank,1);shot.stats[STAT_MINDAMAGE]=lo;shot.stats[STAT_MAXDAMAGE]=hi;owner.synergy=12345;physicalRoll=draw;
int rolled=MISSMODE_RollDamageValue(&shot,STAT_MINDAMAGE,STAT_MAXDAMAGE,-1);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",rank,hard,draw,lo,hi,rolled);}
std::printf("],\"stunCases\":[");first=true;
for(int rank=0;rank<=255;rank++)for(int param:{0,7})for(int valid:{0,1})for(int present:{0,1}){
hasSkill=valid;hasMissile=present;missileRow.dwDmgParam[0]=param;shot.stats[STAT_LEVEL]=rank;D2DamageStrc d;d.dwStunLen=999;d.dwHitClass=1;MISSMODE_SrvDmg07_Warcry_ShockWave(&game,&shot,&target,&d);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%u]",rank,param,valid,present,d.dwStunLen,d.dwHitClass);}
std::printf("],\"dispatchCases\":[");first=true;hasSkill=hasMissile=true;missileRow.dwDmgParam[0]=0;missileRow.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_GETHIT];target.dwClassId=1;component=0;mon.dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_NOSHLDBLOCK];
for(int rank:{1,20,255})for(int rate:{0,10,75})for(int roll:{0,9,10,74,75,99})for(int phys:{0,2560})for(int uninterrupt:{0,1}){
shot.stats[STAT_LEVEL]=rank;target.stats.clear();target.stats[STAT_TOBLOCK]=rate;blockRoll=roll;uninterruptable=uninterrupt;D2DamageStrc d;d.dwPhysDamage=phys;d.wResultFlags=4;d.dwHitClass=64;eventCalls=commitCalls=0;
MISSMODE_SrvDmg07_Warcry_ShockWave(&game,&shot,&target,&d);MISSMODE_SetDamageFlags(&game,&shot,&target,&d);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%u,%d,%d,%d,%u]",rank,rate,roll,phys,uninterrupt,d.wResultFlags,eventCalls,commitCalls,d.dwStunLen,d.dwHitClass);}
std::printf("]}\n");}
'''
    damage=execute('damage',cpp)
    def tier(rank,base,key):
        left=rank-1;amount=int(skill[base])
        for i,cap in enumerate([7,8,6,6,2**30],1):
            n=min(left,cap);amount+=n*int(skill[key+str(i)]);left-=n
        return amount
    for rank,hard,draw,lo,hi,rolled in damage['physicalCases']:
        low=tier(rank,'MinDam','MinLevDam');high=tier(rank,'MaxDam','MaxLevDam')
        low=(low+low*hard*10//100)<<8;high=(high+high*hard*10//100)<<8
        assert (lo,hi,rolled)==(low,high,low+min(draw,high-low-1))
    for rank,param,valid,present,stun,hitclass in damage['stunCases']:
        expected=(param if param>0 else 40+15*(rank-1) if valid and rank>0 else 0,0x60) if present else (999,1)
        assert (stun,hitclass)==expected
    for rank,rate,roll,phys,uninterrupt,result,events,commit,stun,hitclass in damage['dispatchCases']:
        blocked=bool(phys and roll<rate)
        assert (result,events,commit,stun,hitclass)==(4|(16 if blocked else 1),int(not blocked),1,40+15*(rank-1),0x60)
    # Reuse the source-zero creator scaffold, replacing its physical-getter
    # services with the complete getters above. Keep source bodies unchanged.
    missile_source='source/D2Common/src/Units/Missile.cpp'
    creator_names=[(missile_source,x) for x in ['MISSILE_AddStatsToDamage','MISSILE_CalculateFinalDamage','MISSILE_CalculateDamageData','MISSILE_SetDamageStats']]
    creator_names += [(common,x) for x in ['SKILLS_CalculateDamageBonusByLevel','SKILLS_CalculateMasteryBonus','SKILLS_GetMinElemDamage','SKILLS_GetMaxElemDamage','SKILLS_GetElementalLength']]
    capture=scaffold('druid-rabies-damage-source-probe.cpp','tools/reference/druid-rabies-factory-engine-evidence.json',creator_names)
    capture=capture.replace('int nSrcDam=0,nEType=0,','unsigned dwFlags[2]={};int dwMinDam=10,dwMaxDam=20,dwMinLvlDam[5]={3,5,7,7,7},dwMaxLvlDam[5]={3,5,7,7,7},dwDmgSymPerCalc=2;int nSrcDam=0,nEType=0,')
    capture=capture.replace('int nToHitShift=3,','int nToHitShift=8,') if 'int nToHitShift=3,' in capture else capture.replace('nToHitShift=3,','nToHitShift=8,')
    capture=capture.replace('return hard*20;','return hard*10;')
    for name,endpoint in [('SKILLS_GetMinPhysDamage','skillMin'),('SKILLS_GetMaxPhysDamage','skillMax')]:
        old=f'int {name}(D2UnitStrc*,int,int,int){{return {endpoint};}}';assert old in capture
        capture=capture.replace(old,f'int {name}(D2UnitStrc*,int,int,int);')
    header=BASE/'d2moo-pinned-source/source/D2Common/include/DataTbls/SkillsTbls.h';raw=header.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']=='source/D2Common/include/DataTbls/SkillsTbls.h')
    enum=re.search(r'enum\s+\w+\s*\{[^}]*SKILLSFLAGINDEX_KICK[^}]*\};',raw.decode(),re.S).group()
    hashes['source/D2Common/include/DataTbls/SkillsTbls.h']=sha(header)
    capture+='\n'+enum+'\nint STATLIST_GetMinDamageFromUnit(D2UnitStrc*,int){return 99999;}int STATLIST_GetMaxDamageFromUnit(D2UnitStrc*,int){return 99999;}\n'
    for name in ['SKILLS_GetMinPhysDamage','SKILLS_GetMaxPhysDamage']:capture+=body(common,name)+'\n'
    capture=capture.replace('void STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){p->stats[stat]=value;}',
        'int layerMode=0,copiedCount=0,copiedLayers[2]={},copiedValues[2]={};void STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int layer){p->stats[stat]=value;if(stat==STAT_DAMAGE_VS_MONTYPE){copiedLayers[copiedCount]=layer;copiedValues[copiedCount++]=value;}}')
    capture=capture.replace('int STATLIST_CopyStats(D2UnitStrc*,int,D2StatStrc*,int){return 0;}',
        'int STATLIST_CopyStats(D2UnitStrc* p,int stat,D2StatStrc* rows,int capacity){if(p!=&owner || stat!=STAT_DAMAGE_VS_MONTYPE || capacity<2)std::abort();if(!layerMode)return 0;rows[0]={125,7};rows[1]={250,11};return 2;}')
    capture='#include <cstdlib>\n'+capture
    fields=sorted(set(re.findall(r'pMissileDamageData->(\w+)',function(capture,'MISSILE_CalculateDamageData')+function(capture,'MISSILE_AddStatsToDamage')+function(capture,'MISSILE_CalculateFinalDamage'))))
    capture+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank=1;rank<=255;rank++)for(int points:{0,1,20})for(int noise:{0,1})for(int hasOrigin:{0,1})for(int layers:{0,1}){
 owner={};weapon={};missile={};skill={};row={};missileData={};weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=511;missileData.nSkill=243;missileData.nLevel=rank;
 row.nSkill=243;skill.nEType=ELEMTYPE_NONE;skill.dwEMin=skill.dwEMax=skill.dwELen=0;hard=points;critical=true;critCalls=0;layerMode=layers;copiedCount=0;copiedLayers[0]=copiedLayers[1]=copiedValues[0]=copiedValues[1]=0;
 for(int& stat:owner.stats)stat=noise?37:0;for(int& stat:weapon.stats)stat=99999;
 D2UnitStrc origin;for(int& stat:origin.stats)stat=99999;
 D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,hasOrigin?&origin:nullptr,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&d,rank);
 printf("%s[%d,%d,%d,%d,%d,[",first?"":",",rank,points,noise,hasOrigin,layers);first=false;
'''
    for i,field in enumerate(fields):capture+=f'printf("{"," if i else ""}%d",d.{field});\n'
    capture+='printf("],%d,%d,%d,%d,%d,%d,%d,%d]",missile.stats[STAT_MINDAMAGE],missile.stats[STAT_MAXDAMAGE],critCalls,copiedCount,copiedLayers[0],copiedValues[0],copiedLayers[1],copiedValues[1]);}printf("]\\n");}\n'
    captures=execute('capture',capture)
    for rank,hard,noise,origin,layers,values,stored_min,stored_max,critical_calls,count,layer0,value0,layer1,value1 in captures:
        low=tier(rank,'MinDam','MinLevDam');high=tier(rank,'MaxDam','MaxLevDam')
        low=(low+low*hard*10//100)<<8;high=(high+high*hard*10//100)<<8
        expected=[low if field=='nMinDamage' else high if field=='nMaxDamage' else 0 for field in fields]
        assert values==expected,(rank,hard,dict(zip(fields,values)))
        assert (stored_min,stored_max,critical_calls)==(low,high,0)
        assert (count,layer0,value0,layer1,value1)==((2,7,125,11,250) if layers else (0,0,0,0,0))
    assert len(captures)==6120
    fan=scaffold('fan-source-probe.cpp','tools/reference/fan-engine-evidence.json',[
        ('source/D2Game/src/SKILLS/Skills.cpp',x) for x in ['sub_6FD140D0','sub_6FD14120']]+[
        ('source/D2Game/src/SKILLS/SkillAma.cpp','SKILLS_SrvDo008_MultipleShot_Teeth_ShockWave')])
    fan+=r'''int main(){row.wSrvMissileA=row.wSrvMissileB=511;calc[0]=5;calc[1]=calc[2]=0;bool first=true;std::printf("[");
for(int dx=-8;dx<=8;dx++)for(int dy=-8;dy<=8;dy++)for(int rank:{1,20,255})for(int w:{0,1})for(int allocate:{0,1}){
weapon=w;allocations=allocate;tx=owner.x+dx;ty=owner.y+dy;missiles.clear();owner.dwFlags=0;int result=SKILLS_SrvDo008_MultipleShot_Teeth_ShockWave(&game,&owner,243,rank);
if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%u,[",dx,dy,rank,w,allocate,result,owner.dwFlags);bool next=false;
for(auto& m:missiles){if(next)std::printf(",");next=true;std::printf("[%d,%d,%u,%d,%d,%d,%d,%d]",m.nTargetX-owner.x,m.nTargetY-owner.y,m.dwFlags,m.nMissile,m.nActivateFrame,m.nSkill,m.nSkillLevel,m.pOwner==&owner && m.pOrigin==&owner);}std::printf("]]");}
std::printf("]\n");}
'''
    fans=execute('fan',fan)
    retained=json.loads((BASE/'fan-source-probe.json').read_text())['fanCases']
    vectors={(x[0],x[1]):x[7] for x in retained if x[2:5]==[5,0,0]}
    assert len(vectors)==289
    for dx,dy,rank,weapon,allocation,result,owner_flags,requests in fans:
        expected=[[r[0],r[1],2080,511,0,243,rank,1] for r in vectors[dx,dy]]
        assert (result,owner_flags,requests)==(1,64,expected)
    assert [len(damage[k]) for k in ['physicalCases','stunCases','dispatchCases']]==[3825,2048,216]
    assert len(fans)==3468
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,nativeAccepted=False,currentExecutableParity=False,
        preflightSha256=sha(pinpath),sourceHashes=hashes,completeRoutineHashes=bodies,inheritedScaffolds=inherited,artifacts=artifacts,
        verified=dict(physicalCapturesAndRolls=3825,stunCallbacks=2048,orderedCallbackFlagDispatch=216,sourceZeroCreatorAndWriter=6120,currentFiveWaveFans=3468,sanitizers='ASan/UBSan clean'),
        established=[
            'Current Shock Wave physical endpoints use whole-point hard-Maul synergy before HitShift8. The complete no-weapon getters and per-hit roll match all255ranks and three hard-point levels; changing the supplied owner synergy after capture does not alter the stored per-hit endpoint roll.',
            'Complete damage7 writes40+15*(rank-1)raw stun frames and hit class0x60 for the current dParam1=0. Positive dParam1, missing row and invalid rank are independent controls. This is raw length, not the eventual250-frame cap or target eligibility.',
            'Current missile ResultFlags4 enters flag dispatch already carrying GetHit. The uninterruptable check does not remove that preexisting flag; physical shield blocking clears success and suppresses the event-service call, while final missile presentation is requested in both branches. Callback stun length survives in the packet even on blocked hits; native stun must follow successful event dispatch.',
            'Current do8 makes five cursor-relative requests for missile511,all with flags2080,activation0,captured rank and owner/origin identity. Both weapon selectors agree because both rows select511. Failed allocation attempts still leave the do routine successful. Shared unchanged fan helpers match retained source vectors; no fixed cone is substituted.',
            'The complete missile creator/writer plus complete physical getters match6120current Shock Wave contexts/all255ranks. Owner elemental/weapon/drain noise and a separate noisy origin contribute nothing to a source-zero packet; critical collection is never requested. Physical endpoints are stored exactly. Two DAMAGE_VS_MONTYPE layers are copied from the owner independently of source damage,so this special item-stat branch cannot be silently omitted.',
        ],services=[
            'Inherited declared tables,formula evaluation,stat/inventory/component lookup and RNG services supply the current row,hard synergy and selected rolls. Complete extracted routines are compared byte-for-byte to the pinned tree before compilation.',
            'Fan allocation records requests; actual missile factory,path and world collision are outside that program. A separate creator/writer program executes complete damage capture with declared stat-layer copy/set services. Ordered dispatch records event and final presentation calls; it does not replace those services with an invented health or stun consumer.',
        ],remaining=[
            'Compose the actual missile factory,path and collision with the accepted source-zero physical writer,including its separately copied DAMAGE_VS_MONTYPE layers. Verify full damage/event/stun eligibility and caps,ReturnFire,LastCollide and modern same-skill NextHit scope before enabling learned casting.',
            'Bear SC/UseAttackRate timing,input,mana,cancellation,saved travelling waves,world geometry/owner transitions and current-executable behavior remain native acceptance work. Separate final directional wave/cast/contact/stun effects/audio and talent/left/right/quick icons are still missing.',
        ])
    path=ROOT/'tools/reference/druid-shock-wave-engine-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Shock Wave source:3825physical/2048stun/216dispatch/6120creator-writer/3468five-wave fan cases; native handler still pending.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');build(parser.parse_args().check)
