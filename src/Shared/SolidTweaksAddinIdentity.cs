namespace SolidTweaks.Shared
{
    internal static class SolidTweaksAddinIdentity
    {
        // Esta identidade e estavel entre versoes normais. Atualizacoes trocam a DLL e
        // o caminho versionado, nunca o GUID, para que o SOLIDWORKS nao retenha entradas
        // concorrentes. Uma nova identidade exige migracao explicita da anterior.
        public const string CurrentGuid = "{79D3CB00-7167-4FD6-9F1B-92D541DCA7CD}";
        public const string CurrentGuidWithoutBraces = "79D3CB00-7167-4FD6-9F1B-92D541DCA7CD";
        public const string CurrentProgId = "SolidTweaks.SolidWorksAddin.V114";

        public const string LegacyGuidV113 = "{6330361C-6CCC-4DA1-890D-D6685BC8DBF0}";
        public const string LegacyGuidV111 = "{F27105A6-4344-4081-B657-7E421FF0D6ED}";
        public const string LegacyGuidV110 = "{E8327E13-5F74-41DB-9234-47994B1DDC75}";
        public const string LegacyGuidV19 = "{0E9FA022-92A6-4C34-8915-7512514F3B05}";
        public const string LegacyGuidV18 = "{131EB904-E091-4C86-95B0-B41E4F9869F0}";
        public const string LegacyGuidV17 = "{C7D41943-0042-4140-93DE-7523EEDE7684}";
        public const string LegacyGuidV16 = "{D52DA99A-B23B-4342-83F6-D9779C252217}";
        public const string LegacyGuidV15 = "{ADB6989A-A69B-4A6B-B979-37527988BB15}";
        public const string LegacyGuidV14 = "{DF9D52E8-E929-42A4-A742-DB8F54B4F984}";
        public const string LegacyGuidV13 = "{0C6C9A95-EE42-40AE-928D-D31949D7251E}";
        public const string LegacyGuidV12 = "{7271ABC6-6994-4C09-A8E1-CE231967E48A}";
        public const string LegacyGuid = "{7D87C1B8-777D-4D69-BD53-6BD88C2F3E48}";

        public static readonly string[] LegacyGuids =
        {
            LegacyGuidV113,
            LegacyGuidV111,
            LegacyGuidV110,
            LegacyGuidV19,
            LegacyGuidV18,
            LegacyGuidV17,
            LegacyGuidV16,
            LegacyGuidV15,
            LegacyGuidV14,
            LegacyGuidV13,
            LegacyGuidV12,
            LegacyGuid
        };
    }
}
