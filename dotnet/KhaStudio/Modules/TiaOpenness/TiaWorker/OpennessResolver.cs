using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace KhaStudio.TiaOpenness;

/// <summary>
/// Tự động dò tìm và nạp thư viện Siemens.Engineering.dll, Siemens.Engineering.Hmi.dll
/// cùng các Contract liên quan từ các phiên bản TIA Portal (V21, V20, V19, V18, V17...) trên hệ điều hành.
/// </summary>
public static class OpennessResolver
{
    private static readonly List<string> ProbeDirectories = new();
    private static bool _isRegistered;
    private static string? _primaryVersion;

    public static void Initialize()
    {
        if (_isRegistered) return;

        // Quét các phiên bản TIA Portal phổ biến (ưu tiên phiên bản cao nhất trước)
        var versions = new[] { "V21", "V20", "V19", "V18", "V17", "V16", "V15.1", "V15" };
        var basePath = @"C:\Program Files\Siemens\Automation";

        foreach (var v in versions)
        {
            var portalDir = Path.Combine(basePath, $"Portal {v}");
            if (!Directory.Exists(portalDir)) continue;

            if (_primaryVersion == null)
            {
                _primaryVersion = v;
            }

            // 1. PublicAPI/Vxx/net48 (TIA V21+ chứa Siemens.Engineering.Base.dll, Step7, WinCC...)
            var net48ApiDir = Path.Combine(portalDir, "PublicAPI", v, "net48");
            if (Directory.Exists(net48ApiDir))
            {
                ProbeDirectories.Add(net48ApiDir);
            }

            // 2. PublicAPI/Vxx (TIA V17-V20 chứa Siemens.Engineering.dll)
            var publicApiDir = Path.Combine(portalDir, "PublicAPI", v);
            if (Directory.Exists(publicApiDir))
            {
                ProbeDirectories.Add(publicApiDir);
            }

            // Bin/PublicAPI (nơi chứa Siemens.Engineering.Contract.dll)
            var binPublicApiDir = Path.Combine(portalDir, "Bin", "PublicAPI");
            if (Directory.Exists(binPublicApiDir))
            {
                ProbeDirectories.Add(binPublicApiDir);
            }

            // Bin (chứa các dependency phụ trợ của Siemens)
            var binDir = Path.Combine(portalDir, "Bin");
            if (Directory.Exists(binDir))
            {
                ProbeDirectories.Add(binDir);
            }
        }

        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        _isRegistered = true;
    }

    public static string? PrimaryVersion => _primaryVersion;

    public static IReadOnlyList<string> DiscoveredProbeDirectories => ProbeDirectories;

    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var requestedName = new AssemblyName(args.Name).Name;
        if (string.IsNullOrEmpty(requestedName)) return null;

        foreach (var dir in ProbeDirectories)
        {
            var candidate = Path.Combine(dir, requestedName + ".dll");
            if (File.Exists(candidate))
            {
                try
                {
                    return Assembly.LoadFrom(candidate);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Resolver Load Fail] {candidate}: {ex.Message}");
                }
            }
        }

        return null;
    }
}
