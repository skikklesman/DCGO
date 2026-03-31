using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Captures a full game state snapshot to disk (human-readable text + JSON).
// Triggered via Ctrl+G debug shortcut in GManager.AllowAlphaInputs().
public static class GameStateSerializer
{
    static readonly string OutputDir = Path.Combine(Application.dataPath, "..", "ClaudeReports", "GameStateSnapshots");

    public static void CaptureAndWrite()
    {
        GameContext ctx = GManager.instance.turnStateMachine.gameContext;
        Player you = GManager.instance.You;
        Player opponent = GManager.instance.Opponent;

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        Directory.CreateDirectory(OutputDir);

        string txtPath = Path.Combine(OutputDir, $"snapshot_{timestamp}.txt");
        string jsonPath = Path.Combine(OutputDir, $"snapshot_{timestamp}.json");

        File.WriteAllText(txtPath, BuildText(ctx, you, opponent, timestamp), Encoding.UTF8);
        File.WriteAllText(jsonPath, BuildJson(ctx, you, opponent, timestamp), Encoding.UTF8);

        Debug.Log($"[GameState] Snapshot saved:\n  {txtPath}\n  {jsonPath}");
    }

    // -------------------------------------------------------------------------
    // Text output
    // -------------------------------------------------------------------------

    static string BuildText(GameContext ctx, Player you, Player opponent, string timestamp)
    {
        var sb = new StringBuilder();
        int turn = GManager.instance.turnStateMachine.TurnCount;
        bool isYourTurn = ctx.TurnPlayer == you;

        sb.AppendLine("=== GAME STATE SNAPSHOT ===");
        sb.AppendLine($"Time: {timestamp}");
        sb.AppendLine($"Turn: {turn}  |  Phase: {ctx.TurnPhase}  |  Turn Player: {(isYourTurn ? "YOU" : "OPPONENT")}  |  Memory: {ctx.Memory}");
        sb.AppendLine("Note: effect text not included -- refer to card database by ID.");
        sb.AppendLine();

        sb.AppendLine("=== YOUR SIDE ===");
        AppendPlayerText(sb, you, isYours: true, turn);
        sb.AppendLine();

        sb.AppendLine("=== OPPONENT SIDE ===");
        AppendPlayerText(sb, opponent, isYours: false, turn);

        return sb.ToString();
    }

    static void AppendPlayerText(StringBuilder sb, Player player, bool isYours, int turn)
    {
        // Hand
        if (isYours)
        {
            List<CardSource> hand = player.HandCards;
            sb.AppendLine($"Hand ({hand.Count} cards):");
            if (hand.Count == 0)
                sb.AppendLine("  (empty)");
            else
                foreach (CardSource c in hand)
                    sb.AppendLine($"  {CardTextLine(c)}");
        }
        else
        {
            sb.AppendLine($"Hand: {player.HandCards.Count} cards (hidden)");
        }

        // Counts
        sb.AppendLine($"Deck: {player.LibraryCards.Count}  |  Security: {player.SecurityCards.Count}  |  Digitama Deck: {player.DigitamaLibraryCards.Count}");

        // Trash
        List<CardSource> trash = player.TrashCards;
        if (trash.Count == 0)
            sb.AppendLine("Trash: (empty)");
        else
        {
            sb.AppendLine($"Trash ({trash.Count} cards):");
            foreach (CardSource c in trash)
                sb.AppendLine($"  {CardTextLine(c)}");
        }

        // Lost zone
        List<CardSource> lost = player.LostCards;
        if (lost.Count > 0)
        {
            sb.AppendLine($"Lost Zone ({lost.Count} cards):");
            foreach (CardSource c in lost)
                sb.AppendLine($"  {CardTextLine(c)}");
        }

        // Breeding area
        List<Permanent> breeding = player.GetBreedingAreaPermanents();
        sb.AppendLine("Breeding Area:");
        if (breeding.Count == 0)
            sb.AppendLine("  [empty]");
        else
            foreach (Permanent p in breeding)
                AppendPermanentText(sb, p, turn, "  ");

        // Battle area
        List<Permanent> battle = player.GetBattleAreaPermanents();
        sb.AppendLine("Battle Area:");
        if (battle.Count == 0)
            sb.AppendLine("  [empty]");
        else
            foreach (Permanent p in battle)
                AppendPermanentText(sb, p, turn, "  ");
    }

