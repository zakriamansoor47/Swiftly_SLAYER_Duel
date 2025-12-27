using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Core.Menus.OptionsBase;
using System.Text.Json;

namespace SLAYER_Duel;

public partial class SLAYER_Duel : BasePlugin
{
    private void PlayerDuelSettingsMenu(IPlayer player)
    {
        if (player == null || !player.IsValid || !Config.PluginEnabled) return;

        // Create menu
        var settingsMenu = Core.MenusAPI.CreateBuilder().EnableExit().EnableSound().SetPlayerFrozen(true)
        .Design.SetMenuTitle($"{Localizer["MenuSettings.Title"]}")
        .Design.SetGlobalScrollStyle(MenuOptionScrollStyle.CenterFixed)
        .Build();
        
        // Player Duel Rank
        var RankOption = new ButtonMenuOption($"{Localizer["PlayerMenu.Rank"]}");
        RankOption.Click += (sender, args) =>
        {
            PlayerRankMenu(args.Player, settingsMenu);
            return ValueTask.CompletedTask;
        };
        

        // Change player settings
        var SettingsOption = new ButtonMenuOption($"{Localizer["PlayerMenu.Settings"]}");
        SettingsOption.Click += (sender, args) =>
        {
            PlayerSettingsMenu(args.Player, settingsMenu);
            return ValueTask.CompletedTask;
        };

        // Add options to menu
        settingsMenu.AddOption(RankOption);
        settingsMenu.AddOption(SettingsOption);

        Core.MenusAPI.OpenMenuForPlayer(player, settingsMenu);
    }
    private void PlayerRankMenu(IPlayer player, IMenuAPI parentMenu)
    {
        if (player == null || !player.IsValid || !Config.PluginEnabled) return;

        var stats = GetPlayerStats(player);
        int played = (stats?.Wins ?? 0) + (stats?.Losses ?? 0);

        // Create menu
        var settingsMenu = Core.MenusAPI.CreateBuilder().EnableExit().EnableSound().SetPlayerFrozen(true)
        .Design.SetMenuTitle($"{Localizer["PlayerMenu.RankTitle"]}")
        .Design.SetGlobalScrollStyle(MenuOptionScrollStyle.CenterFixed)
        .BindToParent(parentMenu)
        .Build();

        var PlayedOption = new ButtonMenuOption($"{Localizer["PlayerMenu.Played", played]}");
        PlayedOption.Enabled = false;
        settingsMenu.AddOption(PlayedOption);

        var WinsOption = new ButtonMenuOption($"{Localizer["PlayerMenu.Wins", stats?.Wins ?? 0]}");
        WinsOption.Enabled = false;
        settingsMenu.AddOption(WinsOption);

        var LossesOption = new ButtonMenuOption($"{Localizer["PlayerMenu.Losses", stats?.Losses ?? 0]}");
        LossesOption.Enabled = false;
        settingsMenu.AddOption(LossesOption);

        var TopPlayersOption = new ButtonMenuOption($"{Localizer["PlayerMenu.TopPlayers"]}");
        TopPlayersOption.Click += (sender, args) =>
        {
            TopPlayersMenu(args.Player, settingsMenu);
            return ValueTask.CompletedTask;
        };
        settingsMenu.AddOption(TopPlayersOption);

        Core.MenusAPI.OpenMenuForPlayer(player, settingsMenu);
    }

