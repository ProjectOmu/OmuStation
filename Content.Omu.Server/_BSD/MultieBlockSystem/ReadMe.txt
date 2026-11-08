Multistructs are made of several entities.

The real entities that make up the structure will be refered to as parts [P].
There is only one "special" part known as the CORE [O] or ORIGIN it is the point that the system starts scanning.
Ususally COREs or ORIGINS are not allowed to be present more than once in a structure. Some Structures have a interface set to true.
In that case the structure will both inherit each others Controll entity.

Controll entities are imaginary entities and cant be seen by players. They exist at the core of the MAP at position (0,0) and handles the vital information.
Controll entities are attached to the Core but in case of core detachment from a structure distribute values based on the new network grath

Structure example:

[O]->[P]->[P]->[P]->[P]
\|/       \|/       \|/
[P]       [P]       [P]
\|/                 \|/
[P]->[P]->[P]->[P]  [P]

-> [CONTROLL]


We try to avoid having local information on part entites to minimise the amount of querries we need to do the main advantage of this system is that we
can treat our entities as a black box and just trust in it. (some structures may distguard it you still save on querries seeing as they are all pre done for you)

When making a yaml prototype for one of these structures the MultiBlockStructureComponent needs to be on the controll entity.

The controll entity is saved for every part of the strucutre allowing easy access to it for any part.

Adacency behavior is applied after general structure has been identified.
- type A -> next to type B: Saves number of this ajacency --> c# code needs to handle what it does.(not ideal but it works)

Any component that interacts with multistructs should always be attached to the controll entity