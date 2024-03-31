using Microsoft.Xna.Framework;

using Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using Terraria.DataStructures;
using Terraria.GameContent.UI;
using TerrariaApi.Server;

using TShockAPI;

namespace WorldHistory
{
    public static class Observer
    {
        #region Data

        private static TerrariaPlugin plugin;
        static int WhoAmI = -1;
        static string? Action;
        static Dictionary<Point, ITile>? History;

        #endregion
        #region Initialize

        public static void Initialize(TerrariaPlugin observer)
        {
            plugin = observer;
            ServerApi.Hooks.NetGetData.Register(observer, OnGetData, int.MinValue);

            On.Terraria.WorldGen.TileFrame += WorldGen_TileFrame;
            On.Terraria.WorldGen.KillTile += WorldGen_KillTile;
            On.Terraria.Wiring.MassWireOperationStep += Wiring_MassWireOperationStep;

            On.Terraria.Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float += OnNewProjectile;
        }
        public static void Deinitialize()
        {
            ServerApi.Hooks.NetGetData.Deregister(plugin, OnGetData);

            On.Terraria.WorldGen.TileFrame -= WorldGen_TileFrame;
            On.Terraria.WorldGen.KillTile -= WorldGen_KillTile;
            On.Terraria.Wiring.MassWireOperationStep -= Wiring_MassWireOperationStep;

            On.Terraria.Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float -= OnNewProjectile;
        }

        #endregion

        #region Handler

        #region PreHandle

        static void PreHandle(int whoAmI, string action, Point point)
        {
            WhoAmI = whoAmI;
            Action = action;
            History = new Dictionary<Point, ITile>
            {
                { point, new Tile(point.GetTileOnPosition()) }
            };
        }

        #endregion
        #region PostHandle

        static void PostHandle()
        {
            if (WhoAmI == -1 || History == null || Action == null)
                return;

            Dictionary<Point, Tuple<ITile, ITile>> terms = History
                .Where(i => !i.Value.isTheSameAs(i.Key.GetTileOnPosition()))
                .ToDictionary(i => i.Key, i => new Tuple<ITile, ITile>(i.Value, new Tile(i.Key.GetTileOnPosition())));

            if (terms.Count() > 0)
            {
                int accountId = TShock.Players[WhoAmI].Account.ID;
                string action = Action;
                long ticks = DateTime.UtcNow.Ticks;

                ThreadPool.QueueUserWorkItem((_) =>
                {
                    foreach (KeyValuePair<Point, Tuple<ITile, ITile>> pair in terms)
                    {
                        TileAction tileAction = new TileAction()
                        {
                            ID = -1,

                            AccountID = accountId,
                            Ticks = ticks,

                            TileX = (short)pair.Key.X,
                            TileY = (short)pair.Key.Y,

                            Action = action,

                            Previous = pair.Value.Item1,
                            Following = pair.Value.Item2
                        };
                        WorldHistoryPlugin.History.Add(tileAction);
                    }
                });
            }

            WhoAmI = -1;
            Action = null;
            History = null;
        }

        #endregion

        #endregion

        #region OnGetData

