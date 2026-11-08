#!/usr/bin/env python3
"""Record original prepared Sigil world/roster/UTC/saves and retained controls."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from record_inferno_held_input import preservation
from build_sigil_world_data import build
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/sigil-actor'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
 build(True)
 for name,n in [('sigil-tracking',111),('sigil-launch',126)]:
  p=ROOT/f'tools/reference/{name}-current-evidence.json';r=json.loads(p.read_text());assert r['cases']==n
  for rel,h in r['artifacts'].items():assert sha(ROOT/rel)==h,rel
 # Reuse the complete current factory capture;ordinary flags1 Range writes
 # signed-short total frames and float14=int(Range*40). This authenticates
 # the normal stationary lifetime projection,not the whole native factory.
 cp=ROOT/'.local/class-skills/warlock-runtime-reference/warlock-new-do-20261108/services-ring-missile-factory.json';capture=json.loads(cp.read_text());row=next(r for r in capture['records']if r['rva']=='0x400760');assert row['allUnwindFragmentsCaptured']
 f=row['fragments'][0];binary=next(ROOT/n for n,h in capture['artifacts'].items()if n.endswith('.bin')and h==f['sha256']);raw=binary.read_bytes();assert sha(binary)==f['sha256'];offset=0x400cc8-int(f['rva'],16);assert raw[offset:offset+19]==bytes.fromhex('438d0480c1e003660f6ec00f5bc0f30f114114')
 log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==21 and ': failed'not in log and ' ERR]'not in log
 for name in ['sigil-actor','sigil-tracking','sigil-launch']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
 assert counts['sigil-actor-check-result']==1 and counts['sigil-actor-reload-check-result']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-3.log').read_text()
 for kind in ['content','integration']:assert all(x in(STAGE/f'{kind}-build-3.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snap=json.loads((STAGE/'source-snapshot-6.json').read_text());assert native['copiedData']==snap['inputs']['copiedData']
 for rel,h in native['sourceInputs'].items():assert sha(ROOT/rel)==h,rel
 for rel,h in snap['inputs']['sourceInputs'].items():assert native['sourceInputs'][rel]==h,rel
 prior=json.loads((ROOT/'.local/class-skills/sigil-tracking/birth-inputs.json').read_text())['copiedData'];assert set(native['copiedData'])-set(prior)=={'Data/Sanctuary/Balance/sigil-world.json'}and all(native['copiedData'][k]==v for k,v in prior.items())
 saved=json.loads((STAGE/'sigil-actor-fixtures.json').read_text());assert saved['Generation']==2 and saved['Actor']['Rank']==200 and saved['Actor']['Frames']==5100
 assert int((STAGE/'sigil-tracking-fixtures.json').read_text())==2 and int((STAGE/'sigil-launch-fixtures.json').read_text())==2
 files=['src/Sanctuary.Content/SigilActors.cs','src/Sanctuary.Content/MissileValues.cs','data/Sanctuary/Balance/sigil-world.json','tests/Sanctuary.Integration/SigilActorChecks.cs','tests/check_sigil_actor.py','tools/build_sigil_world_data.py','tools/record_sigil_actor.py']
 result=dict(referenceVersion='3.3.93847',handlers=210,completedClasses=[],learnedSigilEnabled=False,worldAdapter='Prepared Lethargy only;all3owned missile tiers',nativeMarkers=21,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=3,sourceSnapshot=6,nativeRun=3,newCompilerMatrices=0,retainedSourceCases=dict(tracking=111,launch=126),factoryCaptureSha256=sha(cp),factoryLifetimeBlockSha256=hashlib.sha256(raw[offset:offset+19]).hexdigest(),artifacts=native['artifacts'],files={p:sha(ROOT/p)for p in files},preservation=preservation(),
 scope='Prepared original world marker composes current launch/tracking and existing do14driver. Nine owned rows/count742cached separately;existing missile expression engine reused through a fields overload. Native Warlock/Witch checks cover terrain-only placement beneath a monster,three Lethargy tiers/Range,actual items/labelled art,owner/gear snapshots,period6identity,facet/offline no-replay,normal expiry,lifetime-zero retirement on next tick,slot repair,failed placement/no resources,atomic malformed save refusal and original owner/marker/context/UTC/roster through two real reloads. One normal rank200204second marker is saved;no artificial lifetime hold.',
 clock='Saved owner-state205has a per-roster UTC epoch;integer25Hzframes preserve sort/expiry and float state arithmetic. Marker birth is quantized to that same frame so event12cannot predate a new marker by a partial frame. No health or mana recovery is attached to this state. Whole-room/source-coordinate geometry and current-frame parity remain host-adapter limits.',
 diagnostic='Native1passed before a review found partial-frame marker/state expiry skew. Native2passes the aligned birth invariant. A later source-mask review removed leap-only multi/mobile restrictions from placement;native3adds occupied-monster-cell casting and passes. Prior passed source/input/binary/log/fixture sets remain archived under native-1/native-2. No old compiler matrix was repeated.',
 remaining=['Lethargy numeric recipient stats/curse replacement,animation/movement/defense hooks and player SC/payment/cancellation/input still required before learned admission','Owner-state/event representation is a saved host adapter with no D2client list/event objects;exact source death-mask/callback parity remains unqualified. Current prepared world follows existing temporary actor policy and removes markers when the owner dies/is deleted;do not claim that as observed D2R parity','Whole current factory RNG/notification/unknown12ccontext and exact room/geometry/ordering/client parity are not certified by this specialized stationary marker adapter','Rancor Confuse61and Death reactive/execution/explosion remain separate from this Lethargy world host','Final ground/cast/pulse/recipient/expiry/audio and talent/hotkey icons remain separate placeholders'])
 p=ROOT/'tools/reference/sigil-actor-native-evidence.json';s=json.dumps(result,indent=2)+'\n'
 if check:assert p.read_text()==s
 else:p.write_text(s)
 print('Prepared Sigil world:21markers,actual original marker/two reloads,237retained current cases;occupied placement and frame alignment verified;no learned status route or installed/save change.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
