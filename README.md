# WindowsGSM.MythofEmpires
🧩WindowsGSM plugin that provides Myth of Empires Dedicated server support!

# The Game
https://store.steampowered.com/app/1371580/Myth_of_Empires/

# Requirements
WindowsGSM >= 1.21.0

# IMPORTANT INFO
- After installation change the log=123456.log to an random number or generate a number from the "PrivateServerTool".
- Change -SessionName=MyNewServer to what ever your Server should show up in the Server List.
- To give yourself and/or others Admin acces add the Steam ID's after -ServerAdminAccounts=XXXXXXXXXXXXXXXXXX and separate with ";" if there are multiple admins.

# Other Server Settings:
You can add,change or delete certain Server settings here are some examples:
-GameServerPVPType=1 (0=PVP 1=PVE)
-UseBatEye (Will enable BattleEye)
-ForceSteamNet (Will enable Steam P2P Network)
-Description="Text" (Will add a Server description but keep it short)
-NoticeSelfEnable=true (Enables/Disables welcome message)
-NoticeSelfEnterServer="Write here your Welcome Message!"
-NoticeAllEnable=true (Enables/Disables join and leave message)
-NoticeEnterServer=" has joined" (joining Message)
-NoticeLeaveServer=" has left!" (leaving Message)
-SaveGameIntervalMinute=5 (Server Save Intervall)
-InitCapitalCopper=1000 (Starting Coins for new Players)
-NormalReduceDurableMultiplier=1 (Durability Multipler for Tools/Weapons/Armors while using or getting damaged.
-NUM_AllGeneralMax=10 (Amount of Warrior a Player can recruit)
-NUM_WarGeneralMax=5 (Amount of Warriors a Player can lead at once)
-bLimitTameHorseNum=true (Enables/Disables the Limit for tamed Mounts)
-bLimitWarHorseNum=false (Enables/Disables the Limit for hourses outside of the Stable)
-SkillExpMultiplier=1
-AddExpMultiplier=1
-PlayerCollectionExpMultiplier=1
-PlayerKillMonstersExpMultiplier=1

You can find all other Server Settings in the "PrivateServerTool" and try around for yourself but it seems that not all Settings work or take affect.
