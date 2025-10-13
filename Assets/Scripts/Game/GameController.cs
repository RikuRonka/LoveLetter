using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


[Serializable]
public class PublicPlayer
{
    public uint NetId;
    public string Name;
    public bool Eliminated;
    public bool Protected;
    public List<CardType> Discards = new();
    public int Score;
}

[Serializable]
public class PublicState
{
    public int CurrentIndex;
    public int DeckCount;
    public int BurnedCount;
    public List<PublicPlayer> Players = new();
}

public class GameController : NetworkBehaviour
{
    readonly HashSet<uint> spyPlayedThisRound = new();
    public static GameController Instance { get; private set; }
    void Awake() => Instance = this;

    class SPlayer
    {
        public NetworkConnectionToClient Conn;
        public NetworkIdentity Identity;
        public string Name;
        public bool Eliminated;
        public bool Protected;
        public readonly List<CardType> Hand = new();
        public readonly List<CardType> Discards = new();
        public int Score;
        public uint NetId => Identity ? Identity.netId : 0;
    }

    readonly List<SPlayer> sPlayers = new();
    readonly List<CardType> deck = new();
    int currentIndex;
    int burnedCount;
    bool roundActive;

    // -------- Server lifecycle --------
    public override void OnStartServer()
    {
        base.OnStartServer();
        BuildPlayersFromConnections();
        StartNewRound();
    }

    void BuildPlayersFromConnections()
    {
        sPlayers.Clear();
        foreach (var kv in NetworkServer.connections.OrderBy(k => k.Key))
        {
            var conn = kv.Value;
            if (!conn?.identity) continue;
            var pn = conn.identity.GetComponent<PlayerNetwork>();
            sPlayers.Add(new SPlayer
            {
                Conn = conn,
                Identity = conn.identity,
                Name = pn ? pn.PlayerName : $"Player {conn.connectionId}",
                Eliminated = false,
                Protected = false,
                Score = 0
            });
        }
    }

    void StartNewRound()
    {
        spyPlayedThisRound.Clear();
        roundActive = true;
        currentIndex = 0;
        burnedCount = 0;
        deck.Clear();

        Add(CardType.Guard, 5);
        Add(CardType.Priest, 2);
        Add(CardType.Baron, 2);
        Add(CardType.Handmaid, 2);
        Add(CardType.Prince, 2);
        Add(CardType.King, 1);
        Add(CardType.Countess, 1);
        Add(CardType.Princess, 1);
        Shuffle(deck);

        foreach (var p in sPlayers)
        {
            p.Eliminated = false; p.Protected = false;
            p.Hand.Clear(); p.Discards.Clear();
        }

        burnedCount = (sPlayers.Count == 2) ? 3 : 1;
        for (int i = 0; i < burnedCount && deck.Count > 0; i++) deck.RemoveAt(0);

        foreach (var p in sPlayers) DealTo(p, 1);

        BroadcastState();
        StartTurn();
    }

