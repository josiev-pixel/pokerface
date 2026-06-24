using System.Text;
using PokerEngine.Core;
using PokerEngine.Decision;
using PokerEngine.Table;
using Raylib_cs;

// pokerface — scenario table (ADR-0004): a tiny Raylib VIEWER for heads-up spots.
// It is a dev/test tool, not a product: pick from the canned scenarios, see the felt,
// the hero's cards and the board, and read the engine's recommendation for that spot.
// The decision call is deterministic — seeded by `seed` — so what you see is reproducible,
// and pressing R re-samples the mixed strategy with the next seed.
//
// Headless note: this opens a window, so it cannot run in CI. Compile-clean is the bar.

const int Width = 1100;
const int Height = 720;

var engine = new DecisionEngine();
var scenarios = Scenarios.All;

int index = 0;       // which scenario we are viewing
ulong seed = 1;      // RNG seed for the decision (R increments it to re-sample)

Raylib.InitWindow(Width, Height, "pokerface — scenario table");
Raylib.SetTargetFPS(60);

// Cache the current decision; recompute only when the scenario or seed changes.
var current = scenarios[index];
var result = Decide(current, seed);

while (!Raylib.WindowShouldClose())
{
    // ---- input -------------------------------------------------------------
    bool dirty = false;
    if (Raylib.IsKeyPressed(KeyboardKey.Right)) { index = (index + 1) % scenarios.Count; seed = 1; dirty = true; }
    if (Raylib.IsKeyPressed(KeyboardKey.Left)) { index = (index - 1 + scenarios.Count) % scenarios.Count; seed = 1; dirty = true; }
    if (Raylib.IsKeyPressed(KeyboardKey.R)) { seed++; dirty = true; } // re-run the mixed strategy on a new seed

    if (dirty)
    {
        current = scenarios[index];
        result = Decide(current, seed);
    }

    // ---- draw --------------------------------------------------------------
    Raylib.BeginDrawing();
    Raylib.ClearBackground(new Color(24, 24, 28, 255));

    DrawTable();
    DrawScenarioHeader(current, index, scenarios.Count, seed);
    DrawSpotFacts(current.Spot, current.Opponent);
    DrawCards(current.Spot);
    DrawRecommendation(result);
    DrawLegend();

    Raylib.EndDrawing();
}

Raylib.CloseWindow();

// ---------------------------------------------------------------- engine call

// Run the engine once for a scenario at a given seed. Deterministic: same (spot, model, seed)
// always yields the same sampled action (DECISION_ALGORITHM §9).
DecisionResult Decide(Scenario s, ulong sd) =>
    engine.Decide(s.Spot, new DeterministicRandom(sd), s.Opponent);

// ---------------------------------------------------------------- drawing

// The green felt: a dark rounded rectangle with a lighter rail, plus a centre ellipse.
void DrawTable()
{
    var felt = new Rectangle(40, 150, Width - 80, 360);
    Raylib.DrawRectangleRounded(felt, 0.5f, 24, new Color(12, 70, 36, 255));
    Raylib.DrawRectangleRoundedLines(felt, 0.5f, 24, new Color(150, 110, 60, 255));
    Raylib.DrawEllipse(Width / 2, 330, 360, 130, new Color(18, 92, 48, 255));
}

void DrawScenarioHeader(Scenario s, int i, int total, ulong sd)
{
    Raylib.DrawText("pokerface — scenario table", 40, 24, 28, Color.RayWhite);
    Raylib.DrawText($"Scenario {i + 1}/{total}   (seed {sd})", 40, 60, 18, new Color(170, 170, 180, 255));
    Raylib.DrawText(s.Title, 40, 90, 22, Color.Gold);
}

// Pot / to-call / stacks / position / street, drawn over the felt.
void DrawSpotFacts(Spot spot, OpponentModel? opp)
{
    int x = 70;
    int y = 175;
    Raylib.DrawText($"Street: {spot.StreetName}", x, y, 20, Color.RayWhite);
    Raylib.DrawText($"Position: {spot.Position}", x, y + 28, 20, Color.RayWhite);
    Raylib.DrawText($"Pot: {spot.Pot}", x, y + 56, 20, Color.RayWhite);
    Raylib.DrawText($"To call: {spot.ToCall}", x, y + 84, 20, Color.RayWhite);

    int rx = Width - 320;
    Raylib.DrawText($"Effective stack: {spot.EffectiveStack}", rx, y, 20, Color.RayWhite);
    Raylib.DrawText($"Big blind: {spot.BigBlind}", rx, y + 28, 20, Color.RayWhite);
    Raylib.DrawText($"Eff. BB: {spot.EffectiveBigBlinds:0.#}", rx, y + 56, 20, Color.RayWhite);
    if (opp is not null)
        Raylib.DrawText($"Villain folds {opp.FoldToBet:P0} (conf {opp.Confidence:0.##})",
            rx, y + 84, 20, Color.Orange);
}

