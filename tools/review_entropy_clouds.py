#!/usr/bin/env python3
"""Bind Entropy's separate cloud channels to the owned pinned missile table."""
import argparse,csv,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def review(check=False):
 p=ROOT/'tools/reference/d2r-local-missile-data-evidence.json';proof=json.loads(p.read_text());assert proof['referenceVersion']=='3.3.93847'and proof['comparison']['unexplainedDifferences']==0
 local=ROOT/proof['local']['path'];cached=ROOT/proof['cached']['path'];assert sha(local)==proof['local']['sha256']and sha(cached)==proof['cached']['sha256']
 rows=json.loads(cached.read_text());raw={r['Missile']:r for r in csv.DictReader(local.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t')if r.get('Missile')}
 runtime={r['key']:r['fields']for r in json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())}
 fields={}
 for key,params in [('miasmaboltcloud',['Param4','Param5']),('miasmachainscloud',['Param2','Param3'])]:
  names=['pSrvDoFunc','NextDelay','EType','Range','Radius',*params]+(['SrvCalc1']if key=='miasmachainscloud'else[])
  fields[key]={n:rows[key][n]for n in names}
  for n,v in fields[key].items():assert runtime[key][n]==v and str(v).strip('"')==raw[key][n].strip('"'),(key,n)
 assert fields['miasmachainscloud']['SrvCalc1']=="(skill('Enhanced Entropy'.blvl) >= 20)?4:((skill('Enhanced Entropy'.blvl)>=10)?3:2)"
 for row in fields.values():assert "'Enhanced Entropy'.lvl"in row['Range']and "'Enhanced Entropy'.lvl"in row['Radius']and row['pSrvDoFunc']==41 and row['NextDelay']==25
 result=dict(referenceVersion='3.3.93847',handlerEnabled=False,learnedCloudBehaviorVerified=False,processMemoryRead=False,sourceFields=fields,
  established=['Owned local missile705and696match the cached/runtime formula fields. Range uses effective Entropy rank with a100-frame bonus cap;Radius uses effective5/10thresholds. Chain SrvCalc1 separately uses hard10/20thresholds. These values must not be merged.',
   'Existing Miasma Bolt numerical packets reuse the common captured cloud shape. The raw Chain server calculation is retained as a separate output without assigning it an unverified geometry role.'],
  supplementary=dict(url='https://diabloforfans.com/skill/enhanced-entropy',reportedVersion='3.3.93847',scope='Independent data-miner description corroborates effective duration/radius and absence of damage scaling;it does not explain the separate hard-point Chain SrvCalc1.'),
  remaining=['Current do41 layer geometry,hit-delay identity,first/last contacts and Chain casting remain unresolved. Abyss sksrc lookup/owner capture remains separate;no skill() alias is inferred. Enhanced Entropy remains unavailable as a certified complete passive.'],
  inputs={str(q.relative_to(ROOT)):sha(q)for q in [p,local,cached,ROOT/'data/Sanctuary/Balance/necromancer-summons.json',ROOT/'tools/build_class_skills.py',Path(__file__).resolve()]})
 out=ROOT/'tools/reference/entropy-clouds-reference.json';encoded=json.dumps(result,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Entropy cloud reference stale'
 else:out.write_text(encoded)
 print('Owned Entropy cloud formulas:effective Range/Radius and distinct hard-only Chain SrvCalc1;unknown do41/Abyss context retained.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');review(p.parse_args().check)
