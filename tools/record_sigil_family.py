#!/usr/bin/env python3
"""Authenticate shared Sigil world admission and retained learned Lethargy."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
from record_sigil_rancor_gap import record as gap
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/sigil-family'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 gap(True);proofs={}
 for name,n in [('sigil-lethargy-source-evidence',128),('sigil-tracking-current-evidence',111),('sigil-launch-current-evidence',126)]:
  p=ROOT/f'tools/reference/{name}.json';r=json.loads(p.read_text());assert r['cases']==n
  for rel,h in r['artifacts'].items():assert sha(ROOT/rel)==h,rel
  proofs[name]=sha(p)
 inventory=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());handlers=[r for r in inventory['skills']if r['implementation']=='handler-present']
 assert len(handlers)==211 and sum(r['characterClass']=='Warlock'for r in handlers)==2 and inventory['completedClasses']==[]
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log))
 assert sum(counts.values())==57 and ': failed'not in log and ' ERR]'not in log
 for name in ['sigil-family','sigil-cast','sigil-lethargy','sigil-actor','sigil-tracking','sigil-launch']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 for name in ['sigil-family','sigil-cast','sigil-lethargy','sigil-actor']:assert counts[name+'-check-result']==1 and counts[name+'-reload-check-result']==2
 for name in ['sigil-cast-left','sigil-cast-right','sigil-cast-quick']:assert counts[name+'-check']==3 and counts[name+'-result']==3 and log.count(name+': windup')==3
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-4.log').read_text()
 for kind,n in [('content',1),('integration',4)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-5.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 assert native['copiedData']==json.loads((ROOT/'.local/class-skills/sigil-cast/birth-inputs.json').read_text())['copiedData']
 saved=json.loads((STAGE/'sigil-family-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Entries'])==3
 assert len({e['Actor']['Ability']for e in saved['Entries']})==3 and all(e['Actor']['Rank']==200 and e['Actor']['Frames']==5100 for e in saved['Entries'])
 for name in ['sigil-cast','sigil-lethargy','sigil-actor']:assert json.loads((STAGE/(name+'-fixtures.json')).read_text())['Generation']==2
 paths=['src/Sanctuary.Content/SigilActors.cs','src/Sanctuary.Content/SigilLethargy.cs','tests/Sanctuary.Integration/SigilFamilyChecks.cs','tests/check_sigil_family.py','tools/record_sigil_family.py','tools/reference/sigil-rancor-reference-gap.json','tools/reference/sigil-lifecycle-approval.json']
 result=dict(referenceVersion='3.3.93847',handlers=211,warlockHandlers=2,completedClasses=[],learnedLethargyEnabled=True,learnedRancorEnabled=False,learnedDeathEnabled=False,currentExecutableParity=False,
  nativeMarkers=57,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=4,sourceSnapshot=5,nativeRun=4,newCompilerMatrices=0,newSourceEmulatorCases=0,retainedSourceCases=365,sourceEvidence=proofs,
  artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in paths},preservation=preservation(),
  scope='One shared saved host now supports all9 owned Sigil marker bodies and preserves captured ability,sourceId,tier/rank/level,do14 function30 versus61,position/clock/roster. Cross-family one-slot replacement and next-tick retirement retain reviewed launch/tracking. Each recipient subscribes only to its own ability;Rancor/Death cannot silently apply Lethargy. Prepared creation is free and unfinished learned handlers remain closed. Actual old version0 JSON without Ability restores as original Lethargy;cross-family/body mismatch refuses atomically. Three original normal rank200 markers,one per skill,retain identity/roster across two reloads and each resumes exactly one new pulse on reconnect,without duplicate-time replay. No marker lifetime holds. Existing actual left/right/quick Lethargy,resource/cancellation/recipient/stat controls and365 source comparisons remain passing.',
  diagnostic='Native1 reached all9 body/replacement controls then fixture save-owner corridor7180 exceeded map bounds. Integration2 uses in-map corridors;native2 passed birth,migration and all learned socket controls then exposed reuse of one authenticated NetState across owners without detachment. Integration3 detaches each fixture owner at birth/reload and adds explicit resumed-pulse assertions. Native3 then caught that one global Tick advances offline markers too;Integration4 tests each marker at its own next unprocessed six-frame boundary,without replaying elapsed offline pulses. Content1 is unchanged across these fixture-only corrections;failed source/input/log/binaries and available saved fixtures are archived in native-1/native-2/native-3.',
  remaining=['Rancor current do61 at454b50 has5 protected fragments;primary stat,75percent proc cadence/ownership,allied recipients,AI difficulty duration,state193/group8/curse interactions remain unverified. Keep learned route closed and move to unaffected Warlock work rather than polling unchanged code.','Death numeric/reactive/execution/explosion recipients remain unfinished;prepared marker is not a completed skill.','Existing whole-factory/unknown12c/room geometry/client/PvP acceptance limits remain explicit;no class certificate.','Final cast/ground/pulse/contact/expiry/audio and talent/hotkey icons remain separate pending assets with labelled placeholders.'])
 p=ROOT/'tools/reference/sigil-family-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Shared Sigils:57 native markers/2 reloads;all9 bodies,original saves and migration,retained learned Lethargy pass. 211 handlers;Rancor/Death recipient gates remain.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
