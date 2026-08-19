using UnityEngine;

/// <summary>
/// Generic sound helper for anything in the game (gunshots, NPC hits, footsteps, etc).
/// No need to drag an AudioSource around manually: it's created on the Transform you
/// pass in, or you can play a one-shot at a world position. Drop an AudioClip into any
/// gun/NPC "Audio" field in the inspector and call SoundFX.Play(...).
/// </summary>
public static class SoundFX
{
    // 3D sound at a Transform (reuses its AudioSource if it already has one).
    public static void Play(Transform where, AudioClip clip, float volume = 1f,
        float minDist = 1f, float maxDist = 20f)
    {
        if (clip == null || where == null) return;
        AudioSource a = where.GetComponent<AudioSource>();
        if (a == null)
        {
            a = where.gameObject.AddComponent<AudioSource>();
            a.playOnAwake = false;
        }
        a.spatialBlend = 0.9f;
        a.minDistance = minDist;
        a.maxDistance = maxDist;
        a.pitch = Random.Range(0.95f, 1.05f); // tiny pitch shift so rapid shots don't sound robotic
        a.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    // One-shot 3D sound at a world position (object auto-dies when the clip ends).
    public static void PlayAtPoint(Vector3 point, AudioClip clip, float volume = 1f,
        float minDist = 1f, float maxDist = 20f)
    {
        if (clip == null) return;
        GameObject go = new GameObject("SoundFX");
        go.transform.position = point;
        AudioSource a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.spatialBlend = 1f;
        a.minDistance = minDist;
        a.maxDistance = maxDist;
        a.pitch = Random.Range(0.95f, 1.05f);
        a.PlayOneShot(clip, Mathf.Clamp01(volume));
        Object.Destroy(go, clip.length / 2f + 0.1f);
    }
}
