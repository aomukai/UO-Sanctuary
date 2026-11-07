#!/usr/bin/env python3
"""Current Shadow birth writes with the retained learned Valkyrie package."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_valkyrie_cast import configuration as prior
def configuration(retained=False,isolate=False,shared=False):
 c=prior(retained,isolate,shared)
 c['stage']=ROOT/'.local/class-skills'/('shadow-birth-shared'if shared else'shadow-birth-isolate'if isolate else'shadow-birth-retained'if retained else'shadow-birth')
 if not shared:
  c['extra_families']+=('shadow-birth',)
  c['extra_files']['shadow-birth-source-cases.json']=ROOT/'.local/class-skills/shadow-current-birth-cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/probe_shadow_current_birth.py',ROOT/'tools/reference/shadow-current-birth-source-evidence.json',ROOT/'tools/review_shadow_birth.py',ROOT/'tools/reference/shadow-birth-source-evidence.json')
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained,a.isolate,a.shared))
