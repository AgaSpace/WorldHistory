using Terraria;

namespace WorldHistory
{
	public struct TileAction
	{
		public int ID;
		public int AccountID;

		public long Ticks;
		public DateTime Time => new DateTime(Ticks);

		public short TileX;
		public short TileY;

		public string Action;

		public ITile Previous;
		public ITile Following;
	}
}
