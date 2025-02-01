using System.Threading.Tasks;
using UnityEngine;

public class Pulse : Ability
{
    [SerializeField]
    private Collider _collider;

    private int _repetitionCount, _counter;

    public override void Initialize(AbilityController abilityController, float lateBy)
    {
        base.Initialize(abilityController, lateBy);
    }

    public void Initialize(float delay, float repeatRate, int repetitionCount)
    {
        _repetitionCount = repetitionCount;

        InvokeRepeating(nameof(CreatePulse), delay, repeatRate);
    }

    private async void CreatePulse()
    {
        _collider.enabled = true;

        await Task.Delay(100);

        _collider.enabled = false;
        _counter++;
        if (_counter >= _repetitionCount)
        {
            CancelInvoke();
            ReleaseObject();
        }
    }
}
