#!/usr/bin/env python3
"""Compose complete legacy Dragon Claw wrappers and progressive helpers.

Declared combat services expose packet ordering; this does not establish the
modern per-contact charge policy or enable a runtime handler.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def build(check=False):
    subprocess.run(['python3', str(ROOT / 'tools/probe_martial_fire_release_reference.py'), '--check'], check=True, stdout=subprocess.DEVNULL)
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    sources, blocks = {}, {}

    def source(rel):
        raw = (STAGE / 'd2moo-pinned-source' / rel).read_bytes()
        assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == rel)
        sources[rel] = hashlib.sha256(raw).hexdigest()
        return raw.decode()

    def routine(text, name):
        match = re.search(r'^.*\b' + name + r'\([^;\n]*\)\s*\n\{', text, re.M)
        assert match, name
        end = text.index('{', match.start()) + 1
        depth = 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        blocks[name] = text[match.start():end]
        return blocks[name]

    ass = source('source/D2Game/src/SKILLS/SkillAss.cpp')
    monst = source('source/D2Game/src/SKILLS/SkillMonst.cpp')
    base = (STAGE / 'martial-fire-release-source-probe.cpp').read_text().split('int main(){', 1)[0]

    def replace(old, new):
        nonlocal base
        assert base.count(old) == 1, old
        base = base.replace(old, new)

    replace('dwCalc[1]={0}', 'dwCalc[4]={0,0,0,0}')
    replace('} row;', '} row,dragon;')
    replace('dwHitFlags=0,', 'dwConvPct=0,nConvType=0,dwHitFlags=0,')
    replace('D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int){return record?&row:nullptr;}',
            'bool dragonRecord=true;D2SkillsTxt* SKILLS_GetSkillsTxtRecord(int id){return id==260?(dragonRecord?&dragon:nullptr):record?&row:nullptr;}')
    replace('int SKILLS_GetToHitFactor(D2UnitStrc*,int,int rank){return 25+10*(rank-1);}',
            'int SKILLS_GetToHitFactor(D2UnitStrc*,int id,int rank){return id==260?40+25*(rank-1):25+10*(rank-1);}')
    replace('D2DamageStrc* SUNITDMG_GetDamageFromUnits(D2UnitStrc*,D2UnitStrc*){return &saved;}',
            'bool damageLookup=true;D2DamageStrc* SUNITDMG_GetDamageFromUnits(D2UnitStrc*,D2UnitStrc*){return damageLookup?&saved:nullptr;}')
    replace('return formula==375?375:', 'return formula==200?50+15*(rank-1):formula==375?375:')
    # Exact packet supplied by this service is intentional. Complete weapon stat,
    # critical and mitigation internals are separately scoped existing evidence.
    base += r'''
int postedAr=0,fillCalls=0,filledED=0;
int STATLIST_UnitGetStatValue(D2UnitStrc*,int stat,int){if(stat!=325)std::abort();return postedAr;}
void SUNITDMG_FillDamageValues(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc* d,int flag,int src){
 if(flag!=0 || src!=128)std::abort();++fillCalls;filledED=d->dwEnDmgPct;trace.push_back(4);d->dwPhysDamage=25600;
}
void sub_6FCF8C70(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,int32_t,int32_t);
'''
    base += '\n' + routine(ass, 'sub_6FCF5680')
    base += '\n' + routine(monst, 'SKILLS_SrvSt25_64_DragonClaw_MonFrenzy')
    base += '\n' + routine(ass, 'sub_6FCF8C70')
    base += '\n' + routine(ass, 'SKILLS_SrvDo046_DragonClaw')
    for name in ['sub_6FCF5870', 'sub_6FCF5BC0', 'sub_6FCF77E0', 'sub_6FCF5DE0',
                 'SKILLS_GetProgressiveSkillMissileId', 'SKILLS_EvaluateProgressiveSkillCalc',
                 'SKILLS_SrvDo038_FistsOfFire_BladesOfIce_ProgressiveFn2',
                 'SKILLS_SrvDo039_FistsOfFire_BladesOfIce_ProgressiveFn3',
                 'SKILLS_SrvDo143_FistsOfFire_RoyalStrike_ProgressiveFn']:
        assert base.count(routine(ass, name)) == 1, name
    base += r'''
int main(){gpSkillSrvDoFnTable_6FD40A20[143]=SKILLS_SrvDo143_FistsOfFire_RoyalStrike_ProgressiveFn;gpSkillSrvDoFnTable_6FD40A20[38]=SKILLS_SrvDo038_FistsOfFire_BladesOfIce_ProgressiveFn2;gpSkillSrvDoFnTable_6FD40A20[39]=SKILLS_SrvDo039_FistsOfFire_BladesOfIce_ProgressiveFn3;
 bool comma=false;printf("[");
 for(int family:{1,2,4})for(int count:{0,1,3})for(int rank:{1,20})for(int mask=0;mask<4;++mask)for(int gates=0;gates<16;++gates){
 row={};dragon={};dragon.dwCalc[0]=200;dragon.wSrvPrgFunc[0]=dragon.wSrvPrgFunc[1]=dragon.wSrvPrgFunc[2]=0;row.nPrgDamage=family;row.dwCalc[0]=family==1?50:0;
 row.wSrvPrgFunc[0]=family==4?143:0;row.wSrvPrgFunc[1]=family==4?38:0;row.wSrvPrgFunc[2]=family==4?39:0;
 list={count,count*50,rank,259,321};currentRank=rank;exists=count>0;enabled=true;selected=gates&1;dragonRecord=gates&2;inRange=gates&4;damageLookup=gates&8;record=coordinates=roomAvailable=true;owner={};owner.pSeed={7,0,0};postedAr=count*50;
 int started=SKILLS_SrvSt25_64_DragonClaw_MonFrenzy(&game,&owner,260,rank);if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,[",family,count,rank,mask,gates,started);
 for(int contact=0;contact<2;++contact){owner.dwSeqFrame=contact<<8;hit=mask&(1<<contact);saved={};drains=allocs=freed=fillCalls=filledED=0;explicitAr=-1;trace.clear();hits.clear();missiles.clear();int result=SKILLS_SrvDo046_DragonClaw(&game,&owner,260,rank);
 if(contact)printf(",");printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,[",result,int(bool(owner.dwFlags&UNITFLAG_SKSRVDOFUNC)),allocs,drains,fillCalls,filledED,explicitAr,saved.wResultFlags,saved.dwHitFlags,saved.dwPhysDamage,saved.dwFireDamage,saved.dwLifeLeech,saved.dwManaLeech,freed,int(exists));bool sep=false;for(int t:trace){if(sep)printf(",");sep=true;printf("%d",t);}printf("]]");
 }printf("]]");
 }printf("]\n");}
'''
    path = STAGE / 'dragon-claw-source-probe.cpp'
    path.write_text(base)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-omit-frame-pointer', '-fno-pie', '-no-pie', str(path), '-o', str(binary)], check=True)
    data = json.loads(subprocess.check_output([str(binary)]))
    assert len(data) == 1152
    for family, count, rank, mask, gates, started, contacts in data:
        assert started == bool(gates & 1)
        active = count > 0
        for contact, out in enumerate(contacts):
            result, flag, allocs, drains, fills, ed, ar, flags, hitflags, physical, fire, life, mana, freed, remains, trace = out
            target = bool(gates & 1)
            record = bool(gates & 2)
            success = target and record and bool(mask & (1 << contact))
            lookup = bool(gates & 8)
            release = success and lookup and bool(gates & 4) and active
            assert (result, flag, allocs, fills, ar) == (int(target), contact if target else 0, int(target and record), int(success), count * 50 + 40 + 25 * (rank - 1) if target and record else -1)
            assert ed == (50 + 15 * (rank - 1) + (count * 50 if active and family == 1 else 0) if success else 0)
            assert (flags, hitflags) == ((1 | (0x4000 if active and family == 4 else 0)) if success else 0, 2 if success else 0)
            assert physical == (25600 if success else 0) * (100 - min(100, 3 * rank) if active and family == 4 else 100) // 100
            assert fire == (rank * 100 + 25600 * min(100, 3 * rank) // 100 if success and active and family == 4 else 0)
            leech = 4 * rank * (2 if count == 3 else 1) if success and active and family == 2 else 0
            assert (life, mana) == (leech, leech if count >= 2 else 0)
            assert freed == int(release)
            area = release and family == 4 and count >= 2
            assert drains == int(target) + int(area)
            assert trace[:int(success)] == ([4] if success else [])
            assert trace[-int(target):] == ([1] if target else [])
            if release:
                active = False
            assert remains == active
    fixture = STAGE / 'dragon-claw-source-probe.json'
    fixture.write_text(json.dumps(data, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', talentId='assassin.dragonclaw', sourceId=260,
        legacyCommit=tree['sha'], sourceHashes=sources,
        compiledRoutines={k: hashlib.sha256(v.encode()).hexdigest() for k, v in blocks.items()},
        reusedComposition='tools/reference/martial-fire-release-source-evidence.json',
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(), cppSha256=hashlib.sha256(path.read_bytes()).hexdigest(), cases=len(data), contacts=2 * len(data), sanitizers='ASan+UBSan'),
        established=[
            'Complete start25 checks target existence only; record/range/charge state is not a start25 condition. Generic eligibility/payment wrappers are separate.',
            'Complete do46 invokes its complete packet builder on each contact, then queries combat, invokes full progressive release when a packet is returned, drains durability and sets the do-function flag from the saved contact ordinal. Missing target returns before all of that.',
            'Complete packet builder adds posted progressive AR to skill AR. Successful packets add full progressive Tiger ED and skill calc1 before FillDamageValues, then apply full progressive Cobra/Fists conversion helpers before AllocCombat. Misses still allocate combat.',
            'Under legacy full-list removal, a first successful in-range contact with returned combat removes the state before the next contact. A miss or failed lookup/range leaves it for the second. This does not select the current consume-one or guarantee/block policy.',
            'Fists secondary area callback can drain the parent before the outer do46 drain. This ordering is visible in the composed trace rather than silently deduplicated.'
        ],
        declaredServices=[
            'One progressive family at a time: Tiger/Cobra/Fists with count0/1/3, stored/live rank1/20. Full stacked ledger/state selection is not supplied by this bounded fixture.',
            'Result flags are injected success/miss per contact; current hit/defense/block rules are not compiled. Posted progressive AR is a declared stat, including after list removal, not a proven modern stat-removal lifetime.',
            'FillDamageValues asserts source128/flag0, captures pre-fill ED and supplies exactly100 physical in FP256 units. Weapon/stat/critical damage and native health/procs are separate; this proves ordering, not real full weapon damage.',
            'Formula service supplies current selected Dragon calc1 and declared Tiger50-per-charge/Cobra4-per-rank/Fists rank*100 elemental values. Exact current numeric getters remain separately pinned data/native work.',
            'Complete existing Fists progressive dispatcher and area/ground callbacks reuse their declared RNG/world/factory/health boundaries. Missing-row tests reset old combat; real stale combat allocation/lookup behavior is not inferred.',
            'Calls explicitly supply contact ordinals0/1; complete scheduler/selector composition is separate assassin-claw-sequence-source-evidence.json. Hand eligibility and actual both-hand stat posting are not certified.'
        ],
        currentReference=dict(officialPatchNotes='https://news.blizzard.com/en-us/article/23788293/diablo-ii-resurrected-patch-2-4-ladder-now-live',
            established='Released2.4 notes specify one charge per family and guaranteed hit with martial charges, and Dragon damage+15 per level. Current extracted table corroborates consumed1 and calc1. Exact multiple-contact consumption and block bypass are not settled by that wording.',
            stillUncertain=['Per-contact versus per-cast consumption in3.3.93847.', 'Whether charged guaranteed hit skips defense only or also dodge/block and which contact receives the guarantee.']),
        handlerEnabled=False, installed=False,
        limits=['Source preparation only; no native Dragon Claw skill is enabled.', 'Old full-list removal is deliberately retained in this oracle and must not be ported as the modern consume-one behavior.', 'Current executable/stat/rate/mode/seed/target/geometry/item/client acceptance and final effects/icons remain pending.'])
    out = ROOT / 'tools/reference/dragon-claw-source-evidence.json'
    encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert out.read_text() == encoded, 'Dragon Claw source evidence stale'
    else:
        out.write_text(encoded)
    print('Dragon Claw:1152 complete start/packet/do46/progressive compositions,2304 contacts pass; modern/native consumption remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
