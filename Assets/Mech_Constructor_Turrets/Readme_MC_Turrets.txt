Hello! Thank you for the purchase. Hope the assets work well.
If they don't, please contact me slsovest@gmail.com.



_______________________
Assembling the turrets:


All the parts may have containers for mounting other parts, their names start with "Mount_".
(In some cases they may be deep in bone hierarchy).
Just drop the part in the corresponding container, and It'll snap into place.

- Start with Base ("Base_Turret_" or "Base_Tower_"), find the "Mount_top" container inside it. 
- Drop the "Base_top" part into it, find another "Mount_top" container inside it.
- Drop the Shoulders or the Cockpit into the "Base_Top"
- Find the other mounting points inside the Cockpit or Shoulders


Which parts you can drop into which containers:
(The best way would be just to explore the turrets in the demo scene).

"Mount_Top": "Base_Top", "Top_...", "Tower_...", "Shoulders_", "Cockpit_..."
"Mount_Cockpit": Cockpits
"Mount_Backpack": Backpacks
"Mount_Weapon L/R": Weapon_...", "HalfShoulder_..."
You can try to drop the "Roof" parts into "Mount_Weapon_Top" containers inside the cockpits, but generally you'll just have to eyeball it.


After the assembly, the turrets consist of many separate parts and, even with batching, can produce high number of draw calls.
You may want to combine non-animated parts into a single mesh for the sake of optimization.

All the weapons contain locators at their barrel ends (named "Barrel_end", or "Barrel_end_[number]" in case there are multiple barrels).



_________
Textures:


The source .PSD can be downloaded here: 
https://drive.google.com/file/d/16B0DQE-gr5wvC3uLAcd2BVIe5vLijsQM/view?usp=sharing

For a quick repaint, adjust the layers in the "COLOR" folders. 
You can drop your decals and textures (camouflage, for example) in the folder as well. Just be careful with texture seams.

In version 2.1, the texture brightness was changed to match the other Mech Constructor packages.
The link to the older version "Materials" folder:
https://drive.google.com/file/d/1OEoN5jFT0AdH4asYfHegb3Ruu1eS0zKA/view?usp=sharing



________
Updates:

Version 2.0 (March 2018):
Added flat-colored PBR textures.
Added new parts:
- fences (3 levels)
- top-mounted radars (3 levels)


Version 2.1 (August 2023):
- Equalized the texture brightness across all the Mech Constructor packages
- Added Metalness map
- Added HDRP Mask map