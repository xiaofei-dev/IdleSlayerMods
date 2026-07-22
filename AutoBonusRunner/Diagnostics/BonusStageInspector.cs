using System.Collections;
using System.Reflection;
using Il2Cpp;
using UnityEngine;

namespace AutoBonusRunner.Diagnostics;

internal static class BonusStageInspector
{
    private static bool pickedUpReadFailureLogged;

    private static readonly string[] RelevantMemberTerms =
    {
        "bonus", "section", "sphere", "orb", "time", "spirit",
        "silver", "death", "map", "speed", "boost", "required",
        "current", "complete", "ground", "jump", "reward", "giving",
        "wait"
    };

    private static readonly string[] RelevantObjectTerms =
    {
        "bonus", "section", "sphere", "orb", "spirit", "silver",
        "death", "spike", "saw", "platform", "finish",
        "player", "timer", "reward", "coin", "chest", "box"
    };

    internal static void LogControllerSnapshot(string reason)
    {
        BonusMapController controller = BonusMapController.instance;
        if (controller == null)
        {
            BonusRunnerLog.Debug($"Controller snapshot ({reason}): BonusMapController.instance is null.", "Inspector");
            return;
        }

        BonusRunnerLog.Debug(
            $"Controller snapshot ({reason}): Type={controller.GetType().FullName}, " +
            $"InstanceId={controller.GetInstanceID()}, Active={controller.gameObject?.activeInHierarchy}.",
            "Inspector");

        LogRelevantMembers(controller);
        LogRelevantSceneObjects();
    }

