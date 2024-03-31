using System.Text;

using Microsoft.Xna.Framework;

using Terraria;

namespace WorldHistory
{
    internal static class Extensions
    {
        #region WriteTile

        internal static void Write(this BinaryWriter writer, ITile tile)
        {
            writer.Write((ushort)tile.sTileHeader);
            writer.Write((byte)tile.bTileHeader);
            writer.Write((byte)tile.bTileHeader2);
            writer.Write((byte)tile.bTileHeader3);

            if (tile.active())
            {
                writer.Write((ushort)tile.type);
                if (Main.tileFrameImportant[tile.type])
                {
                    writer.Write((short)tile.frameX);
                    writer.Write((short)tile.frameY);
                }
            }
            writer.Write((ushort)tile.wall);
            writer.Write((byte)tile.liquid);
        }

        #endregion
        #region ReadTile

        internal static ITile ReadTile(this BinaryReader reader)
        {
            var tile = new Tile
            {
                sTileHeader = reader.ReadUInt16(),
                bTileHeader = reader.ReadByte(),
                bTileHeader2 = reader.ReadByte(),
                bTileHeader3 = reader.ReadByte()
            };

            if (tile.active())
            {
                tile.type = reader.ReadUInt16();
                if (Main.tileFrameImportant[tile.type])
                {
                    tile.frameX = reader.ReadInt16();
                    tile.frameY = reader.ReadInt16();
                }
            }
            tile.wall = reader.ReadUInt16();
            tile.liquid = reader.ReadByte();

            return tile;
        }

        #endregion

        #region GetTileOnPosition

        internal static ITile GetTileOnPosition(this Point point)
        {
            return Main.tile[point.X, point.Y];
        }

        #endregion
        #region GetPoint

        internal static Point GetPoint(this short x, short y)
        {
            return new Point(x, y);
        }

        #endregion

        #region Comprassion

        internal static string Comprassion(this ITile p, ITile f)
        {
            StringBuilder builder = new StringBuilder();

            if (p.type != f.type || p.active() != f.active())
                builder.Append($" | type ({p.type} - {f.type})");
            if (p.wall != f.wall)
                builder.Append($" | wall ({p.wall} - {f.wall})");

            if (p.active() == f.active() && p.inActive() != f.inActive())
                builder.Append($" | inActive ({p.inActive()} - {f.inActive()})");

            if (p.active() == f.active() && p.color() != f.color())
                builder.Append($" | color ({p.color()} - {f.color()})");
            if (p.wall == f.wall && p.wallColor() != f.wallColor())
                builder.Append($" | wallColor ({p.wallColor()} - {f.wallColor()})");

            if (p.active() == f.active() && p.fullbrightBlock() != f.fullbrightBlock())
                builder.Append($" | fullbrightBlock ({p.fullbrightBlock()} - {f.fullbrightBlock()})");
            if (p.wall == f.wall && p.fullbrightWall() != f.fullbrightWall())
                builder.Append($" | fullbrightWall ({p.fullbrightWall()} - {f.fullbrightWall()})");

            if (p.active() == f.active() && p.invisibleBlock() != f.invisibleBlock())
                builder.Append($" | invisibleBlock ({p.invisibleBlock()} - {f.invisibleBlock()})");
            if (p.wall == f.wall && p.invisibleWall() != f.invisibleWall())
                builder.Append($" | invisibleWall ({p.invisibleWall()} - {f.invisibleWall()})");

            if (p.active() == f.active() && p.slope() != f.slope())
                builder.Append($" | slope ({p.slope()} - {f.slope()})");

            if (p.wire() != f.wire())
                builder.Append($" | wire ({p.wire()} - {f.wire()})");
            if (p.wire2() != f.wire2())
                builder.Append($" | wire2 ({p.wire2()} - {f.wire2()})");
            if (p.wire3() != f.wire3())
                builder.Append($" | wire3 ({p.wire3()} - {f.wire3()})");
            if (p.wire4() != f.wire4())
                builder.Append($" | wire4 ({p.wire4()} - {f.wire4()})");

            return builder.ToString();
        }

        #endregion
    }
}

