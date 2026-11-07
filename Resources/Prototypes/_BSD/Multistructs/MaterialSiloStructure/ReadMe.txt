This file is here to explain the reasoning behind some of the design decisions and how the silo multistruct operates.

The Core is denoted in the diagrams as [C]. The core is the main structure you interact with, other interactive structures that exist are denoted as [i]

Connectors are present they only come in 3 forms:
- T junctions
- Straight
- Corner (north to east and north to west)
A four-way junction was not added in order to add additional complexity to the construction of the silo, but can theoretically be achived using larger silo storage units

Storage units hold materials they should generally obay the following formular:

[Materials Stored] = 100 * [Overall Size of the StorageStructure in Tiles] ^ 2 * ( 1 / [Material tier] ^ 2)

[OverallSize of the StorageStructure in Tiles] = number of tiles the structure takes

[Material tier] = the rarity tier of the material:
Tier 1:
- Steel
- Glass
- Plastic
Tier 2:
- Uranium
- Gold
- Silver
Tier 3:
- Plasma

Research should not offer better small constructs but enable construction of ever larger structures.
They are:

Tier 1 Silo:
- 1 tile size limit
- only fits 1 material type

Tier 2 Silo:
- 4 tile size limit
- fits 1 tier of materials

Tier 3 Silo:
- no size limit
- fits any material

WARNING to declare new materials please look at the SignalSalvage system thanks ^^
File contains the words: THIS_FILE_TO_ADD_NEW_MATERIALS
and can be found at Content.Omu.Server/_BSD/SignalSalv/Helpers/MaterialListsMatTypes.cs