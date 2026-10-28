#!/usr/bin/env python3
"""Execute Rabies child damage capture and complete relative-origin factory bodies."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    subprocess.run(['python3', str(ROOT / 'tools/prepare_druid_rabies_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    preflight = ROOT / 'tools/reference/druid-rabies-preflight.json'
    pin = json.loads(preflight.read_text())
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == pin['legacyCommit']
    hashes, routines, enums, reused = {}, {}, {}, {}

    def source(relative):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        hashes[relative] = sha(path)
        return raw.decode()

    def routine(relative, name):
        body = function(source(relative), name)
        routines[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    def enum(relative, name):
        body = re.search(r'\benum\s+' + name + r'\s*\{.*?\};', source(relative), re.S).group()
        enums[name] = hashlib.sha256(body.encode()).hexdigest()
        return body + '\n'

    # Reuse only the declared scaffolds; re-extract and compare every production
    # function against the pinned Git tree, without rerunning frozen old suites.
    def scaffold(name, evidence, cutoff):
        path = BASE / name
        parent = ROOT / evidence
        data = json.loads(parent.read_text())
        assert data['legacyCommit'] == tree['sha']
        raw = path.read_text()
        for symbol, digest in data['blockHashes'].items():
            if symbol in raw:
                assert hashlib.sha256(function(raw, symbol).encode()).hexdigest() == digest, symbol
        reused[evidence] = sha(parent)
        return raw.split(cutoff, 1)[0]

    common = 'source/D2Common/src/D2Skills.cpp'
    missile = 'source/D2Common/src/Units/Missile.cpp'
    flags = enum('source/D2Common/include/DataTbls/MissilesTbls.h', 'D2C_MissilesTxtFlagIndices')
    base = scaffold('bow-launch-source-probe.cpp', 'tools/reference/bow-launch-engine-evidence.json', 'int main()')
    # Replace the synthetic scaffold enumerations with original enum values.
    base = re.sub(r'enum Stats\{[^}]*\};', enum('source/D2Common/include/D2StatList.h', 'D2C_ItemStats'), base)
    base = re.sub(r'enum Elements\{[^}]*\};', enum('source/D2Common/include/D2Combat.h', 'D2C_ElemTypes'), base)
    base = re.sub(r'enum Flags\{[^}]*\};', flags, base)
    base = base.replace('stats[128]', 'stats[1024]')
    base = base.replace('#define __fastcall', '#define D2_MAYBE_UNUSED(x) (void)(x)\n#define ARRAY_SIZE(x) (sizeof(x)/sizeof((x)[0]))\nusing BOOL=int;\n#define __fastcall')
    base = base.replace('int nSkill=6;', 'int nSkill=238,nLevel=1,fFlags=0;')
    base = base.replace('int nSrcDam=128,nEType=0;', 'int nSrcDam=0,nEType=0,dwEMin=6,dwEMax=14,dwEMinLev[5]={4,5,7,11,16},dwEMaxLev[5]={4,5,7,11,16},nToHitShift=3,dwEDmgSymPerCalc=1,dwELen=100,dwELevLen[3]={10,10,10},dwELenSymPerCalc=-1;')
    base = base.replace('bool critical=false;int skillMin=0,skillMax=0;', 'bool critical=false;int skillMin=0,skillMax=0,hard=0,critCalls=0;')
    base = base.replace('bool MISSILE_HasBonusStats(D2UnitStrc*,D2UnitStrc*){return critical;}', 'bool MISSILE_HasBonusStats(D2UnitStrc*,D2UnitStrc*){critCalls++;return critical;}')
    for name in ['SKILLS_GetMinElemDamage', 'SKILLS_GetMaxElemDamage', 'SKILLS_GetElementalLength']:
        old = 'int ' + name + '(D2UnitStrc*,int,int,int){return 0;}'
        assert old in base
        base = base.replace(old, 'int ' + name + '(D2UnitStrc*,int,int,int);')
    # Pin even the unchanged creator bodies inherited from the scaffold.
    for name in ['MISSILE_AddStatsToDamage', 'MISSILE_CalculateFinalDamage', 'MISSILE_CalculateDamageData']:
        body = routine(missile, name)
        assert function(base, name) + '\n' == body
    base += '\nstruct D2StatStrc{int nValue=0,nLayer=0;};\nvoid STATLIST_SetUnitStat(D2UnitStrc* p,int stat,int value,int){p->stats[stat]=value;}\nint STATLIST_CopyStats(D2UnitStrc*,int,D2StatStrc*,int){return 0;}\nint SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int,int){return hard*20;}\nint DATATBLS_ApplyRatio(int value,int percent,int divisor){return (int)((int64_t)value*percent/divisor);}\n'
    for name in ['SKILLS_CalculateDamageBonusByLevel', 'SKILLS_CalculateMasteryBonus', 'SKILLS_GetMinElemDamage', 'SKILLS_GetMaxElemDamage', 'SKILLS_GetElementalLength']:
        base += routine(common, name)
    base += routine(missile, 'MISSILE_SetDamageStats')
    fields = sorted(set(re.findall(r'pMissileDamageData->(\w+)', function(base, 'MISSILE_CalculateDamageData') + function(base, 'MISSILE_AddStatsToDamage') + function(base, 'MISSILE_CalculateFinalDamage'))))
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int rank=1;rank<=255;rank++)for(int points:{0,1,20})for(int mastery:{-50,0,25,100})for(int noise:{0,1})for(int hasOrigin:{0,1}){
 owner={};weapon={};missile={};skill={};row={};missileData={};weapon.dwUnitType=UNIT_ITEM;missile.dwUnitType=UNIT_MISSILE;missile.dwClassId=516;missileData.nLevel=rank;
 row.nSkill=238;skill.nEType=ELEMTYPE_POIS;hard=points;critical=true;critCalls=0;skillMin=skillMax=0;
 for(int& stat:owner.stats)stat=noise?37:0;
 owner.stats[STAT_PASSIVE_POIS_MASTERY]=mastery;
 D2UnitStrc origin;for(int& stat:origin.stats)stat=99999;
 D2MissileDamageDataStrc d;MISSILE_CalculateDamageData(&d,&owner,hasOrigin?&origin:nullptr,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&d,rank);
 printf("%s[%d,%d,%d,%d,%d,[",first?"":",",rank,points,mastery,noise,hasOrigin);first=false;
'''
    for index, field in enumerate(fields):
        base += f'printf("{"," if index else ""}%d",d.{field});\n'
    base += 'printf("],%d,%d,%d,%d,%d]",missile.stats[STAT_POISONMINDAM],missile.stats[STAT_POISONLENGTH],missile.stats[STAT_POISON_COUNT],missile.stats[STAT_PASSIVE_POIS_MASTERY],critCalls);}\nprintf("]\\n");}\n'

    def execute(label, cpp):
        path = BASE / f'druid-rabies-{label}-source-probe.cpp'
        path.write_text(cpp)
        compiler = BASE / f'druid-rabies-{label}-compiler.log'
        with compiler.open('w') as log:
            subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(path.with_suffix(''))], stdout=log, stderr=subprocess.STDOUT, check=True)
        result = subprocess.run([str(path.with_suffix(''))], capture_output=True)
        sanitizer = BASE / f'druid-rabies-{label}-sanitizer.log'
        sanitizer.write_bytes(result.stderr)
        assert result.returncode == 0 and not result.stderr, result.stderr.decode()[-3000:]
        return json.loads(result.stdout), {str(x.relative_to(ROOT)): sha(x) for x in [path, compiler, sanitizer]}

    damage, artifacts = execute('damage', base)
    assert len(damage) == 12240
    row = pin['currentRows']['skills']['Rabies']

    def trunc(value):
        return abs(value) // 100 * (1 if value >= 0 else -1)

    for rank, hard, mastery, noise, origin, values, rate, length, count, stored_mastery, critical_calls in damage:
        expected = []
        for lane in ['EMin', 'EMax']:
            amount, left = int(row[lane]), rank - 1
            for i, cap in enumerate([7, 8, 6, 6, 2**30], 1):
                n = min(cap, left)
                amount += n * int(row[f'{lane}Lev{i}'])
                left -= n
            amount <<= int(row['HitShift'])
            amount += trunc(amount * hard * 20)
            amount += trunc(amount * mastery)
            expected.append(amount)
        actual = dict(zip(fields, values))
        assert [actual['nPoisonMinDamage'], actual['nPoisonMaxDamage'], actual['nPoisonLength']] == expected + [100 + 10 * (rank - 1)]
        assert all(value == 0 for key, value in actual.items() if key not in ['nPoisonMinDamage', 'nPoisonMaxDamage', 'nPoisonLength'])
        assert [rate, length, count, stored_mastery, critical_calls] == [expected[0], 100 + 10 * (rank - 1), 0, 0, 0]

    # Complete factory with existing explicit world/path/stat services. No guided
    # launch or homing bodies are retained or called.
    base = scaffold('guided-arrow-source-probe.cpp', 'tools/reference/guided-arrow-engine-evidence.json', 'unsigned int __stdcall UNITS_GetDistanceToCoordinates')
    # The exact first routine spelling is pinned below; avoid retaining duplicate
    # production factory bodies if the scaffold format ever changes.
    assert 'D2UnitStrc* __fastcall MISSILES_CreateMissileFromParams' not in base
    base = re.sub(r'enum\{MISSILESFLAGINDEX_CANSLOW,MISSILESFLAGINDEX_CANDESTROY,MISSILESFLAGINDEX_PIERCE\};', flags, base)
    for symbol in ['UNITFLAG_DOUPDATE', 'UNITFLAG_ISVALIDTARGET', 'UNITFLAG_CANBEATTACKED', 'UNITFLAG_SKSRVDOFUNC', 'UNITFLAG_ISDEAD']:
        base = re.sub(r',' + symbol + r'=(?:0x[0-9A-Fa-f]+|[0-9]+)', '', base)
    unit_flags = enum('source/D2Common/include/Units/Units.h', 'D2C_UnitFlags')
    base = base.replace('struct D2GameStrc', unit_flags + '\nstruct D2GameStrc', 1)
    base = base.replace('total=128,mode=0', 'total=128,activate=0,mode=0')
    base = base.replace('void MISSILE_SetActivateFrame(D2UnitStrc*,int){}', 'void MISSILE_SetActivateFrame(D2UnitStrc* u,int v){u->activate=v;}')
    # Guided declarations reference its uncalled callback; provide a hard stop.
    base += '\nint sub_6FD10880(D2AuraCallbackStrc*,D2UnitStrc*){std::abort();}\n'
    base += '\nint UNITS_GetDistanceToCoordinates(D2UnitStrc*,int,int){std::abort();}\n'
    base = base.replace('#include <cstdint>', '#include <cstdint>\n#include <cassert>')
    base += routine('source/D2Game/src/MISSILES/Missiles.cpp', 'MISSILES_CreateMissileFromParams')
    base += r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;printf("[");
for(int mid:{515,516})for(int rank:{1,3,20,28,60,255})for(int dx=-7;dx<=7;dx++)for(int dy=-7;dy<=7;dy++)for(int slowed:{0,1}){
 owner={};target={};created={};owner.pDynamicPath=&owner.path;target.pDynamicPath=&target.path;created.pDynamicPath=&created.path;
 owner.dwUnitType=mid==515?UNIT_MONSTER:UNIT_PLAYER;owner.x=mid==515?31:400;owner.y=mid==515?47:700;target.x=31;target.y=47;
 row={};row.nVel=row.nMaxVel=mid==516?5:0;row.wRange=mid==516?20:0;row.nActivate=mid==516?2:0;row.nCollideType=3;row.dwMissileFlags=gdwBitMasks[MISSILESFLAGINDEX_CANSLOW];
 hasRoom=hasRecord=allocated=true;slow=slowed;draws=creates=repaths=0;
 D2MissileStrc p;p.pOwner=&owner;p.pOrigin=mid==515?&owner:&target;p.nMissile=mid;p.nSkill=238;p.nSkillLevel=rank;p.dwFlags=mid==515?0x8000:2;p.nRange=100+10*(rank-1);p.nTargetX=dx;p.nTargetY=dy;
 auto* result=MISSILES_CreateMissileFromParams(&game,&p);
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",first?"":",",mid,rank,dx,dy,slowed,result!=nullptr,created.x,created.y,created.path.x,created.path.y,created.path.velocity,created.total,created.remaining,created.skill,created.rank,int(created.owner==&owner),draws,created.activate);first=false;
}printf("]\n");}
'''
    factory, more = execute('factory', base)
    artifacts.update(more)
    assert len(factory) == 5400
    for mid, rank, dx, dy, slow, made, x, y, aim_x, aim_y, velocity, total, remaining, skill, stored_rank, owned, draws, activate in factory:
        child = mid == 516
        expected_aim = [31 + dx, 47 + dy] if child else [31, 47]
        if child and dx == 0 and dy == 0:
            expected_aim = [32, 48]
        assert [made, x, y, aim_x, aim_y, velocity, total, remaining, skill, stored_rank, owned, draws] == [1, 31, 47, *expected_aim, (480 if slow else 960) if child else 0, 20 if child else 100 + 10 * (rank - 1), 20 if child else 100 + 10 * (rank - 1), 238, rank, 1, 0]
        assert activate == (18 if child else 100 + 10 * (rank - 1))

    fixture = BASE / 'druid-rabies-factory-source-probe.json'
    fixture.write_text(json.dumps(dict(damageFields=fields, damage=damage, factory=factory), separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', sourcePreparation=True, nativeAccepted=False, currentExecutableParity=False,
        legacyCommit=tree['sha'], preflightSha256=sha(preflight), completeRoutineHashes=routines, pinnedEnumHashes=enums,
        sourceHashes=hashes, reusedScaffolds=reused, artifacts=artifacts,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture)), damageContexts=len(damage), factoryContexts=len(factory),
        damageColumns=['rank', 'hardCreeper', 'poisonMastery', 'ownerNoise', 'originPresent', 'damageFields', 'storedRate256', 'storedFrames', 'storedCount', 'storedMastery', 'criticalCalls'],
        factoryColumns=['missileId', 'rank', 'offsetX', 'offsetY', 'slowMissiles', 'created', 'originX', 'originY', 'aimX', 'aimY', 'pathVelocity256', 'totalFrames', 'currentFrames', 'storedSkill', 'storedRank', 'ownerRetained', 'pierceDraws', 'activationFrame'],
        established=[
            'Complete child516 creator/getters/stat writer recalculate skill238 poison endpoints at the supplied retained rank, using declared live caster hard Creeper points and poison mastery. Source damage0 excludes weapon physical, typed damage, item poison/rate/length/count/drain, critical and origin packet stats. Mastery applies once after hard synergy; stored missile mastery is0. All255ranks and independent hard/mastery/origin/noise controls pass.',
            'Complete factory positions both515and516at supplied origin, not original caster coordinates. Child flags2interprets independent offsets[-7,7]relative to emitter. Zero/zero is accepted and changes aim to origin+(1,1); it is not silently dropped. Child velocity5becomes960fixed-path units after the75percent factor; declared50percent Slow Missiles gives480. Range20and activation2remain distinct from inherited infection deadline.',
            'Plague flags0x8000uses supplied infection lifetime at stationary host origin. Child retains original caster owner, while plague retains its supplied infected-host owner. Factory does not consume pierce RNG for these records; no damage packet is inherited from the origin. Existing spread proof separately binds original caster identity/deadline in emitter metadata.',
        ], declaredServices=[
            'Factory world/room allocation, path integration, collision masks, stat-list lifecycle, damage creator/writer calls and owner/event services are explicit adapters. It executes the full factory but not real geometric flight/collision or modern ABI. The separate damage matrix executes complete creator/getters/stat writer, not a combined factory+damage+world simulation.',
            'Hard-point formula lookup, stat projection, ratio, zero physical getters, zero unselected missile endpoints, critical selection and empty monster-type stat copy are declared damage services. No actual modern gear preparation, seed or damage events execute. Skill and child rows come from licensed/current preflight; old source is behavioral evidence with explicit modern limits.',
        ], remaining=[
            'Connect native retained-rank/live caster child capture, zero-offset aim, independent marker/emitter, poison mitigation/replacement/cure and true input/resources/persistence. Exact Fog path, world contact order/activation, logout/facet ownership, lethal bite ordering and current executable payment/snapshot phases remain open. No Rabies handler or installed build changes are enabled by this source proof.',
        ])
    path = ROOT / 'tools/reference/druid-rabies-factory-engine-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print(f'Rabies factory: {len(damage)} damage / {len(factory)} origin/aim/lifetime cases; source preparation only.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
