using System;
using SolidTweaks.Core.Features;
using SolidTweaks.Infrastructure.Persistence;
using SolidTweaks.Infrastructure.SolidWorks;
using SolidTweaks.Shared;

namespace SolidTweaks.Features
{
    internal sealed class SpaceNormalToTweak : ITweak
    {
        private const string FeatureId = "keyboard.space-normal-to";
        private readonly SolidWorksKeyboardShortcutService shortcutService;
        private readonly FeatureStateStore stateStore;
        private readonly Action<string> reportProgress;

        public SpaceNormalToTweak(
            SolidWorksKeyboardShortcutService shortcutService,
            FeatureStateStore stateStore,
            Action<string> reportProgress)
        {
            this.shortcutService = shortcutService;
            this.stateStore = stateStore;
            this.reportProgress = reportProgress;
        }

        public string Id { get { return FeatureId; } }
        public string DisplayName { get { return UiText.Get("Space key (3D View -> Normal To)"); } }
        public bool IsPersistent { get { return true; } }

        public bool Read()
        {
            return stateStore.Read(FeatureId, false);
        }

        public void Apply()
        {
            shortcutService.AssignSpace(SolidWorksKeyboardCommand.NormalTo, reportProgress);
            stateStore.Write(FeatureId, true);
        }

        public void Revert()
        {
            shortcutService.AssignSpace(SolidWorksKeyboardCommand.ViewOrientation, reportProgress);
            stateStore.Write(FeatureId, false);
        }
    }
}
