using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidTweaks.Shared;

namespace SolidTweaks.Infrastructure.SolidWorks
{
    internal sealed class SolidWorksPreferenceResolver
    {
        // Valor oficial no interop do SOLIDWORKS 2025; usado apenas se o DLL não puder ser carregado.
        private const int AutoNormalToSketchMode2025 = 477;
        private readonly Dictionary<string, int> cache = new Dictionary<string, int>(StringComparer.Ordinal);

        public int ResolveToggle(string preferenceName)
        {
            int value;
            if (cache.TryGetValue(SolidWorksTarget.Year + ":" + preferenceName, out value))
            {
                return value;
            }

            Type enumType = LoadPreferenceEnum();
            if (enumType != null && Enum.IsDefined(enumType, preferenceName))
            {
                value = Convert.ToInt32(Enum.Parse(enumType, preferenceName));
            }
            else if (string.Equals(preferenceName, "swAutoNormalToSketchMode", StringComparison.Ordinal))
            {
                value = AutoNormalToSketchMode2025;
            }
            else
            {
                throw new InvalidOperationException("SOLIDWORKS preference not found: " + preferenceName);
            }

            cache[SolidWorksTarget.Year + ":" + preferenceName] = value;
            return value;
        }

        private static Type LoadPreferenceEnum()
        {
            foreach (string path in CandidateInteropPaths())
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    Assembly assembly = Assembly.LoadFrom(path);
                    Type enumType = assembly.GetType("SolidWorks.Interop.swconst.swUserPreferenceToggle_e", false);
                    if (enumType != null)
                    {
                        return enumType;
                    }
                }
                catch (BadImageFormatException) { }
                catch (FileLoadException) { }
            }

            return null;
        }

        private static IEnumerable<string> CandidateInteropPaths()
        {
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SolidWorks.Interop.swconst.dll");

            string installFolder = ReadInstallFolder();
            if (!string.IsNullOrWhiteSpace(installFolder))
            {
                yield return Path.Combine(installFolder, "SolidWorks.Interop.swconst.dll");
                yield return Path.Combine(installFolder, "api", "redist", "SolidWorks.Interop.swconst.dll");
            }


        }

        internal static string ReadInstallFolder()
        {
            SolidWorksInstallation installation = SolidWorksInstallation.Selected;
            return installation == null ? null : installation.Folder;
        }

        internal static string ReadCurrentLanguage()
        {
            ISldWorks application = null;
            try
            {
                application = SolidWorksRunningSessions.Find(SolidWorksTarget.Year);
                if (application != null)
                {
                    string activeLanguage = application.GetCurrentLanguage();
                    if (!string.IsNullOrWhiteSpace(activeLanguage))
                    {
                        return activeLanguage;
                    }
                }
            }
            catch (COMException)
            {
            }
            catch (InvalidCastException)
            {
            }
            finally
            {
                try
                {
                    if (application != null && Marshal.IsComObject(application))
                    {
                        Marshal.ReleaseComObject(application);
                    }
                }
                catch (COMException)
                {
                }
            }

            string versionRoot = SolidWorksTarget.VersionRoot;
            try
            {
                using (RegistryKey general = Registry.CurrentUser.OpenSubKey(versionRoot + @"\General", false))
                {
                    object useEnglish = general == null ? null : general.GetValue("Use English language");
                    if (useEnglish != null && Convert.ToInt32(useEnglish) != 0)
                    {
                        return "english";
                    }
                }

                using (RegistryKey designCheck = Registry.CurrentUser.OpenSubKey(
                    versionRoot + @"\DesignCheck\CommonSettings", false))
                {
                    string language = designCheck == null
                        ? null
                        : designCheck.GetValue("CurrentLanguage") as string;
                    if (!string.IsNullOrWhiteSpace(language))
                    {
                        return language;
                    }
                }

                using (RegistryKey taskPane = Registry.CurrentUser.OpenSubKey(
                    versionRoot + @"\User Interface\TaskPane", false))
                {
                    object languageId = taskPane == null ? null : taskPane.GetValue("LangId");
                    if (languageId != null)
                    {
                        return languageId.ToString();
                    }
                }
            }
            catch
            {
            }

            return "english";
        }
    }
}
