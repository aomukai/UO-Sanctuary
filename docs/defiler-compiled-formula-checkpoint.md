# Defiler compiled-formula boundary

Reference: **D2R Reign of the Warlock 3.3.93847**. This checkpoint records new reference evidence; it enables no talent or damage multiplier.

The installation's owned `skills.bin` and `skillscode.bin` were extracted read-only with the existing hash-pinned CASC reader, checking the executable and build manifest before and after extraction. No game-memory read or write was involved. The files contain429skill records of748bytes and10,324bytes of compiled formula data.

All429records and12passive slots agree with the pinned stat-name mapping and formula presence: **5,148 stat matches and5,148 formula-presence matches**. This establishes current passive stat/offset layouts at186/216; it does not establish formula runtime semantics.

Defiler skill377, passive slot2, is `damagepercent` stat25. Its formula points to19bytes at offset8152. The bytes contain parameter/rank/hard-Mastery references and arithmetic, but also0x02 markers whose modern interpretation remains unverified. The exact bytes and a specifically **legacy** token view are in [source evidence](../tools/reference/defiler-compiled-formula-evidence.json).

The complete unchanged older evaluator consumes those actual bytes in **5,355** rank/hard-Mastery contexts. It returns hard Mastery alone because its handling of0x02 exits before the remaining arithmetic. **5,354** cases differ from the proposed10*(rank-1+hard Mastery) curve. These are incompatibility results,not passing current damage comparisons. No callback-arity stripping,marker removal or production text repair is used to manufacture a result. The earlier failed tenfold assertion and its binary/logs/outputs are preserved.

The older skill and description loaders pass their text directly to the compiler; no implicit outer wrapping was found in those callers. The current formula-context candidate at0x2bae90 is observed unreadable. Its mapping observation sampled no code. Current callback/marker behavior therefore remains an explicit source gap. The raw text compiler failure from the preceding checkpoint remains valid but is a separate issue from this bytecode-format mismatch.

Defiler's tooltip definition lists life,defense and Health Link information; it may not expose ordinary damage. The optional tooltip question can be skipped if no damage line exists. A hypothetical multiplier must not be accepted from that absence.

The latest production checkpoint remains `524328631d6b8eae7e2e040660bf0dfc34ffa11e`:393broader/61focused checks,original Warlock/Witch Gazer bodies and protected installation/saves. No C# or runtime data changes are included here. Defiler offense,Health Link,GenericPet/learned summoning and the full eight-class goal remain unfinished. Effects and icons retain their separate existing registers.
