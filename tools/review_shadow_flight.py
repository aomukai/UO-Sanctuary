#!/usr/bin/env python3
"""Review owned Shadow Flight data against retained complete source proofs."""
import argparse,csv,hashlib,json
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main(check=False):
 tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
 claw=ROOT/'tools/reference/shadow-claw-sequence-source-evidence.json';flight=ROOT/'tools/reference/dragon-flight-source-evidence.json'
 c=json.loads(claw.read_text());f=json.loads(flight.read_text());inputs={};routines={}
 for rel in ['.local/research/d2r-3.3.93847/monseq.txt','.local/research/d2r-3.3.93847/monstats.txt']:
  assert sha(ROOT/rel)==c['ownedInputs'][rel];inputs[rel]=sha(ROOT/rel)
 def body(rel,name,expected):
  p=BASE/'d2moo-pinned-source'/rel;raw=p.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==rel)
  text=function(raw.decode(),name);h=hashlib.sha256(text.encode()).hexdigest();assert h==expected,name
  inputs[str(p.relative_to(ROOT))]=sha(p);routines[name]=h;return text
 for rel,name in [('source/D2Common/src/D2Skills.cpp','SKILLS_GetSeqNumFromSkill'),('source/D2Common/src/DataTbls/SequenceTbls.cpp','DATATBLS_GetSeqRecordFromUnit'),('source/D2Common/src/Units/Units.cpp','UNITS_StopSequence'),('source/D2Game/src/MONSTER/MonsterMode.cpp','sub_6FC64E90')]:body(rel,name,c['routineHashes'][name])
 do=body('source/D2Game/src/SKILLS/SkillAss.cpp','SKILLS_SrvDo052_DragonFlight',f['compiledUnchangedRoutineHashes']['SKILLS_SrvDo052_DragonFlight'])
 assert '((pUnit->dwSeqFrame >> 8) % 2) != 0'in do
 for d in [c,f]:
  for rel,h in d['artifacts'].items():assert sha(ROOT/rel)==h,rel
 local=ROOT/'.local/research/d2r-3.3.93847'
 rows=[r for r in csv.DictReader((local/'monseq.txt').read_text().splitlines(),delimiter='\t')if r['sequence']=='seq_swdragonflight'];assert len(rows)==23
 data=[[{'SC':7,'A1':4}[r['mode']],int(r['frame']),int(r['dir']or 0),int(r['event']or 0)]for r in rows]
 assert [i for i,r in enumerate(data)if r[3]]==[9,14]
 links={r['Id']:[r['Skill5'],r['Sk5mode']]for r in csv.DictReader((local/'monstats.txt').read_text().splitlines(),delimiter='\t')if r['Id']in ['shadowwarrior','shadowmaster']}
 assert len(links)==2 and all(v==['Dragon Flight','seq_swdragonflight']for v in links.values())
 path=ROOT/'data/Sanctuary/Balance/npc-skill-metadata.json';meta=next(r for r in json.loads(path.read_text())if r['Id']==275);assert meta['Mode']==14;inputs[str(path.relative_to(ROOT))]=sha(path)
 path=ROOT/'data/Sanctuary/Balance/item-skill-execution.json';row=next(r for r in json.loads(path.read_text())['Skills']if r['Id']==275);assert row['Start']==12 and row['Do']==52 and row['Fields']['weapsel']==4 and row['Fields']['Kick']==1;inputs[str(path.relative_to(ROOT))]=sha(path)
 receipt=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],parent='e4d06ccd0c32f494e7e8718d03061f4b71300cb6',currentExecutableParity=False,newCompilerMatrices=0,actionEnabled=False,adaptationApproved=True,approval="Approve this Shadow Flight rule",inputs=inputs,completeReusedRoutineHashes=routines,retainedProofs={str(p.relative_to(ROOT)):sha(p)for p in [claw,flight]},slot5=links,sequence=data,events=[dict(rawPoint=i,olderDo52Branch='kick'if i%2 else'teleport')for i in [9,14]],
  inference='The retained ordinary NPC sequence callback does not perform the player ordinal rewrite. Composing that raw point with do52 implies kick at9 and teleport at14,contrary to intended Flight order. Successful tight contact can also set the server-do flag and suppress the later callback. This is a source/data inference,not a current executable observation or a newly compiled composition.',
  proposedAdaptation='Use the owned23-point sequence with explicit first-marker teleport and second-marker kick stages. Require actual successful landing before kick,retain the original target,spend no NPC/player mana,and reuse approved accuracy-only charged guarantee/block retention/per-successful-contact consume-one. Discard interrupted/pending action without replay. Native source-to-tile landing adapter remains explicit;do not call the player caster.',
  next='Implement the approved Shadow phase/landing adaptation using own sequence and shared packet/landing services. Player Flight approval does not establish NPC callback identity.')
 out=ROOT/'tools/reference/shadow-flight-source-review.json'
 if check:assert json.loads(out.read_text())==receipt;print('Shadow Flight source/data review matches.')
 else:out.write_text(json.dumps(receipt,indent=2)+'\n');print('Recorded owned Shadow Flight sequence/older callback phase conflict;no new compiler matrix or runtime enable.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');main(p.parse_args().check)