    static void AppendPermanentText(StringBuilder sb, Permanent p, int turn, string indent)
    {
        CardSource top = p.TopCard;
        string status = p.IsSuspended ? "SUSPENDED" : "ACTIVE";
        bool justPlayed = p.EnterFieldTurnCount == turn;

        sb.AppendLine($"{indent}[{status}] {top.BaseENGCardNameFromEntity} ({top.CardID}) | {CardKindText(top)} | Lv{top.Level} | {p.DP} DP | Colors: {ColorsText(top)}");
        sb.Append($"{indent}  Enter turn: {p.EnterFieldTurnCount}");
        if (justPlayed) sb.Append(" (played this turn -- may have summoning sickness)");
        sb.AppendLine();

        List<CardSource> stack = p.DigivolutionCards;
        if (stack.Count > 0)
        {
            // cardSources[0] = TopCard, DigivolutionCards[0] = just below top, last = bottom
            // Print top-to-bottom (most recent digivolution first)
            sb.AppendLine($"{indent}  Digivolution stack (top to bottom):");
            foreach (CardSource c in stack)
                sb.AppendLine($"{indent}    {c.BaseENGCardNameFromEntity} ({c.CardID}) | Lv{c.Level} | Colors: {ColorsText(c)}");
        }

        List<CardSource> linked = p.LinkedCards;
        if (linked.Count > 0)
        {
            sb.AppendLine($"{indent}  Linked cards:");
            foreach (CardSource c in linked)
                sb.AppendLine($"{indent}    {c.BaseENGCardNameFromEntity} ({c.CardID}) | {CardKindText(c)}");
        }
    }

    static string CardTextLine(CardSource c)
    {
        var parts = new List<string>
        {
            $"{c.BaseENGCardNameFromEntity} ({c.CardID})",
            CardKindText(c)
        };

        if (c.IsDigimon || c.IsDigiEgg)
        {
            parts.Add($"Lv{c.Level}");
            parts.Add($"{c.BaseCardDP} DP");
        }

        int cost = c.BasePlayCostFromEntity;
        if (cost >= 0)
            parts.Add($"Cost: {cost}");

        parts.Add($"Colors: {ColorsText(c)}");

        return string.Join(" | ", parts);
    }

    // -------------------------------------------------------------------------
    // JSON output
    // -------------------------------------------------------------------------

    static string BuildJson(GameContext ctx, Player you, Player opponent, string timestamp)
    {
        var sb = new StringBuilder();
        int turn = GManager.instance.turnStateMachine.TurnCount;
        bool isYourTurn = ctx.TurnPlayer == you;

        sb.AppendLine("{");
        sb.AppendLine($"  \"snapshot_time\": \"{timestamp}\",");
        sb.AppendLine($"  \"turn\": {turn},");
        sb.AppendLine($"  \"phase\": \"{ctx.TurnPhase}\",");
        sb.AppendLine($"  \"memory\": {ctx.Memory},");
        sb.AppendLine($"  \"turn_player\": \"{(isYourTurn ? "you" : "opponent")}\",");
        sb.AppendLine($"  \"you\": {{");
        AppendPlayerJson(sb, you, isYours: true, turn, indent: "    ");
        sb.AppendLine("  },");
        sb.AppendLine($"  \"opponent\": {{");
        AppendPlayerJson(sb, opponent, isYours: false, turn, indent: "    ");
        sb.AppendLine("  }");
        sb.Append("}");

        return sb.ToString();
    }

