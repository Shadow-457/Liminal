using UnityEngine;

/// <summary>
/// Runtime-built blood squib. Spawns a short red particle burst at a hit point that
/// sprays along the surface normal (toward the shooter), so the blood "comes from
/// where we hit". No art assets required - the particle system is built once on first
/// use and cached.
/// </summary>
public static class BloodVFX
{
    private static GameObject _cached;
    private static Material _cachedMat;

    private static void Build()
    {
        _cached = new GameObject("BloodSquibFX", typeof(ParticleSystem));
        var ps = _cached.GetComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.15f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);      // travels along local -Z
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.10f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.70f, 0.10f, 0.11f),
            new Color(0.42f, 0.05f, 0.06f));
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f); // droplets tumble
        main.gravityModifier = 2.2f; // heavier droplets arc downward
        main.maxParticles = 320;

        // Two bursts: a fast forward spray, then a slower follow-up splash/clot.
        var em = ps.emission;
        em.enabled = true;
        em.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 22, 30),
            new ParticleSystem.Burst(0.03f, 10, 14),
        });

        // Cone shape gives the spray a natural spread. A cone emits along the
        // particle system's local -Z axis, so we orient the system on spawn to
        // spray along the surface normal (toward the shooter).
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 26f;
        sh.radius = 0.015f;
        sh.length = 0.015f;

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        if (rend == null) rend = _cached.AddComponent<ParticleSystemRenderer>();

        Shader s = Shader.Find("Particles/Standard Unlit");
        if (s == null) s = Shader.Find("Particles/Alpha Blended");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) s = Shader.Find("Sprites-Default");
        _cachedMat = new Material(s);
        _cachedMat.renderQueue = 4000;
        if (_cachedMat.HasProperty("_BaseColor")) _cachedMat.SetColor("_BaseColor", new Color(0.7f, 0.1f, 0.11f));
        if (_cachedMat.HasProperty("_TintColor")) _cachedMat.SetColor("_TintColor", new Color(0.7f, 0.1f, 0.11f, 0.6f));
        if (_cachedMat.HasProperty("_Color")) _cachedMat.SetColor("_Color", new Color(0.7f, 0.1f, 0.11f));
        rend.material = _cachedMat;

        _cached.hideFlags = HideFlags.HideAndDontSave;
    }

    // point  = world position of the hit
    // normal  = surface normal at the hit (blood sprays outward ALONG this direction)
    public static void Spawn(Vector3 point, Vector3 normal)
    {
        if (_cached == null) Build();
        if (_cached == null) return;

        // Cone emits along local -Z; forward (+Z) = -normal => -Z = +normal,
        // so blood squirts out toward the shooter.
        Vector3 fwd = -normal;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

        GameObject go = Object.Instantiate(_cached, point, rot);
        var ps = go.GetComponent<ParticleSystem>();
        ps.Play();

        float life = ps.main.startLifetime.constantMax;
        if (life <= 0f) life = 1f;
        Object.Destroy(go, life + 0.2f);
    }
}
