#!/usr/bin/env python3
"""Execute the unchanged reference PMH guard; pin the current item writer."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'npc-prevent-heal-source'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    relative='source/D2Game/src/UNIT/SUnitDmg.cpp'
    source=BASE/'d2moo-pinned-source'/relative;raw=source.read_bytes()
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==relative)
    body=function(raw.decode(),'SUNITDMG_PreventMonsterHeal')
    caller=function(raw.decode(),'SUNITDMG_IsHitSuccessful')
    ids=BASE/'d2moo-pinned-source/source/D2Common/include/DataTbls/MonsterIds.h'
    id_raw=ids.read_bytes();assert hashlib.sha1(b'blob '+str(len(id_raw)).encode()+b'\0'+id_raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']=='source/D2Common/include/DataTbls/MonsterIds.h')
    assert 'SUNITDMG_PreventMonsterHeal(pAttacker, pDefender);'in caller
    reference=ROOT/'tools/reference/d2r-named-equipment'
    manifest=json.loads((reference/'sources.json').read_text())['files'];pins={};tables={}
    for name in ('properties','itemstatcost'):
        p=reference/'json'/(name+'.json');assert sha(p)==manifest['json/'+name+'.json']['sha256']
        pins[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
    writer=tables['properties']['noheal'];stat=tables['itemstatcost']['item_preventheal']
    assert writer['func1']==1 and writer['stat1']=='item_preventheal'and not writer.get('stat2')
    assert stat['*ID']==117 and not stat.get('itemevent1')and not stat.get('itemevent2')
    cpp=r'''#include <cassert>
#include <cstdint>
#include <cstdio>
#define __fastcall
constexpr int UNIT_PLAYER=0,UNIT_MONSTER=1,STAT_ITEM_PREVENTHEAL=117,STAT_ARMORCLASS=31,STATE_PREVENTHEAL=52;
struct D2UnitStrc{int dwUnitType=0,dwClassId=0,stat=0;};
struct D2CurseStrc{D2UnitStrc*pUnit=nullptr,*pTarget=nullptr;int nSkill=0,nSkillLevel=0,nDuration=0,nStat=0,nStatValue=0,nState=0;};
int reads=0,applications=0,duration=0;
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*p,int stat,int layer){assert(stat==117&&layer==0);reads++;return p->stat;}
void sub_6FD10EC0(D2CurseStrc*p){assert(p->pUnit&&p->pTarget&&p->nSkill==0&&p->nSkillLevel==1&&p->nStat==31&&p->nStatValue==0&&p->nState==52);applications++;duration=p->nDuration;}
'''+body+r'''
int main(){bool first=true;
#ifdef D2_VERSION_HAS_UBERS
printf("{\"firstUber\":%d,\"lastUber\":%d,\"cases\":[",MONSTER_FIRST_UBER,MONSTER_LAST_UBER);
#else
printf("{\"firstUber\":0,\"lastUber\":0,\"cases\":[");
#endif
for(int a=0;a<6;a++)for(int d=0;d<6;d++)for(int cls:{0,1,703,704,705,706,707,708,709,710,711})for(int value:{-1,0,1,2,255}){
 D2UnitStrc source{a,0,value},target{d,cls,0};reads=applications=duration=0;SUNITDMG_PreventMonsterHeal(&source,&target);
 printf("%s[%d,%d,%d,%d,%d,%d,%d]",first?"":",",a,d,cls,value,reads,applications,duration);first=false;
}printf("]}\n");}
'''
    # initializer_list is needed only by this standalone driver.
    cpp='#include <initializer_list>\n#include '+json.dumps(str(ids))+'\n'+cpp
    generated=STAGE/'probe.cpp';generated.write_text(cpp);artifacts={}
    for ubers in (False,True):
        name='ubers'if ubers else 'classic';binary=STAGE/name
        flags=['-DD2_VERSION_HAS_UBERS']if ubers else []
        r=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',*flags,str(generated),'-o',str(binary)],capture_output=True)
        log=STAGE/(name+'-compiler.log');log.write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stderr.decode()[-3000:]
        r=subprocess.run([str(binary)],capture_output=True);err=STAGE/(name+'-sanitizer.log');err.write_bytes(r.stderr)
        assert r.returncode==0 and not r.stderr,r.stderr.decode()[-3000:]
        data=json.loads(r.stdout);rows=data['cases'];assert len(rows)==1980
        if ubers:assert data['lastUber']-data['firstUber']==5
        for a,d,cls,value,reads,applications,duration in rows:
            assert reads==int(a==0 and d==1)
            expected=a==0 and d==1 and value!=0 and not(ubers and data['firstUber']<=cls<=data['lastUber'])
            assert applications==int(expected)and duration==(120000 if expected else 0)
        out=STAGE/(name+'-cases.json');out.write_text(json.dumps(rows,separators=(',',':'))+'\n')
        for p in (binary,log,err,out):artifacts[str(p.relative_to(ROOT))]=sha(p)
    result=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',legacyCommit=tree['sha'],cases=3960,
        inputs={str(source.relative_to(ROOT)):sha(source),str(ids.relative_to(ROOT)):sha(ids),**pins,str(Path(__file__).relative_to(ROOT)):sha(Path(__file__))},
        executed={'SUNITDMG_PreventMonsterHeal':hashlib.sha256(body.encode()).hexdigest()},
        reviewed={'SUNITDMG_IsHitSuccessful':hashlib.sha256(caller.encode()).hexdigest()},
        currentWriter=dict(property='noheal',function=1,stat='item_preventheal',id=117,registeredEvents=[]),
        artifacts={str(generated.relative_to(ROOT)):sha(generated),**artifacts},
        established=['Both source variants exclude every NPC attacker before reading the item stat or creating a state. Player-versus-monster controls apply120000frames and the conditional Uber exclusion;all other unit-type pairs remain inert.',
            'Pinned current noheal writes raw stat117 through function1 and registers no ItemStatCost event. NPC admission must retain the captured stat without creating a callback,player attribution or Prevent Healing state.'],
        limitations=['This executes the complete older helper with declared stat lookup and curse sink,not the current executable or full hit/event/curse engine. Current NPC item PMH remains a source-based interpretation;no player implementation or owner-forwarded pet proc is introduced.'])
    path=ROOT/'tools/reference/npc-prevent-heal-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('NPC Prevent Monster Heal:3960complete unchanged helper controls;current noheal/stat117 writer pinned;NPC path inert before stat access.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
