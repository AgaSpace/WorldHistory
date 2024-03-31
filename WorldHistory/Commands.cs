using Microsoft.Xna.Framework;

using Terraria;

using TShockAPI;
using TShockAPI.DB;

namespace WorldHistory
{
    public static class PluginCommands
    {
        #region Commands

        public static Command[] commands = new Command[]
        {
            new Command(PluginPermissions.GetHistory, History, "worldhistory", "worldh", "wh", 
                "tilehistory", "tileh", "th"),
            new Command(PluginPermissions.LoadPrevTiles, LoadPreviousTile, "loadprevioustile", "loadprevious", 
                "loadprev", "lp", "loadprevtile", "lpt"),
            new Command(PluginPermissions.LoadFollsTiles, LoadFollowingTile, "loadfollowingtile", "loadfollowing",
                "loadfoll", "lf", "loadfolltile", "lft")
        };

        #endregion

        #region History

        static void History(CommandArgs args)
        {
            if (args.Parameters.Count == 2)
            {
                if (!short.TryParse(args.Parameters[0], out short tileX))
                {
                    args.Player.SendErrorMessage("Failed to get X coordinate.");
                    return;
                }
                if (!short.TryParse(args.Parameters[1], out short tileY))
                {
                    args.Player.SendErrorMessage("Failed to get Y coordinate.");
                    return;
                }

                ThreadPool.QueueUserWorkItem((_) => 
                    WorldHistoryPlugin.BuildHistory(args.Player, WorldHistoryPlugin.History.Get(tileX, tileY)));
            }
            else
            {
                WorldHistoryPlugin.AwaitingHistory[args.Player.Index] = true;
                args.Player.SendInfoMessage("Hit any tile to get its history.");
            }
        }

        #endregion
        #region LoadPreviousTile

        static void LoadPreviousTile(CommandArgs args)
        {
            if (args.Parameters.Count >= 1 && args.Parameters[0].ToLower() == "help")
            {
                string prefix = Commands.Specifier + args.Message.Split(' ')[0];
                args.Player.SendInfoMessage($"{prefix} <User Account> <Time> [Radius] [Clear]");
                args.Player.SendInfoMessage("User Account - Account ID or name.");
                args.Player.SendInfoMessage("Time - Subtracted time from now. Need to restore actions only after some time.");
                args.Player.SendInfoMessage("Radius (Int16) - Radius to obtain the diagonal of a square of tiles. Originally 0, which means all over the world.");
                args.Player.SendInfoMessage("Clear (Boolean) - Whether the database needs to be purged of records. Initially true, which means auto-clear.");
                return;
            }

            if (args.Parameters.Count < 2)
            {
                args.Player.SendErrorMessage("Invalid syntax. Proper syntax in {0}{1} help.", 
                    Commands.Specifier, args.Message.Split(' ')[0]);
                return;
            }

            UserAccount? account = null;
            if (int.TryParse(args.Parameters[0], out int exceptedAccountId))
                account = TShock.UserAccounts.GetUserAccountByID(exceptedAccountId);
            else
                account = TShock.UserAccounts.GetUserAccountByName(args.Parameters[0]);

            if (account == null)
            {
                args.Player.SendErrorMessage("Unknown account.");
                return;
            }

            if (!TShock.Utils.TryParseTime(args.Parameters[1], out int seconds))
            {
                args.Player.SendErrorMessage("Invalid time string! Proper format: _d_h_m_s, with at least one time specifier.");
                args.Player.SendInfoMessage("For example, 1d and 10h-30m+2m are both valid time strings, but 2 is not.");
                return;
            }

            long time = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(seconds)).Ticks;

            int radius = 0;
            if (args.Parameters.Count >= 3 && int.TryParse(args.Parameters[2], out int newRadius))
                radius = newRadius;

            Rectangle rec;
            if (radius <= 0)
                rec = new Rectangle(0, 0, Main.maxTilesX, Main.maxTilesY);
            else
                rec = new Rectangle(args.Player.TileX - radius, args.Player.TileY - radius,
                    args.Player.TileX + radius, args.Player.TileY + radius);

            bool clear = true;
            if (args.Parameters.Count >= 4 && bool.TryParse(args.Parameters[3], out bool newClear))
                clear = newClear;

            WorldHistoryPlugin.LoadPreviousTiles(args.Player, 
                WorldHistoryPlugin.History.Get(account.ID, time, (short)rec.X, 
                (short)rec.Y, (short)rec.Width, (short)rec.Height), clear);
        }

        #endregion
        #region LoadFollowingTile

        static void LoadFollowingTile(CommandArgs args)
        {
            if (args.Parameters.Count >= 1 && args.Parameters[0].ToLower() == "help")
            {
                string prefix = Commands.Specifier + args.Message.Split(' ')[0];
                args.Player.SendInfoMessage($"{prefix} <User Account> <Time> [Radius] [Clear]");
                args.Player.SendInfoMessage("User Account - Account ID or name.");
                args.Player.SendInfoMessage("Time - Subtracted time from now. Need to restore actions only after some time.");
                args.Player.SendInfoMessage("Radius (Int16) - Radius to obtain the diagonal of a square of tiles. Originally 0, which means all over the world.");
                args.Player.SendInfoMessage("Clear (Boolean) - Whether the database needs to be purged of records. Initially false, which means there will be no auto-cleaning.");
                return;
            }

            if (args.Parameters.Count < 2)
            {
                args.Player.SendErrorMessage("Invalid syntax. Proper syntax in {0}{1} help.",
                    Commands.Specifier, args.Message.Split(' ')[0]);
                return;
            }

            UserAccount? account = null;
            if (int.TryParse(args.Parameters[0], out int exceptedAccountId))
                account = TShock.UserAccounts.GetUserAccountByID(exceptedAccountId);
            else
                account = TShock.UserAccounts.GetUserAccountByName(args.Parameters[0]);

            if (account == null)
            {
                args.Player.SendErrorMessage("Unknown account.");
                return;
            }

            if (!TShock.Utils.TryParseTime(args.Parameters[1], out int seconds))
            {
                args.Player.SendErrorMessage("Invalid time string! Proper format: _d_h_m_s, with at least one time specifier.");
                args.Player.SendInfoMessage("For example, 1d and 10h-30m+2m are both valid time strings, but 2 is not.");
                return;
            }

            long time = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(seconds)).Ticks;

            int radius = 0;
            if (args.Parameters.Count >= 3 && int.TryParse(args.Parameters[2], out int newRadius))
                radius = newRadius;

            Rectangle rec;
            if (radius <= 0)
                rec = new Rectangle(0, 0, Main.maxTilesX, Main.maxTilesY);
            else
                rec = new Rectangle(args.Player.TileX - radius, args.Player.TileY - radius,
                    args.Player.TileX + radius, args.Player.TileY + radius);

            bool clear = false;
            if (args.Parameters.Count >= 4 && bool.TryParse(args.Parameters[3], out bool newClear))
                clear = newClear;

            WorldHistoryPlugin.LoadFollowingTiles(args.Player,
                WorldHistoryPlugin.History.Get(account.ID, time, (short)rec.X,
                (short)rec.Y, (short)rec.Width, (short)rec.Height), clear);
        }

        #endregion
    }
}
