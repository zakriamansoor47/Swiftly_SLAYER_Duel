using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Commands;

namespace SLAYER_Duel;
// Used these to remove compile warnings
#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8619

public partial class SLAYER_Duel : BasePlugin
{
    // Commands
    [Command("duel")]
	public void PlayerDuelSettings(ICommandContext command)
	{
        var player = command.Sender;
        if (!Config.PluginEnabled || player == null || !player.IsValid) return;

        PlayerDuelSettingsMenu(player);
    }

    [Command("duel_settings", permission: "admin.root")]
	public void DuelSettings(ICommandContext command)
	{
        var player = command.Sender;
        if (!Config.PluginEnabled || player == null || !player.IsValid) return;
		
        DuelSettingsMenu(player);
    }
}