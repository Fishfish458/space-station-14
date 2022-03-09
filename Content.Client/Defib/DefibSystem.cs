using Robust.Client.GameObjects;

namespace Content.Client.Defib;

public sealed class DefibSystem : VisualizerSystem<DefibVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, DefibVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (TryComp(uid, out SpriteComponent? sprite))
        {
            sprite.LayerSetVisible(DefibVisualLayers.Base, true);
            sprite.LayerSetVisible(DefibVisualLayers.Paddles, true);
            sprite.LayerSetVisible(DefibVisualLayers.Powered, true);
        }
    }
}

public enum DefibVisualLayers
{
    Base,
    Paddles,
    Powered,
    Charge
}
