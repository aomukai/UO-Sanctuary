#!/usr/bin/env python3
"""Compare independent Blizzard historical tables with decompiled curve arithmetic."""
from pathlib import Path
from html.parser import HTMLParser
import datetime,hashlib,json,re,subprocess,urllib.request
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills';CACHE=STAGE/'diminishing-primary';CACHE.mkdir(exist_ok=True)
class Rows(HTMLParser):
 def __init__(self):super().__init__();self.rows=[];self.cells=[];self.cell=None
 def handle_starttag(self,tag,attrs):
  if tag=='tr':self.cells=[]
  if tag in ('td','th'):self.cell=[]
 def handle_data(self,text):
  if self.cell is not None:self.cell.append(text)
 def handle_endtag(self,tag):
  if tag in ('td','th') and self.cell is not None:self.cells.append(' '.join(''.join(self.cell).split()));self.cell=None
  if tag=='tr' and self.cells:self.rows.append(self.cells);self.cells=[]
curves=[('Summon Resist','necromancer-summoning','Resist All %',[28,34,39,44,47,50,52,54,56,57,59,60,61,62,62,64,64,65,65,66]),('Natural Resistance','barbarian-combatmasteries','Increase All Resistances %',[12,21,28,35,40,44,47,49,52,54,56,58,60,61,62,64,64,65,66,67]),('Increased Speed','barbarian-combatmasteries','Walk/Run Speed Bonus %',[13,18,22,25,28,30,32,33,35,36,37,38,39,40,40,41,41,42,42,43])]
sourcePages={}
for slug in {row[1] for row in curves}:
 path=CACHE/(slug+'.html');url='https://classic.battle.net/diablo2exp/skills/'+slug+'.shtml'
 if not path.exists():path.write_bytes(urllib.request.urlopen(url,timeout=30).read())
 raw=path.read_bytes();parser=Rows();parser.feed(raw.decode('utf-8',errors='replace'));sourcePages[slug]=dict(url=url,sha256=hashlib.sha256(raw).hexdigest(),cachePath=str(path.relative_to(ROOT)),rows=parser.rows)
modernPath=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';modern={r['skill']:r for r in json.loads(modernPath.read_text()).values() if r.get('skill')}
result=[]
for skill,slug,label,expected in curves:
 rows=[row for row in sourcePages[slug]['rows'] if row[0]==label];assert len(rows)==1,(label,rows)
 actual=[int(re.sub(r'[^\d-]','',x)) for x in rows[0][1:]];assert actual==expected,(skill,actual)
 row=modern[skill];a,b=row.get('Param1',0),row.get('Param2',0);native=[a+(b-a)*min(100,110*n//(n+6))//100 for n in range(1,21)];assert native==expected,skill
 result.append(dict(skill=skill,source=sourcePages[slug]['url'],label=label,parameter1=a,parameter2=b,values=expected))
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';p='source/D2Common/src/D2Skills.cpp';raw=(STAGE/'d2moo-pinned-source'/p).read_bytes();blob=next(x['sha'] for x in tree['tree'] if x['path']==p);assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blob
text=raw.decode();start=text.index('int __stdcall D2Common_11033(');end=text.index('\n}',start)+2;body=text[start:end]
cpp='#include <algorithm>\n#include <cstdio>\n#define __stdcall\n'+body+'\nint main(){bool first=true;std::printf("[");'
for i,row in enumerate(result):cpp+='for(int n=1;n<=20;n++){if(!first)std::printf(",");first=false;std::printf("['+str(i)+',%d,%d]",n,D2Common_11033(n,'+str(row['parameter1'])+','+str(row['parameter2'])+'));}'
cpp+='std::printf("]\\n");}\n';path=STAGE/'diminishing-conflict-source.cpp';path.write_text(cpp);binary=STAGE/'diminishing-conflict-source';subprocess.run(['g++','-std=c++20','-O2',str(path),'-o',str(binary)],check=True);compiled=json.loads(subprocess.check_output([str(binary)]));differences=[dict(skill=result[i]['skill'],rank=n,published=result[i]['values'][n-1],decompiled=v) for i,n,v in compiled if result[i]['values'][n-1]!=v]
assert differences
out=dict(referenceVersion='3.3.93847',status='Unresolved source disagreement; current native two-stage rounding retained. Historical Blizzard tables contradict the proposed decompiled-helper correction.',publishedCurves=result,sourcePages={slug:{k:v for k,v in page.items() if k!='rows'} for slug,page in sourcePages.items()},pinnedSkillTableHash=hashlib.sha256(modernPath.read_bytes()).hexdigest(),legacyCommit=tree['sha'],legacySourceHash=hashlib.sha256(raw).hexdigest(),legacyBlockHash=hashlib.sha256(body.encode()).hexdigest(),compiledCases=len(compiled),disagreements=differences,decision='Do not replace the shared native curve with decompiled multiplication-first arithmetic without pinned modern confirmation. The previous86-field inventory describes model differences, not established game defects.',question='Optional pinned-build Summon Resist tooltip: effective rank3 gives39% under current model versus40% under decompiled helper; rank21 gives66% versus67%. No Barbarian observation requested.',limits=['Blizzard tables are historical and do not identify D2R3.3.93847; they disprove treating this decompiled helper as sole authoritative evidence, but do not certify the modern executable.','60 published ranks cover three unchanged current parameter pairs. Native fixture compares actual evaluator/consumer paths separately.','Compiled legacy body is unchanged but is decompiled supplementary code, not a verified modern formula engine.'])
# Firsthand pinned-build observations are source evidence, not generated runtime output.
out['pinnedBuildObservations'] = {'referenceVersion': '3.3.93847', 'source': 'User firsthand tooltip readings in this session; uninstrumented', 'skill': 'Summon Resist', 'values': [{'effectiveRank': 7, 'reportedPercent': 52, 'currentNativePercent': 52, 'decompiledHelperPercent': 52}, {'effectiveRank': 13, 'reportedPercent': 61, 'currentNativePercent': 61, 'decompiledHelperPercent': 61}, {'effectiveRank': 19, 'reportedPercent': 65, 'currentNativePercent': 65, 'decompiledHelperPercent': 65}], 'interpretation': 'All three observations agree with both candidate formulas. They corroborate these ranks but do not distinguish rounding; retain the current runtime curve.'}
# Current Amazon readings independently discriminate the two arithmetic orders.
proof=json.loads((ROOT/'tools/reference/amazon-dodge-engine-evidence.json').read_text())
out['pinnedAmazonObservations']=proof['currentBuildTooltips']
assert [x['compiledLegacyPercent'] for x in out['pinnedAmazonObservations']['comparisons']]==[30,37,40]
out['status']='Current pinned-build Dodge3=29 and Avoid3=36 support retained two-stage rounding over supplementary30/37; Evade6=40 agrees with both. Other parameter/rank/context parity remains open.'
out['decision']='Retain the shared native two-stage curve. Current firsthand Amazon readings distinguish it from the supplementary helper at two parameter pairs; do not generalize three observations into full formula-engine equivalence or86 confirmed defects.'
(ROOT/'tools/reference/diminishing-reference-conflict.json').write_text(json.dumps(out,indent=2)+'\n');print('Historical published cases:',len(compiled),'legacy disagreements:',len(differences),'runtime unchanged')
