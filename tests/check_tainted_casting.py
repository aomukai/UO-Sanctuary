#!/usr/bin/env python3
"""Tainted body/qualified fire hit, Hydra and the complete retained supplier cohort."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    probes=('goatman-formula','goatman-creation','generic-pet-warp-candidate','generic-pet-monster-placement','generic-pet-warp-primitives','generic-pet-warp','generic-pet-motion-services','generic-pet-follow-movement','generic-pet-follow-executor','generic-pet-decisions','generic-pet-movement','generic-pet-parameters','generic-pet-path-commit','generic-pet-footprints','generic-pet-nearby','pet-collision-reads','pet-collision-writes','pet-room-links','pet-monster-placement-hooks','pet-path-targets')
    files={n+'-source-probe.json':ROOT/'.local/class-skills'/(n+'-source-probe.json') for n in probes}
    files['druid-form-cast-clock-source-probe.json']=ROOT/'.local/class-skills/druid-form-cast-clock-source-probe.json'
    files['assassin-cast-clock-source-probe.json']=ROOT/'.local/class-skills/assassin-cast-clock-source-probe.json'
    files['blade-shield-damage-source-probe.json']=ROOT/'.local/class-skills/blade-shield-damage-source-probe.json'
    files['sorceress-cast-clock-source-probe.json']=ROOT/'.local/class-skills/sorceress-cast-clock-source-probe.json'
    files['barbarian-cast-clock-source-probe.json']=ROOT/'.local/class-skills/barbarian-cast-clock-source-probe.json'
    files['battle-orders-source-probe.json']=ROOT/'.local/class-skills/battle-orders-source-probe.json'
    files['paladin-cast-clock-source-probe.json']=ROOT/'.local/class-skills/paladin-cast-clock-source-probe.json'
    files['holy-shield-cast-source-cases.json']=ROOT/'.local/class-skills/holy-shield-cast-source/cases.json'
    files['necromancer-cast-clock-source-probe.json']=ROOT/'.local/class-skills/necromancer-cast-clock-source-probe.json'
    files['amazon-cast-clock-source-probe.json']=ROOT/'.local/class-skills/amazon-cast-clock-source-probe.json'
    files['inner-sight-source-probe.json']=ROOT/'.local/class-skills/inner-sight-source-probe.json'
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
    files['tainted-formula-source-cases.json']=ROOT/'.local/class-skills/tainted-foundation-source/formula.json'
    files['tainted-fireball-source-cases.json']=ROOT/'.local/class-skills/tainted-foundation-source/fireball.json'
    files['tainted-cast-source-cases.json']=ROOT/'.local/class-skills/tainted-fireball-cast-source/cases.json'
    check(stage=ROOT/'.local/class-skills/tainted-casting',extra_files=files,
        extra_families=('tainted-casting','tainted-fireball','tainted-body','hydra-world','tainted-foundation','iron-golem-birth-graph','iron-golem-body','iron-golem-world','skeleton-contact','skeleton-world','skeleton-birth','skeleton-roster','skeleton-combat','summon-difficulty','pet-obstacle','pet-reconnect','goatman-body','goatman-foundation','pet-native-placement','pet-placement-world','grim-ward-transport','amazon-cast-clock','inner-sight','slow-missiles-windup','slow-missiles-cast','holy-shield-cast','battle-orders','player-teleport','blade-shield','druid-form-casting'),
        extra_checks=('druid-human-cast-clock-check','druid-form-delay-live-check','assassin-cast-clock-check','sorceress-cast-clock-check','barbarian-cast-clock-check','paladin-cast-clock-check','slow-missiles-restore-check','necromancer-cast-clock-check','grim-ward-world-pulse-check','grim-ward-lifecycle-check','grim-ward-curse-check','necromancer-pet-ai-check','necromancer-pet-targets-check','astar-path-check','toward-path-check',*tuple(n+'-check' for n in probes if n.startswith(('generic-pet-','pet-collision-','pet-room-','pet-monster-','pet-path-')))),
        extra_live_commands=('iron-golem-world-live','skeleton-world-live','skeleton-combat-live','pet-obstacle-live','grim-ward-transport-live','slow-missiles-windup-live'),
        extra_manifest_names={'battle-orders':'battle-orders.json','holy-shield-cast':'holy-shield-cast-checks.json','pet-obstacle':'pet-obstacle-fixture.json','pet-reconnect':'pet-reconnect-fixture.json'},
        extra_connected_checks=('tainted-casting-reload-check','tainted-fireball-reload-check','hydra-world-reload-check','pet-reconnect-reload-check','player-teleport-reload-check'),extra_wait_results={'tainted-casting-reload-check':'tainted-casting-reload-result','tainted-fireball-reload-check':'tainted-fireball-reload-result','hydra-world-reload-check':'hydra-world-reload-result','druid-form-delay-live-check':'druid-form-delay-result','blade-shield-check':'blade-shield-result','blade-shield-reload-check':'blade-shield-result','player-teleport-reload-check':'player-teleport-reload-result','battle-orders-check':'battle-orders-result','battle-orders-reload-check':'battle-orders-result','pet-reconnect-reload-check':'pet-reconnect-reload-result','inner-sight-check':'inner-sight-result'},
        save_command='holy-shield-cast-save',save_result='holy-shield-cast-save',
        extra_ground_input_commands=('player-teleport-left','player-teleport-right','player-teleport-quick'),
        extra_target_input_commands=('hydra-world-right','hydra-world-quick','druid-human-cast-clock-right','druid-human-cast-clock-quick','assassin-cast-clock-right','assassin-cast-clock-quick','sorceress-cast-clock-right','sorceress-cast-clock-quick','player-teleport-left','player-teleport-right','player-teleport-quick','barbarian-cast-clock-right','barbarian-cast-clock-quick','paladin-cast-clock-right','paladin-cast-clock-quick','holy-shield-cast-right','holy-shield-cast-quick','skeleton-birth-right','skeleton-birth-quick','amazon-cast-clock-right','amazon-cast-clock-quick','necromancer-cast-clock-right','necromancer-cast-clock-quick'),extra_inputs=(Path(__file__).resolve(),))
