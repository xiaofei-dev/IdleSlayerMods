# Ascension and Purchases

## Normal Ascension

AutoProgression compares pending Slayer Points with lifetime Slayer Points.
At the default `50%` threshold, pending points must reach half of lifetime
points. The check runs immediately after
enabling and every 1 minute by default.

Only normal Ascension is supported. Ultra Ascension is never invoked.

After Ascension, the optional Buy All phase repeatedly spends available Slayer
Points until two stable passes make no further progress. Other services pause
behind a two-second post-Ascension lock while game objects are rebuilt.

## Skills

Available shop skills are purchased every 10 seconds. Debug output aggregates
successful purchases into a 30-second summary instead of logging every click.

## Normal Equipment

Only unlocked normal shop equipment is considered. The service begins with the
newest unlocked item and exhausts its valid purchase amount before moving up.
Purchases are made in supported 10- and 50-level increments rather than a long
sequence of single levels.

If no equipment meets the minimum purchase threshold for the configured idle
period, equipment purchasing sleeps temporarily. Skill purchasing continues
during this sleep. Purchase priority determines which service receives the
first opportunity when both are enabled.

[Back to the Complete Manual](../MANUAL.md)
