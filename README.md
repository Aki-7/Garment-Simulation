# Garment Simulator

PBD based garment simulation

## Usage

1. Prepare a 3D model with
   - skinned body mesh
   - skinned garment mesh (blend shaping is supported)
2. Add a GSBody component to the skinned body mesh object.
3. Add a GSGarment component to the skinned garment mesh object.
4. Create an empty game object and add a GSSimulator component to it.
5. Associate the GSBody and GSGarment component to the GSSimulator.
