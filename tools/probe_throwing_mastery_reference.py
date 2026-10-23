#!/usr/bin/env python3
"""Compile unchanged throw/melee mastery routing before reusing native consumers."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / '.local/class-skills'
tree = json.loads((STAGE / 'd2moo-source-tree.json').read_text())
assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
path = 'source/D2Common/src/D2Skills.cpp'
raw = (STAGE / 'd2moo-pinned-source' / path).read_bytes()
blob = next(v['sha'] for v in tree['tree'] if v['path'] == path)
assert hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest() == blob
source = raw.decode()
blocks = {}


def routine(name):
    match = re.search(r'^.*\b' + re.escape(name) + r'\([^;\n]*\)\s*\n\{', source, re.M)
    assert match, name
    end = source.index('{', match.start()) + 1
    depth = 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    blocks[name] = source[match.start():end]
    return blocks[name]


cpp = r'''#include <cstdint>
#include <cstdio>
#include <initializer_list>
#define __stdcall
#define ARRAY_SIZE(x) (sizeof(x)/sizeof(x[0]))
using BOOL=int;
constexpr int FALSE=0,TRUE=1,STAT_PASSIVE_MASTERY_MELEE_TH=0,STAT_PASSIVE_MASTERY_MELEE_DMG=1,STAT_PASSIVE_MASTERY_MELEE_CRIT=2,STAT_PASSIVE_MASTERY_THROW_TH=3,STAT_PASSIVE_MASTERY_THROW_DMG=4,STAT_PASSIVE_MASTERY_THROW_CRIT=5,ITEMTYPE_THROWN_WEAPON=42;
struct D2SkillsTxt{int nITypeA[1]={1},nRange=2;} row;
struct D2SkillStrc{D2SkillsTxt* pSkillsTxt=&row;} skill;
struct D2StatStrc{int nLayer=0,nValue=0;};
struct D2UnitStrc{int types=0;D2StatStrc layers[6][4]={};} actor,item;
bool used=true,throwable=true,typeMatch=true;
D2SkillStrc* UNITS_GetUsedSkill(D2UnitStrc*){return used?&skill:nullptr;}
bool ITEMS_CheckIfThrowable(D2UnitStrc*){return throwable;}
bool ITEMS_CheckType(int,int){return typeMatch;}
bool ITEMS_CheckItemTypeId(D2UnitStrc* p,int type){return (p->types&(1<<type))!=0;}
int STATLIST_CopyStats(D2UnitStrc* p,int stat,D2StatStrc* out,int){for(int i=0;i<4;i++)out[i]=p->layers[stat][i];return 4;}
'''
for name in ['D2Common_11024', 'SKILLS_GetWeaponMasteryBonus']:
    cpp += routine(name) + '\n'
cpp += r'''int main(){bool first=true;std::printf("{\"routingCases\":[");
for(int gates=0;gates<64;gates++)for(int types:{0,2,4,6})for(int stat:{0,1,2,3}){
 used=gates&1;throwable=gates&2;skill.pSkillsTxt=gates&4?&row:nullptr;row.nITypeA[0]=gates&8?1:0;typeMatch=gates&16;row.nRange=gates&32?2:1;item.types=types;
 for(int s=0;s<6;s++){actor.layers[s][0]={1,10+s};actor.layers[s][1]={1,30+s};actor.layers[s][2]={2,20+s};actor.layers[s][3]={3,999};}
 int has=99;int direct=D2Common_11024(&actor,&item,nullptr,stat,&has);int value=SKILLS_GetWeaponMasteryBonus(&actor,&item,nullptr,stat);
 if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",gates,types,stat,has,direct,value);
}
std::printf("],\"zeroThrowDoesNotFallBack\":[");
used=throwable=typeMatch=true;skill.pSkillsTxt=&row;row.nITypeA[0]=1;row.nRange=2;item.types=2;
for(int s=3;s<6;s++)for(auto& value:actor.layers[s])value={};
for(int stat=0;stat<3;stat++){if(stat)std::printf(",");std::printf("%d",SKILLS_GetWeaponMasteryBonus(&actor,&item,nullptr,stat));}
std::printf("],\"nullGuards\":[%d,%d]}\n",SKILLS_GetWeaponMasteryBonus(nullptr,&item,nullptr,0),SKILLS_GetWeaponMasteryBonus(&actor,nullptr,nullptr,0));}
'''
probe = STAGE / 'throwing-mastery-source-probe.cpp'
probe.write_text(cpp)
binary = STAGE / 'throwing-mastery-source-probe'
subprocess.run(['g++', '-std=c++20', '-O2', str(probe), '-o', str(binary)], check=True)
data = json.loads(subprocess.check_output([str(binary)]))
for gates, types, stat, has, direct, value in data['routingCases']:
    throwing = gates == 63 and stat < 3
    base = 30 if types & 2 else 20 if types & 4 else None
    expected_throw = 0 if not throwing or base is None else base + stat + 3
    expected = expected_throw if throwing else 0 if stat == 3 or base is None else base + stat
    assert (has, direct, value) == (int(throwing), expected_throw, expected)
assert data['zeroThrowDoesNotFallBack'] == [0, 0, 0]
assert data['nullGuards'] == [0, 0]
data_path = STAGE / 'throwing-mastery-source-probe.json'
data_path.write_text(json.dumps(data, indent=2) + '\n')

skill_path = ROOT / 'tools/reference/d2r-named-equipment/json/skills.json'
skill_row = next(v for v in json.loads(skill_path.read_text()).values() if v.get('skill') == 'Throwing Mastery')
assert [skill_row['passivestat' + str(n)] for n in range(1, 7)] == [
    'passive_mastery_throw_th', 'passive_mastery_throw_dmg', 'passive_mastery_throw_crit',
    'skill_pierce', 'passive_mastery_noconsume', 'passive_mastery_replenish_oncrit']
primary_url = 'https://news.blizzard.com/en-us/article/23788293/diablo-ii-resurrected-patch-2-4-ladder-now-live'
primary_path = STAGE / 'barbarian-2.4-primary.html'
if not primary_path.exists():
    primary_path.write_bytes(urllib.request.urlopen(primary_url, timeout=30).read())
primary = re.sub(r'<[^>]*>', ' ', primary_path.read_text())
primary = re.sub(r'\s+', ' ', primary)
for text in ['Critical strikes with throwing weapons now replenish quantity', 'Added chance to pierce', 'Find Potion synergy added: +5% Damage Taken per level']:
    assert text in primary, text
out = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'],
           status='Bounded selector proof and modern primary/table reference; native implementation and verification are tracked separately.',
           nativeEvidence='tools/reference/throwing-mastery-native-evidence.json',
           sourceHashes={path: hashlib.sha256(raw).hexdigest()},
           skillTableSha256=hashlib.sha256(skill_path.read_bytes()).hexdigest(), skill=skill_row,
           primary=dict(url=primary_url, cachePath=str(primary_path.relative_to(ROOT)), sha256=hashlib.sha256(primary_path.read_bytes()).hexdigest(),
                        scope='Released2.4 corroborates throwing AR44, pierce, conservation and critical quantity recovery; it does not define all modern event/RNG/snapshot semantics or supersede the pinned3.3.93847 values.'),
           probe=dict(caseCounts={k: len(v) for k, v in data.items()},
                      totalCases=sum(len(v) for v in data.values()), dataPath=str(data_path.relative_to(ROOT)),
                      blockHashes={k: hashlib.sha256(v.encode()).hexdigest() for k, v in blocks.items()}),
           established=[
               'Shared mastery routing first tests an actual throwable weapon, positive throwing item-type requirement on the used skill and skill range2. Only then does it select throw AR/damage/critical layers; ordinary melee use of a throwing base does not automatically receive Throwing Mastery.',
               'Each throw layer takes its strongest matching item-type value, never a sum. A valid throw route yielding zero suppresses melee fallback. The outer shared selector otherwise falls back to melee stat layers.',
               'Pinned modern skill has six passive consumers, not only AR/damage/critical: additive pierce, no-consume and critical-linked quantity recovery are also required. Current DM reference disagreement remains separate.'
           ],
           reusePlan=[
               dict(system='BarbarianMasteries/ClassItemTypes/ClassGear', use='Extend the existing eligibility/strongest bonus selector with an explicit throwing action context, preserving active/broken/backpack/native-base gates. Avoid assuming item category alone determines attack mode.'),
               dict(system='ClassStats/GearEffects/NamedCombat', use='Reuse additive AR/damage pools and ordinary critical/Deadly Strike exclusivity. Trace released projectile critical snapshot and RNG before quantity integration; do not silently reroll the linked critical for each pierced victim.'),
               dict(system='NamedProjectiles', use='Reuse traversal/collision helpers after checking source pierce timing/caps. Existing budget prerolls at most4 continuations; modern equivalence is unverified, so blindly adding the passive percentage is insufficient.'),
               dict(system='NamedAmmunition', use='Extend the current one-item saved quantity ledger and replenish schedule. Separate release conservation/critical recovery from on-hit damage. Never duplicate or split a named item to represent ammo.'),
               dict(system='ClassCombat/CombatTiming', use='Basic throw currently uses an equipped Throw family through shared attack; retain existing weapon timing and prepared packets where source contracts agree. Double Throw and dual wield need separate later consumers.')
           ],
           nonblockingQuestions=[
               'Modern conservation/recovery order, quantity restored per critical, recovery at cap/zero/ethereal and which critical sources qualify require authoritative event evidence; no Barbarian observation is requested from the user.',
               'Modern launch versus hit critical and piercing continuation reuse, hit/miss/no-target behavior, item swap/death/offline and saved projectile identity remain reference/test work.',
               'Native quantity currently tracks named throwing bases only; ordinary converted stack capacity and eligible native throwing materials need an explicit supported adapter, not unlimited implicit ammunition.'
           ],
           assets=dict(effect='Passive has no separate cast effect; reuse weapon projectile presentation. Final throwing projectile assets belong to attack skills.', icon='Separate talent-tree icon needed; no active hotkey icon for this passive.'),
           limits=['Declared used-skill/item-type/stat-layer services; this compiles the unchanged selector only. It does not execute modern critical, projectile, conservation, quantity recovery, or native item/state consumers.', 'This source probe does not enable or verify native handlers. See the separate native evidence; it changes no runtime/save format/shared DM formula and claims no class completion.'])
(ROOT / 'tools/reference/barbarian-throwing-mastery-preflight.json').write_text(json.dumps(out, indent=2) + '\n')
print('Throwing Mastery shared-selector cases:', sum(len(v) for v in data.values()))
