#!/usr/bin/env python3
"""Ordinary/incoming original item producers and retained class consumers."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_npc_original_procs import configuration as prior
def configuration(retained=False,isolate=False,shared=False):
 c=prior(retained,isolate,shared)
 c['stage']=ROOT/'.local/class-skills'/('npc-original-contact-shared'if shared else'npc-original-contact-isolate'if isolate else'npc-original-contact-retained'if retained else'npc-original-contact')
 if isolate:c['extra_families']=()
 if not shared:c['extra_families']+=('npc-original-contact',)
 c['extra_inputs']+=(Path(__file__).resolve(),)
 return c
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--retained',action='store_true');p.add_argument('--isolate',action='store_true');p.add_argument('--shared',action='store_true');a=p.parse_args()
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(a.retained,a.isolate,a.shared))
