# Native monster combat conversion track

The user has explicitly separated assigning D2 spells to UO monsters from the class-skill project. Revived creatures may retain their living abilities until that later conversion;ordinary heal/cure spells should be removed with that work,while deliberate special support monsters may keep authored abilities such as resurrection/healing others. Native species without a D2 equivalent require an explicit relative-strength classification,not an invented identity.

**Pending scope decision:** the Slow Missiles proposal asks whether legacy UO instantaneous ranged attacks may join that same later track. The spell-conversion instruction alone is not treated as approval to defer ordinary ranged hits. See [the complete proposal](slow-missiles-rule-proposal.md);do not claim these legacy attacks already consume Slow Missiles.

Future acceptance needs to cover:

- Native ranged launch/travel/impact,hit and avoidance,item events,ammunition/wear,owner credit,terrain/expiry and saved lifecycle,using existing D2 projectile services where appropriate.
- Replacing native MagicArrow/Fireball/EnergyBolt-style timers and breath with selected D2 spell routes. A visual delay or broad AOS received-damage multiplier does not establish missile motion or ownership.
- Explicit native attackable/NPC/no-aura/boss classification while keeping actual D2 species-boss flags distinct from champion/unique grade. Unknown contexts remain reported.
- Every new D2 producer's Slow Missiles birth payload,CanSlowvelocity versus independent damage factor,packet/roll boundary,once-only parent/child treatment,unchanged durations,defender mitigation and actual post-load damage.
- Burning fields and other currently unqualified special damage contexts,including their rate/order/child ownership. Existing modifier APIs do not by themselves certify all producers.
- Final projectile/cast/impact/audio assets whose visible lifetime agrees with active damage,tracked separately from mechanical work.

Current qualified class-skill work can reuse the existing encounter arrow/fire/lightning and Graveyard source-vector firebolt paths. Their explicit native geometry adapters remain visible;this note does not certify the broader monster conversion or PvP/property scope. The four pending Warlock proposals and class-certification audit are separate.
