namespace DocFx.Plugin.LastModified;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibGit2Sharp;

/// <summary>
/// Points LibGit2Sharp at the native libraries shipped beside this plugin.
/// </summary>
/// <remarks>
/// LibGit2Sharp finds <c>git2-*</c> through the host application's deps.json.
/// DocFX loads a post-processor with <see cref="Assembly.LoadFrom(string)"/> into
/// its own process, so that probing never covers the plugin folder: the native
/// load then fails outright with DllNotFoundException, or — worse, with some
/// version pairings — binds a git2 built for a different LibGit2Sharp and faults
/// inside it once a buffer is freed.
/// <para>
/// Setting <see cref="GlobalSettings.NativeLibraryPath"/> is the supported way to
/// override that search, and it has to happen before the first native call, hence
/// the module initializer.
/// </para>
/// </remarks>
internal static class NativeLibraryLoader
{
    [ModuleInitializer]
    internal static void Init()
    {
        try
        {
            var dir = Path.GetDirectoryName(typeof(NativeLibraryLoader).Assembly.Location);
            if (string.IsNullOrEmpty(dir))
                return;

            foreach (var rid in CandidateRids())
            {
                var native = Path.Combine(dir, "runtimes", rid, "native");
                if (Directory.Exists(native))
                {
                    GlobalSettings.NativeLibraryPath = native;
                    return;
                }
            }
        }
        catch
        {
            // Nothing here is worth failing a docs build over. If the path cannot
            // be set, LibGit2Sharp falls back to its own probing and the processor
            // reports the failure per file.
        }
    }

    /// <summary>
    /// Runtime identifiers to try, most specific first. Both linux flavours are
    /// offered because the musl check is not worth a native call here: only one
    /// of the two directories is published for a given build.
    /// </summary>
    private static IEnumerable<string> CandidateRids()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => null,
        };
        if (arch is null)
            yield break;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return $"win-{arch}";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            yield return $"osx-{arch}";
        }
        else
        {
            yield return $"linux-{arch}";
            yield return $"linux-musl-{arch}";
        }
    }
}
