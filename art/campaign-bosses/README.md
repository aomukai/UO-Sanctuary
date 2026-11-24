# Campaign boss animation art

| Body | Boss | Gameplay art |
| --- | --- | --- |
| 2005 | King Leoric | Jagged crown, royal mantle and a two-handed executioner's sword |
| 2006 | Archbishop Lazarus | Crimson and purple vestments, gold crozier and a pale spirit in his casting hand |
| 2007 | Diablo | Blood-red hide, black wings, spiked steel armour, fiery eyes and a whip |

The three turntables use the succubus importer's directional convention. The five columns face down, lower left, left, upper left and up on screen. ClassicUO mirrors the other directions. The first four rows provide idle, stride, windup and release poses. Leoric and Lazarus also have a death row.

The separate walk sheets have five rows of facings and eight successive footfall poses across each row. ClassicUO advances four poses per tile using the displayed distance travelled. A step ending halfway around the cycle leaves the opposite foot ready to lead the next step. The installed UO daemon (body 9) has ten walk frames and one static stand in every authored facing. The built-in image generator used native sprite poses as gait references. [Motion art prompts](motion-prompts.txt)

Leoric and Lazarus use one resting pose per facing. Diablo takes an 8.4-second breath. Only a small patch around his ribcage deforms. Diablo’s upright stance comes from the nine views in [his standing sheet](diablo-stand.png), registered between his hooves. The sheet alternates the five resting views with the four intermediate turns. Leoric and Lazarus use the shared stationary turn sheet for their intermediate angles. Combined with native mirroring, this gives sixteen visible headings. The client takes the short arc on a turn and faces the target immediately on a directed combat action. Lazarus still waits at his altar and teleports during his ritual.

Diablo's fifth key comes from the separate face-up corpse strip. Its first rotation puts the hooves nearest the viewer. Two native death slots share the same fall and four flame phases. The client cycles those last four frames while the corpse is on the ground. Fire uses the middle row of Sanctuary's Fire Wall art and stays cosmetic.

Run `python3 tools/build_campaign_boss_animations.py` from the project root to rebuild the indexed archive. The script records the individual ground pivots and cuts. The client loads the resulting MUL and IDX from `Data/Sanctuary/Animations`. The server and client share the body IDs in `shared/CampaignBossAppearance.cs`.

[Diablo walking to a stop](diablo-stance.mp4) · [Movement in game](motion-preview.mp4) · [In-game preview](arena-bosses.jpg) · [Diablo's remains](diablo-remains.gif)
