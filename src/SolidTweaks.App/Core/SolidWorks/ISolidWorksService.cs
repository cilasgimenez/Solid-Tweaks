namespace SolidTweaks.Core.SolidWorks
{
    internal enum SolidWorksDocumentKind
    {
        Part,
        Assembly,
        Drawing
    }

    internal interface ISolidWorksService
    {
        bool IsConnected { get; }
        bool HasActiveDocument { get; }
        string Version { get; }
        bool TryConnect();
        bool GetToggle(string preferenceName);
        void SetToggle(string preferenceName, bool enabled);
        bool RunActiveDocumentCommand(int commandId);
        object GetActiveDocument();
        object CreateDocument(string templatePath, SolidWorksDocumentKind kind);
    }
}
