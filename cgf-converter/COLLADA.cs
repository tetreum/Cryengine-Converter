﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.Schema;
using System.Xml.Serialization;
using grendgine_collada; // No idea how to actually use this.

namespace CgfConverter
{
    //[System.Xml.Serialization.XmlRootAttribute(ElementName = "COLLADA", Namespace = "https://www.khronos.org/files/collada_schema_1_5", IsNullable = false)]
    //[XmlRootAttribute("COLLADA", Namespace = "http://www.collada.org/2005/11/COLLADASchema", IsNullable = false)]

    class COLLADA  // class to export to .dae format (COLLADA)
    {
        public XmlSchema schema = new XmlSchema();
        public FileInfo daeOutputFile;
        public XmlDocument daeDoc = new XmlDocument();                      // the COLLADA XML doc.  Going to try serialized instead.
        public CgfData cgfData;                                             // The Cryengine data
        public Grendgine_Collada daeObject = new Grendgine_Collada();       // This is the serializable class.
        XmlSerializer mySerializer = new XmlSerializer(typeof(Grendgine_Collada));

        public void WriteCollada(CgfData dataFile)  // Write the dae file
        {
            // The root of the functions to write Collada files
            // At this point, we should have a CgfData object, fully populated.
            Console.WriteLine();
            Console.WriteLine("*** Starting WriteCOLLADA() ***");
            Console.WriteLine();

            cgfData = dataFile;                                              // cgfData is now a pointer to the data object
            // File name will be "object name.dae"
            daeOutputFile = new FileInfo(cgfData.RootNode.Name + ".dae");
            GetSchema();                                                    // Loads the schema.  Needs error checking in case it's offline.
            WriteRootNode();
            WriteAsset();
            WriteLibrary_Images();
            WriteLibrary_Materials();
            WriteLibrary_Effects();
            WriteLibrary_Geometries();
            TextWriter writer = new StreamWriter(daeOutputFile.FullName);   // Makes the Textwriter object for the output
            mySerializer.Serialize(writer, daeObject);                      // Serializes the daeObject and writes to the writer
            Console.WriteLine("End of Write Collada");
        }

        private void WriteRootNode()
        {
            //Grendgine_Collada collada = new Grendgine_Collada();
            daeObject.Collada_Version = "1.5.0";
        }

        public void GetSchema()                                             // Get the schema from kronos.org.  Needs error checking in case it's offline
        {
            schema.ElementFormDefault = XmlSchemaForm.Qualified;
            schema.TargetNamespace = "https://www.khronos.org/files/collada_schema_1_5";
        }

