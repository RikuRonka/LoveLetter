using Mirror;
using UnityEngine;

public class PlayerActions : NetworkBehaviour
{
    public static PlayerActions Local;

    void Awake() { if (isLocalPlayer) Local = this; }
    public override void OnStartLocalPlayer() { Local = this; }

    // existing:
    public void PlayCard(CardType card, uint targetNetId = 0, CardType guardGuess = 0)
    {
        if (!isLocalPlayer) return;
        GameController.Instance.CmdPlayCard(netIdentity.netId, card, targetNetId, guardGuess);
    }

    // NEW: Chancellor keep one
    public void ChooseChancellor(CardType keep)
    {
        if (!isLocalPlayer) return;
        GameController.Instance.CmdChancellorKeep(netIdentity.netId, keep);
    }
}
