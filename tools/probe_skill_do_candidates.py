#!/usr/bin/env python3
"""Corroborate the pinned server-do table shape; map metadata only, no code reads."""
import argparse,json,re,struct
from pathlib import Path
from probe_warlock_runtime import ROOT,Image,digest,processes,identity
from pe_unwind_groups import UnwindGroups
OUT=ROOT/'tools/reference/skill-do-table-candidate-evidence.json'
def static(image):
    source=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/SKILLS/Skills.cpp'
    raw=source.read_bytes();tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text())
    import hashlib
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']=='source/D2Game/src/SKILLS/Skills.cpp')
    block=raw.decode().split('static const SkillDoFunc gpSkillSrvDoFnTable_6FD40A20[] =',1)[1].split('};',1)[0]
    names=re.findall(r'^\s*(nullptr|SKILLS_\w+)\s*,',block,re.M);assert len(names)==191 and [i for i,n in enumerate(names[:153]) if n=='nullptr']==[0]
    skills=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';rows=json.loads(skills.read_text());used=sorted({int(r.get('srvdofunc',0)) for r in rows.values()});assert max(used)==171
    lo=image.base+image.section(0x1000)['rva'];hi=lo+image.section(0x1000)['size'];found=[]
    for sec in [x for x in image.sections if x['name']=='.rdata']:
        blob=image.bytes(sec['rva'],sec['rawsize']);values=struct.unpack('<'+'Q'*(len(blob)//8),blob[:len(blob)//8*8])
        for i in range(len(values)-191):
            if values[i] or any(not lo<=values[i+j]<hi for j in range(1,172)) or any(values[i+j] for j in range(172,191)):continue
            found.append((sec['rva']+8*i,values[i:i+191]))
    assert len(found)==1 and found[0][0]==0x163a8b0
    table,words=found[0];groups=UnwindGroups(image);selected=[]
    for slot,ids in ((19,[41,230]),(20,[42]),(22,[44,48,92])):
        assert all(rows[str(i)]['srvdofunc']==slot for i in ids)
        entry=words[slot]-image.base
        selected.append(dict(slot=slot,sourceSkillIds=ids,candidateRva=hex(entry),fragments=[dict(rva=hex(a),endRva=hex(b),unwindRva=hex(u)) for a,b,u in groups.fragments(entry)]))
    return dict(tableCandidateRva=hex(table),uniqueShapeCandidates=1,classicNonzeroPrefixThrough=152,currentUsedMaximum=171,zeroTail=list(range(172,191)),selected=selected),source,skills

def record(check=False):
    image=Image();data,source,skills=static(image)
    if check:
        result=json.loads(OUT.read_text());assert result['staticCorroboration']==data and result['executableSha256']==image.ref['executable']['sha256']
        for p,h in result['artifacts'].items():assert digest((ROOT/p).read_bytes())==h,p
        assert all(result[k] is False for k in ('processMemoryRead','processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten','dispatcherIndexLinkVerified','callbackSemanticsVerified'))
        assert result['bytesRead']==0
    else:
        assert not OUT.exists(),'Preserve prior observations';ps=processes();assert len(ps)==1;process=ps[0];maps=[]
        for line in Path('/proc',str(process['pid']),'maps').read_text().splitlines():
            p=line.split();a,b=(int(x,16) for x in p[0].split('-'));maps.append((a,b,p[1]))
        observed=[]
        for row in data['selected']:
            fragments=[]
            for fragment in row['fragments']:
                lo=image.base+int(fragment['rva'],16);hi=image.base+int(fragment['endRva'],16)
                pieces=[(max(lo,a),min(hi,b),p) for a,b,p in maps if a<hi and b>lo];covered=sum(b-a for a,b,_ in pieces)==hi-lo
                fragments.append(dict(rva=fragment['rva'],mappingCoverageComplete=covered,permissions=[p for _,_,p in pieces],readableExecutable=covered and all('r' in p and 'x' in p for _,_,p in pieces)))
            observed.append(dict(slot=row['slot'],fragments=fragments,completeCandidateReadable=all(x['readableExecutable'] for x in fragments)))
        assert identity(process['pid'])==process
        files=[Path(__file__).resolve(),ROOT/'tools/probe_warlock_runtime.py',ROOT/'tools/pe_unwind_groups.py',source,skills,image.reference,ROOT/'.local/class-skills/d2moo-source-tree.json']
        result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],staticCorroboration=data,process=process,observedPermissions=observed,
            processMemoryRead=False,bytesRead=0,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,dispatcherIndexLinkVerified=False,callbackSemanticsVerified=False,
            artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in files},
            limits=['Unique table-shape corroboration and unwind bounds do not verify dispatch/index linkage or callback semantic identity.',
                'Only the pinned executable file and owned-process map metadata were read. No protected code,process heap or saves were read. No forced access or game input.',
                'Unavailability of candidate code leaves modern Static negative-resistance/floor/event order and Inferno cadence unresolved. Continue unaffected mechanics;this does not block the class roadmap.'])
        OUT.write_text(json.dumps(result,indent=2)+'\n')
    print('Pinned server-do candidate table corroborated;slots19/20/22 metadata recorded;no process memory/code read or semantic equivalence claimed.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
