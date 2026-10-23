#!/usr/bin/env python3
"""Compile Find Item start/do and shared curve/selection; do not invent its loot event."""
from pathlib import Path
import ast,hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
blobs={x['path']:x['sha'] for x in tree['tree'] if x['type']=='blob'};hashes={};blocks={}
def source(path):
 raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blobs[path]
 hashes[path]=hashlib.sha256(raw).hexdigest();return raw.decode()
def routine(text,name):
 m=re.search(r'^.*\b'+re.escape(name)+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name
 end=text.index('{',m.start())+1;depth=1
 while depth:depth+=(text[end]=='{')-(text[end]=='}');end+=1
 blocks[name]=text[m.start():end];return blocks[name]
bar=source('source/D2Game/src/SKILLS/SkillBar.cpp');common=source('source/D2Common/src/D2Skills.cpp');ordinals=source('source/D2Common/src/UselessOrdinals.cpp')
# Reuse the literal declared-services harness, without executing that probe.
harnessPath=ROOT/'tools/probe_find_potion_reference.py';module=ast.parse(harnessPath.read_text())
cpp=next(ast.literal_eval(n.value) for n in module.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='cpp' for t in n.targets))
cpp=cpp.replace('dwParam[4]={0,100,30,10}','dwParam[4]={5,60,0,0}')
cpp+='int hardPotion=0;int modeCalls=0,modeParam=0;D2UnitStrc* modeCorpse=nullptr;D2UnitStrc* modeCaster=nullptr;\n'
for text,name in [(common,'SKILLS_CanUnitCorpseBeSelected'),(common,'D2Common_11033'),(common,'D2Common_11034'),(ordinals,'D2COMMON_11019')]:cpp+=routine(text,name)+'\n'
cpp+='int SKILLS_EvaluateSkillFormula(D2UnitStrc*,int,int skill,int rank){return D2Common_11034(rank,skill)+hardPotion;}\n'
cpp+='void sub_6FC659B0(D2GameStrc*,D2UnitStrc* corpse,D2UnitStrc* caster,int param){modeCalls++;modeParam=param;modeCorpse=corpse;modeCaster=caster;}\n'
for name in ['SKILLS_SrvSt34_FindItem','SKILLS_SrvDo072_FindItem']:cpp+=routine(bar,name)+'\n'
cpp+=r'''void Reset(D2UnitStrc& caster,D2UnitStrc& corpse){caster.pSeed=0;corpse.noselect=corpse.udead=false;refresh=modeCalls=modeParam=0;modeCorpse=modeCaster=nullptr;}
int main(){D2UnitStrc caster,corpse;caster.dwUnitType=0;selected=&corpse;bool first=true;std::printf("{\"eligibilityCases\":[");
for(int typ:{-1,0,1,2})for(int mode:{0,1,12})for(int flags:{0,128,4096,4224})for(int undead:{0,1})for(int valid:{0,1}){Reset(caster,corpse);selected=typ<0?nullptr:&corpse;corpse.dwUnitType=typ;corpse.dwAnimMode=mode;corpse.udead=undead;hasModes=valid;modes.dwFlags=flags;int result=SKILLS_SrvSt34_FindItem(&game,&caster,142,1);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d]",typ,mode,flags,undead,valid,result);}
selected=&corpse;corpse.dwUnitType=1;corpse.dwAnimMode=12;hasModes=true;modes.dwFlags=128;
std::printf("],\"chanceCases\":[");first=true;
for(int rank:{0,1,2,3,8,20,21,60})for(int hard:{0,1,20})for(int draw:{0,12,13,19,20,50,51,52,99}){Reset(caster,corpse);hardPotion=hard;draws[0]=draw;draws[1]=7;int chance=SKILLS_EvaluateSkillFormula(&caster,0,142,rank),result=SKILLS_SrvDo072_FindItem(&game,&caster,142,rank),count=caster.pSeed,again=SKILLS_SrvDo072_FindItem(&game,&caster,142,rank);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",rank,hard,draw,chance,result,corpse.noselect,refresh,modeCalls,count,again,caster.pSeed);}
std::printf("],\"modeCases\":[");first=true;hardPotion=0;draws[0]=0;
for(int draw=0;draw<100;draw++){Reset(caster,corpse);draws[1]=draw;int result=SKILLS_SrvDo072_FindItem(&game,&caster,142,1);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",draw,result,modeCalls,modeParam,modeCorpse==&corpse,modeCaster==&caster,caster.pSeed);}
std::printf("],\"guardCases\":[");first=true;
for(int rowValid:{0,1})for(int present:{0,1})for(int used:{0,1}){Reset(caster,corpse);hasRow=rowValid;selected=present?&corpse:nullptr;corpse.noselect=used;int result=SKILLS_SrvDo072_FindItem(&game,&caster,142,1);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d,%d,%d,%d]",rowValid,present,used,result,corpse.noselect,caster.pSeed,modeCalls);}
std::printf("],\"curveCases\":[");first=true;
for(int start:{0,5,15})for(int maximum:{60,80,100,150})for(int rank:{0,1,2,3,8,20,21,60,255}){int result=D2Common_11033(rank,start,maximum);if(!first)std::printf(",");first=false;std::printf("[%d,%d,%d,%d]",start,maximum,rank,result);}
std::printf("]}\n");}
'''
path=STAGE/'find-item-source-probe.cpp';path.write_text(cpp);binary=STAGE/'find-item-source-probe';subprocess.run(['g++','-std=c++20','-Wno-multichar','-O2',str(path),'-o',str(binary)],check=True)
data=json.loads(subprocess.check_output([str(binary)]))
for typ,mode,flags,undead,valid,result in data['eligibilityCases']:assert result==int(typ==1 and mode==12 and flags&128 and not undead and valid)
for rank,hard,draw,chance,result,used,refresh,calls,count,again,last in data['chanceCases']:
 expected=(min(60,5+55*110*rank//(rank+6)//100) if rank>0 else 0)+hard;ok=draw<expected
 assert (chance,result,used,refresh,calls,count,again,last)==(expected,int(ok),1,1,int(ok),2 if ok else 1,0,2 if ok else 1)
for draw,result,calls,param,corpse,caster,count in data['modeCases']:assert (result,calls,param,corpse,caster,count)==(1,1,2 if 5<=draw<65 else 1,1,1,2)
for valid,present,used,result,noselect,count,calls in data['guardCases']:
 ok=valid and present and not used;assert (result,noselect,count,calls)==(int(ok),int(used or ok),2 if ok else 0,int(ok))
for start,maximum,rank,result in data['curveCases']:assert result==min(maximum,start+(maximum-start)*110*rank//(rank+6)//100)
(STAGE/'find-item-source-probe.json').write_text(json.dumps(data,indent=2)+'\n')
differences=[dict(first=a,second=b,rank=n,source=v,currentNative=a+(b-a)*min(100,110*n//(n+6))//100) for a,b,n,v in data['curveCases'] if n>0 and v!=a+(b-a)*min(100,110*n//(n+6))//100]
skillPath=ROOT/'tools/reference/d2r-named-equipment/json/skills.json'
consumers=[]
for row in json.loads(skillPath.read_text()).values():
 if row.get('charclass') not in ('ama','sor','nec','pal','bar','dru','ass','war'):continue
 for field,formula in row.items():
  if 'calc' not in field.lower() or not isinstance(formula,str):continue
  for a,b in re.findall(r'\bdm([1-9])([1-9])\b',formula):
   a,b=int(a),int(b)
   if (a,b)==(9,1):b=10
   first,last=int(row.get('Param'+str(a),0)),int(row.get('Param'+str(b),0))
   mismatch=[]
   for rank in range(1,22):
    exact=min(last,first+(last-first)*110*rank//(rank+6)//100)
    current=first+(last-first)*min(100,110*rank//(rank+6))//100
    if exact!=current:mismatch.append(dict(rank=rank,source=exact,currentNative=current))
   if mismatch:consumers.append(dict(skill=row['skill'],characterClass=row['charclass'],field=field,first=first,second=last,differences=mismatch))
out=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],status='Bounded start/do/curve proof; native Find Item payment and loot event remain pending.',sourceHashes=hashes,sharedHarness=dict(path=str(harnessPath.relative_to(ROOT)),sha256=hashlib.sha256(harnessPath.read_bytes()).hexdigest()),probe=dict(caseCounts={k:len(v) for k,v in data.items()},blockHashes={k:hashlib.sha256(v.encode()).hexdigest() for k,v in blocks.items()},dataPath='.local/class-skills/find-item-source-probe.json'),curveRoundingDifferences=differences,currentTableConsumerScan=dict(skillTableHash=hashlib.sha256(skillPath.read_bytes()).hexdigest(),scope='DM tokens in playable current table calc fields at effective ranks1..21; inventory only, not executed skill outcomes.',consumers=consumers),established=['Start34 uses corpseSel/dead/Udead selection without a separate SOFT test. Do72 marks corpse_noselect and refreshes before chance; failed chance returns0 and still consumes the attempt. Repeated calls consume no further RNG.','Pinned modern chance uses source dm12 plus hard Find Potion. Successful do makes a second draw and sends corpse/caster and nParam to a stubbed mode-event sink; this is not a loot outcome certificate.','Shared native SkillValues.Diminishing truncates the ratio early for general parameter pairs. Source11033 multiplies parameter delta before the divisions and caps the final value. Find Item rank2 with5/60 yields20 instead of native19. Find Potion0/100 remains equal. Record shared-curve correction separately before extending native use.'],limits=['Mode/death/treasure generation and surrounding payment are not compiled here. No native handler, mana/refund behavior or drop policy is enabled.','The source helper is supplemental; modern general formula/overflow behavior and affected existing skill consumers need focused correction/verification.','Shared literal harness declares engine/stat/RNG/state/selection services. No game executable/art is imported.'])
(ROOT/'tools/reference/find-item-engine-evidence.json').write_text(json.dumps(out,indent=2)+'\n')
print('Find Item bounded source cases:',out['probe']['caseCounts'],'general curve differences:',len(differences))
