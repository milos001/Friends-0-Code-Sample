using System.Threading.Tasks;
using UnityEngine;

public class PulseController : AbilityController
{
    [SerializeField]
    private Collider _collider;

    private int _repetitionCount, _counter;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
    }

    public void Initialize(float delay, float repeatRate, int repetitionCount)
    {
        _repetitionCount = repetitionCount;

        InvokeRepeating(nameof(Pulse), delay, repeatRate);
    }

    private async void Pulse()
    {
        _collider.enabled = true;

        await Task.Delay(100);

        _collider.enabled = false;
        _counter++;
        if (_counter >= _repetitionCount)
        {
            CancelInvoke();
            Destroy(gameObject);
        }
    }

    protected override void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnter(other);
        hitATarget = false;
    }
}
