using System.Runtime.Versioning;
using System.Reflection;
using ResoniteModLoader;

namespace ResoniteImeIntegration.Windows;

[SupportedOSPlatform("windows")]
internal static class WindowsHwndProvider
{
    private static readonly string[] HandleNames = ["HWND", "Hwnd", "WindowHandle", "Handle"];
    private static readonly string[] InstanceNames = ["Instance", "Current", "Singleton"];

    public static bool TryGetHwndViaRenderite(out nint hwnd)
    {
        hwnd = IntPtr.Zero;

        try
        {
            var assemblies = AppDomain
                .CurrentDomain.GetAssemblies()
                .Where(a =>
                    a.GetName().Name?.Contains("Renderite", StringComparison.OrdinalIgnoreCase)
                    ?? false
                )
                .ToArray();

            foreach (var assembly in assemblies)
            {
                foreach (var type in SafeGetTypes(assembly))
                {
                    if (TryGetStaticHandle(type, out hwnd) || TryGetInstanceHandle(type, out hwnd))
                    {
                        return hwnd != IntPtr.Zero;
                    }
                }
            }
        }
        catch
        {
            return false;
        }

        return hwnd != IntPtr.Zero;
    }

    private static bool TryGetStaticHandle(Type type, out nint hwnd)
    {
        hwnd = IntPtr.Zero;

        foreach (
            var property in type.GetProperties(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            )
        )
        {
            if (
                HandleNames.Any(name =>
                    property.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
                )
                && TryGetIntPtr(() => property.GetValue(null), out hwnd)
            )
            {
                SafeLog($"Renderite handle candidate: {type.FullName}::{property.Name}");
                return true;
            }
        }

        foreach (
            var method in type.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            )
        )
        {
            if (
                method.GetParameters().Length == 0
                && HandleNames.Any(name => method.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                && TryGetIntPtr(() => method.Invoke(null, Array.Empty<object>()), out hwnd)
            )
            {
                SafeLog($"Renderite handle candidate: {type.FullName}::{method.Name}()");
                return true;
            }
        }

        return false;
    }

    private static bool TryGetInstanceHandle(Type type, out nint hwnd)
    {
        hwnd = IntPtr.Zero;

        foreach (
            var property in type.GetProperties(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            )
        )
        {
            if (
                !InstanceNames.Any(name =>
                    property.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                continue;
            }

            var instance = property.GetValue(null);
            if (instance is null)
            {
                continue;
            }

            foreach (
                var instanceProperty in instance
                    .GetType()
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            )
            {
                if (
                    HandleNames.Any(name =>
                        instanceProperty.Name.Contains(name, StringComparison.OrdinalIgnoreCase)
                    )
                    && TryGetIntPtr(() => instanceProperty.GetValue(instance), out hwnd)
                )
                {
                    SafeLog(
                        $"Renderite handle candidate: {instance.GetType().FullName}::{instanceProperty.Name}"
                    );
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetIntPtr(Func<object?> getter, out nint value)
    {
        value = IntPtr.Zero;

        try
        {
            var raw = getter();
            value = raw switch
            {
                nint native => native,
                long signed => new IntPtr(signed),
                ulong unsigned => new IntPtr(unchecked((long)unsigned)),
                _ => IntPtr.Zero,
            };
            return value != IntPtr.Zero;
        }
        catch
        {
            return false;
        }
    }

    private static Type[] SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null).Cast<Type>().ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static void SafeLog(string message)
    {
        try
        {
            ResoniteMod.Msg(message);
        }
        catch
        {
            // Ignore logging failures.
        }
    }
}
