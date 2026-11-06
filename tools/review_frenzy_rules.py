#!/usr/bin/env python3
"""Bind the approved Frenzy contract and runtime fields to the owned pinned data."""
import argparse,csv,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def review(check=False):
 decision=ROOT/'tools/reference/frenzy-start-decision.json';d=json.loads(decision.read_text());assert d['approved']and d['answer']=='Approve this explicit Frenzy rule'and d['questionId']=='call_0f42547163074eceb1467ab3b7945b1c'
 proof=ROOT/'tools/reference/warlock-foundation-preflight.json';p=json.loads(proof.read_text());assert p['referenceVersion']=='3.3.93847'
 local=ROOT/'.local/research/d2r-3.3.93847/skills.txt';cached=ROOT/'tools/reference/d2r-named-equipment/json/skills.json'
 assert sha(local)==p['licensedSkillSha256']and sha(cached)==p['pinnedInputHashes']['skills']
 source=json.loads(cached.read_text())['147'];owned=next(r for r in csv.DictReader(local.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t')if r.get('skill')=='Frenzy')
 runtime=next(r for r in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text())if r['talentId']=='barbarian.frenzy')
 fields=runtime['sourceFields'];assert runtime['sourceId']==147 and source['skill']==fields['skill']=='Frenzy'
 assert {k:v for k,v in source.items()if isinstance(v,(int,str))and not k.startswith('*')and k!='lineNumber'}==fields
 for k,v in fields.items():assert str(v).strip('"')==owned[k].strip('"'),k
 assert [fields[k]for k in ['srvstfunc','srvdofunc','seqnum','weapsel','mana','manashift','lvlmana']]==[67,9,11,3,3,7,0]
 assert fields['itypea1']==fields['itypeb1']=='mele'and fields['SrcDam']==128 and fields['EType']=='mag'
 assert fields['auralencalc']=="par7+skill('Increased Stamina'.blvl)*10"and fields['aurastatcalc1']=='dm34'and fields['aurastatcalc2']=='dm56'
 assert fields['calc1']=="ln12+(skill('Double Swing'.blvl) + skill('Taunt'.blvl))*par8"and fields['calc4']=="skill('Berserk'.blvl)"
 sequence=ROOT/'tools/reference/double-swing-sequence-source-evidence.json';sq=json.loads(sequence.read_text());assert sq['referenceVersion']=='3.3.93847'and sq['sequence']==11 and sq['profiles']==14
 data=ROOT/'data/Sanctuary/Balance/double-swing-sequence.json';assert sha(data)==sq['artifacts'][str(data.relative_to(ROOT))]
 result=dict(referenceVersion='3.3.93847',approved=True,currentExecutableStartupParity=False,fieldsCompared=len(fields),sourceFields=fields,
  sharedSequence=dict(sequence=11,profiles=14,actionIndices=sq['actionIndices'],points=sq['points']),
  scope=['All runtime Frenzy fields match both the owned local skills table and its pinned cached counterpart. Current start67/do9/SQ11/two-melee filters,cost,AR/damage/hard synergies,magic conversion and aura fields stay separate from the approved startup/payment/charge timing.',
   'Existing current-file SQ11 data and shared ordinary contact/hand/geometry services are reused. Legacy do9controls remain historical service-bound evidence;they do not certify current startup or the approved new charge chronology.'],
  inputs={str(q.relative_to(ROOT)):sha(q)for q in [decision,proof,local,cached,ROOT/'data/Sanctuary/Balance/class-skills.json',sequence,data,Path(__file__).resolve()]})
 out=ROOT/'tools/reference/frenzy-rule-reference.json';text=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==text,'Frenzy rule reference stale'
 else:out.write_text(text)
 print('Owned Frenzy fields and current SQ11 match;approved startup/payment/charge timing remains explicitly separate from current code parity.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
