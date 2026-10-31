#!/usr/bin/env python3
"""Compose Fire Golem's death callbacks with complete retained-owner missile damage."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/fire-golem-death-source'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def runtime_api():
    folder=STAGE/'api';folder.mkdir(exist_ok=True)
    project=folder/'Api.csproj';program=folder/'Program.cs'
    project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>\n')
    program.write_text('''using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
var output=new List<object>();
foreach(var path in args){using var stream=File.OpenRead(path);using var pe=new PEReader(stream);var md=pe.GetMetadataReader();
foreach(var handle in md.TypeDefinitions){var type=md.GetTypeDefinition(handle);if(md.GetString(type.Namespace)!="Server"||md.GetString(type.Name)!="Mobile")continue;
foreach(var methodHandle in type.GetMethods()){var method=md.GetMethodDefinition(methodHandle);var name=md.GetString(method.Name);if(name is not("get_Alive" or "Delete" or "OnDeath" or "OnBeforeDeath" or "Kill"))continue;
output.Add(new{assembly=Path.GetFileName(path),type="Server.Mobile",name,virtualMethod=(method.Attributes&MethodAttributes.Virtual)!=0,finalMethod=(method.Attributes&MethodAttributes.Final)!=0,publicMethod=(method.Attributes&MethodAttributes.MemberAccessMask)==MethodAttributes.Public});}}}
Console.WriteLine(JsonSerializer.Serialize(output));
''')
    native=ROOT/'.local/class-skills/fire-golem-aura/native/Distribution'
    evidence=ROOT/'tools/reference/fire-golem-aura-native-evidence.json'
    frozen=json.loads(evidence.read_text())['inputInventories']['birth']
    assert sha(ROOT/frozen['path'])==frozen['sha256']
    inputs=json.loads((ROOT/frozen['path']).read_text())
    assemblies=[native/name for name in ['ModernUO.dll','Server.dll']]
    for path in assemblies:assert sha(path)==inputs['artifacts'][path.name]
    run=subprocess.run([str(ROOT/'.tools/dotnet/dotnet'),'run','--project',str(project),'--',*[str(path) for path in assemblies]],capture_output=True)
    (STAGE/'native-api-stderr.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
    rows=json.loads(run.stdout)
    assert len(rows)==5 and {r['name'] for r in rows}=={'get_Alive','Delete','Kill','OnDeath','OnBeforeDeath'}
    assert all(r['type']=='Server.Mobile' and r['virtualMethod'] and not r['finalMethod'] and r['publicMethod'] for r in rows)
    path=STAGE/'native-api.json';path.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    return dict(scope='Read-only PE metadata from the tested frozen kernel; no server assembly loaded or changed.',
                testedEvidence=dict(path=str(evidence.relative_to(ROOT)),sha256=sha(evidence)),
                assemblies={str(path.relative_to(ROOT)):sha(path) for path in assemblies},methods=rows),[project,program,path,STAGE/'native-api-stderr.log']


def build(cache,check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    tree=json.loads((cache/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes={};routines={}
    def read(relative):
        raw=(cache/'d2moo-pinned-source'/relative).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=hashlib.sha256(raw).hexdigest();return raw.decode()
    unique=read('source/D2Game/src/MONSTER/MonsterUnique.cpp')
    damage=read('source/D2Game/src/UNIT/SUnitDmg.cpp')
    mode=read('source/D2Game/src/MONSTER/MonsterMode.cpp')
    header=read('source/D2Common/include/D2Monsters.h')
    collision=read('source/D2Common/include/D2Collision.h')
    masks={name:int(value,16) for name,value in re.findall(r'\b(COLLIDE_(?:DOOR|MISSILE_BARRIER|WALL))\s*=\s*(0x[0-9A-Fa-f]+)',collision)}
    assert masks['COLLIDE_DOOR']|masks['COLLIDE_MISSILE_BARRIER']|masks['COLLIDE_WALL']==0x805
    events=read('source/D2Game/include/GAME/Event.h');assert re.search(r'\bEVENTTYPE_MONUMOD\s*=\s*7\s*,',events)
    enum=header.split('enum D2C_MonUMods',1)[1].split('};',1)[0]
    modifiers=re.findall(r'^\s*(MONUMOD_\w+)\s*,',enum,re.M)
    modifier=modifiers.index('MONUMOD_CORPSEBOOMDEATH');assert modifier==31
    dispatch=function(unique,'sub_6FC6E780')
    table=dispatch.split('stru_6FD2F0C8[43]',1)[1].split('};',1)[0]
    entries=[re.findall(r'\w+',line) for line in table.splitlines() if '{' in line and 'nullptr' in line]
    assert len(entries)==43 and entries[modifier][1:3]==['MONSTERUNIQUE_ColdEnchantedModeChange','MONSTERUNIQUE_CastCorpseExplode2']
    spawn=read('source/D2Game/src/MONSTER/MonsterSpawn.cpp')
    assert 'case MONSTER_FIREGOLEM:\n        D2GAME_BOSSES_AssignUMod_6FC6FF10(pGame, pUnit, MONUMOD_CORPSEBOOMDEATH, 0);' in spawn
    pets=read('source/D2Game/src/PLAYER/PlayerPets.cpp')
    replacement=function(pets,'sub_6FC7CF50')
    assert 'SUNITDMG_KillMonster(pGame, pPet, nullptr, 0);' in replacement
    assert 'MONSTERMODE_GetModeChangeInfo(pPet, MONMODE_DEATH, &pModeChange);' in replacement

    reference=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((reference/'sources.json').read_text())
    current={};pins={}
    for name,key in [('monstats','firegolem'),('missiles','monstercorpseexplode'),('skills','94')]:
        path=reference/'json'/(name+'.json');assert sha(path)==manifest['files']['json/'+name+'.json']['sha256']
        current[name]=json.loads(path.read_text())[key];pins[name]=sha(path)
    assert current['monstats']['Crit']==5 and current['monstats']['*hcIdx']==292
    missile=current['missiles'];assert missile['*ID']==117 and missile['pSrvDoFunc']==1
    assert not any(missile.get(k) for k in ['GetHit','SoftHit','KnockBack','pSrvDmgFunc'])
    assert [int(current['skills'][key]) for key in ['calc2','calc3','calc4']]==[100,100,6]

    prior=json.loads((ROOT/'tools/reference/sentry-dead-damage-source-evidence.json').read_text())
    parent=cache/'sentry-dead-damage-source-probe.cpp';cpp=parent.read_text().split('int main()',1)[0]
    # Reuse the existing full damage/owner ABI and verify every reused routine.
    for relative,names in [
        ('source/D2Game/src/UNIT/SUnit.cpp',['SUNIT_GetServerUnit','SUNIT_GetOwner','SUNIT_IsDead','sub_6FCBD900']),
        ('source/D2Game/src/AI/AiGeneral.cpp',['AIGENERAL_GetMinionOwner']),
        ('source/D2Game/src/MISSILES/MissMode.cpp',['MISSMODE_SetDamageFlags']),
        ('source/D2Game/src/UNIT/SUnitDmg.cpp',['SUNITDMG_GetMonStatsTxtRecordFromUnit','SUNITDMG_ApplyResistancesAndAbsorb','SUNITDMG_CalculateTotalDamage','SUNITDMG_ExecuteEvents','SUNITDMG_ExecuteMissileDamage']),
    ]:
        text=read(relative)
        for name in names:
            body=function(text,name);assert cpp.count(body)==1,name
            digest=hashlib.sha256(body.encode()).hexdigest();routines[name]=digest
            if name in prior['compiledRoutineHashes']:assert digest==prior['compiledRoutineHashes'][name]
    def replace(old,new):
        nonlocal cpp
        assert cpp.count(old)==1,old;cpp=cpp.replace(old,new)
    replace('#include <cassert>','#include <cassert>\n#include <cstring>\n#include <cstdlib>')
    replace('int nVelocity=0,','int nCrit=5,nVelocity=0,')
    replace('missile==1&&physical==0','missile==1&&physical==1')
    replace('unsigned ITEMS_RollRandomNumber(int*){assert(false);return 0;}','unsigned criticalRoll=99;int criticalDraws=0;unsigned ITEMS_RollRandomNumber(int*){criticalDraws++;return criticalRoll;}')
    critical=function(mode,'D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70')
    hitclass=function(damage,'SUNITDMG_SetHitClass')
    replacement_critical='unsigned ITEMS_RollRandomNumber(int*);\n'+hitclass+'\nvoid UNITS_SetOverlay(D2UnitStrc*,int,int){}\n'+critical
    replace('void D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70(D2UnitStrc* a,D2UnitStrc*,D2DamageStrc*){assert(a==&actor);}',replacement_critical)
    routines['D2GAME_MONSTER_ApplyCriticalDamage_6FC62E70']=hashlib.sha256(critical.encode()).hexdigest()
    routines['SUNITDMG_SetHitClass']=hashlib.sha256(hitclass.encode()).hexdigest()
    cpp+=r'''
constexpr int MISSILE_MONSTERCORPSEEXPLODE=117,EVENTTYPE_MONUMOD=7,COLLIDE_MASK_RADIAL_BARRIER=0x805;
struct D2UnitFindArgStrc{int nFlags=0,nSize=0,nX=0,nY=0;};
struct D2UnitFindDataStrc{int nIndex=0;D2UnitStrc**pUnitsArray=nullptr;};
D2UnitStrc*found[3]={&actor,&master,&target};int allocation=1,allocations=0,barrier=1,barrierCalls=0,findFlags=0,findSize=0,frees=0,scheduled=-1;
int CLIENTS_GetUnitX(D2UnitStrc*p){assert(p==&actor);return 100;}
int CLIENTS_GetUnitY(D2UnitStrc*p){assert(p==&actor);return 200;}
D2UnitStrc*sub_6FD11420(D2GameStrc*,int id,D2UnitStrc*p,int zero,int one,int z2,int z3,int x,int y,int end){assert(id==117&&p==&actor&&zero==0&&one==1&&z2==0&&z3==0&&x==100&&y==200&&end==1);allocations++;return allocation?&missile:nullptr;}
D2ActiveRoomStrc*D2GAME_GetRoom_6FC52070(D2ActiveRoomStrc*r,int x,int y){assert(x==100&&y==200);return r;}
void UNITFINDS_InitializeUnitFindData(void*,D2UnitFindDataStrc*d,D2ActiveRoomStrc*,int x,int y,int size,void*,D2UnitFindArgStrc*a){assert(x==100&&y==200&&size==6);findFlags=a->nFlags;findSize=a->nSize;d->nIndex=3;d->pUnitsArray=found;}
void UNITFINDS_FindAllMatchingUnitsInNeighboredRooms(D2UnitFindDataStrc*){}
void UNITFINDS_FreeUnitFindData(D2UnitFindDataStrc*){frees++;}
bool D2Common_11026(int x,int y,D2UnitStrc*p,int mask){assert(x==100&&y==200&&p==&target&&mask==COLLIDE_MASK_RADIAL_BARRIER);barrierCalls++;return barrier;}
using FARPROC=void(*)();bool IsBadCodePtr(FARPROC){return false;}
void FOG_DisplayAssert(const char*,const char*,int){assert(false);}
void EVENT_SetEvent(D2GameStrc*,D2UnitStrc*p,int event,int frame,int a,int b){assert(p==&actor&&event==EVENTTYPE_MONUMOD&&a==0&&b==0);scheduled=frame;}
'''
    for text,name in [(damage,'SUNITDMG_SetMissileDamageFlagsForNearbyUnits'),(unique,'MONSTERUNIQUE_CastCorpseExplode2'),(unique,'MONSTERUNIQUE_ColdEnchantedModeChange')]:
        body=function(text,name);routines[name]=hashlib.sha256(body.encode()).hexdigest();cpp+=body+'\n'
    cpp+=r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int am:{1,0,12})for(int pm:{1,0,17})for(int create:{0,1})for(int present:{0,1})for(int aligned:{0,1})for(int wall:{0,1})for(int crit:{0,99})for(int block:{0,1})for(int physical:{0,50,100})for(int fire:{0,50,100}){
game={};actor={};master={};missile={};target={};row={};D2AiControlStrc control;control.pGame=&game;actor.ai=&control;
actor.dwClassId=292;actor.dwAnimMode=am;actor.pGame=master.pGame=missile.pGame=target.pGame=&game;
actor.dwFlagEx=UNITFLAGEX_STOREOWNERINFO;actor.dwOwnerType=0;actor.dwOwnerGUID=1;
master.dwUnitType=0;master.dwUnitId=1;master.dwAnimMode=pm;game.pUnitList[0][1]=&master;if(present)game.pUnitList[1][77]=&actor;
target.dwUnitId=99;target.group=aligned?1:2;target.stats[STAT_HITPOINTS]=25600;target.stats[STAT_DAMAGERESIST]=physical;target.stats[STAT_FIRERESIST]=fire;
missile.dwUnitType=3;missile.dwClassId=117;missile.dwFlagEx=UNITFLAGEX_STOREOWNERINFO;row.dwMonStatsFlags=gdwBitMasks[MONSTATSFLAGINDEX_KILLABLE];missileRow={};
allocation=create;barrier=wall;criticalRoll=crit;blocked=block;allocations=barrierCalls=criticalDraws=findFlags=findSize=frees=kills=callbackCount=0;scheduled=-1;lastAttacker=nullptr;
MONSTERUNIQUE_ColdEnchantedModeChange(&game,&actor,31,0);
MONSTERUNIQUE_CastCorpseExplode2(&game,&actor,31,0);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",am,pm,create,present,aligned,wall,crit,block,physical,fire,target.stats[STAT_HITPOINTS],allocations,barrierCalls,criticalDraws,findFlags,findSize,frees,scheduled,kills,callbackCount,lastAttacker==&actor);first=false;
}printf("]\n");}
'''
    path=STAGE/'probe.cpp';path.write_text(cpp)
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(path),'-o',str(STAGE/'probe')],capture_output=True)
    (STAGE/'compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()
    run=subprocess.run([str(STAGE/'probe')],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
    cases=json.loads(run.stdout);assert len(cases)==5184
    for params,result in cases:
        am,pm,create,present,aligned,wall,crit,block,physical,fire=params
        eligible=bool(create and present and not aligned and wall)
        raw=(100-physical+100-fire)*256*(2 if crit==0 else 1) if eligible and not block else 0
        expected_life=max(0,25600-raw)
        assert result[0]==expected_life,(params,result,expected_life)
        assert result[1:8]==[1,int(create and present and not aligned),int(eligible),0x583 if create else 0,6 if create else 0,int(create),1004 if am==0 else -1],(params,result)
        assert result[8]==int(expected_life==0),(params,result)
        assert result[10]==int(eligible and not block),(params,result)
    fixture=STAGE/'cases.json';fixture.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    api,api_files=runtime_api()
    proof=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],runtimeChanged=False,probeSha256=sha(Path(__file__)),cases=len(cases),modifier=modifier,nativeApi=api,
        sourceHashes=hashes,unchangedRoutineHashes=routines,dispatchSha256=hashlib.sha256(dispatch.encode()).hexdigest(),reviewedReplacementSha256=hashlib.sha256(replacement.encode()).hexdigest(),
        reusedFixtureSha256=sha(parent),pinnedTables=pins,pinnedMissile=missile,
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [path,fixture,STAGE/'compiler.log',STAGE/'sanitizer.log',*api_files]},
        established=['Fire spawn assigns modifier31. Its dispatch row maps death mode to the complete four-frame scheduler and callback2to the complete fixed corpse-explosion routine. Dispatch/spawn/replacement links are source-inspected;the callbacks themselves execute.',
            'Complete corpse callback,radial wrapper,typed owner/allegiance lookup,missile result flags,intrinsic critical,resistance/total/events/HP and missile death dispatcher compose5184retained-registry contexts. Packet100physical+100fire,radius6,flags0x583;source and summoner are excluded. Empty event subscribers and controlled room/find/barrier/allocation/block/RNG services remain explicit.',
            'Missile117has no get-hit,soft-hit or knockback flag. Declared blocking suppresses damage;intrinsic5percent critical still consumes a draw and doubles both channels. Aligned targets,barrier failures,allocation failure and missing source registration suppress damage. Retained actor/master death modes alone do not suppress it.'],
        remaining=['Runtime delayed death/replacement emission is not changed. UO deletes a dead NPC and clears ownership immediately;D2source damage requires a registered owner. The retained source registry/stat lifetime is a declared service,not established by this probe. Do not invent an immortal stand-in or substitute the player as damage source.',
            'Replacement source chooses kill/death-mode entry for an undeleted pet;full modern death entry,modifier installation/dispatch scheduling,stat cleanup,corpse/GUID retention and final removal remain unverified.',
            'Current executable parity,real room/filter/radial barrier geometry/enumeration,arbitrary reactions/absorption/leech and client/effects remain separate. The direct callback is also exercised in nondeath modes as a raw control;that does not make those mode requests reachable.',
            'Tested kernel metadata confirms Alive/Delete/Kill/OnDeath/OnBeforeDeath are virtual and non-final. A bounded retirement of the original actor is implementable without replacing its identity;normal death bookkeeping,roster eligibility,save/reload and final deletion still need native implementation and verification.'])
    destination=ROOT/'tools/reference/fire-golem-death-source-evidence.json';rendered=json.dumps(proof,indent=2)+'\n'
    if check:assert destination.read_text()==rendered
    else:destination.write_text(rendered)
    print('Fire death source:5184complete retained-owner explosion contexts pass ASan+UBSan;native corpse/source lifetime remains open.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--cache-root',type=Path,required=True);p.add_argument('--check',action='store_true');args=p.parse_args();build(args.cache_root,args.check)
