#!/usr/bin/env python3
"""Execute the shared complete Shadow/Valkyrie equipment selector, not item generation."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-equipment-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(exist_ok=True);tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    p=BASE/'d2moo-pinned-source/source/D2Game/src/SKILLS/SkillAss.cpp';raw=p.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']=='source/D2Game/src/SKILLS/SkillAss.cpp')
    body=function(raw.decode(),'sub_6FCF9580');prior=json.loads((ROOT/'tools/reference/valkyrie-equipment-engine-evidence.json').read_text())
    assert hashlib.sha256(body.encode()).hexdigest()==prior['blockHashes']['sub_6FCF9580']
    pin=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((pin/'sources.json').read_text())['files'];inputs={str(p.relative_to(ROOT)):sha(p)};tables={}
    for name in ('monequip','skills'):
        p=pin/'json'/(name+'.json');assert sha(p)==manifest['json/'+name+'.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
    loc={'head':1,'neck':2,'tors':3,'rarm':4,'larm':5,'rrin':6,'lrin':7,'belt':8,'feet':9,'glov':10}
    profiles=[]
    for name,id in [('shadowwarrior',268),('shadowmaster',279)]:
        rows=[r for r in tables['monequip'].values() if r.get('monster')==name]
        assert len(rows)==(4 if id==268 else 19)
        profiles.append(dict(Ability='ShadowWarrior' if id==268 else 'ShadowMaster',Rows=[dict(Rank=r['level'],Choices=[dict(Slot=loc[r['loc'+str(i)]],Code=r.get('item'+str(i),''),Quality=r.get('mod'+str(i),0)) for i in range(1,4) if r.get('loc'+str(i))]) for r in rows]))
    runtime=ROOT/'data/Sanctuary/Balance/shadow-equipment.json';encoded=json.dumps(profiles,indent=2)+'\n'
    if check:assert runtime.read_text()==encoded,'Shadow equipment rows stale'
    else:runtime.write_text(encoded)
    test_profiles=profiles+[dict(Ability='SyntheticNonmonotone',Rows=[dict(Rank=0,Choices=[dict(Slot=4,Code='ces',Quality=8)]),dict(Rank=99,Choices=[dict(Slot=3,Code='',Quality=4)])])]
    cpp=r'''
#include <cstdint>
#include <cstdio>
#include <initializer_list>
#include <vector>
#define __fastcall
constexpr int UNIT_MONSTER=1;struct D2UnitStrc;struct Inventory{D2UnitStrc*items[11]={};};
struct D2UnitStrc{int dwUnitType=1,dwClassId=418,pSeed=0,code=0;Inventory*pInventory=nullptr;}owner,pet,items[11];Inventory ownInv,petInv;
struct D2GameStrc{}game;struct D2MonStatsTxt{int nMonEquipTxtRecordId=0;}mon;
struct D2MonEquipTxt{int nMonster=0,nOnInit=0,wLevel=0,nLoc[3]={},nMod[3]={},dwItem[3]={};};
struct Tables{D2MonEquipTxt pMonEquipTxt[30];}tables;auto*sgptDataTables=&tables;
struct Unk{int nItemCode=0,nBodyLoc=0,unk0x05=0;};struct Request{int slot,code,quality,level;};std::vector<Request>requests;std::vector<int>draws;
int choice=0,fail=0;D2MonStatsTxt* MONSTERMODE_GetMonStatsTxtRecord(int){return &mon;}
unsigned ITEMS_RollLimitedRandomNumber(int*,int bound){draws.push_back(bound);return bound?choice%bound:0;}
D2UnitStrc* INVENTORY_GetItemFromBodyLoc(Inventory*p,int slot){return p?p->items[slot]:nullptr;}
int ITEMS_GetBaseCode(D2UnitStrc*p){return p->code;}
void sub_6FC61190(D2GameStrc*,D2UnitStrc*p,Unk*r,int level,int quality){requests.push_back({r->nBodyLoc,r->nItemCode,quality,level});if(fail==1||fail==2&&quality==6)return;p->pInventory->items[r->nBodyLoc]=&items[r->nBodyLoc];}
'''+body+'\n'
    code=lambda s:int.from_bytes(s.ljust(4).encode(),'little')
    cpp+='void load(int profile){for(auto&r:tables.pMonEquipTxt)r={};\n'
    for kind,profile in enumerate(test_profiles):
        cpp+='if(profile=='+str(kind)+'){\n'
        for i,r in enumerate(profile['Rows']):
            choices=r['Choices'];fields=[str(418+kind),'0',str(r['Rank'])]
            for key,convert in [('Slot',int),('Quality',int),('Code',code)]:fields.append('{'+','.join(str(convert(c[key])) for c in choices)+'}')
            cpp+='tables.pMonEquipTxt['+str(i)+']={'+','.join(fields)+'};\n'
        cpp+='}\n'
    cpp+=r'''}
int main(){bool comma=false;printf("[");for(int profile:{0,1,2})for(int rank:{0,1,5,6,9,11,13,17,20,40,255})for(int pick:{0,1,2})for(int mode:{0,1,2,3})for(int mask:{0,16,10,2046})for(int failure:{0,1,2}){
 load(profile);owner={};pet={};ownInv={};petInv={};owner.pInventory=mode==1?nullptr:&ownInv;pet.pInventory=&petInv;pet.dwClassId=418+profile;requests.clear();draws.clear();choice=pick;fail=failure;
 for(int i=1;i<=10;i++)if(mask&(1<<i))petInv.items[i]=&items[i];items[1].code=0x20706163;items[3].code=0x20696c71;ownInv.items[1]=&items[1];ownInv.items[3]=&items[3];if(mode==2)ownInv={};
 int level=profile==2?37:rank<=0?1:((profile==0?18:24)+(rank-1)*(profile==0?2:3));if(level<1)level=1;if(level>99)level=99;
 sub_6FCF9580(&game,mode==0?nullptr:&owner,&pet,268,rank,level,0);
 if(comma)printf(",");comma=true;printf("[[%d,%d,%d,%d,%d,%d,%d],[",profile,rank,pick,mode,mask,failure,level);
 for(size_t i=0;i<requests.size();i++){auto r=requests[i];printf("%s[%d,%d,%d,%d]",i?",":"",r.slot,r.code,r.quality,r.level);}printf("],[");for(size_t i=0;i<draws.size();i++)printf("%s%d",i?",":"",draws[i]);printf("]]");
}printf("]\n");}
'''
    # Literal owner codes are verified below against the same little-endian codec.
    cpp=cpp.replace('0x20696c71',str(code('qui')))
    generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
    with (STAGE/'compiler.log').open('w') as out:subprocess.run(['g++','-std=c++20','-Wno-multichar','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=out,stderr=subprocess.STDOUT,check=True)
    run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
    data=json.loads(run.stdout);assert len(data)==4752
    for args,actual,draws in data:
        profile,rank,pick,mode,mask,failure,level=args;rows=test_profiles[profile]['Rows'];start=next((i for i,r in enumerate(rows) if r['Rank']<=rank),len(rows));filled={i for i in range(1,11) if mask&(1<<i)};expected=[];bounds=[]
        for r in rows[start:]:
            choices=r['Choices'];bounds.append(len(choices));c=choices[pick%len(choices)];slot=c['Slot'];base=c['Code'];quality=c['Quality'] if 0<=c['Quality']<8 else 0
            if slot in filled:continue
            if not base and mode!=0:
                if mode!=3:continue
                base={1:'cap',3:'qui'}.get(slot)
                if base is None:continue
            expected.append([slot,code(base),quality,level])
            if failure!=1 and not(failure==2 and quality==6):filled.add(slot)
        assert actual==expected and draws==bounds,(args,actual,expected,draws,bounds)
    cases=STAGE/'cases.json';cases.write_text(json.dumps(data,separators=(',',':'))+'\n')
    e=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],handlerEnabled=False,currentExecutableCodeParity=False,cases=4752,actualProfiles=2,syntheticProfiles=1,
        compiledRoutine='sub_6FCF9580',routineSha256=hashlib.sha256(body.encode()).hexdigest(),inputs=inputs,runtimePath=str(runtime.relative_to(ROOT)),runtimeSha256=sha(runtime),
        sanitizers='ASan+UBSan clean',artifacts={str(q.relative_to(ROOT)):sha(q) for q in (generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',cases,Path(__file__).resolve())},
        established=['Shared full Shadow/Valk selector executes current4Warrior/19Master rows,three candidate draws,occupied slots,owner-absent/inventory-absent/empty/base-copy contexts,and creation failures with lower-row retry.',
                     'Initial high thresholds are skipped once;subsequent rows are processed literally. Synthetic nonmonotone input proves no per-row level filter. Invalid quality8normalizes0. Every visited row draws even for singleton/occupied slots;blank owner-base copies only the code.',
                     'Requested quality3/4/6 is retained without applying player supported-property filters. Current skillParam5/6supplies item-level18+2*(rank-1) or24+3*(rank-1),with do49cap1..99;that caller arithmetic is table/static evidence,not execution of full do49.'],
        services=['Bounded draw returns supplied pick modulo bound,not a fresh RNG proof. Existing SourceRandom full controls remain the production supplier. Creation is an observed request/equip-success seam,not a generated item/stat/property/event engine.',
                  'Native owner inventories and NPC equipment/body/AI/persistence,do49life/stat-list indexing,current extended passives and current executable code remain separate. No Shadow learned cast or live actor is enabled.'])
    out=ROOT/'tools/reference/shadow-equipment-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Shadow equipment source evidence stale'
    else:out.write_text(encoded)
    print('Shadow equipment:4752 complete shared-selector cases,23current rows,owner-base/multichoice/occupied/failure/quality/order controls;ASan+UBSan clean;item generation/actors not certified.')
if __name__=='__main__':
    a=argparse.ArgumentParser();a.add_argument('--check',action='store_true');build(a.parse_args().check)
