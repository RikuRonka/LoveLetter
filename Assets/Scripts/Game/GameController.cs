using Mirror;
using System;
using System.Collections;
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
    int PointsToWin() => sPlayers.Count switch { 2 => 7, 3 => 5, 4 => 4, _ => 4 };
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
    readonly HashSet<uint> spyPlayedThisRound = new();
    readonly Dictionary<uint, List<CardType>> chancellorPending = new();

    int currentIndex;
    int burnedCount;
    bool roundActive;
    int firstPlayerIndexThisMatch = -1;
    readonly List<uint> matchRoster = new();
    bool rosterLocked = false;
    readonly HashSet<string> takenNames = new(System.StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<uint, string> nameByNetId = new();
    public override void OnStartServer()
    {
        base.OnStartServer();
        StartCoroutine(BootstrapRoundWhenPlayersReady());
    }

    [Server]
    public string GetUniqueName(string requested)
    {
        string baseName = Sanitize(requested);
        if (string.IsNullOrEmpty(baseName)) baseName = "Player";

        if (!takenNames.Contains(baseName))
        {
            takenNames.Add(baseName);
            return baseName;
        }

        int n = 2;
        string candidate;
        do { candidate = $"{baseName} ({n++})"; }
        while (takenNames.Contains(candidate));

        takenNames.Add(candidate);
        return candidate;
    }


    [Server]
    public string ReserveUniqueName(uint netId, string requested)
    {
        string baseName = Sanitize(requested);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "Player";

        // If this netId already had a name, release it first (rename case)
        if (nameByNetId.TryGetValue(netId, out var oldName))
        {
            // don't remove if it's the same (case-insensitive) — nothing to change
            if (!string.Equals(oldName, baseName, StringComparison.OrdinalIgnoreCase))
            {
                takenNames.Remove(oldName);
                nameByNetId.Remove(netId);
            }
            else
            {
                // keep current name
                return oldName;
            }
        }

        // Find unique candidate
        if (!takenNames.Contains(baseName))
        {
            takenNames.Add(baseName);
            nameByNetId[netId] = baseName;
            return baseName;
        }

        int n = 2;
        string candidate;
        do { candidate = $"{baseName} ({n++})"; }
        while (takenNames.Contains(candidate));

        takenNames.Add(candidate);
        nameByNetId[netId] = candidate;
        return candidate;
    }

    [Server]
    public void ReleaseNameFor(uint netId)
    {
        if (nameByNetId.TryGetValue(netId, out var owned))
        {
            takenNames.Remove(owned);
            nameByNetId.Remove(netId);
        }
    }

    public static string Sanitize(string s)
    {
        s = string.IsNullOrWhiteSpace(s) ? "Player" : s.Trim();
        if (s.Length > 20) s = s[..20];
        return s;
    }

    IEnumerator BootstrapRoundWhenPlayersReady()
    {
        yield return new WaitUntil(() =>
            NetworkServer.active &&
            NetworkServer.connections.Count >= 2 &&
            NetworkServer.connections.Values.All(c => c?.identity)
        );

        // wait until every connected identity has a non-empty PlayerName
        yield return new WaitUntil(() =>
            NetworkServer.connections.Values
                .Where(c => c?.identity)
                .Select(c => c.identity.GetComponent<PlayerNetwork>())
                .All(pn => pn != null && !string.IsNullOrWhiteSpace(pn.PlayerName))
        );

        BuildPlayersFromConnections();
        StartNewRound();
    }

    public void BuildPlayersFromConnections()
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
                Name = pn && !string.IsNullOrWhiteSpace(pn.PlayerName)
                        ? pn.PlayerName
                        : $"Player {conn.connectionId}",
                Eliminated = false,
                Protected = false,
                Score = 0
            });

            if (sPlayers.Count == 4) break;
        }
    }

    [Server]
    public void ServerRebroadcastStateNames()
    {
        BroadcastState();
    }

    void StartNewRound()
    {

        if (sPlayers.Count == 0)
        {
            Debug.LogWarning("[SRV] StartNewRound called but no players yet.");
            return;
        }

        if (!rosterLocked)
        {
            matchRoster.Clear();
            matchRoster.AddRange(sPlayers.Select(p => p.NetId));
            rosterLocked = true;
        }

        roundActive = true;
        spyPlayedThisRound.Clear();
        chancellorPending.Clear();
        deck.Clear();

        foreach (var kv in CardDB.Count)
            for (int i = 0; i < kv.Value; i++) deck.Add(kv.Key);
        Shuffle(deck);

        if (firstPlayerIndexThisMatch < 0)
            firstPlayerIndexThisMatch = UnityEngine.Random.Range(0, sPlayers.Count);
        else
            firstPlayerIndexThisMatch = (firstPlayerIndexThisMatch + 1) % sPlayers.Count;
        currentIndex = firstPlayerIndexThisMatch;

        foreach (var p in sPlayers)
        {
            p.Eliminated = false;
            p.Protected = false;
            p.Hand.Clear();
            p.Discards.Clear();
            PushHandTo(p);
        }

        burnedCount = (sPlayers.Count == 2) ? 3 : 1;
        for (int i = 0; i < burnedCount && deck.Count > 0; i++)
            deck.RemoveAt(0);

        foreach (var p in sPlayers) DealTo(p, 1);

        if (currentIndex < 0 || currentIndex >= sPlayers.Count)
            currentIndex = 0;
        RpcLog($"New round. {NameOf(sPlayers[currentIndex])} starts.");
        BroadcastState();
        StartTurn();
    }

    void StartTurn()
    {
        if (!roundActive) return;
        if (sPlayers.All(p => p.Eliminated)) { EndRoundBySurvivorOrHighest(); return; }

        while (sPlayers[currentIndex].Eliminated)
            currentIndex = (currentIndex + 1) % sPlayers.Count;

        sPlayers[currentIndex].Protected = false;

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

    static bool MustPlayCountess(List<CardType> hand)
    {
        return hand.Contains(CardType.Countess) &&
               (hand.Contains(CardType.King) || hand.Contains(CardType.Prince));
    }

    void EndTurnAdvance()
    {
        currentIndex = (currentIndex + 1) % sPlayers.Count;
        StartTurn();
    }

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
                Name = NameOf(p),
                Eliminated = p.Eliminated,
                Protected = p.Protected,
                Discards = new List<CardType>(p.Discards),
                Score = p.Score
            }).ToList()
        };

