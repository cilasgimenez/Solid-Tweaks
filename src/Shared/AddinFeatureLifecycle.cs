using System;

namespace SolidTweaks.Shared
{
    // The state machine has no registry/COM side effects of its own, so every
    // startup/update/dormant transition can also be exercised without SOLIDWORKS.
    internal interface IAddinFeatureEnvironment
    {
        bool Read(string feature);
        void Write(string feature, bool enabled);
        bool AnyEnabled { get; }
        bool IsCurrent { get; }
        bool IsRegistered { get; }
        bool IsTargetRunning { get; }
        bool IsConnected { get; }
        void Install();
        void SetStartup(bool enabled);
        void Synchronize();
        void RemoveInterface();
        bool IsFeaturePresent(string feature);
    }

    internal sealed class AddinFeatureLifecycle
    {
        private readonly IAddinFeatureEnvironment environment;
        public AddinFeatureLifecycle(IAddinFeatureEnvironment environment) { this.environment = environment; }

        public void Change(string feature, bool enabled)
        {
            bool previous = environment.Read(feature);
            // Refuse replacement BEFORE persisting a new feature choice.
            EnsureCanInstall();
            environment.Write(feature, enabled);
            try
            {
                ApplyConfiguredState();
                if (environment.IsConnected && environment.IsFeaturePresent(feature) != enabled)
                    throw new InvalidOperationException(UiText.Get("The requested change could not be confirmed."));
            }
            catch
            {
                environment.Write(feature, previous);
                try { ApplyConfiguredState(); } catch { }
                throw;
            }
        }

        public void Update()
        {
            if (environment.IsTargetRunning)
                throw new InvalidOperationException(UiText.Get("Close the selected SOLIDWORKS version before updating the add-in."));
            environment.Install();
            environment.SetStartup(environment.AnyEnabled);
        }

        private void EnsureCanInstall()
        {
            if (!environment.IsCurrent && environment.IsRegistered && environment.IsTargetRunning)
                throw new InvalidOperationException(UiText.Get("Close the selected SOLIDWORKS version before updating the add-in."));
        }

        private void ApplyConfiguredState()
        {
            if (environment.AnyEnabled)
            {
                if (!environment.IsCurrent) { EnsureCanInstall(); environment.Install(); }
                environment.SetStartup(true);
                if (environment.IsConnected) environment.Synchronize();
            }
            else
            {
                // Retain the COM registration and loaded object; never unload/reload.
                if (environment.IsConnected) environment.RemoveInterface();
                environment.SetStartup(false);
            }
        }
    }
}
