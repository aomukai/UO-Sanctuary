#!/usr/bin/env python3
"""Saved intrinsic Shadow skill books on real original-equipment bodies."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_body import configuration as prior
def configuration(retained=False,isolate=False,shared=False):
 c=prior(retained,isolate,shared)
 c['stage']=ROOT/'.local/class-skills'/('shadow-skills-shared'if shared else'shadow-skills-isolate'if isolate else'shadow-skills-retained'if retained else'shadow-skills')
 if not shared:c['extra_files']['shadow-skill-assignment-cases.json']=ROOT/'.local/class-skills/shadow-skills-source/cases.json'
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained,a.isolate,a.shared))
