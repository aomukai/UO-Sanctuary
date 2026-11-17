#!/usr/bin/env python3
"""Sorceress/shared direct spell and Static Field Health Link compatibility."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_sorceress_planned_acceptance import configuration as prior
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/sorceress-spell-link'
def configuration():
    c=prior();c['stage']=STAGE;c['extra_families']=('defiler-health-link','hellslayer-readiness','arctic-potion',*c['extra_families'],'ring-world','flame-wave-world')
    c['extra_files']['hellslayer-before-readiness.json']=ROOT/'tools/reference/named-item-pre-proc-baseline.json'
    c['extra_connected_checks']+=('defiler-health-link-check','defiler-health-link-reload-check','flame-wave-world-check','flame-wave-world-reload-check')
    c['extra_wait_results'].update({n:n+'-result'for n in ('defiler-health-link-check','defiler-health-link-reload-check','flame-wave-world-check','flame-wave-world-reload-check')})
    c['extra_connection_settle']=.15
    c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json',ROOT/'tools/reference/blade-link-native-evidence.json',ROOT/'tools/build_skill_roadmap.py')
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
