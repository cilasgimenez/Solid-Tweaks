using SolidTweaks.Core.Features;
using SolidTweaks.Infrastructure.SolidWorks;
using SolidTweaks.Shared;

namespace SolidTweaks.Features
{
    internal sealed class ExportStepTweak : ITweak
    {
        private readonly SolidWorksAddinRegistrationService registration;

        public ExportStepTweak(SolidWorksAddinRegistrationService registration)
        {
            this.registration = registration;
        }

        public string Id { get { return "resources.export-step"; } }
        public string DisplayName { get { return UiText.Get("Export STEP"); } }
        public bool IsPersistent { get { return true; } }
        public bool Read() { return registration.IsFeatureEnabled(SolidTweaksFeatureSettings.ExportStepFeatureId); }
        public void Apply() { registration.SetFeatureEnabled(SolidTweaksFeatureSettings.ExportStepFeatureId, true); }
        public void Revert() { registration.SetFeatureEnabled(SolidTweaksFeatureSettings.ExportStepFeatureId, false); }
    }
}