// Hero's two hole cards (left) and the community board (centre), as hand-drawn sprites.
void DrawCards(Spot spot)
{
    Raylib.DrawText("Hero", 110, 360, 20, Color.RayWhite);
    DrawCard(spot.Hero.High, 95, 390);
    DrawCard(spot.Hero.Low, 165, 390);

    Raylib.DrawText("Board", Width / 2 - 30, 270, 20, Color.RayWhite);
    if (spot.Board.Count == 0)
    {
        Raylib.DrawText("(preflop — no board)", Width / 2 - 90, 320, 18, new Color(190, 200, 190, 255));
    }
    else
    {
        const int cardW = 64;
        const int gap = 12;
        int totalW = (spot.Board.Count * cardW) + ((spot.Board.Count - 1) * gap);
        int startX = (Width / 2) - (totalW / 2);
        for (int i = 0; i < spot.Board.Count; i++)
            DrawCard(spot.Board[i], startX + (i * (cardW + gap)), 300);
    }
}

// One card sprite: a rounded white rectangle with rank text and a suit glyph,
// red for hearts/diamonds, black for clubs/spades. Size matches the board layout.
void DrawCard(Card card, int x, int y)
{
    const int w = 64;
    const int h = 88;
    var rect = new Rectangle(x, y, w, h);
    Raylib.DrawRectangleRounded(rect, 0.18f, 8, Color.RayWhite);
    Raylib.DrawRectangleRoundedLines(rect, 0.18f, 8, new Color(60, 60, 60, 255));

    Color ink = card.Suit is Suit.Hearts or Suit.Diamonds
        ? new Color(200, 30, 40, 255)
        : new Color(20, 20, 20, 255);

    Raylib.DrawText(RankText(card.Rank), x + 8, y + 6, 26, ink);   // top-left rank
    string glyph = SuitGlyph(card.Suit);
    int glyphWidth = Raylib.MeasureText(glyph, 34);
    Raylib.DrawText(glyph, x + (w - glyphWidth) / 2, y + 40, 34, ink); // centre suit
}

// The engine's answer: headline move + chips, the equity/required-equity, the exploit
// weight (when used), the full mixed strategy, and the word-wrapped explanation.
void DrawRecommendation(DecisionResult r)
{
    int x = 40;
    int y = 540;
    string chips = r.Chips > 0 ? $" {r.Chips}" : "";
    Raylib.DrawText($"Recommendation:  {r.Move}{chips}", x, y, 30, Color.Lime);

    var line = new StringBuilder($"equity {r.Equity:P1}");
    if (r.RequiredEquity > 0) line.Append($"   |   pot-odds need {r.RequiredEquity:P1}");
    if (r.ExploitWeight > 0) line.Append($"   |   exploit weight {r.ExploitWeight:0.##}");
    Raylib.DrawText(line.ToString(), x, y + 38, 20, Color.RayWhite);

    string strat = string.Join("    ",
        r.Strategy.Where(o => o.Probability > 0)
                  .Select(o => o.Chips > 0
                      ? $"{o.Move} {o.Chips} — {o.Probability:P0}"
                      : $"{o.Move} — {o.Probability:P0}"));
    Raylib.DrawText($"Strategy:  {strat}", x, y + 66, 20, new Color(180, 220, 255, 255));

    DrawWrapped(r.Explanation, x, y + 100, Width - 80, 18, new Color(200, 200, 205, 255));
}

void DrawLegend()
{
    Raylib.DrawText("<- / ->  cycle scenarios     R  re-run (next seed)     Esc  quit",
        40, Height - 28, 18, new Color(150, 150, 160, 255));
}

// ---------------------------------------------------------------- text helpers

// Greedy word-wrap so long explanations stay inside the window.
void DrawWrapped(string text, int x, int y, int maxWidth, int fontSize, Color color)
{
    var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var lineBuf = new StringBuilder();
    int lineY = y;
    foreach (var word in words)
    {
        string candidate = lineBuf.Length == 0 ? word : $"{lineBuf} {word}";
        if (Raylib.MeasureText(candidate, fontSize) > maxWidth && lineBuf.Length > 0)
        {
            Raylib.DrawText(lineBuf.ToString(), x, lineY, fontSize, color);
            lineBuf.Clear().Append(word);
            lineY += fontSize + 6;
        }
        else
        {
            lineBuf.Clear().Append(candidate);
        }
    }
    if (lineBuf.Length > 0)
        Raylib.DrawText(lineBuf.ToString(), x, lineY, fontSize, color);
}

static string RankText(Rank rank) => rank switch
{
    Rank.Ace => "A",
    Rank.King => "K",
    Rank.Queen => "Q",
    Rank.Jack => "J",
    Rank.Ten => "T",
    _ => ((int)rank).ToString(),
};

static string SuitGlyph(Suit suit) => suit switch
{
    Suit.Clubs => "C",
    Suit.Diamonds => "D",
    Suit.Hearts => "H",
    Suit.Spades => "S",
    _ => "?",
};
