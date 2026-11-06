#!/usr/bin/env python3
"""Approved Frenzy and retained paired-contact/class mechanics."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_double_swing_sequence import configuration as focused
from check_entropy_clouds import configuration as prior
def configuration(retained=False):
 c=prior(True)if retained else focused(False)
 c['stage']=ROOT/'.local/class-skills'/('frenzy-retained'if retained else'frenzy')
 c['extra_families']+=('frenzy',)
 c['extra_checks']+=('frenzy-release-check',)
 c['extra_target_input_commands']+=('frenzy-left','frenzy-right','frenzy-quick')
 c.setdefault('extra_input_timeouts',{})['frenzy-left']=12
 c['extra_files']['frenzy-release-source-probe.json']=ROOT/'.local/class-skills/frenzy-release-source-probe.json'
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained))
