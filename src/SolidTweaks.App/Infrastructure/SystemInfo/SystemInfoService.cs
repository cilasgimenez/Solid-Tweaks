using System;
using System.Collections.Generic;
using System.Management;
using Microsoft.Win32;
using SolidTweaks.Shared;

namespace SolidTweaks.Infrastructure.SystemInfo
{
    internal sealed class SystemInformationSnapshot
    {
        public string GraphicsCard { get; set; }
        public string DotNetFramework { get; set; }
    }

    internal sealed class SystemInfoService
    {
        public SystemInformationSnapshot Read()
        {
            return new SystemInformationSnapshot
            {
                GraphicsCard = ReadGraphicsCards(),
                DotNetFramework = ReadDotNetFramework()
            };
        }

        private static string ReadGraphicsCards()
        {
            var adapters = new List<GraphicsAdapter>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject item in results)
                    {
                        string name = Convert.ToString(item["Name"]);
                        if (!string.IsNullOrWhiteSpace(name) &&
                            !adapters.Exists(adapter => string.Equals(adapter.Name, name, StringComparison.OrdinalIgnoreCase)))
                        {
                            ulong memory = 0;
                            try
                            {
                                memory = Convert.ToUInt64(item["AdapterRAM"]);
                            }
                            catch (Exception) { }
                            adapters.Add(new GraphicsAdapter(name, memory, adapters.Count));
                        }
                    }
                }
            }
            catch (ManagementException)
            {
                return UiText.Get("Not identified");
            }

            adapters.Sort(delegate(GraphicsAdapter left, GraphicsAdapter right)
            {
                int scoreComparison = right.DedicatedScore.CompareTo(left.DedicatedScore);
                return scoreComparison != 0 ? scoreComparison : left.OriginalIndex.CompareTo(right.OriginalIndex);
            });

            var names = adapters.ConvertAll(adapter => adapter.Name);
            return names.Count == 0 ? UiText.Get("Not identified") : string.Join(" • ", names.ToArray());
        }

        private sealed class GraphicsAdapter
        {
            public GraphicsAdapter(string name, ulong memory, int originalIndex)
            {
                Name = name;
                OriginalIndex = originalIndex;
                DedicatedScore = CalculateDedicatedScore(name, memory);
            }

            public string Name { get; private set; }
            public int OriginalIndex { get; private set; }
            public int DedicatedScore { get; private set; }

            private static int CalculateDedicatedScore(string name, ulong memory)
            {
                string normalized = name.ToLowerInvariant();
                if (normalized.Contains("microsoft") || normalized.Contains("remote") || normalized.Contains("virtual"))
                {
                    return -100;
                }
                if (normalized.Contains("nvidia") || normalized.Contains("geforce") ||
                    normalized.Contains("quadro") || normalized.Contains(" rtx") || normalized.Contains(" gtx"))
                {
                    return 300;
                }
                if (normalized.Contains("radeon rx") || normalized.Contains("radeon pro") ||
                    normalized.Contains("firepro"))
                {
                    return 260;
                }
                if (normalized.Contains("intel") && normalized.Contains("arc") && memory >= 2147483648UL)
                {
                    return 220;
                }
                if (normalized.Contains("iris") || normalized.Contains("uhd") ||
                    normalized.Contains("intel(r) hd") || normalized.Contains("intel hd"))
                {
                    return 0;
                }
                return memory >= 2147483648UL ? 150 : 50;
            }
        }

        private static string ReadDotNetFramework()
        {
            const string keyPath = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
            {
                object releaseValue = key == null ? null : key.GetValue("Release");
                int release;
                if (releaseValue == null || !int.TryParse(releaseValue.ToString(), out release))
                {
                    return UiText.Get(".NET Framework 4.x not identified");
                }

                string version = release >= 533320 ? "4.8.1" : release >= 528040 ? "4.8" : "4.x";
                return ".NET Framework " + version + " (Release " + release + ")";
            }
        }
    }
}