        static void OnGetData(GetDataEventArgs args)
        {
            if (args.Handled)
                return;

            BinaryReader reader = args.Msg.reader;
            reader.BaseStream.Position = args.Index;

            #region PacketTypes.Tile
            if (args.MsgID == PacketTypes.Tile)
            {
                args.Handled = true;

                byte action = reader.ReadByte();
                short x = reader.ReadInt16();
                short y = reader.ReadInt16();
                short type = reader.ReadInt16();
                int style = reader.ReadByte();

                bool fail = type == 1;

                if (!WorldGen.InWorld(x, y, 3))
                    return;

                PreHandle(args.Msg.whoAmI, ((GetDataHandlers.EditAction)action).ToString(), x.GetPoint(y));

                if (Main.tile[x, y] == null)
                    Main.tile[x, y] = new Tile();

                if (!fail)
                {
                    if (action == 0 || action == 2 || action == 4)
                    {
                        Netplay.Clients[args.Msg.whoAmI].SpamDeleteBlock += 1f;
                    }
                    if (action == 1 || action == 3)
                    {
                        Netplay.Clients[args.Msg.whoAmI].SpamAddBlock += 1f;
                    }
                }
                if (!Netplay.Clients[args.Msg.whoAmI].TileSections[Netplay.GetSectionX(x), Netplay.GetSectionY(y)])
                    fail = true;

                if (action == 0)
                {
                    WorldGen.KillTile(x, y, fail);
                }
                bool flag13 = false;
                if (action == 1)
                {
                    bool forced = true;
                    if (WorldGen.CheckTileBreakability2_ShouldTileSurvive(x, y))
                    {
                        flag13 = true;
                        forced = false;
                    }
                    WorldGen.PlaceTile(x, y, type, mute: false, forced, -1, style);
                }
                if (action == 2)
                {
                    WorldGen.KillWall(x, y, fail);
                }
                if (action == 3)
                {
                    WorldGen.PlaceWall(x, y, type);
                }
                if (action == 4)
                {
                    WorldGen.KillTile(x, y, fail, effectOnly: false, noItem: true);
                }
                if (action == 5)
                {
                    WorldGen.PlaceWire(x, y);
                }
                if (action == 6)
                {
                    WorldGen.KillWire(x, y);
                }
                if (action == 7)
                {
                    WorldGen.PoundTile(x, y);
                }
                if (action == 8)
                {
                    WorldGen.PlaceActuator(x, y);
                }
                if (action == 9)
                {
                    WorldGen.KillActuator(x, y);
                }
                if (action == 10)
                {
                    WorldGen.PlaceWire2(x, y);
                }
                if (action == 11)
                {
                    WorldGen.KillWire2(x, y);
                }
                if (action == 12)
                {
                    WorldGen.PlaceWire3(x, y);
                }
                if (action == 13)
                {
                    WorldGen.KillWire3(x, y);
                }
                if (action == 14)
                {
                    WorldGen.SlopeTile(x, y, type);
                }
                if (action == 15)
                {
                    Minecart.FrameTrack(x, y, pound: true);
                }
                if (action == 16)
                {
                    WorldGen.PlaceWire4(x, y);
                }
                if (action == 17)
                {
                    WorldGen.KillWire4(x, y);
                }
                switch (action)
                {
                    case 18:
                        Wiring.SetCurrentUser(args.Msg.whoAmI);
                        Wiring.PokeLogicGate(x, y);
                        Wiring.SetCurrentUser();
                        break;
                    case 19:
                        Wiring.SetCurrentUser(args.Msg.whoAmI);
                        Wiring.Actuate(x, y);
                        Wiring.SetCurrentUser();
                        break;
                    case 20:
                        if (WorldGen.InWorld(x, y, 2))
                        {
                            int type16 = Main.tile[x, y].type;
                            WorldGen.KillTile(x, y, fail);
                            type = (short)((Main.tile[x, y].active() && Main.tile[x, y].type == type16) ? 1 : 0);
                            NetMessage.TrySendData(17, -1, -1, null, action, x, y, type, style);
                        }
                        break;
                    case 21:
                        WorldGen.ReplaceTile(x, y, (ushort)type, style);
                        break;
                }
                if (action == 22)
                {
                    WorldGen.ReplaceWall(x, y, (ushort)type);
                }
                if (action == 23)
                {
                    WorldGen.SlopeTile(x, y, type);
                    WorldGen.PoundTile(x, y);
                }

                PostHandle();

                if (flag13)
                {
                    NetMessage.SendTileSquare(-1, x, y, 5);
                }
                else if ((action != 1 && action != 21) || !Terraria.ID.TileID.Sets.Falling[type] || Main.tile[x, y].active())
                {
                    NetMessage.TrySendData(17, -1, args.Msg.whoAmI, null, action, x, y, type, style);
                }
            }
            #endregion
            #region PacketTypes.PlaceObject
            else if (args.MsgID == PacketTypes.PlaceObject)
            {
                args.Handled = true;

                short x = reader.ReadInt16();
                short y = reader.ReadInt16();
                short type = reader.ReadInt16();
                int style = reader.ReadInt16();
                int alternate = reader.ReadByte();
                int random = reader.ReadSByte();
                int direction = (reader.ReadBoolean() ? 1 : (-1));

                Netplay.Clients[args.Msg.whoAmI].SpamAddBlock += 1f;
                if (!WorldGen.InWorld(x, y, 10)
                    || !Netplay.Clients[args.Msg.whoAmI].TileSections[Netplay.GetSectionX(x), Netplay.GetSectionY(y)])
                    return;

                PreHandle(args.Msg.whoAmI, "PlaceObject", x.GetPoint(y));
                /////////////////////////////////////////////////////
                        // Terraria.WorldGen.PlaceObject() //
                /////////////////////////////////////////////////////

                if (type >= (int)TileID.Count)
                    return;
                TileObject toBePlaced;
                if (!TileObject.CanPlace(x, y, type, style, direction, out toBePlaced, false, null))
                    return;

                TileObjectData tileData =
                    TileObjectData.GetTileData(toBePlaced.type, toBePlaced.style, toBePlaced.alternate);

                for (int i = 0; i < tileData.Width; i++)
                    for (int j = 0; j < tileData.Height; j++)
                    {
                        Point pos = new Point(toBePlaced.xCoord + i, toBePlaced.yCoord + j);
                        History?.TryAdd(pos, new Tile(Main.tile[pos.X, pos.Y]));
                    }

                toBePlaced.random = random;
                if (TileObject.Place(toBePlaced))
                    WorldGen.SquareTileFrame(x, y, true);

                /////////////////////////////////////////////////////

                NetMessage.SendObjectPlacement(args.Msg.whoAmI, x, y, type,
                    style, alternate, random, direction);
                PostHandle();
            }
            #endregion
            #region PlaceChest
            else if (args.MsgID == PacketTypes.PlaceChest)
            {
                args.Handled = true;

                byte flag = reader.ReadByte();
                short x = reader.ReadInt16();
                short y = reader.ReadInt16();
                int style = reader.ReadInt16();
                // int num215 = reader.ReadInt16(); // ?

                int PlaceChest(int x, int y,
                    ushort type = 21, bool notNearOtherChests = false, int style = 0)
                {
                    int num = -1;
                    if (TileID.Sets.Boulders[(int)Main.tile[x, y + 1].type] || TileID.Sets.Boulders[(int)Main.tile[x + 1, y + 1].type])
                        return -1;
                    TileObject tileObject;
                    if (TileObject.CanPlace(x, y, (int)type, style, 1, out tileObject, false, null))
                    {
                        bool flag = true;
                        if (notNearOtherChests && Chest.NearOtherChests(x - 1, y - 1))
                        {
                            flag = false;
                        }
                        if (flag)
                        {
                            TileObjectData tileData =
                                TileObjectData.GetTileData(tileObject.type, tileObject.style, tileObject.alternate);
                            for (int i = 0; i < tileData.Width; i++)
                                for (int j = 0; j < tileData.Height; j++)
                                {
                                    Point pos = new Point(tileObject.xCoord + i, tileObject.yCoord + j);
                                    History?.TryAdd(pos, new Tile(Main.tile[pos.X, pos.Y]));
                                }

                            TileObject.Place(tileObject);
                            num = Chest.CreateChest(tileObject.xCoord, tileObject.yCoord, -1);
                        }
                    }
                    else
                    {
                        num = -1;
                    }
                    if (num != -1 && Main.netMode == 1 && type == 21)
                    {
                        NetMessage.SendData(34, -1, -1, null, 0, (float)x, (float)y, (float)style, 0, 0, 0);
                    }
                    if (num != -1 && Main.netMode == 1 && type == 467)
                    {
                        NetMessage.SendData(34, -1, -1, null, 4, (float)x, (float)y, (float)style, 0, 0, 0);
                    }
                    return num;
                }

                PreHandle(args.Msg.whoAmI, flag == 0 || flag == 4 ? "PlaceTile - Chests" :
                    flag == 1 || flag == 5 ? "KillTile - Chests" :
                    flag == 2 ? "PlaceTile - Dressers" : "KillTile - Dressers", x.GetPoint(y));

                switch (flag)
                {
                    case 0: // Place: Containers1
                        {
                            int num95 = PlaceChest(x, y, 21, false, style);
                            if (num95 == -1)
                            {
                                NetMessage.TrySendData(34, args.Msg.whoAmI, -1, null, (int)flag, (float)x, (float)y, (float)style, num95, 0, 0);
                                Item.NewItem(new EntitySource_TileBreak(x, y), x * 16, y * 16, 32, 32, Chest.chestItemSpawn[style], 1, true, 0, false, false);
                                break;
                            }
                            NetMessage.TrySendData(34, -1, -1, null, (int)flag, (float)x, (float)y, (float)style, num95, 0, 0);
                        }
                        break;
                    case 1: // Kill: Containers1
                        {
                            if (Main.tile[x, y].type == 21)
                            {
                                ITile tile2 = Main.tile[x, y];
                                if (tile2.frameX % 36 != 0)
                                {
                                    x--;
                                }
                                if (tile2.frameY % 36 != 0)
                                {
                                    y--;
                                }
                                History?.TryAdd(new Point(x, y), new Tile(Main.tile[x, y]));
                                int number = Chest.FindChest(x, y);
                                WorldGen.KillTile(x, y, false, false, false);
                                if (!tile2.active())
                                {
                                    NetMessage.TrySendData(34, -1, -1, null, (int)flag, (float)x, (float)y, 0f, number, 0, 0);
                                    break;
                                }
                            }
                        }
                        break;
                    case 2: // Place: Dressers
                        {
                            int num96 = PlaceChest(x, y, 88, false, style);
                            if (num96 == -1)
                            {
                                NetMessage.TrySendData(34, args.Msg.whoAmI, -1, null, (int)flag, (float)x, (float)y, (float)style, num96, 0, 0);
                                Item.NewItem(new EntitySource_TileBreak(x, y), x * 16, y * 16, 32, 32, Chest.dresserItemSpawn[style], 1, true, 0, false, false);
                                break;
                            }
                            NetMessage.TrySendData(34, -1, -1, null, (int)flag, (float)x, (float)y, (float)style, num96, 0, 0);
                        }
                        break;
                    case 3: // Kill: Dressers
                        {
                            if (Main.tile[x, y].type == 88)
                            {
                                ITile tile3 = Main.tile[x, y];
                                x -= (short)(tile3.frameX % 54 / 18);
                                if (tile3.frameY % 36 != 0)
                                {
                                    y--;
                                }
                                int number2 = Chest.FindChest(x, y);
                                History?.TryAdd(new Point(x, y), new Tile(Main.tile[x, y]));
                                WorldGen.KillTile(x, y, false, false, false);
                                if (!tile3.active())
                                {
                                    NetMessage.TrySendData(34, -1, -1, null, (int)flag, (float)x, (float)y, 0f, number2, 0, 0);
                                    break;
                                }
                            }
                        }
                        break;
                    case 4: // Place: Containers2
                        {
                            int num97 = PlaceChest(x, y, 467, false, style);
                            if (num97 == -1)
                            {
                                NetMessage.TrySendData(34, args.Msg.whoAmI, -1, null, (int)flag, (float)x, (float)y, (float)style, num97, 0, 0);
                                Item.NewItem(new EntitySource_TileBreak(x, y), x * 16, y * 16, 32, 32, Chest.chestItemSpawn2[style], 1, true, 0, false, false);
                                break;
                            }
                            NetMessage.TrySendData(34, -1, -1, null, (int)flag, (float)x, (float)y, (float)style, num97, 0, 0);
                        }
                        break;
                    case 5: // Kill: Containers2
                        {
                            if (Main.tile[x, y].type == 467)
                            {
                                ITile tile4 = Main.tile[x, y];
                                if (tile4.frameX % 36 != 0)
                                {
                                    x--;
                                }
                                if (tile4.frameY % 36 != 0)
                                {
                                    y--;
                                }
                                int number3 = Chest.FindChest(x, y);
                                History?.TryAdd(new Point(x, y), new Tile(Main.tile[x, y]));
                                WorldGen.KillTile(x, y, false, false, false);
                                if (!tile4.active())
                                {
                                    NetMessage.TrySendData(34, -1, -1, null, (int)flag, (float)x, (float)y, 0f, number3, 0, 0);
                                    break;
                                }
                            }
                        }
                        break;
                }

                PostHandle();
            }
            #endregion
            #region PacketTypes.PaintTile
            else if (args.MsgID == PacketTypes.PaintTile)
            {
                args.Handled = true;

                short x = reader.ReadInt16();
                short y = reader.ReadInt16();
                byte paint = reader.ReadByte();
                byte coat = reader.ReadByte();

                PreHandle(args.Msg.whoAmI, $"{(paint == 0 ? "Scrap" : "")}Paint{(coat != 0 ? "Coat" : "")}Tile", x.GetPoint(y));

                History?.TryAdd(new Point(x, y), new Tile(Main.tile[x, y]));

                if (coat == 0)
                    WorldGen.paintTile(x, y, paint);
                else
                    WorldGen.paintCoatTile(x, y, paint);

                NetMessage.TrySendData(63, -1, WhoAmI, null, x, y, (int)paint, (int)coat);

                PostHandle();
            }
            #endregion
            #region PaintWall
            else if (args.MsgID == PacketTypes.PaintWall)
            {
                args.Handled = true;

                short x = reader.ReadInt16();
                short y = reader.ReadInt16();
                byte paint = reader.ReadByte();
                byte coat = reader.ReadByte();

                PreHandle(args.Msg.whoAmI, $"{(paint == 0 ? "Scrap" : "")}Paint{(coat != 0 ? "Coat" : "")}Wall", x.GetPoint(y));

                History?.TryAdd(new Point(x, y), new Tile(Main.tile[x, y]));

                if (coat == 0)
                    WorldGen.paintWall(x, y, paint);
                else
                    WorldGen.paintCoatWall(x, y, paint);

                NetMessage.TrySendData(64, -1, WhoAmI, null, x, y, (int)paint, (int)coat);

                PostHandle();
            }
            #endregion
            #region PacketTypes.MassWireOperation
            else if (args.MsgID == PacketTypes.MassWireOperation)
            {
                args.Handled = true;

                short x = reader.ReadInt16();
                short y = reader.ReadInt16();

                int x2 = reader.ReadInt16();
                int y2 = reader.ReadInt16();
                byte toolMode = reader.ReadByte();

                WiresUI.Settings.MultiToolMode toolMode2 = WiresUI.Settings.ToolMode;
                WiresUI.Settings.ToolMode = (WiresUI.Settings.MultiToolMode)toolMode;
                PreHandle(args.Msg.whoAmI, $"MassWireOperation - {WiresUI.Settings.ToolMode}", x.GetPoint(y));
                Wiring.MassWireOperation(new Point(x, y), new Point(x2, y2), Main.player[args.Msg.whoAmI]);
                PostHandle();
                WiresUI.Settings.ToolMode = toolMode2;
            }
            #endregion
        }

