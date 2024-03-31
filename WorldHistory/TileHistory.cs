using System.Data;
using System.Reflection;

using TShockAPI;
using TShockAPI.DB;

using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;

using Terraria;

namespace WorldHistory
{
    public class TileHistory
	{
        #region Data

        internal static readonly string name = "TileHistory";
        public IDbConnection _db;

        #endregion
        #region Initialize

        public TileHistory()
        {
            Dictionary<MySqlDbType, string> TypesAsStrings = typeof(MysqlQueryCreator)
				.GetField("TypesAsStrings", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
				.GetValue(null) as Dictionary<MySqlDbType, string>;
			TypesAsStrings.TryAdd(MySqlDbType.TinyBlob, "TINYBLOB");
			TypesAsStrings.TryAdd(MySqlDbType.Int16, "SMALLINT");

			IQueryBuilder builder = null;
			switch (TShock.Config.Settings.StorageType.ToLowerInvariant())
			{
				case "mysql":
					string[] host = TShock.Config.Settings.MySqlHost.Split(':');
					_db = new MySqlConnection
					{
						ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
							host[0],
							host.Length == 1 ? "3306" : host[1],
							TShock.Config.Settings.MySqlDbName,
							TShock.Config.Settings.MySqlUsername,
							TShock.Config.Settings.MySqlPassword)
					};
					builder = new MysqlQueryCreator();
					break;
				case "sqlite":
					_db = new SqliteConnection(new SqliteConnectionStringBuilder()
					{
						DataSource = Path.Combine(TShock.SavePath, name + ".sqlite")
					}.ToString());
					builder = new SqliteQueryCreator();
					break;
			}

			new SqlTableCreator(_db, builder).EnsureTableStructure(new SqlTable(name,
				new SqlColumn("ID", MySqlDbType.Int32) { AutoIncrement = true, Primary = true },
				new SqlColumn("AccountID", MySqlDbType.Int32),
				new SqlColumn("Time", MySqlDbType.Int64),
				new SqlColumn("TileX", MySqlDbType.Int16),
				new SqlColumn("TileY", MySqlDbType.Int16),
				new SqlColumn("Action", MySqlDbType.TinyText),
				new SqlColumn("Tiles", MySqlDbType.TinyBlob)));

			if (WorldHistoryPlugin.Config.Settings.RemovalTime > 0)
			{
				TimeSpan span = TimeSpan.FromSeconds(WorldHistoryPlugin.Config.Settings.RemovalTime);
				_db.Query($"DELETE FROM {name} WHERE Time < {DateTime.Now.Ticks - span.Ticks};");
			}
		}

        #endregion

        #region Add

        public bool Add(TileAction action)
        {
			byte[] tiles = new byte[28];
			using (MemoryStream stream = new MemoryStream(tiles, 0, tiles.Length))
			using (BinaryWriter writer = new BinaryWriter(stream))
			{
				writer.Write(action.Previous);
				writer.Write(action.Following);
			}
			try
            {
				return _db.Query($"INSERT INTO {name} VALUES(@0, @1, @2, @3, @4, @5, @6)",
				null, action.AccountID, action.Ticks, action.TileX, action.TileY, action.Action, tiles) > 0;
			}
			catch (Exception ex)
            {
				TShock.Log.ConsoleError(ex.ToString());
            }
			return false;
		}

        #endregion
        #region Get

        #region GetByAccount

        public IEnumerable<TileAction> Get(int AccountID, long Time, short X, short Y, short X2, short Y2)
		{
			using (QueryResult result = _db.QueryReader($"SELECT * FROM {name} WHERE AccountID = @0 AND Time > @1 AND TileX >= @2 AND TileX <= @4 AND TileY >= @3 AND TileY <= @5",
				AccountID, Time, X, Y, X2, Y2))
            {
				while (result.Read())
				{
					IDataReader reader = result.Reader;
					yield return Read(reader);
				}
			}
		}

        #endregion
        #region GetByPoint

        public IEnumerable<TileAction> Get(short x, short y)
        {
			using (QueryResult result = _db.QueryReader($"SELECT * FROM {name} WHERE TileX = @0 AND TileY = @1", x, y))
            {
				while (result.Read())
                {
					IDataReader reader = result.Reader;
					yield return Read(reader);
				}
            }
        }

        #endregion

        #endregion
        #region Delete

        #region DeleteById

        public bool Delete(int id)
        {
			return _db.Query($"DELETE FROM {name} WHERE ID = @0", id) > 0;
        }

        #endregion
        #region DeleteMinToMaxIds

        public bool Delete(int minId, int maxId)
        {
			return _db.Query($"DELETE FROM {name} WHERE ID >= @0 AND ID <= @1", minId, maxId) > 0;
        }

        #endregion

        #endregion
        #region Read

        TileAction Read(IDataReader reader)
        {
			byte[] blob = new byte[28];
			reader.GetBytes(6, 0, blob, 0, blob.Length);

			using MemoryStream stream = new MemoryStream(blob);
			using BinaryReader br = new BinaryReader(stream);

			ITile p = br.ReadTile();
			ITile f = br.ReadTile();

			return new TileAction()
			{
				ID = reader.GetInt32(0),

				AccountID = reader.GetInt32(1),

				Ticks = reader.GetInt64(2),

				TileX = reader.GetInt16(3),
				TileY = reader.GetInt16(4),

				Action = reader.GetString(5),

				Previous = p,
				Following = f
			};
		}

        #endregion
    }
}
