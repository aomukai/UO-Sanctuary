#!/usr/bin/env python3
"""Source Shadow initialization,original item graphs and retained learned routes."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_birth import configuration as prior
def configuration(retained=False,isolate=False,shared=False):
 c=prior(retained,isolate,shared)
 c['stage']=ROOT/'.local/class-skills'/('shadow-initializer-shared'if shared else'shadow-initializer-isolate'if isolate else'shadow-initializer-retained'if retained else'shadow-initializer')
 if not shared:
  c['extra_families']+=('shadow-initializer',)
  c['extra_files']['shadow-initializer-source-cases.json']=ROOT/'.local/class-skills/shadow-initializer-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_shadow_initializer.py',ROOT/'tools/corroborate_shadow_initializer.py',ROOT/'tools/reference/shadow-initializer-data-evidence.json',ROOT/'tools/reference/shadow-initializer-source-evidence.json')
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained,a.isolate,a.shared))
