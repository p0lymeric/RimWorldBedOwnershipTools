# Bed Ownership Tools

## Summary
- Communal beds are free-use beds that do not have set owners, according to the game's assignment system.
- Non-communal beds have set owners, where the affinity of the ownership is classified as "pinned" or "unpinned".
- Non-communal beds are associated with an ordered attribute called an "assignment group".

When a pawn wishes to sleep, they'll attempt to use a non-communal bed in the most preferred assignment group that they own and can reach. Failing that, they will attempt to claim a non-communal bed (if not pinned to another bed) or sleep in a communal bed.

When all beds in a world are in the same assignment group, and are non-communal unpinned beds, pawns will claim beds and sleep as they would in a vanilla game.

## Communal beds
Any colonist can rest in a communal bed subject to availability.

Only occupancy is checked to qualify sleeping in a communal bed. Sleeping in a communal bed does not change the colonist's existing owned beds. Gizmos relating to setting owners are hidden to emphasize this fact.

Colonists attempt to coordinate communal bed usage, so that partners can sleep together. Single pawns prefer single beds, whereas pawns in relationships prefer beds currently occupied by their partner or empty double beds. This relationship-aware search routine can be disabled via a mod setting.

## Assignment pinning
Pinned ownership cannot be relinquished by a colonist's own will. This is accomplished by blocking voluntary actions that lead to its loss (like sleeping in another bed within the same assignment group). Involuntary (e.g. death) or player-driven actions can lead to loss of pinned ownership.

## Assignment groups
A colonist may own at most one non-communal bed within an assignment group.

When the mod is first initialized, it creates three assignment groups (Default, Home, Ship). All beds start in the Default assignment group unless the player designates otherwise. The other two groups carry no initial meaning other than serving as a starting template.

When a bed is moved to another assignment group, the game will only consider assignment conflicts with beds in the same assignment group.

Hence, a pawn may own one bed in each assignment group. The groups themselves are ordered by priority, dictating which bed the pawn will choose when multiple assigned beds are available.

When a pawn needs rest, it will initiate a bed search, looking through all beds they own to pick the highest priority bed they can reach. They will internally set that bed to be their owned bed and hand off to the vanilla game's single-bed ownership system. Vanilla game bedroom quality evaluation and mood effects then apply as normal.

In the starter set, the Default group exists separately from the Home and Ship groups, and has no clear use. This is so that newly claimed or constructed beds are placed by default in a separate group from the Home group. It's also valid to use the Default group to hold home bed assignments, and use pinning to protect assignments.
