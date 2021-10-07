
using System;
using System.Collections.Generic;
using System.Linq;
using Elements;
using Elements.Geometry;
using Elements.Serialization.DXF.Extensions;
using IxMilia.Dxf;
using IxMilia.Dxf.Blocks;
using IxMilia.Dxf.Entities;

namespace Elements.Serialization.DXF
{
    /// <summary>
    /// The class defining a drawing range used during rendering
    /// </summary>
    public class DrawingRange
    {
        /// <summary>
        /// The transform representing the origin, and direction of the camera
        /// for this render.
        /// The origin is the camera location, and defines the cut plane.
        /// The camera direction is the -Z axis of this transform.
        /// The X-axis is considered to point to the right of the dxf "paperspace".
        /// </summary>
        public Transform Transform;
        /// <summary>
        /// How far in the -Z direction does the DrawingRange extend.
        /// </summary>
        public double Depth;
    }

    /// <summary>
    /// Provides information about the model and the drawing configuration. 
    /// </summary>
    public class DxfRenderContext
    {
        /// <summary>
        /// The entire model that is being rendered.
        /// </summary>
        public Model Model;

        /// <summary>
        /// The drawing range that is currently being modeled.
        /// </summary>
        public DrawingRange DrawingRange;

        /// <summary>
        /// Configuration information containing layer mappings for element types.
        /// </summary>
        public MappingConfiguration MappingConfiguration { get; set; }

    }

    /// <summary>
    /// A class that provides a mechanism for converting elements into DXF
    /// should inherit this interface.
    /// </summary>
    public interface IRenderDxf
    {
        /// <summary>
        /// Add a DXF entity to a document, along with any layers necesary.
        /// </summary>
        void TryAddDxfEntity(DxfFile document, Element element, DxfRenderContext context);
    }

    /// <summary>
    /// A generic implementation of the IRenderDxf interface.
    /// </summary>
    public abstract class DxfConverter<T> : IRenderDxf where T : Element
    {
        /// <summary>
        /// Add a DXF entity to the document for an element.
        /// </summary>
        public abstract void TryAddDxfEntity(DxfFile document, T element, DxfRenderContext context);

        /// <summary>
        /// Add a DXF entity to the document for an element.
        /// </summary>
        public void TryAddDxfEntity(DxfFile document, Element element, DxfRenderContext context)
        {
            this.TryAddDxfEntity(document, element as T, context);
        }

        /// <summary>
        /// Add the entities associated with a given element to the appropriate
        /// layer, adding a new layer if necessary.
        /// </summary>
        public void AddElementToLayer(DxfFile document, Element e, IEnumerable<DxfEntity> entities, DxfRenderContext context)
        {
            try
            {
                var config = context.MappingConfiguration;
                if (config == null)
                {
                    // TODO: add a default configuration
                    return;
                }
                var layerConfigForElement = FindLayerForElement(config, e);
                if (layerConfigForElement == null)
                {
                    return;
                }
                var matchingLayer = document.Layers.FirstOrDefault((l) => l.Name == layerConfigForElement.LayerName);
                if (matchingLayer == null)
                {
                    var layer = new DxfLayer(layerConfigForElement.LayerName, layerConfigForElement.LayerColor.ToDxfColor());
                    if (layerConfigForElement.Lineweight != 0)
                    {
                        // lineweight value is in 1/100ths of a millimeter
                        layer.LineWeight = new DxfLineWeight
                        {
                            Value = (short)layerConfigForElement.Lineweight
                        };
                    }
                    document.Layers.Add(layer);
                    matchingLayer = layer;
                }
                foreach (var entity in entities)
                {
                    entity.Layer = matchingLayer.Name;
                    if (layerConfigForElement.ElementColorSetting == MappingConfiguration.ElementColorSetting.TryGetColorFromMaterial)
                    {
                        if (e is GeometricElement ge)
                        {
                            var materialColor = ge.Material.Color;
                            var entityColor = materialColor.ToDxfColor();
                            entity.Color = entityColor;
                            entity.Color24Bit = materialColor.To24BitColor();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // TODO: Implement exception logging
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static MappingConfiguration.Layer FindLayerForElement(MappingConfiguration config, Element e)
        {
            var discriminator = e.GetType().FullName;

            // if we receive an element that isn't of a known type, it will be
            // deserialized as a GeometricElement, so we have to dig into its
            // AdditionalProperties to get its type.
            if (e.AdditionalProperties.ContainsKey("discriminator"))
            {
                discriminator = e.AdditionalProperties["discriminator"] as string;
            }
            return config.Layers.FirstOrDefault(l => l.Types.Contains(discriminator));
        }
    }

    /// <summary>
    /// A generic implementation of the IRenderDxf interface for any GeometricElement.
    /// </summary>
    public abstract class GeometricDxfConverter<T> : DxfConverter<T> where T : GeometricElement
    {
        /// <summary>
        /// Add a DXF entity to the document for an element.
        /// </summary>
        public override void TryAddDxfEntity(DxfFile document, T element, DxfRenderContext context)
        {
            // TODO: handle context / drawing range / etc.
            if (element.Representation == null)
            {
                return;
            }
            var entities = element.GetEntitiesFromRepresentation();

            if (element.IsElementDefinition)
            {
                var block = new DxfBlock
                {
                    Name = element.GetBlockName(),
                    BasePoint = new DxfPoint(0, 0, 0)
                };
                foreach (var e in entities)
                {
                    block.Entities.Add(e);
                }
                document.Blocks.Add(block);
                document.BlockRecords.Add(new DxfBlockRecord(block.Name));
                return;
            }
            foreach (var e in entities)
            {
                document.Entities.Add(e);
            }
            AddElementToLayer(document, element, entities, context);
        }
    }
}