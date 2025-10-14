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
    public static GameController Instance { get; private set; }
    void Awake() => Instance = this;
    readonly HashSet<uint> waitingChancellor = new();

    // ===== Server-only structures =====
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

    // state
    readonly List<SPlayer> sPlayers = new();
    readonly List<CardType> deck = new();
    readonly HashSet<uint> spyPlayedThisRound = new();            // for Spy bonus
    readonly Dictionary<uint, List<CardType>> chancellorPending = new(); // actorNetId -> 3-card options

    int currentIndex;
    int burnedCount;
    bool roundActive;
    int firstPlayerIndexThisMatch = -1;

    // ===== Server lifecycle =====
    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(BootstrapRoundWhenPlayersReady());
    }

    private System.Collections.IEnumerator BootstrapRoundWhenPlayersReady()
    {
        // wait until at least one PlayerNetwork has spawned & has an identity
        while (true)
        {
            BuildPlayersFromConnections();
            if (sPlayers.Count >= 2) break;
            yield return null;
        }

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
                // store a fallback; we'll use the live SyncVar when broadcasting
                Name = pn && !string.IsNullOrWhiteSpace(pn.PlayerName) ? pn.PlayerName : $"Player {conn.connectionId}",
                Eliminated = false,
                Protected = false,
                Score = 0
            });
        }
    }

    // ===== Round/start turn =====
    void StartNewRound()
    {

        if (sPlayers.Count == 0)
        {
            Debug.LogWarning("[SRV] StartNewRound called but no players yet.");
            return;
        }
        roundActive = true;
        spyPlayedThisRound.Clear();
        chancellorPending.Clear();
        deck.Clear();

        // Build deck from DB (handles Spy/Chancellor/anything you add there)
        foreach (var kv in CardDB.Count)
            for (int i = 0; i < kv.Value; i++) deck.Add(kv.Key);
        Shuffle(deck);

        // rotate starting player each round (random for the first round)
        if (firstPlayerIndexThisMatch < 0)
            firstPlayerIndexThisMatch = UnityEngine.Random.Range(0, sPlayers.Count);
        else
            firstPlayerIndexThisMatch = (firstPlayerIndexThisMatch + 1) % sPlayers.Count;
        currentIndex = firstPlayerIndexThisMatch;

        // reset players
        foreach (var p in sPlayers)
        {
            p.Eliminated = false;
            p.Protected = false;
            p.Hand.Clear();
            p.Discards.Clear();
        }

        // burn rule
        burnedCount = (sPlayers.Count == 2) ? 3 : 1;
        for (int i = 0; i < burnedCount && deck.Count > 0; i++)
            deck.RemoveAt(0);

        // deal 1 to each
        foreach (var p in sPlayers) DealTo(p, 1);

        if (currentIndex < 0 || currentIndex >= sPlayers.Count)
            currentIndex = 0;
        RpcLog($"New round. {sPlayers[currentIndex].Name} starts.");
        BroadcastState();
        StartTurn();
    }

    void StartTurn()
    {
        if (!roundActive) return;
        if (sPlayers.All(p => p.Eliminated)) { EndRoundBySurvivorOrHighest(); return; }

        // advance to next alive player if needed
        while (sPlayers[currentIndex].Eliminated)
            currentIndex = (currentIndex + 1) % sPlayers.Count;

        // handmaid wears off at start of your turn
        sPlayers[currentIndex].Protected = false;

        // draw a card if deck not empty
        var actor = sPlayers[currentIndex];
        if (deck.Count > 0)
        {
            var c = deck[0]; deck.RemoveAt(0);
            actor.Hand.Add(c);
            TargetGiveCard(actor.Conn, c);
        }

        bool mustCountess = MustPlayCountess(actor.Hand);
        TargetYourTurn(actor.Conn, mustCountess);
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

    // ===== Networking: public state & messages =====
    void BroadcastState()
    {
        var ps = new PublicState
        {
            CurrentIndex = currentIndex,
            DeckCount = deck.Count,
            BurnedCount = burnedCount,
            Players = sPlayers.Select(p =>
            {
                // prefer the synced name from the component
                var liveName = p.Identity ? p.Identity.GetComponent<PlayerNetwork>()?.PlayerName : null;
                return new PublicPlayer
                {
                    NetId = p.NetId,
                    Name = string.IsNullOrWhiteSpace(liveName) ? p.Name : liveName,
                    Eliminated = p.Eliminated,
                    Protected = p.Protected,
                    Discards = new List<CardType>(p.Discards),
                    Score = p.Score
                };
            }).ToList()
        };
        RpcState(ps);
    }

    [TargetRpc]
    void TargetFreezeUntilChoice(NetworkConnection target)
    {
        HandUI.Instance?.EndTurn();   // a small helper on the client (see below)
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

    // Chancellor: present 3 options to the actor (their other card + 2 drawn)
    [TargetRpc]
    void TargetChancellorChoice(NetworkConnection target, CardType[] options)
        => ChancellorPrompt.Show(options, keep => PlayerActions.Local?.ChooseChancellor(keep)); // you provide this UI

    [TargetRpc]
    void TargetReplaceHand(NetworkConnection target, List<CardType> hand)
        => HandUI.Instance?.ReplaceHand(hand);

    // ===== Commands from clients =====
    [Server]
    public void CmdPlayCard(uint actorNetId, CardType card, uint targetNetId, CardType guardGuess)
    {

        if (!roundActive) return;

        int actorIdx = sPlayers.FindIndex(p => p.NetId == actorNetId);
        if (actorIdx < 0 || actorIdx != currentIndex) return;

        var actor = sPlayers[actorIdx];
        if (actor.Eliminated) return;

        if (!actor.Hand.Contains(card)) return;

        // Enforce Countess rule
        if (card != CardType.Countess && MustPlayCountess(actor.Hand)) return;

        // play: move from hand into discards (face-up)
        actor.Hand.Remove(card);
        actor.Discards.Add(card);
        PushHandTo(actor);
        if (card == CardType.Spy)
            spyPlayedThisRound.Add(actor.NetId);

        switch (card)
        {
            case CardType.Guard:
            {
                var valid = sPlayers.Where(p => !p.Eliminated && !p.Protected && p != actor).ToList();
                if (valid.Count == 0)
                {
                    RpcLog($"{actor.Name} played Guard, but no valid targets.");
                    BroadcastState();
                    TryEndOfRound();
                    if (roundActive) EndTurnAdvance();
                    return;
                }

                uint[] ids = valid.Select(p => p.NetId).ToArray();
                string[] names = valid.Select(p => p.Name).ToArray();

                TargetGuardPrompt(actor.Conn, ids, names);   // <— ONE TargetRpc
                RpcLog($"{actor.Name} played Guard. Choose a target and guess.");
                return; // IMPORTANT: wait for client response
            }
            case CardType.Priest:
            {
                // after you already did: actor.Hand.Remove(card); actor.Discards.Add(card); PushHandTo(actor);
                var valid = sPlayers
                    .Where(p => !p.Eliminated && !p.Protected && p != actor)
                    .ToList();

                if (valid.Count == 0)
                {
                    RpcLog($"{actor.Name} played Priest, but there are no valid targets.");
                    BroadcastState();
                    TryEndOfRound();
                    if (roundActive) EndTurnAdvance();
                    return;
                }

                uint[] ids = valid.Select(p => p.NetId).ToArray();
                string[] names = valid.Select(p => p.Name).ToArray();

                TargetPriestPrompt(actor.Conn, ids, names);
                RpcLog($"{actor.Name} played Priest. Choose a player to peek.");
                return; // IMPORTANT: wait for client choice
            }
            case CardType.Baron:
            {
                var valid = sPlayers
                    .Where(p => !p.Eliminated && !p.Protected && p != actor)
                    .ToList();

                if (valid.Count == 0)
                {
                    RpcLog($"{actor.Name} played Baron, but there are no valid targets.");
                    BroadcastState();
                    TryEndOfRound();
                    if (roundActive) EndTurnAdvance();
                    return;
                }

                uint[] ids = valid.Select(p => p.NetId).ToArray();
                string[] names = valid.Select(p => p.Name).ToArray();

                TargetBaronPrompt(actor.Conn, ids, names);
                RpcLog($"{actor.Name} played Baron. Choose a player to compare hands with.");
                return; // IMPORTANT: wait for client response
            }
            case CardType.Handmaid: ResolveHandmaid(actor); break;
            case CardType.Prince: ResolvePrince(actor, targetNetId); break;
            case CardType.King:
            {
                // after you already did:
                // actor.Hand.Remove(card); actor.Discards.Add(card); PushHandTo(actor);

                var valid = sPlayers
                    .Where(p => !p.Eliminated && !p.Protected && p != actor)
                    .ToList();

                if (valid.Count == 0)
                {
                    RpcLog($"{actor.Name} played King, but there are no valid targets.");
                    BroadcastState();
                    TryEndOfRound();
                    if (roundActive) EndTurnAdvance();
                    return;
                }

                uint[] ids = valid.Select(p => p.NetId).ToArray();
                string[] names = valid.Select(p => p.Name).ToArray();

                TargetKingPrompt(actor.Conn, ids, names);
                RpcLog($"{actor.Name} played King. Choose a player to trade hands with.");
                return; // IMPORTANT: wait for client choice
            }
            case CardType.Countess: RpcLog($"{actor.Name} played Countess."); break;
            case CardType.Princess: ResolvePrincess(actor); break;
            case CardType.Chancellor:
            ResolveChancellor(actor);
            BroadcastState();
            return;
            default: RpcLog($"{actor.Name} played {card}."); break;
        }

        BroadcastState();
        TryEndOfRound();
        if (roundActive) EndTurnAdvance();
    }

    [TargetRpc]
    void TargetKingPrompt(NetworkConnection target, uint[] targetIds, string[] targetNames)
    {
        TargetPrompt.ShowTargets(targetIds, targetNames, chosenTarget =>
        {
            PlayerActions.Local?.ChooseKing(chosenTarget);
        });
    }
        
    [Server]
    public void CmdKingTarget(uint targetNetId, NetworkConnectionToClient sender = null)
    {
        if (!roundActive) return;

        // who sent this?
        var actor = sPlayers.FirstOrDefault(p => p.Conn == sender);
        if (actor == null || actor.Eliminated) { Debug.LogWarning("[SRV] King: actor not found"); return; }

        // validate target (not protected, not self, alive)
        var target = GetValidTarget(targetNetId, requireNotProtected: true, allowSelf: false);
        if (target == null)
        {
            RpcLog($"{actor.Name} played King, invalid target.");
            Finish();
            return;
        }

        // swap lists
        var tmp = new List<CardType>(actor.Hand);
        actor.Hand.Clear(); actor.Hand.AddRange(target.Hand);
        target.Hand.Clear(); target.Hand.AddRange(tmp);

        // push hands to both clients
        PushHandTo(actor);
        PushHandTo(target);

        RpcLog($"{actor.Name} traded hands with {target.Name}.");

        Finish();
        return;

        void Finish()
        {
            BroadcastState();
            TryEndOfRound();
            if (roundActive) EndTurnAdvance();
        }
    }

    [TargetRpc]
    void TargetPriestPrompt(NetworkConnection target, uint[] targetIds, string[] targetNames)
    {
        TargetPrompt.ShowTargets(targetIds, targetNames, chosenTarget =>
        {
            PlayerActions.Local?.ChoosePriest(chosenTarget);
        });
    }

    [TargetRpc]
    void TargetBaronPrompt(NetworkConnection target, uint[] targetIds, string[] targetNames)
    {
        Debug.Log($"[Client] TargetBaronPrompt ids={targetIds.Length}");
        TargetPrompt.ShowTargets(targetIds, targetNames, chosenTarget =>
        {
            Debug.Log($"[Client] Baron chosen target={chosenTarget}");
            PlayerActions.Local?.ChooseBaron(chosenTarget);
        });
    }

    [Server]
    public void CmdBaronTarget(uint targetNetId, NetworkConnectionToClient sender)
    {
        if (!roundActive) return;

        // find actor from the SENDER (not connectionToClient)
        var actor = sPlayers.FirstOrDefault(p => p.Conn == sender);
        if (actor == null) { Debug.LogWarning("[SRV] Baron: actor not found for sender"); return; }

        var target = GetValidTarget(targetNetId, requireNotProtected: true, allowSelf: false);
        if (target == null)
        {
            RpcLog($"{actor.Name} played Baron, invalid target.");
            Finish();
            return;
        }

        var a = actor.Hand.FirstOrDefault();
        var b = target.Hand.FirstOrDefault();
        int va = CardDB.Value[a], vb = CardDB.Value[b];

        if (va == vb)
        {
            TargetBaronCompare(actor.Conn, actor.Name, a, target.Name, b, "Tie — nobody is eliminated.");
            TargetBaronCompare(target.Conn, actor.Name, a, target.Name, b, "Tie — nobody is eliminated.");
            RpcLog($"{actor.Name} compared with {target.Name}: tie ({a} vs {b}).");
        }
        else
        {
            var loser = (va < vb) ? actor : target;
            var winner = (loser == actor) ? target : actor;

            TargetBaronCompare(actor.Conn, actor.Name, a, target.Name, b, $"{winner.Name} wins — {loser.Name} is eliminated!");
            TargetBaronCompare(target.Conn, actor.Name, a, target.Name, b, $"{winner.Name} wins — {loser.Name} is eliminated!");

            Eliminate(loser);
            PushHandTo(loser); // keep hands in sync if not already
            RpcLog($"{actor.Name} compared with {target.Name}: {(va < vb ? "loses" : "wins")} ({a} vs {b}).");
        }

        Finish();
        return;

        void Finish()
        {
            BroadcastState();
            TryEndOfRound();
            if (roundActive) EndTurnAdvance();
        }
    }


    [TargetRpc]
    void TargetBaronCompare(NetworkConnection target,
                        string aName, CardType aCard,
                        string bName, CardType bCard,
                        string resultText)
    {
        Debug.Log("[Client] TargetBaronCompare modal");
        ComparePrompt.Show(aName, aCard, bName, bCard, resultText);
    }

    [Server]
    public void CmdPriestTarget(uint actorNetId, uint targetNetId, NetworkConnectionToClient sender)
    {
        if (!roundActive) return;

        var actor = sPlayers.FirstOrDefault(p => p.NetId == actorNetId);
        if (actor == null || actor.Eliminated) return;

        var target = GetValidTarget(targetNetId, requireNotProtected: true, allowSelf: false);
        if (target == null)
        {
            RpcLog($"{actor?.Name} played Priest, invalid target.");
            EndAfter();
            return;
        }

        // Send the private peek to the actor only
        TargetShowPeek(actor.Conn, target.Name, target.Hand.FirstOrDefault());
        RpcLog($"{actor.Name} looked at {target.Name}'s hand.");

        EndAfter();

        void EndAfter()
        {
            BroadcastState();
            TryEndOfRound();
            if (roundActive) EndTurnAdvance();
        }
    }
    List<PublicPlayer> BuildPublicTargets(SPlayer actor)
    {
        return sPlayers
            .Where(p => !p.Eliminated && !p.Protected && p != actor)
            .Select(p => new PublicPlayer
            {
                NetId = p.NetId,
                Name = p.Name,
                Eliminated = p.Eliminated,
                Protected = p.Protected,
                Discards = new List<CardType>(p.Discards),
                Score = p.Score
            })
            .ToList();
    }

    [TargetRpc]
    void TargetGuardPrompt(NetworkConnection target, uint[] targetIds, string[] targetNames)
    {
        var guessOptions = CardDB.AllExceptGuard;
        TargetPrompt.ShowTargetsAndGuesses(targetIds, targetNames, guessOptions, (chosenTarget, chosenGuess) =>
        {
            PlayerActions.Local?.ChooseGuard(chosenTarget, chosenGuess);
        });
    }


    [Server]
    public void CmdGuardGuess(uint actorNetId, uint targetNetId, CardType guess, NetworkConnectionToClient sender)
    {
        if (!roundActive) return;

        var actor = sPlayers.FirstOrDefault(p => p.NetId == actorNetId);
        if (actor == null || actor.Eliminated) return;

        var target = GetValidTarget(targetNetId, requireNotProtected: true, allowSelf: false);
        if (target == null) { RpcLog($"{actor.Name} Guard guess: invalid target."); EndAfter(); return; }

        if (guess == 0 || guess == CardType.Guard) { RpcLog("Guard must guess a non-Guard card."); EndAfter(); return; }

        var targetCard = target.Hand.FirstOrDefault();
        if (targetCard == guess)
        {
            RpcLog($"{actor.Name} guessed {guess} — {target.Name} eliminated!");
            Eliminate(target);
        }
        else
        {
            RpcLog($"{actor.Name} guessed {guess} — wrong.");
        }

        EndAfter();

        void EndAfter()
        {
            BroadcastState();
            TryEndOfRound();
            if (roundActive) EndTurnAdvance();
        }
    }

    [Server]
    public void CmdChancellorKeep(CardType keep, NetworkConnectionToClient sender)
    {
        if (!roundActive) return;

        // find the actor from the calling connection
        var actor = sPlayers.FirstOrDefault(p => p.Conn == sender);
        if (actor == null || actor.Eliminated) return;

        if (!chancellorPending.TryGetValue(actor.NetId, out var options))
        {
            Debug.LogWarning($"[SRV] CmdChancellorKeep: no pending options for {actor?.NetId}");
            // fail-safe: don’t leave the game stuck
            BroadcastState();
            TryEndOfRound();
            if (roundActive) EndTurnAdvance();
            return;
        }

        if (!options.Contains(keep)) keep = options[0]; // validate

        // apply result
        actor.Hand.Clear();
        actor.Hand.Add(keep);
        TargetReplaceHand(actor.Conn, new List<CardType>(actor.Hand));

        // others to bottom
        foreach (var c in options)
            if (!EqualityComparer<CardType>.Default.Equals(c, keep))
                deck.Add(c);

        chancellorPending.Remove(actor.NetId);
        RpcLog($"{actor.Name} kept {keep} (Chancellor).");

        BroadcastState();
        TryEndOfRound();
        if (roundActive) EndTurnAdvance();   // <- ADVANCE NOW
    }

    // ===== Resolvers =====


    // Priest — peek target’s hand (private)
    void ResolvePriest(SPlayer actor, uint targetNetId)
    {
        var target = GetValidTarget(targetNetId, requireNotProtected: true, allowSelf: false);
        if (target == null) { RpcLog($"{actor.Name} played Priest, invalid target."); return; }

        TargetShowPeek(actor.Conn, target.Name, target.Hand.FirstOrDefault());
        RpcLog($"{actor.Name} looked at {target.Name}'s hand.");
    }

    // Baron — compare hands; lower value eliminated (tie = none)
    void ResolveBaron(SPlayer actor, uint targetNetId)
    {
        var target = GetValidTarget(targetNetId, requireNotProtected: true, allowSelf: false);
        if (target == null) { RpcLog($"{actor.Name} played Baron, invalid target."); return; }

        var a = actor.Hand.FirstOrDefault();
        var b = target.Hand.FirstOrDefault();
        int va = CardDB.Value[a];
        int vb = CardDB.Value[b];

        if (va == vb) { RpcLog($"{actor.Name} and {target.Name} tied ({a} vs {b}). Nobody is eliminated."); return; }

        var loser = (va < vb) ? actor : target;
        RpcLog($"{actor.Name} compares with {target.Name}: {(va < vb ? "loses" : "wins")} ({a} vs {b}).");
        Eliminate(loser);
    }

    // Handmaid — protection until your next turn (already cleared at StartTurn)
    void ResolveHandmaid(SPlayer actor)
    {
        actor.Protected = true;
        RpcLog($"{actor.Name} is protected until their next turn.");
    }

    // Prince — target (including self) discards and draws a new card; if Princess, eliminated
    void ResolvePrince(SPlayer actor, uint targetNetId)
    {
        var target = GetValidTarget(targetNetId, requireNotProtected: true, allowSelf: true);
        if (target == null) { RpcLog($"{actor.Name} played Prince, invalid target."); return; }

        var discarded = target.Hand.FirstOrDefault();
        // move to discards (public)
        target.Hand.Clear();
        target.Discards.Add(discarded);
        PushHandTo(target);
        if (discarded == CardType.Spy) spyPlayedThisRound.Add(target.NetId);

        if (discarded == CardType.Princess)
        {
            RpcLog($"{actor.Name} forced {target.Name} to discard Princess — eliminated!");
            Eliminate(target);
        }
        else
        {
            RpcLog($"{actor.Name} forced {target.Name} to discard {discarded}.");
            // draw replacement if deck not empty
            if (deck.Count > 0 && !target.Eliminated)
            {
                var c = deck[0]; deck.RemoveAt(0);
                target.Hand.Add(c);
                TargetGiveCard(target.Conn, c);
            }
        }
    }

    void PushHandTo(SPlayer p)
    {
        TargetReplaceHand(p.Conn, new List<CardType>(p.Hand));
    }

    // King — swap hands with another player
    void ResolveKing(SPlayer actor, uint targetNetId)
    {
        var target = GetValidTarget(targetNetId, requireNotProtected: true, allowSelf: false);
        if (target == null) { RpcLog($"{actor.Name} played King, invalid target."); return; }

        var temp = new List<CardType>(actor.Hand);
        actor.Hand.Clear(); actor.Hand.AddRange(target.Hand);
        target.Hand.Clear(); target.Hand.AddRange(temp);

        TargetReplaceHand(actor.Conn, new List<CardType>(actor.Hand));
        TargetReplaceHand(target.Conn, new List<CardType>(target.Hand));

        RpcLog($"{actor.Name} traded hands with {target.Name}.");
    }

    // Princess — you discard Princess: eliminated immediately
    void ResolvePrincess(SPlayer actor)
    {
        Eliminate(actor);
        RpcLog($"{actor.Name} discarded Princess and is eliminated!");
    }

    // Chancellor — draw 2; keep 1; put the other 2 (from your hand) on bottom
    void ResolveChancellor(SPlayer actor)
    {
        var options = new List<CardType>(actor.Hand);
        for (int i = 0; i < 2 && deck.Count > 0; i++)
        {
            var c = deck[0]; deck.RemoveAt(0);
            options.Add(c);
        }
        if (options.Count <= 1) { RpcLog($"{actor.Name} played Chancellor (no choices)."); return; }

        // store options keyed by the actor
        chancellorPending[actor.NetId] = options;

        TargetChancellorChoice(actor.Conn, options.ToArray());
        RpcLog($"{actor.Name} played Chancellor and drew {options.Count - 1}.");
    }

    // ===== helpers =====
    void DealTo(SPlayer p, int count)
    {
        for (int i = 0; i < count && deck.Count > 0; i++)
        {
            var c = deck[0]; deck.RemoveAt(0);
            p.Hand.Add(c);
            TargetGiveCard(p.Conn, c);
        }
    }

    SPlayer GetValidTarget(uint netId, bool requireNotProtected, bool allowSelf)
    {
        var t = sPlayers.FirstOrDefault(p => p.NetId == netId);
        if (t == null || t.Eliminated) return null;
        if (requireNotProtected && t.Protected) return null;
        if (!allowSelf && t == sPlayers[currentIndex]) return null;
        return t;
    }

    void Eliminate(SPlayer p)
    {
        if (p.Eliminated) return;
        // reveal remaining hand on elimination
        foreach (var c in p.Hand)
        {
            p.Discards.Add(c);
            if (c == CardType.Spy) spyPlayedThisRound.Add(p.NetId);
        }
        p.Hand.Clear();
        p.Eliminated = true;
        PushHandTo(p);
    }

    void TryEndOfRound()
    {
        int alive = sPlayers.Count(x => !x.Eliminated);
        if (alive <= 1 || deck.Count == 0)
            EndRoundBySurvivorOrHighest();
    }

    void EndRoundBySurvivorOrHighest()
    {
        // Spy bonus: exactly one unique player used/discarded Spy this round = +1 token
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
        {
            // highest card value among hands (everyone should have 1)
            winner = sPlayers.Where(p => !p.Eliminated && p.Hand.Count > 0)
                             .OrderByDescending(p => CardDB.Value[p.Hand[0]])
                             .FirstOrDefault();
        }

        if (winner != null)
        {
            winner.Score++;
            RpcLog($"{winner.Name} wins the round!");
        }

        roundActive = false;
        BroadcastState();
        Invoke(nameof(StartNewRound), 2f);
    }

    // ===== utils =====
    static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }
}
