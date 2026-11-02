using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace SLAYER_Duel;

public partial class SLAYER_Duel : BasePlugin
{
    private static string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return Regex.Replace(text, "<.*?>", string.Empty).Trim();
    }

    private void PlayerDuelSettingsMenu(IPlayer player)
    {
        if (player == null || !player.IsValid || !Config.PluginEnabled) return;

        string selectedOption = "";
        if (PlayerOption != null && PlayerOption.ContainsKey(player))
        {
            if (PlayerOption[player].Option == 1) selectedOption = CleanText(Localizer["PlayerMenu.Accept"]);
            else if (PlayerOption[player].Option == 0) selectedOption = CleanText(Localizer["PlayerMenu.Decline"]);
            else if (PlayerOption[player].Option == -1) selectedOption = CleanText(Localizer["PlayerMenu.Vote"]);
        }

        var builder = Core.Menu.CreateMenu("slayer_duel.main")
            .WithTitle(CleanText(Localizer["MenuSettings.Title"]));

        // 1. Jelenlegi állapot kijelzése
        builder.Add(new TextComponent($"{CleanText(Localizer["PlayerMenu.Selected"])}: {selectedOption}"));

        // 2. Szavazás kérése (Vote)
        builder.Add(new ButtonComponent(CleanText(Localizer["PlayerMenu.Vote"]), ctx =>
        {
            if (PlayerOption != null && PlayerOption.ContainsKey(ctx.Player))
            {
                PlayerOption[ctx.Player].Option = -1;
            }
            ctx.Player.SendChatAsync($"{Localizer["Chat.Prefix"]}  {Localizer["Chat.DuelSettings.Vote"]}");
            SetPlayerDuelOption(ctx.Player, -1);
            PlayerDuelSettingsMenu(ctx.Player);
            return ValueTask.CompletedTask;
        }));

        // 3. Mindig elfogad (Accept)
        builder.Add(new ButtonComponent(CleanText(Localizer["PlayerMenu.Accept"]), ctx =>
        {
            if (PlayerOption != null && PlayerOption.ContainsKey(ctx.Player))
            {
                PlayerOption[ctx.Player].Option = 1;
            }
            ctx.Player.SendChatAsync($"{Localizer["Chat.Prefix"]}  {Localizer["Chat.DuelSettings.Accept"]}");
            SetPlayerDuelOption(ctx.Player, 1);
            PlayerDuelSettingsMenu(ctx.Player);
            return ValueTask.CompletedTask;
        }));

        // 4. Mindig elutasít (Decline)
        builder.Add(new ButtonComponent(CleanText(Localizer["PlayerMenu.Decline"]), ctx =>
        {
            if (PlayerOption != null && PlayerOption.ContainsKey(ctx.Player))
            {
                PlayerOption[ctx.Player].Option = 0;
            }
            ctx.Player.SendChatAsync($"{Localizer["Chat.Prefix"]}  {Localizer["Chat.DuelSettings.Decline"]}");
            SetPlayerDuelOption(ctx.Player, 0);
            PlayerDuelSettingsMenu(ctx.Player);
            return ValueTask.CompletedTask;
        }));

        // 5. Rang menüpont
        builder.Add(new ButtonComponent(CleanText(Localizer["PlayerMenu.Rank"]), ctx =>
        {
            var parent = Core.Menu.CreateMenu("slayer_duel.main")
                .WithTitle(CleanText(Localizer["MenuSettings.Title"]))
                .Build();
            PlayerRankMenu(ctx.Player, parent);
            return ValueTask.CompletedTask;
        }));

        builder.Build().Open(player);
    }

    private void PlayerRankMenu(IPlayer player, IMenu parentMenu)
    {
        if (player == null || !player.IsValid || !Config.PluginEnabled) return;

        var stats = GetPlayerStats(player);
        int played = (stats?.Wins ?? 0) + (stats?.Losses ?? 0);

        var builder = Core.Menu.CreateMenu("slayer_duel.rank")
            .WithParent(parentMenu)
            .WithTitle(CleanText(Localizer["PlayerMenu.RankTitle"]));

        builder.Add(new TextComponent(CleanText(Localizer["PlayerMenu.Played", played])));
        builder.Add(new TextComponent(CleanText(Localizer["PlayerMenu.Wins", stats?.Wins ?? 0])));
        builder.Add(new TextComponent(CleanText(Localizer["PlayerMenu.Losses", stats?.Losses ?? 0])));

        builder.Add(new ButtonComponent(CleanText(Localizer["PlayerMenu.TopPlayers"]), ctx =>
        {
            var parent = Core.Menu.CreateMenu("slayer_duel.rank")
                .WithParent(parentMenu)
                .WithTitle(CleanText(Localizer["PlayerMenu.RankTitle"]))
                .Build();
            TopPlayersMenu(ctx.Player, parent);
            return ValueTask.CompletedTask;
        }));

        builder.Build().Open(player);
    }

    private void TopPlayersMenu(IPlayer player, IMenu parentMenu)
    {
        if (player == null || !player.IsValid || !Config.PluginEnabled) return;

        GetTopPlayersSettings(Config.Duel_TopPlayersCount, (topPlayers) =>
        {
            var builder = Core.Menu.CreateMenu("slayer_duel.top_players")
                .WithParent(parentMenu)
                .WithTitle(CleanText(Localizer["PlayerMenu.TopPlayers"]));

            int rank = 1;
            foreach (var playerSettings in topPlayers)
            {
                var capturedSettings = playerSettings;
                var playerOptionText = $"#{rank} {capturedSettings.PlayerName}";

                builder.Add(new ButtonComponent(playerOptionText, ctx =>
                {
                    int totalPlayed = capturedSettings.Wins + capturedSettings.Losses;
                    double winRate = totalPlayed > 0 ? (double)capturedSettings.Wins / totalPlayed * 100 : 0;
                    ctx.Player.SendChatAsync($"{Localizer["Chat.Stats.HeaderFooter"]}");
                    ctx.Player.SendChatAsync($"{Localizer["Chat.Stats.Name", capturedSettings.PlayerName]}");
                    ctx.Player.SendChatAsync($"{Localizer["Chat.Stats.Played", totalPlayed]}");
                    ctx.Player.SendChatAsync($"{Localizer["Chat.Stats.Wins", capturedSettings.Wins]}");
                    ctx.Player.SendChatAsync($"{Localizer["Chat.Stats.Losses", capturedSettings.Losses]}");
                    ctx.Player.SendChatAsync($"{Localizer["Chat.Stats.WinRate", $"{winRate:F2}%"]}");
                    ctx.Player.SendChatAsync($"{Localizer["Chat.Stats.HeaderFooter"]}");
                    return ValueTask.CompletedTask;
                }));

                rank++;
            }

            builder.Build().Open(player);
        });
    }

    private void DuelSettingsMenu(IPlayer player)
    {
        if (player == null || !player.IsValid || !Config.PluginEnabled || player.PlayerPawn == null) return;

        var builder = Core.Menu.CreateMenu("slayer_duel.admin_settings")
            .WithTitle(CleanText(Localizer["MenuSettings.Title"]));

        builder.Add(new ButtonComponent(CleanText(Localizer["MenuSettings.TeleportSetT"]), ctx =>
        {
            if (Duel_Positions.ContainsKey(Core.Engine.GlobalVars.MapName))
            {
                Dictionary<string, string> mapData = Duel_Positions[Core.Engine.GlobalVars.MapName];
                mapData["T_Pos"] = $"{player.PlayerPawn.AbsOrigin!.Value.X} {player.PlayerPawn.AbsOrigin!.Value.Y} {player.PlayerPawn.AbsOrigin!.Value.Z};{player.PlayerPawn.AbsRotation!.Value.Pitch} {player.PlayerPawn.AbsRotation!.Value.Yaw} {player.PlayerPawn.AbsRotation!.Value.Roll}";
                File.WriteAllText(GetMapTeleportPositionConfigPath(), JsonSerializer.Serialize(Duel_Positions));
            }
            else
            {
                Duel_Positions.Add(Core.Engine.GlobalVars.MapName, new Dictionary<string, string> { { "T_Pos", $"{player.PlayerPawn.AbsOrigin!.Value.X} {player.PlayerPawn.AbsOrigin!.Value.Y} {player.PlayerPawn.AbsOrigin!.Value.Z};{player.PlayerPawn.AbsRotation!.Value.Pitch} {player.PlayerPawn.AbsRotation!.Value.Yaw} {player.PlayerPawn.AbsRotation!.Value.Roll}" }, { "CT_Pos", "" } });
                File.WriteAllText(GetMapTeleportPositionConfigPath(), JsonSerializer.Serialize(Duel_Positions));
            }
            player.SendChatAsync($"{Localizer["Chat.Prefix"]}  {Localizer["Chat.DuelSettings.TeleportSetT", $"{player.PlayerPawn.AbsOrigin!.Value.X} {player.PlayerPawn.AbsOrigin!.Value.Y} {player.PlayerPawn.AbsOrigin!.Value.Z}"]}");
            return ValueTask.CompletedTask;
        }));

        builder.Add(new ButtonComponent(CleanText(Localizer["MenuSettings.TeleportSetCT"]), ctx =>
        {
            if (Duel_Positions.ContainsKey(Core.Engine.GlobalVars.MapName))
            {
                Dictionary<string, string> mapData = Duel_Positions[Core.Engine.GlobalVars.MapName];
                mapData["CT_Pos"] = $"{player.PlayerPawn.AbsOrigin!.Value.X} {player.PlayerPawn.AbsOrigin!.Value.Y} {player.PlayerPawn.AbsOrigin!.Value.Z};{player.PlayerPawn.AbsRotation!.Value.Pitch} {player.PlayerPawn.AbsRotation!.Value.Yaw} {player.PlayerPawn.AbsRotation!.Value.Roll}";
                File.WriteAllText(GetMapTeleportPositionConfigPath(), JsonSerializer.Serialize(Duel_Positions));
            }
            else
            {
                Duel_Positions.Add(Core.Engine.GlobalVars.MapName, new Dictionary<string, string> { { "T_Pos", "" }, { "CT_Pos", $"{player.PlayerPawn.AbsOrigin!.Value.X} {player.PlayerPawn.AbsOrigin!.Value.Y} {player.PlayerPawn.AbsOrigin!.Value.Z};{player.PlayerPawn.AbsRotation!.Value.Pitch} {player.PlayerPawn.AbsRotation!.Value.Yaw} {player.PlayerPawn.AbsRotation!.Value.Roll}" } });
                File.WriteAllText(GetMapTeleportPositionConfigPath(), JsonSerializer.Serialize(Duel_Positions));
            }
            player.SendChatAsync($"{Localizer["Chat.Prefix"]} {Localizer["Chat.DuelSettings.TeleportSetCT", $"{player.PlayerPawn.AbsOrigin!.Value.X} {player.PlayerPawn.AbsOrigin!.Value.Y} {player.PlayerPawn.AbsOrigin!.Value.Z}"]}");
            return ValueTask.CompletedTask;
        }));

        builder.Add(new ButtonComponent(CleanText(Localizer["MenuSettings.TeleportDelete"]), ctx =>
        {
            if (Duel_Positions != null && Duel_Positions.ContainsKey(Core.Engine.GlobalVars.MapName))
            {
                Duel_Positions.Remove(Core.Engine.GlobalVars.MapName);
                File.WriteAllText(GetMapTeleportPositionConfigPath(), JsonSerializer.Serialize(Duel_Positions));
            }
            player.SendChatAsync($"{Localizer["Chat.Prefix"]} {Localizer["Chat.DuelSettings.TeleportDelete"]}");
            return ValueTask.CompletedTask;
        }));

        builder.Build().Open(player);
    }

    private void ShowDuelVoteMenu(IPlayer player)
    {
        if (player == null || !player.IsValid || !Config.PluginEnabled || player.PlayerPawn == null) return;

        var builder = Core.Menu.CreateMenu("slayer_duel.vote")
            .WithTitle(CleanText(Localizer["Menu.Title"]));

        builder.Add(new ButtonComponent(CleanText(Localizer["Menu.Accept"]), ctx =>
        {
            AcceptDuelVoteOption(ctx.Player);
            Core.Menu.CloseFor(ctx.Player);
            return ValueTask.CompletedTask;
        }));

        builder.Add(new ButtonComponent(CleanText(Localizer["Menu.Decline"]), ctx =>
        {
            DeclineDuelVoteOption(ctx.Player);
            Core.Menu.CloseFor(ctx.Player);
            return ValueTask.CompletedTask;
        }));

        builder.Build().Open(player);
    }
}