#!/usr/bin/env python3
"""Original Shadow bodies/resources and retained learned class mechanics."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_initializer import configuration as prior
def configuration(retained=False,isolate=False,shared=False):
 c=prior(retained,isolate,shared)
 c['stage']=ROOT/'.local/class-skills'/('shadow-body-shared'if shared else'shadow-body-isolate'if isolate else'shadow-body-retained'if retained else'shadow-body')
 if not shared:c['extra_families']+=('shadow-body',)
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained,a.isolate,a.shared))
