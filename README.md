[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3558407174)

[GitHub](https://github.com/p0lymeric/RimWorldBedOwnershipTools/releases)

# Bed Ownership Tools

![communal beds](About/Preview.png)

Bed Ownership Tools reduces micromanagement of colonists' bed assignments by introducing new ownership controls on beds.

In my RimWorld playthroughs, I didn't like how sending colonists on quests or designating emergency sleeping quarters often led to them forgetting home bed assignments.

With the release of Odyssey, I started to love questing around the world. But in that love was still pain, and after bumping my head fixing bed assignments too many times, I broke.

A brief psychotic wander later...

## Communal beds

Communal beds are good for situations when colonists need a bed, but cannot access a permanent bed due to travel, zoning restrictions, or limited availability of permanent bedrooms.

![communal beds](Collateral/DocMedia/ReadmeCommunal.png)

Instead of assigning named owners to beds, you can now mark a bed for "communal" use. When a colonist cannot access or claim an owned bed, they'll use a communal bed before sleeping on the ground. While using a communal bed, they'll still remember their previous bed.

Communal beds are highly versatile for early to late game use. Use cases for communal beds include: Odyssey gravship bedrooms, Anomaly incident shelters, or Royalty guest barracks.

## Pinned assignments

Pinned assignments prevent colonists from forgetting prior bed assignments, unless manually reassigned by the player (or arrested, killed, divorced, etc.)

![pinned assignments](Collateral/DocMedia/ReadmePinned.png)

Perhaps you've sent several colonists to a quest site and mass-claimed buildings before. A brief undraft to have the (exhausted) troops perform some deconstruction, and half of your colony has suddenly forgotten their home beds in favour of some dead tribals' bed rolls.

By pinning those colonists' home bed assignments beforehand, they'll sleep on the ground instead. This gives you a chance to intervene by designating those bed rolls as communal beds (or you could push them to finish their tasks). In any case, you wouldn't need to fix their bed assignments when they return home.

## Assignment groups

Assignment groups allow colonists to own and automatically switch between multiple beds based on priority rankings.

![assignment groups](Collateral/DocMedia/ReadmeAssignmentGroup.png)

You may find multiple assignments useful for late-game colonies where colonists have personalized bedrooms.

Perhaps you have a large gravship and wish to build second bedrooms on the ship. But, you might have a greedy colonist whose bedrooms should be just slightly fancier than your archon's. Or many couples whose bed assignments are painful to redo.

With assignment groups, you can set up ownership associations once per bed and have the game automatically switch between them.

### Getting started with assignment groups

Let's say you wish for your colony to normally sleep in their home base, but also have dedicated beds on their gravship.

1. Shift-select all the beds in the base, and click "Assign group" > "Home".
2. Then, do the same for the beds on the ship, with "Assign group" > "Ship".
3. Set each bed's ownership with "Set owner". You should notice that assignments within the Home group and Ship group don't conflict with each other.

Now, when your gravship is landed at your home base, your crew will sleep in their Home bedrooms.
When a crew member is on a mission, or if they've been zoned into the gravship, they'll use their Ship bed instead.

Let's say you want your ship's captain to sleep in their quarters when possible (for the fun of it), while the rest of the crew prefer to sleep at home. To do this, you can create another assignment group.

![assignment groups](Collateral/DocMedia/ReadmeAssignmentGroupEditDialog.png)

1. To do this, click on any bed, and select "Assign group" > "Edit...".
2. A dialog will appear where you can add/rename/remove groups, and reorder their priorities.
3. Create a new untitled group, rename it to "Cap'n", and prioritize it above Home.
4. Click on the captain's intended bed on the gravship, and select "Assign group" > "Cap'n".

Maybe the captain got malaria and is out of commission for an urgent quest. No worries at all.
- While the ship is away, the captain will automatically use their Home bed instead.
- The crew members who leave for the quest will switch from their Home to Ship beds on the first night out.

In the starter set, the Default group exists separately from the Home and Ship groups, and has no clear use.
This is so that newly claimed or constructed beds are placed by default in a separate group from the Home group.
It's also valid to use the Default group to hold home bed assignments, and use pinning to protect assignments.

### Brief technical explanation of assignment groups

When the mod is first initialized, it creates three assignment groups (Default, Home, Ship).
All beds start in the Default assignment group unless you designate otherwise. The other two groups carry no initial meaning other than serving as a starting template.

Having all beds in the same assignment group produces vanilla game ownership behaviour. When a bed is moved to another assignment group, the game will only consider assignment conflicts with beds in the same assignment group.

Hence, a colonist may own one bed in each assignment group. The groups themselves are ordered by priority, dictating which bed a colonist will choose when multiple assigned beds are available.

When a colonist needs rest, it will initiate a bed search, looking through all beds they own to pick the highest priority bed they can reach. They will internally set that bed to be their owned bed and hand off to the vanilla game's single-bed ownership system. Vanilla game bedroom quality evaluation and mood effects then apply as normal.

## Issues
Please post in the Bug Reports thread on the mod's [Steam Workshop bug thread](https://steamcommunity.com/workshop/filedetails/discussion/3558407174/600787986327757372/), or [file an issue on GitHub](https://github.com/p0lymeric/RimWorldBedOwnershipTools/issues).

## Save compatibility
It's safe to add or remove this mod at any time. You may see harmless one-time errors printed to the game's console following mod removal.

Upon mod removal, colonists with multiple bed assignments will keep the last non-communal bed they used.

## Licensing
This mod is released under the terms of the MIT license.

The following notice is posted from the RimWorld EULA.
> Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon.
