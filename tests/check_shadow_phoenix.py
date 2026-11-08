#!/usr/bin/env python3
"""Own Phoenix in shared pools plus changed player/charge/finisher regressions."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-phoenix'
 c['extra_files'].update({'shadow-charge-animation-cases.json':ROOT/'.local/class-skills/shadow-charge-animation-source/cases.json','tiger-strike-source-probe.json':ROOT/'.local/class-skills/tiger-strike-source-probe.json','tiger-strike-source-evidence.json':ROOT/'tools/reference/tiger-strike-source-evidence.json'})
 c['extra_families']=('shadow-phoenix','shadow-charge','shadow-finisher','phoenix-release')
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/phoenix-release-source-evidence.json',ROOT/'tools/reference/phoenix-chain-init-source-evidence.json',ROOT/'tools/reference/phoenix-chaos-source-evidence.json',ROOT/'tools/reference/phoenix-meteor-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
