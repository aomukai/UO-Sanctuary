#!/usr/bin/env python3
"""Own Fists in shared contact/field services plus affected original/player consumers."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-fists'
 c['extra_files'].update({'shadow-claw-cases.json':ROOT/'.local/class-skills/shadow-claw-sequence-source/cases.json','shadow-claw-rates.json':ROOT/'.local/class-skills/shadow-claw-sequence-source/rates.json','shadow-charge-animation-cases.json':ROOT/'.local/class-skills/shadow-charge-animation-source/cases.json','tiger-strike-source-probe.json':ROOT/'.local/class-skills/tiger-strike-source-probe.json','tiger-strike-source-evidence.json':ROOT/'tools/reference/tiger-strike-source-evidence.json','fists-fire-conversion-source-probe.json':ROOT/'.local/class-skills/fists-fire-conversion-source-probe.json','fists-fire-builder-source-probe.json':ROOT/'.local/class-skills/fists-fire-builder-source-probe.json'})
 c['extra_families']=('shadow-fists','shadow-claw','shadow-charge','shadow-finisher','shadow-phoenix','fists-fire')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/fists-fire-source-evidence.json',ROOT/'tools/reference/martial-fire-release-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