#if UNITY_SERVER || UNITY_EDITOR
        Debug.Log("[SRV] Names: " + string.Join(", ", sPlayers.Select(p => $"{p.NetId}:{NameOf(p)}")));
#endif

        RpcState(ps);
    }

    [ClientRpc] void RpcState(PublicState s) => BoardUI.Instance?.RenderState(s);
    [ClientRpc] void RpcLog(string msg) { BoardUI.Instance?.Log(msg); }

    [TargetRpc]
    void TargetGiveCard(NetworkConnection target, CardType c)
        => HandUI.Instance?.AddCard(c);

    [TargetRpc]
    void TargetYourTurn(NetworkConnection target, bool mustPlayCountess)
        => HandUI.Instance?.BeginTurn(mustPlayCountess);

    [TargetRpc]
    void TargetShowPeek(NetworkConnection target, string targetPlayer, CardType card)
        => HandUI.Instance?.ShowPriestPeek(targetPlayer, card);

    [TargetRpc]
    void TargetChancellorChoice(NetworkConnection target, CardType[] options)
        => ChancellorPrompt.Show(options, keep => PlayerActions.Local?.ChooseChancellor(keep));

    [TargetRpc]
    void TargetReplaceHand(NetworkConnection target, List<CardType> hand)
        => HandUI.Instance?.ReplaceHand(hand);

    [Server]
    public void CmdPlayCard(uint actorNetId, CardType card, uint targetNetId, CardType guardGuess)
    {

        if (!roundActive) return;

        int actorIdx = sPlayers.FindIndex(p => p.NetId == actorNetId);
        if (actorIdx < 0 || actorIdx != currentIndex) return;

        var actor = sPlayers[actorIdx];
        if (actor.Eliminated) return;

        if (!actor.Hand.Contains(card)) return;

        if (card != CardType.Countess && MustPlayCountess(actor.Hand)) return;

        actor.Hand.Remove(card);
        actor.Discards.Add(card);
        PushHandTo(actor);
        if (card == CardType.Spy)
            spyPlayedThisRound.Add(actor.NetId);

        switch (card)
        {
            case CardType.Guard:
            {
                var valid = ValidTargets(actor, allowSelf: false, requireNotProtected: true);
                if (valid.Count == 0)
                {
                    RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Guard)}, but no valid targets.");
                    BroadcastState(); TryEndOfRound(); if (roundActive) EndTurnAdvance();
                    return;
                }

                uint[] ids = valid.Select(p => p.NetId).ToArray();
                string[] names = valid.Select(p => NameOf(p)).ToArray();

                TargetGuardPrompt(actor.Conn, ids, names);
                RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Guard)}. Choose a target and guess.");
                return;
            }
            case CardType.Priest:
            {
                var valid = ValidTargets(actor, allowSelf: false, requireNotProtected: true);
                if (valid.Count == 0)
                {
                    RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Priest)}, but there are no valid targets.");
                    BroadcastState(); TryEndOfRound(); if (roundActive) EndTurnAdvance();
                    return;
                }
                if (valid.Count == 1)
                {
                    var t = valid[0];
                    TargetShowPeek(actor.Conn, NameOf(t), t.Hand.FirstOrDefault());
                    RpcLog($"{NameOf(actor)} looked at {NameOf(t)}'s hand.");
                    BroadcastState(); TryEndOfRound(); if (roundActive) EndTurnAdvance();
                    return;
                }

                uint[] ids = valid.Select(p => p.NetId).ToArray();
                string[] names = valid.Select(p => NameOf(p)).ToArray();
                TargetPriestPrompt(actor.Conn, ids);
                RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Priest)}. Choose a player to peek.");
                return;
            }
            case CardType.Baron:
            {
                var valid = ValidTargets(actor, allowSelf: false, requireNotProtected: true);
                if (valid.Count == 0)
                {
                    RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Baron)}, but there are no valid targets.");
                    BroadcastState(); TryEndOfRound(); if (roundActive) EndTurnAdvance();
                    return;
                }
                if (valid.Count == 1)
                {
                    CmdBaronTarget(valid[0].NetId, actor.Conn);
                    return;
                }

                uint[] ids = valid.Select(p => p.NetId).ToArray();
                string[] names = valid.Select(p => NameOf(p)).ToArray();
                TargetBaronPrompt(actor.Conn, ids);
                RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Baron)}. Choose a player to compare hands with.");
                return;
            }
            case CardType.Handmaid: ResolveHandmaid(actor); break;
            case CardType.Prince:
            {
                var valid = ValidTargets(actor, allowSelf: true, requireNotProtected: false)
                            .Where(p => p != null)
                            .ToList();

                if (valid.Count == 0)
                {
                    RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Prince)}, but there are no valid targets.");
                    BroadcastState(); TryEndOfRound(); if (roundActive) EndTurnAdvance();
                    return;
                }
                if (valid.Count == 1)
                {
                    ResolvePrince(actor, valid[0].NetId);
                    BroadcastState(); TryEndOfRound(); if (roundActive) EndTurnAdvance();
                    return;
                }

                uint[] ids = valid.Select(p => p.NetId).ToArray();
                string[] names = valid.Select(p => NameOf(p)).ToArray();
                TargetPrincePrompt(actor.Conn, ids);
                RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Prince)}. Choose a player to discard and draw.");
                return;
            }
            case CardType.King:
            {
                var valid = ValidTargets(actor, allowSelf: false, requireNotProtected: true);
                if (valid.Count == 0)
                {
                    RpcLog($"{NameOf(actor)} played {CardLabel(CardType.King)}, but there are no valid targets.");
                    BroadcastState(); TryEndOfRound(); if (roundActive) EndTurnAdvance();
                    return;
                }
                if (valid.Count == 1)
                {
                    CmdKingTarget(valid[0].NetId, actor.Conn);
                    return;
                }

                uint[] ids = valid.Select(p => p.NetId).ToArray();
                string[] names = valid.Select(p => NameOf(p)).ToArray();
                TargetKingPrompt(actor.Conn, ids);
                RpcLog($"{NameOf(actor)} played {CardLabel(CardType.King)}. Choose a player to trade hands with.");
                return;
            }
            case CardType.Countess: RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Countess)}."); break;
            case CardType.Princess: ResolvePrincess(actor); break;
            case CardType.Chancellor:
            ResolveChancellor(actor);
            BroadcastState();
            return;
            default: RpcLog($"{NameOf(actor)} played {card}."); break;
        }

        BroadcastState();
        TryEndOfRound();
        if (roundActive) EndTurnAdvance();
    }

    string TargetInvalidReason(SPlayer actor, uint targetNetId, bool allowSelf, bool requireNotProtected, out SPlayer target)
    {
        target = sPlayers.FirstOrDefault(p => p.NetId == targetNetId);
        if (target == null) return "the chosen player is no longer available.";
        if (target.Eliminated) return $"{NameOf(target)} is already eliminated.";
        if (!allowSelf && target == actor) return "you can’t target yourself with this card.";
        if (requireNotProtected && target != actor && target.Protected) return $"{NameOf(target)} is protected by {CardLabel(CardType.Handmaid)}.";
        return null;
    }

    List<SPlayer> ValidTargets(SPlayer actor, bool allowSelf, bool requireNotProtected)
    {
        return sPlayers
            .Where(p =>
                p != null &&
                !p.Eliminated &&
                (allowSelf || p != actor) &&
                (!requireNotProtected || !p.Protected))
            .ToList();
    }

    static readonly string[] PlayerPalette =
    {
        "#4CC9F0",
        "#F72585",
        "#E9C46A",
        "#2A9D8F",
        "#F77F00",
        "#9B5DE5",
        "#43AA8B",
        "#577590", 
    };

    static string PlayerColor(uint netId)
    {
        int i = (int)(netId % (uint)PlayerPalette.Length);
        return PlayerPalette[i];
    }

    string P(SPlayer p) => $"<b><color={PlayerColor(p.NetId)}>{NameOf(p)}</color></b>";
    static string CardLabel(CardType c) => $"<i><color=#FFD166>{CardDB.Title[c]}</color></i>";
    [Server]
    public void CmdPrinceTarget(uint targetNetId, NetworkConnectionToClient sender)
    {
        if (!roundActive) return;

        var actor = sPlayers.FirstOrDefault(p => p.Conn == sender);
        if (actor == null || actor.Eliminated)
            return;

        var reason = TargetInvalidReason(actor, targetNetId, allowSelf: true, requireNotProtected: false, out var target);
        if (target == null || target.Eliminated)
        {
            RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Prince)}, but {reason}");
            Finish();
            return;
        }

        if (target != actor && target.Protected)
        {
            RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Guard)} on {NameOf(target)}, but they are protected by Handmaid.");
            Finish();
            return;
        }

        ResolvePrince(actor, targetNetId);

        Finish();
        return;

        void Finish()
        {
            BroadcastState();
            TryEndOfRound();
            if (roundActive) EndTurnAdvance();
        }
    }


    [Server]
    public void CmdKingTarget(uint targetNetId, NetworkConnectionToClient sender)
    {   
        if (!roundActive) return;

        var actor = sPlayers.FirstOrDefault(p => p.Conn == sender);
        if (actor == null || actor.Eliminated) { Debug.LogWarning("[SRV] King: actor not found"); return; }

        var target = GetValidTarget(targetNetId, requireNotProtected: true, allowSelf: false);
        if (target == null)
        {
            RpcLog($"{NameOf(actor)} played {CardLabel(CardType.King)}, invalid target.");
            Finish();
            return;
        }

        var tmp = new List<CardType>(actor.Hand);
        actor.Hand.Clear(); actor.Hand.AddRange(target.Hand);
        target.Hand.Clear(); target.Hand.AddRange(tmp);

        PushHandTo(actor);
        PushHandTo(target);

        RpcLog($"{NameOf(actor)} traded hands with {NameOf(target)}.");

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
    void TargetGuardPrompt(NetworkConnection target, uint[] targetIds, string[] targetNames)
    {
        TargetPrompt.ShowTargetsAndGuessesViaIds(targetIds, CardDB.AllExceptGuard,
       (chosenTarget, chosenGuess) => PlayerActions.Local?.ChooseGuard(chosenTarget, chosenGuess));
    }

    [TargetRpc]
    void TargetPriestPrompt(NetworkConnection target, uint[] targetIds)
    {
        TargetPrompt.ShowTargetsViaIds(targetIds,
       chosenTarget => PlayerActions.Local?.ChoosePriest(chosenTarget));
    }

    [TargetRpc]
    void TargetBaronPrompt(NetworkConnection target, uint[] targetIds)
    {
        TargetPrompt.ShowTargetsViaIds(targetIds,
       chosenTarget => PlayerActions.Local?.ChooseBaron(chosenTarget));
    }

    [TargetRpc]
    void TargetKingPrompt(NetworkConnection target, uint[] targetIds)
    {
        TargetPrompt.ShowTargetsViaIds(targetIds,
            chosenTarget => PlayerActions.Local?.ChooseKing(chosenTarget));
    }

    [TargetRpc]
    void TargetPrincePrompt(NetworkConnection target, uint[] targetIds)
    {
        TargetPrompt.ShowTargetsViaIds(targetIds,
            chosenTarget => PlayerActions.Local?.ChoosePrince(chosenTarget));
    }

    string NameOf(SPlayer p)
    {
        var live = p.Identity ? p.Identity.GetComponent<PlayerNetwork>()?.PlayerName : null;
        return string.IsNullOrWhiteSpace(live) ? p.Name : live;
    }

    [Server]
    public void CmdBaronTarget(uint targetNetId, NetworkConnectionToClient sender)
    {
        if (!roundActive) return;

        var actor = sPlayers.FirstOrDefault(p => p.Conn == sender);
        if (actor == null) { Debug.LogWarning("[SRV] Baron: actor not found for sender"); return; }

        var reason = TargetInvalidReason(actor, targetNetId, allowSelf: false, requireNotProtected: true, out var target);
        if (reason != null || target == null)
        {
            RpcLog($"{NameOf(actor)} played Baron, but {reason ?? "the chosen player is no longer available."}");
            StartCoroutine(FinishAfter(0.1f));
            return;
        }

        var a = actor.Hand.FirstOrDefault();
        var b = target.Hand.FirstOrDefault();
        int va = CardDB.Value[a], vb = CardDB.Value[b];

        var aName = NameOf(actor);
        var bName = NameOf(target);

        if (va == vb)
        {
            TargetBaronCompare(actor.Conn, aName, a, bName, b, "Tie — nobody is eliminated.");
            TargetBaronCompare(target.Conn, aName, a, bName, b, "Tie — nobody is eliminated.");
            RpcLog($"{aName} compared with {bName}: tie ({a} vs {b}).");

            StartCoroutine(FinishAfter(5.0f));
            return;
        }

        var loser = (va < vb) ? actor : target;
        var winner = (loser == actor) ? target : actor;
        var wName = NameOf(winner);
        var lName = NameOf(loser);

        TargetBaronCompare(actor.Conn, aName, a, bName, b, $"{wName} wins — {lName} is eliminated!");
        TargetBaronCompare(target.Conn, aName, a, bName, b, $"{wName} wins — {lName} is eliminated!");

        Eliminate(loser);
        PushHandTo(loser);
        RpcLog($"{aName} compared with {bName}: {(va < vb ? "loses" : "wins")} ({a} vs {b}).");

        StartCoroutine(FinishAfter(5.0f)); 
    }


    [TargetRpc]
    void TargetBaronCompare(NetworkConnection target,
                        string aName, CardType aCard,
                        string bName, CardType bCard,
                        string resultText)
    {
        ComparePrompt.Show(aName, aCard, bName, bCard, resultText);
    }

    [Server]
    public void CmdPriestTarget(uint actorNetId, uint targetNetId, NetworkConnectionToClient sender)
    {
        if (!roundActive) return;

        var actor = sPlayers.FirstOrDefault(p => p.NetId == actorNetId);
        if (actor == null || actor.Eliminated) return;

        var reason = TargetInvalidReason(actor, targetNetId, allowSelf: true, requireNotProtected: false, out var target);

        if (target == null)
        {
            RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Priest)}, but {reason}");
            EndAfter();
            return;
        }

        TargetShowPeek(actor.Conn, NameOf(target), target.Hand.FirstOrDefault());
        RpcLog($"{NameOf(actor)} looked at {NameOf(target)}'s hand.");

        EndAfter();

        void EndAfter()
        {
            BroadcastState();
            TryEndOfRound();
            if (roundActive) EndTurnAdvance();
        }
    }


    [Server]
    public void CmdGuardGuess(uint actorNetId, uint targetNetId, CardType guess, NetworkConnectionToClient sender)
    {
        if (!roundActive) return;

        var actor = sPlayers.FirstOrDefault(p => p.NetId == actorNetId);
        if (actor == null || actor.Eliminated) return;

        var target = GetValidTarget(targetNetId, requireNotProtected: true, allowSelf: false);
        if (target == null) { RpcLog($"{NameOf(actor)} Guard guess: invalid target."); EndAfter(); return; }

        if (guess == CardType.None || guess == CardType.Guard)
        {
            RpcLog($"{CardLabel(CardType.Guard)} must guess a non-Guard card.");
            EndAfter();
            return;
        }

        var targetCard = target.Hand.FirstOrDefault();
        if (targetCard == guess)
        {
            RpcLog($"{NameOf(actor)} guessed {CardLabel(guess)} — {NameOf(target)} eliminated!");
            Eliminate(target);
        }
        else
        {
            RpcLog($"{NameOf(actor)} guessed {CardLabel(guess)} — wrong.");
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

        var actor = sPlayers.FirstOrDefault(p => p.Conn == sender);
        if (actor == null || actor.Eliminated) return;

        if (!chancellorPending.TryGetValue(actor.NetId, out var options))
        {
            Debug.LogWarning($"[SRV] CmdChancellorKeep: no pending options for {actor?.NetId}");
            BroadcastState();
            TryEndOfRound();
            if (roundActive) EndTurnAdvance();
            return;
        }

        if (!options.Contains(keep)) keep = options[0];

        actor.Hand.Clear();
        actor.Hand.Add(keep);
        TargetReplaceHand(actor.Conn, new List<CardType>(actor.Hand));

        foreach (var c in options)
            if (!EqualityComparer<CardType>.Default.Equals(c, keep))
                deck.Add(c);

        chancellorPending.Remove(actor.NetId);
        RpcLog($"{NameOf(actor)} kept {keep} (Chancellor).");

        BroadcastState();
        TryEndOfRound();
        if (roundActive) EndTurnAdvance();
    }

    // ===== Resolvers =====


    // Priest — peek target’s hand (private)
    void ResolvePriest(SPlayer actor, uint targetNetId)
    {
        var reason = TargetInvalidReason(actor, targetNetId, allowSelf: true, requireNotProtected: false, out var target);

        if (target == null) { RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Priest)}, but {reason}."); return; }

        TargetShowPeek(actor.Conn, NameOf(target), target.Hand.FirstOrDefault());
        RpcLog($"{NameOf(actor)} looked at {NameOf(target)}'s hand.");
    }

    // Baron — compare hands; lower value eliminated (tie = none)
    void ResolveBaron(SPlayer actor, uint targetNetId)
    {
        var reason = TargetInvalidReason(actor, targetNetId, allowSelf: true, requireNotProtected: false, out var target);

        if (target == null) { RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Baron)}, but {reason}."); return; }

        var a = actor.Hand.FirstOrDefault();
        var b = target.Hand.FirstOrDefault();
        int va = CardDB.Value[a];
        int vb = CardDB.Value[b];

        if (va == vb) { RpcLog($"{NameOf(actor)} and {NameOf(target)} tied ({a} vs {b}). Nobody is eliminated."); return; }

        var loser = (va < vb) ? actor : target;
        RpcLog($"{NameOf(actor)} compares with {NameOf(target)}: {(va < vb ? "loses" : "wins")} ({a} vs {b}).");
        Eliminate(loser);
    }

    void ResolveHandmaid(SPlayer actor)
    {
        actor.Protected = true;
        RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Handmaid)} and is protected until their next turn.");
    }

    void ResolvePrince(SPlayer actor, uint targetNetId)
    {
        var reason = TargetInvalidReason(actor, targetNetId, allowSelf: true, requireNotProtected: false, out var target);

        if (target == null) { RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Prince)}, but {reason}"); return; }

        var discarded = target.Hand.FirstOrDefault();
        target.Hand.Clear();
        target.Discards.Add(discarded);
        PushHandTo(target);
        if (discarded == CardType.Spy) spyPlayedThisRound.Add(target.NetId);

        if (discarded == CardType.Princess)
        {
            RpcLog($"{NameOf(actor)} forced {NameOf(target)} to discard {CardLabel(CardType.Princess)} — eliminated!");
            Eliminate(target);
        }
        else
        {
            RpcLog($"{NameOf(actor)} forced {NameOf(target)} to discard {discarded}.");
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

    void ResolveKing(SPlayer actor, uint targetNetId)
    {
        var target = GetValidTarget(targetNetId, requireNotProtected: true, allowSelf: false);
        if (target == null) { RpcLog($"{NameOf(actor)} played {CardLabel(CardType.King)}, invalid target."); return; }

        var temp = new List<CardType>(actor.Hand);
        actor.Hand.Clear(); actor.Hand.AddRange(target.Hand);
        target.Hand.Clear(); target.Hand.AddRange(temp);

        TargetReplaceHand(actor.Conn, new List<CardType>(actor.Hand));
        TargetReplaceHand(target.Conn, new List<CardType>(target.Hand));

        RpcLog($"{NameOf(actor)} traded hands with {NameOf(target)}.");
    }

    void ResolvePrincess(SPlayer actor)
    {
        Eliminate(actor);
        RpcLog($"{NameOf(actor)} discarded Princess and is eliminated!");
    }

    void ResolveChancellor(SPlayer actor)
    {
        var options = new List<CardType>(actor.Hand);
        for (int i = 0; i < 2 && deck.Count > 0; i++)
        {
            var c = deck[0]; deck.RemoveAt(0);
            options.Add(c);
        }
        if (options.Count <= 1) { RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Chancellor)} (no choices)."); return; }

        chancellorPending[actor.NetId] = options;

        TargetChancellorChoice(actor.Conn, options.ToArray());
        RpcLog($"{NameOf(actor)} played {CardLabel(CardType.Chancellor)} and drew {options.Count - 1}.");
    }

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
        if (spyPlayedThisRound.Count == 1)
        {
            uint spyNetId = spyPlayedThisRound.First();
            var spyPlayer = sPlayers.FirstOrDefault(p => p.NetId == spyNetId);
            if (spyPlayer != null)
            {
                spyPlayer.Score++;
                RpcLog($"{NameOf(spyPlayer)} gains +1 token for Spy!");
            }
        }

        var alive = sPlayers.Where(p => !p.Eliminated).ToList();

        List<SPlayer> winners = new();

        if (alive.Count == 1)
        {
            winners.Add(alive[0]);
        }
        else
        {
            int maxHand = alive.Where(p => p.Hand.Count > 0)
                               .Select(p => CardDB.Value[p.Hand[0]])
                               .DefaultIfEmpty(-1).Max();
            var tied = alive.Where(p => p.Hand.Count > 0 && CardDB.Value[p.Hand[0]] == maxHand).ToList();

            if (tied.Count <= 1)
            {
                if (tied.Count == 1) winners.Add(tied[0]);
            }
            else
            {
                static int BestDiscardSum(SPlayer sp) => sp.Discards.Sum(c => CardDB.Value[c]);

                int bestSum = tied.Select(BestDiscardSum).Max();
                winners = tied.Where(p => BestDiscardSum(p) == bestSum).ToList();

            }
        }

        // Award tokens & log
        if (winners.Count > 0)
        {
            foreach (var w in winners)
            {
                w.Score++;
            }

            if (winners.Count == 1)
                RpcLog($"{NameOf(winners[0])} wins the round!");
            else
                RpcLog($"{string.Join(", ", winners.Select(NameOf))} tie — all gain a token!");
        }
        else
        {
            RpcLog("No winner could be determined.");
        }

        roundActive = false;

        var rows = sPlayers
            .Select(p => new SummaryRow
            {
                NetId = p.NetId,
                Name = NameOf(p),
                Score = p.Score
            })
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Name)
            .ToArray();

        int target = PointsToWin();
        bool isMatchOver = sPlayers.Any(p => p.Score >= target);

        uint winnerNetIdForTitle = winners
            .OrderBy(p => NameOf(p))
            .Select(p => p.NetId)
            .FirstOrDefault();

        RpcShowRoundSummary(rows, winnerNetIdForTitle, target, isMatchOver);

        BroadcastState();

    }

    [ClientRpc]
    void RpcCloseRoundSummary()
    {
        RoundSummaryUI.Instance?.Hide();
    }

    [Serializable]
    public struct SummaryRow
    {
        public uint NetId;
        public string Name;
        public int Score;
    }

    [ClientRpc]
    void RpcShowRoundSummary(SummaryRow[] rows, uint winnerNetId, int pointsToWin, bool isMatchOver)
    {
        RoundSummaryUI.Instance?.Show(rows, winnerNetId, pointsToWin, isMatchOver);
    }



    [Server]
    public void ServerStartNextRound()
    {
        StopAllCoroutines();
        RpcCloseRoundSummary();

        if (roundActive) return;

        StartNewRound();
    }

    IEnumerator FinishAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);

        BroadcastState();
        TryEndOfRound();
        if (roundActive) EndTurnAdvance();
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }
}
