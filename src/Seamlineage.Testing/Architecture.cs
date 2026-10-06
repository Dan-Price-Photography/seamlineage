using System.Reflection;

namespace Seamlineage.Testing;

/// <summary>
/// The seam, enforced: product assemblies may not know how they are deployed, so they reference only .NET's base class
/// library and an allowlist (typically Seamlineage.Contracts, Seamlineage.Operators and the product's own assemblies).
/// </summary>
public static class Architecture
{
    /// <summary>Referenced assemblies that are neither base class library nor start with one of <paramref name="allowedPrefixes"/>.</summary>
    public static IReadOnlyList<string> ReferencesOutside(Assembly assembly, params string[] allowedPrefixes) =>
    [
        .. assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => !IsBaseClassLibrary(name) && !allowedPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>Throws unless <paramref name="assembly"/> references only the base class library and <paramref name="allowedPrefixes"/>.</summary>
    public static void ReferencesOnlyBaseClassLibraryAnd(Assembly assembly, params string[] allowedPrefixes)
    {
        var offenders = ReferencesOutside(assembly, allowedPrefixes);
        if (offenders.Count > 0)
            throw new CheckFailedException(
                $"{assembly.GetName().Name} may reference only the base class library and {string.Join(", ", allowedPrefixes.Select(p => p + "*"))}, "
                + $"but references: {string.Join(", ", offenders)}");
    }

    private static bool IsBaseClassLibrary(string name) =>
        name == "System" || name.StartsWith("System.", StringComparison.Ordinal) || name is "netstandard" or "mscorlib";
}
