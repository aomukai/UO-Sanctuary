#!/usr/bin/env python3
"""Reuse the existing complete hit-success probe for NPC defense-item admission."""
import argparse,hashlib,json,shutil,subprocess
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'npc-defense-conditions-source'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def block(text,start):
    first=text.index('{',start);end=first+1;depth=1
    while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[first:end],end
def build(check=False):
    STAGE.mkdir(parents=True,exist_ok=True)
    prior_path=ROOT/'tools/reference/assassin-trap-attack-engine-evidence.json';prior=json.loads(prior_path.read_text())
    source=BASE/'d2moo-pinned-source'/prior['source']['path'];raw=source.read_bytes()
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==prior['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']==prior['source']['path'])
    assert sha(source)==prior['source']['sha256']
    body=function(raw.decode(),'SUNITDMG_IsHitSuccessful');assert hashlib.sha256(body.encode()).hexdigest()==prior['compiledUnchangedRoutine']['sha256']
    npc,end=block(body,body.index('if (pAttacker->dwUnitType != UNIT_PLAYER)'))
    player,_=block(body,body.index('else',end))
    reference=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((reference/'sources.json').read_text())['files'];tables={};pins={}
    for name in ('properties','itemstatcost'):
        p=reference/'json'/(name+'.json');assert sha(p)==manifest['json/'+name+'.json']['sha256'];pins[str(p.relative_to(ROOT))]=sha(p);tables[name]=json.loads(p.read_text())
    rules=[]
    for code,stat,identifier,symbol in (('ignore-ac','item_ignoretargetac',115,'STAT_ITEM_IGNORETARGETAC'),('reduce-ac','item_fractionaltargetac',116,'STAT_ITEM_FRACTIONALTARGETAC')):
        assert body.count(symbol)==1 and symbol in player and symbol not in npc
        p=tables['properties'][code];s=tables['itemstatcost'][stat]
        assert p['func1']==1 and p['stat1']==stat and not p.get('stat2')and s['*ID']==identifier and not s.get('itemevent1')and not s.get('itemevent2')
        rules.append(dict(property=code,stat=stat,id=identifier,function=1,registeredEvents=[]))
    original=BASE/'assassin-trap-attack-source-probe.cpp';text=original.read_text();assert body in text
    assert 'actor.stats[STAT_ITEM_IGNORETARGETAC]=1;actor.stats[STAT_ITEM_FRACTIONALTARGETAC]=99;'in text
    # Copy and execute the existing complete driver unchanged; add sanitizers.
    generated=STAGE/'probe.cpp';shutil.copy2(original,generated);assert sha(generated)==sha(original)
    binary=STAGE/'probe';r=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(binary)],capture_output=True)
    compiler=STAGE/'compiler.log';compiler.write_bytes(r.stdout+r.stderr);assert r.returncode==0,r.stderr.decode()[-3000:]
    r=subprocess.run([str(binary)],capture_output=True);sanitizer=STAGE/'sanitizer.log';sanitizer.write_bytes(r.stderr)
    assert r.returncode==0 and not r.stderr,r.stderr.decode()[-3000:]
    cases=json.loads(r.stdout);assert len(cases)==prior['fixture']['cases']==6804
    output=STAGE/'cases.json';output.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    old_fixture=ROOT/prior['fixture']['path'];assert sha(output)==sha(old_fixture)==prior['fixture']['sha256']
    result=dict(referenceVersion='D2R Reign of the Warlock 3.3.93847',legacyCommit=tree['sha'],cases=6804,
        inputs={str(source.relative_to(ROOT)):sha(source),str(prior_path.relative_to(ROOT)):sha(prior_path),str(original.relative_to(ROOT)):sha(original),str(old_fixture.relative_to(ROOT)):sha(old_fixture),**pins,str(Path(__file__).relative_to(ROOT)):sha(Path(__file__))},
        routineSha256=prior['compiledUnchangedRoutine']['sha256'],currentWriters=rules,
        artifacts={str(p.relative_to(ROOT)):sha(p)for p in (generated,binary,compiler,sanitizer,output)},
        established=['The complete unchanged hit-success routine reads raw115/116 exclusively in its player branch. The existing6804-case NPC driver deliberately supplies ignore-defense1 and fractional-defense99,without entering that branch.',
            'The exact existing driver reproduces every prior output under ASan/UBSan. Current owned properties map ignore-ac/reduce-ac through writer1 to115/116 with no ItemStatCost events. NPC registry admission should retain those stats without new hit modifiers or owner forwarding.'],
        limitations=['The driver uses declared stat/defense/classification/percentage/RNG/PMH services and is not execution of the complete current game. Current-executable NPC applicability remains unobserved.',
            'This does not replace current shared hit-chance rounding,geometry,blocking,source-seed or player special-recipient acceptance. Runtime tests must demonstrate field-specific inertness and real original-item admission separately.'])
    path=ROOT/'tools/reference/npc-defense-conditions-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('NPC defense conditions:existing6804complete hit-success cases reproduced unchanged under ASan/UBSan;current115/116writers pinned;player-only source branch retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
