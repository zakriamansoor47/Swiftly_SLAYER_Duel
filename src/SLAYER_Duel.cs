using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Convars;
using SwiftlyS2.Shared.Sounds;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Translation;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.ProtobufDefinitions;
using Dapper;
using System.Data;

namespace SLAYER_Duel;

#pragma warning disable CS9107

public class SLAYER_DuelConfig
{
    public bool PluginEnabled { get; set; } = true;
    public bool Duel_ForceStart { get; set; } = false;
    public int Duel_ShowDuelCounterIn { get; set; } = 1;
    public int Duel_TopPlayersCount { get; set; } = 10;
    public bool Duel_FreezePlayerOnMenuShown { get; set; } = true;
    public bool Duel_DrawLaserBeam { get; set; } = true;
    public bool Duel_BotAcceptDuel { get; set; } = true;
    public bool Duel_BotsDoDuel { get; set; } = true;
    public int Duel_WinnerExtraHealth { get; set; } = 10;
    public float Duel_WinnerExtraSpeed { get; set; } = 0.2f;
    public int Duel_WinnerExtraMoney { get; set; } = 1000;
    public int Duel_Time { get; set; } = 30;
    public int Duel_PrepTime { get; set; } = 3;
    public int Duel_MinPlayers { get; set; } = 3;
    public int Duel_DrawPunish { get; set; } = 3;
    public bool Duel_Beacon { get; set; } = true;
    public bool Duel_Teleport { get; set; } = true;
    public bool Duel_FreezePlayers { get; set; } = false;
    public string Duel_DuelSoundPath { get; set; } = "";
    public string Duel_DatabaseConnection { get; set; } = "local"; // name from database.jsonc
    public List<DuelModeSettings> Duel_Modes { get; set; } = new List<DuelModeSettings>();
}
public class DuelModeSettings
{
    public bool BulletTracers { get; set; } = true;
    public string Name { get; set; } = "";
    public string Weapons { get; set; } = "weapon_knife";
    public string CMD { get; set; } = "";
    public string CMD_End { get; set; } = "";
    public int Health { get; set; } = 100;
    public int Armor { get; set; } = 0;
    public int Helmet { get; set; } = 0;
    public float Speed { get; set; } = 1.0f;
    public float Gravity { get; set; } = 1.0f;
    public int InfiniteAmmo { get; set; } = 2;
    public bool NoZoom { get; set; } = false;
    public bool Only_headshot { get; set; } = false;
    public bool DisableKnife { get; set; } = false;
}

[PluginMetadata
(
    Id = "SLAYER_Duel", 
    Version = "1.0", 
    Name = "SLAYER_Duel", 
    Author = "SLAYER", 
    Description = "1vs1 Duel at the end of the round with different weapons"
)]
public partial class SLAYER_Duel(ISwiftlyCore core) : BasePlugin(core)
{
    // Use static Core to access across plugin files if not using partial class
    public static new ISwiftlyCore Core { get; private set; } = null!;
    private ServiceProvider? _provider;
    private static SLAYER_DuelConfig Config { get; set; } = new();
    private ILocalizer Localizer => core.Localizer;
    Dictionary<IPlayer, PlayerSettings> PlayerOption = new Dictionary<IPlayer, PlayerSettings>();
    Dictionary<string, List<string>> playerSavedWeapons = new Dictionary<string, List<string>>();
    List<int> LastDuelNums = new List<int>();
    Dictionary<string, Dictionary<string, string>> Duel_Positions = new Dictionary<string, Dictionary<string, string>>();

