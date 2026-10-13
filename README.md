
<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>SLAYER_Duel</strong></h2>
  <h3>1v1 duels at the end of a round with different weapons</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/github/downloads/zakriamansoor47/Swiftly_SLAYER_Duel/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/zakriamansoor47/Swiftly_SLAYER_Duel?style=flat&logo=github" alt="Stars">
</p>

# Accepting Paid Requests!
Discord: Slayer47#7002

# Donation

If you like this project, consider supporting me:

<a href="https://www.buymeacoffee.com/slayer47" target="_blank">
  <img src="https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png" alt="Buy Me A Coffee" style="height: 41px !important;width: 174px !important;box-shadow: 0px 3px 2px 0px rgba(190, 190, 190, 0.5) !important;">
</a>

[![PayPal](https://www.paypalobjects.com/webstatic/mktg/logo/pp_cc_mark_37x23.jpg)](https://paypal.me/zakriamansoor)

## Description

SLAYER_Duel is a SwiftlyS2 plugin that allows the last two remaining players to participate in a **1v1 duel** at the end of a round.

The plugin offers extensive customization, including the ability to create your own **Duel Modes** using a JSON configuration file.

You can configure weapons, health, armor, player speed, gravity, special rules, rewards, and much more.

The plugin supports two different audio playback methods for duel-start sounds:

- **Workshop Sound Events + Volume API** — Plays sounds through the native CS2 sound system. Players can adjust Workshop sound volume using the global `!volume` command.
- **Swiftly Audio API** — Plays sounds through the Swiftly Audio plugin using its audio playback system.

## Requirements

- **[SwiftlyS2](https://github.com/swiftly-solution/swiftlys2)** — Required plugin framework.
- **[Volume API](https://github.com/a2Labs-cc/Volume.API)** — Required for Workshop sound volume control using `!volume`.
- **[Audio](https://github.com/SwiftlyS2-Plugins/Audio)** — Optional. Required only when using the Swiftly Audio playback method.

**Note:** The Audio plugin is not required when using Workshop Sound Events.

## Installation

1. Download the latest plugin release.
2. Upload the plugin files to your CS2 server.
3. Install the required dependencies for your selected audio playback method.
4. Edit the configuration file:

   `configs/plugins/SLAYER_Duel/SLAYER_Duel.json`

5. Configure your preferred Duel Modes and sound settings.
6. Restart the server, change the map, or reload the plugin.

**Workshop users:** Make sure the required sound files are included in your Workshop addon and the Sound Event file is configured correctly.

## Features

1. Fully customizable duel system.
2. Create custom **Duel Modes** through JSON configuration.
3. Force players into duels or allow them to vote.
4. Draw a **green laser beam** between players to help them find each other.
5. Configure punishments when the duel timer expires.
6. Customize player health, armor, movement speed, and gravity.
7. Enable **Noscope Only** and **Headshot Only** modes.
8. Give players any supported weapon.
9. Execute custom server commands when duels start and end.
10. Enable or disable bullet tracers during duels.
11. Enable or disable player beacons.
12. Add or remove **custom teleport points** using `!duel_settings` (root admins only).
13. Disable knife damage while keeping the knife available for movement or weapon switching.
14. Enable or disable duels between bots.
15. Enable or disable player-versus-bot duels.
16. Play a custom sound when a duel starts.
17. Enable infinite ammunition (unlimited clip or reserve ammo).
18. Reward duel winners with additional health, speed, or money in the next round.
19. Allow players to configure their duel voting preferences using `!duel`.
20. Track and display duel statistics.
21. Support Workshop Sound Events and the optional Swiftly Audio plugin.
22. Support Volume API for Workshop sound volume control.
23. Support translatable chat messages.

## Commands

| Command | Description |
|---------|-------------|
| `!duel` | Configure duel voting preferences and view duel statistics. |
| `!duel_settings` | Add or remove custom teleport points (root admins only). |
| `!volume` | Open the global Volume API controls for Workshop sounds. |

## Audio Playback and Volume Control

SLAYER_Duel supports two audio playback methods, selectable through the configuration file.

### 1. Workshop Sound Events (Recommended)

Set:

```jsonc
"Duel_UseAudioPlugin": false
```

- Plays sounds through the native CS2 sound system.
- Uses Workshop sound files and Sound Events.
- Does not require the Swiftly Audio plugin.
- Supports player volume control through `!volume` using Volume API.
- Does not apply the plugin's own Audio volume multiplier to Workshop playback.
- Avoids server-side VoIP audio encoding.
- Recommended for better sound quality and reduced server-side audio processing overhead.

When Workshop playback is enabled, players can use `!volume` to adjust their sound volume.

### 2. Swiftly Audio Plugin

Set:

```jsonc
"Duel_UseAudioPlugin": true
```

- Requires the Swiftly Audio plugin.
- Plays duel-start sounds through the Audio API.
- Uses the plugin's Audio-specific volume settings instead of Workshop volume control.

**Important:** The global `!volume` command is intended for Workshop sounds and does not replace Audio API volume settings.

## Configuration

Configuration file:

`configs/plugins/SLAYER_Duel/SLAYER_Duel.json`

```jsonc
{
  "PluginEnabled": true,                  // Enable or disable the plugin.
  "Duel_ForceStart": false,               // true = Forced duel, false = Players vote.
  "Duel_ShowDuelCounterIn": 1,            // 1 = Center HTML, 2 = Center Alert.
  "Duel_TopPlayersCount": 10,             // Number of top players displayed in the !duel menu.
  "Duel_FreezePlayerOnMenuShown": true,   // Freeze players while the menu is open.
  "Duel_UseAudioPlugin": false,           // false = Workshop, true = Swiftly Audio.
  "Duel_SoundEventFile": "path/your.vsndevts",
  "Duel_DrawLaserBeam": true,             // Draw a laser beam between duel players.
  "Duel_WinnerExtraHealth": 10,           // Extra health for the winner next round.
  "Duel_WinnerExtraSpeed": 0.2,           // Extra speed for the winner next round.
  "Duel_WinnerExtraMoney": 1000,          // Extra money for the winner next round.
  "Duel_Time": 30,                        // Duel duration in seconds.
  "Duel_PrepTime": 3,                     // Duel preparation time in seconds.
  "Duel_MinPlayers": 3,                   // Minimum players required (minimum value: 3).
  "Duel_DrawPunish": 3,                   // 0 = Nothing, 1 = Kill both, 2 = Kill random player,
                                        // 3 = Kill player with less health (or both if tied).
  "Duel_Beacon": true,                    // Enable player beacons.
  "Duel_Teleport": true,                  // Use custom teleport points configured with !duel_settings.
  "Duel_FreezePlayers": false,            // Freeze players during preparation.
  "Duel_BotAcceptDuel": true,             // Allow player-versus-bot duels.
  "Duel_BotsDoDuel": true,                // Allow bot-versus-bot duels.
  "Duel_DuelSoundPath": "",               // Duel-start sound path. Empty = Disabled.
                                        // Use a short sound because playback may continue after the duel ends.

  "Duel_Modes": [
    // Example of a fully customizable Duel Mode:
    //{
    //  "Weapons": "weapon_awp,weapon_knife",
    //  "CMD": "sv_autobunnyhopping 0",
    //  "CMD_End": "sv_autobunnyhopping 1",
    //  "Health": 100,
    //  "Armor": 100,
    //  "Helmet": 0,
    //  "Speed": 2.0,
    //  "Gravity": 0.2,
    //  "InfiniteAmmo": 2,
    //  "NoZoom": true,
    //  "OnlyHeadshot": true,
    //  "BulletTracers": true,
    //  "DisableKnife": true,
    //  "Name": "Awp+Noscope"
    //},

    {
      "Weapons": "weapon_knife",
      "Health": 100,
      "Armor": 0,
      "Helmet": 0,
      "CMD": "sv_autobunnyhopping 1",
      "CMD_End": "sv_autobunnyhopping 0",
      "Name": "Knife Only + Bhop"
    },
    {
      "Weapons": "weapon_knife",
      "Health": 35,
      "Armor": 0,
      "Helmet": 0,
      "Name": "35 HP + Knife Only"
    },
    {
      "Weapons": "weapon_awp,weapon_knife",
      "Health": 100,
      "Armor": 100,
      "Helmet": 0,
      "InfiniteAmmo": 2,
      "NoZoom": true,
      "BulletTracers": true,
      "DisableKnife": true,
      "Name": "Awp+Noscope"
    },
    {
      "Weapons": "weapon_ssg08,weapon_knife",
      "Health": 100,
      "Armor": 100,
      "Helmet": 0,
      "Gravity": 0.2,
      "InfiniteAmmo": 2,
      "BulletTracers": true,
      "DisableKnife": true,
      "Name": "Scout+Gravity"
    },
    {
      "Weapons": "weapon_nova,weapon_knife",
      "Health": 200,
      "Armor": 100,
      "Helmet": 0,
      "Speed": 2,
      "InfiniteAmmo": 2,
      "OnlyHeadshot": true,
      "BulletTracers": true,
      "DisableKnife": true,
      "Name": "Shotgun+Speed"
    },
    {
      "Weapons": "weapon_ak47,weapon_knife",
      "Health": 100,
      "Armor": 100,
      "Helmet": 0,
      "InfiniteAmmo": 2,
      "OnlyHeadshot": true,
      "BulletTracers": true,
      "DisableKnife": true,
      "Name": "AK47+Headshot"
    },
    {
      "Weapons": "weapon_hegrenade,weapon_knife",
      "Health": 100,
      "Armor": 100,
      "Helmet": 0,
      "InfiniteAmmo": 1,
      "BulletTracers": true,
      "DisableKnife": true,
      "Name": "Grenade Only"
    }
  ]
}
```

## Configuration Options

| Setting | Description |
|---------|-------------|
| `PluginEnabled` | Enable or disable the entire plugin. |
| `Duel_ForceStart` | Force duels or require player voting. |
| `Duel_ShowDuelCounterIn` | Select the duel countdown display type. |
| `Duel_TopPlayersCount` | Number of players shown in the duel leaderboard. |
| `Duel_FreezePlayerOnMenuShown` | Freeze players while the duel menu is open. |
| `Duel_UseAudioPlugin` | Choose Swiftly Audio (`true`) or Workshop Sound Events (`false`). |
| `Duel_SoundEventFile` | Path to the Workshop Sound Event file. |
| `Duel_DrawLaserBeam` | Enable or disable the laser beam between duel players. |
| `Duel_WinnerExtraHealth` | Additional health awarded to the winner next round. |
| `Duel_WinnerExtraSpeed` | Additional movement speed awarded to the winner next round. |
| `Duel_WinnerExtraMoney` | Additional money awarded to the winner next round. |
| `Duel_Time` | Duel duration in seconds. |
| `Duel_PrepTime` | Preparation time before a duel begins. |
| `Duel_MinPlayers` | Minimum number of players required to start a duel. |
| `Duel_DrawPunish` | Punishment applied when the duel timer expires. |
| `Duel_Beacon` | Enable or disable player beacons. |
| `Duel_Teleport` | Enable or disable custom teleport locations. |
| `Duel_FreezePlayers` | Freeze players during duel preparation. |
| `Duel_BotAcceptDuel` | Allow duels between a player and a bot. |
| `Duel_BotsDoDuel` | Allow duels between bots. |
| `Duel_DuelSoundPath` | Sound played when a duel starts. |
| `Duel_Modes` | List of custom duel modes and their rules. |

## Custom Duel Modes

You can create additional duel modes by adding new entries to the `Duel_Modes` array.

Each mode can define its own:

- Weapons and ammunition settings.
- Health and armor.
- Movement speed and gravity.
- Noscope-only or headshot-only restrictions.
- Bullet tracers and knife damage settings.
- Server commands executed at duel start and end.

The `Name` field identifies the duel mode.

## Translations

SLAYER_Duel supports customizable chat messages through its translation files.

For Workshop audio playback, the volume notification can be translated using:

```jsonc
"Chat.Duel.WorkshopVolume": "[gold]To adjust Workshop duel sound volume, use [lime]!volume[gold]."
```

When Workshop playback is enabled, the volume notification can be displayed when a player opens the `!duel` menu.

## Support

For questions, bug reports, or paid requests:

**Discord:** Slayer47#7002
