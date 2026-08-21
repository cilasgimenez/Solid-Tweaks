using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using Microsoft.Win32;
using SolidTweaks.Infrastructure.Diagnostics;
using SolidTweaks.Shared;

namespace SolidTweaks.Infrastructure.SolidWorks
{
    internal sealed class VisualSettingsService
    {
        private const string VersionRoot = @"Software\SolidWorks\SOLIDWORKS 2025";
        private const string AllowListRoot = @"Software\SolidWorks\AllowList";
        private const string RealViewBackupRoot = @"Software\SolidTweaks\Visual\RealViewBackup";
        private const string WorkaroundsValueName = "Workarounds";

        private static readonly IDictionary<GpuBrand, BrandSettings> BrandSettingsByBrand =
            new Dictionary<GpuBrand, BrandSettings>
            {
                {
                    GpuBrand.Nvidia,
                    new BrandSettings(
                        "NVIDIA Corporation", "NV40", 0x00032408U,
                        0x04000000U, "2501001")
                },
                {
                    GpuBrand.Amd,
                    new BrandSettings(
                        "Advanced Micro Devices", "R420", 0x00032408U,
                        0x04000085U, "52400c84")
                },
                {
                    GpuBrand.Intel,
                    new BrandSettings(
                        "Intel", "Other", 0x00030008U,
                        0x04080080U, "10")
                }
            };

        public bool IsRealViewEnabled()
        {
            RealViewTarget target = ReadStoredTarget() ?? ResolveRealViewTarget();
            return ReadDword(target.GlPath, WorkaroundsValueName) == target.GlWorkarounds &&
                ReadDword(target.BrandPath, WorkaroundsValueName) == target.BrandWorkarounds;
        }

        public void EnableRealView()
        {
            RealViewTarget target = ResolveRealViewTarget();
            RealViewTarget stored = ReadStoredTarget();
            if (stored != null && !stored.SamePaths(target))
            {
                RestoreRealViewBackup(stored);
                ClearRealViewBackup();
                stored = null;
            }
            else if (stored != null)
            {
                // Reaplica exatamente o alvo originalmente salvo. Assim, uma
                // alteração externa parcial não troca o valor esperado no meio
                // do ciclo de ativação/reversão.
                target = stored;
            }

            try
            {
                if (stored == null)
                {
                    CaptureRealViewBackup(target);
                }
                WriteDword(target.GlPath, WorkaroundsValueName, target.GlWorkarounds);
                WriteDword(target.BrandPath, WorkaroundsValueName, target.BrandWorkarounds);
                ApplicationLog.Info(
                    "RealView registry entries enabled. Renderer=" + target.Renderer +
                    "; brand=" + target.Brand +
                    "; Gl2Shaders=" + target.GlPath +
                    "; performance=" + target.BrandPath + ".");
            }
            catch
            {
                RealViewTarget backupTarget = ReadStoredTarget();
                if (backupTarget != null)
                {
                    RestoreRealViewBackup(backupTarget);
                    ClearRealViewBackup();
                }
                throw;
            }
        }

        public void DisableRealView()
        {
            RealViewTarget stored = ReadStoredTarget();
            if (stored != null)
            {
                RestoreRealViewBackup(stored);
                ClearRealViewBackup();
                ApplicationLog.Info("RealView registry entries restored to their previous values.");
                return;
            }

            RealViewTarget target = ResolveRealViewTarget();
            DeleteValueAndEmptyLeaf(target.GlPath, WorkaroundsValueName);
            DeleteValueAndEmptyLeaf(target.BrandPath, WorkaroundsValueName);
            ApplicationLog.Info("RealView registry entries removed for renderer=" + target.Renderer + ".");
        }

        public bool IsEnhancedGraphicsPerformanceEnabled()
        {
            return ReadDword(VersionRoot + @"\Performance", "Use Performance Pipeline 2020") == 1U;
        }

        public void SetEnhancedGraphicsPerformance(bool enabled)
        {
            WriteDword(
                VersionRoot + @"\Performance",
                "Use Performance Pipeline 2020",
                enabled ? 1U : 0U);
        }

