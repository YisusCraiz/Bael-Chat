namespace Bael;

internal sealed record PlayingCard(int Rank, string Suit)
{
    public string Label => (Rank switch { 11 => "J", 12 => "Q", 13 => "K", 14 => "A", _ => Rank.ToString() }) + Suit;
}

internal sealed class GameSession
{
    internal int Balance { get; private set; } = 1000;
    internal int Stake { get; private set; }
    internal int Rounds { get; private set; }
    internal bool Active { get; private set; }
    internal PlayingCard[] Hand { get; private set; } = [];
    internal static PlayingCard[] House => [new(10, "♥"), new(11, "♥"), new(12, "♥"), new(13, "♥"), new(14, "♥")];
    private Queue<PlayingCard> deck = new();

    internal bool Begin(int stake)
    {
        if (Active || stake is not (10 or 50 or 100) || stake > Balance) return false;
        Stake = stake; Balance -= stake; Active = true; Hand = []; return true;
    }
    internal int RevealCup(int chosen)
    {
        if (!Active || chosen is < 0 or > 2) throw new InvalidOperationException("Ronda de vasos inválida.");
        int ball = (chosen + Random.Shared.Next(1, 3)) % 3;
        Active = false; Rounds++; return ball;
    }
    internal void DealPoker()
    {
        if (!Active) throw new InvalidOperationException("Primero inicia una ronda.");
        // Espectáculo anunciado: la escalera real de corazones se reserva a BAEL.
        var house = House.ToHashSet();
        deck = new Queue<PlayingCard>(new[] { "♥", "♦", "♣", "♠" }
            .SelectMany(suit => Enumerable.Range(2, 13).Select(rank => new PlayingCard(rank, suit)))
            .Where(card => !house.Contains(card)).OrderBy(_ => Random.Shared.Next()));
        Hand = Enumerable.Range(0, 5).Select(_ => deck.Dequeue()).ToArray();
        // Si el jugador recibe otra escalera real, cambia el as para mantener el guion anunciado.
        EnsureBelowRoyal();
    }
    internal string FinishPoker(int[] replace)
    {
        if (!Active || Hand.Length != 5 || replace.Length > 3 || replace.Distinct().Count() != replace.Length
            || replace.Any(index => index is < 0 or > 4)) throw new InvalidOperationException("Elige hasta tres cartas.");
        foreach (int index in replace) Hand[index] = deck.Dequeue();
        EnsureBelowRoyal();
        Active = false; Rounds++; return HandName(Hand);
    }
    private void EnsureBelowRoyal()
    {
        if (HandName(Hand) == "Escalera real") Hand[Array.FindIndex(Hand, c => c.Rank == 14)] = deck.Dequeue();
    }
    internal bool Refill()
    {
        if (Active || Balance != 0) return false;
        Balance = 1000; return true;
    }
    internal static string HandName(PlayingCard[] cards)
    {
        int[] ranks = cards.Select(c => c.Rank).Order().ToArray();
        bool flush = cards.Select(c => c.Suit).Distinct().Count() == 1;
        bool straight = ranks.Distinct().Count() == 5 && (ranks[4] - ranks[0] == 4 || ranks.SequenceEqual(new[] { 2, 3, 4, 5, 14 }));
        int[] groups = cards.GroupBy(c => c.Rank).Select(g => g.Count()).OrderDescending().ToArray();
        if (flush && straight) return ranks[0] == 10 ? "Escalera real" : "Escalera de color";
        if (groups[0] == 4) return "Póker";
        if (groups.SequenceEqual(new[] { 3, 2 })) return "Full";
        if (flush) return "Color";
        if (straight) return "Escalera";
        if (groups[0] == 3) return "Trío";
        if (groups.Count(g => g == 2) == 2) return "Doble pareja";
        return groups[0] == 2 ? "Pareja" : "Carta alta";
    }
    internal static void Verify()
    {
        var cups = new GameSession();
        for (int i = 0; i < 10; i++)
        {
            if (!cups.Begin(100) || cups.Begin(10)) throw new InvalidOperationException("Se duplicó una apuesta.");
            if (cups.RevealCup(i % 3) == i % 3) throw new InvalidOperationException("Se perdió el guion de vasos.");
        }
        if (cups.Balance != 0 || cups.Begin(10) || !cups.Refill() || cups.Refill())
            throw new InvalidOperationException("Saldo ficticio incorrecto.");
        for (int i = 0; i < 100; i++)
        {
            var game = new GameSession(); game.Begin(10); game.DealPoker(); game.FinishPoker([0, 2, 4]);
            if (game.Hand.Distinct().Count() != 5 || game.Hand.Intersect(House).Any() || HandName(game.Hand) == "Escalera real")
                throw new InvalidOperationException("La baraja del espectáculo es inválida.");
        }
        if (HandName(House) != "Escalera real" || HandName([new(14,"♣"), new(2,"♣"),new(3,"♣"),new(4,"♣"),new(5,"♣")]) != "Escalera de color")
            throw new InvalidOperationException("El evaluador de cartas falló.");
    }
}