    private void TopPlayersMenu(IPlayer player, IMenuAPI parentMenu)
    {
        if (player == null || !player.IsValid || !Config.PluginEnabled) return;

        // Create menu
        var settingsMenu = Core.MenusAPI.CreateBuilder().EnableExit().EnableSound().SetPlayerFrozen(true)
        .Design.SetMenuTitle($"<font color='lime'>{Localizer["PlayerMenu.TopPlayers"]}</font>")
        .Design.SetGlobalScrollStyle(MenuOptionScrollStyle.CenterFixed)
        .BindToParent(parentMenu)
        .Build();

        GetTopPlayersSettings(Config.Duel_TopPlayersCount, (topPlayers) =>
        {
            int rank = 1;
            foreach (var playerSettings in topPlayers)
            {
                // Player rank and name
                var playerOption = new ButtonMenuOption($"<font color='yellow'>#{rank}</font> <font color='white'>{playerSettings.PlayerName}</font>");
                playerOption.Click += (sender, args) =>
                {
                    // Show player stats for the selected player (using playerSettings from the loop)
                    int totalPlayed = playerSettings.Wins + playerSettings.Losses;
                    double winRate = totalPlayed > 0 ? (double)playerSettings.Wins / totalPlayed * 100 : 0;
                    args.Player.SendChat($"{Localizer["Chat.Stats.HeaderFooter"]}");
                    args.Player.SendChat($"{Localizer["Chat.Stats.Name", playerSettings.PlayerName]}");
                    args.Player.SendChat($"{Localizer["Chat.Stats.Played", totalPlayed]}");
                    args.Player.SendChat($"{Localizer["Chat.Stats.Wins", playerSettings.Wins]}");
                    args.Player.SendChat($"{Localizer["Chat.Stats.Losses", playerSettings.Losses]}");
                    args.Player.SendChat($"{Localizer["Chat.Stats.WinRate", $"{winRate:F2}%"]}");
                    args.Player.SendChat($"{Localizer["Chat.Stats.HeaderFooter"]}");
                    return ValueTask.CompletedTask;
                };
                settingsMenu.AddOption(playerOption);

                rank++;
            }
            Core.MenusAPI.OpenMenuForPlayer(player, settingsMenu);
        });

        
    }
    private void PlayerSettingsMenu(IPlayer player, IMenuAPI parentMenu)
    {
        if (player == null || !player.IsValid || !Config.PluginEnabled) return;

        string SelectedOption = "";
        if(PlayerOption?.ContainsKey(player) == true && PlayerOption[player].Option == 1){SelectedOption = $"{Localizer["PlayerMenu.Accept"]}";}
        if(PlayerOption?.ContainsKey(player) == true && PlayerOption[player].Option == 0){SelectedOption = $"{Localizer["PlayerMenu.Decline"]}";}
        if(PlayerOption?.ContainsKey(player) == true && PlayerOption[player].Option == -1){SelectedOption = $"{Localizer["PlayerMenu.Vote"]}";}

        // Create menu
        var settingsMenu = Core.MenusAPI.CreateBuilder().EnableExit().EnableSound().SetPlayerFrozen(true)
        .Design.SetMenuTitle($"{Localizer["MenuSettings.Title"]}")
        .Design.SetGlobalScrollStyle(MenuOptionScrollStyle.CenterFixed)
        .BindToParent(parentMenu)
        .Build();

        // Show current vote option
        var currentOption = new ButtonMenuOption($"{Localizer["PlayerMenu.Selected"]}: {SelectedOption}");
        currentOption.Enabled = false;

        // Ask for vote
        var voteOption = new ButtonMenuOption($"{Localizer["PlayerMenu.Vote"]}");
        voteOption.Click += (sender, args) =>
        {
            if(PlayerOption?.ContainsKey(player) == true) PlayerOption[player].Option = -1;
            SetPlayerDuelOption(player, -1);
            player.SendChat($"{Localizer["Chat.Prefix"]}  {Localizer["Chat.DuelSettings.Vote"]}");
            currentOption.Text = $"{Localizer["PlayerMenu.Selected"]}: {Localizer["PlayerMenu.Vote"]}";
            return ValueTask.CompletedTask;
        };
        settingsMenu.AddOption(currentOption);

        // always Accept
        var acceptOption = new ButtonMenuOption($"{Localizer["PlayerMenu.Accept"]}");
        acceptOption.Click += (sender, args) =>
        {
            if(PlayerOption?.ContainsKey(player) == true) PlayerOption[player].Option = 1;
            SetPlayerDuelOption(player, 1);
            player.SendChat($"{Localizer["Chat.Prefix"]}  {Localizer["Chat.DuelSettings.Accept"]}");
            currentOption.Text = $"{Localizer["PlayerMenu.Selected"]}: {Localizer["PlayerMenu.Accept"]}";
            return ValueTask.CompletedTask;
        };
        settingsMenu.AddOption(acceptOption);

        // always Reject
        var declineOption = new ButtonMenuOption($"{Localizer["PlayerMenu.Decline"]}");
        declineOption.Click += (sender, args) =>
        {
            if(PlayerOption?.ContainsKey(player) == true) PlayerOption[player].Option = 0;
            SetPlayerDuelOption(player, 0);
            player.SendChat($"{Localizer["Chat.Prefix"]}  {Localizer["Chat.DuelSettings.Decline"]}");
            currentOption.Text = $"{Localizer["PlayerMenu.Selected"]}: {Localizer["PlayerMenu.Decline"]}";
            return ValueTask.CompletedTask;
        };
        settingsMenu.AddOption(declineOption);

        Core.MenusAPI.OpenMenuForPlayer(player, settingsMenu);
    }
    private void DuelSettingsMenu(IPlayer player)
    {
        if (player == null || !player.IsValid || !Config.PluginEnabled || player.PlayerPawn == null) return;

        // Create menu
        var settingsMenu = Core.MenusAPI.CreateBuilder().EnableExit().EnableSound().SetPlayerFrozen(true)
        .Design.SetMenuTitle($"{Localizer["MenuSettings.Title"]}")
        .Design.SetGlobalScrollStyle(MenuOptionScrollStyle.CenterFixed)
        .Build();

        // Set T Position
        var TeleportSetT = new ButtonMenuOption($"{Localizer["MenuSettings.TeleportSetT"]}");
        TeleportSetT.Click += (sender, args) =>
        {
            if (Duel_Positions.ContainsKey(Core.Engine.GlobalVars.MapName)) // Check If Map already exist in JSON file
            {
                Dictionary<string, string> MapData = Duel_Positions[Core.Engine.GlobalVars.MapName]; // Get Map Settings
                MapData["T_Pos"] = $"{player.PlayerPawn.AbsOrigin}"; // Edit t_pos value
                File.WriteAllText(GetMapTeleportPositionConfigPath(), JsonSerializer.Serialize(Duel_Positions));
            }
            else // If Map not found in JSON file
            {
                // Save/add this in Global Variable
                Duel_Positions.Add(Core.Engine.GlobalVars.MapName, new Dictionary<string, string>{{"T_Pos", $"{player.PlayerPawn.AbsOrigin}"}, {"CT_Pos", ""}});
                File.WriteAllText(GetMapTeleportPositionConfigPath(), JsonSerializer.Serialize(Duel_Positions)); // Saving Position in File
            }
            player.SendChat($"{Localizer["Chat.Prefix"]}  {Localizer["Chat.DuelSettings.TeleportSetT", player.PlayerPawn.AbsOrigin!]}");
            return ValueTask.CompletedTask;
        };
        settingsMenu.AddOption(TeleportSetT);

        // Set CT Position
        var TeleportSetCT = new ButtonMenuOption($"{Localizer["MenuSettings.TeleportSetCT"]}");
        TeleportSetCT.Click += (sender, args) =>
        {
            if (Duel_Positions.ContainsKey(Core.Engine.GlobalVars.MapName)) // Check If Map already exist in JSON file
            {
                Dictionary<string, string> MapData = Duel_Positions[Core.Engine.GlobalVars.MapName]; // Get Map Settings
                MapData["CT_Pos"] = $"{player.PlayerPawn.AbsOrigin}"; // Edit ct_pos value
                File.WriteAllText(GetMapTeleportPositionConfigPath(), JsonSerializer.Serialize(Duel_Positions));
            }
            else // If Map not found in JSON file
            {
                // Save/add this in Global Veriable
                Duel_Positions.Add(Core.Engine.GlobalVars.MapName, new Dictionary<string, string>{{"T_Pos", ""}, {"CT_Pos", $"{player.PlayerPawn.AbsOrigin}"}});
                File.WriteAllText(GetMapTeleportPositionConfigPath(), JsonSerializer.Serialize(Duel_Positions)); // Saving Position in File
            }
            player.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.DuelSettings.TeleportSetCT", player.PlayerPawn.AbsOrigin!]}");
            return ValueTask.CompletedTask;
        };
        settingsMenu.AddOption(TeleportSetCT);

