#!/usr/bin/env python3
"""Re-execute accepted source binaries without rebuilding or mutating their oracles."""
import csv,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';REF=ROOT/'tools/reference'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def function(text,name):
 m=re.search(r'^(?:static |inline )?[A-Za-z_][^;\n]*\b'+re.escape(name)+r'\([^;{]*?\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[m.start():end]
def check():
 general=json.loads((REF/'npc-item-affix-engine-evidence.json').read_text());rare=json.loads((REF/'npc-rare-names-source-evidence.json').read_text())
 for e,cpp,key in [(general,BASE/'npc-item-affix-source-probe.cpp','compiledUnchangedRoutines'),(rare,BASE/'npc-rare-names-source/source.cpp','completeRoutineHashes')]:
  assert e['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416';text=cpp.read_text()
  for n,h in e[key].items():assert hashlib.sha256(function(text,n).encode()).hexdigest()==h,n
 for p,h in general['sourceHashes'].items():assert sha(BASE/'d2moo-pinned-source'/p)==h,p
 for p,h in rare['inputs'].items():assert sha(ROOT/p)==h,p
 for n,h in general['pinnedTableHashes'].items():assert sha(REF/'d2r-named-equipment/json'/(n+'.json'))==h,n
 for binary,cases in [(BASE/'npc-item-affix-source-probe',BASE/'npc-item-affix-source-probe.json'),(BASE/'npc-rare-names-source/probe',BASE/'npc-rare-names-source/cases.json')]:
  result=subprocess.run([str(binary)],capture_output=True);assert result.returncode==0 and not result.stderr,result.stderr.decode()
  canonical=json.dumps(json.loads(result.stdout),separators=(',',':'))+'\n';assert canonical==cases.read_text(),str(cases)
 assert sha(ROOT/general['fixture']['path'])==general['fixture']['sha256']
 for p,h in rare['artifacts'].items():assert sha(ROOT/p)==h,p
 source=REF/'item-generation';pins=json.loads((source/'sources.json').read_text());fields=0
 for name in ('rareprefix','raresuffix'):
  for extension in ('json','txt'):
   p=source/(name+'.'+extension);assert sha(p)==pins['files'][p.name]['sha256']
  rows=json.loads((source/(name+'.json')).read_text());table=list(csv.DictReader((source/(name+'.txt')).open(encoding='utf-8-sig'),delimiter='\t'))
  clean=lambda r:{str(k).casefold():str(v).strip().strip('"') for k,v in r.items() if k is not None}
  for index,row in rows.items():
   other=clean(table[int(index)])
   for k,v in clean(row).items():
    if k.startswith('*') or k=='linenumber':continue
    assert other.get(k)==v,(name,index,k,v,other.get(k));fields+=1
 assert fields==811
 print('NPC source controls:22432magic/14472rare cases reproduce from accepted sanitizer binaries;13unchanged complete routine copies,current pins and811rare table fields match. Read-only replay;no rebuild or oracle mutation.')
if __name__=='__main__':check()
