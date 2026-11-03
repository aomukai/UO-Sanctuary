#!/usr/bin/env python3
"""Ward explicit-context actual recipient/AI,curse/lifecycle and retained pet world acceptance."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    probes=('goatman-formula','goatman-creation','generic-pet-warp-candidate','generic-pet-monster-placement','generic-pet-warp-primitives','generic-pet-warp','generic-pet-motion-services','generic-pet-follow-movement','generic-pet-follow-executor','generic-pet-decisions','generic-pet-movement','generic-pet-parameters','generic-pet-path-commit','generic-pet-footprints','generic-pet-nearby','pet-collision-reads','pet-collision-writes','pet-room-links','pet-monster-placement-hooks','pet-path-targets')
    files={n+'-source-probe.json':ROOT/'.local/class-skills'/(n+'-source-probe.json') for n in probes}
    files['grim-ward-curse-source-probe.json']=ROOT/'.local/class-skills/grim-ward-curse-source-probe.json'
    files['grim-ward-lifecycle-source-probe.json']=ROOT/'.local/class-skills/grim-ward-lifecycle-source-probe.json'
    files.update({
        'goatman-tooltip-observation.json':ROOT/'.local/class-skills/goatman-tooltip-observation.json',
        'iron-golem-birth-graph-source-cases.json':ROOT/'.local/class-skills/iron-golem-birth-graph-source/cases.json',
        'iron-golem-body-source-cases.json':ROOT/'.local/class-skills/iron-golem-body-source/cases.json',
        'necromancer-pet-ai-source-probe.json':ROOT/'.local/class-skills/necromancer-pet-ai-source-probe.json',
        'skeleton-birth-source-cases.json':ROOT/'.local/class-skills/skeleton-birth-source/cases.json',
        'skeleton-roster-source-cases.json':ROOT/'.local/class-skills/skeleton-roster-source/cases.json',
        'astar-path-source-cases.json':ROOT/'.local/class-skills/astar-path-source/cases.json',
        'toward-path-source-probe.json':ROOT/'.local/class-skills/toward-path-source-probe.json'})
    check(stage=ROOT/'.local/class-skills/grim-ward-transport',extra_files=files,
        extra_families=('iron-golem-birth-graph','iron-golem-body','iron-golem-world','skeleton-contact','skeleton-world','skeleton-birth','skeleton-roster','skeleton-combat','summon-difficulty','pet-obstacle','pet-reconnect','goatman-body','goatman-foundation','pet-native-placement','pet-placement-world','grim-ward-transport'),
        extra_checks=('grim-ward-lifecycle-check','grim-ward-curse-check','necromancer-pet-ai-check','necromancer-pet-targets-check','astar-path-check','toward-path-check',*tuple(n+'-check' for n in probes if n.startswith(('generic-pet-','pet-collision-','pet-room-','pet-monster-','pet-path-')))),
        extra_live_commands=('iron-golem-world-live','skeleton-world-live','skeleton-combat-live','pet-obstacle-live','grim-ward-transport-live'),
        extra_manifest_names={'pet-obstacle':'pet-obstacle-fixture.json','pet-reconnect':'pet-reconnect-fixture.json'},
        extra_connected_checks=('pet-reconnect-reload-check',),extra_wait_results={'pet-reconnect-reload-check':'pet-reconnect-reload-result'},
        extra_target_input_commands=('skeleton-birth-right','skeleton-birth-quick'),extra_inputs=(Path(__file__).resolve(),))
