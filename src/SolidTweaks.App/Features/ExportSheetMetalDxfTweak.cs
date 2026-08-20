using SolidTweaks.Core.Features;
using SolidTweaks.Infrastructure.SolidWorks;
using SolidTweaks.Shared;

namespace SolidTweaks.Features
{
    internal sealed class ExportSheetMetalDxfTweak : ITweak
    {
        private readonly SolidWorksAddinRegistrationService registration;

        public ExportSheetMetalDxfTweak(SolidWorksAddinRegistrationService registration)
        {
            this.registration = registration;
        }

        public string Id { get { return SolidTweaksFeatureSettings.ExportDxfFeatureId; } }
        public string DisplayName { get { return UiText.Get("Export Sheet Metal DXF"); } }
        public bool IsPersistent { get { return true; } }
        public bool Read() { return registration.IsFeatureEnabled(SolidTweaksFeatureSettings.ExportDxfFeatureId); }
        public void Apply() { registration.SetFeatureEnabled(SolidTweaksFeatureSettings.ExportDxfFeatureId, true); }
        public void Revert() { registration.SetFeatureEnabled(SolidTweaksFeatureSettings.ExportDxfFeatureId, false); }
    }
}
