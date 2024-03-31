using System.Diagnostics;

using Terraria;
using TerrariaApi.Server;

using Microsoft.Xna.Framework;

using TShockAPI;
using TShockAPI.Hooks;
using TShockAPI.Configuration;

namespace WorldHistory
{
    [ApiVersion(2, 1)]
    public class WorldHistoryPlugin : TerrariaPlugin 
    {
        #region Data

        public override string Author => "Zoom L1";
        public override string Name => "WorldHistory";
        public override Version Version => new Version(1, 0, 1, 3);
        public WorldHistoryPlugin(Main game) : base(game) { }

        public static TileHistory History { get; private set; }
        public static ConfigFile<ConfigSettings> Config { get; private set; }
        
        public static bool[] AwaitingHistory = new bool[Main.maxPlayers];
        public static int[] TileSlopeThreshold = new int[Main.maxPlayers];

        #endregion
        #region Initialize

        public override void Initialize()
        {
            GeneralHooks.ReloadEvent += OnReload;
            OnReload(new ReloadEventArgs(TSPlayer.Server));

            History = new TileHistory();

            Observer.Initialize(this);
            FallingObserver.Initialize();

            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.GamePostUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);

            Commands.ChatCommands.AddRange(PluginCommands.commands);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GeneralHooks.ReloadEvent -= OnReload;

                Observer.Deinitialize();
                FallingObserver.Deinitialize();

                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.GamePostUpdate.Register(this, OnUpdate);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);

                PluginCommands.commands.ForEach(c => Commands.ChatCommands.Remove(c));

                History._db.Dispose();
                History = null;
                AwaitingHistory = null;
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Hooks

        #region ServerApi

        #region NetGetData

        void OnGetData(GetDataEventArgs args)
        {
            if (args.Handled)
                return;

            BinaryReader reader = args.Msg.reader;
            reader.BaseStream.Position = args.Index;

            TSPlayer player = TShock.Players[args.Msg.whoAmI];

            ref bool await = ref AwaitingHistory[args.Msg.whoAmI];

            #region PacketTypes.Tile
            if (args.MsgID == PacketTypes.Tile)
            {
                byte action = reader.ReadByte();
                short x = reader.ReadInt16();
                short y = reader.ReadInt16();

                if (await)
                {
                    args.Handled = true;

                    ThreadPool.QueueUserWorkItem((_) => BuildHistory(player, History.Get(x, y)));

                    player.SendTileSquareCentered(x, y, 1);
                    await = false;
                }
                else if (action == 7 || action == 14)
                {
                    if (!player.HasPermission(PluginPermissions.IgnoreTileSlopeThreshold) &&
                        TileSlopeThreshold[args.Msg.whoAmI]++ >= Config.Settings.TileSlopeThreshold)
                    {
                        args.Handled = true;

                        if (Config.Settings.KickOnTileSlopeThresholdBrocken)
                            player.Kick("Tile slope threshold exceeded " + Config.Settings.TileSlopeThreshold, true);
                        else
                        {
                            player.Disable("Reached TileSlope threshold.");
                            player.SendTileSquareCentered(x, y, 1);
                        }
                    }
                }
            }
            #endregion
            #region PacketTypes.MassWireOperation
            else if (args.MsgID == PacketTypes.MassWireOperation)
            {
                short x = reader.ReadInt16();
                short y = reader.ReadInt16();

                if (await)
                {
                    args.Handled = true;

                    ThreadPool.QueueUserWorkItem((_) => BuildHistory(player, History.Get(x, y)));

                    player.SendTileSquareCentered(x, y, 1);
                    await = false;
                }
            }
            #endregion
        }

        #endregion
        #region ServerLeave

        void OnLeave(LeaveEventArgs args)
        {
            AwaitingHistory[args.Who] = false;
            TileSlopeThreshold[args.Who] = 0;
        }

        #endregion
        #region GamePostUpdate

        DateTime lastUpdate = DateTime.MinValue;
        void OnUpdate(EventArgs e)
        {
            if (lastUpdate.AddSeconds(1) <= DateTime.Now)
            {
                lastUpdate = DateTime.Now;
                for (int i = 0; i < TileSlopeThreshold.Length; i++)
                    TileSlopeThreshold[i] = 0;
            }
        }

        #endregion

        #endregion
        #region TShockAPI

        #region GeneralHooks

        #region ReloadEvent

        void OnReload(ReloadEventArgs args)
        {
            string path = Path.Combine(TShock.SavePath, "WorldHistory.json");
            Config = new ConfigFile<ConfigSettings>();
            Config.Read(path, out bool write);
            if (write)
                Config.Write(path);
        }

        #endregion

        #endregion

        #endregion

        #endregion

        #region Command Helper

        #region BuildHistory