        public void WriteAsset()
        {
            // Writes the Asset element in a Collada XML doc
            DateTime fileCreated = DateTime.Now;
            DateTime fileModified = DateTime.Now;           // since this only creates, both times should be the same
            Grendgine_Collada_Asset asset = new Grendgine_Collada_Asset();

            Grendgine_Collada_Asset_Contributor [] contributors = new Grendgine_Collada_Asset_Contributor[2];
            contributors[0] = new Grendgine_Collada_Asset_Contributor();
            contributors[0].Author = "Heffay";
            contributors[0].Author_Website = "https://github.com/Markemp/Cryengine-Converter";
            contributors[0].Author_Email = "markemp@gmail.com";
            contributors[0].Source_Data = cgfData.RootNode.Name;                    // The cgf/cga/skin/whatever file we read
            // Get the actual file creators from the Source Chunk
            contributors[1] = new Grendgine_Collada_Asset_Contributor();
            foreach (CgfData.ChunkSourceInfo tmpSource in cgfData.CgfChunks.Where(a => a.chunkType == ChunkType.SourceInfo))
            {
                contributors[1].Author = tmpSource.Author;
                contributors[1].Source_Data = tmpSource.SourceFile;
            }
            asset.Created = fileCreated;
            asset.Modified = fileModified;
            asset.Up_Axis = "Z_UP";
            asset.Title = cgfData.RootNode.Name;
            daeObject.Asset = asset;
            daeObject.Asset.Contributor = contributors;

        }
        public void WriteLibrary_Images()
        {
            // I think this is a  list of all the images used by the asset.
            Grendgine_Collada_Library_Images libraryImages = new Grendgine_Collada_Library_Images();
            daeObject.Library_Images = libraryImages;
            List<Grendgine_Collada_Image> imageList = new List<Grendgine_Collada_Image>();
            // We now have the image library set up.  start to populate.
            int numImages = 0;
            foreach (MtlFormat mats in cgfData.MatFile.Materials)
            {
                // each mat will have a number of texture files.  Need to create an <image> for each of them.
                int numTextures = mats.Textures.Count;
                for (int i=0; i < numTextures; i++ )
                {
                    // For each texture in the material, we make a new <image> object and add it to the list. 
                    Grendgine_Collada_Image tmpImage = new Grendgine_Collada_Image();
                    tmpImage.ID = mats.MaterialName + "_" + mats.Textures[i].Map;
                    tmpImage.Init_From = new Grendgine_Collada_Init_From();
                    // Build the URI path to the file as a .dds, clean up the slashes.
                    StringBuilder builder;
                    if (mats.Textures[i].File.Contains(@"/") || mats.Textures[i].File.Contains(@"\"))
                    {
                        builder = new StringBuilder(cgfData.Args.ObjectDir + @"\" + mats.Textures[i].File);
                    }
                    else
                    {
                        builder = new StringBuilder(mats.Textures[i].File);
                    }
                    builder.Replace(".tif", ".dds");
                    builder.Replace(@"/", @"\");

                    //tmpImage.Init_From.Ref = mats.Textures[i].File;
                    tmpImage.Init_From.Ref = builder.ToString();
                    imageList.Add(tmpImage);
                    numImages++;                    // increment the number of image files found.
                }
            }
            // images is the array of image (Gredgine_Collada_Image) objects
            Grendgine_Collada_Image[] images = imageList.ToArray();
            daeObject.Library_Images.Image = images;
        }
        public void WriteLibrary_Materials()
        {
            // Create the list of materials used in this object
            // There is just one .mtl file we need to worry about.
            Grendgine_Collada_Library_Materials libraryMaterials = new Grendgine_Collada_Library_Materials();
            // We have our top level.
            daeObject.Library_Materials = libraryMaterials;
            int numMaterials = cgfData.MatFile.Materials.Count;
            // Now create a material for each material in the object
            Console.WriteLine("Number of materials: {0}", numMaterials);
            Grendgine_Collada_Material[] materials = new Grendgine_Collada_Material[numMaterials];
            for (int i=0; i < numMaterials; i++ )
            {
                Grendgine_Collada_Material tmpMaterial = new Grendgine_Collada_Material();
                tmpMaterial.Name = cgfData.MatFile.MaterialNameArray[i].MaterialName;
                // Create the instance_effect for each material
                tmpMaterial.Instance_Effect = new Grendgine_Collada_Instance_Effect();
                // The # in front of tmpMaterial.name is needed to reference the effect in Library_effects.
                tmpMaterial.Instance_Effect.URL = "#" + tmpMaterial.Name;
                // Need material ID here, so the meshes can reference it.  Use the chunk ID.
                tmpMaterial.ID = i.ToString();          // this is the order the materials appear in the .mtl file.  Needed for geometries.
                materials[i] = tmpMaterial;
            }
            libraryMaterials.Material = materials;
        }
        public void WriteLibrary_Effects()
        {
            // The Effects library.  This is actual material stuff, so... let's get into it!  First, let's make a library effects object
            Grendgine_Collada_Library_Effects libraryEffects = new Grendgine_Collada_Library_Effects();
            daeObject.Library_Effects = libraryEffects;
            // libraryEffects contains a number of effects objects.  One effects object for each material.

        }
        public void WriteLibrary_Geometries()
        {
            // Geometry library.  this is going to be fun...
            Grendgine_Collada_Library_Geometries libraryGeometries = new Grendgine_Collada_Library_Geometries();
            libraryGeometries.ID = cgfData.RootNode.Name;
            // Make a list for all the geometries objects we will need. Will convert to array at end.  Define the array here as well
            List<Grendgine_Collada_Geometry> geometryList = new List<Grendgine_Collada_Geometry>();

            // For each of the nodes, we need to write the geometry.
            // Need to figure out how to assign the right material to the node as well.
            // Use a foreach statement to get all the node chunks.  This will get us the meshes, which will contain the vertex, UV and normal info.
            foreach (CgfData.ChunkNode nodeChunk in cgfData.CgfChunks.Where(a => a.chunkType == ChunkType.Node))
            {
                // Create a geometry object.  Use the chunk ID for the geometry ID
                // Will have to be careful with this, since with .cga/.cgam pairs will need to match by Name.
                Grendgine_Collada_Geometry tmpGeo = new Grendgine_Collada_Geometry();
                tmpGeo.Name = nodeChunk.Name;
                tmpGeo.ID = nodeChunk.id.ToString();
                // Now make the mesh object.  This will have 3 sources, 1 vertices, and 1 triangles (with material ID)
                // If the Object ID of Node chunk points to a Helper or a Controller though, place an empty.
                // Will have to figure out transforms here too.
                Grendgine_Collada_Mesh tmpMesh = new Grendgine_Collada_Mesh();
                // need to make a list of the sources and triangles to add to tmpGeo.Mesh
                List<Grendgine_Collada_Source> sourceList = new List<Grendgine_Collada_Source>();
                List<Grendgine_Collada_Triangles> triList = new List<Grendgine_Collada_Triangles>();
                CgfData.ChunkDataStream tmpNormals = new CgfData.ChunkDataStream();
                CgfData.ChunkDataStream tmpUVs = new CgfData.ChunkDataStream();
                CgfData.ChunkDataStream tmpVertices = new CgfData.ChunkDataStream();
                CgfData.ChunkDataStream tmpVertsUVs = new CgfData.ChunkDataStream();

                if (cgfData.ChunkDictionary[nodeChunk.Object].chunkType == ChunkType.Mesh)
                {
                    // Get the mesh chunk and submesh chunk for this node.
                    CgfData.ChunkMesh tmpMeshChunk = (CgfData.ChunkMesh)cgfData.ChunkDictionary[nodeChunk.Object];
                    CgfData.ChunkMeshSubsets tmpMeshSubsets = tmpMeshSubsets = (CgfData.ChunkMeshSubsets)cgfData.ChunkDictionary[tmpMeshChunk.MeshSubsets];  // Listed as Object ID for the Node

                    // Get pointers to the vertices data
                    if (tmpMeshChunk.VerticesData != 0)
                    {
                        tmpVertices = (CgfData.ChunkDataStream)cgfData.ChunkDictionary[tmpMeshChunk.VerticesData];
                    }
                    if (tmpMeshChunk.NormalsData != 0)
                    {
                        tmpNormals = (CgfData.ChunkDataStream)cgfData.ChunkDictionary[tmpMeshChunk.NormalsData];
                    }
                    if (tmpMeshChunk.UVsData != 0)
                    {
                        tmpUVs = (CgfData.ChunkDataStream)cgfData.ChunkDictionary[tmpMeshChunk.UVsData];
                    }
                    if (tmpMeshChunk.VertsUVsData != 0)
                    {
                        tmpVertsUVs = (CgfData.ChunkDataStream)cgfData.ChunkDictionary[tmpMeshChunk.VertsUVsData];
                    }

                    // need a collada_source for position, normal, UV and color, what the source is (verts), and the tri index
                    Grendgine_Collada_Source posSource = new Grendgine_Collada_Source();
                    Grendgine_Collada_Source normSource = new Grendgine_Collada_Source();
                    Grendgine_Collada_Source uvSource = new Grendgine_Collada_Source();
                    Grendgine_Collada_Vertices verts = new Grendgine_Collada_Vertices();
                    Grendgine_Collada_Triangles tris = new Grendgine_Collada_Triangles();

                    posSource.ID = nodeChunk.Name + "_pos";  // I want to use the chunk ID, but that may be daunting.
                    posSource.Name = nodeChunk.Name + "_pos";
                    normSource.ID = nodeChunk.Name + "_norm";
                    normSource.Name = nodeChunk.Name + "_norm";
                    uvSource.Name = nodeChunk.Name + "_UV";
                    uvSource.ID = nodeChunk.Name + "_UV";

                    // Create a float_array object to store all the data
                    Grendgine_Collada_Float_Array floatArray = new Grendgine_Collada_Float_Array();
                    floatArray.ID = posSource.Name + "_array";
                    floatArray.Count = (int)tmpVertices.NumElements;
                    // Build the string of vertices with a stringbuilder
                    StringBuilder vertString = new StringBuilder();
                    for (int i=0; i<floatArray.Count; i++)
                    {
                        // this is an array of Vector3s?
                    }
                    //floatArray = 


                    normSource.ID = nodeChunk.Name + "_pos";
                    normSource.Name = nodeChunk.Name + "_pos";

                    uvSource.ID = nodeChunk.Name + "_UV";
                    uvSource.Name = nodeChunk.Name + "_UV";

                    // make a vertices eliment.  Only one, so no list needed.

                    // tris are easy.  Just the index of faces


                }
                else if (cgfData.ChunkDictionary[nodeChunk.Object].chunkType == ChunkType.Helper)
                {
                    
                } else if (cgfData.ChunkDictionary[nodeChunk.Object].chunkType == ChunkType.Controller)
                {

                }
                tmpGeo.Mesh = tmpMesh;

                // Add the tmpGeo geometry to the list

                geometryList.Add(tmpGeo);
                
                
            }
            libraryGeometries.Geometry = geometryList.ToArray();
            daeObject.Library_Geometries = libraryGeometries;

        }
        public void WriteLibrary_Controllers()
        {

        }
        public void WriteLibrary_VisualScenes()
        {

        }
        public void WriteScene()
        {

        }

    }
}
