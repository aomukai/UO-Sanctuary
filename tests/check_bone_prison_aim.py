#!/usr/bin/env python3
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    config=configuration('check_bone_barrier_cast.py');config['stage']=ROOT/'.local/class-skills/bone-prison-aim'
    config['extra_families']=('bone-barrier-cast','bone-barrier-birth','spirit-wolf-casting','psychic-hammer')
    config['extra_wait_results']={'psychic-hammer-check':'psychic-hammer-result','psychic-hammer-reload-check':'psychic-hammer-reload-result'}
    config['extra_checks']=(*config.get('extra_checks',()),'bone-prison-aim-check')
    config['extra_target_input_commands']=(*config['extra_target_input_commands'],'bone-prison-ground-right','bone-prison-ground-quick')
    config['extra_input_timeouts'].update({'bone-prison-ground-right':30,'bone-prison-ground-quick':30})
    config['extra_files'].update({name:ROOT/'.local/class-skills'/name for name in ('psychic-hammer-source-probe.json','psychic-hammer-damage-source-probe.json')})
    config['extra_inputs']=(*config['extra_inputs'],Path(__file__).resolve(),ROOT/'tests/check_bone_barrier_cast.py')
    check(**config)
