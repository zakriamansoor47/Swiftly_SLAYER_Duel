using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Natives;

namespace SLAYER_Duel;

public partial class SLAYER_Duel : BasePlugin
{
    public void DrawBeaconOnPlayer(IPlayer? player)
    {
        if(player == null || !player.IsValid || player.Pawn == null || player.PlayerPawn == null || player.Pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE || player.PlayerPawn.AbsOrigin == null)return;
        
        Vector mid =  new Vector(player.PlayerPawn.AbsOrigin.Value.X ,player.PlayerPawn.AbsOrigin.Value.Y, player.PlayerPawn.AbsOrigin.Value.Z);

        int lines = 20;
        int[] ent = new int[lines];
        CBeam?[] beam_ent = new CBeam?[lines];

        // draw piecewise approx by stepping angle
        // and joining points with a dot to dot
        float step = (float)(2.0f * Math.PI) / (float)lines;
        float radius = 20.0f;

        float angle_old = 0.0f;
        float angle_cur = step;

        float BeaconTimerSecond = 0.0f;

        
        for(int i = 0; i < lines; i++) // Drawing Beacon Circle
        {
            Vector start = angle_on_circle(angle_old, radius, mid);
            Vector end = angle_on_circle(angle_cur, radius, mid);

            if(player.Controller.TeamNum == 2)
            {
                var result = DrawLaserBetween(start, end, Color.FromBuiltin(System.Drawing.Color.Red), 1.0f, 2.0f);
                ent[i] = result.Item1;
                beam_ent[i] = result.Item2;
            } 
            if(player.Controller.TeamNum == 3)
            {
                var result = DrawLaserBetween(start, end, Color.FromBuiltin(System.Drawing.Color.Blue), 1.0f, 2.0f);
                ent[i] = result.Item1;
                beam_ent[i] = result.Item2;
            }

            angle_old = angle_cur;
            angle_cur += step;
        }
        
        Core.Scheduler.DelayAndRepeatBySeconds(0.1f, 0.1f, ()=>
        {
            if (BeaconTimerSecond >= 0.9f)
            {
                return;
            }
            for(int i = 0; i < lines; i++) // Moving Beacon Circle
            {
                Vector start = angle_on_circle(angle_old, radius, mid);
                Vector end = angle_on_circle(angle_cur, radius, mid);

                TeleportLaser(beam_ent[i], start, end);

                angle_old = angle_cur;
                angle_cur += step;
            }
            radius += 10;
            BeaconTimerSecond += 0.1f;
        });
        PlaySoundOnPlayer(player, "sounds/tools/sfm/beep.vsnd_c");
        return;
    }
    private void PlaySoundOnPlayer(IPlayer? player, String sound)
    {
        if(player == null || !player.IsValid)return;
        player.ExecuteCommand($"play {sound}");
        
    }
    private static readonly Vector VectorZero = new Vector(0, 0, 0);
    private static readonly QAngle RotationZero = new QAngle(0, 0, 0);
    public (int, CBeam?) DrawLaserBetween(Vector startPos, Vector endPos, Color color, float life, float width)
    {
        var beam = Core.EntitySystem.CreateEntityByDesignerName<CBeam>("beam");

        if (beam == null)
        {
            Core.Logger.LogError($"Failed to create beam...");
            return (-1, null);
        }

        beam.Render = color;
        beam.Width = width;

        beam.Teleport(startPos, RotationZero, VectorZero);
        beam.EndPos.X = endPos.X;
        beam.EndPos.Y = endPos.Y;
        beam.EndPos.Z = endPos.Z;
        beam.DispatchSpawn();

        Core.Scheduler.DelayBySeconds(life, () => {if(beam != null && beam.IsValid) beam.Despawn(); }); // destroy beam after specific time

        return ((int)beam.Index, beam);
    }
    public void TeleportLaser(CBeam? laser,Vector start, Vector end)
    {
        if(laser == null || !laser.IsValid)return;
        // set pos
        laser.Teleport(start, RotationZero, VectorZero);
        // end pos
        // NOTE: we cant just move the whole vec
        laser.EndPos.X = end.X;
        laser.EndPos.Y = end.Y;
        laser.EndPos.Z = end.Z;
        laser.EndPosUpdated();
    }
    private float CalculateDistance(Vector point1, Vector point2)
    {
        float dx = point2.X - point1.X;
        float dy = point2.Y - point1.Y;
        float dz = point2.Z - point1.Z;

        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
    private Vector angle_on_circle(float angle, float radius, Vector mid)
    {
        // {r * cos(x),r * sin(x)} + mid
        // NOTE: we offset Z so it doesn't clip into the ground
        return new Vector((float)(mid.X + (radius * Math.Cos(angle))),(float)(mid.Y + (radius * Math.Sin(angle))), mid.Z + 6.0f);
    }
    private void TeleportPlayer(CCSPlayerController? player)
	{
        
		if (Duel_Positions?.ContainsKey(Core.Engine.GlobalVars.MapName) == true) // If Map Exist in File
		{
            if(player == null || !player.IsValid || player.Pawn.Value == null || player.PlayerPawn.Value == null || player.Pawn.Value.LifeState != (byte)LifeState_t.LIFE_ALIVE)return; // If player is not Valid then return
            var TeleportPosition = GetPositionFromFile(player.TeamNum); // Get Teleport Position From JSON file
            if(TeleportPosition != null)player.PlayerPawn.Value.Teleport(TeleportPosition, player.PlayerPawn.Value.AngVelocity, new Vector(0f, 0f, 0f)); // Teleport Player to That position
        }
        else return; // If Map not Exist in File then do nothing
    }
    public static CCSGameRules GetGameRules()
    {
        return Core.EntitySystem.GetAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
    }
    private void FreezePlayer(CCSPlayerController? player)
    {
        if(player == null || !player.IsValid || player.PlayerPawn.Value == null)return;
        player.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_OBSOLETE;
        player.PlayerPawn.Value.ActualMoveType = MoveType_t.MOVETYPE_OBSOLETE;

        player.PlayerPawn.Value.MoveTypeUpdated();
        player.PlayerPawn.Value.TakesDamage = false;
    }
    private void UnFreezePlayer(CCSPlayerController? player)
    {
        if(player == null || !player.IsValid || player.PlayerPawn.Value == null)return;
        player.PlayerPawn.Value.MoveType = MoveType_t.MOVETYPE_WALK;
        player.PlayerPawn.Value.ActualMoveType = MoveType_t.MOVETYPE_WALK;
        player.PlayerPawn.Value.MoveTypeUpdated();
        player.PlayerPawn.Value.TakesDamage = true;
    }
    private void ApplyInfiniteClip(CCSPlayerController player)
    {
        var activeWeaponHandle = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;
        if (activeWeaponHandle != null && activeWeaponHandle.IsValid)
        {
            activeWeaponHandle.Clip1 = 100;
        }
    }

    private void ApplyInfiniteReserve(CCSPlayerController player)
    {
        var activeWeaponHandle = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;
        if (activeWeaponHandle != null && activeWeaponHandle.IsValid)
        {
            activeWeaponHandle.ReserveAmmo[0] = 100;
        }
    }
    
    private void RemoveObjectives()
    {
        foreach (var entity in Core.EntitySystem.GetAllEntities().Where(entity => entity != null && entity.IsValid))
        {
            if (entity.DesignerName == "c4" ||
                entity.DesignerName == "hostage_entity")
            {
                entity.Despawn();
            }

        }
    }
}