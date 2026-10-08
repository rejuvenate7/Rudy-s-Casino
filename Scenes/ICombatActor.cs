using UnityEngine;


public interface ICombatActor
{
    Transform ActorTransform { get; }
    CombatHealth Health { get; }
    SpriteRenderer Renderer { get; }
    int FacingDirection { get; }
    bool CanUseAttacks { get; }
    Vector3 GetAttackPosition();
    void PlaySound(AudioClip clip, float volume);
}
