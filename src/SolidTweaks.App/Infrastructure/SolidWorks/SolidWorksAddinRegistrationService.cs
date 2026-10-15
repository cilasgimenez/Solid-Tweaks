using System;
using SolidTweaks.Shared;

namespace SolidTweaks.Infrastructure.SolidWorks
{
    // One lifecycle for both versions; the SW2025 binary itself stays unchanged.
    internal sealed class SolidWorksAddinRegistrationService
    {
        private readonly SolidWorksService host;
        public SolidWorksAddinRegistrationService(SolidWorksService host) { this.host = host; }
        private SolidWorksVersionedAddinRegistration Current
        {
            get { return new SolidWorksVersionedAddinRegistration(host, SolidWorksTarget.Year); }
        }
        public bool IsFeatureEnabled(string feature)
        {
            ValidateFeature(feature);
            return SolidWorksTarget.IsSupported && Current.IsFeatureEnabled(feature);
        }
        public bool IsUpdateAvailable() { return SolidWorksTarget.IsSupported && Current.IsUpdateAvailable; }
        public bool IsInstalledInterfaceReady() { return SolidWorksTarget.IsSupported && Current.IsInterfaceReady; }
        public void UpdateInstalledAddin() { Current.Update(); }
        public void SetFeatureEnabled(string feature, bool enabled)
        {
            ValidateFeature(feature);
            Current.Change(feature, enabled);
        }
        public void Uninstall()
        {
            var current = Current;
            if (current.IsConnected) current.RemoveInterface();
            current.SetStartup(false);
        }
        private static void ValidateFeature(string feature)
        {
            if (feature != SolidTweaksFeatureSettings.ExportStepFeatureId && feature != SolidTweaksFeatureSettings.ExportDxfFeatureId)
                throw new ArgumentException("Unknown add-in feature: " + feature);
        }
    }
}