        public bool AreGpuSilhouetteEdgesEnabled()
        {
            return ReadDword(VersionRoot + @"\Performance", "Use GPU Silhouette Edges") == 1U;
        }

        public void SetGpuSilhouetteEdges(bool enabled)
        {
            WriteDword(
                VersionRoot + @"\Performance",
                "Use GPU Silhouette Edges",
                enabled ? 1U : 0U);
        }

        public bool IsFullAntialiasingEnabled()
        {
            string path = VersionRoot + @"\Edges";
            return ReadDword(path, "Show Fullscene Anti Alias") == 1U &&
                ReadDword(path, "Show Anti Alias Shaded Edges") == 0U;
        }

        public void SetFullAntialiasing(bool enabled)
        {
            string path = VersionRoot + @"\Edges";
            WriteDword(path, "Show Anti Alias Shaded Edges", enabled ? 0U : 1U);
            WriteDword(path, "Show Fullscene Anti Alias", enabled ? 1U : 0U);
        }

        private static RealViewTarget ResolveRealViewTarget()
        {
            RendererInfo renderer = ReadRendererFromRegistry(AllowListRoot + @"\Current") ??
                ReadRendererFromRegistry(VersionRoot + @"\Performance\Graphics\Hardware\Current") ??
                ReadRendererFromWindows();

            if (renderer == null || string.IsNullOrWhiteSpace(renderer.Renderer))
            {
                throw new UserFacingException(
                    "No graphics renderer was found in the SOLIDWORKS registry or Windows display adapters.",
                    UiText.Get("The graphics renderer could not be identified."));
            }
            if (renderer.Renderer.IndexOf('\\') >= 0)
            {
                throw new UserFacingException(
                    "The graphics renderer contains an invalid registry path separator: " + renderer.Renderer,
                    UiText.Get("The graphics renderer name is not valid for enabling RealView."));
            }

            GpuBrand brand = DetectBrand(renderer.Vendor, renderer.Renderer);
            BrandSettings settings;
            if (!BrandSettingsByBrand.TryGetValue(brand, out settings))
            {
                throw new UserFacingException(
                    "Unsupported GPU brand. Renderer=" + renderer.Renderer + "; vendor=" + renderer.Vendor + ".",
                    UiText.Get("RealView supports NVIDIA, AMD, and Intel graphics cards."));
            }

            uint brandWorkarounds = settings.DefaultBrandWorkarounds;
            uint currentWorkarounds;
            if (TryParseHex(renderer.Workarounds, out currentWorkarounds) &&
                !string.Equals(
                    NormalizeHex(renderer.Workarounds),
                    settings.BrandWorkaroundsToReplace,
                    StringComparison.OrdinalIgnoreCase))
            {
                brandWorkarounds = currentWorkarounds;
            }

            return new RealViewTarget(
                renderer.Renderer,
                brand,
                AllowListRoot + @"\Gl2Shaders\" + settings.GlFolder + @"\" + renderer.Renderer,
                AllowListRoot + @"\" + settings.BrandFolder + @"\" + renderer.Renderer,
                settings.GlWorkarounds,
                brandWorkarounds);
        }

