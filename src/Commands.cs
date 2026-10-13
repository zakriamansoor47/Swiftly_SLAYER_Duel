using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Commands;

namespace SLAYER_Duel;

public partial class SLAYER_Duel : BasePlugin
{
    // Commands
    [Command("duel")]
	public void PlayerDuelSettings(ICommandContext command)
	{
        var player = command.Sender;
        if (!Config.PluginEnabled || player == null || !player.IsValid) return;

        if (_audioApi == null)
            player.SendChat($"{Localizer["Chat.Prefix"]} {Localizer["Chat.Duel.WorkshopVolume"]}");

        PlayerDuelSettingsMenu(player);
    }

	public void DuelSettings(ICommandContext command)
	{
        var player = command.Sender;
        if (!Config.PluginEnabled || player == null || !player.IsValid || !Core.Permission.PlayerHasPermission(player.SteamID, Config.Duel_AdminFlag)) return;
		
        DuelSettingsMenu(player);
    }
}