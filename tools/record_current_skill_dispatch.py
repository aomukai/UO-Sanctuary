#!/usr/bin/env python3
"""Verify current NPC mode-to-skill table linkage separately from execution."""
import argparse,json,struct
from pathlib import Path
import skill_checkpoint_evidence as e
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';LIVE=BASE/'warlock-runtime-reference'
NAMES=['services-shadow-skill-entry-20261107.json','services-shadow-mode-row-20261107.json','shadow-mode-table-20261107.json','services-shadow-skill-start-20261107.json','services-shadow-skill-do-20261107.json','services-shadow-skill-dispatch-20261107.json']
def record(check=False):
    inputs={};captures=[]
    def pin(p):inputs[str(p.relative_to(ROOT))]=e.sha(p);return p.read_bytes()
    for name in NAMES:
        c=json.loads(pin(LIVE/name));assert c['referenceVersion']=='3.3.93847' and c['bytesRead']<=8192
        assert c['bytesRead']==2*sum(x['size']for x in c['reads'])
        assert not any(c[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
        for p,h in c['artifacts'].items():assert e.sha(ROOT/p)==h,p;pin(ROOT/p)
        assert all(r['allUnwindFragmentsCaptured']and all(f['invalidInstructionMarkers']==0 for f in r['fragments'])for r in c['records'])
        captures.append(c)
    assert all(c['process']==captures[0]['process'] and c['executableSha256']==captures[0]['executableSha256']for c in captures)
    assert captures[2]['tableEdgesVerified'] and captures[2]['sourceFunction']=='0x3dd650'
    current=json.loads(pin(BASE/'skill-do-dispatch-current/capture.json'))
    assert current['immutableTableMatches'] and current['executableSha256']==captures[0]['executableSha256']
    rawtable=pin(BASE/'skill-do-dispatch-current/candidate-table.bin');assert len(rawtable)==191*8
    words=struct.unpack('<191Q',rawtable);assert words[49]==0x140447730
    parent=json.loads(pin(ROOT/'tools/reference/shadow-current-birth-source-evidence.json'))
    assert parent['helperRva']=='0x4473c0' and parent['sourceContexts']==3456
    library=LIVE/'venv/lib/python3.12/site-packages/unicorn/lib/libunicorn.so.2'
    assert e.sha(library)==parent['inputs'][str(library.relative_to(ROOT))];pin(library)
    caller=json.loads(pin(LIVE/'current-shadow-do49-20261107.json'));assert caller['records']==[r for r in current['records']if r['slot']==49]
    for p,h in caller['artifacts'].items():assert e.sha(ROOT/p)==h,p;pin(ROOT/p)
    raw=pin(BASE/'defiler-compiled-formula/skills.bin');rows=json.loads(pin(ROOT/'tools/reference/d2r-named-equipment/json/skills.json'))
    assert len(raw)==4+429*748 and struct.unpack_from('<I',raw)[0]==429
    for i in range(429):
        b=4+i*748;r=rows[str(i)]
        assert struct.unpack_from('<h',raw,b+0x4e)[0]==r.get('srvdofunc',0)
        assert struct.unpack_from('<h',raw,b+0x20a)[0]==r.get('ItemEffect',0)
        assert bool(raw[b+0x28]&32)==bool(r.get('usemanaondo',0))
        assert 0<=r.get('ItemEffect',0)<=190
    assert all(rows[str(i)]['srvdofunc']==49 for i in [268,279])
    cases=json.loads(pin(BASE/'current-skill-dispatch-cases.json'));assert len(cases)==1440
    for n in ['probe_current_skill_dispatch.py','probe_shadow_skill_gateway.py','probe_generic_pet_decisions.py','record_current_skill_dispatch.py']:pin(ROOT/'tools'/n)
    for n in ['dispatch-probe-2.log','dispatch-check.log']:
        p=BASE/'skill-do-dispatch-current'/n;assert '1440contexts'in p.read_text();pin(p)
    pin(ROOT/'docs/current-skill-dispatch-checkpoint.md')
    result=dict(referenceVersion='3.3.93847',skillDoTableIdentityVerified=True,shadowEntryLinked=True,fullExecutorVerified=False,handlerEnabled=False,tableRva='0x163a8b0',ordinaryMaximumIndex=190,dispatchSliceContexts=1440,ownedFieldControls=1287,sourceCaptures=[dict(name=n,bytesRead=c['bytesRead'],process=c['process'])for n,c in zip(NAMES,captures)],established=['Preserved GenericPet action calls the captured NPC skill/mode entry. A captured lookup forms mode table+32*index;immutable table edges lead to the captured mode callbacks and skill wrapper. The wrapper directly calls the complete1642byte common executor.','Its executed dispatch slice loads srvdofunc at record0x4e,checks ordinary index<=190,conditionally calls the mana service,applies item/outgoing ItemEffect override and calls the indexed immutable table with original game/source/skill/rank. All429owned compiled records corroborate three consumed fields.','Shadow Warrior268 and Master279 select49;the verified table entry points to the preserved caller0x447730,which directly invokes the already verified stat helper0x4473c0. This closes table/slot linkage without asserting full permission/start/input/AI execution.'],limits=['Only the dispatch slice executes in1440controlled contexts. The full executor,actual NPC skill lists,resource/room/target services,mode/event ordering,original AI and learned casting remain unverified. Alternate indices use the trusted current catalogue range;no new source bounds check is invented.','Slow Missiles6 and Valkyrie16 callback bodies remain unreadable in the preserved capture;their table addresses alone do not establish modern behavior. No current game input,memory write,attach or security change occurred. No runtime C#/data or skill handler is changed by this reference.'],inputs=inputs)
    out=ROOT/'tools/reference/current-skill-dispatch-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Current table/Shadow slot linkage verified:1440slice controls/1287owned fields;full executor and learned actors remain separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
