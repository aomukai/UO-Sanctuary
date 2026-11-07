#!/usr/bin/env python3
"""Verify owned KK metadata and the pinned author's original Talon calculator.

This is a prepared clock projection, not captured D2R execution or a learned
skill. No live process, game input, installation write or downloaded update.
"""
import argparse
import hashlib
import json
import re
import struct
import subprocess
from pathlib import Path
from review_weapon_event_order import function as cpp_function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
STAGE=BASE/'dragon-talon-clock'
CALC=ROOT/'.local/research/ias-calculator-b7d7910'


def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()


def function(text,name):
    match=re.search(r'\bfunction\s+'+re.escape(name)+r'\([^;\n]*\)\s*\{',text)
    assert match,name
    end=text.index('{',match.start())+1;depth=1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[match.start():end]


def record(check=False):
    STAGE.mkdir(exist_ok=True)
    owned=ROOT/'tools/reference/d2r-local-animation-evidence.json'
    evidence=json.loads(owned.read_text());raw_path=ROOT/evidence['localRawPath']
    assert evidence['referenceVersion']=='3.3.93847' and sha(raw_path)==evidence['sha256']
    raw=raw_path.read_bytes();at=0;profiles=[]
    for _ in range(256):
        count=struct.unpack_from('<I',raw,at)[0];at+=4
        for _ in range(count):
            data=raw[at:at+160];at+=160
            code=data[:8].rstrip(b'\0').decode()
            if code.startswith('AIKK'):
                frames,speed=struct.unpack_from('<ii',data,8)
                events=[(i,v) for i,v in enumerate(data[16:]) if v]
                assert (frames,speed,events)==(13,256,[(4,1)]),code
                profiles.append(dict(WeaponClass=code[4:],Frames=frames,Speed=speed,Action=4,Flag=1))
    assert at==len(raw)
    assert {p['WeaponClass'] for p in profiles}=={'HTH','1HS','1HT','2HS','2HT','STF','BOW','XBW','HT1','HT2'}
    profiles.sort(key=lambda p:p['WeaponClass'])
    out=ROOT/'data/Sanctuary/Balance/assassin-kick-clock.json'
    encoded=json.dumps(profiles,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    skills=ROOT/'tools/reference/d2r-named-equipment/json/skills.json'
    row=json.loads(skills.read_text())['255']
    assert (row['anim'],row['seqtrans'],row['seqnum'],row['srvstfunc'],row['srvdofunc'],row['calc1'])==('KK','KK',19,24,42,'lvl/6+1')
    commit=json.loads((CALC/'commit.json').read_text());tree=json.loads((CALC/'tree.json').read_text())
    assert commit['sha']=='b7d791031b73b0c6e56a3659e9198578aed21261' and tree['sha']==commit['commit']['tree']['sha']
    inputs={str(owned.relative_to(ROOT)):sha(owned),str(raw_path.relative_to(ROOT)):sha(raw_path),str(skills.relative_to(ROOT)):sha(skills)}
    legacy_tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert legacy_tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    reviewed={}
    for rel,names in {
        'source/D2Game/src/UNIT/SUnit.cpp':['sub_6FCBC4D0','sub_6FCBCE70','sub_6FCBCFD0'],
        'source/D2Common/src/Units/Units.cpp':['UNITS_CanAnimModeUseAttackRate','UNITS_UpdateAttackAnimRateAndVelocity'],
    }.items():
        p=BASE/'d2moo-pinned-source'/rel;data=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==next(x['sha'] for x in legacy_tree['tree'] if x['path']==rel)
        inputs[str(p.relative_to(ROOT))]=sha(p);text=data.decode()
        for name in names:reviewed[name]=hashlib.sha256(cpp_function(text,name).encode()).hexdigest()
        if 'gaPlayerModesAnimModulators' in text:
            block=re.search(r'static D2AnimUnkStrc gaPlayerModesAnimModulators\[[^]]+\]\s*=\s*\{(.*?)\};',text,re.S).group(1)
            modes=[list(map(int,m)) for m in re.findall(r'\{\s*(\d),\s*(\d),\s*(\d),\s*(\d),\s*(\d)\s*\}',block)]
            assert len(modes)==20 and modes[12]==[0,0,0,1,0]
    for name in ('README.md','calculator.js','constants.js'):
        p=CALC/name;data=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==name)
        inputs[str(p.relative_to(ROOT))]=sha(p)
    calc=(CALC/'calculator.js').read_text();constants=(CALC/'constants.js').read_text()
    names=['calculateEIAS','limitEIAS','getStartingFrame','calculateActionFrame','calculateFramesPerDirection','calculateAnimationSpeed','rbf','rhits']
    bodies={n:function(calc,n) for n in names}
    bodies['convertIAStoEIAS']=function(constants,'convertIAStoEIAS')
    assert 'DRAGON_TALON: addSkill(new Skill("Dragon Talon", false, false, false, true))' in constants
    start=calc.index('let firstHitLength = 256 *');brace=calc.index('{',calc.index('if (skill == skills.FURY || skill == skills.STRAFE || skill == skills.FEND',start))+1;end=brace;depth=1
    while depth:
        depth+=(calc[end]=='{')-(calc[end]=='}');end+=1
    rollback=calc[start:end]
    # UI, skill/weapon identities and SIAS are declared inputs. All selected
    # calculator math and the complete original rollback block remain intact.
    js=r'''
const trun=Math.trunc,log=()=>{};
const skills=Object.fromEntries(['DRAGON_TALON','FURY','STRAFE','FEND','ZEAL','WHIRLWIND','STANDARD','KICK','DODGE','THROW','DRAGON_TAIL','SMITE','LAYING_TRAPS','FISTS_OF_FIRE','CLAWS_OF_THUNDER','BLADES_OF_ICE','DRAGON_CLAW','HUNGER','RABIES'].map(k=>[k,{}]));
const skill=skills.DRAGON_TALON;skill.isDualWieldedSequenceSkill=()=>false;
const char={AMAZON:0,SORCERESS:1,ASSASSIN:6,BARBARIAN:4,DRUID:5,FRENZY_BARBARIAN:8},character=char.ASSASSIN;
const wf={HUMAN:0,WEREWOLF:1,WEREBEAR:2},wereform=wf.HUMAN,other={MIN_EIAS:-85,MAX_EIAS:75,MAX_EIAS_WEREFORMS:150};
const wt=Object.fromEntries(['CLAW','GENERIC','UNARMED','ONE_HANDED_SWINGING','TWO_HANDED_SWORD','ONE_HANDED_THRUSTING','TWO_HANDED_THRUSTING','TWO_HANDED','THROWING'].map(k=>[k,{}]));
for(const w of Object.values(wt)){w.getFramesPerDirection=()=>999;w.getActionFrame=()=>999;}
const tv={IAS:0},tableVariable=1,number={IAS:{value:'0'}};
let sourceSias=0,sourceWsm=0,secondaryProbe=0;
function calculateSIAS(){return sourceSias;}
function getWSM(primary){if(!primary)throw Error('Talon read secondary WSM');return sourceWsm;}
function getWeaponIAS(primary){if(!primary)throw Error('Talon read secondary IAS');return 0;}
function isSecondWeaponSet(){return false;}
function formatRollbackHitLength(x){return x;}
function frameLengthsNotEqual(){return true;}
'''
    js+='\n'.join(bodies.values())+'\nconst constants={convertIAStoEIAS};\nlet rates=[],timing=[];\n'
    js+=r'''
for(let ias of [0,1,20,50,120,1000,10000])for(let wsm of [-30,-20,0,20,50])for(let sias of [-150,-85,-20,0,20,75,150])for(let offhand of [0,10000]){
number.IAS.value=String(ias);sourceWsm=wsm;sourceSias=sias;secondaryProbe=offhand;
const eias=calculateEIAS(true),base=calculateAnimationSpeed(wt.CLAW,13),advance=trun(base*(100+limitEIAS(eias[0]))/100);
rates.push([ias,wsm,sias,offhand,eias,base,advance]);
}
for(let speedIncrease of [38,64,127,128,180,208,255,256,257,307,320,384,448]){
const weaponType=wt.CLAW,startingFrame=getStartingFrame(weaponType),framesPerDirection1=calculateActionFrame(weaponType),framesPerDirection2=calculateFramesPerDirection(weaponType),offset=0,acceleration=0,EIAS=0;
const previousFrameLengths=[[],[]],results=[[],[]],accelerationTables=results.map(x=>({addBreakpoint:(_,v)=>x.push(v)}));
'''+rollback+r'''
timing.push([speedIncrease,startingFrame,framesPerDirection1,framesPerDirection2,rbf(),results[0][0]]);
}
process.stdout.write(JSON.stringify({rates,timing})+'\n');
'''
    script=STAGE/'original-calculator.js';script.write_text(js)
    run=subprocess.run(['node',str(script)],capture_output=True,check=True)
    assert not run.stderr
    result=json.loads(run.stdout);assert len(result['rates'])==490 and len(result['timing'])==13
    for ias,wsm,sias,offhand,eias,base,advance in result['rates']:
        effective=120*ias//(120+ias)
        assert eias==[sias-wsm+effective,sias,wsm,-1,effective,ias,0,-1]
        assert base==256 and advance==256*max(15,min(175,100+sias-wsm+effective))//100
    plans=[]
    for advance,start,action,total,factor,lengths in result['timing']:
        step=(4*256+advance-1)//advance;final=(13*256+advance-1)//advance-1
        assert (start,action,total,factor,lengths)==(0,4,13,100,[step,step,final])
        for rank in range(1,256):
            count=rank//6+1
            plans.append([rank,advance,[step*(i+1) for i in range(count)],step*(count-1)+final])
    result['plans']=plans
    cases=STAGE/'cases.json';cases.write_text(json.dumps(result,separators=(',',':'))+'\n')
    legacy=ROOT/'tools/reference/dragon-talon-sequence-source-evidence.json'
    proof=dict(referenceVersion='3.3.93847',component='Prepared Dragon Talon current-calculator clock',handlerEnabled=False,
        currentExecutableTimingVerified=False,installed=False,currentAnimationProfiles=len(profiles),rateCases=len(result['rates']),
        rankPlanCases=len(plans),rollbackRows=len(result['timing']),originalAuthor=dict(repository='https://github.com/Warren1001/IAS_Calculator',commit=commit['sha']),
        reviewedOlderRoutineHashes=reviewed,olderRoutinesExecuted=False,legacyCommit=legacy_tree['sha'],
        functionHashes={n:hashlib.sha256(body.encode()).hexdigest() for n,body in bodies.items()},rollbackBlockHash=hashlib.sha256(rollback.encode()).hexdigest(),
        inputs=inputs,artifacts={str(p.relative_to(ROOT)):sha(p) for p in (out,script,cases,Path(__file__).resolve(),legacy)},
        established=['Owned current KK records are13frames/base256/event4 for all10weapon classes. The skill selects KK/KK;seqnum19does not select SQ in the separately reviewed ordinary player entry.',
            'Pinned original author calculator selects primary IAS/WSM,base256,zero starting frame,100percent rollback and action4/full13. Secondary WSM/IAS read attempts are refused by the declared harness.',
            'Original rollback output keeps final full-cycle ceil(13*256/advance)-1 separately from repeated ceil(4*256/advance) contacts.255ranks across13rates retain floor(rank/6)+1kicks.'],
        limits=['This is the original calculator with declared UI/metadata/SIAS inputs,not current executable or graphical/input equivalence. Negative SIAS aggregates owner slow and skill bonuses;their native suppliers need separate checks.',
            'The older actual mode/scheduler/rewind composition and its frameFlags[-1] header access are not executed by this probe. Do not relabel this projection as complete older/current engine timing.',
            'No hit/block/charge consumption,target selection,payment,packet/proc,world actor or learned cast is enabled. Any unresolved modern policy remains explicitly gated.'])
    target=ROOT/'tools/reference/dragon-talon-clock-source-evidence.json';encoded=json.dumps(proof,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print(f'Talon clock:10owned KK profiles/490original-author rates/13rollback rows/{len(plans)}rank plans;prepared projection only;no learned route.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
