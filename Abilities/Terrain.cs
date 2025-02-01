using System.Collections;
using UnityEngine;

public class Terrain : Ability
{
    [SerializeField]
    private float _scaleUpDelay, _scaleUpDuration;
    [SerializeField]
    private Vector3 _scaleUpDirection;
    [SerializeField]
    private GameObject _terrainVisual;
    [SerializeField]
    private Collider[] _terrainChildColliders;
    [SerializeField]
    private Rigidbody[] _terrainChildRbs;

    public override void Initialize(AbilityController abilityController, float lateBy)
    {
        base.Initialize(abilityController, lateBy);

        controller.ObjectDestroyed += OnObjectDestroyed;

        StartCoroutine(SizeUpTerrain(_scaleUpDelay, _scaleUpDuration, _scaleUpDirection));
    }

    private IEnumerator SizeUpTerrain(float delay, float duration, Vector3 direction)
    {
        BoxCollider collider = this.collider as BoxCollider;
        Vector3 initialSize = collider.size;
        Vector3 initialCenter = collider.center;
        Vector3 currentSize = Vector3.zero, currentCenter = Vector3.zero;

        collider.size = currentSize;
        collider.center = currentCenter;

        yield return new WaitForSeconds(delay);
        telegraphEffect.SetActive(false);
        float time = 0f;
        
        Vector3 scale = _terrainVisual.transform.localScale;
        float scaleIncrement = 1f / duration * .02f;
        Vector3 sizeIncrement = initialSize / duration * .02f;
        Vector3 centerIncrement = initialCenter / duration * .02f;

        while (time < duration)
        {
            scale += direction.normalized * scaleIncrement;
            currentSize += sizeIncrement;
            currentCenter += centerIncrement;
            time += 0.02f;

            _terrainVisual.transform.localScale = scale;
            collider.size = currentSize;
            collider.center = currentCenter;

            yield return new WaitForFixedUpdate();
        }

        collider.isTrigger = false;
    }

    private void OnObjectDestroyed()
    {
        collider.enabled = false;

        foreach (var collider in _terrainChildColliders)
        {
            collider.enabled = true;
            collider.transform.parent = null;
            Destroy(collider.gameObject, 10f);
        }

        foreach (var rb in _terrainChildRbs)
        {
            rb.isKinematic = false;
        }

        Destroy(gameObject);
    }

}
