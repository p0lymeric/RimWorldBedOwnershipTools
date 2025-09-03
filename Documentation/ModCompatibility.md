## Mod compatibility with Bed Ownership Tools

Bed Ownership Tools patches much of RimWorld's code related to building ownership and Pawn sleeping routines.
Since many other mods also do similar things, it's inevitiable that there are conflicts.

This page lists mods where support has been added by Bed Ownership Tools.

| Date       | Status | BOT fix version | Mod name                                     | Mod URL                                                           | Remarks                                                                                                                                                                                |
|------------|--------|-----------------|----------------------------------------------|-------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 2025-09-03 | OK     | 1.0.0           | Dubs Mint Menus                              | https://steamcommunity.com/sharedfiles/filedetails/?id=1446523594 | No patches. BOT's assignment group feature should work with most UI mods that lightly augment the vanilla assignment dialog.                                                           |
| 2025-09-03 | OK     | 1.0.0           | Hospitality (Continued)                      | https://steamcommunity.com/sharedfiles/filedetails/?id=3509486825 | BOT adds 1 Harmony patch against Hospitality's code. BOT locks out its features from being used on guest beds.                                                                         |
| 2025-09-03 | OK     | 1.0.2           | Set Owner for Prisoner Beds                  | https://steamcommunity.com/sharedfiles/filedetails/?id=2053931388 | No patches. BOT only touches assignment candidate lists for non-prisoner, non-medical beds as of v1.0.2.                                                                               |
| 2025-09-03 | OK     | 1.0.3           | One bed to sleep with all - Polycule Edition | https://steamcommunity.com/sharedfiles/filedetails/?id=3244294636 | BOT adds 5 Harmony patches and 2 remote calls against OBTSWA's code. BOT locks out the communal bed feature when polyamory mode is enabled on a bed.                                   |
