using Mirror;
using UnityEngine;

public class PlayerActions : NetworkBehaviour
{
    public static PlayerActions Local;

    void Start() { if (isLocalPlayer) Local = this; }

    public void PlayCard(CardType card, uint targetNetId = 0, CardType? guess = null)
    {
        if (!isLocalPlayer) return;
        CmdPlay(card, targetNetId, guess.HasValue ? guess.Value : (CardType)0);
    }

    [Command]
    void CmdPlay(CardType card, uint targetNetId, CardType guess)
    {   // Route to the server authority
        GameController.Instance?.CmdPlayCard(netIdentity.netId, card, targetNetId, guess);
    }
}
