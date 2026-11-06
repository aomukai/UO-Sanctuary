#!/usr/bin/env python3
"""Original Valkyrie ordered equipment contacts with retained class consumers."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_valkyrie_motion import configuration as prior
def configuration(retained=False):
 c=prior(retained);c['stage']=ROOT/'.local/class-skills'/('valkyrie-contact-retained'if retained else'valkyrie-contact')
 c['extra_families']+=('valkyrie-contact',);c['extra_files']['valkyrie-contact-cases.json']=ROOT/'.local/class-skills/valkyrie-contact-source/cases.json';c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args();os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
