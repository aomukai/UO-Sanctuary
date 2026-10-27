#!/usr/bin/env python3
"""Saved Totem motion and natural Oak clocks with retained body/aura/summon checks."""
import check_creeper_stats as retained


if __name__ == '__main__':
    retained.STAGE = retained.ROOT / '.local/class-skills/oak-motion'
    retained.check(extra_commands=['totem-ai-check', 'oak-sage-stats-check', 'oak-sage-body-check', 'oak-world-check', 'battle-orders-check'],
                   extra_reload_commands=['oak-expired-reload-check', 'oak-sage-body-reload-check', 'oak-world-reload-check', 'battle-orders-reload-check'],
                   extra_probes=['oak-sage', 'oak-aura', 'totem-ai'],
                   extra_fixtures=['oak-sage-body-fixtures.json', 'oak-world-fixtures.json', 'oak-expired-aura-fixtures.json'],
                   extra_live_commands=['oak-world-live'], extra_before_save_commands=['oak-expired-stage'])
    print('Oak saved motion/life aura passes three boots/two actual reloads and three connected natural follow/expiry sessions; learned casting remains unavailable.')
