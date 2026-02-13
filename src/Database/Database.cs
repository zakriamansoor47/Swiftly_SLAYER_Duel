using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using Dapper;
using SwiftlyS2.Shared.Players;

namespace SLAYER_Duel;

public partial class SLAYER_Duel : BasePlugin
{
    public class PlayerSettings
    {
        public string PlayerName { get; set; } = "";
        public int Option { get; set; } = -1;
        public int Wins { get; set; } = 0;
        public int Losses { get; set; } = 0;
    }

    private void LoadPlayerSettings(IPlayer player)
    {
        var steamId = player.SteamID;
        if (PlayerOption == null) PlayerOption = new Dictionary<IPlayer, PlayerSettings>();
        if (PlayerOption?.ContainsKey(player) == false) PlayerOption[player] = new PlayerSettings();
        PlayerOption![player].PlayerName = player.Controller.PlayerName;

        Task.Run(async () =>
        {
            try
            {
                using var connection = Core.Database.GetConnection(Config.Duel_DatabaseConnection);
                connection.Open();

                var result = await connection.QueryFirstOrDefaultAsync(@"SELECT `option`, `wins`, `losses` FROM `SLAYER_Duel` WHERE `steamid` = @SteamId;",
                new
                {
                    SteamId = steamId
                });

                Core.Scheduler.NextTick(() =>
                {
                    PlayerOption![player].Option = Convert.ToInt32($"{result?.option ?? -1}");
                    PlayerOption[player].Wins = Convert.ToInt32($"{result?.wins ?? 0}");
                    PlayerOption[player].Losses = Convert.ToInt32($"{result?.losses ?? 0}");
                });

                // Update player name in database when they connect
                await connection.ExecuteAsync(@"INSERT INTO `SLAYER_Duel` (`steamid`, `name`, `option`, `wins`, `losses`) VALUES (@SteamId, @Name, -1, 0, 0)
                    ON DUPLICATE KEY UPDATE `name` = @Name;",
                    new
                    {
                        SteamId = steamId,
                        Name = PlayerOption![player].PlayerName
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SLAYER_Duel] Error on PlayerConnectFull while retrieving player data: {ex.Message}");
                Core.Logger.LogError($"[SLAYER_Duel] Error on PlayerConnectFull while retrieving player data: {ex.Message}");
            }
        });
    }

    private void SetPlayerDuelOption(IPlayer? player, int choice)
    {
        if (player == null || player.IsValid == false || PlayerOption == null) return;

        var steamId = player.SteamID;

        // Update local settings
        if (PlayerOption?.ContainsKey(player) == true)
        {
            PlayerOption[player].Option = choice;
            PlayerOption![player].PlayerName = player.Controller.PlayerName;
        }

        Task.Run(async () =>
        {
            try
            {
                using var connection = Core.Database.GetConnection(Config.Duel_DatabaseConnection);
                connection.Open();

                await connection.ExecuteAsync(@"
                    INSERT INTO `SLAYER_Duel` (`steamid`, `name`, `option`, `wins`, `losses`) VALUES (@SteamId, @Name, @Option, 0, 0)
                    ON DUPLICATE KEY UPDATE `name` = @Name, `option` = @Option;",
                    new
                    {
                        SteamId = steamId,
                        Name = PlayerOption![player].PlayerName,
                        Option = choice
                    });
                connection.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SLAYER_Duel] Error while saving player 'Option': {ex.Message}");
                Core.Logger.LogError($"[SLAYER_Duel] Error while saving player 'Option': {ex.Message}");
            }
        });
    }

