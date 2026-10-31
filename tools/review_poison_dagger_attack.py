#!/usr/bin/env python3
"""Pin the complete source routines reviewed for Poison Dagger preparation."""
import argparse
import hashlib
import json
from pathlib import Path
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def build(check=False):
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    files={};routines={};bodies={}
    paths={
        'source/D2Game/src/SKILLS/SkillNec.cpp':['SKILLS_SrvSt16_PoisonDagger','SKILLS_SrvDo032_PoisonDagger'],
        'source/D2Game/src/SKILLS/Skills.cpp':['sub_6FD12950','D2GAME_SKILLS_Handler_6FD12BA0'],
        'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_GetResultFlags','SUNITDMG_AllocCombat','SUNITDMG_CalculateTotalDamage','SUNITDMG_ExecuteEvents','SUNITDMG_DrainItemDurability'],
        'source/D2Common/src/D2Skills.cpp':['SKILLS_GetToHitFactor']}
    for rel,names in paths.items():
        raw=(BASE/'d2moo-pinned-source'/rel).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==rel)
        files[rel]=hashlib.sha256(raw).hexdigest()
        for name in names:
            bodies[name]=function(raw.decode(),name)
            routines[name]=hashlib.sha256(bodies[name].encode()).hexdigest()
    start=bodies['SKILLS_SrvSt16_PoisonDagger'];release=bodies['SKILLS_SrvDo032_PoisonDagger']
    assert start.index('SUNITDMG_GetResultFlags')<start.index('D2GAME_RollElementalDamage_6FD14DD0')<start.index('SUNITDMG_AllocCombat')
    assert 'if (damage.wResultFlags & DAMAGERESULTFLAG_SUCCESSFULHIT)' in start
    assert 'nSrcDam = 0x80u;' in start and 'SUNITDMG_DrainItemDurability' in release
    wrapper=bodies['sub_6FD12950']
    assert wrapper.index('pfSkillStart(pGame, pUnit, nSkillId, nSkillLevel)')<wrapper.index('D2GAME_SKILLMANA_Consume_6FD10A50')
    assert 'SKILLSFLAGINDEX2_USEMANAONDO' in wrapper
    allocation=bodies['SUNITDMG_AllocCombat']
    assert 'pDamage->dwDmgTotal = pDamage->dwPhysDamage + pDamage->dwFireDamage + pDamage->dwLtngDamage + pDamage->dwMagDamage + pDamage->dwColdDamage + pDamage->dwPoisDamage;' in bodies['SUNITDMG_CalculateTotalDamage']
    assert allocation.index('SUNITDMG_FillDamageValues')<allocation.index('SUNITDMG_CalculateTotalDamage')<allocation.index('memcpy(&pCombat->tDamage')
    fields=ROOT/'tools/reference/d2r-named-equipment/json/skills.json'
    raw=fields.read_bytes();pins=json.loads((fields.parent.parent/'sources.json').read_text())
    assert hashlib.sha256(raw).hexdigest()==pins['files']['json/skills.json']['sha256']
    row=json.loads(raw)['73']
    assert (row['srvstfunc'],row['srvdofunc'],row['SrcDam'],row['AttackNoMana'])==(16,32,128,1)
    assert not row.get('usemanaondo',0) and not row.get('ToHitCalc')
    result=dict(referenceVersion='3.3.93847',talentId='necromancer.poisondagger',legacyCommit=tree['sha'],
        evidenceKind='Complete-routine source review; not a new compiled execution probe',sourceHashes=files,
        completeRoutineHashes=routines,pinnedSkillsSha256=hashlib.sha256(raw).hexdigest(),
        currentRow=dict(start=16,release=32,sourceDamage=128,attackNoMana=True,useManaOnDo=False,
            attackPercentBase=row['ToHit'],attackPercentPerRank=row['LevToHit']),
        conclusions=[
            'Start16 resolves the hit before allocating combat. Successful hits roll skill poison before ordinary weapon allocation. '
            'Allocation fills weapon damage,calculates total damage and stores a copy before the action event.',
            'The generic start wrapper consumes mana after a successful start unless UseManaOnDo or its explicit bypass applies. '
            'The pinned row has a real start callback and no UseManaOnDo flag. AttackNoMana is explicitly set.',
            'Do32 sets the server-do flag,fetches the target again and drains the stored combat record. '
            'DrainItemDurability checks current reach,delivers successful stored damage and updates player weapon durability; '
            'melee-attempt events also occur on stored misses/blocks.',
            'The complete total calculator includes mitigated poison rate once in immediate damage,even for player attackers. '
            'ExecuteEvents subtracts the stored total independently of later poison state ticks. Modern applicability remains unobserved.',
            'The existing shared GetResultFlags implementation and its16384compiled controls supply decision order. '
            'The player adapter supplies additive skill AR and the existing recipient shield rule.'],
        limits=[
            'This review hashes complete pinned source routines and checks specific source-order anchors. '
            'It does not execute the start/do wrappers or prove all their branches via a new compiler probe.',
            'Native weapon geometry,hostility,component-choice RNG,presentation and current executable behavior remain adapters. '
            'The broader player damage/event/return pipeline is not certified by this review.',
            'The full reviewed DrainItemDurability route includes attempt events,Thorns and later missile/free ordering. '
            'Their complete native player composition remains separate work; do not mark the class complete.'])
    output=ROOT/'tools/reference/poison-dagger-attack-source-review.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==text
    else:output.write_text(text)
    print('Poison Dagger complete source review pinned:prepared start,post-start mana,do32 contact;native event/return limits explicit.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
