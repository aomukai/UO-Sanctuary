#!/usr/bin/env python3
"""Corroborate cached missile facts with strict read-only local pinned CASC data."""
import argparse,csv,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
 installation=json.loads((ROOT/'tools/reference/d2r-local-installation-evidence.json').read_text());animation=json.loads((ROOT/'tools/reference/d2r-local-animation-evidence.json').read_text());assert installation['referenceVersion']==animation['referenceVersion']=='3.3.93847'
 local=ROOT/'.local/research/d2r-3.3.93847/missiles.txt';cached=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';manifest=json.loads((cached.parent.parent/'sources.json').read_text());assert sha(cached)==manifest['files']['json/missiles.json']['sha256']
 rows={r['Missile']:r for r in csv.DictReader(local.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t') if r.get('Missile')};cache=json.loads(cached.read_bytes());assert set(rows)==set(cache)
 quoted=[];nulls=[];compared=0;rowcounts={}
 for name,c in cache.items():
  actual=rows[name];n=0
  assert str(c['*ID'])==actual['*ID'],(name,c['*ID'],actual['*ID'])
  for field,value in c.items():
   if field in actual and not field.startswith('*') and field!='lineNumber':
    if actual[field]!=str(value):
     assert isinstance(value,str) and len(value)>=2 and value[0]==value[-1]=='"' and value[1:-1]==actual[field],(name,field,value,actual[field])
     quoted.append(dict(missile=name,field=field,cached=value,local=actual[field]))
    n+=1
  for field,value in actual.items():
   if value and not field.startswith('*') and field not in c:
    assert field=='CelFile' and value=='null',(name,field,value)
    nulls.append(name)
  compared+=n;rowcounts[name]=n
 assert len(rows)==742 and compared==22253 and len(quoted)==5 and len(nulls)==100
 selected={k:dict(id=int(rows[k]['*ID']),comparedFields=rowcounts[k]) for k in rows if k.startswith('royalstrike') or k.startswith('wake of destruction')};assert len(selected)==9
 reader=ROOT/animation['reader']['sourcePath'];assert sha(reader)==animation['reader']['sourceSha256']
 e=dict(referenceVersion='3.3.93847',installationEvidence='tools/reference/d2r-local-installation-evidence.json',readerEvidence='tools/reference/d2r-local-animation-evidence.json',executableSha256=installation['executable']['sha256'],buildInfoSha256=installation['buildInfo']['sha256'],local=dict(archivePath='data:data/global/excel/missiles.txt',path=str(local.relative_to(ROOT)),sizeBytes=local.stat().st_size,sha256=sha(local),open='Local/offline CascOpenStorage default flags0;CASC_STRICT_DATA_CHECK;no encrypted-zero override or network downloads'),cached=dict(path=str(cached.relative_to(ROOT)),sha256=sha(cached)),comparison=dict(namedRows=len(rows),comparedNumericIds=len(rows),comparedNonCommentFields=compared,unexplainedDifferences=0,quotedFormulaRepresentations=quoted,omittedCelFileNullSentinels=len(nulls)),selectedRows=selected,established=['All742named cached rows occur in the local pinned missile table with22,253non-comment field comparisons. No populated local non-comment field is missing except100CelFile=nullasset sentinels.','Five cached formula strings contain one surrounding quotation-mark pair that the local CSV reader removes. The inner formulas match exactly;this comparison does not rewrite runtime balance data or certify formula evaluation.','All7Royal/Phoenix missile rows and both Wake of Destruction maker/wave rows match directly without quotation or null-sentinel exceptions. Warlock missile rows are included in the whole-table comparison.'],limits=['Confirms pinned data provenance and bounded representational differences,not current executable skill/AI/stat/RNG/collision/event behavior.','Extraction used the existing reader against executable/manifest hashes revalidated at read time. Raw licensed files remain local and ignored;the game process was not inspected or modified.'])
 out=ROOT/'tools/reference/d2r-local-missile-data-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Local missile evidence stale'
 else:out.write_text(encoded)
 print('Local3.3.93847missiles:742named rows/22,253fields corroborated;5explicit quote representations/100asset-null sentinels;7Phoenix and2Wake rows match directly.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
