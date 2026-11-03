using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace SLAYER_Duel;

public partial class SLAYER_Duel : BasePlugin
{
    private static string OptimizeColors(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        return text
            .Replace("color='gold'", "color='#fd0'")
            .Replace("color=\"gold\"", "color='#fd0'")
            .Replace("color='lime'", "color='#0f0'")
            .Replace("color=\"lime\"", "color='#0f0'")
            .Replace("color='red'", "color='#f00'")
            .Replace("color=\"red\"", "color='#f00'")
            .Replace("color='yellow'", "color='#ff0'")
            .Replace("color=\"yellow\"", "color='#ff0'")
            .Replace("color='onull'", "color='#fa0'")
            .Replace("color=\"onull\"", "color='#fa0'")
            .Replace("color='royalblue'", "color='#48f'")
            .Replace("color=\"royalblue\"", "color='#48f'");
    }

    private static ButtonComponent CreateMenuButton(string text, Func<MenuActionContext, ValueTask> onSelect)
    {
        return new ButtonComponent(OptimizeColors(text), onSelect)
        {
            MaxWidth = 0,
            Overflow = MenuTextOverflow.None
        };
    }

    private static TextComponent CreateMenuText(string text)
    {
        return new TextComponent(OptimizeColors(text))
        {
            MaxWidth = 0,
            Overflow = MenuTextOverflow.None
        };
    }

    private static IMenuBuilder CreateMenuWithTitle(string id, string title)
    {
        return Core.Menu.CreateMenu(id)
            .AddHeader(new TitleComponent(OptimizeColors(title))
            {
                MaxWidth = 0,
                Overflow = MenuTextOverflow.None
            });
    }

    public IMenu BuildPlayerDuelSettingsMenu(IPlayer player)
    {
        var builder = CreateMenuWithTitle("slayer_duel.main", Localizer["MenuSettings.Title"]);

        string selectedOption = "";
        if (PlayerOption != null && PlayerOption.ContainsKey(player))
        {
            if (PlayerOption[player].Option == 1) selectedOption = OptimizeColors(Localizer["PlayerMenu.Accept"]);
            else if (PlayerOption[player].Option == 0) selectedOption = OptimizeColors(Localizer["PlayerMenu.Decline"]);
            else if (PlayerOption[player].Option == -1) selectedOption = OptimizeColors(Localizer["PlayerMenu.Vote"]);
        }

        builder.Add(CreateMenuText($"{Localizer["PlayerMenu.Selected"]} {selectedOption}"));

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

        var builder = CreateMenuWithTitle("slayer_duel.rank", Localizer["PlayerMenu.RankTitle"]);

        if (parentMenu != null)
            builder.WithParent(parentMenu);

        builder.Add(CreateMenuText(Localizer["PlayerMenu.Played", played]));
        builder.Add(CreateMenuText(Localizer["PlayerMenu.Wins", stats?.Wins ?? 0]));
        builder.Add(CreateMenuText(Localizer["PlayerMenu.Losses", stats?.Losses ?? 0]));

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
            var builder = CreateMenuWithTitle("slayer_duel.top_players", Localizer["PlayerMenu.TopPlayers"]);

            if (parentMenu != null)
                builder.WithParent(parentMenu);

            int rank = 1;
            foreach (var playerSettings in topPlayers)
            {
                var capturedSettings = playerSettings;
                var playerOptionText = $"<font color='#fd0'>#{rank}</font> <font color='#fff'>{capturedSettings.PlayerName}</font>";

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

        var builder = CreateMenuWithTitle("slayer_duel.admin_settings", Localizer["MenuSettings.Title"]);

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
        if (player == null || !player.IsValid || !Config.PluginEnabled || player.PlayerPawn == null) return; // wait, null is typo, let's fix to player.PlayerPawn == null

        if (player.PlayerPawn == null) return;

        var builder = CreateMenuWithTitle("slayer_duel.vote", Localizer["Menu.Title"]);

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