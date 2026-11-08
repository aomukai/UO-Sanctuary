#!/usr/bin/env python3
"""Five intrinsic do45 consumers with retained S2 and player CBS contracts."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_shadow_buffs import configuration
if __name__=='__main__':
 c=configuration();c['stage']=ROOT/'.local/class-skills/shadow-sentries'
 c['extra_families']=('shadow-sentry','shadow-actions','sentry-actor')
 c['extra_files']['sentry-controller-source-probe.json']=ROOT/'.local/class-skills/sentry-controller-source-probe.json'
 c['extra_checks']=('sentry-learned-mechanics-check',)
 c['extra_inputs']+=(Path(__file__).resolve(),ROOT/'tools/reference/assassin-trap-summon-engine-evidence.json',ROOT/'tools/reference/sentry-cast-source-evidence.json')
 os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**c)
