using Elements.Geometry.Tessellation;
using Elements.Geometry;
//using glTFLoader.Schema;
using System;
using System.Collections.Generic;
using System.Text;
using Elements.Geometry.Interfaces;
using Elements.Serialization.glTF;
using System.IO;
using System.Linq;
using static glTFLoader.Schema.MeshPrimitive;

namespace Elements.Utilities
{
    /// <summary>
    /// The helpful methods for push elements to visualization.
    /// </summary>
    public static class VisualizationUtils
    {
        /// <summary>
        /// Translate the element geometry to the GraphicsBuffer structure, making it easier to push it to gltf or other visualization tools.
        /// </summary>
        public static List<GraphicsBuffers> ProcessGeometricRepresentation(
            GeometricElement geometricElement, 
            bool updateElementRepresentations,
            out ModeEnum? mode)
        {
            List<GraphicsBuffers> buffers = null;
            if (updateElementRepresentations)
            {
                geometricElement.UpdateRepresentations();
            }

            geometricElement.UpdateBoundsAndComputeSolid(true);

            //Se já conseguir obter os buffers internamente, seguir o bonde
            if (geometricElement.TryToGraphicsBuffers(out buffers, out string id, out mode))
            {
                return buffers;
            }
            //Se houver representação, puxar o buffer do sólido
            if (geometricElement.Representation != null && geometricElement._csg != null)
            {
                buffers = new List<GraphicsBuffers>() { ProcessSolidsAsCSG(geometricElement) };
                mode = ModeEnum.TRIANGLES;
            }
            //Se for um elemento tesselável (em geral um mesh), tesselar
            if(geometricElement is ITessellate tess)
            {
                var mesh = new Mesh();
                tess.Tessellate(ref mesh);
                buffers = new List<GraphicsBuffers>() { mesh.GetBuffers() };
                mode = ModeEnum.TRIANGLES;
            }
            return buffers;
        }

        private static GraphicsBuffers ProcessSolidsAsCSG(GeometricElement geometricElement)
        {
            GraphicsBuffers buffers = null;

            // If we've explicitly skipped csg union or the element
            // only has one solid operation, we can perform this micro-optimization
            // of skipping CSG creation.
            if (geometricElement.Representation.SkipCSGUnion)
            {
                // There's a special flag on Representation that allows you to
                // skip CSG unions. In this case, we tessellate all solids
                // individually, and do no booleaning. Voids are also ignored.

                // We create a collection of SolidTesselationTargetProviders, one for each solid operation.
                // Each SolidTesselationTargetProvider has a GetTessellationTargets method which returns a new SolidFaceTessAdapter.
                // Each SolidFaceTessAdapter is responsible for the tesselation of a single face.
                // The SolidFaceTessAdapter's GetTess method calls face.ToContourVertexArray.
                // ToContourVertexArray attaches a faceId and a solidIdto the Data object we hang on the contour vertex.
                // The faceId and solidId are used during packing to lookup existing shared vertices, to avoid recreating them.
                uint solidId = 0;
                var providers = new List<SolidTesselationTargetProvider>();
                foreach (var so in geometricElement.Representation.SolidOperations)
                {
                    providers.Add(new SolidTesselationTargetProvider(so.Solid, solidId, so.LocalTransform));
                    solidId++;
                }
                buffers = Tessellation.Tessellate<GraphicsBuffers>(providers, false, geometricElement.ModifyVertexAttributes);
            }
            else
            {
                buffers = geometricElement._csg.Tessellate(modifyVertexAttributes: geometricElement.ModifyVertexAttributes);
            }

            if (buffers.Vertices.Count == 0)
            {
                return null;
            }

            return buffers;
        }
    }
}
