#!/usr/bin/env python3
"""Necromancer magic flights publish typed components to approved Defiler links."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/necro-spell-link'
def configuration():
    names=('defiler-health-link-check','defiler-health-link-reload-check')
    c=dict(stage=STAGE,extra_files={'hellslayer-before-readiness.json':ROOT/'tools/reference/named-item-pre-proc-baseline.json'},extra_families=('defiler-health-link','hellslayer-readiness'),extra_manifest_names={},extra_wait_results={n:n+'-result'for n in names},extra_connected_checks=names,extra_connection_settle=.15,extra_target_input_commands=tuple(f'{spell}-{mode}'for spell in ('teeth','bone-spear','bone-spirit')for mode in ('right','quick')),extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json',ROOT/'tools/reference/pierce-action-policy-native-evidence.json',ROOT/'tools/build_skill_roadmap.py'))
    c['extra_families']+=('teeth','bone-spear','bone-spirit')
    for spell,parts in (('teeth',('damage','route')),('bone-spear',('damage','route')),('bone-spirit',('damage','guidance'))):
        for part in parts:
            path=ROOT/f'.local/class-skills/{spell}-source/{part}-cases.json';assert path.is_file(),path
            c['extra_files'][f'{spell}-{part}-source-cases.json']=path
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
