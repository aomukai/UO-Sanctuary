#!/usr/bin/env python3
"""Current Sorceress cohort with adopted terrain,cold corpses and native shield/return scopes."""
import os,sys
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_sorceress_spell_link import configuration as previous
from check_explosive_terrain_adopted import WALL_INPUTS
from check_assassin_planned_acceptance import paced_clients
STAGE=ROOT/'.local/class-skills/sorceress-lifecycle-acceptance'
def configuration():
    c=previous();c['stage']=STAGE
    c['extra_families']=tuple(dict.fromkeys((*c['extra_families'],'explosive-terrain','unresisted-shields','player-melee-return','cold-corpse-proposal','corpse-shatter-guard')))
    c['extra_target_input_commands']=tuple(dict.fromkeys((*c['extra_target_input_commands'],*WALL_INPUTS)))
    c['extra_wait_results'].update({'corpse-shatter-guard-check':'corpse-shatter-guard-result','corpse-shatter-guard-reload-check':'corpse-shatter-guard-result'})
    c['extra_inputs']+=tuple(ROOT/p for p in ('tests/check_sorceress_lifecycle_acceptance.py','tools/reference/ordinary-cold-corpse-approval.json','tools/reference/ordinary-cold-corpse-adoption-native-evidence.json','tools/reference/unresisted-shields-native-evidence.json','tools/reference/explosive-terrain-rule-approval.json','tools/reference/player-melee-return-native-evidence.json','tools/reference/sorceress-spell-link-native-evidence.json'))
    helpers=tuple(Path(m.__file__).resolve()for m in tuple(sys.modules.values())if getattr(m,'__file__',None)and Path(m.__file__).resolve().parent==ROOT/'tests');c['extra_inputs']=tuple(dict.fromkeys((*c['extra_inputs'],*helpers)))
    for p in c['extra_inputs']:assert p.is_file(),p
    return c
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():check(**configuration())
