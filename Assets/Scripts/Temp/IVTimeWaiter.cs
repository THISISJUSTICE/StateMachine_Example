using UnityEngine;
using System.Collections;

public class IVTimeWaiter : IVCoroutineProvider
{
    [SerializeField, Range(0f, 10f)] private float _delay;

    private WaitForSeconds _wait;

    private void Awake()
    {
        _wait = new WaitForSeconds(_delay);
    }

    public IEnumerator Wait()
    {
        yield return _wait;
    }
}