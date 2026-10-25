#!/usr/bin/env python3
"""Run full shared do34 builder with current Phoenix state/AR constants."""
import argparse,hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 parent=json.loads((ROOT/'tools/reference/cobra-strike-source-evidence.json').read_text());path=STAGE/'cobra-strike-builder-source-probe.cpp';base=path.read_text();assert 'SKILLS_SrvDo034_TigerStrike_CobraStrike_RoyalStrike' in base
 old='formula==50?50';assert base.count(old)==1;base=base.replace(old,'formula==50?25').replace('count*50,stored,265','count*25,stored,280').replace('&owner,265,rank','&owner,280,rank')
 cpp=STAGE/'phoenix-builder-source-probe.cpp';cpp.write_text(base);binary=cpp.with_suffix('');subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-pie','-no-pie',str(cpp),'-o',str(binary)],check=True);rows=json.loads(subprocess.check_output([str(binary)]));assert len(rows)==512
 for count,stored,rank,success,allocation,target,record,result,exists,newcount,ar,newrank,expiry,allocs,drains,events in rows:
  valid=bool(target and record);gain=valid and success and(count>0 or allocation)
  assert result==int(valid) and drains==int(valid) and allocs==int(valid) and events==int(gain)
  assert exists==int(count>0 or gain) and newcount==(min(count+1,3) if gain else count) and ar==(min(count+1,3)*25 if gain else count*25) and newrank==(stored if count>0 or not gain else rank) and expiry==(475 if gain else 321)
 fixture=STAGE/'phoenix-builder-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n');e=dict(referenceVersion='3.3.93847',talentId='assassin.phoenixstrike',installed=False,handlerEnabled=False,reusedCompleteComposition='tools/reference/cobra-strike-source-evidence.json',parentSourceHashes=parent['sourceHashes'],parentCompiledRoutines=parent['compiledRoutines'],fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=hashlib.sha256(fixture.read_bytes()).hexdigest(),cppSha256=hashlib.sha256(cpp.read_bytes()).hexdigest(),cases=len(rows),sanitizers='ASan+UBSan'),established=['Complete shared do34executes Phoenix280with current25charge AR and375frame duration. Success adds one independent state charge up to3,preserves initial rank,refreshes expiry and invokes the existing physical allocation/drain/event services.','Invalid target/record,miss and declared failed new-state allocation do not gain/refesh a charge. No progressive release executes during the charging attack.'],services=['Current Param7=25is supplied through the inherited formula50service identifier;synthetic formula IDs are not current binary formula indices.','Target/stat-list/allocation/physical/durability/events use bounded parent services;4mana belongs to the generic accepted-start caller and is verified natively.','Current retained-expiry/proportional AR after consuming one and cached A1 timing remain separate existing adapter gaps;no native or current executable skill certificate.'])
 out=ROOT/'tools/reference/phoenix-builder-source-evidence.json';encoded=json.dumps(e,indent=2)+'\n'
 if check:assert out.read_text()==encoded,'Phoenix builder evidence stale'
 else:out.write_text(encoded)
 print('Phoenix builder:512 complete shared do34contexts pass;25AR per charge/375frames/current row280.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