    internal static bool TryGetBonusSphereCount(out int count)
    {
        count = -1;
        try
        {
            BonusMapController controller = BonusMapController.instance;
            if (controller == null)
                return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            Type type = controller.GetType();
            PropertyInfo property = type.GetProperty(
                "bonusSpheresPickedUp",
                flags);
            object value = property?.GetValue(controller) ??
                type.GetField("bonusSpheresPickedUp", flags)?.GetValue(controller);
            if (value == null)
                return false;
            count = Convert.ToInt32(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static string DescribeActiveSpheres(float left, float right)
    {
        try
        {
            GameObject[] objects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            Vector3[] spheres = objects
                .Where(gameObject =>
                    IsActiveCurrentSectionBonusSphere(gameObject) &&
                    gameObject.transform.position.x >= left &&
                    gameObject.transform.position.x <= right)
                .Select(gameObject => gameObject.transform.position)
                .OrderBy(position => position.x)
                .ThenBy(position => position.y)
                .ToArray();
            string positions = string.Join(
                ";",
                spheres.Take(40).Select(
                    position => $"({position.x:F2},{position.y:F2})"));
            return $"Active={spheres.Length},Range=[{left:F2},{right:F2}]," +
                $"Positions=[{positions}]" +
                (spheres.Length > 40 ? ",Capped=True" : string.Empty);
        }
        catch (Exception exception)
        {
            return $"ScanFailed:{exception.GetType().Name}";
        }
    }

    internal static bool TryGetActiveSphereVerticalBounds(
        float left,
        float right,
        out int count,
        out float minimumY,
        out float maximumY)
    {
        return TryGetActiveSphereVerticalBounds(
            left,
            right,
            out count,
            out minimumY,
            out maximumY,
            out _);
    }

    internal static bool TryGetActiveSphereVerticalBounds(
        float left,
        float right,
        out int count,
        out float minimumY,
        out float maximumY,
        out bool scanSucceeded)
    {
        count = 0;
        minimumY = float.PositiveInfinity;
        maximumY = float.NegativeInfinity;
        scanSucceeded = false;
        try
        {
            GameObject[] objects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (GameObject gameObject in objects)
            {
                if (!IsActiveCurrentSectionBonusSphere(gameObject))
                {
                    continue;
                }

                Vector3 position = gameObject.transform.position;
                if (position.x < left || position.x > right)
                    continue;

                count++;
                minimumY = Mathf.Min(minimumY, position.y);
                maximumY = Mathf.Max(maximumY, position.y);
            }

            scanSucceeded = true;
            return count > 0;
        }
        catch
        {
            count = 0;
            minimumY = float.PositiveInfinity;
            maximumY = float.NegativeInfinity;
            scanSucceeded = false;
            return false;
        }
    }

    internal static Vector2[] GetActiveSpherePositions(
        float left,
        float right,
        int maximumCount = 160)
    {
        try
        {
            return UnityEngine.Object.FindObjectsOfType<GameObject>()
                .Where(gameObject =>
                    IsActiveCurrentSectionBonusSphere(gameObject) &&
                    gameObject.transform.position.x >= left &&
                    gameObject.transform.position.x <= right)
                .Select(gameObject =>
                {
                    Vector3 position = gameObject.transform.position;
                    return new Vector2(position.x, position.y);
                })
                .OrderBy(position => position.x)
                .ThenBy(position => position.y)
                .Take(Mathf.Max(1, maximumCount))
                .ToArray();
        }
        catch
        {
            return Array.Empty<Vector2>();
        }
    }

    internal static string DescribeActiveRewardObjects(
        float left,
        float right)
    {
        try
        {
            GameObject[] matches = UnityEngine.Object
                .FindObjectsOfType<GameObject>()
                .Where(gameObject =>
                    gameObject != null &&
                    gameObject.activeInHierarchy &&
                    gameObject.transform.position.x >= left &&
                    gameObject.transform.position.x <= right &&
                    IsRewardObject(gameObject.name))
                .OrderBy(gameObject => gameObject.transform.position.x)
                .ThenBy(gameObject => gameObject.name)
                .Take(40)
                .ToArray();
            string descriptions = string.Join(
                ";",
                matches.Select(gameObject =>
                {
                    Vector3 position = gameObject.transform.position;
                    return $"{GetPath(gameObject.transform)}@" +
                        $"({position.x:F2},{position.y:F2})";
                }));
            return $"Active={matches.Length},Range=[{left:F2},{right:F2}]," +
                $"Objects=[{descriptions}]" +
                (matches.Length >= 40 ? ",Capped=True" : string.Empty);
        }
        catch (Exception exception)
        {
            return $"ScanFailed:{exception.GetType().Name}";
        }
    }

    private static bool IsActiveCurrentSectionBonusSphere(
        GameObject gameObject)
    {
        BonusSphere sphere;
        if (gameObject == null ||
            !gameObject.activeInHierarchy ||
            !gameObject.name.StartsWith(
                "Sphere",
                StringComparison.OrdinalIgnoreCase) ||
            (sphere = gameObject.GetComponent<BonusSphere>()) == null)
        {
            return false;
        }

        // BonusSphere objects stay active after collection. Active state alone
        // therefore makes already collected spheres look like valid route
        // objectives and can repeatedly pull jumps toward empty space.
        try
        {
            if (sphere.PickedUp())
                return false;
        }
        catch (Exception exception)
        {
            // Missing a sphere objective is safer than steering toward an
            // object whose pickup state could not be verified.
            if (!pickedUpReadFailureLogged)
            {
                pickedUpReadFailureLogged = true;
                BonusRunnerLog.Debug(
                    $"BonusSphere.PickedUp read failed; unverified spheres " +
                    $"will be excluded. Error={exception.GetType().Name}:" +
                    $"{exception.Message}",
                    "Sphere");
            }
            return false;
        }

        BonusMapController controller = BonusMapController.instance;
        int sectionIndex = controller?.currentSectionIndex ?? -1;
        if (sectionIndex < 0)
            return false;

        string expectedRoot = $"Bonus Map Level {sectionIndex}";
        Transform cursor = gameObject.transform.parent;
        int depth = 0;
        while (cursor != null && depth++ < 12)
        {
            if (string.Equals(
                    cursor.name,
                    expectedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            cursor = cursor.parent;
        }

        return false;
    }

    private static void LogRelevantMembers(object instance)
    {
        Type type = instance.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

        foreach (PropertyInfo property in type.GetProperties(flags)
                     .Where(property => property.GetIndexParameters().Length == 0 && IsRelevant(property.Name))
                     .OrderBy(property => property.Name))
        {
            LogMember(instance, property.Name, () => property.GetValue(instance));
        }

        foreach (FieldInfo field in type.GetFields(flags)
                     .Where(field => IsRelevant(field.Name))
                     .OrderBy(field => field.Name))
        {
            LogMember(instance, field.Name, () => field.GetValue(instance));
        }
    }

    private static void LogMember(object owner, string name, Func<object> read)
    {
        try
        {
            object value = read();
            BonusRunnerLog.Debug($"{owner.GetType().Name}.{name}={FormatValue(value)}", "Inspector");
        }
        catch (Exception exception)
        {
            BonusRunnerLog.Debug($"{owner.GetType().Name}.{name}=<read failed: {exception.GetType().Name}: {exception.Message}>", "Inspector");
        }
    }

    private static string FormatValue(object value)
    {
        if (value == null) return "<null>";
        if (value is UnityEngine.Object unityObject)
            return unityObject == null ? "<destroyed>" : $"{unityObject.name} ({unityObject.GetType().Name}, Id={unityObject.GetInstanceID()})";
        if (value is string text) return $"\"{text}\"";
        if (value is IEnumerable enumerable)
        {
            int count = 0;
            foreach (object _ in enumerable)
            {
                count++;
                if (count >= 10000) break;
            }
            return $"{value.GetType().Name}(Count={count})";
        }
        return value.ToString();
    }

    private static void LogRelevantSceneObjects()
    {
        try
        {
            GameObject[] objects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            string[] matches = objects
                .Where(gameObject => gameObject != null && IsRelevantObject(gameObject.name))
                .OrderBy(gameObject => gameObject.name)
                .Take(250)
                .Select(gameObject =>
                {
                    Vector3 position = gameObject.transform.position;
                    return $"{GetPath(gameObject.transform)}@({position.x:F2},{position.y:F2},{position.z:F2})";
                })
                .ToArray();

            BonusRunnerLog.Debug(
                $"Relevant active scene objects ({matches.Length}, capped at 250): {string.Join(" | ", matches)}",
                "Scene");
        }
        catch (Exception exception)
        {
            BonusRunnerLog.Debug($"Scene object scan failed: {exception.Message}", "Scene");
        }
    }

    private static string GetPath(Transform transform)
    {
        string path = transform.name;
        Transform parent = transform.parent;
        int depth = 0;
        while (parent != null && depth++ < 8)
        {
            path = $"{parent.name}/{path}";
            parent = parent.parent;
        }
        return path;
    }

    private static bool IsRelevant(string name) =>
        RelevantMemberTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static bool IsRelevantObject(string name) =>
        RelevantObjectTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
        name.Equals("Ground", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Ground ", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Ground(", StringComparison.OrdinalIgnoreCase);

    private static bool IsRewardObject(string name) =>
        name.Contains("coin", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("chest", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("box", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("reward", StringComparison.OrdinalIgnoreCase);
}
