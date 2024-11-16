using UnityEngine;

public class PlaySoundEffect : Effect
{
    [SerializeField]
    private AudioSource _source;

    public override void OnHitHandler(Collider collider, string collisionIdentifier)
    {
        GameManager.Instance.SoundManager.PlaySound(_source);
    }
}