    private void AddPlayerWin(IPlayer? player)
    {
        if (player == null || !player.IsValid || PlayerOption == null) return;

        var steamId = player.SteamID;

        // Update local settings
        var PlayerName = "";
        if (PlayerOption?.ContainsKey(player) == true)
        {
            PlayerOption[player].Wins++;
            PlayerName = PlayerOption[player].PlayerName;
        }
        else
        {   
            PlayerName = player.Controller.PlayerName;
        }

        // Connection
        using var connection = Core.Database.GetConnection(Config.Duel_DatabaseConnection);
        connection.Open();

        Task.Run(async () =>
        {
            try
            {
                using var connection = Core.Database.GetConnection(Config.Duel_DatabaseConnection);
                connection.Open();

                await connection.ExecuteAsync(@"
                    INSERT INTO `SLAYER_Duel` (`steamid`, `name`, `option`, `wins`, `losses`) VALUES (@SteamId, @Name, -1, 1, 0)
                    ON DUPLICATE KEY UPDATE `name` = @Name, `wins` = `wins` + 1;",
                    new
                    {
                        SteamId = steamId,
                        Name = PlayerName
                    });
                connection.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SLAYER_Duel] Error while updating player wins: {ex.Message}");
                Core.Logger.LogError($"[SLAYER_Duel] Error while updating player wins: {ex.Message}");
            }
        });
    }

    private void AddPlayerLoss(IPlayer? player)
    {
        if (player == null || !player.IsValid || PlayerOption == null) return;

        var steamId = player.SteamID;

        // Update local settings
        var PlayerName = "";
        if (PlayerOption?.ContainsKey(player) == true)
        {
            PlayerOption[player].Losses++;
            PlayerName = PlayerOption[player].PlayerName;
        }
        else
        {   
            PlayerName = player.Controller.PlayerName;
        }

        // Connection
        using var connection = Core.Database.GetConnection(Config.Duel_DatabaseConnection);
        connection.Open();
        
        Task.Run(async () =>
        {
            try
            {
                using var connection = Core.Database.GetConnection(Config.Duel_DatabaseConnection);
                connection.Open();

                await connection.ExecuteAsync(@"
                    INSERT INTO `SLAYER_Duel` (`steamid`, `name`, `option`, `wins`, `losses`) VALUES (@SteamId, @Name, -1, 0, 1)
                    ON DUPLICATE KEY UPDATE `name` = @Name, `losses` = `losses` + 1;",
                    new
                    {
                        SteamId = steamId,
                        Name = PlayerName
                    });
                connection.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SLAYER_Duel] Error while updating player losses: {ex.Message}");
                Core.Logger.LogError($"[SLAYER_Duel] Error while updating player losses: {ex.Message}");
            }
        });
    }

    private void SetPlayerStats(IPlayer? player, int wins, int losses)
    {
        if (player == null || !player.IsValid) return;

        var steamId = player.SteamID;

        // Update local settings
        var PlayerName = "";
        if (PlayerOption?.ContainsKey(player) == true)
        {
            PlayerOption[player].Wins = wins;
            PlayerOption[player].Losses = losses;
            PlayerName = player.Controller.PlayerName;
        }
        else
        {
            PlayerName = player.Controller.PlayerName;
        }

        // Connection
        using var connection = Core.Database.GetConnection(Config.Duel_DatabaseConnection);
        connection.Open();

        Task.Run(async () =>
        {
            try
            {
                using var connection = Core.Database.GetConnection(Config.Duel_DatabaseConnection);
                connection.Open();

                await connection.ExecuteAsync(@"
                    INSERT INTO `SLAYER_Duel` (`steamid`, `name`, `option`, `wins`, `losses`) VALUES (@SteamId, @Name, -1, @Wins, @Losses)
                    ON DUPLICATE KEY UPDATE `name` = @Name, `wins` = @Wins, `losses` = @Losses;",
                    new
                    {
                        SteamId = steamId,
                        Name = PlayerName,
                        Wins = wins,
                        Losses = losses
                    });
                    
                connection.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SLAYER_Duel] Error while setting player stats: {ex.Message}");
                Core.Logger.LogError($"[SLAYER_Duel] Error while setting player stats: {ex.Message}");
            }
        });
    }

    private PlayerSettings? GetPlayerStats(IPlayer? player)
    {
        if (player == null || !player.IsValid || PlayerOption?.ContainsKey(player) != true)
            return null;

        return PlayerOption[player];
    }
    
    private void GetTopPlayersSettings(int limit, Action<List<PlayerSettings>> callback)
    {
        // Connection
        using var connection = Core.Database.GetConnection(Config.Duel_DatabaseConnection);
        connection.Open();
        
        Task.Run(async () =>
        {
            var topPlayersSettings = new List<PlayerSettings>();

            try
            {
                using var connection = Core.Database.GetConnection(Config.Duel_DatabaseConnection);
                connection.Open();

                var result = await connection.QueryAsync(@"
                    SELECT `steamid`, `name`, `option`, `wins`, `losses` 
                    FROM `SLAYER_Duel` 
                    WHERE `wins` > 0 
                    ORDER BY `wins` DESC, `losses` ASC 
                    LIMIT @Limit;",
                    new { Limit = limit });

                var dbResults = new List<(ulong steamId, string name, int option, int wins, int losses)>();
                
                // Store database results first
                foreach (var row in result)
                {
                    dbResults.Add((
                        Convert.ToUInt64(row.steamid),
                        Convert.ToString(row.name) ?? "",
                        Convert.ToInt32(row.option),
                        Convert.ToInt32(row.wins),
                        Convert.ToInt32(row.losses)
                    ));
                }

                // Switch back to main thread to process results
                Core.Scheduler.NextTick(() =>
                {
                    foreach (var (steamId, storedName, option, wins, losses) in dbResults)
                    {
                        // Use stored name from database, but try to get updated name from online players if available
                        string playerName = storedName;
                        var onlinePlayer = Core.PlayerManager.GetAllPlayers().FirstOrDefault(p => 
                            p != null && p.IsValid && p.SteamID == steamId);

                        if (onlinePlayer != null)
                        {
                            playerName = onlinePlayer.Controller.PlayerName;
                        }
                        else if (string.IsNullOrEmpty(storedName))
                        {
                            playerName = $"[{steamId}]";
                        }

                        var playerSettings = new PlayerSettings
                        {
                            PlayerName = playerName,
                            Option = option,
                            Wins = wins,
                            Losses = losses
                        };

                        topPlayersSettings.Add(playerSettings);
                    }

                    callback(topPlayersSettings);
                });
                connection.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SLAYER_Duel] Error retrieving top players settings from database: {ex.Message}");
                Core.Logger.LogError($"[SLAYER_Duel] Error retrieving top players settings from database: {ex.Message}");
                
                // Even on error, call the callback on main thread
                Core.Scheduler.NextTick(() =>
                {
                    callback(topPlayersSettings);
                });
            }
        });
    }
}