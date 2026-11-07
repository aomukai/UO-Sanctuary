#!/usr/bin/env python3
"""Derived source startup and actual initialized Valkyrie ground consumers."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_valkyrie_ground_combat import configuration as prior
def configuration(retained=False,isolate=False,shared=False):
 c=prior(retained,isolate,shared)
 c['stage']=ROOT/'.local/class-skills'/('valkyrie-startup-shared'if shared else'valkyrie-startup-isolate'if isolate else'valkyrie-startup-retained'if retained else'valkyrie-startup')
 if not shared:
  c['extra_families']+=('valkyrie-startup',)
  c['extra_files']['valkyrie-startup-source-cases.json']=ROOT/'.local/class-skills/valkyrie-startup-source/cases.json'
  c['extra_files']['npc-expanded-spells-source-cases.json']=ROOT/'.local/class-skills/npc-expanded-spells-source/cases.json'
 c.setdefault('extra_check_timeouts',{}).update({'valkyrie-startup-check':120,'valkyrie-startup-reload-check':120})
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_valkyrie_startup.py',ROOT/'tools/probe_npc_expanded_spells.py',ROOT/'tools/probe_npc_expanded_origins.py')
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained,a.isolate,a.shared))
