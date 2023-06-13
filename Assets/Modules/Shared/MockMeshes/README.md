Scenes that use ARDK meshing require a mock mesh to be loaded in order to run in Unity Editor. Each scene that uses meshing references the path of the mock mesh to load in its MockMesh GameObject in the scene hierarchy.

These meshes are provided with the ARDKExamples package. They're included here so that you can run ARVoyage without having to include ARDKExamples.

You can add your own saved meshes and then update the path in the MockMesh GameObject as well.