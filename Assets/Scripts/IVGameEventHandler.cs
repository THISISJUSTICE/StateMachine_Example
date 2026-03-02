using UnityEngine;
using UnityEngine.Events;

public class IVGameEventHandler : MonoBehaviour
{
    [SerializeField] private UnityEvent _onRaisedEvent;

    public virtual void RaiseEvent()
    { 
        _onRaisedEvent?.Invoke();
    }
}
