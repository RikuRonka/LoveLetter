using Mirror;
using UnityEngine;

public class PlayerActions : NetworkBehaviour
{
    public static PlayerActions Local;
    public uint MyNetId => netId;
    void Awake() { if (isLocalPlayer) Local = this; }
    public override void OnStartLocalPlayer() { Local = this; }

    // existing:
    public void PlayCard(CardType card, uint targetNetId = 0, CardType guardGuess = 0)
    {
        if (!isLocalPlayer) return;
        GameController.Instance.CmdPlayCard(netIdentity.netId, card, targetNetId, guardGuess);
    }

    public void ChooseChancellor(CardType keep)
    {
        GameController.Instance.CmdChancellorKeep(keep);
    }


    public void ChooseGuard(uint targetNetId, CardType guess)
    {
        GameController.Instance.CmdGuardGuess(MyNetId, targetNetId, guess);
    }

    public void ChoosePriest(uint targetNetId)
    {
        GameController.Instance.CmdPriestTarget(netId, targetNetId);
    }

    public void ChooseBaron(uint targetNetId)
    {
        GameController.Instance.CmdBaronTarget(targetNetId);
    }

}
