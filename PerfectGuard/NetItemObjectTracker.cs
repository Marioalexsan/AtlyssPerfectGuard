using Mirror;

namespace Marioalexsan.PerfectGuard;

public class NetItemObjectTracker : NetworkBehaviour
{
    public static readonly List<Net_ItemObject> Items = [];

    private Net_ItemObject _itemObject = null!;

    public void Awake()
    {
        _itemObject = GetComponent<Net_ItemObject>();
        Items.Add(_itemObject);
    }

    public void OnDestroy()
    {
        Items.Remove(_itemObject);
    }
}