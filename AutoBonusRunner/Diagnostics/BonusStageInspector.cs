using System.Collections;
using System.Reflection;
using AutoBonusRunner.Routing;
using Il2Cpp;
using UnityEngine;

namespace AutoBonusRunner.Diagnostics;

internal static class BonusStageInspector
{
    // PlayerMovement stores runner speeds in native units while Rigidbody2D
    // exposes world units. Current game builds use 50 native units per one
    // world-unit/second (for example currentSpeed=470 and body VX=9.4).
    // Infer the scale from the same-frame pair and retain 50 only when the
    // typed values are unmistakably native-scaled but the body is stationary.
    private const float NativeSpeedUnitsPerWorldUnit = 50f;

    private static float ResolveNativeSpeedScale(
        float rawCurrentSpeed,
        float observedWorldSpeed,
        float rawMaximumBoost,
        float rawDecrease)
    {
        bool unmistakablyNativeScaled =
            Mathf.Abs(rawCurrentSpeed) > 80f ||
            Mathf.Abs(rawMaximumBoost) > 80f ||
            Mathf.Abs(rawDecrease) > 120f;
        if (unmistakablyNativeScaled)
        {
            // During map activation currentSpeed can still contain the prior
            // native step (the retained trace has 200 beside body VX 9.5).
            // Accept the same-frame ratio only near the verified scale; an
            // asynchronous first frame must not invent a 21:1 conversion.
            if (observedWorldSpeed > 0.25f)
            {
                float inferred =
                    Mathf.Abs(rawCurrentSpeed) / observedWorldSpeed;
                if (float.IsFinite(inferred) &&
                    Mathf.Abs(inferred - NativeSpeedUnitsPerWorldUnit) <= 5f)
                {
                    return inferred;
                }
            }

            return NativeSpeedUnitsPerWorldUnit;
        }

        return 1f;
    }

    internal static bool TryReadSpiritBoostComponentWorldUnits(
        PlayerMovement player,
        out float component)
    {
        component = 0f;
        if (player == null)
            return false;
        try
        {
            float rawComponent = player.currentSpiritBoost;
            float rawMaximum = player.maxSpiritBoostSpeed;
            float rawDecrease = player.spiritBoostSpeedDecrease;
            float rawCurrentSpeed = player.currentSpeed;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            float observedSpeed = Mathf.Abs(body?.velocity.x ?? 0f);
            float scale = ResolveNativeSpeedScale(
                rawCurrentSpeed,
                observedSpeed,
                rawMaximum,
                rawDecrease);
            component = Mathf.Max(0f, rawComponent / scale);
            return float.IsFinite(component) && component <= 80f;
        }
        catch
        {
            component = 0f;
            return false;
        }
    }

