Evening this TXT file exists cause some channels are predefined already:

List of current channels:
- "ResearchPoints" -> used by things that want to add research points to a server [GRID WIDE]
- "SiloNetwork" -> used by the silo network to distribute resources
- "MaterialTransit" -> used by mining structures to move materials to grids [GLOBAL]
- "LocalMatDistribution" -> used by the routers to link to the lathes
- "Signal" -> used by the off station satelites to link to the Local controll [MAP WIDE]

future networks to add:
- "Research" -> used by Laths to know what they can print from RND techs and RND consoles [GRID WIDE]
- "Telecoms" -> used by headsets to communicate [Unknown yet]
- "AI" -> used by the AI to controll things [Unknown yet possibly entity dependent]
- "Camera" -> used by cameras [GRID WIDE]
- "CameraAccess" -> used by the camera monitor to access the camera system [GRID WISDE]


Old descriptor:

General idea of this system it to use internal clarifier to either connect:
(client -> server)
or
(server -> clients)

this is mostly a UI code challange as the clients usually need to save to whom they are linked. The current linked to system parically could work for that,
yet to use that requires physical movment in the game and makes it hard to link via ui and exclude certain objects from the search list.

This plans to adress this:
primary work way:
- using internal struct to declare the types of belonging and if they act as a server or as a client for that type(YML definable idealy
   [probably not as our yml does not support structs nor strings lol so manual declaration in the C# code will be required by developers])
- when a querry is made it querries for all entities with the component and returns the specialised lists
- lastly we safe the link in a directory, this happens on both the client and the server[important note this is 2 devices/entites not server/clientside]


TODO: update this descriptor