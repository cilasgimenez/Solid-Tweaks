namespace SolidTweaks.Core.Features
{
    internal interface ITweak
    {
        string Id { get; }
        string DisplayName { get; }
        bool IsPersistent { get; }
        bool Read();
        void Apply();
        void Revert();
    }
}