    static void AppendPlayerJson(StringBuilder sb, Player player, bool isYours, int turn, string indent)
    {
        if (isYours)
        {
            sb.AppendLine($"{indent}\"hand\": [");
            AppendCardListJson(sb, player.HandCards, indent + "  ");
            sb.AppendLine($"{indent}],");
        }
        else
        {
            sb.AppendLine($"{indent}\"hand_count\": {player.HandCards.Count},");
        }

        sb.AppendLine($"{indent}\"deck_count\": {player.LibraryCards.Count},");
        sb.AppendLine($"{indent}\"security_count\": {player.SecurityCards.Count},");
        sb.AppendLine($"{indent}\"digitama_count\": {player.DigitamaLibraryCards.Count},");

        sb.AppendLine($"{indent}\"trash\": [");
        AppendCardListJson(sb, player.TrashCards, indent + "  ");
        sb.AppendLine($"{indent}],");

        sb.AppendLine($"{indent}\"lost_zone\": [");
        AppendCardListJson(sb, player.LostCards, indent + "  ");
        sb.AppendLine($"{indent}],");

        sb.AppendLine($"{indent}\"breeding_area\": [");
        AppendPermanentListJson(sb, player.GetBreedingAreaPermanents(), turn, indent + "  ");
        sb.AppendLine($"{indent}],");

        sb.AppendLine($"{indent}\"battle_area\": [");
        AppendPermanentListJson(sb, player.GetBattleAreaPermanents(), turn, indent + "  ");
        sb.Append($"{indent}]");
        sb.AppendLine();
    }

    static void AppendCardListJson(StringBuilder sb, List<CardSource> cards, string indent)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            string comma = i < cards.Count - 1 ? "," : "";
            sb.AppendLine($"{indent}{CardJson(cards[i])}{comma}");
        }
    }

    static void AppendPermanentListJson(StringBuilder sb, List<Permanent> permanents, int turn, string indent)
    {
        for (int i = 0; i < permanents.Count; i++)
        {
            string comma = i < permanents.Count - 1 ? "," : "";
            AppendSinglePermanentJson(sb, permanents[i], turn, indent);
            if (comma.Length > 0) sb.AppendLine(",");
        }
    }

    static void AppendSinglePermanentJson(StringBuilder sb, Permanent p, int turn, string indent)
    {
        CardSource top = p.TopCard;
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}  \"id\": \"{J(top.CardID)}\",");
        sb.AppendLine($"{indent}  \"name\": \"{J(top.BaseENGCardNameFromEntity)}\",");
        sb.AppendLine($"{indent}  \"type\": \"{CardKindText(top)}\",");
        sb.AppendLine($"{indent}  \"level\": {top.Level},");
        sb.AppendLine($"{indent}  \"dp\": {p.DP},");
        sb.AppendLine($"{indent}  \"colors\": [{ColorsJsonArray(top)}],");
        sb.AppendLine($"{indent}  \"suspended\": {Bool(p.IsSuspended)},");
        sb.AppendLine($"{indent}  \"enter_turn\": {p.EnterFieldTurnCount},");
        sb.AppendLine($"{indent}  \"played_this_turn\": {Bool(p.EnterFieldTurnCount == turn)},");

        List<CardSource> stack = p.DigivolutionCards;
        sb.AppendLine($"{indent}  \"digivolution_stack\": [");
        AppendCardListJson(sb, stack, indent + "    ");
        sb.AppendLine($"{indent}  ],");

        List<CardSource> linked = p.LinkedCards;
        sb.AppendLine($"{indent}  \"linked_cards\": [");
        AppendCardListJson(sb, linked, indent + "    ");
        sb.AppendLine($"{indent}  ]");

        sb.Append($"{indent}}}");
    }

    static string CardJson(CardSource c)
    {
        return $"{{\"id\": \"{J(c.CardID)}\", \"name\": \"{J(c.BaseENGCardNameFromEntity)}\", " +
               $"\"type\": \"{CardKindText(c)}\", \"level\": {c.Level}, \"dp\": {c.BaseCardDP}, " +
               $"\"cost\": {c.BasePlayCostFromEntity}, \"colors\": [{ColorsJsonArray(c)}]}}";
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    static string CardKindText(CardSource c)
    {
        if (c.IsDigiEgg) return "DigiEgg";
        if (c.IsDigimon) return "Digimon";
        if (c.IsTamer) return "Tamer";
        if (c.IsOption) return "Option";
        return "Unknown";
    }

    static string ColorsText(CardSource c)
    {
        return string.Join("/", c.CardColors.ConvertAll(col => col.ToString()));
    }

    static string ColorsJsonArray(CardSource c)
    {
        return string.Join(", ", c.CardColors.ConvertAll(col => $"\"{col}\""));
    }

    static string J(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    static string Bool(bool v) => v ? "true" : "false";
}
