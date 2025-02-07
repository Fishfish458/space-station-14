using System;
using System.Collections.Generic;
using Content.Shared.Whitelist;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Storage.Components
{
    [Serializable, NetSerializable]
    public enum StorageMapVisuals : sbyte
    {
        InitLayers,
        LayerChanged,
    }

    [Serializable]
    [DataDefinition]
    public sealed class SharedMapLayerData
    {
        public string Layer = string.Empty;

        [DataField("whitelist", required: true, serverOnly: true)]
        public EntityWhitelist ServerWhitelist { get; set; } = new();
    }

    [Serializable, NetSerializable]
    public sealed class ShowLayerData : ICloneable
    {
        public IReadOnlyList<string> QueuedEntities { get; internal set; }

        public ShowLayerData()
        {
            QueuedEntities = new List<string>();
        }

        public ShowLayerData(IEnumerable<string> other)
        {
            QueuedEntities = new List<string>(other);
        }

        public object Clone()
        {
            return new ShowLayerData(QueuedEntities);
        }
    }
}
