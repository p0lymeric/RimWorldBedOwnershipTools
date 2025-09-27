# Enhanced deathrest

better bedtimes for eepy sangs

zzz...

## Summary
- Spare deathrest bindings: allows a deathrester to switch between using deathrest buildings beyond their binding capacity.
- Automatic deathrest: allows a deathrester to follow automatic deathrest schedules.

## Spare deathrest bindings
Normally, RimWorld allows a deathrester to bind to at most one deathrest casket, and at most the number of support buildings dictated by their deathrest serum capacity. Once bound, a deathrester cannot switch between caskets or support buildings unless existing bound buildings are first destroyed.

With "spare deathrest bindings", a player can maintain redundant deathrest buildings for a deathrester beyond their binding capacity. This eliminates pressure to transport a Pawn to one particular location to deathrest, or reinstall/destroy existing deathrest buildings to follow a travelling Pawn.

### Binding permanance
Even with spare bindings, binding is normally permanent.

A mod setting exists to disable the permanent nature of deathrest bindings. This setting allows buildings to be reallocated between deathresters as desired.

### Interactions with assignment groups
Assignment groups are used to prioritize caskets for automatic deathrest.

Pawns can be assigned to at most one deathrest casket per assignment group, but may be bound to more than one casket associated with the same assignment group.

Similarly, if assignment group support is disabled, pawns may be bound to more than one deathrest casket, but can only be assigned to one at a time.

### UI tweaks
Because pawns can now be bound to more deathrest caskets than assigned, map labels will show the deathrest casket's owner as either the casket's bindee or assignee.

## Automatic deathrest
With "automatic deathrest", a deathrester can autonomously deathrest based on a configured rule. A new gizmo is added next to their deathrest auto-wake gizmo, to select an auto-deathrest scheme.

Automatic deathrest gives 9 scheduling options governed by 3 major scheduling schemes.
- Manual (manual)
- 3 hours to exhaustion (exhaustion-synchronous)
- 1 day to exhaustion (exhaustion-synchronous)
- Aprimay/Septober 1-5 (calendar-synchronous)
- Aprimay/Septober 6-10 (calendar-synchronous)
- Aprimay/Septober 11-15 (calendar-synchronous)
- Jugust/Decembary 1-5 (calendar-synchronous)
- Jugust/Decembary 6-10 (calendar-synchronous)
- Jugust/Decembary 11-15 (calendar-synchronous)

### Trigger conditions

The text in this section does not apply to the "manual" scheduling option.

Automatic deathrest is triggered using the game's job system.

Deathresters will only automatically deathrest if they can reach an assigned/bound deathrest casket. With an optional mod setting. they can also automatically target a non-communal or communal bed. Deathrest will never be automatically initiated on the ground or in the sun.

An in-game alert is fired whenever a deathrester's automatic deathrest scheduler is in a armed state, but cannot locate a valid location to deathrest.

Deathresters will normally respect their activity schedule and will attempt to start deathresting during assigned bedtime. If a deathrester is nearing exhaustion (after a "needs deathrest" alert is fired), they will ignore their activity schedule to deathrest. Similarly, if a pawn does not require normal sleep, they will attempt to initiate deathrest across all activity types.

### Interactions with assignment groups
Assignment groups are used to prioritize between multiple deathrest caskets for automatic deathrest.

Automatic deathrest casket searches consider both the binding and assignment field on caskets. Assignments hold higher priority than bindings inside an assignment group.

### Manual scheduling
Manual scheduling is the default player-managed option, identical to vanilla deathrest management.

As before, the game displays a persistent "needs deathrest" alert below the 10% deathrest need mark (3 days to exhaustion), and it is up to the player when the pawn should deathrest, if at all.

The player should probably manage deathrest themselves if the colony depends on a small number of important sanguophages, in preference over automatic scheduling.

### Exhaustion-synchronous schedules
Exhaustion-synchronous schedules trigger whenever a deathrester's deathrest need level falls below a certain watermark.

- 3 hours to exhaustion: Start deathrest below the 1% deathrest need mark (3 hours to exhaustion). The game will only display a "needs deathrest" alert after the Pawn becomes exhausted.
- 1 day to exhaustion: Start deathrest below 24 hours to exhaustion. The game will display a "needs deathrest" alert after 12 hours to exhaustion.

### Calendar-synchronous schedules
The calendar scheduler is designed to facilitate coordinated deathrest between multiple deathresters, and to allow the player to anticipate when each deathrester will deathrest.

Inside their scheduled period, deathresters will deathrest regardless of their need level. This forces synchronization to the schedule. Waste is minimized in steady state, since the natural period of deathrest (33.125-35.000 days with 100% recovery) approximately matches the forced deathrest period of 30 days.

Deathresters will always automatically schedule deathrest below 24 hours to exhaustion, to avoid exhaustion penalties. The game will display a "needs deathrest" advisory at 12 hours to exhaustion. Exhaustion fallback should not be hit in steady state.

Following a calendar-synchronous schedule forces Auto-wake on. Auto-wake is required for calendar-synchronous schedules to maintain stability and behaves slightly differently with the calendar scheduler. Typically, deathresters wake immediately after reaching the safe wake threshold (80% need recovery; 24.0 days of uptime). When a deathrester follows a calendar schedule, they will wait until they first reach the safe wake threshold, then wake only after reaching 100% deathrest need (30.0 days of uptime).

The calendar scheduler accounts for time zones, to handle travelling Pawns. The scheduler becomes active at 0:00 in the local time of the selected interval and deactivates by 23:60 on the last day of the interval. To prevent double resting across time zone changes, the scheduler will not trigger again if the last deathrest completed in the same interval in any time zone on the rimworld.
