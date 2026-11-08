#!/usr/bin/env python3
"""Record original NPC kick packet composition and the complete rewind proof."""
import argparse,hashlib,json,re
from collections import Counter
from pathlib import Path
from probe_current_item_affixes import function
from record_shadow_kick import sources
from record_inferno_held_input import preservation
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-kick-packets'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source():
 reused=sources();tree=json.loads((BASE/'d2moo-source-tree.json').read_text())
 for name,field in [('npc-damage-fill-source-evidence','completeRoutineHashes'),('shadow-finisher-source-review','routines')]:
  path=ROOT/'tools/reference'/(name+'.json');d=json.loads(path.read_text());texts=[];reused['receipts'][str(path.relative_to(ROOT))]=sha(path)
  inputs=d.get('inputs')or{'.local/class-skills/d2moo-pinned-source/'+rel:h for rel,h in d['sourceHashes'].items()}
  for rel,h in inputs.items():
   p=ROOT/rel
   if name=='npc-damage-fill-source-evidence'and rel=='tools/probe_npc_damage_fill.py'and sha(p)!=h:
    successor=ROOT/'tools/reference/npc-blade-shield-fill-source-evidence.json';new=json.loads(successor.read_text())
    assert new['inputs'][rel]==sha(p)and new['completeRoutineHashes']==d[field]
    for artifact,value in new['artifacts'].items():assert sha(ROOT/artifact)==value
    reused['receipts'][str(successor.relative_to(ROOT))]=sha(successor)
    reused['generatorSuccessor']=dict(path=rel,historical=h,current=sha(p),reason='Later shield controls extended the generator;original5120 compiler/binary/cases remain unchanged and all six complete source bodies match.')
   else:assert sha(p)==h,rel
   if 'd2moo-pinned-source/'in rel:
    raw=p.read_bytes();r=rel.split('d2moo-pinned-source/',1)[1];assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==r)
    if p.suffix=='.cpp':texts.append(raw.decode())
  for symbol,h in d[field].items():
   found=[]
   for text in texts:
    if re.search(r'^.*\b'+re.escape(symbol)+r'\([^;\n]*\)\s*\n\{',text,re.M):found.append(hashlib.sha256(function(text,symbol).encode()).hexdigest())
   assert found==[h],symbol;reused['routines'][symbol]=h
  for rel,h in d.get('artifacts',{}).items():assert sha(ROOT/rel)==h,rel
 assert len(reused['routines'])==17
 path=ROOT/'tools/reference/shadow-kick-rewind-source-evidence.json';rewind=json.loads(path.read_text());assert rewind['contexts']==46 and len(rewind['ownedRows'])==3
 for rel,h in (rewind['inputs']|rewind['artifacts']).items():assert sha(ROOT/rel)==h,rel
 assert not(BASE/'shadow-kick-rewind-source/sanitizer.log').read_text()
 return dict(reused=reused,rewindReceipt=sha(path),newRewindControls=46)
def main(check=False):
 auth=source();log=(STAGE/'birth-native-checks.log').read_text();counts=Counter(re.findall(r'\] ([\w-]+): passed',log));assert sum(counts.values())==24 and ': failed'not in log and ' ERR]'not in log
 for family in ['shadow-kick-packets','shadow-kick','shadow-dragon-claw','shadow-finisher','npc-original-contact']:assert counts[family+'-check']==1 and counts[family+'-reload-check']==2
 assert 'pass across3boots/2reloads'in(STAGE/'native-run-2.log').read_text()
 for kind,n in [('content',3),('integration',4)]:assert all(x in(STAGE/f'{kind}-build-{n}.log').read_text()for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
 native=json.loads((STAGE/'birth-inputs.json').read_text());snapshot=json.loads((STAGE/'source-snapshot-5.json').read_text());built=json.loads((STAGE/'source-snapshot-4.json').read_text())
 for p,h in native['sourceInputs'].items():assert sha(ROOT/p)==h,p
 for p,h in snapshot['inputs']['sourceInputs'].items():assert native['sourceInputs'][p]==h,p
 assert {p:h for p,h in snapshot['inputs']['sourceInputs'].items()if not p.startswith('tests/')}=={p:h for p,h in built['inputs']['sourceInputs'].items()if not p.startswith('tests/')}
 assert snapshot['inputs']['copiedData']==built['inputs']['copiedData']
 saved=json.loads((STAGE/'shadow-kick-packets-fixtures.json').read_text());assert saved['Generation']==2 and len(saved['Bodies'])==2 and all(b['Pending']['Impact']['Packet']['HitFlags']==35 for b in saved['Bodies'])
 status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert sum(x['implementation']=='handler-present'for x in status['skills'])==208 and status['completedClasses']==[]
 files=[STAGE/n for n in ['source-snapshot-4.json','source-snapshot-5.json','birth-inputs.json','birth-native-checks.log','native-run-2.log','content-build-3.log','integration-build-4.log','shadow-kick-packets-fixtures.json','birth-inputs-failed-1.json','birth-native-checks-failed-1.log','native-run-1.log','content-build-1.log']]
 files+=[ROOT/p for p in ['tools/record_shadow_kick_packets.py','tools/probe_shadow_kick_rewind.py','tools/reference/shadow-kick-rewind-source-evidence.json','tests/check_shadow_kick_packets.py']]
 receipt=dict(parent='3d01c8bcc799b8dd9ef9bf20f051f2a1e63b79b6',referenceVersion='3.3.93847',handlers=208,assassinHandlers=28,completedClasses=[],learnedShadowsEnabled=False,intrinsicTalonConnected=False,
  authenticatedSource=auth,nativeMarkers=24,markers=dict(counts),boots=3,reloads=2,saves=3,contentBuild=3,integrationBuild=4,sourceSnapshot=5,contentUnchangedSinceSnapshot=4,
  artifacts=native['artifacts'],files={str(p.relative_to(ROOT)):sha(p)for p in files},preservation=preservation(),
  diagnostic='Initial content build had a with-initializer punctuation error,corrected before testing. First native run delivered HP but the replay fixture expected null instead of the shared routine throwing on a consumed contact;fixture corrected. Foreign-frame preflight and resource-ledger capture were tightened before rerun. Failed logs/inputs retained;no remaining native failure.',
  scope='Both original Shadows:own NPC body/boot roll plus full source128 kick flags3 typed fill,actual primary-only typed weapon context,weapon ED/ordinary critical exclusion,own actor RNG,shared six-family damage postfill without premature emission/consumption,miss path,ordered contact/no replay and synthetic prepared-packet restore with original actor/items/resources/wounds. Retained own kick capture/Dragon Claw/ordinary finishers and original Valkyrie contacts pass.17 previous complete routines authenticated without duplicate matrices;46 new complete ordinary percent-rewind source controls establish actual header byte0 and +4 kick/+13 end at speed256.',
  limits=['NPC start24 cached packet/do42 repeated action,progressive callback timing/consumption and actual world/AI Talon are not connected yet.','Saved synthetic prepared packet checks the existing contact format;it does not authorize replay of a persisted action windup.','Sequence rewind branch is unqualified;nonzero-header controls are counterfactual. Modern NPC/flat-kick double-add/event/removal/PvP boundaries remain uncertain.','Other intrinsic/death/learned Shadow gates and separate player mixed-finisher audit remain. Effects/icons stay separately inventoried.'])
 out=ROOT/'tools/reference/shadow-kick-packets-native-evidence.json'
 if check:assert json.loads(out.read_text())==receipt;print('Own NPC kick packet receipt matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded own NPC kick packet:24 native markers/two reloads,17 reused routines,46 new rewind controls;installed build/saves preserved.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
