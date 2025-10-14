using Mirror;
using UnityEngine;

public class PlayerActions : NetworkBehaviour
{
    public static PlayerActions Local;
    public uint MyNetId => netId;
    void Awake() { if (isLocalPlayer) Local = this; }
    public override void OnStartLocalPlayer() { Local = this; }

    public void PlayCard(CardType card, uint targetNetId = 0, CardType guardGuess = 0)
    {
        if (!isLocalPlayer) return;
        CmdPlayCard(netIdentity.netId, card, targetNetId, guardGuess);
    }

    [Command]
    void CmdPlayCard(uint actorNetId, CardType card, uint targetNetId, CardType guardGuess)
    {
        GameController.Instance.CmdPlayCard(actorNetId, card, targetNetId, guardGuess);
    }

    [Command]
    public void ChooseChancellor(CardType keep)
    {
        GameController.Instance.CmdChancellorKeep(keep, connectionToClient);
    }

    [Command]
    public void ChooseGuard(uint targetNetId, CardType guess)
    {
        GameController.Instance.CmdGuardGuess(MyNetId, targetNetId, guess, connectionToClient);
    }
    [Command]
    public void ChoosePriest(uint targetNetId)
    {
        GameController.Instance.CmdPriestTarget(MyNetId, targetNetId, connectionToClient);
    }
    [Command]
    public void ChooseBaron(uint targetNetId)
    {
        GameController.Instance.CmdBaronTarget(targetNetId, connectionToClient);
    }
    [Command]
    public void ChooseKing(uint targetNetId)
    {
        GameController.Instance.CmdKingTarget(targetNetId, connectionToClient);
    }
}
