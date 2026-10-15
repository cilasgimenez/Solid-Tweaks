using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using SolidWorks.Interop.sldworks;
using SolidTweaks.Shared;

namespace SolidTweaks.Infrastructure.SolidWorks
{
    internal static class SolidWorksRunningSessions
    {
        [DllImport("ole32.dll")] private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable table);
        [DllImport("ole32.dll")] private static extern int CreateBindCtx(int reserved, out IBindCtx context);

        public static ISldWorks Find(int year)
        {
            // A versioned ProgID alone is insufficient: verify the actual server revision.
            foreach (string progId in new[] { "SldWorks.Application." + (year - 1992), "SldWorks.Application" })
            {
                object candidate = null;
                try
                {
                    candidate = Marshal.GetActiveObject(progId);
                    if (Matches(candidate, year)) return (ISldWorks)candidate;
                }
                catch (COMException) { }
                Release(candidate);
            }

            IRunningObjectTable table = null;
            IEnumMoniker enumerator = null;
            IBindCtx context = null;
            try
            {
                Marshal.ThrowExceptionForHR(GetRunningObjectTable(0, out table));
                Marshal.ThrowExceptionForHR(CreateBindCtx(0, out context));
                table.EnumRunning(out enumerator);
                var monikers = new IMoniker[1];
                while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    object candidate = null;
                    try
                    {
                        string name;
                        monikers[0].GetDisplayName(context, null, out name);
                        if (name.IndexOf("SolidWorks", StringComparison.OrdinalIgnoreCase) < 0 &&
                            name.IndexOf("SldWorks", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        table.GetObject(monikers[0], out candidate);
                        if (Matches(candidate, year)) { object selected = candidate; candidate = null; return (ISldWorks)selected; }
                    }
                    catch (COMException) { }
                    finally { Release(candidate); Release(monikers[0]); }
                }
            }
            catch (COMException) { }
            finally { Release(enumerator); Release(context); Release(table); }
            return null;
        }

        private static bool Matches(object candidate, int year)
        {
            ISldWorks sw = candidate as ISldWorks;
            return sw != null && SolidWorksTarget.YearFromRevision(sw.RevisionNumber()) == year;
        }
        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
        }
    }
}