        public static bool BuildHistory(TSPlayer player, IEnumerable<TileAction> tileActions)
        {
            IEnumerable<IGrouping<int, TileAction>> actions = tileActions.GroupBy(i => i.AccountID);
            if (actions.Count() == 0)
            {
                player?.SendErrorMessage("There's no history of the tile.");
                return false;
            }
            else
            {
                foreach (IGrouping<int, TileAction> group in actions)
                {
                    player?.SendSuccessMessage("Tile history from {0} {{{1}}} ({2}).",
                        TShock.UserAccounts.GetUserAccountByID(group.Key), group.Key, group.Count());

                    foreach (TileAction action in group)
                        player?.SendInfoMessage($"[{action.Time.ToString("d-M @ HH:mm:ss")}] ({(DateTime.UtcNow - action.Time).ToString(@"d\d\.hh\h\:mm\m\:ss\s")}) @ {action.Action} >> Tile Comparison {action.Previous.Comprassion(action.Following)}");
                }
            }
            return true;
        }

        #endregion
        #region LoadPreviousTiles

        public static void LoadPreviousTiles(TSPlayer? player, IEnumerable<TileAction> actions, bool clear = true)
        {
            ThreadPool.QueueUserWorkItem((_) =>
            {
                Stopwatch sw = new Stopwatch();

                int count = actions.Count();
                if (count > 0)
                {
                    sw.Start();
                    foreach (TileAction action in actions.Reverse())
                        Main.tile[action.TileX, action.TileY] = action.Previous;

                    IEnumerable<Point> points = actions.Select(i => new Point(i.TileX, i.TileY));
                    ResetSection(points.Min(i => i.X), points.Min(i => i.Y),
                        points.Max(i => i.X), points.Max(i => i.Y));

                    if (clear)
                    {
                        IEnumerable<IEnumerable<int>> alternatelyIds = actions.Select(i => i.ID)
                        .OrderBy(i => i)
                        .Select((x, i) => new { Value = x, Index = i })
                        .GroupBy(item => item.Value - item.Index)
                        .Select(g => g.Select(item => item.Value));

                        foreach (IEnumerable<int> ids in alternatelyIds)
                        {
                            if (ids.Count() == 1)
                                History.Delete(ids.First());
                            else if (ids.Count() > 1)
                                History.Delete(ids.Min(), ids.Max());
                        }
                    }

                    sw.Stop();
                }

                player?.SendInfoMessage("Tiles ({0}) were restored in {1}.", count, sw.Elapsed.ToString(@"hh\:mm\:ss"));
            });
        }

        #endregion
        #region LoadFollowingTiles

        public static void LoadFollowingTiles(TSPlayer? player, IEnumerable<TileAction> actions, bool clear = false)
        {
            ThreadPool.QueueUserWorkItem((_) =>
            {
                Stopwatch sw = new Stopwatch();

                int count = actions.Count();
                if (count > 0)
                {
                    sw.Start();
                    foreach (TileAction action in actions)
                        Main.tile[action.TileX, action.TileY] = action.Following;

                    IEnumerable<Point> points = actions.Select(i => new Point(i.TileX, i.TileY));
                    ResetSection(points.Min(i => i.X), points.Min(i => i.Y),
                        points.Max(i => i.X), points.Max(i => i.Y));

                    if (clear)
                    {
                        IEnumerable<IEnumerable<int>> alternatelyIds = actions.Select(i => i.ID)
                        .OrderBy(i => i)
                        .Select((x, i) => new { Value = x, Index = i })
                        .GroupBy(item => item.Value - item.Index)
                        .Select(g => g.Select(item => item.Value));

                        foreach (IEnumerable<int> ids in alternatelyIds)
                        {
                            if (ids.Count() == 1)
                                History.Delete(ids.First());
                            else if (ids.Count() > 1)
                                History.Delete(ids.Min(), ids.Max());
                        }
                    }

                    sw.Stop();
                }

                player?.SendInfoMessage("Tiles ({0}) were restored in {1}.", count, sw.Elapsed.ToString(@"hh\:mm\:ss"));
            });
        }

        #endregion

        #endregion

        #region ResetSection

        static void ResetSection(int x, int y, int x2, int y2)
        {
            int lowX = Netplay.GetSectionX(x);
            int highX = Netplay.GetSectionX(x2);
            int lowY = Netplay.GetSectionY(y);
            int highY = Netplay.GetSectionY(y2);
            foreach (RemoteClient sock in Netplay.Clients.Where(s => s.IsActive))
            {
                int w = sock.TileSections.GetLength(0), h = sock.TileSections.GetLength(1);
                for (int i = lowX; i <= highX; i++)
                {
                    for (int j = lowY; j <= highY; j++)
                    {
                        if (i < 0 || j < 0 || i >= w || j >= h) { continue; }
                        sock.TileSections[i, j] = false;
                    }
                }
            }
        }

        #endregion
    }
}