#!/usr/bin/env python3
"""Pin direct weapon setup phases separately from the event registry."""
import argparse,hashlib,json
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
def build(check=False):
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    hashes={}
    def source(path):
        raw=(ROOT/'.local/class-skills/d2moo-pinned-source'/path).read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==path)
        hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
    dmg=source('source/D2Game/src/UNIT/SUnitDmg.cpp');units=source('source/D2Common/src/Units/Units.cpp')
    functions={n:function(t,n) for t,n in [(dmg,'SUNITDMG_IsHitSuccessful'),(dmg,'SUNITDMG_PreventMonsterHeal'),(dmg,'SUNITDMG_GetResultFlags'),(dmg,'SUNITDMG_FillDamageValues'),(units,'UNITS_GetDefense')]}
    hit=functions['SUNITDMG_IsHitSuccessful'];assert hit.index('if (nRand >= nChanceToHit)')<hit.index('SUNITDMG_PreventMonsterHeal(pAttacker, pDefender);')<hit.rindex('return 1;')
    flags=functions['SUNITDMG_GetResultFlags'];assert flags.index('SUNITDMG_IsHitSuccessful(')<flags.index('SUNITDMG_ApplyBlockOrDodge(')
    fill=functions['SUNITDMG_FillDamageValues'];assert 'STATLIST_SetUnitStat(pDefender, STAT_ARMORCLASS, nArmorClass, 0);' in fill and 'std::max(nDamageTargetAc +' in fill
    defense=functions['UNITS_GetDefense'];assert defense.index('STAT_ARMORCLASS')<defense.index('nDefenseBonus = nDefense * nArmorPercent')
    current=ROOT/'tools/reference/d2r-named-equipment/json/monstats.json';rows=json.loads(current.read_text());assert len(rows)==751
    immune=sorted(k for k,r in rows.items() if r.get('nopreventmonsterheal',0));assert len(immune)==9
    runtime=ROOT/'data/Sanctuary/Balance/prevent-heal-immune-monsters.json';assert json.loads(runtime.read_text())==immune
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],scope='Complete older-source function review plus current owned monster-table flags;not execution of the modern code or the full weapon event pipeline.',sourceHashes=hashes,
        functions={n:hashlib.sha256(t.encode()).hexdigest() for n,t in functions.items()},
        currentMonsterTable=dict(path=str(current.relative_to(ROOT)),sha256=hashlib.sha256(current.read_bytes()).hexdigest(),rows=751,preventHealImmune=immune),
        runtimeRules=dict(path=str(runtime.relative_to(ROOT)),sha256=hashlib.sha256(runtime.read_bytes()).hexdigest()),
        established=['Successful hit-roll resolution invokes PreventMonsterHeal before GetResultFlags tries block/dodge. Misses do not invoke it. Later interruption does not undo a completed start effect.',
            'FillDamageValues applies flat item_damagetargetac to target STAT_ARMORCLASS,clamped at zero,before the prepared packet is stored. A blocked/missed start does not fill the damage packet.',
            'GetDefense reads the mutated base armor before percent defense. The existing persistent flat-loss lane is reused;the prior final-defense subtraction was incorrectly outside the multiplier.',
            'Current nopreventmonsterheal flags supersede the older hard-coded Uber-only range. All751rows are checked;nine are immune,including colossal1/2/3.'],
        limits=['Other registered item events retain their prior callback/RNG order;this does not invent their registration chronology.',
            'Only Poison Dagger is migrated to direct setup phases. Other weapon routes retain their explicit legacy timing;positive target-armor changes remain unsupported.',
            'Native encounter/no-source creatures retain the existing prevent-heal eligibility policy. Full modern stat-graph/mutation,source RNG and runtime observation of block/start behavior remain open.'])
    path=ROOT/'tools/reference/weapon-start-source-review.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Direct weapon setup reviewed:successful roll before block;armor before percent;751current monster rules. Modern full event parity remains open.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
