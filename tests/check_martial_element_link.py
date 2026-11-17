#!/usr/bin/env python3
"""Martial Ice/Thunder/Phoenix elemental contacts retain typed Health Link context."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
from check_druid_planned_acceptance import add_fixture
STAGE=ROOT/'.local/class-skills/martial-element-link'
def configuration():
    names=('defiler-health-link-check','defiler-health-link-reload-check');natural=('thunder-natural-check','ice-natural-check','phoenix-natural-check')
    c=dict(stage=STAGE,extra_files={'hellslayer-before-readiness.json':ROOT/'tools/reference/named-item-pre-proc-baseline.json'},extra_families=('defiler-health-link','hellslayer-readiness'),extra_checks=natural,extra_manifest_names={},extra_connected_checks=(*names,*natural),extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.15,extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json',ROOT/'tools/reference/pierce-action-adoption-native-evidence.json',ROOT/'tools/build_skill_roadmap.py'))
    c['extra_wait_results'].update({n:n.removesuffix('-check')+'-result'for n in natural})
    for fixture in ('ThunderRelease','IceRelease','PhoenixRelease'):add_fixture(c,fixture)
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
