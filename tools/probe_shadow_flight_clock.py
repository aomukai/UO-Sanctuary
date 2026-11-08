#!/usr/bin/env python3
"""Compose owned NPC Flight sequence initialization/rate/advance,12 contexts."""
import hashlib,json,subprocess
from pathlib import Path
from probe_current_item_affixes import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills';STAGE=BASE/'shadow-flight-clock-source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
 STAGE.mkdir(exist_ok=True);review=json.loads((ROOT/'tools/reference/shadow-flight-source-review.json').read_text());prior=json.loads((ROOT/'tools/reference/dragon-flight-mode-source-evidence.json').read_text());claw=json.loads((ROOT/'tools/reference/shadow-claw-sequence-source-evidence.json').read_text())
 p=BASE/'dragon-flight-mode/source.cpp';assert sha(p)==prior['artifacts'][str(p.relative_to(ROOT))];cpp=p.read_text().split('int main(){',1)[0]
 units=(BASE/'d2moo-pinned-source/source/D2Common/src/Units/Units.cpp').read_text();inputs={str(p.relative_to(ROOT)):sha(p)}
 for rel,h in review['inputs'].items():assert sha(ROOT/rel)==h
 for name in ['UNITS_InitializeSequence','UNITS_UpdateAnimRateAndVelocity','UNITS_UpdateOtherAnimRateAndVelocity','UNITS_CanAnimModeUseAttackRate','UNITS_IsSeqAnimSpeedModulatedByFCR']:
  body=function(units,name);assert function(cpp,name)==body;assert hashlib.sha256(body.encode()).hexdigest()==prior['compiledUnchangedRoutineHashes'][name]
 # Conversion was already compiled with the original state119/monster14
 # context in the retained own-claw proof. Supply that established result.
 assert len(claw['artifacts'])>0
 for rel,h in claw['artifacts'].items():assert sha(ROOT/rel)==h
 old='void D2COMMON_11013_ConvertMode(D2UnitStrc*,int*,int*,int*,const char*,int){}';assert old in cpp
 cpp=cpp.replace(old,'void D2COMMON_11013_ConvertMode(D2UnitStrc* p,int* t,int* c,int* m,const char*,int){assert(p->dwUnitType==1&&(p->dwClassId==417||p->dwClassId==418)&&p->dwAnimMode==14);*t=0;*c=6;*m=18;}')
 cpp=cpp.replace('sequenceFound&&p->dwAnimMode==18','sequenceFound&&p->dwAnimMode==14')
 stop=function(units,'UNITS_StopSequence');assert hashlib.sha256(stop.encode()).hexdigest()==claw['routineHashes']['UNITS_StopSequence'];cpp+='\nvoid UNITS_SetAnimActionFrame(D2UnitStrc*,int){std::abort();}\n'+stop
 rows=','.join('{'+','.join(str(v)for v in [0]+r)+'}'for r in review['sequence'])
 cpp+='\nint main(){D2AnimSeqTxt owned[]={'+rows+'};printf("[");bool sep=false;for(int cls:{417,418})for(int base:{128,256})for(int other:{15,100,175}){reset();std::copy(std::begin(owned),std::end(owned),sequence);owner.dwUnitType=1;owner.dwClassId=cls;owner.dwAnimMode=14;anim.dwAnimSpeed=base;otherRate=other;attackUpdates=castUpdates=0;row.nSeqTrans=7;row.dwFlags[0]=0;UNITS_InitializeSequence(&owner);UNITS_UpdateAnimRateAndVelocity(&owner,__FILE__,__LINE__);assert(owner.dwSeqSpeed==256&&attackUpdates==0&&castUpdates==0);std::vector<int> markers;int frame=1;for(;owner.dwFrameCountPrecise>0;frame++){if(owner.nActionFrame==1){markers.push_back(frame);markers.push_back(owner.dwSeqFrame);}UNITS_StopSequence(&owner);}assert(frame==24&&markers==std::vector<int>({10,2304,15,3584}));if(sep)printf(",");sep=true;list({cls,base,other,owner.dwSeqSpeed,owner.wAnimSpeed,10,15,24});}printf("]\\n");}\n'
 generated=STAGE/'source.cpp';generated.write_text(cpp);binary=STAGE/'probe'
 with(STAGE/'compiler.log').open('w')as log:subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all','-fno-pie','-no-pie',str(generated),'-o',str(binary)],stdout=log,stderr=subprocess.STDOUT,check=True)
 run=subprocess.run([str(binary)],capture_output=True);(STAGE/'sanitizer.log').write_bytes(run.stderr);assert run.returncode==0 and not run.stderr,run.stderr.decode()
 cases=STAGE/'cases.json';cases.write_bytes(run.stdout);assert len(json.loads(run.stdout))==12
 data=dict(ReferenceVersion='3.3.93847',Code='seq_swdragonflight',Mode=14,Points=23,Speed=256,TeleportFrame=10,KickFrame=15,EndFrame=24,Rows=review['sequence']);balance=ROOT/'data/Sanctuary/Balance/shadow-flight-sequence.json';balance.write_text(json.dumps(data,indent=2)+'\n')
 receipt=dict(referenceVersion='3.3.93847',contexts=12,completeClockRoutines={n:prior['compiledUnchangedRoutineHashes'][n]for n in ['UNITS_InitializeSequence','UNITS_UpdateAnimRateAndVelocity','UNITS_UpdateOtherAnimRateAndVelocity','UNITS_CanAnimModeUseAttackRate','UNITS_IsSeqAnimSpeedModulatedByFCR']}|{'UNITS_StopSequence':claw['routineHashes']['UNITS_StopSequence']},inputs=inputs,reusedProofs={str(p.relative_to(ROOT)):sha(p)for p in [ROOT/'tools/reference/shadow-flight-source-review.json',ROOT/'tools/reference/shadow-claw-sequence-source-evidence.json',ROOT/'tools/reference/dragon-flight-mode-source-evidence.json']},artifacts={str(p.relative_to(ROOT)):sha(p)for p in [generated,binary,STAGE/'compiler.log',STAGE/'sanitizer.log',cases,balance,Path(__file__).resolve()]},scope='Own monster mode14/slot5 current23-point sequence,retained established state119 conversion supplied as a service,full initialization/rate dispatch/stop advance. Optional UseAttackRate0 and A1 transition select Other;only displayed animation speed changes,sequence stays256. NPC consume-before-advance gives first marker10,second15,end24. Phase meaning follows explicit user approval;this does not compile new modern code or a full NPC action/landing callback.')
 (ROOT/'tools/reference/shadow-flight-clock-source-evidence.json').write_text(json.dumps(receipt,indent=2)+'\n');print('Own Shadow Flight clock:12 composed controls,first10/second15/end24;ASan/UBSan clean.')
if __name__=='__main__':main()
