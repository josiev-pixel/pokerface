using System.Globalization;
using PokerEngine.Core;
using PokerEngine.Core.Equity;
using PokerEngine.Decision;
using PokerEngine.Solver;

// pokerface CLI — a headless tool to interrogate the engine: recommend an action for a spot,
// compute equity, and solve/benchmark the CFR validation game. Deterministic given --seed.

if (args.Length == 0)
{
    PrintUsage();
    return 0;
}

var opts = ParseOptions(args);
string command = args[0].ToLowerInvariant();

try
{
    switch (command)
    {
        case "decide": return Decide(opts);
        case "equity": return Equity(opts);
        case "kuhn": return Kuhn(opts);
        case "leduc": return Leduc(opts);
        case "demo": return Demo();
        case "help" or "-h" or "--help": PrintUsage(); return 0;
        default:
            Console.Error.WriteLine($"Unknown command '{command}'.");
            PrintUsage();
            return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

// ----------------------------------------------------------------- commands

int Decide(Dictionary<string, string> o)
{
    var spot = new Spot
    {
        Hero = HoleCards.Parse(Required(o, "hero")),
        Board = ParseCards(Get(o, "board", "")),
        Position = Get(o, "pos", "ip").StartsWith('o') ? Position.OutOfPosition : Position.InPosition,
        Pot = Int(o, "pot", 0),
        ToCall = Int(o, "tocall", 0),
        EffectiveStack = Int(o, "eff", 100),
        BigBlind = Int(o, "bb", 1),
    };

    OpponentModel? model = null;
    if (o.ContainsKey("foldtobet"))
        model = new OpponentModel { FoldToBet = Dbl(o, "foldtobet", 0.5), Confidence = Dbl(o, "confidence", 1.0) };

    var engine = new DecisionEngine();
    var rng = new DeterministicRandom((ulong)Int(o, "seed", 1));
    var result = engine.Decide(spot, rng, model);

    Console.WriteLine($"Spot: {spot.Hero} on {(spot.Board.Count == 0 ? "preflop" : Cards(spot.Board))}  "
        + $"({spot.StreetName}, {spot.Position}, pot {spot.Pot}, to-call {spot.ToCall}, eff {spot.EffectiveStack}, bb {spot.BigBlind})");
    Console.WriteLine($"Recommendation: {result.Move}{(result.Chips > 0 ? " " + result.Chips : "")}");
    Console.WriteLine($"  equity {result.Equity:P1}"
        + (result.RequiredEquity > 0 ? $", pot-odds need {result.RequiredEquity:P1}" : "")
        + (result.ExploitWeight > 0 ? $", exploit weight {result.ExploitWeight:0.##}" : ""));
    Console.WriteLine($"  strategy: {string.Join(", ", result.Strategy.Where(s => s.Probability > 0))}");
    Console.WriteLine($"  why: {result.Explanation}");
    return 0;
}

int Equity(Dictionary<string, string> o)
{
    var hero = ParseCards(Required(o, "hero"));
    var board = ParseCards(Get(o, "board", ""));
    var rng = new DeterministicRandom((ulong)Int(o, "seed", 1));
    int samples = Int(o, "samples", EquityCalculator.DefaultSamples);

    EquityResult r;
    if (o.ContainsKey("villain"))
        r = EquityCalculator.HeadsUp(hero, ParseCards(o["villain"]), board, rng, samples);
    else
        r = EquityCalculator.HeadsUpVsRandom(hero, board, rng, samples);

    string vs = o.ContainsKey("villain") ? Cards(ParseCards(o["villain"])) : "a random hand";
    Console.WriteLine($"{Cards(hero)} vs {vs}"
        + (board.Count > 0 ? $" on {Cards(board)}" : " preflop") + $": equity {r.Equity:P2}");
    Console.WriteLine($"  win {r.Win:P2}, tie {r.Tie:P2}, lose {r.Loss:P2}  ({(r.IsExact ? "exact enumeration" : $"Monte-Carlo n={r.Samples}")})");
    return 0;
}

int Kuhn(Dictionary<string, string> o)
{
    int iters = Int(o, "iters", 200_000);
    var solver = new CfrPlusSolver<KuhnState>(new KuhnPoker());
    solver.Run(iters);
    var s = solver.AverageStrategy();

    double jackBet = s["J"][1], kingBet = s["K"][1], queenCall = s["Qpb"][1];
    double alpha = KuhnEquilibrium.RecoverAlphaFromKingBet(kingBet);

    Console.WriteLine($"CFR+ on Kuhn poker — {iters:N0} iterations");
    Console.WriteLine($"Theoretical game value to player 0: {KuhnEquilibrium.ExpectedValuePlayer1():0.#####} (-1/18)");
    Console.WriteLine("Solved player-0 frequencies:");
    Console.WriteLine($"  bet Jack   {jackBet:0.000}  (theory α      = {alpha:0.000})");
    Console.WriteLine($"  bet King   {kingBet:0.000}  (theory 3α     = {KuhnEquilibrium.Player0KingBet(alpha):0.000})");
    Console.WriteLine($"  call Queen {queenCall:0.000}  (theory α+1/3  = {KuhnEquilibrium.Player0QueenCall(alpha):0.000})");
    Console.WriteLine($"Solved player-1 frequencies:");
    Console.WriteLine($"  bet Jack (after check) {s["Jp"][1]:0.000}  (theory 1/3)");
    Console.WriteLine($"  call Queen             {s["Qb"][1]:0.000}  (theory 1/3)");
    bool inFamily = KuhnEquilibrium.IsPlayer0InAlphaFamily(jackBet, queenCall, kingBet, 0.02);
    Console.WriteLine($"Recovered α = {alpha:0.000} ∈ [0, 1/3]; member of the equilibrium family: {inFamily}");
    return 0;
}

int Leduc(Dictionary<string, string> o)
{
    int iters = Int(o, "iters", 5000);
    var game = new LeducPoker();
    var solver = new CfrPlusSolver<LeducState>(game);
    solver.Run(iters);
    var strat = solver.AverageStrategy();
    double exploit = BestResponse.Exploitability(game, strat);
    double mbb = BestResponse.ExploitabilityMilliBigBlinds(game, strat, 1.0);

    Console.WriteLine($"CFR+ on Leduc Hold'em — {iters:N0} iterations");
    Console.WriteLine($"  info sets solved: {strat.Count}");
    Console.WriteLine($"  exploitability:   {exploit:0.0000} chips/hand");
    Console.WriteLine($"  exploitability:   {mbb:0.0} mbb/hand");
    return 0;
}

int Demo()
{
    Console.WriteLine("pokerface — scenario demo (deterministic, seed 1)\n");
    var engine = new DecisionEngine();
    void Show(string title, Spot spot, OpponentModel? m = null)
    {
        var r = engine.Decide(spot, new DeterministicRandom(1), m);
        Console.WriteLine($"• {title}");
        Console.WriteLine($"    {spot.Hero} | {(spot.Board.Count == 0 ? "preflop" : Cards(spot.Board))} | pot {spot.Pot}, to-call {spot.ToCall}, eff {spot.EffectiveStack}, bb {spot.BigBlind}");
        Console.WriteLine($"    → {r.Move}{(r.Chips > 0 ? " " + r.Chips : "")}  ({r.Explanation})\n");
    }

    Show("Short-stack jam: Aces in the SB at 8bb",
        MakeSpot("AhAs", "", Position.InPosition, 15, 5, 80, 10));
    Show("Short-stack fold: 72o in the SB at 8bb",
        MakeSpot("7h2c", "", Position.InPosition, 15, 5, 80, 10));
    Show("Deep open: AKo on the button, 100bb",
        MakeSpot("AhKs", "", Position.InPosition, 15, 5, 1000, 10));
    Show("Flop value bet: top set on a dry board",
        MakeSpot("AcAd", "Ah 7s 2c", Position.InPosition, 100, 0, 1000, 10));
    Show("River fold: air facing a pot-sized bet",
        MakeSpot("7h2c", "Ah Ks Qd 9c 3s", Position.OutOfPosition, 200, 100, 1000, 10));
    Show("Exploit a nit: same air, but villain folds 90% (high confidence) → bluff more",
        MakeSpot("7h2c", "Ah Ks Qd 9c", Position.InPosition, 100, 0, 1000, 10),
        new OpponentModel { FoldToBet = 0.9, Confidence = 1.0 });
    return 0;
}

// ----------------------------------------------------------------- helpers

static Spot MakeSpot(string hero, string board, Position pos, int pot, int toCall, int eff, int bb) => new()
{
    Hero = HoleCards.Parse(hero),
    Board = ParseCards(board),
    Position = pos,
    Pot = pot,
    ToCall = toCall,
    EffectiveStack = eff,
    BigBlind = bb,
};

static IReadOnlyList<Card> ParseCards(string s) =>
    string.IsNullOrWhiteSpace(s)
        ? Array.Empty<Card>()
        : s.Replace(",", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Card.Parse).ToList();

static string Cards(IReadOnlyList<Card> cards) => string.Join(" ", cards);

static Dictionary<string, string> ParseOptions(string[] args)
{
    var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int i = 1; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
        string key = args[i][2..];
        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            o[key] = args[++i];
        else
            o[key] = "true";
    }
    return o;
}

static string Get(Dictionary<string, string> o, string k, string fallback) => o.TryGetValue(k, out var v) ? v : fallback;
static string Required(Dictionary<string, string> o, string k) =>
    o.TryGetValue(k, out var v) ? v : throw new ArgumentException($"missing required --{k}");
static int Int(Dictionary<string, string> o, string k, int fallback) =>
    o.TryGetValue(k, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : fallback;
static double Dbl(Dictionary<string, string> o, string k, double fallback) =>
    o.TryGetValue(k, out var v) ? double.Parse(v, CultureInfo.InvariantCulture) : fallback;

static void PrintUsage()
{
    Console.WriteLine("""
pokerface — No-Limit Hold'em decision engine (CLI)

USAGE
  decide   --hero AhKs [--board "Ah 7s 2c"] [--pos ip|oop] [--pot N] [--tocall N]
           [--eff N] [--bb N] [--seed N] [--foldtobet 0..1 --confidence 0..1]
  equity   --hero AhAs [--villain KhKs | (vs a random hand)] [--board "..."]
           [--samples N] [--seed N]
  kuhn     [--iters N]            Solve the Kuhn validation game; show convergence.
  leduc    [--iters N]            Solve Leduc Hold'em; show exploitability.
  demo                           Run a set of canned scenarios.
  help

All commands are deterministic given --seed. Examples:
  decide --hero AhAs --pos ip --pot 15 --tocall 5 --eff 80 --bb 10
  equity --hero AhAs --villain KhKs --seed 1
  kuhn --iters 200000
""");
}
