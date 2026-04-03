using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Throw a <see cref="SpawnLure"/> prefab in the current move/facing direction (stealth distraction).
/// </summary>
public class PlayerLure : MonoBehaviour
{
    [SerializeField] GameObject lurePrefab;
    [SerializeField] float throwSpeed = 9f;
    [SerializeField] float spawnOffset = 0.45f;
    [SerializeField] float cooldownSeconds = 1.25f;
    [SerializeField] AudioClip[] throwSfx;
    [SerializeField, Range(0f, 1f)] float throwSfxVolume = 1f;
    [Tooltip("Optional. Routes throw SFX here; if empty, uses GameAudio.SfxOutputGroup (settings SFX bus).")]
    [SerializeField] AudioMixerGroup throwSfxOutput;

    float _nextThrowTime;

    /// <summary>True when a lure prefab is assigned (throw is possible when off cooldown and allowed to move).</summary>
    public bool HasLurePrefab => lurePrefab != null;

    public float CooldownDuration => cooldownSeconds;

    /// <summary>Unscaled seconds until the next throw is allowed.</summary>
    public float CooldownRemainingUnscaled => Mathf.Max(0f, _nextThrowTime - Time.unscaledTime);

    void Update()
    {
        if (Time.timeScale <= 0f)
            return;
        if (lurePrefab == null)
            return;

        if (PlayerControls.Instance == null || !PlayerControls.Instance.interactPressed)
            return;
        if (Time.unscaledTime < _nextThrowTime)
            return;

        var pm = GetComponent<PlayerMovement>();
        if (pm != null && !pm.canMove)
            return;

        Vector2 dir = GetThrowDirection(pm);
        Vector3 spawnPos = transform.position + (Vector3)(dir * spawnOffset);
        var go = Instantiate(lurePrefab, spawnPos, Quaternion.identity);
        if (go.TryGetComponent<Rigidbody2D>(out var rb))
            rb.linearVelocity = dir * throwSpeed;

        PlayRandomThrowSfx(spawnPos);

        _nextThrowTime = Time.unscaledTime + cooldownSeconds;
    }

    static bool HasAnyClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return false;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null) return true;
        }
        return false;
    }

    void PlayRandomThrowSfx(Vector3 worldPos)
    {
        if (!HasAnyClip(throwSfx)) return;
        int count = 0;
        for (int i = 0; i < throwSfx.Length; i++)
        {
            if (throwSfx[i] != null) count++;
        }
        int pick = Random.Range(0, count);
        for (int i = 0; i < throwSfx.Length; i++)
        {
            if (throwSfx[i] == null) continue;
            if (pick-- == 0)
            {
                GameAudio.PlaySfx(throwSfx[i], worldPos, throwSfxVolume, throwSfxOutput);
                return;
            }
        }
    }

    static Vector2 GetThrowDirection(PlayerMovement pm)
    {
        if (pm != null)
        {
            if (pm.moveX != 0f || pm.moveY != 0f)
                return new Vector2(pm.moveX, pm.moveY).normalized;
            // Match PlayerMovement facing / roll: local +Y is "straight ahead" (ApplyRotation uses Atan2 - 90°).
            return ((Vector2)pm.transform.up).normalized;
        }
        return Vector2.up;
    }
}
