#!/usr/bin/env python3
"""Pin Levitation inputs and bounded reused references; no executable parity claim."""
import argparse,hashlib,json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build():
    source=ROOT/'tools/reference/d2r-named-equipment'
    manifest=json.loads((source/'sources.json').read_text())
    inputs={}
    def table(name):
        p=source/('json/'+name+'.json');assert sha(p)==manifest['files']['json/'+name+'.json']['sha256'];inputs[str(p.relative_to(ROOT))]=sha(p);return json.loads(p.read_text())
    row=next(r for r in table('skills').values() if r.get('*Id')==383)
    assert row['skill']=='Levitate' and row['passiveitype']=='weap' and row['passivereqweaponcount']==1
    fields={k:v for k,v in row.items() if k.startswith(('passive','Param')) or k in ('skill','*Id')}
    assert [row['passivecalc'+str(i)] for i in range(1,5)]==['dm12','"-1*min(ln78,50)"','ln56','ln34']
    weapons=table('weapons');bases=[]
    for code,strength,dexterity,outstr,outdex in [('flb',70,49,68,48),('cbw',25,35,24,34),('gsc',37,0,36,0)]:
        r=next(r for r in weapons.values() if r.get('code')==code);assert (r['reqstr'],r.get('reqdex',0))==(strength,dexterity)
        bases.append(dict(code=code,baseStrength=strength,baseDexterity=dexterity,observedStrength=outstr,observedDexterity=outdex))
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    blocks={}
    for name,functions in [('D2Skills.cpp',['D2Common_11024','SKILLS_GetWeaponMasteryBonus']),('Units/Missile.cpp',['MISSILE_HasBonusStats']),('Items/Items.cpp',['ITEMS_CheckRequirements'])]:
        relative='source/D2Common/src/'+name;p=ROOT/'.local/class-skills/d2moo-pinned-source'/relative;raw=p.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha'] for r in tree['tree'] if r['path']==relative)
        inputs[str(p.relative_to(ROOT))]=sha(p);text=raw.decode()
        for f in functions:
            m=re.search(r'^.*\b'+f+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m
            end=text.index('{',m.start())+1;depth=1
            while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
            blocks[f]=hashlib.sha256(text[m.start():end].encode()).hexdigest()
    for name in ['barbarian-mastery-engine-evidence.json','throwing-projectile-engine-evidence.json','warlock-foundation-preflight.json']:
        p=ROOT/'tools/reference'/name;inputs[str(p.relative_to(ROOT))]=sha(p)
    earlier=json.loads((ROOT/'tools/reference/throwing-projectile-engine-evidence.json').read_text())
    assert all(earlier['probe']['blockHashes'][n]==blocks[n] for n in ['D2Common_11024','SKILLS_GetWeaponMasteryBonus','MISSILE_HasBonusStats'])
    return dict(referenceVersion='3.3.93847',sourceCommit='fa1286949266498b294c40c2942521a7c0d7207a',legacyCommit=tree['sha'],skill=fields,
        observations=dict(effectiveRank=2,hardRank=1,reportedItems=['Ripsaw','Rogue Bow','Rusthandle'],normalWeaponRequirements=bases,
            unequippedCheck='Rusthandle shows the same36Strength while unarmed and equipped.',modifierCheck='User has no ethereal/requirements-minus example available; no additional observation assumed.',
            level='Rusthandle level18reported; raw unique17and Vengeance unlock18do not establish exact required-level override.'),
        reusedRoutineHashes=blocks,inputs=inputs,
        established=['Current four passive stats use weap type, one-weapon condition and effective rank. AR40+5/rank;damage25+4/rank;critical dm12(0,35);requirements -min(2*rank,50).',
            'Shared diminishing arithmetic remains unchanged. Current Dodge/Avoid/Evade observations support its early integer curve, but do not independently instrument modern Levitation critical rounding.',
            'Three current weapon requirement reports match basis + trunc(basis * -4 /100). Legacy item requirement percentage uses the same signed-truncation operation; its item-modifier/ethereal order is not assumed for the new Warlock stat.',
            'Reuse already compiled strongest-layer mastery and missile-critical references. Missile critical calls full weapon mastery after generic critical and Deadly Strike; weap can include bows. An explicit thrown used-skill route selects throw layers and skips melee layers. Native basic-throw adaptation follows that separate route.'],
        limits=['Reference preparation and reused bounded legacy routines do not certify the modern executable or every Warlock skill override.',
            'Ethereal/item-ease stacking is unresolved. The candidate explicitly rejects equipping those weapon combinations on a trained Warlock rather than silently choosing an order. This temporary restriction must be removed after resolving the mechanic.',
            'Innate two-hand/offhand weapon permissions, Eldritch-specific throwing overrides, other passives and all unimplemented talents remain separate. Rusthandle/Rogue Bow named effects remain gated.',
            'Final levitated weapon/class animation, sound and passive talent icon remain placeholders in the separate asset registers. No independent passive hotkey is applicable.'])
def main():
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');a=p.parse_args();out=ROOT/'tools/reference/levitation-mastery-reference.json';text=json.dumps(build(),indent=2)+'\n'
    if a.check:assert out.read_text()==text
    else:out.write_text(text)
    print('Pinned Levitation four-stat source, three current normal requirements and bounded reused consumer references.')
if __name__=='__main__':main()