        #endregion

        #region WorldGen_KillTile
        
        static void WorldGen_KillTile(On.Terraria.WorldGen.orig_KillTile orig, int i, int j, bool fail, bool effectOnly, bool noItem)
        {
            History?.TryAdd(new Point(i, j), new Tile(Main.tile[i, j]));
            orig.Invoke(i, j, fail, effectOnly, noItem);
        }

        #endregion
        #region WorldGen_TileFrame

        static void WorldGen_TileFrame(On.Terraria.WorldGen.orig_TileFrame orig,
            int i, int j, bool resetFrame, bool noBreak)
        {
            History?.TryAdd(new Point(i, j), new Tile(Main.tile[i, j]));
            orig.Invoke(i, j, resetFrame, noBreak);
        }

        #endregion
        #region Wiring_MassWireOperationStep
        static bool? Wiring_MassWireOperationStep(On.Terraria.Wiring.orig_MassWireOperationStep orig,
            Player user, Point pt, WiresUI.Settings.MultiToolMode mode,
            ref int wiresLeftToConsume, ref int actuatorsLeftToConstume)
        {
            History?.TryAdd(new Point(pt.X, pt.Y), new Tile(Main.tile[pt.X, pt.Y]));
            return orig.Invoke(user, pt, mode, ref wiresLeftToConsume, ref actuatorsLeftToConstume);
        }

        #endregion
        #region OnNewProjectile

        private static int OnNewProjectile(On.Terraria.Projectile.orig_NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float orig,
            IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY,
            int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2)
        {
            if (WhoAmI != -1 && ProjectileID.Sets.FallingBlockDoesNotFallThroughPlatforms[Type])
            {
                int num = orig.Invoke(spawnSource, X, Y, SpeedX, SpeedY,
                    Type, Damage, KnockBack, Owner, ai0, ai1, ai2);
                if (num != -1)
                    FallingObserver.AddObserved(num, WhoAmI);
                return num;
            }
            return orig.Invoke(spawnSource, X, Y, SpeedX, SpeedY, Type, Damage, KnockBack, Owner, ai0, ai1, ai2);
        }

        #endregion
    }
}
