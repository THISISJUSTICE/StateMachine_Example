using System.Collections;
using UnityEngine;

public class IVDelayLogger : IVCoroutineProvider
{
    [SerializeField] private string _text;
    [SerializeField, Range(0f, 10f)] private float _delay;

    private WaitForSeconds _wait;

    private void Awake()
    {
        _wait = new WaitForSeconds(_delay);
    }

    public IEnumerator LogText()
    {
        yield return _wait;
        Debug.Log($"Log: {_text}");
    }

    public void LogName()
    {
        Debug.Log($"Log: Logger({gameObject.name})");
    }
}