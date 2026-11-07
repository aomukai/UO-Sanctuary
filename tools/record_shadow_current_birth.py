#!/usr/bin/env python3
"""Bind current captured summon setup, owned compiled fields and offline proof."""
import argparse,json,struct
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_inferno_held_input import preservation
ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';CAP=BASE/'skill-do-dispatch-current';LIVE=BASE/'warlock-runtime-reference'

def record(check=False):
    inputs={}
    def pin(p):
        inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return p.read_bytes()
    captures=[]
    for p in [CAP/'capture.json',LIVE/'current-shadow-do49-20261107.json',LIVE/'callees-shadow-init-20261107.json']:
        c=json.loads(pin(p));assert c['referenceVersion']=='3.3.93847' and c['bytesRead']<=8192
        assert c['bytesRead']==2*sum(r['size']for r in c['reads'])
        for read in c['reads']:
            assert read['stable'] and 'r' in read['permissions'] and 0<read['size']<=4096
            if read['kind']=='code':assert 'x' in read['permissions']
        assert not any(c[k]for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
        for n,h in c['artifacts'].items():assert e.sha(ROOT/n)==h,n;pin(ROOT/n)
        captures.append(c)
    assert all(c['process']==captures[0]['process'] for c in captures)
    assert all(c['executableSha256']==captures[0]['executableSha256'] for c in captures)
    assert captures[0]['tableRva']=='0x163a8b0' and captures[0]['sampledSlots']==191
    assert not captures[0]['dispatchIdentityVerified']
    assert captures[1]['records']==[r for r in captures[0]['records']if r['slot']==49]
    helper=captures[2]['records'][0]
    assert helper['rva']=='0x4473c0' and helper['bytes']==872 and helper['allUnwindFragmentsCaptured'] and len(helper['fragments'])==4
    cases=json.loads(pin(BASE/'shadow-current-birth-cases.json'));assert len(cases)==3456
    compiled=BASE/'defiler-compiled-formula';extraction=json.loads(pin(compiled/'extraction.json'))
    assert extraction['referenceVersion']=='3.3.93847' and not extraction['processMemoryRead'] and not extraction['gameFilesWritten']
    assert extraction['executableSha256']==captures[0]['executableSha256']
    for name,meta in extraction['files'].items():assert e.sha(compiled/name)==meta['sha256']
    raw=pin(compiled/'skills.bin');code=pin(compiled/'skillscode.bin')
    assert struct.unpack_from('<I',raw)[0]==429 and len(raw)==4+429*748
    manifest=json.loads(pin(ROOT/'tools/reference/d2r-named-equipment/sources.json'))
    path=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';assert e.sha(path)==manifest['files']['json/skills.json']['sha256'];skills=json.loads(pin(path))
    stats={r['Name']:r['Id']for r in json.loads(pin(ROOT/'data/Sanctuary/Balance/equipment-stat-costs.json'))};matches=0
    for i in range(429):
        b=4+i*748;row=skills[str(i)];assert struct.unpack_from('<h',raw,b)[0]==i
        for prefix,calc,offset,formula,count in [('aurastat','aurastatcalc',0x74,0x88,6),('passivestat','passivecalc',0xba,0xd8,14)]:
            for k in range(count):
                value=struct.unpack_from('<h',raw,b+offset+2*k)[0];assert value==stats.get(row.get(prefix+str(k+1)),-1),(i,k,prefix)
                at=struct.unpack_from('<i',raw,b+formula+4*k)[0]
                assert (at>=0)==(row.get(calc+str(k+1)) is not None) and (at==-1 or 0<=at<len(code))
                matches+=1
    assert matches==8580
    for n in ['probe_skill_do_dispatch.py','probe_shadow_current_birth.py','probe_generic_pet_decisions.py','capture_reference_callees.py','record_shadow_current_birth.py']:pin(ROOT/'tools'/n)
    pin(LIVE/'venv/lib/python3.12/site-packages/unicorn/lib/libunicorn.so.2')
    for n in ['capture-check.log','helper-probe-2.log','helper-check.log']:pin(CAP/n)
    assert '3456 complete offline contexts' in (CAP/'helper-probe-2.log').read_text() and '3456 complete offline contexts' in (CAP/'helper-check.log').read_text()
    parent=json.loads(pin(ROOT/'tools/reference/valkyrie-cast-native-evidence.json'))
    snapshot(BASE/'valkyrie-cast/source-snapshot-8.json');pin(BASE/'valkyrie-cast/source-snapshot-8.json')
    for kind in ['Content','Integration']:
        path=BASE/'valkyrie-cast'/kind.lower()/f'Sanctuary.{kind}.dll';assert e.sha(path)==parent['artifacts']['Assemblies/'+path.name]
    protected=preservation();assert not protected['installed']
    client=json.loads(pin(ROOT/'tools/reference/inferno-held-input-client-evidence.json'))
    assert len(client['patchHashes'])==41
    for group in ['patchHashes','sharedInputs']:
        for name,h in client[group].items():assert e.sha(ROOT/name)==h,name;pin(ROOT/name)
    for n in ['shadow-current-birth-checkpoint.md','slow-missiles-windup-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-mechanical-reference.md','skill-effects.md','skill-icons.md']:pin(ROOT/'docs'/n)
    result=dict(referenceVersion='3.3.93847',parent='a10e7c08be094547d7a51b37128a1686468a592c',sourceOnly=True,handlerEnabled=False,handlers=203,completedClasses=[],dispatchIdentityVerified=False,helperRva='0x4473c0',helperBytes=872,completeUnwindFragments=4,sourceContexts=3456,ownedCompiledStatSlots=8580,formulaPresenceControls=8580,candidateTableRva='0x163a8b0',sampledTableSlots=191,sourceCaptures=[dict(process=c['process'],bytesRead=c['bytesRead'])for c in [captures[0],captures[2]]],protection=protected,established=['The captured candidate caller references this helper directly. All four helper fragments execute offline against explicit stat/formula/allocation services. Captured game instructions run only in Unicorn;no calls are made into the live game from the host.','Rank below1 exits;rank1 also initializes. Six aura and fourteen passive slots advance both stat and formula indices. Formula services receive the owner for stat slots and the pet for summon-modifier calculation;valid modifier results1..44 are assigned only after successful list allocation.','Owned CASC skills.bin confirms all429 records and8580 aura/passive stat offsets with matching formula presence/bounds. Sentinel cases distinguish every slot/context,invalid stat IDs,allocation failure,all supported table selectors and signed percentage precision/overflow boundaries. Existing SourcePercentage is the next reuse candidate.'],limits=['The table pattern and caller structure corroborate the proposed skill-do identity; the actual indirect dispatcher/table bound remains unproven. Sampled191slots is not a current table-length certificate. Slow Missiles6 and Valkyrie16 candidates remain unreadable,with no forced access.','Formula evaluation,stat-list allocation/posting/writes and modifier assignment are declared services;their full current implementation and real Shadow spawn/AI/equipment/combat/casting/payment/input/saves remain unimplemented. The 512-stat test service is an explicit fixture count,not a recovered current table size.','No production C#,runtime data,handler,installed build/save or client changed. Effects and talent/hotkey icons remain separately listed.'],inputs=inputs)
    out=ROOT/'tools/reference/shadow-current-birth-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Current summon helper:3456 offline cases,8580 owned compiled slots;production unchanged;203handlers;dispatcher/learned Shadows still gated.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
