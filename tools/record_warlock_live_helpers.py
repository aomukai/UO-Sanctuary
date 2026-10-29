#!/usr/bin/env python3
"""Verify bounded live-code captures offline; never open process memory here."""
import argparse,hashlib,json,re,struct,subprocess
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,Image
from record_shape_restrictions import frozen_receipt
CAPTURES=['event-bodies-35-36-37.json','event-bodies-39-40.json','generic-helpers-live-root-20261029.json','callees-live-actions-execute-20261029.json','callees-live-actions-targets-20261029.json','callees-live-actions-order-20261029.json','callees-live-actions-rating-20261029.json','callees-live-after-combat-actions-20261029.json','callees-live-after-combat-rating-20261029.json','event-bodies-37-36-35.json','event-bodies-40-39.json']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    image=Image();artifacts=set();visited=set()
    def add(path):
        artifacts.add(path)
        if path in visited:return
        visited.add(path)
        if path.suffix=='.json':
            try:r=json.loads(path.read_text())
            except (ValueError,UnicodeDecodeError):return
            if isinstance(r,dict):
                for name,digest in r.get('artifacts',{}).items():
                    p=ROOT/name;assert sha(p)==digest,name;add(p)
    complete={};incomplete=set();identities=[];total=0;new_records=[]
    for name in CAPTURES:
        p=STAGE/name;r=json.loads(p.read_text());add(p)
        assert r['referenceVersion']=='3.3.93847' and r['executableSha256']==image.ref['executable']['sha256']
        assert r['processMemoryRead'] and not any(r[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten'])
        assert r.get('immutableCandidateTableMatches') or r.get('directCallTargetsVerified')
        assert r['bytesRead']<=8192 and r['bytesRead']==2*sum(x['size'] for x in r['reads']);total+=r['bytesRead'];identities.append(r['process'])
        for x in r['reads']:
            assert x['stable'] and 'r' in x['permissions'] and x['size']<=4096
            if x['kind']=='code':assert 'x' in x['permissions']
            else:assert (x['kind'],x['rva'],x['size']) in [('header','0x0',64),('sequence-table','0x162ba30',216),('immutable-event-candidate-table','0x163a750',328)]
        for x in r['records']:
            assert x['allUnwindFragmentsCaptured']==all(f['captured'] for f in x['fragments'])
            if x['allUnwindFragmentsCaptured']:complete[x['rva']]=x
            else:incomplete.add(x['rva'])
        new_records.append(dict(capture=name,bytesRead=r['bytesRead'],complete=[x['rva'] for x in r['records'] if x['allUnwindFragmentsCaptured']],incomplete=[x['rva'] for x in r['records'] if not x['allUnwindFragmentsCaptured']]))
    assert all(x==identities[0] for x in identities) and total==33714
    assert len(complete)==18 and sum(x.get('bytes',x.get('totalBytes',0)) for x in complete.values())==12429
    unresolved=sorted(incomplete-complete.keys());assert unresolved==['0x44c330','0x44c6d0','0x44ca70','0x44cc40','0x4521d0','0x4b7100']
    for rva,x in complete.items():
        assert sum(f.get('invalidInstructionMarkers',0) for f in x['fragments'])==(1 if rva=='0x4b16c0' else 0)
    # This function's final24bytes are six DWORD RVAs, referenced by its
    # guarded indexed jump. Keep original full disassembly and derive a view
    # that stops before data; do not erase or patch captured bytes.
    binary=STAGE/'callees-live-actions-execute-20261029-4b16c0-part0.bin';data=binary.read_bytes();start=0x4b16c0;table=0x4b2044;off=table-start
    assert len(data)==2460 and off==2436 and data[off-2:off]==b'\xc3\x90'
    assert data[0x4b1818-start:0x4b181c-start]==bytes.fromhex('41 83 ff 05')
    assert data[0x4b1829-start:0x4b1831-start]==bytes.fromhex('42 8b 84 b9 44 20 4b 00')
    assert data[0x4b1831-start:0x4b1836-start]==bytes.fromhex('48 03 c1 ff e0')
    targets=struct.unpack_from('<6I',data,off);assert all(start<=t<table for t in targets)
    asm=subprocess.check_output(['objdump','-D','-b','binary','-m','i386:x86-64','-M','intel','--adjust-vma='+hex(image.base+start),'--stop-address='+hex(image.base+table),str(binary)],text=True)
    assert '(bad)' not in asm
    addresses={int(v,16)-image.base for v in re.findall(r'^\s*([0-9a-f]+):',asm,re.M)};assert set(targets)<=addresses
    derived=STAGE/'live-execute-code-only-20261029.asm'
    if check:assert derived.read_text()==asm
    else:assert not derived.exists() or derived.read_text()==asm;derived.write_text(asm)
    add(derived);add(Path(__file__).resolve());add(image.reference)
    _,native=frozen_receipt('db42371','tools/reference/item-property-writers-native-evidence.json');assert native['verified']['passMarkers']==219
    mainpath=ROOT/'tools/reference/creeper-frame-installation-evidence.json';main=json.loads(mainpath.read_text());assert len(main['installedArtifacts'])==106
    for name,digest in main['installedArtifacts'].items():assert sha(Path(main['runtimeRoot'])/name)==digest,name
    add(mainpath)
    result=dict(referenceVersion='3.3.93847',component='New live Warlock/GenericPet code reference',executableSha256=image.ref['executable']['sha256'],observedDate='2026-10-29',process=identities[0],readOnly=True,processMemoryReadDuringCapture=True,processMemoryReadDuringVerification=False,gameMemoryWritten=False,processInput=False,ptraceAttach=False,securitySettingsChanged=False,targetProcessCodeExecuted=False,offlineMachineCodeExecuted=False,semanticIdentitiesVerified=False,handlerEnabled=False,goalComplete=False,verified=dict(captures=len(CAPTURES),totalBytesRead=total,maximumBytesPerCapture=7834,completeHelpers=18,completeHelperBytes=12429,afterCombatNewCompleteHelpers=['0x4b54d0','0x4b56e0','0x4b5200','0x4b45e0'],remainingIncomplete=unresolved,inlineJumpTable=dict(functionRva=hex(start),tableRva=hex(table),entries=6,targets=[hex(t) for t in targets],guardedIndexMaximum=5,codeOnlyInvalidMarkers=0,fullDisassemblyInvalidMarkers=1),unchangedMainInstallationArtifacts=106,frozenNativeChecks=219),captures=new_records,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(artifacts)},frozenImplementation=dict(commit='db42371',receipt='tools/reference/item-property-writers-native-evidence.json'),established=['All captures revalidate the pinned executable,process identity,immutable image anchors and readable executable ranges. Each capture stays within8192bytes and reads twice for stability. Only narrowly identified code/immutable tables are sampled;there is no heap/save/authentication sampling,input,attach,security change or game-memory write.','Eighteen complete current helper bodies are preserved,including four newly readable after the user reported brief combat. Direct-call provenance remains distinct from semantic identity or dispatch verification. Protected event/proc pages and one ordering helper remain explicitly unavailable.','The single full-disassembly invalid marker in0x4b16c0falls inside a six-entry RVA jump table after the return. Guard/indexed-load/add/jump bytes and all six in-code targets are checked. A separate code-only disassembly has zero invalid markers. Original bytes/full disassembly remain unchanged;this is not full control-flow verification.'],remaining=['Offline helper identity,service binding,mechanical execution/source comparison and native integration are next. No new skill or complete AI/event/proc behavior is accepted from capture alone. Six incomplete code candidates remain unavailable;do not invent their mechanics. The overall eight-class goal remains active.'])
    out=ROOT/'tools/reference/warlock-live-helpers-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Live reference:18complete helpers/12429code bytes;four after combat;6unresolved;read-only/offline verification;no handler enabled.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
