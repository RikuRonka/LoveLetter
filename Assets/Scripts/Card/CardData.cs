using System.Collections.Generic;
using UnityEngine;

// Keep enum names EXACTLY matching your Resources sprite names
public enum CardType { Spy = 0, Guard = 1, Priest = 2, Baron = 3, Handmaid = 4, Prince = 5, Chancellor = 6, King = 7, Countess = 8, Princess = 9 }

// One place for rules, counts, values, text, and sprites (client-side)
public static class CardDB
{
    public static readonly Dictionary<CardType, int> Count = new()
    {
        { CardType.Spy, 2 },
        { CardType.Guard, 5 },
        { CardType.Priest, 2 },
        { CardType.Baron, 2 },
        { CardType.Handmaid, 2 },
        { CardType.Prince, 2 },
        { CardType.Chancellor, 2 },
        { CardType.King, 1 },
        { CardType.Countess, 1 },
        { CardType.Princess, 1 },
    };

    // Card values for end-of-round compare (official)
    public static readonly Dictionary<CardType, int> Value = new()
    {
        { CardType.Spy, 0 },
        { CardType.Guard, 1 },
        { CardType.Priest, 2 },
        { CardType.Baron, 3 },
        { CardType.Handmaid, 4 },
        { CardType.Prince, 5 },
        { CardType.Chancellor, 5 },
        { CardType.King, 7 },
        { CardType.Countess, 8 },
        { CardType.Princess, 9 },
    };

    public static readonly Dictionary<CardType, string> Title = new()
    {
        { CardType.Spy, "Spy" },
        { CardType.Guard, "Guard" },
        { CardType.Priest, "Priest" },
        { CardType.Baron, "Baron" },
        { CardType.Handmaid, "Handmaid" },
        { CardType.Prince, "Prince" },
        { CardType.Chancellor, "Chancellor" },
        { CardType.King, "King" },
        { CardType.Countess, "Countess" },
        { CardType.Princess, "Princess" },
    };

    public static readonly Dictionary<CardType, string> Description = new()
    {
        { CardType.Spy, "End of round: if you are the ONLY player who played/discarded a Spy this round, gain 1 token." },
        { CardType.Guard, "Guess a non-Guard; if correct, target is eliminated." },
        { CardType.Priest, "Look at another player's hand." },
        { CardType.Baron, "Compare hands; lower value is eliminated." },
        { CardType.Handmaid, "You are protected until your next turn." },
        { CardType.Prince, "Choose a player to discard their hand and draw a new card." },
        { CardType.Chancellor, "Draw 2 cards. Keep 1 card and put your other 2 on the bottom of the deck in any order." },
        { CardType.King, "Trade hands with another player." },
        { CardType.Countess, "Must be played if with King or Prince; otherwise no effect." },
        { CardType.Princess, "If you discard this, you are eliminated." },
    };

    public static IEnumerable<CardType> All =>
    (CardType[])System.Enum.GetValues(typeof(CardType));


    // Client-side sprite cache (lazy)
    static readonly Dictionary<CardType, Sprite> _sprite = new();

    public static Sprite Sprite(CardType t)
    {
        if (_sprite.TryGetValue(t, out var s) && s != null) return s;
        // Resources/Cards/<EnumName>
        var loaded = Resources.Load<Sprite>($"Cards/{t}");
        _sprite[t] = loaded;
        return loaded;
    }
}
