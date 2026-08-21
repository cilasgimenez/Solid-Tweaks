using SolidTweaks.Core.Features;
using SolidTweaks.Infrastructure.SolidWorks;
using SolidTweaks.Shared;

namespace SolidTweaks.Features
{
    internal sealed class RealViewTweak : ITweak
    {
        private readonly VisualSettingsService settings;

        public RealViewTweak(VisualSettingsService settings)
        {
            this.settings = settings;
        }

        public string Id { get { return "visual.real-view"; } }
        public string DisplayName { get { return "RealView"; } }
        public bool IsPersistent { get { return true; } }
        public bool Read() { return settings.IsRealViewEnabled(); }
        public void Apply() { settings.EnableRealView(); }
        public void Revert() { settings.DisableRealView(); }
    }

    internal sealed class EnhancedGraphicsPerformanceTweak : ITweak
    {
        private readonly VisualSettingsService settings;

        public EnhancedGraphicsPerformanceTweak(VisualSettingsService settings)
        {
            this.settings = settings;
        }

        public string Id { get { return "visual.enhanced-graphics-performance"; } }
        public string DisplayName { get { return UiText.Get("Enhanced Graphics Performance"); } }
        public bool IsPersistent { get { return true; } }
        public bool Read() { return settings.IsEnhancedGraphicsPerformanceEnabled(); }
        public void Apply() { settings.SetEnhancedGraphicsPerformance(true); }
        public void Revert() { settings.SetEnhancedGraphicsPerformance(false); }
    }

    internal sealed class GpuSilhouetteEdgesTweak : ITweak
    {
        private readonly VisualSettingsService settings;

        public GpuSilhouetteEdgesTweak(VisualSettingsService settings)
        {
            this.settings = settings;
        }

        public string Id { get { return "visual.gpu-silhouette-edges"; } }
        public string DisplayName { get { return UiText.Get("GPU silhouette edges"); } }
        public bool IsPersistent { get { return true; } }
        public bool Read() { return settings.AreGpuSilhouetteEdgesEnabled(); }
        public void Apply() { settings.SetGpuSilhouetteEdges(true); }
        public void Revert() { settings.SetGpuSilhouetteEdges(false); }
    }

    internal sealed class FullAntialiasingTweak : ITweak
    {
        private readonly VisualSettingsService settings;

        public FullAntialiasingTweak(VisualSettingsService settings)
        {
            this.settings = settings;
        }

        public string Id { get { return "visual.full-antialiasing"; } }
        public string DisplayName { get { return UiText.Get("Full antialiasing"); } }
        public bool IsPersistent { get { return true; } }
        public bool Read() { return settings.IsFullAntialiasingEnabled(); }
        public void Apply() { settings.SetFullAntialiasing(true); }
        public void Revert() { settings.SetFullAntialiasing(false); }
    }
}