    void Add(CardType t, int n) { for (int i = 0; i < n; i++) deck.Add(t); }
    void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        { int r = UnityEngine.Random.Range(i, list.Count); (list[i], list[r]) = (list[r], list[i]); }
    }

    // -------- Turn loop --------
    void StartTurn()
    {
        if (!roundActive) return;

        if (sPlayers.All(p => p.Eliminated)) { EndRoundBySurvivorOrHighest(); return; }

        while (sPlayers[currentIndex].Eliminated)
            currentIndex = (currentIndex + 1) % sPlayers.Count;

        sPlayers[currentIndex].Protected = false; // Handmaid wears off at start of your turn

        if (deck.Count > 0)
        {
            var c = deck[0]; deck.RemoveAt(0);
            sPlayers[currentIndex].Hand.Add(c);
            TargetGiveCard(sPlayers[currentIndex].Conn, c);
        }

        bool mustCountess = MustPlayCountess(sPlayers[currentIndex].Hand);
        TargetYourTurn(sPlayers[currentIndex].Conn, mustCountess);
        BroadcastState();
    }

    bool MustPlayCountess(List<CardType> hand)
    {
        return hand.Contains(CardType.Countess) &&
               (hand.Contains(CardType.King) || hand.Contains(CardType.Prince));
    }

    void EndTurnAdvance()
    {
        currentIndex = (currentIndex + 1) % sPlayers.Count;
        StartTurn();
    }

    // -------- Public state to all clients --------
    void BroadcastState()
    {
        var ps = new PublicState
        {
            CurrentIndex = currentIndex,
            DeckCount = deck.Count,
            BurnedCount = burnedCount,
            Players = sPlayers.Select(p => new PublicPlayer
            {
                NetId = p.NetId,
                Name = p.Name,
                Eliminated = p.Eliminated,
                Protected = p.Protected,
                Discards = new List<CardType>(p.Discards),
                Score = p.Score
            }).ToList()
        };
        RpcState(ps);
    }

    [ClientRpc] void RpcState(PublicState s) => BoardUI.Instance?.RenderState(s);
    [ClientRpc] void RpcLog(string msg) { BoardUI.Instance?.Log(msg); Debug.Log(msg); }

    [TargetRpc]
    void TargetGiveCard(NetworkConnection target, CardType c)
        => HandUI.Instance?.AddCard(c);

    [TargetRpc]
    void TargetYourTurn(NetworkConnection target, bool mustPlayCountess)
        => HandUI.Instance?.BeginTurn(mustPlayCountess);

    [TargetRpc]
    void TargetShowPeek(NetworkConnection target, string targetPlayer, CardType card)
        => HandUI.Instance?.ShowPriestPeek(targetPlayer, card);

    // -------- Commands from clients --------
    [Command(requiresAuthority = false)]
    public void CmdPlayCard(uint actorNetId, CardType card, uint targetNetId, CardType guardGuess)
    {
        if (!roundActive) return;

        int actorIdx = sPlayers.FindIndex(p => p.NetId == actorNetId);
        if (actorIdx != currentIndex || actorIdx < 0) return;
        var actor = sPlayers[actorIdx];
        if (actor.Eliminated) return;
        if (!actor.Hand.Contains(card)) return;
        if (card != CardType.Countess && MustPlayCountess(actor.Hand)) return;

        // play it
        actor.Hand.Remove(card);
        actor.Discards.Add(card);

        if (card == CardType.Spy)
            spyPlayedThisRound.Add(actor.NetId);

        switch (card)
        {
            case CardType.Guard: ResolveGuard(actor, targetNetId, guardGuess); break;
            case CardType.Priest: ResolvePriest(actor, targetNetId); break;
            case CardType.Baron: ResolveBaron(actor, targetNetId); break;         // TODO Phase 2
            case CardType.Handmaid: ResolveHandmaid(actor); break;         // TODO
            case CardType.Prince: ResolvePrince(actor, targetNetId); break;         // TODO
            case CardType.King: ResolveKing(actor, targetNetId); break;         // TODO
            case CardType.Countess: RpcLog($"{actor.Name} played Countess."); break;
            case CardType.Princess: ResolvePrincess(actor); break;         // TODO
        }

        BroadcastState();
        TryEndOfRound();
        if (roundActive) EndTurnAdvance();
    }

    // -------- Resolvers (Guard + Priest fully, others are stubs to fill next) --------
    void ResolveGuard(SPlayer actor, uint targetNetId, CardType guess)
    {
        if (guess == 0 || guess == CardType.Guard) { RpcLog("Guard must guess a non-Guard card."); return; }
        var target = sPlayers.FirstOrDefault(p => p.NetId == targetNetId);
        if (target == null || target.Eliminated || target.Protected) { RpcLog($"{actor.Name} played Guard, invalid target."); return; }

        var targetCard = target.Hand.FirstOrDefault();
        if (targetCard == guess) { Eliminate(target); RpcLog($"{actor.Name} guessed {guess} — {target.Name} eliminated!"); }
        else RpcLog($"{actor.Name} guessed {guess} — wrong.");
    }

    void ResolvePriest(SPlayer actor, uint targetNetId)
    {
        var target = sPlayers.FirstOrDefault(p => p.NetId == targetNetId);
        if (target == null || target.Eliminated || target.Protected) { RpcLog($"{actor.Name} played Priest, invalid target."); return; }
        TargetShowPeek(actor.Conn, target.Name, target.Hand.FirstOrDefault());
        RpcLog($"{actor.Name} looked at {target.Name}'s hand.");
    }

    // ---- TODO next phases (you’ll fill these in) ----
    void ResolveBaron(SPlayer actor, uint targetNetId) { /* compare hands, lower eliminated; tie = none */ RpcLog($"{actor.Name} played Baron."); }
    void ResolveHandmaid(SPlayer actor) { actor.Protected = true; RpcLog($"{actor.Name} is protected until their next turn."); }
    void ResolvePrince(SPlayer actor, uint targetNetId) { RpcLog($"{actor.Name} played Prince."); /* target discards; if Princess -> eliminated; else draw replacement (TargetGiveCard) */ }
    void ResolveKing(SPlayer actor, uint targetNetId) { RpcLog($"{actor.Name} played King.");   /* swap hands between actor and target with TargetReplaceHand(...) */ }
    void ResolvePrincess(SPlayer actor) { Eliminate(actor); RpcLog($"{actor.Name} discarded Princess and is eliminated!"); }

    [TargetRpc]
    void TargetReplaceHand(NetworkConnection target, List<CardType> hand)
        => HandUI.Instance?.ReplaceHand(hand);

    // -------- helpers --------
    void DealTo(SPlayer p, int count)
    {
        for (int i = 0; i < count && deck.Count > 0; i++)
        { var c = deck[0]; deck.RemoveAt(0); p.Hand.Add(c); TargetGiveCard(p.Conn, c); }
    }

    void Eliminate(SPlayer p) { p.Eliminated = true; }

    void TryEndOfRound()
    {
        int alive = sPlayers.Count(x => !x.Eliminated);
        if (alive <= 1 || deck.Count == 0)
            EndRoundBySurvivorOrHighest();
    }

    void EndRoundBySurvivorOrHighest()
    {
        if (spyPlayedThisRound.Count == 1)
        {
            uint spyNetId = spyPlayedThisRound.First();
            var spyPlayer = sPlayers.FirstOrDefault(p => p.NetId == spyNetId);
            if (spyPlayer != null)
            {
                spyPlayer.Score++;
                RpcLog($"{spyPlayer.Name} gains +1 token for Spy!");
            }
        }

        var alive = sPlayers.Where(x => !x.Eliminated).ToList();
        SPlayer winner = null;

        if (alive.Count == 1) winner = alive[0];
        else
            winner = sPlayers.Where(p => !p.Eliminated && p.Hand.Count > 0)
                             .OrderByDescending(p => (int)p.Hand[0]).FirstOrDefault();

        if (winner != null) { winner.Score++; RpcLog($"{winner.Name} wins the round!"); }
        roundActive = false;
        Invoke(nameof(StartNewRound), 2f);
    }
}
