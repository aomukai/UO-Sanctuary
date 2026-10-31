#!/usr/bin/env python3
"""Verify ordinary NPC drain fields against complete older FillDamageValues and current item writers."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def build(check=False):
    reference=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((reference/'sources.json').read_text());tables={};pins={}
    for name in ['properties','itemstatcost']:
        p=reference/'json'/(name+'.json');assert sha(p)==manifest['files']['json/'+name+'.json']['sha256'];pins[name]=sha(p);tables[name]=json.loads(p.read_text())
    rules={}
    for code,stat in [('lifesteal','lifedrainmindam'),('manasteal','manadrainmindam'),('stamdrain','item_staminadrainpct')]:
        row=tables['properties'][code];assert row['func1']==1 and row['stat1']==stat and not row.get('stat2')
        rules[code]=dict(function=1,stat=stat,id=tables['itemstatcost'][stat]['*ID'])
    maxima={'lifedrainmaxdam','manadrainmaxdam','stamdrainmaxdam'}
    assert not any(row.get('stat'+str(i)) in maxima for row in tables['properties'].values() for i in range(1,8))
    proof_path=ROOT/'tools/reference/monster-weapon-phases-source-evidence.json';proof=json.loads(proof_path.read_text());parent=BASE/'monster-weapon-phases-source-probe.cpp'
    assert sha(parent)==proof['generatedSourceSha256'];cpp=parent.read_text().split('int main(){')[0]
    relative='source/D2Game/src/UNIT/SUnitDmg.cpp';source=BASE/'d2moo-pinned-source'/relative;tree=json.loads((BASE/'d2moo-source-tree.json').read_text());raw=source.read_bytes()
    assert tree['sha']==proof['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
    names=['SUNITDMG_FillDamageValues','SUNITDMG_RollDamageValueInRange','SUNITDMG_CalculateTotalDamage'];hashes={}
    for name in names:
        body=function(raw.decode(),name);assert body in cpp;hashes[name]=hashlib.sha256(body.encode()).hexdigest()
    cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int scenario=0;scenario<6;scenario++)for(int amount:{0,1,3,6,15,100})for(int src:{64,128})for(int initial:{0,17}){
Reset(416,1);mon={};victimHire=victimBoss=victimUnique=false;tapOn=deathDelay=false;attacker.stats[STAT_MINDAMAGE]=1;attacker.stats[STAT_MAXDAMAGE]=2;
int minimum=scenario<2?STAT_LIFEDRAINMINDAM:scenario<4?STAT_MANADRAINMINDAM:STAT_STAMDRAINMINDAM;
int maximum=scenario<2?STAT_LIFEDRAINMAXDAM:scenario<4?STAT_MANADRAINMAXDAM:STAT_STAMDRAINMAXDAM;
// Scenario4is current stamina-consumption stat154,not an attack drain.
if(scenario!=4)attacker.stats[minimum]=amount;else attacker.stats[1201]=amount;
if(scenario&1)attacker.stats[maximum]=amount;
D2DamageStrc d={};d.wResultFlags=1;if(scenario<2)d.dwLifeLeech=initial;else if(scenario<4)d.dwManaLeech=initial;else d.dwStamLeech=initial;
SUNITDMG_FillDamageValues(&game,&attacker,&defender,&d,0,src);SUNITDMG_CalculateTotalDamage(&game,&attacker,&defender,&d);
printf("%s[[%d,%d,%d,%d],[%d,%d,%d,%d,%d]]",first?"":",",scenario,amount,src,initial,d.dwLifeLeech,d.dwManaLeech,d.dwStamLeech,d.dwPhysDamage,d.dwDmgTotal);first=false;
}printf("]\n");}
'''
    generated=BASE/'npc-item-drain-source-probe.cpp';generated.write_text(cpp);binary=generated.with_suffix('')
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(binary)],capture_output=True)
    (BASE/'npc-item-drain-compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()[-3000:]
    run=subprocess.run([str(binary)],capture_output=True);(BASE/'npc-item-drain-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-3000:]
    rows=json.loads(run.stdout);assert len(rows)==144
    for (scenario,amount,src,initial),o in rows:
        slot=scenario//2;field=initial+(amount*256 if scenario&1 else 0);field+=field*src//128
        assert o[slot]==field and all(v==0 for n,v in enumerate(o[:3]) if n!=slot)
        assert o[4]==o[3]+o[0]
        if scenario%2==0 and initial==0:assert o[:3]==[0,0,0]
    fixture=BASE/'npc-item-drain-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceFile=relative,sourceSha256=sha(source),unchangedRoutineHashes=hashes,currentTableHashes=pins,currentWriterRules=rules,
        currentPropertiesWritingDrainMaxima=[],cases=144,fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture)),generatedSourceSha256=sha(generated),sanitizers='ASan+UBSan exit0;empty runtime stderr',
        parent=dict(path=str(parent.relative_to(ROOT)),sha256=sha(parent),receipt=str(proof_path.relative_to(ROOT)),receiptSha256=sha(proof_path)),
        established=['Current item life/mana steal writers set only stats60/62;current stamdrain sets stamina-consumption stat154. No current property row writes attack-drain maxima61/63/65. Complete older non-hireling NPC fill leaves ordinary zero-initial drain fields0with these minimum-only/item154inputs.',
            'Artificial nonzero maxima and skill-supplied initial fields exercise different positive source lanes,including its additive source-damage scaling. They must not be treated as item percentage leech or silently discarded. Only the ordinary zero-initial,current min-only item contexts justify removing the overbroad allocation refusal.'],
        scope='Complete unchanged older FillDamageValues/range/total with declared raw item-stat pools,non-hireling NPC/classification,random,world and event services. Current property mapping is pinned table evidence,not execution of the property writer in this probe. No current executable certificate,actual recovery/resource-event path or generic innate/skill-supplied drain support is claimed.',
        remaining=['Native current writer-plan guards and actual equipped allocation need verification. Modern Iron Golem item-leech behavior remains unobserved. Full positive-max/skill-supplied drain and later resource/recovery events remain unsupported.'])
    target=ROOT/'tools/reference/npc-item-drain-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==text
    else:target.write_text(text)
    print('NPC item drain:144complete older fill/range/total controls;current min-only life/mana and stamina-consumption writers pinned;positive-max/skill drains remain separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
