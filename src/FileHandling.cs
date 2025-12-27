using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Natives;
using System.Text.Json;

namespace SLAYER_Duel;

public partial class SLAYER_Duel : BasePlugin
{
    private string GetMapTeleportPositionConfigPath()
    {
        if (Core.Configuration.BasePathExists)
        {
            return $"{Core.Configuration.BasePath}/Duel_TeleportPositions.json";
        }
        return $"{Core.PluginPath}/Duel_TeleportPositions.json";
    }
    private Vector? GetPositionFromFile(int TeamNum)
    {
        var mapData = Duel_Positions[Core.Engine.GlobalVars.MapName]; // Get Current Map Teleport Positions
        if (TeamNum == 2 && mapData.ContainsKey("T_Pos") && mapData["T_Pos"] != "") // If player team is Terrorist then get the T_Pos from File
        {
            string[] Positions = mapData["T_Pos"].Split(" "); // Split Coordinates with space " "
            return new Vector(float.Parse(Positions[0]), float.Parse(Positions[1]), float.Parse(Positions[2])); // Return Coordinates in Vector
        }
        else if(TeamNum == 3 && mapData.ContainsKey("CT_Pos") && mapData["CT_Pos"] != "") // If player team is C-Terrorist then get the CT_Pos from File
        {
            string[] Positions = mapData["CT_Pos"].Split(" "); // Split Coordinates with space " "
            return new Vector(float.Parse(Positions[0]), float.Parse(Positions[1]), float.Parse(Positions[2])); // Return Coordinates in Vector
        }
        return null;
    }
    private void LoadPositionsFromFile()
    {
        if (!File.Exists(GetMapTeleportPositionConfigPath()))
		{
			return;
		}
		
		var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(GetMapTeleportPositionConfigPath()));
		
		if(data != null)
		{
			Duel_Positions = data;
		}
    }
}