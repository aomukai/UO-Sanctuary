#!/usr/bin/env python3
"""Prepared Tail hit/KK suppliers with original kick/charge cohorts; three boots."""
import argparse,os
from pathlib import Path
from check_iron_golem_birth import check

ROOT=Path(__file__).resolve().parents[1]

def configuration(broader=False):
    files={name:ROOT/'.local/class-skills'/name for name in (
        'dragon-tail-clock-source-probe.json','dragon-tail-rate-source-probe.json','dragon-tail-source-probe.json',
        'dragon-kick-damage-source-probe.json','dragon-kick-packet-source-probe.json','tiger-strike-source-probe.json')}
    for name in ('dragon-kick-damage-source-evidence.json','tiger-strike-source-evidence.json'):
        files[name]=ROOT/'tools/reference'/name
    own=dict(stage=ROOT/'.local/class-skills/dragon-tail-preparation',extra_files=files,
                extra_families=('dragon-tail-preparation','dragon-kick','kick-packet','tiger-strike'),
                extra_inputs=(Path(__file__).resolve(),ROOT/'tools/reference/dragon-tail-sanctuary-decision.json',
                              ROOT/'tools/reference/dragon-tail-lifetime-source-evidence.json'))
    if not broader:return own
    from check_wake_fire_automatic import configuration as previous
    c=previous();c['stage']=ROOT/'.local/class-skills/dragon-tail-preparation-broader'
    c['extra_files'].update(files)
    c['extra_families']+=('wake-learned',*own['extra_families'])
    c['extra_checks']+=('wake-learned-mechanics-check',)
    c['extra_connected_checks']+=('wake-learned-check','wake-learned-reload-check')
    c['extra_wait_results'].update({'wake-learned-check':'wake-learned-result','wake-learned-reload-check':'wake-learned-reload-result'})
    c['extra_target_input_commands']+=('wake-learned-left','wake-learned-right','wake-learned-quick')
    c['extra_ground_input_commands']=c['extra_target_input_commands']
    c['extra_instant_input_commands']+=('wake-learned-left',)
    c['extra_inputs']+=own['extra_inputs']
    return c

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--broader',action='store_true');args=parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration(args.broader))
