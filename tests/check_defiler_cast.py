#!/usr/bin/env python3
"""Learned Defiler actual SC/AI/S1/group/flying host and saved/input regressions."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    names=tuple(f'{family}-{suffix}'for family in ('goatman-cast','goatman-native-host','tainted-native-decision','tainted-cast','defiler-health-link','defiler-flying-world','defiler-link-casting','defiler-cast')for suffix in ('check','reload-check'))
    check(stage=ROOT/'.local/class-skills/defiler-cast',
          extra_files={'defiler-link-source-cases.json':ROOT/'.local/class-skills/defiler-link-source/formula.json',
              'defiler-link-cast-source-cases.json':ROOT/'.local/class-skills/defiler-link-cast-source/cases.json',
              'defiler-flying-motion-source-cases.json':ROOT/'.local/class-skills/defiler-flying-source/cases.json'},
          extra_families=('goatman-cast','goatman-native-host','goatman-body','tainted-ground-movement','tainted-native-decision','tainted-body','tainted-cast','defiler-link-numbers','defiler-body','defiler-health-link','defiler-flying-motion','defiler-flying-world','defiler-link-casting','defiler-cast'),
          extra_connected_checks=names,extra_wait_results={n:n+'-result'for n in names},extra_connection_settle=.05,
          extra_target_input_commands=('goatman-cast-right','goatman-cast-quick','tainted-cast-right','tainted-cast-quick','defiler-cast-right','defiler-cast-quick'),
          save_command='defiler-link-casting-save',save_result='defiler-link-casting-save-result',connected_save=True,
          extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/defiler-health-link-rule-approval.json'))
