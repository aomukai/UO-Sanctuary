#!/usr/bin/env python3
"""Execute the complete pinned flat-ATD callback with declared aggregate-stat/dispatch services."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative = 'source/D2Game/src/SKILLS/SkillItem.cpp'
    raw = (STAGE / 'd2moo-pinned-source' / relative).read_bytes()
    assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
    body = function(raw.decode(), 'SKILLITEM_EventFunc06_AttackerTakesDamage')
    data_root = ROOT / 'tools/reference/d2r-named-equipment'
    manifest = json.loads((data_root / 'sources.json').read_text())
    pins = {}
    for name in ['properties', 'itemstatcost']:
        path = data_root / 'json' / (name + '.json')
        assert sha(path) == manifest['files']['json/' + name + '.json']['sha256']
        pins[name] = sha(path)
    prop = json.loads((data_root / 'json/properties.json').read_text())['thorns']
    cost = json.loads((data_root / 'json/itemstatcost.json').read_text())['item_attackertakesdamage']
    assert prop['stat1'] == 'item_attackertakesdamage' and prop['func1'] == 1
    assert [cost[k] for k in ['*ID', 'fCallback', 'itemevent1', 'itemeventfunc1']] == [78, 1, 'attackedinmelee', 6]
    cpp = r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __fastcall
constexpr int UNITFLAG_CANBEATTACKED=1;
struct D2GameStrc{};
struct D2UnitStrc{int dwFlags=1,aggregate=0;}defender,attacker;
struct D2DamageStrc{int dwPhysDamage=0,dwHitClass=0;uint16_t wResultFlags=0;};
int queries=0,stat=-1,layer=-1,events=0,reactions=0,credit=0;D2DamageStrc packet;
int STATLIST_UnitGetItemStatOrSkillStatValue(D2UnitStrc*p,int s,int l){queries++;stat=s;layer=l;return p->aggregate;}
void SUNITDMG_ExecuteEvents(D2GameStrc*,D2UnitStrc*from,D2UnitStrc*to,int missile,D2DamageStrc*p){events++;credit=from==&defender&&to==&attacker&&missile==1;packet=*p;}
void SUNITDMG_ExecuteMissileDamage(D2GameStrc*,D2UnitStrc*,D2UnitStrc*,D2DamageStrc*){reactions++;}
'''
    cpp += body + r'''
int main(){D2GameStrc game;bool comma=false;printf("[");
for(int flat:{-4,0,1,4,32,37,347,379})for(int kind=0;kind<5;kind++){
defender={};attacker={};defender.aggregate=flat;attacker.dwFlags=kind==3?0:1;queries=events=reactions=credit=0;stat=layer=-1;packet={};
int result=SKILLITEM_EventFunc06_AttackerTakesDamage(&game,0,kind==1?nullptr:&defender,kind==2?nullptr:&attacker,nullptr,78<<16,kind==4?255:1);
if(comma)printf(",");comma=true;printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u]",flat,kind,result,queries,stat,layer,events,reactions,credit,packet.dwPhysDamage,packet.dwHitClass,packet.wResultFlags);
}printf("]\n");}
'''
    path = STAGE / 'flat-retaliation-source-probe.cpp'
    path.write_text(cpp)
    binary = path.with_suffix('')
    subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined', '-fno-sanitize-recover=all', str(path), '-o', str(binary)], check=True)
    result = subprocess.run([str(binary)], capture_output=True, check=True)
    assert not result.stderr, result.stderr.decode()
    rows = json.loads(result.stdout)
    assert len(rows) == 40
    for flat, kind, success, queries, stat, layer, events, reactions, credit, physical, hit_class, flags in rows:
        queried = kind in [0, 4]
        eligible = queried and flat > 0
        assert queries == int(queried) and stat == (78 if queried else -1) and layer == (78 << 16 if queried else -1)
        assert [success, events, reactions, credit, physical, hit_class, flags] == ([1, 1, 1, 1, flat << 8, 0x8D, 0x4021] if eligible else [0] * 7)
    fixture = STAGE / 'flat-retaliation-source-probe.json'
    fixture.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    evidence = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], primaryDataCommit=manifest['commit'],
        sourceFile=relative, sourceSha256=hashlib.sha256(raw).hexdigest(), unchangedRoutineSha256=hashlib.sha256(body.encode()).hexdigest(), primaryDataHashes=pins,
        fixture=dict(path=str(fixture.relative_to(ROOT)), sha256=sha(fixture), cases=len(rows), generatedSourceSha256=sha(path), sanitizers='ASan+UBSan'),
        established=[
            'Current property thorns maps to item_attackertakesdamage/stat78, whose callback is attackedinmelee/function6. Compatible item/gem/ordinary-affix values therefore belong in the same flat stat as Barbs and the flat part of Thorns.',
            'Complete callback executes40 contexts. It reads one supplied aggregate, ignores nonpositive values and invalid/null endpoints, and dispatches one physical aggregate<<8 packet with bearer credit, hitclass0x8D and result0x4021. Rank does not independently multiply that supplied aggregate.',
        ], boundedServices=[
            'Stat aggregation and full ExecuteEvents/ExecuteMissileDamage mitigation/callback/death behavior are recording services. This probe does not implement native aggregation or prove the current executable.',
            'A null incoming damage pointer is accepted by this callback. Whether miss/block/no-damage attempts dispatch it is a separate caller/event-registration contract, not established by these calls.',
            'Lightning retaliation/function10 is a separate stat/packet; Iron Maiden/function4 is an earlier attacker damage event. Neither should be silently folded into this physical flat stat.',
        ])
    output = ROOT / 'tools/reference/flat-retaliation-engine-evidence.json'
    rendered = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert output.read_text() == rendered
    else:
        output.write_text(rendered)
    print('Flat ATD:40 complete callback contexts pass ASan/UBSan; aggregate-stat lookup and full dispatch are bounded services.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
