#!/usr/bin/env python3
"""Learned original Valkyrie transactions,real right/quick combat and saves."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_valkyrie_buffs import configuration as prior
def configuration(retained=False,isolate=False,shared=False):
 c=prior(retained,isolate,shared)
 c['stage']=ROOT/'.local/class-skills'/('valkyrie-cast-shared'if shared else'valkyrie-cast-isolate'if isolate else'valkyrie-cast-retained'if retained else'valkyrie-cast')
 if not shared:
  c['extra_families']+=('valkyrie-cast',)
  c['extra_target_input_commands']=c.get('extra_target_input_commands',())+('valkyrie-cast-right','valkyrie-cast-quick')
 c.setdefault('extra_check_timeouts',{}).update({'valkyrie-cast-check':120,'valkyrie-cast-reload-check':120})
 c.setdefault('extra_input_timeouts',{}).update({'valkyrie-cast-right':20,'valkyrie-cast-quick':20})
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/review_valkyrie_cast.py')
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained,a.isolate,a.shared))
