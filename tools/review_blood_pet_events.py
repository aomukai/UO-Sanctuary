#!/usr/bin/env python3
"""Pin existing Open Wounds/item-proc routes before Blood's remaining events."""
import argparse
import hashlib
import json
from pathlib import Path
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()


def record(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative = 'source/D2Game/src/SKILLS/SkillItem.cpp'
    source = STAGE / 'd2moo-pinned-source' / relative
    raw = source.read_bytes()
    blob = hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest()
    assert blob == next(row['sha'] for row in tree['tree'] if row['path'] == relative)
    names = ('SKILLITEM_CalculateOpenWoundsHpRegen', 'SKILLITEM_EventFunc15_OpenWounds',
             'SKILLITEM_EventFunc20_SkillOnAttackHitKill', 'SKILLITEM_CastSkillOnTarget',
             'SKILLITEM_CastSkillOnPosition', 'SKILLITEM_HandleItemEffectSkill')
    bodies = {name: function(raw.decode(), name) for name in names}
    wounds = bodies[names[1]]
    anchors = ('if (!pAttacker || !pUnit)', 'nChance <= 0', 'ITEMS_RollRandomNumber',
               'STAT_LEVEL', 'SKILLITEM_CalculateOpenWoundsHpRegen', 'pCurse.nDuration = 200;',
               'pCurse.nStat = STAT_HPREGEN;', 'pCurse.nState = STATE_OPENWOUNDS;', 'sub_6FD10EC0(&pCurse);')
    assert [wounds.index(s) for s in anchors] == sorted(wounds.index(s) for s in anchors)
    proc = bodies[names[2]]
    assert proc.index('pDamage && !(pDamage->dwHitFlags & 0x20)') < proc.index('ITEMS_RollRandomNumber')
    assert 'SKILLITEM_CastSkillOnTarget(pAttacker, v10, v9, pUnit, 1)' in proc
    assert 'STATES_CheckState(pUnit, STATE_UNINTERRUPTABLE)' in bodies[names[3]]
    registry = ROOT / 'data/Sanctuary/Balance/npc-item-combat-events.json'
    rows = json.loads(registry.read_text())
    wounds_row = next(r for r in rows if r['Stat'] == 135)
    assert wounds_row['Events'] == [dict(Event='domeleedamage', Function=15), dict(Event='domissiledamage', Function=15)]
    stats_path = ROOT / 'tools/reference/d2r-named-equipment/json/itemstatcost.json'
    stats = json.loads(stats_path.read_text())['item_skillonhit']
    assert [stats[k] for k in ('itemevent1', 'itemeventfunc1', 'itemevent2', 'itemeventfunc2')] == ['domeleedamage', 20, 'domissiledamage', 20]
    skills_path = ROOT / 'tools/reference/d2r-named-equipment/json/skills.json'
    tap = {k.lower(): v for k, v in json.loads(skills_path.read_text())['82'].items()}
    assert tap['skill'] == 'Life Tap' and tap['srvdofunc'] == 30 and tap['itemeffect'] == 1 and not tap.get('itemtgtdo')
    assert tap['auraeventfunc1'] == 5 and tap['auraevent1'] == 'damagedinmelee'
    paths = [Path(__file__), source, registry, stats_path, skills_path,
             ROOT / 'src/Sanctuary.Content/NpcCombatEvents.cs', ROOT / 'src/Sanctuary.Content/NpcOpenWounds.cs', ROOT / 'src/Sanctuary.Content/NamedCombat.cs',
             ROOT / 'src/Sanctuary.Content/TimedEffects.cs', ROOT / 'src/Sanctuary.Content/NecromancerCurseCasts.cs',
             ROOT / 'tools/reference/weapon-event-order-review.json', ROOT / 'tools/reference/life-tap-event-source-evidence.json']
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], sourceFile=relative,
        sourceSha256=sha(source), gitBlob=blob, installed=False, runtimeEventsEnabled=True,
        scope='Static complete-function/registration review; callback execution and native acceptance are recorded separately,not modern-engine proof.',
        npcOpenWoundsEnabled=True, npcLifeTapEnabled=False,
        functions={n: hashlib.sha256(b.encode()).hexdigest() for n, b in bodies.items()},
        inputs={str(p.relative_to(ROOT)): sha(p) for p in paths},
        established=[
            'Current itemstat rows register Open Wounds135/function15 and on-hit skill/function20 on melee/missile damage events. NPC registry now consumes OpenWounds135/function15through its real source event context;skill-on-hit/function20remains unsupported. Shared wound arithmetic/state and exact NPC regeneration are reused;separate source/native receipts establish their verification.',
            'Complete older Open Wounds callback draws once even at100percent,reads attacker level,applies target-type reductions and submits200frame negative HP regeneration to its state factory. It is not a post-hit life-loss percentage or a zero-RNG guaranteed hit.',
            'Complete older proc20checks source/presence/chance and packet hitflag0x20before drawing,decodes skill/rank and routes Life Tap through real attacker target casting. Current Life Tap row is ItemEffect1/non-ItemTgtDo/Do30with registered damagedinmelee healing5. Target-cast helper includes uninterruptible and item-effect routing.',
            'Existing damage ordering review places source domeleedamage and target damagedinmelee before life clipping/HP commitment;the retained Life Tap callback proof covers its fractional healing separately.'],
        remaining=[
            'Open Wounds callback/factory and regeneration have separate executed/native evidence. Next execute/compose complete proc target/item-effect routes for LifeTap5,with real pet source/owner allegiance,no player mana/gear context and area curse semantics.',
            'Subscription order/draws,miss/block/zero/full-absorb/lethal/state-replacement,first proccing swing healing,source death and save/removal behavior still need verified composition. Do not approximate these as an after-HP OnHit hook.',
            'Older source and static registry review do not establish modern executable equivalence. Blood hard10OpenWounds is now registered for the reviewed NPC path;hard20five-percent LifeTap remains pending. Full modern/fallback/PvP/world acceptance is not inferred from this static review.'])
    target = ROOT / 'tools/reference/blood-pet-event-reuse-review.json'
    rendered = json.dumps(result, indent=2) + '\n'
    if check:
        assert target.read_text() == rendered
    else:
        target.write_text(rendered)
    print('Blood event reuse:complete older callbacks/current registrations pinned;NPC OpenWounds enabled,LifeTap still pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
