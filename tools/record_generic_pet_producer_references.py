#!/usr/bin/env python3
"""Bind producer reference captures,including the bounded no-unwind lookup leaf."""
import argparse,json
from pathlib import Path
from probe_warlock_runtime import ROOT,STAGE,digest
LOCAL=ROOT/'.local/class-skills/generic-pet-search-producers'
def build(check=False):
 names=['callees-search-statmeta-idle-20261030.json','services-search-nodevalue-idle-20261030.json','services-search-missilemeta-idle-20261030.json','services-search-roomtrace-idle-20261030.json','services-search-neighbor-room-idle-20261030.json','services-search-statlookup-leaf-idle-20261030.json']
 artifacts=[Path(__file__).resolve()];records=[];total=0;maximum=0
 for name in names:
  p=STAGE/name;r=json.loads(p.read_text());artifacts.append(p);assert r['referenceVersion']=='3.3.93847' and r['executableSha256']=='e1f5436e3d9687f644ef16938b1b183d1fdef434f18cf66d852cf68f48cc8936'
  assert r.get('sourceReferencesVerified') or r.get('directCallTargetsVerified');assert r['bytesRead']<=8192 and not any(r[k] for k in ['processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten']);total+=r['bytesRead'];maximum=max(maximum,r['bytesRead'])
  for n,h in r['artifacts'].items():assert digest((ROOT/n).read_bytes())==h,n;artifacts.append(ROOT/n)
  for rec in r['records']:
   assert all(f['captured'] and not f['invalidInstructionMarkers'] for f in rec['fragments']);leaf=rec['rva']=='0x24a9d0'
   assert rec['allUnwindFragmentsCaptured']==(not leaf)
   if leaf:assert rec['boundedLeafReference'] and rec['bytes']==128 and rec['nextKnownUnwindStart']=='0x24aa50'
   records.append(dict(rva=rec['rva'],bytes=rec['bytes'],completeUnwind=not leaf,boundedLeaf=leaf))
 assert sum(r['bytes'] for r in records if r['completeUnwind'])==2025 and len(records)==6 and total==7666 and maximum==3570
 failed=LOCAL/'capture-statlookup.log';snapshot=LOCAL/'diagnostics/capture-tool-before-leaf.py';text=failed.read_text();assert 'unwind.fragments(rva)' in text and 'StopIteration' in text
 code=snapshot.read_text();assert code.index('reader.anchors()')<code.index('parts=unwind.fragments(rva)');assert 'finally:reader.close()' in code
 # The failed attempt reached unwind lookup only after both fixed anchors;
 # it sampled no code and did not persist a process identity or read receipt.
 artifacts += [failed,snapshot]
 result=dict(referenceVersion='3.3.93847',component='Producer code references;mechanical scope is recorded separately',processMemoryRead=True,processInput=False,ptraceAttach=False,securitySettingsChanged=False,gameMemoryWritten=False,handlerEnabled=False,
 verified=dict(completeFunctions=5,completeFunctionBytes=2025,boundedLeafBytes=128,leafCodeAndNopBytes=113,leafPaddingBytes=15,successfulCaptureReadBytes=7666,failedAttemptAnchorReadBytesInferred=560,totalReadBytesIncludingFailedAnchors=8226,maximumReadBytesPerCall=3570),records=records,
 failedAttempt=dict(reason='No primary unwind entry for the referenced lookup leaf',codeBytesRead=0,anchorBytesInferredFromPreservedToolControlFlow=560,processIdentityPersisted=False,logsPreserved=True),
 artifacts={str(p.relative_to(ROOT)):digest(p.read_bytes()) for p in sorted(set(artifacts))},remaining=['The lookup has no own unwind entry;its bounded128bytes end at the next known unwind start. Do not call it a complete-unwind function. Mechanical comparison,real source selectors and world/mask producers require separate evidence.'])
 p=ROOT/'tools/reference/generic-pet-producer-reference-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==encoded
 else:p.write_text(encoded)
 print('Producer references:5complete functions/2025bytes plus128bounded leaf bytes;7666captured read bytes +560failed-attempt anchors inferred;no input/attach/write.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
