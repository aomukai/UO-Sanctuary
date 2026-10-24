#!/usr/bin/env python3
"""Compile full legacy skill/missile routines and the unchanged melee poison lane."""
import argparse, hashlib, json, re, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
REF = ROOT / 'tools/reference/d2r-named-equipment'


def build(check=False):
    subprocess.run([sys.executable, str(ROOT / 'tools/probe_poison_javelin_damage_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes, blocks = {}, {}

    def source(path):
        raw = (STAGE / 'd2moo-pinned-source' / path).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == path)
        hashes[path] = hashlib.sha256(raw).hexdigest()
        return raw.decode()

    def routine(path, name):
        text = source(path)
        match = re.search(r'^\w[^;\n]*\b' + name + r'\([^;{]*?\)\s*\n\{', text, re.M)
        assert match, name
        end = text.index('{', match.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        body = text[match.start():end]
        blocks[name] = hashlib.sha256(body.encode()).hexdigest()
        return body

    scaffold = STAGE / 'poison-javelin-damage-source-probe.cpp'
    prefix = scaffold.read_text().split('int main()', 1)[0]
    for path, names in [
        ('source/D2Common/src/D2Skills.cpp', ['SKILLS_CalculateDamageBonusByLevel', 'SKILLS_CalculateMasteryBonus', 'SKILLS_GetMinElemDamage', 'SKILLS_GetMaxElemDamage', 'SKILLS_GetElementalLength']),
        ('source/D2Common/src/Units/Missile.cpp', ['MISSILE_CalculateDamageData', 'MISSILE_AddStatsToDamage', 'MISSILE_CalculateFinalDamage', 'MISSILE_SetDamageStats'])
    ]:
        for name in names:
            assert routine(path, name) in prefix, name
    damage = source('source/D2Game/src/UNIT/SUnitDmg.cpp')
    start = damage.index('\tint32_t nMinDamage = STATLIST_UnitGetStatValue(pAttacker, STAT_POISONMINDAM, 0);')
    end = damage.index('\n\tif (pDamage->dwColdDamage > 0)', start)
    lane = damage[start:end]
    percent = routine('source/D2Game/src/MONSTER/MonsterUnique.cpp', 'MONSTERUNIQUE_CalculatePercentage')
    raw = (REF / 'json/skills.json').read_bytes()
    assert hashlib.sha256(raw).hexdigest() == json.loads((REF / 'sources.json').read_text())['files']['json/skills.json']['sha256']
    venom = json.loads(raw)['278']
    prefix = '#include <algorithm>\n' + prefix
    prefix = prefix.replace('int dwUnitType=0,dwClassId=0;', 'int dwUnitType=0,dwClassId=0,pSeed=0;')
    prefix += '\n' + percent + r'''
struct D2DamageStrc{int dwPoisDamage=0,dwPoisLen=0;};
int rollMode=0,rollBound=-1;
int ITEMS_RollLimitedRandomNumber(int*,int span){rollBound=span;return rollMode && span>0?span-1:0;}
void MeleePoisonLane(D2UnitStrc* pAttacker,D2DamageStrc* pDamage,int nSrcDam){
'''+lane+'\n}\n'
    setup = 'skill.dwEMin=' + str(venom['EMin']) + ';skill.dwEMax=' + str(venom['EMax']) + ';'
    setup += 'skill.nToHitShift=' + str(venom['HitShift']) + ';skill.dwEDmgSymPerCalc=-1;skill.dwELen=10;skill.dwELenSymPerCalc=-1;'
    for i in range(5):
        setup += f"skill.dwEMinLev[{i}]={venom['EMinLev'+str(i+1)]};skill.dwEMaxLev[{i}]={venom['EMaxLev'+str(i+1)]};"
    for i in range(3):
        setup += f'skill.dwELevLen[{i}]=0;'
    cpp = prefix + r'''int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;bool first=true;std::printf("[");
for(int rank:{1,8,9,16,17,22,23,28,29,255})for(int castMastery:{0,25})for(int liveMastery:{0,50})for(int item:{0,257})for(int length:{0,101})for(int count:{1,3})for(int overrideLength:{0,10})for(int factor:{32,128})for(int high:{0,1})for(int initialRate:{0,257})for(int initialLength:{0,37}){
 owner={};weapon={};missile={};skill={};row={};missileData={};
'''+setup+r'''
 skill.nEType=ELEMTYPE_POIS;owner.stats[STAT_PASSIVE_POIS_MASTERY]=castMastery;
 int minimum=SKILLS_GetMinElemDamage(&owner,278,rank,1),maximum=SKILLS_GetMaxElemDamage(&owner,278,rank,1),duration=SKILLS_GetElementalLength(&owner,278,rank,1);
 owner.stats[STAT_POISONMINDAM]=minimum+item;owner.stats[STAT_POISONMAXDAM]=maximum+item;
 owner.stats[STAT_POISONLENGTH]=length;owner.stats[STAT_POISON_COUNT]=count;owner.stats[STAT_SKILL_POISON_OVERRIDE_LENGTH]=overrideLength;
 owner.stats[STAT_PASSIVE_POIS_MASTERY]=liveMastery;rollMode=high;rollBound=-1;
 D2DamageStrc melee;melee.dwPoisDamage=initialRate;melee.dwPoisLen=initialLength;MeleePoisonLane(&owner,&melee,factor);
 int bound=rollBound;
 // A weapon missile with no independent elemental skill packet collects owner
 // stats through the full creator. Its source factor is a declared row input.
 skill.nEType=ELEMTYPE_NONE;skill.nSrcDam=factor;row.nSrcDamage=factor;row.nSkill=0;missileData.nSkill=0;missile.dwUnitType=UNIT_MISSILE;weapon.dwUnitType=UNIT_ITEM;
 D2MissileDamageDataStrc packet;MISSILE_CalculateDamageData(&packet,&owner,nullptr,&missile,rank);MISSILE_SetDamageStats(&owner,&missile,&packet,rank);
 if(!first)std::printf(",");first=false;
 std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,castMastery,liveMastery,item,length,count,overrideLength,factor,high,initialRate,initialLength,minimum,maximum,duration,melee.dwPoisDamage,melee.dwPoisLen,bound,packet.nPoisonMinDamage,packet.nPoisonMaxDamage,packet.nPoisonLength,packet.nPoisonCount,missile.stats[STAT_PASSIVE_POIS_MASTERY]);
}std::printf("]\n");}
'''
    path = STAGE / 'venom-damage-source-probe.cpp'
    path.write_text(cpp)
    subprocess.run(['g++', '-std=c++20', '-O2', str(path), '-o', str(path.with_suffix(''))], check=True)
    rows = json.loads(subprocess.check_output([str(path.with_suffix(''))]))
    assert len(rows) == 10240, len(rows)

    def endpoint(rank, maximum):
        key = 'EMax' if maximum else 'EMin'
        result, extra = venom[key], rank-1
        for i, size in enumerate([7,8,6,6,2**30],1):
            used = min(size, extra)
            result += used * venom[key+'Lev'+str(i)]
            extra -= used
        return result << venom['HitShift']

    for rank, cast, live, item, length, count, override, factor, high, initial, initialLength, low, upper, duration, meleeRate, meleeLength, bound, missileMin, missileMax, missileLength, missileCount, missileMastery in rows:
        expected = [endpoint(rank,x) for x in [False,True]]
        expected = [x+x*cast//100 for x in expected]
        assert (low,upper,duration) == (*expected,10)
        mastered = [x+item+(x+item)*live//100 for x in expected]
        rolled = mastered[0] + (mastered[1]-mastered[0]-1 if high else 0)
        assert (meleeRate,meleeLength,bound) == ((initial+rolled)*factor//128,(initialLength+override if override>0 else (initialLength+length)//count),mastered[1]-mastered[0])
        assert (missileMin,missileMax,missileLength,missileCount,missileMastery) == ((low+item)*factor//128,(upper+item)*factor//128,override if override>0 else length,0 if override>0 else count*factor//128,0)
    fixture = STAGE / 'venom-damage-source-probe.json'
    fixture.write_text(json.dumps(rows, separators=(',', ':'))+'\n')
    evidence = dict(referenceVersion='3.3.93847',talentId='assassin.venom',proofEnablesHandler=False,
                    legacyCommit=tree['sha'],sourceHashes=hashes,compiledUnchangedRoutines=blocks,
                    compiledUnchangedStatementBlock=dict(parent='SUNITDMG_FillDamageValues',sha256=hashlib.sha256(lane.encode()).hexdigest(),scope='Complete poison rate/length lane only, not the complete weapon/damage function.'),
                    scaffold=dict(path=str(scaffold.relative_to(ROOT)),sha256=hashlib.sha256(scaffold.read_bytes()).hexdigest()),
                    pinnedSkillsSha256=hashlib.sha256(raw).hexdigest(),
                    fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cases=len(rows)),
                    established=[
                        'Full elemental getters capture Venom rate endpoints with cast-time poison mastery and10-frame length. All tier boundaries and rank255 are separate from live weapon mastery.',
                        'Complete unchanged melee poison lane adds a live-mastered owner poison roll to any prepared rate, scales the sum by SrcDam/128, and adds override length instead of averaging item length/count. Under override the existing packet length is preserved; without override the entire combined length is divided by item count.',
                        'Full older weapon missile creator/add/scale/setter collects captured Venom plus item poison rates without a second owner-mastery multiplication, substitutes override length, excludes item count under override and does not copy owner poison mastery into the missile.'
                    ],services=[
                        'Mastery ratio helper, unit/stat/table/weapon lookups, formula evaluator, roll endpoints, critical and empty mon-type stat copy are explicit services inherited from the checked creator scaffold. No global actor/event or current executable is simulated.',
                        'Source factor32/128 and initial prepared poison rate/length are driver inputs. The outer FillDamageValues zero-to128 default and other damage/event/weapon branches are reviewed outside the compiled poison lane.',
                        'Override0 with captured Venom-rate endpoints is a synthetic arithmetic comparison, not a claim that the actual Venom state can omit its override stat.'
                    ],limits=[
                        'Current-game rank1 and rank21 tooltips corroborate damage/length endpoints and120/360second buff durations; the latter distinguishes pinned12second/rank growth from the older guide. Modern ranged Venom mastery has not been directly observed. Blizzard2.6 explicitly changed ranged Enchant/Fire Mastery; that does not establish whether modern poison was changed. Do not present older missile behavior as current executable proof.',
                        'Ordinary non-Venom melee/ranged collectors retain separate source-count/mastery gaps. Learned Venom deliberately uses raw collection with distinct captured and live mastery; native delivery evidence is separate from this source probe.',
                        'This source probe alone does not verify native source-frame mitigation/application/persistence, delivery, item/source snapshots, real gear/input or reload behavior; those require separate native evidence.'
                    ])
    output = ROOT / 'tools/reference/venom-damage-engine-evidence.json'
    rendered = json.dumps(evidence,indent=2)+'\n'
    if check:
        assert output.read_text()==rendered
    else:
        output.write_text(rendered)
    print('Venom:10240 full skill/missile and unchanged melee poison-lane cases; modern ranged mastery remains explicit acceptance work.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
