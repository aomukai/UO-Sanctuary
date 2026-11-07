#!/usr/bin/env python3
"""Actual equipped Valkyrie AI/action contacts and original saved world state."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_valkyrie_initializer import configuration as prior
def configuration(retained=False,isolate=False,shared=False):
 c=prior(retained,isolate,shared)
 c['stage']=ROOT/'.local/class-skills'/('valkyrie-ground-combat-shared'if shared else'valkyrie-ground-combat-isolate'if isolate else'valkyrie-ground-combat-retained'if retained else'valkyrie-ground-combat')
 if not shared:c['extra_families']+=('valkyrie-ground-combat',)
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained,a.isolate,a.shared))
