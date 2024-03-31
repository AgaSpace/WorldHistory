using Terraria;
using Terraria.ID;

using TShockAPI;

namespace WorldHistory
{
    public static class FallingObserver
    {
        #region Data

        static Dictionary<int, int> observed = new Dictionary<int, int>();

        #endregion
        #region Initialize

        public static void Initialize()
        {
            On.Terraria.Projectile.Kill += Projectile_Kill;
            On.Terraria.Projectile.Update += Projectile_Update;
        }
        public static void Deinitialize()
        {
            On.Terraria.Projectile.Kill -= Projectile_Kill;
            On.Terraria.Projectile.Update -= Projectile_Update;
            observed.Clear();
        }

        #endregion

        #region Projectile_Update

        static void Projectile_Update(On.Terraria.Projectile.orig_Update orig, Projectile self, int i)
        {
            if (!self.active && observed.ContainsKey(i))
                observed.Remove(i);
            orig.Invoke(self, i);
        }

        #endregion
        #region Projectile_Kill

        static void Projectile_Kill(On.Terraria.Projectile.orig_Kill orig, Projectile self)
        {
            if (ProjectileID.Sets.FallingBlockDoesNotFallThroughPlatforms[self.type])
            {
                if (observed.TryGetValue(self.whoAmI, out int owner))
                {
                    TSPlayer player = TShock.Players[owner];

                    short x = (short)(self.position.X / 16);
                    short y = (short)(self.position.Y / 16);

                    if (!player.HasBuildPermission(x, y, true))
                        return;

                    ITile previous = new Tile(Main.tile[x, y]);

                    int accountId = player.Account.ID;
                    string action = "PlaceTile - ProjKill";
                    long ticks = DateTime.UtcNow.Ticks;

                    observed.Remove(self.whoAmI);
                    orig.Invoke(self);

                    ITile following = new Tile(Main.tile[x, y]);

                    ThreadPool.QueueUserWorkItem((_) =>
                    {
                        WorldHistoryPlugin.History.Add(new TileAction()
                        {
                            ID = -1,

                            AccountID = accountId,
                            Ticks = ticks,

                            TileX = x,
                            TileY = y,

                            Action = action,

                            Previous = previous,
                            Following = following
                        });
                    });
                }
            }
            else
                orig.Invoke(self);
        }

        #endregion

        #region AddObserved

        /// <summary>
        /// Добавляет проджектайл падующего тайла в список наблюдаемых.
        /// </summary>
        /// <param name="num">Место в массиве <see cref="Main.projectile"/>.</param>
        /// <param name="owner">Благодаря кому появился данный <see cref="Projectile"/>.</param>
        public static void AddObserved(int num, int owner)
        {
            if (!observed.ContainsKey(num))
                observed.Add(num, owner);
        }

        #endregion
    }
}