    IDbConnection _connection = null!;
    public bool[] g_Zoom = new bool[64];
    public bool g_BombPlanted = false;
    public bool g_DuelStarted = false;
    public bool g_PrepDuel = false;
    public bool g_DuelNoscope = false;
    public bool g_DuelHSOnly = false;
    public bool g_DuelDisableKnife = false;
    public bool g_DuelBullettracers = false;
    public bool g_IsDuelPossible = true;
    public bool g_IsVoteStarted = false;
    public bool[] PlayersDuelVoteOption = new bool[2];
    public float g_PrepTime;
    public float g_DuelTime;
    public int SelectedMode;
    public IPlayer[] Duelist = new IPlayer[2];
    public string SelectedDuelModeName = "";
    public string DuelWinner = "";
    public float mp_death_drop_gun_value;
    public float mp_buytime_value;
    // Timers
    public CancellationTokenSource? t_PrepDuel;
    public CancellationTokenSource? t_DuelTimer;
    Dictionary<IPlayer, CancellationTokenSource?> PlayerBeaconTimer = new Dictionary<IPlayer, CancellationTokenSource?>();
    Dictionary<IPlayer, (int, bool)> PlayerArmorBeforeDuel = new Dictionary<IPlayer, (int, bool)>();
    public override void Load(bool hotReload) 
    {
        // Initialize configuration
        Core.Configuration.InitializeJsonWithModel<SLAYER_DuelConfig>("SLAYER_DuelConfig.jsonc", "Main")
        .Configure(builder =>
        {
            builder.AddJsonFile("SLAYER_DuelConfig.jsonc", optional: false, reloadOnChange: true);
        });

        // Register configuration with dependency injection
        ServiceCollection services = new();
        services.AddSwiftly(Core).AddOptionsWithValidateOnStart<SLAYER_DuelConfig>().BindConfiguration("Main");

        _provider = services.BuildServiceProvider();

        Config = _provider.GetRequiredService<IOptions<SLAYER_DuelConfig>>().Value;

        // Check database connection
        _connection = Core.Database.GetConnection(Config.Duel_DatabaseConnection);
        _connection.Open();

        if(hotReload)
        {
            foreach (var player in Core.PlayerManager.GetAllPlayers().Where(player => player != null && player.IsValid))
            {
                LoadPlayerSettings(player);
            }
        }
        Task.Run(async () =>
        {
            await _connection.ExecuteAsync(@"CREATE TABLE IF NOT EXISTS `SLAYER_Duel` (`steamid` UNSIGNED BIG INT NOT NULL,`name` TEXT NOT NULL DEFAULT '',`option` INT NOT NULL DEFAULT -1,`wins` INT NOT NULL DEFAULT 0,`losses` INT NOT NULL DEFAULT 0, PRIMARY KEY (`steamid`));");
            
            // Migration: Add name column to existing databases
            try
            {
                await _connection.ExecuteAsync(@"ALTER TABLE `SLAYER_Duel` ADD COLUMN `name` TEXT NOT NULL DEFAULT '';");
            }
            catch
            {
                // Column already exists, ignore the error
            }
        });
        LoadPositionsFromFile();

        // Listers
        Core.Event.OnMapLoad += (mapname) =>
        {
            LoadPositionsFromFile();
            if(t_PrepDuel != null) t_PrepDuel.Cancel();
            if(t_DuelTimer != null) t_DuelTimer.Cancel();
            PlayerBeaconTimer?.Clear();
        };
        Core.Event.OnTick += () =>
        {
            if(!Config.PluginEnabled)return;
            foreach (var player in Core.PlayerManager.GetAllPlayers().Where(player => player != null && player.IsValid))
            {
                if(g_PrepDuel)
                {
                    if(Config.Duel_ShowDuelCounterIn == 1)
                    {
                        player.SendCenterHTML
                        (
                            $"{Localizer["CenterHtml.DuelPrep"]}" +
                            $"{Localizer["CenterHtml.DuelPrepTime", g_PrepTime]}"
                        );
                    }
                    else
                    {
                        player.SendAlert
                        (
                            $"{Localizer["CenterAlert.DuelPrep"]}" +
                            $"{Localizer["CenterAlert.DuelPrepTime", g_PrepTime]}"
                        );
                    }
                }
                if(g_DuelStarted)
                {
                    if(Config.Duel_ShowDuelCounterIn == 1)
                    {
                        player.SendCenterHTML
                        (
                            $"{Localizer["CenterHtml.DuelEnd"]}" +
                            $"{Localizer["CenterHtml.DuelEndTime", g_DuelTime]}"
                        );
                    }
                    else
                    {
                        player.SendAlert
                        (
                            $"{Localizer["CenterAlert.DuelEnd"]}" +
                            $"{Localizer["CenterAlert.DuelEndTime", g_DuelTime]}"
                        );
                    }
                    if(g_DuelNoscope && player.Pawn?.LifeState == (byte)LifeState_t.LIFE_ALIVE && player.PlayerPawn != null && player.PlayerPawn.IsValid && player.PlayerPawn.WeaponServices?.MyWeapons != null)
                    {
                        if(player.PlayerPawn.WeaponServices!.MyWeapons.Count != 0 && player.PlayerPawn.WeaponServices!.ActiveWeapon.Value != null) // Noscope
                        {
                            var ActiveWeaponName = player.PlayerPawn.WeaponServices!.ActiveWeapon.Value.DesignerName;
                            if(ActiveWeaponName.Contains("weapon_ssg08") || ActiveWeaponName.Contains("weapon_awp")
                            || ActiveWeaponName.Contains("weapon_scar20") || ActiveWeaponName.Contains("weapon_g3sg1"))
                            {
                                player.PlayerPawn.WeaponServices!.ActiveWeapon.Value.NextSecondaryAttackTick.Value = Core.Engine.GlobalVars.TickCount + 500;
                                var buttons = player.PressedButtons;
                                if(!g_Zoom[player.Slot] && (buttons & GameButtonFlags.Mouse2) != 0)
                                {
                                    g_Zoom[player.Slot] = true;
                                }
                                else if(g_Zoom[player.Slot] && (buttons & GameButtonFlags.Mouse2) == 0)
                                {
                                    g_Zoom[player.Slot] = false;
                                }
                                
                            }
                        }
                    }
                }
                
            }
        };

        // Events
        Core.GameEvent.HookPost<EventPlayerConnectFull>((@event) =>
        {
            var player = @event.UserIdPlayer;
            if(!Config.PluginEnabled || player == null || !player.IsValid)return HookResult.Continue;
            
            LoadPlayerSettings(player);
            return HookResult.Continue;
        });
        Core.GameEvent.HookPost<EventBulletImpact>((@event) =>
        {
            CCSPlayerController player = @event.UserIdController;
            if (!Config.PluginEnabled || !g_DuelStarted || !g_DuelBullettracers || player == null || !player.IsValid || player.PlayerPawn.Value == null || !player.PlayerPawn.IsValid || player.PlayerPawn.Value.AbsOrigin == null)
                return HookResult.Continue;

            Vector PlayerPosition = player.PlayerPawn.Value.AbsOrigin.Value;
            Vector BulletOrigin = new Vector(PlayerPosition.X, PlayerPosition.Y, PlayerPosition.Z+64);//bulletOrigin.X += 50.0f;
            Vector bulletDestination = new Vector(@event.X, @event.Y, @event.Z);
            if(player.TeamNum == 3) DrawLaserBetween(BulletOrigin, bulletDestination, Color.FromBuiltin(System.Drawing.Color.Blue), 0.5f, 2.0f);
            else if(player.TeamNum == 2) DrawLaserBetween(BulletOrigin, bulletDestination, Color.FromBuiltin(System.Drawing.Color.Red), 0.5f, 2.0f);
        
            return HookResult.Continue;
        });
        Core.GameEvent.HookPost<EventRoundStart>((@event) =>
        {
            Duelist = new IPlayer[2];
            g_PrepDuel = false;
            g_DuelStarted = false;
            g_IsDuelPossible = true;
            g_IsVoteStarted = false;
            g_BombPlanted = false;
            if(t_PrepDuel != null) t_PrepDuel.Cancel();
            if(t_DuelTimer != null) t_DuelTimer.Cancel();

            foreach(var timer in PlayerBeaconTimer.Values)
            {
                if(timer != null)timer.Cancel();
            }

            PlayerBeaconTimer?.Clear();
            PlayerArmorBeforeDuel?.Clear();
            return HookResult.Continue;
        });
        Core.GameEvent.HookPost<EventRoundEnd>((@event) =>
        {
            if(g_IsVoteStarted) 
            {
                core.MenusAPI.CloseAllMenus();
            }
            g_PrepDuel = false;
            g_DuelStarted = false;
            g_IsDuelPossible = false;
            g_IsVoteStarted = false;
            if(t_PrepDuel != null)t_PrepDuel.Cancel();
            Core.Engine.ExecuteCommand("mp_default_team_winner_no_objective -1"); // Set to default after duel
            return HookResult.Continue;
        });
        Core.GameEvent.HookPost<EventWeaponFire>((@event) =>
        {
            var player = @event.UserIdController;
            if(!Config.PluginEnabled || !g_DuelStarted || player == null || !player.IsValid)return HookResult.Continue;

            // Unlimited Reserve Ammo
            if(GetDuelItem(SelectedDuelModeName).InfiniteAmmo == 1)
            {
                ApplyInfiniteClip(player);
            }
            else if(GetDuelItem(SelectedDuelModeName).InfiniteAmmo == 2)
            {
                ApplyInfiniteReserve(player);
            }
            return HookResult.Continue;
        });
        Core.GameEvent.HookPost<EventGrenadeThrown>((@event) =>
        {
            var player = @event.UserIdController;
            if(!Config.PluginEnabled || !g_DuelStarted || player == null || !player.IsValid)return HookResult.Continue;
            if(GetDuelItem(SelectedDuelModeName).InfiniteAmmo < 1)return HookResult.Continue;

            // Unlimited Grenade
            string weaponname = @event.Weapon;
            Core.Scheduler.NextTick(() =>
            {
                player.PlayerPawn.Value?.ItemServices?.As<CCSPlayer_ItemServices>().GiveItem<CEntityInstance>($"weapon_{weaponname}");
            });
            return HookResult.Continue;
        });
        Core.GameEvent.HookPost<EventBombPlanted>((@event) =>
        {
            g_BombPlanted = true;
            if(!g_DuelStarted)
            {
                g_IsDuelPossible = false;
            }
            if(g_IsVoteStarted) 
            {
                core.MenusAPI.CloseAllMenus();
            }
            return HookResult.Continue;
        });
        Core.GameEvent.HookPost<EventBombExploded>((@event) =>
        {
            g_BombPlanted = true;
            if(!g_DuelStarted)
            {
                g_IsDuelPossible = false;
            }
            if(g_IsVoteStarted) 
            {
                core.MenusAPI.CloseAllMenus();
            }
            return HookResult.Continue;
        });
        Core.GameEvent.HookPost<EventHostageFollows>((@event) =>
        {
            var player = @event.UserIdController;
            if(player == null || !player.IsValid)return HookResult.Continue;

            //PlayerRescuingHostage[player] = true; // Set Player is Rescuing Hostage
            if(!g_DuelStarted)
            {
                g_IsDuelPossible = false;
            }
            return HookResult.Continue;
        });
        Core.GameEvent.HookPost<EventPlayerSpawn>((@event) =>
        {
            var player = @event.UserIdController;
            if(!Config.PluginEnabled || player == null || !player.IsValid || player.PlayerPawn.Value == null)return HookResult.Continue;

            // Kill player if he spawn during duel
            if(g_PrepDuel || g_DuelStarted)player.PlayerPawn.Value.CommitSuicide(false, true);

            if(DuelWinner != "" && $"{player.SteamID}" == DuelWinner) // Duel Winner
            {
                DuelWinner = ""; // Reset Duel Winner
                Core.Scheduler.NextTick(() =>
                {
                    player.PlayerPawn.Value!.Health += Config.Duel_WinnerExtraHealth; // give extra Health to winner
                    player.InGameMoneyServices!.Account += Config.Duel_WinnerExtraMoney; // Give extra money to winner
                    player.InGameMoneyServicesUpdated();
                    player.PlayerPawn.Value!.VelocityModifier += Config.Duel_WinnerExtraSpeed; // Give extra speed to winner
                });
            }
            return HookResult.Continue;
        });
        Core.GameEvent.HookPost<EventPlayerHurt>((@event) =>
        {
            if(!Config.PluginEnabled || !g_DuelStarted || !g_DuelHSOnly && !g_DuelDisableKnife)return HookResult.Continue;

            var player = @event.UserIdController;
            var attacker = @event.AttackerPlayer;
            if(player == null || attacker == null || !player.IsValid || !attacker.IsValid)return HookResult.Continue;
            // Some Checks to validate Attacker
            if (player.TeamNum == attacker.Controller.TeamNum && !(@event.DmgHealth > 0 || @event.DmgArmor > 0))return HookResult.Continue;

            if(g_DuelHSOnly && @event.HitGroup != (byte)HitGroup_t.HITGROUP_HEAD) // if headshot is enabled and bullet not hitting on Head
            {
                player.PlayerPawn.Value!.Health += @event.DmgHealth; // add the dmg health to Normal health
                player.PlayerPawn.Value.ArmorValue += @event.DmgArmor; // Update the Armor as well
            }

            if(g_DuelDisableKnife && @event.Weapon.Contains("knife") || @event.Weapon.Contains("bayonet")) // if DisableKnife is Enabled then Disable Knife Damage
            {
                player.PlayerPawn.Value!.Health += @event.DmgHealth; // add the dmg health to Normal health
                player.PlayerPawn.Value.ArmorValue += @event.DmgArmor; // Update the Armor as well
                attacker.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.Duel.Knife"]}"); // Send Message to attacker
            }
            return HookResult.Continue;
        });
        Core.GameEvent.HookPost<EventPlayerDeath>((@event) =>
        {
            if(!Config.PluginEnabled || g_BombPlanted)return HookResult.Continue; // Plugin should be Enable
            int ctplayer = 0, tplayer = 0, totalplayers = 0;
            // Count Players in Both Team on Any Player Death
            foreach (var player in Core.PlayerManager.GetAllPlayers().Where(player => player != null && player.IsValid))
            {
                if(player.Pawn?.LifeState == (byte)LifeState_t.LIFE_ALIVE && player.Controller.TeamNum == 2 && !player.Controller.ControllingBot)tplayer++;
                if(player.Pawn?.LifeState == (byte)LifeState_t.LIFE_ALIVE && player.Controller.TeamNum == 3 && !player.Controller.ControllingBot)ctplayer++;
                //if(player.Pawn.Value.LifeState == (byte)LifeState_t.LIFE_ALIVE && g_DuelStarted)player.RemoveWeapons();
                totalplayers++;
            }
            CCSGameRules gamerules = GetGameRules();
            if(!g_IsDuelPossible)return HookResult.Continue;
            if(!gamerules.WarmupPeriod && totalplayers >= Config.Duel_MinPlayers && ctplayer == 1 && tplayer == 1) // 1vs1 Situation and its not warmup
            {
                if(Config.Duel_ForceStart) // If Force Start Duel is true
                {
                    RemoveObjectives(); // Remove Objectives from Map
                    Core.Scheduler.DelayBySeconds(0.1f, ()=> PrepDuel());
                }
                else // if force start duel is false
                {
                    PlayersDuelVoteOption[0] = false; PlayersDuelVoteOption[1] = false;
                    foreach (var player in Core.PlayerManager.GetAllPlayers().Where(player => player != null && player.IsValid && player.Pawn?.LifeState == (byte)LifeState_t.LIFE_ALIVE && !player.Controller.ControllingBot))
                    {
                        // keep track of duelist
                        if(player.Controller.TeamNum == 2){Duelist[0] = player;}
                        if(player.Controller.TeamNum == 3){Duelist[1] = player;}
                        if(player.IsFakeClient && Config.Duel_BotAcceptDuel) // Check Player is BOT and Bot allowed to Duel
                        {
                            if(player.Controller.TeamNum == 2)PlayersDuelVoteOption[0] = true;
                            else if(player.Controller.TeamNum == 3)PlayersDuelVoteOption[1] = true;
                            if(PlayersDuelVoteOption[0] && PlayersDuelVoteOption[1] && Config.Duel_BotsDoDuel)PrepDuel(); // Start Duel Between Bots if Duel_BotsDoDuel is Enabled in Config file
                        }
                        else if(player.IsFakeClient && !Config.Duel_BotAcceptDuel) // Check Player is BOT and Bot is not allowed to Duel
                        {
                            if(player.Controller.TeamNum == 2)PlayersDuelVoteOption[0] = false;
                            else if(player.Controller.TeamNum == 3)PlayersDuelVoteOption[1] = false;
                        }
                        else // Voting
                        {
                            if(PlayerOption.ContainsKey(player) && PlayerOption[player].Option == 1) // if `1` is set in Database, then always accept duel without vote 
                            {
                                if(player.Controller.TeamNum == 2){PlayersDuelVoteOption[0] = true; Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.AcceptedDuel.T", player.Controller.PlayerName]}");}
                                if(player.Controller.TeamNum == 3){PlayersDuelVoteOption[1] = true; Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.AcceptedDuel.CT", player.Controller.PlayerName]}");}
                            }
                            else if(PlayerOption.ContainsKey(player) && PlayerOption[player].Option == 0) // if `0` is set in Database, then always decline duel without vote 
                            {
                                if(player.Controller.TeamNum == 2){PlayersDuelVoteOption[0] = false; Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.RejectedDuel.T", player.Controller.PlayerName]}");}
                                if(player.Controller.TeamNum == 3){PlayersDuelVoteOption[1] = false; Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.RejectedDuel.CT", player.Controller.PlayerName]}");}
                            }
                            else  // if `-1` is set in Database, then start vote 
                            {
                                g_IsVoteStarted = true;
                                ShowDuelVoteMenu(player);
                            }
                            if(!g_IsVoteStarted) // if both players Duel Vote option is saved in Database then it means vote was not started for any player
                            {
                                if(PlayersDuelVoteOption[0] && PlayersDuelVoteOption[1]) // Both accepted duel
                                {
                                    Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.AcceptedDuel.Both"]}");
                                    PrepDuel(); // Start Duel
                                }
                                else if(!PlayersDuelVoteOption[0] && !PlayersDuelVoteOption[1]) // Both rejected Duel
                                {
                                    Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.RejectedDuel.Both"]}");
                                }
                            }
                        }
                    }
                    
                }
            }
            else if(ctplayer == 0 || tplayer == 0)
            {
                g_DuelStarted = false;
                g_IsDuelPossible = false;
            }
            return HookResult.Continue;
        });
    }

    private void AcceptDuelVoteOption(IPlayer player)
    {
        if (player == null || !player.IsValid || player.IsFakeClient || !g_IsDuelPossible || g_BombPlanted || !g_IsVoteStarted || !Duelist.Contains(player)) return;

        if (player.Controller.TeamNum == 2)
        {
            if (Config.Duel_FreezePlayerOnMenuShown) UnFreezePlayer(player.Controller);
            PlayersDuelVoteOption[0] = true;
            Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.AcceptedDuel.T", player.Controller.PlayerName]}");
        }
        else if (player.Controller.TeamNum == 3)
        {
            if (Config.Duel_FreezePlayerOnMenuShown) UnFreezePlayer(player.Controller);
            PlayersDuelVoteOption[1] = true;
            Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.AcceptedDuel.CT", player.Controller.PlayerName]}");
        }

        if (PlayersDuelVoteOption[0] && PlayersDuelVoteOption[1])
        {
            g_IsVoteStarted = false; // Both Accepted the Duel Vote, So no need to Exit Menu at Round End
            Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.AcceptedDuel.Both"]}");
            RemoveObjectives(); // Remove Objectives from Map
            Core.Scheduler.DelayBySeconds(0.1f, () => PrepDuel());
        }
    }
    private void DeclineDuelVoteOption(IPlayer player)
    {
        if (player == null || !player.IsValid || player.IsFakeClient || !g_IsDuelPossible || g_BombPlanted || !g_IsVoteStarted || !Duelist.Contains(player))return;
        
        if(player.Controller.TeamNum == 2)
        {
            if(Config.Duel_FreezePlayerOnMenuShown)UnFreezePlayer(player.Controller);
            PlayersDuelVoteOption[0] = false;
            Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.RejectedDuel.T", player.Controller.PlayerName]}");
        }
        else if(player.Controller.TeamNum == 3)
        {
            if(Config.Duel_FreezePlayerOnMenuShown)UnFreezePlayer(player.Controller);
            PlayersDuelVoteOption[1] = false;
            Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.RejectedDuel.CT", player.Controller.PlayerName]}");
        }
        if(!PlayersDuelVoteOption[0] && !PlayersDuelVoteOption[1])
        {
            g_IsVoteStarted = false; // Both Rejected the Duel Vote, So no need to Exit Menu at Round End
            Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.RejectedDuel.Both"]}");
        }
    }
    public void RemoveAllWeaponsFromMap()
    {
        try
        {
            foreach (var weapon in Core.EntitySystem.GetAllEntities().Where(weapon => weapon != null && weapon.IsValid && (weapon.DesignerName.StartsWith("weapon_") || weapon.DesignerName.StartsWith("hostage_entity"))))
            {
                weapon.Despawn();
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogError($"[SLAYER Duel] Error on Removing Weapon: {ex.Message}");
        }
    }
    
    public void PrepDuel()
    {
        if(g_PrepDuel)return;
        g_PrepDuel = true;
        foreach (var player in Core.PlayerManager.GetAllPlayers().Where(player => player != null && player.IsValid && player.Controller.TeamNum > 0))
        {
            
            if(!player.IsFakeClient && Config.Duel_DuelSoundPath != "")PlaySoundOnPlayer(player, Config.Duel_DuelSoundPath); // Play Duel Sound to all players except Bots if any Given
            if(player.Controller.TeamNum > 1 && player.Pawn?.LifeState == (byte)LifeState_t.LIFE_ALIVE) // Check Players who are in any Team and alive (only two duelist will be alive)
            {
                if(Config.Duel_Teleport)TeleportPlayer(player.Controller);
                if(Config.Duel_FreezePlayers)FreezePlayer(player.Controller);   // Freeze Player
                SavePlayerWeapons(player); // first save player weapons
                player.PlayerPawn?.ItemServices?.As<CCSPlayer_ItemServices>().RemoveItems(); // then remove weapons from player
                if(Config.Duel_Beacon) // If Beacon Enabled
                {
                    // Initialize the dictionary entry if not present
                    if(PlayerBeaconTimer == null)PlayerBeaconTimer = new Dictionary<IPlayer, CancellationTokenSource?>(); // Initialize if null
                    if(!PlayerBeaconTimer.ContainsKey(player)) PlayerBeaconTimer[player] = null; // Add the key if not present
                    if(PlayerBeaconTimer[player] != null)PlayerBeaconTimer[player]?.Cancel(); // Kill Timer if running
                    // Start Beacon
                    PlayerBeaconTimer[player] = Core.Scheduler.DelayAndRepeatBySeconds(1f, 1.0f, ()=>
                    {
                        if(!player.IsValid || player.Pawn?.LifeState != (byte)LifeState_t.LIFE_ALIVE || !g_DuelStarted && !g_PrepDuel)
                        {
                            if(PlayerBeaconTimer != null && PlayerBeaconTimer.ContainsKey(player) &&  PlayerBeaconTimer[player] != null)PlayerBeaconTimer[player]?.Cancel(); // Kill Timer if player dies or leaves
                        } 
                        else DrawBeaconOnPlayer(player);
                    });
                }
            }
        }

        RemoveAllWeaponsFromMap(); // then remove all weapons from Map (this can also remove weapon from players but animation glitch)
       
        g_PrepTime = Config.Duel_PrepTime;
        g_DuelTime = Config.Duel_Time;
        
        Random DuelMode = new Random();
        do
        {
            SelectedMode = DuelMode.Next(0, Config.Duel_Modes.Count);
        } while(LastDuelNums.Count != 0 && LastDuelNums.Contains(SelectedMode) && LastDuelNums.Count < Config.Duel_Modes.Count - 1);
        LastDuelNums.Add(SelectedMode);
        if(LastDuelNums.Count == Config.Duel_Modes.Count-1)LastDuelNums.Clear();

        var mp_buytime = Core.ConVar.Find<float>("mp_buytime");
        if(mp_buytime != null) mp_buytime_value = mp_buytime.Value;
        Core.Engine.ExecuteCommand("mp_buytime 0"); // Disable BuyZone during Duel

        t_PrepDuel = Core.Scheduler.DelayAndRepeatBySeconds(0.2f, 0.2f, PrepDuelTimer); // start Duel Prepration Timer
    }
    public void PrepDuelTimer()
    {
        if (g_PrepTime <= 0.0f)
        {
            if(g_IsDuelPossible)
            {
                var SelectedModeName = Config.Duel_Modes.ElementAt(SelectedMode);
                SelectedDuelModeName = SelectedModeName.Name;
                Core.PlayerManager.SendChat($" {Color.FromBuiltin(System.Drawing.Color.Green)}★ {Color.FromBuiltin(System.Drawing.Color.DarkRed)}-----------------------------------------------------------------------");
                Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.Duel.Started", SelectedModeName.Name]}");
                Core.PlayerManager.SendChat($" {Color.FromBuiltin(System.Drawing.Color.Green)}★ {Color.FromBuiltin(System.Drawing.Color.DarkRed)}-----------------------------------------------------------------------");
                StartDuel(SelectedModeName.Name);
                g_PrepDuel = false;
                g_DuelStarted = true;
                t_DuelTimer = Core.Scheduler.DelayAndRepeatBySeconds(0.2f, 0.2f, DuelStartedTimer); 
            }
            t_PrepDuel?.Cancel();
            return;
        }
        CreateLaserBeamBetweenPlayers(0.2f); // Create Laser Beam
        g_PrepTime = g_PrepTime - 0.2f;
    }
    public void StartDuel(string DuelModeName)
    {
        if(!g_IsDuelPossible || !Config.Duel_Modes.Contains(GetDuelItem(DuelModeName)))return;
        string[] weapons = GetDuelItem(DuelModeName).Weapons.Split(",");
        string[] Commands = GetDuelItem(DuelModeName).CMD.Split(",");
        
        g_DuelNoscope = GetDuelItem(DuelModeName).NoZoom;
        g_DuelHSOnly = GetDuelItem(DuelModeName).Only_headshot;
        g_DuelBullettracers = GetDuelItem(DuelModeName).BulletTracers;
        g_DuelDisableKnife = GetDuelItem(DuelModeName).DisableKnife;
        
        var mp_death_drop_gun = Core.ConVar.Find<float>("mp_death_drop_gun");
        if (mp_death_drop_gun != null) mp_death_drop_gun_value = mp_death_drop_gun.Value; // Get Convar Int value
        if(mp_death_drop_gun_value != 0) Core.Engine.ExecuteCommand("mp_death_drop_gun 0");
        Core.Engine.ExecuteCommand("mp_ignore_round_win_conditions 0");
        
        foreach(var cmd in Commands.Where(commands => commands != "")) // Execute Duel Start Commands
        {
            Core.Engine.ExecuteCommand(cmd);
        }
        foreach (var player in Core.PlayerManager.GetAllPlayers().Where(player => player != null && player.IsValid && player.PlayerPawn != null && player.PlayerPawn.LifeState == (byte)LifeState_t.LIFE_ALIVE))
        {
            player.PlayerPawn!.Health = GetDuelItem(DuelModeName).Health;
            player.PlayerPawn.VelocityModifier = GetDuelItem(DuelModeName).Speed;
            player.PlayerPawn.GravityScale *= GetDuelItem(DuelModeName).Gravity;
            if(GetDuelItem(DuelModeName).Helmet < 1)player.PlayerPawn.ArmorValue = GetDuelItem(DuelModeName).Armor;
            else player.PlayerPawn.ItemServices!.GiveItem("item_assaultsuit");
            foreach(var weapon in weapons) // Give Each Weapon
            {
                player.PlayerPawn.ItemServices!.GiveItem(weapon);
            }
            if(Config.Duel_FreezePlayers) UnFreezePlayer(player.Controller); // Unfreeze Player
        }
    }
    public void DuelStartedTimer()
    {
        float roundtimeleft = GetGameRules().RoundStartTime.Value + GetGameRules().RoundTime - Core.Engine.GlobalVars.CurrentTime;
        if(roundtimeleft <= 0.4f && roundtimeleft >= 0f)
        {
            Core.Engine.ExecuteCommand("mp_ignore_round_win_conditions 1");
        }
        if(g_DuelTime <= 0f || !g_DuelStarted || !g_IsDuelPossible)
        {
            EndDuel();
            t_DuelTimer?.Cancel();
            return;
        }
        CreateLaserBeamBetweenPlayers(0.2f); // Create Laser Beam
        g_DuelTime = g_DuelTime - 0.2f;
    }
    public void EndDuel()
    {
        g_IsDuelPossible = false;
        string Winner = "";
        bool IsCTWon = false;
        g_PrepDuel = false;
        g_DuelStarted = false;
        if(t_DuelTimer != null)t_DuelTimer?.Cancel();
        IPlayer? CT = null, T = null;
        int CTHealth = 0, THealth = 0;
        Random randomplayer = new Random();
        int killplayer = randomplayer.Next(0,2);
        
        foreach (var player in Core.PlayerManager.GetAllPlayers().Where(player => player != null && player.IsValid && player.Controller.TeamNum > 0 && player.Pawn?.LifeState == (byte)LifeState_t.LIFE_ALIVE && g_DuelTime <= 0f))
        {
            if(Config.Duel_DrawPunish == 1) // Kill Both if timer ends
            {
                Core.Scheduler.NextTick(() => player.PlayerPawn?.CommitSuicide(false, true));
            }
            else if(Config.Duel_DrawPunish == 2) // Kill Random if timer ends
            {
                if(killplayer == 0 && player.Controller.TeamNum == 2){Core.Scheduler.NextTick(() => player.PlayerPawn?.CommitSuicide(false, true));}
                else if(killplayer == 1 && player.Controller.TeamNum == 3){Core.Scheduler.NextTick(() => player.PlayerPawn?.CommitSuicide(false, true));}
            }
            else if(Config.Duel_DrawPunish == 3) // Kill who has minimum HP if timer ends
            {
                if(player.Controller.TeamNum == 2){THealth = player.PlayerPawn?.Health ?? 0;T = player;} // save T player
                if(player.Controller.TeamNum == 3){CTHealth = player.PlayerPawn?.Health ?? 0;CT = player;} // save CT player
                if(CT != null && T != null && CT.IsValid && T.IsValid && CT.Pawn?.LifeState == (byte)LifeState_t.LIFE_ALIVE && T.Pawn?.LifeState == (byte)LifeState_t.LIFE_ALIVE)
                {
                    if(CTHealth < THealth){Core.Scheduler.NextTick(() => CT.PlayerPawn?.CommitSuicide(false, true));} 
                    else if(CTHealth > THealth){Core.Scheduler.NextTick(() => T.PlayerPawn?.CommitSuicide(false, true));}
                    else if(CTHealth == THealth) // if no damage given then kill both
                    {
                        Core.Scheduler.NextTick(() => CT.PlayerPawn?.CommitSuicide(false, true));
                        Core.Scheduler.NextTick(() => T.PlayerPawn?.CommitSuicide(false, true));
                    }
                }
            }
        }
        foreach (var duelist in Duelist.Where(player => player != null && player.IsValid && player.Controller.TeamNum > 0 && player.Pawn?.LifeState == (byte)LifeState_t.LIFE_ALIVE))
        {
            if (Winner == "")
            {
                Winner = duelist.Controller.PlayerName;  // Save the name of the alive Player
                DuelWinner = $"{duelist.SteamID}";
                IsCTWon = duelist.Controller.TeamNum == 3 ? true : false;

                AddPlayerWin(duelist);
                // Add loss to the other duelist
                var loser = Duelist.FirstOrDefault(d => d != null && d != duelist && d.IsValid);
                if (loser != null)
                {
                    AddPlayerLoss(loser);
                }
            }
            else // If Winner is already saved its mean 2 players are alived after the Duel. Then remove the Winner.
            {
                Winner = "";
            } 
            GiveBackSavedWeaponsToPlayers(duelist);
        }
        Core.PlayerManager.SendChat($" {Color.FromBuiltin(System.Drawing.Color.Green)}★ {Color.FromBuiltin(System.Drawing.Color.DarkRed)}-----------------------------------------------------------------------");
        if(Winner != ""){Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]}  {Localizer["Chat.Duel.EndWins", Winner]}");}
        else Core.PlayerManager.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.Duel.EndDraw"]}");
        Core.PlayerManager.SendChat($" {Color.FromBuiltin(System.Drawing.Color.Green)}★ {Color.FromBuiltin(System.Drawing.Color.DarkRed)}-----------------------------------------------------------------------");

        string[] Commands = GetDuelItem(SelectedDuelModeName).CMD_End.Split(",");
        if(mp_death_drop_gun_value != 0)Core.Engine.ExecuteCommand($"mp_death_drop_gun {mp_death_drop_gun_value}");
        if(mp_buytime_value != 0)Core.Engine.ExecuteCommand($"mp_buytime {mp_buytime_value}");

        foreach(var cmd in Commands.Where(commands => commands != "")) // Execute Duel End Commands
        {
            Core.Engine.ExecuteCommand(cmd);
        }
        
        // A new clever way to end round (when roundtime is already 0) and add team scores, instead of using TerminateRound
        if(Winner == "")Core.Engine.ExecuteCommand("mp_default_team_winner_no_objective 0"); // 0 = Round Draw
        else if(!IsCTWon)Core.Engine.ExecuteCommand("mp_default_team_winner_no_objective 2"); // 2 = Terrorist Wins
        else if(IsCTWon)Core.Engine.ExecuteCommand("mp_default_team_winner_no_objective 3"); // 3 = Counter Terrorist Wins 
        Core.Scheduler.NextTick(() => Core.Engine.ExecuteCommand("mp_ignore_round_win_conditions 0"));
    }
    private DuelModeSettings GetDuelItem(string DuelModeName)
    {
        DuelModeSettings duelMode = GetDuelModeByName(DuelModeName, StringComparer.OrdinalIgnoreCase);
        return duelMode;
    }
    private void SavePlayerWeapons(IPlayer? player)
    {
        if(player == null || !player.IsValid || player.PlayerPawn == null || !player.PlayerPawn.IsValid || player.PlayerPawn.LifeState != (byte)LifeState_t.LIFE_ALIVE || player.PlayerPawn.WeaponServices?.MyWeapons == null) return;
        // Initialize the list for the current player
        playerSavedWeapons[player.UserID.ToString()] = new List<string>();
        // Get Player Weapons
        foreach (var weapon in player.PlayerPawn.WeaponServices.MyWeapons.Where(weapons => weapons.IsValid && weapons.Value != null && weapons.Value.DesignerName != null))
        {
            if(weapon.Value != null) playerSavedWeapons?[player.UserID.ToString()]?.Add($"{weapon.Value.DesignerName}");
        }
        // Save Player Armor and Helmet
        if(player.PlayerPawn.ItemServices != null && player.PlayerPawn.ItemServices.HasHelmet)
        {
            PlayerArmorBeforeDuel[player] = (player.PlayerPawn.ArmorValue, true); // Save Player Armor + helmet before Duel
        }
        else PlayerArmorBeforeDuel[player] = (player.PlayerPawn.ArmorValue, false); // Save only Player Armor before Duel
    }
    private void GiveBackSavedWeaponsToPlayers(IPlayer? player)
    {
        if(player == null || !player.IsValid || player.PlayerPawn == null || player.PlayerPawn.LifeState != (byte)LifeState_t.LIFE_ALIVE )return;
        player.PlayerPawn?.ItemServices?.As<CCSPlayer_ItemServices>().RemoveItems(); // Remove Weapons from player
        if (playerSavedWeapons?.TryGetValue(player.UserID.ToString(), out var savedWeapons) == true)
        {
            foreach(var weapon in savedWeapons)
            {
                player.PlayerPawn?.ItemServices?.GiveItem($"{weapon}");
            }
        }

        // Give back Player Armor and Helmet
        if(PlayerArmorBeforeDuel.ContainsKey(player) && PlayerArmorBeforeDuel[player].Item2) // if player has helmet then give back armor + helmet
        {
            player.PlayerPawn?.ArmorValue = PlayerArmorBeforeDuel[player].Item1;
            player.PlayerPawn?.ItemServices?.HasHelmet = true;
        }
        else if(PlayerArmorBeforeDuel.ContainsKey(player) && PlayerArmorBeforeDuel[player].Item2 == false) // if player has no helmet then give back only armor
        {
            player.PlayerPawn?.ArmorValue = PlayerArmorBeforeDuel[player].Item1;
            player.PlayerPawn?.ItemServices?.HasHelmet = false;
        }
        player.PlayerPawn?.ItemServicesUpdated();
    }
    private void CreateLaserBeamBetweenPlayers(float time)
    {
        if(!Config.Duel_DrawLaserBeam || !g_PrepDuel && !g_DuelStarted)return;
        Vector CTPlayerPosition = Vector.Zero, TPlayerPosition = Vector.Zero;
        foreach (var player in Core.PlayerManager.GetAllPlayers().Where(player => player != null && player.IsValid && player.Pawn?.LifeState == (byte)LifeState_t.LIFE_ALIVE))
        {
            if(Config.Duel_DrawLaserBeam && player.Pawn?.LifeState == (byte)LifeState_t.LIFE_ALIVE) // if Draw laser beam is true in Config then Get alive players
            {
                if(player.Controller.TeamNum == 3) CTPlayerPosition = player.PlayerPawn!.AbsOrigin!.Value;
                if(player.Controller.TeamNum == 2) TPlayerPosition = player.PlayerPawn!.AbsOrigin!.Value;
            }
        }
        CTPlayerPosition = new Vector(CTPlayerPosition.X, CTPlayerPosition.Y, CTPlayerPosition.Z+50);
        TPlayerPosition = new Vector(TPlayerPosition.X, TPlayerPosition.Y, TPlayerPosition.Z+50);
        float TotalDistance = CalculateDistance(CTPlayerPosition, TPlayerPosition);
        if(TotalDistance > 700.0f) // Create Beam if Distance is Greater then this
        {
            // Create Beam if it was not already Created
            DrawLaserBetween(CTPlayerPosition, TPlayerPosition, Color.FromBuiltin(System.Drawing.Color.Green), time, 2.0f);
        }
    }
    public List<DuelModeSettings> GetDuelModes()
    {
        return Config.Duel_Modes;
    }
    
    public DuelModeSettings GetDuelModeByName(string modeName, StringComparer comparer)
    {
        return Config.Duel_Modes.FirstOrDefault(mode => comparer.Equals(mode.Name, modeName))!;
    }

    public override void Unload()
    {
        // Properly dispose of database connection
        try
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SLAYER_Duel] Error closing database connection: {ex.Message}");
        }
    }
} 