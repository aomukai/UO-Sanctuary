#!/usr/bin/env python3
"""Preserve one changed-process permission observation without opening game memory."""
import argparse
import json
from pathlib import Path
from probe_inferno_current_reference import static,table_check
from probe_warlock_runtime import ROOT,Image,digest,identity,processes

OUT=ROOT/'tools/reference/inferno-reference-context-2026-11-06.json'
PRIOR=ROOT/'tools/reference/inferno-current-reference-preflight.json'


def record(check=False):
    table_check(check=True);image=Image();candidate=static(image)
    if check:
        result=json.loads(OUT.read_text())
        assert result['candidate']==candidate and result['executableSha256']==image.ref['executable']['sha256']
        assert result['process']!=json.loads(PRIOR.read_text())['process']
        for path,sha in result['inputs'].items():assert digest((ROOT/path).read_bytes())==sha,path
        assert result['bytesRead']==0 and all(result[k]is False for k in ('processMemoryOpened','processInput','ptraceAttach','protectionsChanged','gameMemoryWritten','processImageVerified','callbackLinkVerified','completeCandidateReadable'))
    else:
        assert not OUT.exists(),'Preserve existing process observations'
        matches=processes();assert len(matches)==1,'One owned D2R process is required'
        process=matches[0];assert process!=json.loads(PRIOR.read_text())['process'],'No external process change; do not retry the prior blocker'
        mappings=[]
        for line in Path('/proc',str(process['pid']),'maps').read_text().splitlines():
            parts=line.split();lo,hi=(int(x,16)for x in parts[0].split('-'));mappings.append((lo,hi,parts[1]))
        observed=[]
        for fragment in candidate['fragments']:
            lo=image.base+int(fragment['rva'],16);hi=image.base+int(fragment['endRva'],16)
            regions=[dict(rva=hex(max(a,lo)-image.base),endRva=hex(min(b,hi)-image.base),permissions=p)for a,b,p in mappings if a<hi and b>lo]
            covered=sum(int(p['endRva'],16)-int(p['rva'],16)for p in regions)==hi-lo
            observed.append(dict(rva=fragment['rva'],endRva=fragment['endRva'],mappingCoverageComplete=covered,readableExecutable=covered and all('r'in p['permissions']and'x'in p['permissions']for p in regions),regions=regions))
        assert identity(process['pid'])==process,'Reference process changed during metadata inspection'
        assert not all(p['readableExecutable']for p in observed),'Reference availability changed; inspect the new result instead of recording the old blocker'
        paths=[Path(__file__).resolve(),PRIOR,image.reference,ROOT/'tools/probe_inferno_current_reference.py',ROOT/'tools/probe_warlock_runtime.py']
        result=dict(referenceVersion='3.3.93847',executableSha256=image.ref['executable']['sha256'],process=process,candidate=candidate,observedPermissions=observed,
            bytesRead=0,processMemoryOpened=False,processInput=False,ptraceAttach=False,protectionsChanged=False,gameMemoryWritten=False,processImageVerified=False,callbackLinkVerified=False,completeCandidateReadable=False,
            inputs={str(p.relative_to(ROOT)):digest(p.read_bytes())for p in paths},
            limits=['A different owned process justified one fresh permission check;the candidate still spans inaccessible mappings. No memory file was opened and no game input or writes occurred.',
                'Addresses come from the separately hashed pinned executable. This does not verify in-memory PE identity,dispatcher linkage or Inferno callback semantics.',
                'Modern accepted-input/payment/reentry chronology remains unresolved. A proposed Sanctuary policy must remain a candidate until explicitly approved.'])
        OUT.write_text(json.dumps(result,indent=2)+'\n')
    print('Changed Inferno process context recorded;candidate still incomplete;zero game-memory bytes read.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