        // Delete Teleport Positions
        var TeleportDelete = new ButtonMenuOption($"{Localizer["MenuSettings.TeleportDelete"]}");
        TeleportDelete.Click += (sender, args) =>
        {
            if (Duel_Positions != null && Duel_Positions.ContainsKey(Core.Engine.GlobalVars.MapName)) // Check If Map exist in JSON file
            {
                Duel_Positions.Remove(Core.Engine.GlobalVars.MapName); // Delete Map Settings
                File.WriteAllText(GetMapTeleportPositionConfigPath(), JsonSerializer.Serialize(Duel_Positions)); // Update File
            }
            player.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.DuelSettings.TeleportDelete"]}");
            return ValueTask.CompletedTask;
        };
        settingsMenu.AddOption(TeleportDelete);
        Core.MenusAPI.OpenMenuForPlayer(player, settingsMenu);
    }

    private void ShowDuelVoteMenu(IPlayer player)
    {
        if (player == null || !player.IsValid || !Config.PluginEnabled || player.PlayerPawn == null) return;

        // Create menu
        var voteMenu = Core.MenusAPI.CreateBuilder().EnableExit().EnableSound().SetPlayerFrozen(Config.Duel_FreezePlayerOnMenuShown)
        .Design.SetMenuTitle($"{Localizer["Menu.Title"]}</font>")
        .Design.SetGlobalScrollStyle(MenuOptionScrollStyle.CenterFixed)
        .Build();

        // Accept Duel
        var acceptOption = new ButtonMenuOption($"{Localizer["Menu.Accept"]}");
        acceptOption.Click += (sender, args) =>
        {
            AcceptDuelVoteOption(args.Player);
            return ValueTask.CompletedTask;
        };
        voteMenu.AddOption(acceptOption);

        // Decline Duel
        var declineOption = new ButtonMenuOption($"{Localizer["Menu.Decline"]}");
        declineOption.Click += (sender, args) =>
        {
            DeclineDuelVoteOption(args.Player);
            return ValueTask.CompletedTask;
        };
        voteMenu.AddOption(declineOption);

        Core.MenusAPI.OpenMenuForPlayer(player, voteMenu);
    }
}