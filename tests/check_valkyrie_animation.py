#!/usr/bin/env python3
"""Original transformed Valkyrie animation preparation and retained defenses."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_valkyrie_defense import configuration as prior
def configuration(retained=False):
 c=prior(retained);c['stage']=ROOT/'.local/class-skills'/('valkyrie-animation-retained'if retained else'valkyrie-animation')
 c['extra_families']+=('valkyrie-animation',);c['extra_files']['valkyrie-animation-cases.json']=ROOT/'.local/class-skills/valkyrie-animation-source/cases.json';c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
