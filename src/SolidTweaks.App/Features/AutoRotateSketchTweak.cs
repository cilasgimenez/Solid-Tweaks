using SolidTweaks.Core.Features;
using SolidTweaks.Core.SolidWorks;
using SolidTweaks.Shared;

namespace SolidTweaks.Features
{
    internal sealed class AutoRotateSketchTweak : ITweak
    {
        private const string PreferenceName = "swAutoNormalToSketchMode";
        private readonly ISolidWorksService solidWorks;

        public AutoRotateSketchTweak(ISolidWorksService solidWorks)
        {
            this.solidWorks = solidWorks;
        }

        public string Id { get { return "sketch.auto-rotate-normal"; } }
        public string DisplayName { get { return UiText.Get("Rotate when creating a sketch"); } }
        public bool IsPersistent { get { return true; } }

        public bool Read()
        {
            return solidWorks.GetToggle(PreferenceName);
        }

        public void Apply()
        {
            solidWorks.SetToggle(PreferenceName, true);
        }

        public void Revert()
        {
            solidWorks.SetToggle(PreferenceName, false);
        }
    }
}
