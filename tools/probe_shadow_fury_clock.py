#!/usr/bin/env python3
"""Original slot2 sequence uses the established complete NPC clock helpers."""
import csv,hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-fury-clock-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);prior=json.loads((ROOT/'tools/reference/shadow-flight-clock-source-evidence.json').read_text());original=BASE/'shadow-flight-clock-source/source.cpp';assert sha(original)==prior['artifacts'][str(original.relative_to(ROOT))]
 cpp=original.read_text().split('int main(){',1)[0];inputs={str(original.relative_to(ROOT)):sha(original)}
 local=ROOT/'.local/research/d2r-3.3.93847';review=json.loads((ROOT/'tools/reference/shadow-flight-source-review.json').read_text())
 for name in ['monseq.txt','monstats.txt']:
  p=local/name;rel=str(p.relative_to(ROOT));assert sha(p)==review['inputs'][rel];inputs[rel]=sha(p)
 monsters={r['Id']:r for r in csv.DictReader((local/'monstats.txt').read_text().splitlines(),delimiter='\t')}
 for name in ['shadowwarrior','shadowmaster']:assert monsters[name]['Skill2']=='Blade Fury'and monsters[name]['Sk2mode']=='seq_swbladefury'
 rows=[r for r in csv.DictReader((local/'monseq.txt').read_text().splitlines(),delimiter='\t')if r['sequence']=='seq_swbladefury'];data=[[7,int(r['frame']),int(r['dir']or 0),int(r['event']or 0)]for r in rows]
 assert len(data)==17 and all(r==[7,i,0,1 if i==9 else 0]for i,r in enumerate(data))
 raw=ROOT/'tools/reference/d2r-named-equipment/json/skills.json';skills=json.loads(raw.read_text());r=skills['266'];assert r['monanim']=='SC'and r['seqtrans']=='A1'and not r.get('UseAttackRate',0);inputs[str(raw.relative_to(ROOT))]=sha(raw)
 # Current monstats initializer overrides metadata's generic SC mode with14.
 # Full dispatch remains the same Other branch as the retained Flight proof.
 names=['UNITS_InitializeSequence','UNITS_UpdateAnimRateAndVelocity','UNITS_UpdateOtherAnimRateAndVelocity','UNITS_CanAnimModeUseAttackRate','UNITS_IsSeqAnimSpeedModulatedByFCR','UNITS_StopSequence'];units=BASE/'d2moo-pinned-source/source/D2Common/src/Units/Units.cpp';inputs[str(units.relative_to(ROOT))]=sha(units)
 for n in names:assert hashlib.sha256(function(cpp,n).encode()).hexdigest()==prior['completeClockRoutines'][n]and function(cpp,n)==function(units.read_text(),n)
 # Supply the actual17-point monseq to the existing declared row/count readers.
 cpp=cpp.replace('return 23*256;','return 17*256;').replace('return 23;','return 17;')
 # Check the exact existing suppliers rather than accepting an ineffective edit.
 assert 'return 17*256;'in cpp and 'return 17;'in cpp
 rowsCpp=','.join('{'+','.join(str(v)for v in [0]+r)+'}'for r in data)
 cpp+='\nint main(){D2AnimSeqTxt owned[]={'+rowsCpp+'};printf("[");bool sep=false;for(int cls:{417,418})for(int base:{128,256})for(int other:{15,100,175}){reset();std::copy(std::begin(owned),std::end(owned),sequence);owner.dwUnitType=1;owner.dwClassId=cls;owner.dwAnimMode=14;anim.dwAnimSpeed=base;otherRate=other;attackUpdates=castUpdates=0;row.nSeqTrans=7;row.dwFlags[0]=0;UNITS_InitializeSequence(&owner);UNITS_UpdateAnimRateAndVelocity(&owner,__FILE__,__LINE__);assert(owner.dwSeqSpeed==256&&attackUpdates==0&&castUpdates==0);std::vector<int>markers;int frame=1;for(;owner.dwFrameCountPrecise>0;frame++){if(owner.nActionFrame==1){markers.push_back(frame);markers.push_back(owner.dwSeqFrame);}UNITS_StopSequence(&owner);}assert(frame==18&&markers==std::vector<int>({10,2304}));if(sep)printf(",");sep=true;list({cls,base,other,owner.dwSeqSpeed,owner.wAnimSpeed,10,18});}printf("]\\n");}\n'
 generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode();cases=STAGE/'cases.json';cases.write_bytes(run.stdout);assert len(json.loads(run.stdout))==12
 balance=ROOT/'data/Sanctuary/Balance/shadow-fury-sequence.json';balance.write_text(json.dumps(dict(ReferenceVersion='3.3.93847',Code='seq_swbladefury',Mode=14,Points=17,Speed=256,ActionFrame=10,EndFrame=18,Rows=data),indent=2)+'\n')
 # Callback/rewind integration is still unqualified;retain its complete body
 # as reviewed evidence,without labelling it as a newly compiled integration.
 mode=BASE/'d2moo-pinned-source/source/D2Game/src/MONSTER/MonsterMode.cpp';inputs[str(mode.relative_to(ROOT))]=sha(mode)
 receipt=dict(referenceVersion='3.3.93847',contexts=12,completeClockRoutines=prior['completeClockRoutines'],reviewedCallback=hashlib.sha256(function(mode.read_text(),'sub_6FC64E90').encode()).hexdigest(),inputs=inputs,reusedProof={'tools/reference/shadow-flight-clock-source-evidence.json':sha(ROOT/'tools/reference/shadow-flight-clock-source-evidence.json')},artifacts={str(p.relative_to(ROOT)):sha(p)for p in [generated,binary,cases,balance,STAGE/'compiler.log',STAGE/'sanitizer.log',Path(__file__).resolve()]},scope='Original monstats slot2/Fury retained mode14 overrides generic SC metadata. Complete retained initializer/rate/stop routines now use actual17point sequence,action9 consumed atNPCframe10,end18. SeqtransA1/UseAttackRate0 selectOther and leave sequence speed256. Generic SC comparison is not the initialized action. Source do48/state/rewind scheduling,remaining lease across reentry,critical/world/private seeds and full AI remain separate.')
 (ROOT/'tools/reference/shadow-fury-clock-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print('Original Fury slot2:12whole NPC clock controls;mode14/17points/marker10/end18,sequence256;ASan/UBSan clean.')
if __name__=='__main__':main()