    private static bool pickedUpReadFailureLogged;
    private static bool spiritBoostReadFailureLogged;
    private static BonusSphere[] cachedBonusSpheres =
        Array.Empty<BonusSphere>();
    private static SpiritBoost[] cachedSpiritBoosts =
        Array.Empty<SpiritBoost>();
    private readonly record struct SpiritBoostColliderBinding(
        SpiritBoost Boost,
        Collider2D[] Colliders);
    private static SpiritBoostColliderBinding[] cachedSpiritBoostBindings =
        Array.Empty<SpiritBoostColliderBinding>();
    private static int cachedSceneObjectSection = int.MinValue;
    private static bool sceneObjectCacheReady;
    // Bonus-stage sphere rows are pooled. FindObjectsOfType<T>() only returns
    // the rows that are active when the section cache is built, so a cache
    // that lives for the whole section eventually contains only collected
    // objects behind the player. Refresh only when that forward inventory is
    // exhausted. This discovers newly activated rows without restoring the
    // old scene-wide scan on every physics step.
    private const float SphereForwardRefreshCooldownSeconds = 0.60f;
    private const float SphereForwardRefreshMinimumAdvance = 6.0f;
    private static float nextSphereForwardRefreshTime;
    private static float lastSphereForwardRefreshLeft =
        float.NegativeInfinity;
    private const float SpiritBoostForwardRefreshCooldownSeconds = 0.75f;
    private const float SpiritBoostForwardRefreshMinimumAdvance = 8.0f;
    private static float nextSpiritBoostForwardRefreshTime;
    private static float lastSpiritBoostForwardRefreshLeft =
        float.NegativeInfinity;

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
            Vector3[] spheres = GetCachedBonusSpheres()
                .Where(sphere =>
                    IsActiveCurrentSectionBonusSphere(sphere) &&
                    sphere.transform.position.x >= left &&
                    sphere.transform.position.x <= right)
                .Select(sphere => sphere.transform.position)
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
            foreach (BonusSphere sphere in GetCachedBonusSpheres())
            {
                if (!IsActiveCurrentSectionBonusSphere(sphere))
                {
                    continue;
                }

                Vector3 position = sphere.transform.position;
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
            BonusSphere[] spheres =
                GetCachedBonusSpheresForForwardRange(left, right);
            return spheres
                .Where(sphere =>
                    IsActiveCurrentSectionBonusSphere(sphere) &&
                    sphere.transform.position.x >= left &&
                    sphere.transform.position.x <= right)
                .Select(sphere =>
                {
                    Vector3 position = sphere.transform.position;
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

    internal static SpiritBoostRouteContext CaptureSpiritBoostRouteContext(
        PlayerMovement player,
        bool enabled,
        float left,
        float right,
        float verifiedBaseSpeed = 0f)
    {
        if (!enabled)
            return SpiritBoostRouteContext.Disabled("NativeModeDisabled");

        if (player == null)
        {
            return new SpiritBoostRouteContext(
                true,
                false,
                false,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                -0.35f,
                0.35f,
                0f,
                1.80f,
                Array.Empty<BonusSpeedBoostTrigger>(),
                "PlayerUnavailable");
        }

        bool kinematicsAvailable = false;
        float currentBoost = 0f;
        float maximumBoost = 0f;
        float decrease = 0f;
        float nativeCurrentSpeed = 0f;
        float nativePreStepSpeed = 0f;
        float baseSpeed = 0f;
        string kinematicsEvidence;
        try
        {
            float rawCurrentBoost = player.currentSpiritBoost;
            float rawMaximumBoost = player.maxSpiritBoostSpeed;
            float rawDecrease = player.spiritBoostSpeedDecrease;
            float rawCurrentSpeed = player.currentSpeed;
            float rawPreStepSpeed = player.preStepSpeed;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            float observedSpeed = Mathf.Abs(body?.velocity.x ?? 0f);
            float nativeScale = ResolveNativeSpeedScale(
                rawCurrentSpeed,
                observedSpeed,
                rawMaximumBoost,
                rawDecrease);
            currentBoost = Mathf.Max(0f, rawCurrentBoost / nativeScale);
            maximumBoost = rawMaximumBoost / nativeScale;
            decrease = rawDecrease / nativeScale;
            nativeCurrentSpeed = rawCurrentSpeed / nativeScale;
            // preStepSpeed is already world-scaled in the retained runtime
            // (`20.178` beside currentSpeed `470`). Normalize it only when it
            // is itself unmistakably in the native range.
            nativePreStepSpeed = Mathf.Abs(rawPreStepSpeed) > 80f
                ? rawPreStepSpeed / nativeScale
                : rawPreStepSpeed;
            float boostSubtractedSpeed =
                observedSpeed - Mathf.Max(0f, currentBoost);
            baseSpeed = verifiedBaseSpeed > 1f && verifiedBaseSpeed < 80f
                ? verifiedBaseSpeed
                : boostSubtractedSpeed;
            kinematicsAvailable =
                float.IsFinite(currentBoost) &&
                float.IsFinite(maximumBoost) &&
                float.IsFinite(decrease) &&
                float.IsFinite(nativeCurrentSpeed) &&
                float.IsFinite(nativePreStepSpeed) &&
                float.IsFinite(baseSpeed) &&
                currentBoost >= 0f && currentBoost <= 80f &&
                maximumBoost > 0.05f && maximumBoost <= 80f &&
                currentBoost <= maximumBoost + 0.50f &&
                decrease > 0.01f && decrease <= 120f &&
                baseSpeed > 1f && baseSpeed < 80f &&
                (observedSpeed > 1f || verifiedBaseSpeed > 1f);
            kinematicsEvidence = kinematicsAvailable
                ? $"TypedPlayerMovementFieldsNormalized[Scale=" +
                  $"{nativeScale:F3},RawBoost={rawCurrentBoost:F3}," +
                  $"RawMax={rawMaximumBoost:F3},RawDecrease=" +
                  $"{rawDecrease:F3},RawCurrentSpeed=" +
                  $"{rawCurrentSpeed:F3},RawPreStep=" +
                  $"{rawPreStepSpeed:F3}]"
                : $"TypedFieldsOutsidePhysicalRangeAfterNormalization[" +
                  $"Scale={nativeScale:F3},RawBoost=" +
                  $"{rawCurrentBoost:F3},RawMax={rawMaximumBoost:F3}," +
                  $"RawDecrease={rawDecrease:F3},RawCurrentSpeed=" +
                  $"{rawCurrentSpeed:F3},RawPreStep=" +
                  $"{rawPreStepSpeed:F3}]";
        }
        catch (Exception exception)
        {
            kinematicsEvidence =
                $"TypedFieldReadFailed:{exception.GetType().Name}";
        }

        float playerLeftOffset = -0.35f;
        float playerRightOffset = 0.35f;
        float playerBottomOffset = 0f;
        float playerTopOffset = 1.80f;
        try
        {
            if (player.playerCollider != null)
            {
                Bounds bounds = player.playerCollider.bounds;
                Vector3 origin = player.transform.position;
                playerLeftOffset = bounds.min.x - origin.x;
                playerRightOffset = bounds.max.x - origin.x;
                playerBottomOffset = bounds.min.y - origin.y;
                playerTopOffset = bounds.max.y - origin.y;
            }
        }
        catch
        {
            // The conservative fallback body above is sufficient for a
            // fail-safe trigger overlap test; native kinematics remain valid.
        }

        bool triggerScanSucceeded = false;
        bool triggerStateUnknown = false;
        List<BonusSpeedBoostTrigger> triggers = new();
        try
        {
            foreach (SpiritBoostColliderBinding binding in
                     GetCachedSpiritBoostBindingsForForwardRange(
                         left,
                         right))
            {
                SpiritBoost boost = binding.Boost;
                if (boost == null ||
                    boost.gameObject == null ||
                    !boost.gameObject.activeInHierarchy)
                {
                    continue;
                }

                bool pickedUp;
                try
                {
                    pickedUp = boost.PickedUp();
                }
                catch (Exception exception)
                {
                    if (!spiritBoostReadFailureLogged)
                    {
                        spiritBoostReadFailureLogged = true;
                        BonusRunnerLog.Debug(
                            $"SpiritBoost.PickedUp read failed; the " +
                            $"unverified trigger is excluded. Error=" +
                            $"{exception.GetType().Name}:" +
                            $"{exception.Message}",
                            "SpiritBoost");
                    }
                    triggerStateUnknown = true;
                    break;
                }
                if (pickedUp)
                    continue;

                // Pool prefabs may expose a decorative/root collider before
                // the actual pickup trigger. Prefer an enabled trigger from
                // the complete object tree; only then use another enabled
                // collider or the conservative visible-object fallback.
                Collider2D selectedCollider = null;
                foreach (Collider2D candidate in binding.Colliders)
                {
                    if (candidate == null ||
                        !candidate.enabled ||
                        candidate.gameObject == null ||
                        !candidate.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (selectedCollider == null ||
                        candidate.isTrigger && !selectedCollider.isTrigger ||
                        candidate.isTrigger == selectedCollider.isTrigger &&
                        candidate.gameObject == boost.gameObject &&
                        selectedCollider.gameObject != boost.gameObject)
                    {
                        selectedCollider = candidate;
                    }
                }
                Bounds bounds = selectedCollider != null
                    ? selectedCollider.bounds
                    : new Bounds(
                        boost.transform.position,
                        new Vector3(0.90f, 0.90f, 0.10f));

                if (bounds.max.x < left || bounds.min.x > right)
                    continue;

                triggers.Add(new BonusSpeedBoostTrigger(
                    bounds.min.x,
                    bounds.max.x,
                    bounds.min.y,
                    bounds.max.y,
                    boost.GetInstanceID(),
                    boost.gameObject.name));
            }

            triggerScanSucceeded = !triggerStateUnknown;
            if (triggerStateUnknown)
            {
                triggers.Clear();
                kinematicsEvidence += ";TriggerPickupStateUnknown";
            }
        }
        catch (Exception exception)
        {
            kinematicsEvidence +=
                $";TriggerScanFailed:{exception.GetType().Name}";
            triggers.Clear();
        }

        return new SpiritBoostRouteContext(
            true,
            kinematicsAvailable,
            triggerScanSucceeded,
            currentBoost,
            maximumBoost,
            decrease,
            nativeCurrentSpeed,
            nativePreStepSpeed,
            baseSpeed,
            playerLeftOffset,
            playerRightOffset,
            playerBottomOffset,
            playerTopOffset,
            triggers
                .Where(trigger => trigger.IsValid)
                .OrderBy(trigger => trigger.Left)
                .ThenBy(trigger => trigger.Bottom)
                .ToArray(),
            kinematicsEvidence);
    }

    internal static void ResetSceneObjectCaches(string reason)
    {
        bool hadCache = sceneObjectCacheReady;
        cachedBonusSpheres = Array.Empty<BonusSphere>();
        cachedSpiritBoosts = Array.Empty<SpiritBoost>();
        cachedSpiritBoostBindings =
            Array.Empty<SpiritBoostColliderBinding>();
        cachedSceneObjectSection = int.MinValue;
        sceneObjectCacheReady = false;
        nextSphereForwardRefreshTime = 0f;
        lastSphereForwardRefreshLeft = float.NegativeInfinity;
        nextSpiritBoostForwardRefreshTime = 0f;
        lastSpiritBoostForwardRefreshLeft = float.NegativeInfinity;
        if (hadCache)
        {
            BonusRunnerLog.Debug(
                $"SceneObjectCacheReset Reason={reason}.",
                "Performance");
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

    private static BonusSphere[] GetCachedBonusSpheres()
    {
        EnsureSceneObjectCaches();
        return cachedBonusSpheres;
    }

    private static BonusSphere[] GetCachedBonusSpheresForForwardRange(
        float left,
        float right)
    {
        EnsureSceneObjectCaches();

        int cachedActive = 0;
        int cachedAhead = 0;
        int cachedInRange = 0;
        float furthestCachedActiveX = float.NegativeInfinity;
        foreach (BonusSphere sphere in cachedBonusSpheres)
        {
            if (!IsActiveCurrentSectionBonusSphere(sphere))
                continue;

            cachedActive++;
            float x = sphere.transform.position.x;
            furthestCachedActiveX = Mathf.Max(furthestCachedActiveX, x);
            if (x >= left)
                cachedAhead++;
            if (x >= left && x <= right)
                cachedInRange++;
        }

        // An active cached objective beyond the requested right edge proves
        // that the current pool generation is still represented. Rebuilding
        // would add no route information. Refresh only after every surviving
        // cached objective is behind the forward query.
        if (cachedInRange > 0 ||
            cachedAhead > 0 ||
            Time.unscaledTime < nextSphereForwardRefreshTime ||
            left < lastSphereForwardRefreshLeft +
                SphereForwardRefreshMinimumAdvance)
        {
            return cachedBonusSpheres;
        }

        int section =
            BonusMapController.instance?.currentSectionIndex ?? -1;
        if (section < 0 || cachedSceneObjectSection != section)
            return cachedBonusSpheres;

        nextSphereForwardRefreshTime =
            Time.unscaledTime + SphereForwardRefreshCooldownSeconds;
        lastSphereForwardRefreshLeft = left;
        RebuildSceneObjectCaches(
            section,
            "SphereForwardWindowExhausted",
            isRefresh: true);

        int refreshedActive = 0;
        int refreshedAhead = 0;
        int refreshedInRange = 0;
        float furthestRefreshedActiveX = float.NegativeInfinity;
        foreach (BonusSphere sphere in cachedBonusSpheres)
        {
            if (!IsActiveCurrentSectionBonusSphere(sphere))
                continue;

            refreshedActive++;
            float x = sphere.transform.position.x;
            furthestRefreshedActiveX =
                Mathf.Max(furthestRefreshedActiveX, x);
            if (x >= left)
                refreshedAhead++;
            if (x >= left && x <= right)
                refreshedInRange++;
        }

        BonusRunnerLog.Debug(
            $"SphereForwardCacheRefresh Section={section}, " +
            $"Query=[{left:F2},{right:F2}], Before[Active=" +
            $"{cachedActive},Ahead={cachedAhead},InRange={cachedInRange}," +
            $"Furthest={FormatOptionalCoordinate(furthestCachedActiveX)}], " +
            $"After[Cached={cachedBonusSpheres.Length},Active=" +
            $"{refreshedActive},Ahead={refreshedAhead},InRange=" +
            $"{refreshedInRange},Furthest=" +
            $"{FormatOptionalCoordinate(furthestRefreshedActiveX)}]. " +
            "A pooled sphere generation was refreshed only after the " +
            "previous typed forward inventory was exhausted.",
            "Performance");
        return cachedBonusSpheres;
    }

    private static SpiritBoost[] GetCachedSpiritBoosts()
    {
        EnsureSceneObjectCaches();
        return cachedSpiritBoosts;
    }

    private static SpiritBoostColliderBinding[]
        GetCachedSpiritBoostBindings()
    {
        EnsureSceneObjectCaches();
        return cachedSpiritBoostBindings;
    }

    private static SpiritBoostColliderBinding[]
        GetCachedSpiritBoostBindingsForForwardRange(
            float left,
            float right)
    {
        EnsureSceneObjectCaches();

        int active = 0;
        int ahead = 0;
        int inRange = 0;
        foreach (SpiritBoostColliderBinding binding in
                 cachedSpiritBoostBindings)
        {
            SpiritBoost boost = binding.Boost;
            if (boost == null ||
                boost.gameObject == null ||
                !boost.gameObject.activeInHierarchy ||
                !IsUnderCurrentSectionRoot(boost.transform))
            {
                continue;
            }

            bool pickedUp;
            try
            {
                pickedUp = boost.PickedUp();
            }
            catch
            {
                // CaptureSpiritBoostRouteContext remains the authority for
                // reporting an unreadable pickup state. Do not use that
                // uncertainty as permission for a costly cache rebuild.
                pickedUp = false;
            }
            if (pickedUp)
                continue;

            active++;
            float minimumX = boost.transform.position.x;
            float maximumX = minimumX;
            foreach (Collider2D collider in binding.Colliders)
            {
                if (collider == null ||
                    !collider.enabled ||
                    collider.gameObject == null ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                minimumX = Mathf.Min(minimumX, bounds.min.x);
                maximumX = Mathf.Max(maximumX, bounds.max.x);
            }

            if (maximumX >= left)
                ahead++;
            if (maximumX >= left && minimumX <= right)
                inRange++;
        }

        if (ahead > 0 ||
            inRange > 0 ||
            Time.unscaledTime < nextSpiritBoostForwardRefreshTime ||
            left < lastSpiritBoostForwardRefreshLeft +
                SpiritBoostForwardRefreshMinimumAdvance)
        {
            return cachedSpiritBoostBindings;
        }

        int section =
            BonusMapController.instance?.currentSectionIndex ?? -1;
        if (section < 0 || cachedSceneObjectSection != section)
            return cachedSpiritBoostBindings;

        nextSpiritBoostForwardRefreshTime =
            Time.unscaledTime + SpiritBoostForwardRefreshCooldownSeconds;
        lastSpiritBoostForwardRefreshLeft = left;
        int before = cachedSpiritBoostBindings.Length;
        RebuildSceneObjectCaches(
            section,
            "SpiritBoostForwardWindowExhausted",
            isRefresh: true);
        BonusRunnerLog.Debug(
            $"SpiritBoostForwardCacheRefresh Section={section}, " +
            $"Query=[{left:F2},{right:F2}], Before[Bindings={before}," +
            $"Active={active},Ahead={ahead},InRange={inRange}], " +
            $"After[Bindings={cachedSpiritBoostBindings.Length}]. " +
            "A pooled boost generation was refreshed only after the prior " +
            "forward inventory was exhausted and the runner advanced.",
            "Performance");
        return cachedSpiritBoostBindings;
    }

    private static void EnsureSceneObjectCaches()
    {
        int section =
            BonusMapController.instance?.currentSectionIndex ?? -1;
        if (sceneObjectCacheReady &&
            cachedSceneObjectSection == section)
        {
            return;
        }

        RebuildSceneObjectCaches(
            section,
            "SectionInventoryBuild",
            isRefresh: false);
    }

    private static void RebuildSceneObjectCaches(
        int section,
        string reason,
        bool isRefresh)
    {
        if (!isRefresh)
        {
            nextSphereForwardRefreshTime = 0f;
            lastSphereForwardRefreshLeft = float.NegativeInfinity;
            nextSpiritBoostForwardRefreshTime = 0f;
            lastSpiritBoostForwardRefreshLeft =
                float.NegativeInfinity;
        }
        cachedBonusSpheres =
            (UnityEngine.Object.FindObjectsOfType<BonusSphere>() ??
             Array.Empty<BonusSphere>())
            .Where(sphere =>
                sphere != null &&
                IsUnderCurrentSectionRoot(sphere.transform))
            .ToArray();
        cachedSpiritBoosts =
            (UnityEngine.Object.FindObjectsOfType<SpiritBoost>() ??
             Array.Empty<SpiritBoost>())
            .Where(boost =>
                boost != null &&
                IsUnderCurrentSectionRoot(boost.transform))
            .ToArray();
        cachedSpiritBoostBindings = cachedSpiritBoosts
            .Select(boost => new SpiritBoostColliderBinding(
                boost,
                boost.GetComponentsInChildren<Collider2D>(true) ??
                Array.Empty<Collider2D>()))
            .ToArray();
        cachedSceneObjectSection = section;
        sceneObjectCacheReady = true;
        BonusRunnerLog.Debug(
            $"SceneObjectCache{(isRefresh ? "Refreshed" : "Built")} " +
            $"Section={section}, Reason={reason}, " +
            $"BonusSpheres={cachedBonusSpheres.Length}, " +
            $"SpiritBoosts={cachedSpiritBoosts.Length}, " +
            $"SpiritColliders=" +
            $"{cachedSpiritBoostBindings.Sum(binding => binding.Colliders.Length)}.",
            "Performance");
    }

    private static string FormatOptionalCoordinate(float value) =>
        float.IsFinite(value)
            ? value.ToString("F2")
            : "None";

    private static bool IsActiveCurrentSectionBonusSphere(
        BonusSphere sphere)
    {
        GameObject gameObject = sphere?.gameObject;
        if (sphere == null ||
            gameObject == null ||
            !gameObject.activeInHierarchy ||
            !gameObject.name.StartsWith(
                "Sphere",
                StringComparison.OrdinalIgnoreCase))
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

        // Section membership was resolved once when the typed inventory was
        // built. Re-walking every parent transform for every sphere on every
        // physics step is pure IL2CPP interop overhead.
        return true;
    }

    private static bool IsUnderCurrentSectionRoot(Transform transform)
    {
        BonusMapController controller = BonusMapController.instance;
        int sectionIndex = controller?.currentSectionIndex ?? -1;
        if (sectionIndex < 0 || transform == null)
            return false;

        string expectedRoot = $"Bonus Map Level {sectionIndex}";
        Transform cursor = transform.parent;
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
