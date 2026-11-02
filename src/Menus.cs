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

    private static ButtonComponent CreateMenuButton(string text, Func<MenuActionContext, ValueTask> onSelect)
    {
        return new ButtonComponent(CleanText(text), onSelect)
        {
            MaxWidth = 0,
            Overflow = MenuTextOverflow.None
        };
    }

    public IMenu BuildPlayerDuelSettingsMenu(IPlayer player)
    {
        var builder = Core.Menu.CreateMenu("slayer_duel.main")
            .WithTitle(CleanText(Localizer["MenuSettings.Title"]));

        string selectedOption = "";
        if (PlayerOption != null && PlayerOption.ContainsKey(player))
        {
            if (PlayerOption[player].Option == 1) selectedOption = CleanText(Localizer["PlayerMenu.Accept"]);
            else if (PlayerOption[player].Option == 0) selectedOption = CleanText(Localizer["PlayerMenu.Decline"]);
            else if (PlayerOption[player].Option == -1) selectedOption = CleanText(Localizer["PlayerMenu.Vote"]);
        }

        builder.Add(new TextComponent($"{CleanText(Localizer["PlayerMenu.Selected"])}: {selectedOption}")
        {
            MaxWidth = 0,
            Overflow = MenuTextOverflow.None
        });

        builder.Add(CreateMenuButton(Localizer["PlayerMenu.Vote"], ctx =>
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

        builder.Add(CreateMenuButton(Localizer["PlayerMenu.Accept"], ctx =>
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

        builder.Add(CreateMenuButton(Localizer["PlayerMenu.Decline"], ctx =>
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

        builder.Add(CreateMenuButton(Localizer["PlayerMenu.Rank"], ctx =>
        {
            var parent = BuildPlayerDuelSettingsMenu(ctx.Player);
            PlayerRankMenu(ctx.Player, parent);
            return ValueTask.CompletedTask;
        }));

        return builder.Build();
    }

    private void PlayerDuelSettingsMenu(IPlayer player)
    {
        if (player == null || !player.IsValid || !Config.PluginEnabled) return;
        BuildPlayerDuelSettingsMenu(player).Open(player);
    }

    public IMenu BuildPlayerRankMenu(IPlayer player, IMenu? parentMenu)
    {
        var stats = GetPlayerStats(player);
        int played = (stats?.Wins ?? 0) + (stats?.Losses ?? 0);

        var builder = Core.Menu.CreateMenu("slayer_duel.rank")
            .WithTitle(CleanText(Localizer["PlayerMenu.RankTitle"]));

        if (parentMenu != null)
            builder.WithParent(parentMenu);

        builder.Add(new TextComponent(CleanText(Localizer["PlayerMenu.Played", played])) { MaxWidth = 0, Overflow = MenuTextOverflow.None });
        builder.Add(new TextComponent(CleanText(Localizer["PlayerMenu.Wins", stats?.Wins ?? 0])) { MaxWidth = 0, Overflow = MenuTextOverflow.None });
        builder.Add(new TextComponent(CleanText(Localizer["PlayerMenu.Losses", stats?.Losses ?? 0])) { MaxWidth = 0, Overflow = MenuTextOverflow.None });

        builder.Add(CreateMenuButton(Localizer["PlayerMenu.TopPlayers"], ctx =>
        {
            var current = BuildPlayerRankMenu(ctx.Player, parentMenu);
            TopPlayersMenu(ctx.Player, current);
            return ValueTask.CompletedTask;
        }));

        return builder.Build();
    }

    private void PlayerRankMenu(IPlayer player, IMenu parentMenu)
    {
        if (player == null || !player.IsValid || !Config.PluginEnabled) return;
        BuildPlayerRankMenu(player, parentMenu).Open(player);
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
                })
                {
                    MaxWidth = 0,
                    Overflow = MenuTextOverflow.None
                });

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

        builder.Add(CreateMenuButton(Localizer["MenuSettings.TeleportSetT"], ctx =>
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

        builder.Add(CreateMenuButton(Localizer["MenuSettings.TeleportSetCT"], ctx =>
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

        builder.Add(CreateMenuButton(Localizer["MenuSettings.TeleportDelete"], ctx =>
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

        builder.Add(CreateMenuButton(Localizer["Menu.Accept"], ctx =>
        {
            AcceptDuelVoteOption(ctx.Player);
            Core.Menu.CloseFor(ctx.Player);
            return ValueTask.CompletedTask;
        }));

        builder.Add(CreateMenuButton(Localizer["Menu.Decline"], ctx =>
        {
            DeclineDuelVoteOption(ctx.Player);
            Core.Menu.CloseFor(ctx.Player);
            return ValueTask.CompletedTask;
        }));

        builder.Build().Open(player);
    }
}