        private static RendererInfo ReadRendererFromRegistry(string path)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path, false))
            {
                if (key == null)
                {
                    return null;
                }
                string renderer = Convert.ToString(
                    key.GetValue("renderer", null, RegistryValueOptions.DoNotExpandEnvironmentNames),
                    CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(renderer))
                {
                    return null;
                }
                return new RendererInfo
                {
                    Renderer = renderer.Trim(),
                    Vendor = Convert.ToString(
                        key.GetValue("vendor", null, RegistryValueOptions.DoNotExpandEnvironmentNames),
                        CultureInfo.InvariantCulture),
                    Workarounds = RegistryValueAsHex(key.GetValue(WorkaroundsValueName, null))
                };
            }
        }

        private static RendererInfo ReadRendererFromWindows()
        {
            var candidates = new List<RendererCandidate>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name, AdapterRAM FROM Win32_VideoController"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    int order = 0;
                    foreach (ManagementObject item in results)
                    {
                        string name = Convert.ToString(item["Name"], CultureInfo.InvariantCulture);
                        if (string.IsNullOrWhiteSpace(name) ||
                            candidates.Exists(candidate => string.Equals(
                                candidate.Name, name, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }
                        ulong memory = 0;
                        try { memory = Convert.ToUInt64(item["AdapterRAM"], CultureInfo.InvariantCulture); }
                        catch (Exception) { }
                        candidates.Add(new RendererCandidate(name.Trim(), ScoreRenderer(name, memory), order++));
                    }
                }
            }
            catch (ManagementException exception)
            {
                ApplicationLog.Warning("Could not enumerate Windows display adapters: " + exception.Message);
                return null;
            }

            candidates.Sort(delegate(RendererCandidate left, RendererCandidate right)
            {
                int score = right.Score.CompareTo(left.Score);
                return score != 0 ? score : left.Order.CompareTo(right.Order);
            });
            return candidates.Count == 0
                ? null
                : new RendererInfo { Renderer = candidates[0].Name, Vendor = string.Empty, Workarounds = string.Empty };
        }

        private static int ScoreRenderer(string name, ulong memory)
        {
            string value = (name ?? string.Empty).ToUpperInvariant();
            if (value.Contains("MICROSOFT") || value.Contains("REMOTE") || value.Contains("VIRTUAL"))
            {
                return -100;
            }
            if (value.Contains("NVIDIA") || value.Contains("GEFORCE") || value.Contains("QUADRO"))
            {
                return 300;
            }
            if (value.Contains("RADEON RX") || value.Contains("RADEON PRO") || value.Contains("FIREPRO"))
            {
                return 260;
            }
            if (value.Contains("INTEL") && value.Contains("ARC") && memory >= 2147483648UL)
            {
                return 220;
            }
            return memory >= 2147483648UL ? 150 : 50;
        }

        private static GpuBrand DetectBrand(string vendor, string renderer)
        {
            GpuBrand brand = DetectBrandInText(vendor);
            return brand == GpuBrand.Unknown ? DetectBrandInText(renderer) : brand;
        }

        private static GpuBrand DetectBrandInText(string text)
        {
            string value = (text ?? string.Empty).ToUpperInvariant();
            if (value.Contains("NVIDIA") || value.Contains("GEFORCE") || value.Contains("QUADRO") ||
                value.Contains("TITAN") || value.Contains("TESLA") || value.Contains("RTX") ||
                value.Contains("GTX") || value.Contains("GTS") || value.Contains("PALIT") ||
                value.Contains("PNY"))
            {
                return GpuBrand.Nvidia;
            }
            if (value.Contains("AMD") || value.Contains("ATI") || value.Contains("ADVANCED MICRO DEVICES") ||
                value.Contains("RADEON") || value.Contains("FIREPRO") || value.Contains("SAPPHIRE") ||
                value.Contains("RYZEN") || value.Contains("VEGA") || value.Contains(" RX "))
            {
                return GpuBrand.Amd;
            }
            if (value.Contains("INTEL") || value.Contains("IRIS") || value.Contains("UHD") ||
                value.Contains("INTEL ARC") || value.Contains("XE GRAPHICS"))
            {
                return GpuBrand.Intel;
            }
            return GpuBrand.Unknown;
        }

        private static uint? ReadDword(string path, string valueName)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path, false))
            {
                if (key == null)
                {
                    return null;
                }
                object value = key.GetValue(valueName, null);
                if (value is int)
                {
                    return unchecked((uint)(int)value);
                }
                if (value is long)
                {
                    return unchecked((uint)(long)value);
                }
                uint parsed;
                return TryParseHex(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed)
                    ? (uint?)parsed
                    : null;
            }
        }

        private static void WriteDword(string path, string valueName, uint value)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(path))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Could not open the SOLIDWORKS registry key: " + path);
                }
                key.SetValue(valueName, unchecked((int)value), RegistryValueKind.DWord);
            }
        }

        private static void CaptureRealViewBackup(RealViewTarget target)
        {
            using (RegistryKey backup = Registry.CurrentUser.CreateSubKey(RealViewBackupRoot))
            {
                if (backup == null)
                {
                    throw new InvalidOperationException("Could not create the RealView backup registry key.");
                }
                backup.SetValue("Active", 1, RegistryValueKind.DWord);
                backup.SetValue("Renderer", target.Renderer, RegistryValueKind.String);
                backup.SetValue("Brand", (int)target.Brand, RegistryValueKind.DWord);
                backup.SetValue("GlPath", target.GlPath, RegistryValueKind.String);
                backup.SetValue("BrandPath", target.BrandPath, RegistryValueKind.String);
                backup.SetValue("AppliedGl", unchecked((int)target.GlWorkarounds), RegistryValueKind.DWord);
                backup.SetValue("AppliedBrand", unchecked((int)target.BrandWorkarounds), RegistryValueKind.DWord);
                CaptureValue(backup, "Gl", target.GlPath);
                CaptureValue(backup, "Brand", target.BrandPath);
            }
        }

        private static void CaptureValue(RegistryKey backup, string prefix, string path)
        {
            using (RegistryKey source = Registry.CurrentUser.OpenSubKey(path, false))
            {
                object original = source == null
                    ? null
                    : source.GetValue(
                        WorkaroundsValueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);
                backup.SetValue(prefix + "HadValue", original == null ? 0 : 1, RegistryValueKind.DWord);
                if (original != null)
                {
                    RegistryValueKind kind = source.GetValueKind(WorkaroundsValueName);
                    backup.SetValue(prefix + "OriginalKind", (int)kind, RegistryValueKind.DWord);
                    backup.SetValue(prefix + "OriginalData", original, kind);
                }
            }
        }

        private static RealViewTarget ReadStoredTarget()
        {
            using (RegistryKey backup = Registry.CurrentUser.OpenSubKey(RealViewBackupRoot, false))
            {
                if (backup == null || Convert.ToInt32(backup.GetValue("Active", 0), CultureInfo.InvariantCulture) != 1)
                {
                    return null;
                }
                string renderer = Convert.ToString(backup.GetValue("Renderer", string.Empty), CultureInfo.InvariantCulture);
                string glPath = Convert.ToString(backup.GetValue("GlPath", string.Empty), CultureInfo.InvariantCulture);
                string brandPath = Convert.ToString(backup.GetValue("BrandPath", string.Empty), CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(glPath) || string.IsNullOrWhiteSpace(brandPath))
                {
                    return null;
                }
                return new RealViewTarget(
                    renderer,
                    (GpuBrand)Convert.ToInt32(backup.GetValue("Brand", 0), CultureInfo.InvariantCulture),
                    glPath,
                    brandPath,
                    unchecked((uint)Convert.ToInt32(backup.GetValue("AppliedGl", 0), CultureInfo.InvariantCulture)),
                    unchecked((uint)Convert.ToInt32(backup.GetValue("AppliedBrand", 0), CultureInfo.InvariantCulture)));
            }
        }

        private static void RestoreRealViewBackup(RealViewTarget target)
        {
            using (RegistryKey backup = Registry.CurrentUser.OpenSubKey(RealViewBackupRoot, false))
            {
                if (backup == null)
                {
                    return;
                }
                RestoreValue(backup, "Gl", target.GlPath);
                RestoreValue(backup, "Brand", target.BrandPath);
            }
        }

        private static void RestoreValue(RegistryKey backup, string prefix, string path)
        {
            bool hadValue = Convert.ToInt32(
                backup.GetValue(prefix + "HadValue", 0), CultureInfo.InvariantCulture) == 1;
            if (!hadValue)
            {
                DeleteValueAndEmptyLeaf(path, WorkaroundsValueName);
                return;
            }

            RegistryValueKind kind = (RegistryValueKind)Convert.ToInt32(
                backup.GetValue(prefix + "OriginalKind", (int)RegistryValueKind.DWord),
                CultureInfo.InvariantCulture);
            object data = backup.GetValue(
                prefix + "OriginalData",
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);
            using (RegistryKey destination = Registry.CurrentUser.CreateSubKey(path))
            {
                if (destination == null || data == null)
                {
                    throw new InvalidOperationException("Could not restore the previous RealView value at " + path + ".");
                }
                destination.SetValue(WorkaroundsValueName, data, kind);
            }
        }

        private static void DeleteValueAndEmptyLeaf(string path, string valueName)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path, true))
            {
                if (key == null)
                {
                    return;
                }
                key.DeleteValue(valueName, false);
                if (key.ValueCount != 0 || key.SubKeyCount != 0)
                {
                    return;
                }
            }

            int separator = path.LastIndexOf('\\');
            if (separator <= 0 || separator == path.Length - 1)
            {
                return;
            }
            string parentPath = path.Substring(0, separator);
            string leaf = path.Substring(separator + 1);
            using (RegistryKey parent = Registry.CurrentUser.OpenSubKey(parentPath, true))
            {
                if (parent != null)
                {
                    parent.DeleteSubKey(leaf, false);
                }
            }
        }

        private static void ClearRealViewBackup()
        {
            Registry.CurrentUser.DeleteSubKeyTree(RealViewBackupRoot, false);
        }

        private static string RegistryValueAsHex(object value)
        {
            if (value is int)
            {
                return unchecked((uint)(int)value).ToString("x", CultureInfo.InvariantCulture);
            }
            if (value is long)
            {
                return unchecked((ulong)(long)value).ToString("x", CultureInfo.InvariantCulture);
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string NormalizeHex(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(2);
            }
            normalized = normalized.TrimStart('0');
            return normalized.Length == 0 && !string.IsNullOrWhiteSpace(value) ? "0" : normalized;
        }

        private static bool TryParseHex(string value, out uint result)
        {
            string normalized = NormalizeHex(value);
            result = 0;
            return !string.IsNullOrEmpty(normalized) && uint.TryParse(
                normalized,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out result);
        }

        private enum GpuBrand
        {
            Unknown,
            Nvidia,
            Amd,
            Intel
        }

        private sealed class BrandSettings
        {
            public BrandSettings(
                string brandFolder,
                string glFolder,
                uint glWorkarounds,
                uint defaultBrandWorkarounds,
                string brandWorkaroundsToReplace)
            {
                BrandFolder = brandFolder;
                GlFolder = glFolder;
                GlWorkarounds = glWorkarounds;
                DefaultBrandWorkarounds = defaultBrandWorkarounds;
                BrandWorkaroundsToReplace = brandWorkaroundsToReplace;
            }

            public string BrandFolder { get; private set; }
            public string GlFolder { get; private set; }
            public uint GlWorkarounds { get; private set; }
            public uint DefaultBrandWorkarounds { get; private set; }
            public string BrandWorkaroundsToReplace { get; private set; }
        }

        private sealed class RendererInfo
        {
            public string Renderer { get; set; }
            public string Vendor { get; set; }
            public string Workarounds { get; set; }
        }

        private sealed class RendererCandidate
        {
            public RendererCandidate(string name, int score, int order)
            {
                Name = name;
                Score = score;
                Order = order;
            }

            public string Name { get; private set; }
            public int Score { get; private set; }
            public int Order { get; private set; }
        }

        private sealed class RealViewTarget
        {
            public RealViewTarget(
                string renderer,
                GpuBrand brand,
                string glPath,
                string brandPath,
                uint glWorkarounds,
                uint brandWorkarounds)
            {
                Renderer = renderer;
                Brand = brand;
                GlPath = glPath;
                BrandPath = brandPath;
                GlWorkarounds = glWorkarounds;
                BrandWorkarounds = brandWorkarounds;
            }

            public string Renderer { get; private set; }
            public GpuBrand Brand { get; private set; }
            public string GlPath { get; private set; }
            public string BrandPath { get; private set; }
            public uint GlWorkarounds { get; private set; }
            public uint BrandWorkarounds { get; private set; }

            public bool SamePaths(RealViewTarget other)
            {
                return other != null &&
                    string.Equals(GlPath, other.GlPath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(BrandPath, other.BrandPath, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
