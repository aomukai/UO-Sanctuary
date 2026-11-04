#!/usr/bin/env python3
"""Reuse complete FillDamageValues to pin armor mutation and stun-fill phases."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-armor-setup-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    parent_path=ROOT/'tools/reference/monster-weapon-phases-source-evidence.json';parent=json.loads(parent_path.read_text())
    original=BASE/'monster-weapon-phases-source-probe.cpp';assert sha(original)==parent['generatedSourceSha256']
    relative='source/D2Game/src/UNIT/SUnitDmg.cpp';source=BASE/'d2moo-pinned-source'/relative;raw=source.read_bytes()
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==parent['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==relative)
    body=function(raw.decode(),'SUNITDMG_FillDamageValues');cpp=original.read_text().split('int main(){')[0];assert body in cpp
    def replace(before,after):
        nonlocal cpp
        assert cpp.count(before)==1,before;cpp=cpp.replace(before,after,1)
    replace('int STATLIST_GetUnitAlignment(D2UnitStrc*){return UNIT_ALIGNMENT_GOOD;}',
        'int alignment=UNIT_ALIGNMENT_GOOD,armorWrites=0,firstDrawArmor=-1;int*armorWatch=nullptr;int STATLIST_GetUnitAlignment(D2UnitStrc*){return alignment;}')
    # Observe the stat setter and RNG services, not a rewritten damage routine.
    declaration='int alignment=UNIT_ALIGNMENT_GOOD,armorWrites=0,firstDrawArmor=-1;int*armorWatch=nullptr;'
    cpp=cpp.replace(declaration,'',1)
    at=cpp.index('int ITEMS_RollLimitedRandomNumber(');cpp=cpp[:at]+declaration+'\n'+cpp[at:]
    replace('int ITEMS_RollLimitedRandomNumber(int*,int bound){bounds.push_back(bound);',
        'int ITEMS_RollLimitedRandomNumber(int*,int bound){if(armorWatch&&firstDrawArmor<0)firstDrawArmor=*armorWatch;bounds.push_back(bound);')
    replace('uint32_t ITEMS_RollRandomNumber(int*){bounds.push_back(-1);',
        'uint32_t ITEMS_RollRandomNumber(int*){if(armorWatch&&firstDrawArmor<0)firstDrawArmor=*armorWatch;bounds.push_back(-1);')
    replace('void STATLIST_SetUnitStat(D2UnitStrc* u,int stat,int value,int){u->stats[stat]=value;',
        'void STATLIST_SetUnitStat(D2UnitStrc* u,int stat,int value,int){if(stat==STAT_ARMORCLASS)armorWrites++;u->stats[stat]=value;')
    assert body in cpp
    cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int kind:{0,1})for(int targetKind:{0,1})for(int align:{0,1,2})for(int armor:{0,1,50,100,350})for(int delta:{-100,-50,-1,0,1,100})for(int src:{0,64,128,255})for(int initialStun:{0,1,17})for(int rawStun:{0,1,25,100}){
 Reset(416,1);mon={};victimHire=victimBoss=victimUnique=false;tapOn=deathDelay=false;alignment=align;armorWrites=0;firstDrawArmor=-1;
 attacker.dwUnitType=kind;defender.dwUnitType=targetKind;attacker.stats[STAT_MINDAMAGE]=1;attacker.stats[STAT_MAXDAMAGE]=3;
 attacker.stats[STAT_ITEM_DAMAGETARGETAC]=delta;attacker.stats[STAT_STUNLENGTH]=rawStun;defender.stats[STAT_ARMORCLASS]=armor;armorWatch=&defender.stats[STAT_ARMORCLASS];
 D2DamageStrc d={};d.wResultFlags=1;d.dwStunLen=initialStun;SUNITDMG_FillDamageValues(&game,&attacker,&defender,&d,0,src);
 printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d]]",first?"":",",kind,targetKind,align,armor,delta,src,initialStun,rawStun,defender.stats[STAT_ARMORCLASS],armorWrites,firstDrawArmor,d.dwStunLen);first=false;
}printf("]\n");}
'''
    generated=STAGE/'probe.cpp';generated.write_text(cpp);binary=STAGE/'probe'
    r=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(binary)],capture_output=True)
    compiler=STAGE/'compiler.log';compiler.write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stderr.decode()[-3000:]
    r=subprocess.run([str(binary)],capture_output=True);sanitizer=STAGE/'sanitizer.log';sanitizer.write_bytes(r.stderr);assert r.returncode==0 and not r.stderr,r.stderr.decode()[-3000:]
    rows=json.loads(r.stdout);assert len(rows)==17280
    for (kind,target,align,armor,delta,src,initial,stun),(after,writes,first,filled) in rows:
        changes=(kind==0 or align==2)and target==1 and delta!=0
        expected=max(armor+delta,0)if changes else armor
        assert (after,writes,first)==(expected,int(changes),expected)
        assert filled==(initial if initial else (128 if src==0 else src)*stun//128)
    cases=STAGE/'cases.json';cases.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    reference=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((reference/'sources.json').read_text())['files'];tables={};pins={}
    for name in ('properties','itemstatcost'):
        p=reference/'json'/(name+'.json');assert sha(p)==manifest['json/'+name+'.json']['sha256'];pins[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
    writer=tables['properties']['dmg-ac'];stat=tables['itemstatcost']['item_damagetargetac'];assert writer['func1']==1 and writer['stat1']=='item_damagetargetac'and not writer.get('stat2')and stat['*ID']==120 and not stat.get('itemevent1')and not stat.get('itemevent2')
    assert not any(r.get('stat'+str(i))=='stunlength'for r in tables['properties'].values()for i in range(1,8))
    result=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',legacyCommit=tree['sha'],cases=17280,
        inputs={str(source.relative_to(ROOT)):sha(source),str(parent_path.relative_to(ROOT)):sha(parent_path),str(original.relative_to(ROOT)):sha(original),**pins,str(Path(__file__).relative_to(ROOT)):sha(Path(__file__))},
        unchangedFillSha256=hashlib.sha256(body.encode()).hexdigest(),artifacts={str(p.relative_to(ROOT)):sha(p)for p in (generated,binary,compiler,sanitizer,cases)},
        currentWriter=dict(property='dmg-ac',function=1,stat='item_damagetargetac',id=120,registeredEvents=[]),currentStunPropertyWriters=[],
        established=['Complete unchanged FillDamageValues mutates target armor only for a player or good-aligned source against a monster. Nonzero signed delta adds before the zero floor,independently of source-damage scaling;the mutation precedes every damage RNG service.',
            'An already nonzero packet stun length remains unchanged. An empty length receives normalizedSrcDamage*raw66/128. No current item-property row writes raw66;that is not evidence that a supplied nonzero66is inert.',
            'Current dmg-ac writer1 supplies signed raw120 without an event registration. Runtime integration must use the allocation/setup phase and persistent base-defense consumer,not a release-time hit callback.'],
        limitations=['World/stat/percentage/critical/RNG services are supplied by the existing complete damage driver. Source alignment is an explicit service. This is not current-executable or full stat-list/actor/world equivalence.',
            'Positive armor deltas and raw stun66 remain explicit runtime gaps. The raw body is called directly with a successful supplied packet;miss/block/qualification behavior belongs to its separate caller and native controls.'])
    path=ROOT/'tools/reference/npc-armor-setup-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('NPC armor setup:17280complete FillDamageValues contexts;good/player source,pre-RNG armor mutation and stun-fill boundaries pinned;current120writer/no66writers verified.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
