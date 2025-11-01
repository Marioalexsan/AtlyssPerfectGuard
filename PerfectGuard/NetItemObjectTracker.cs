using Mirror;

namespace Marioalexsan.PerfectGuard;

public class NetItemObjectTracker : NetworkBehaviour
{
    public static readonly List<Net_ItemObject> Items = [];

    private Net_ItemObject? _itemObject;

    public void Awake()
    {
        _itemObject = GetComponent<Net_ItemObject>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (_itemObject != null && !Items.Contains(_itemObject))
            Items.Add(_itemObject);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (_itemObject != null)
            Items.Remove(_itemObject);
    }
}