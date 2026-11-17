#!/usr/bin/env python3
"""War Cry/shared landing physical components with existing source/cast/save tests."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_leap_attack import configuration as prior
STAGE=ROOT/'.local/class-skills/warcry-link'
def configuration():
    c=prior(False);c['stage']=STAGE
    c['extra_families']=('defiler-health-link','hellslayer-readiness',*c['extra_families'])
    c['extra_files']['hellslayer-before-readiness.json']=ROOT/'tools/reference/named-item-pre-proc-baseline.json'
    names=('defiler-health-link-check','defiler-health-link-reload-check')
    c['extra_connected_checks']=tuple(c.get('extra_connected_checks',()))+names;c['extra_wait_results'].update({n:n+'-result'for n in names});c['extra_connection_settle']=.15
    probes=tuple(ROOT/'.local/class-skills'/(n+'-source-probe.json')for n in ('warcry','leap','leap-collision'))
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json',ROOT/'tools/reference/armageddon-link-native-evidence.json',ROOT/'tools/build_skill_roadmap.py',*probes)
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests')
    c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
