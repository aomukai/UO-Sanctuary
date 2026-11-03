#!/usr/bin/env python3
"""Released-state fresh Fury entry with complete predecessor and sentry coverage."""
import argparse,os
from pathlib import Path
from check_wake_fire_automatic import ROOT,configuration as previous
from check_iron_golem_birth import check

def configuration():
    c=previous();c['stage']=ROOT/'.local/class-skills/blade-fury-reentry'
    for name in ('blade-fury-channel','blade-fury-resource','blade-fury-release','blade-fury-sequence','blade-fury-timing','blade-fury-rate','blade-fury-damage','blade-fury-reentry'):
        c['extra_files'][name+'-source-probe.json']=ROOT/'.local/class-skills'/(name+'-source-probe.json')
    families=('blade-fury-resource','blade-fury-release','blade-fury-missile','blade-fury-reentry')
    c['extra_families']=(*c['extra_families'],*families)
    c['extra_checks']=(*c['extra_checks'],'blade-fury-channel-check','blade-fury-sequence-check')
    for name in ('blade-fury-resource','blade-fury-release','blade-fury-missile'):c['extra_manifest_names'][name]=name+'-fixture.json'
    for name in ('blade-fury-mode','blade-fury-reentry'):
        command=name+'-live-check';c['extra_checks']=(*c['extra_checks'],command);c['extra_connected_checks']=(*c['extra_connected_checks'],command);c['extra_wait_results'][command]=name+'-live-result'
    c['extra_inputs']=(*c['extra_inputs'],Path(__file__).resolve(),ROOT/'tools/reference/blade-fury-reentry-source-evidence.json',ROOT/'tools/reference/wake-fire-automatic-native-evidence.json')
    return c
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--focused',action='store_true');a=p.parse_args();c=configuration()
    if a.focused:
        c['stage']=ROOT/'.local/class-skills/blade-fury-reentry-targeted'
        c['extra_families']=tuple(x for x in c['extra_families'] if x.startswith('blade-fury-'))
        c['extra_checks']=('blade-fury-channel-check','blade-fury-sequence-check','blade-fury-mode-live-check','blade-fury-reentry-live-check')
        c['extra_connected_checks']=('blade-fury-mode-live-check','blade-fury-reentry-live-check')
        c['extra_wait_results']={name:name.removesuffix('-check')+'-result' for name in c['extra_connected_checks']}
        for key in ('extra_target_input_commands','extra_ground_input_commands','extra_instant_input_commands'):c[key]=()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
