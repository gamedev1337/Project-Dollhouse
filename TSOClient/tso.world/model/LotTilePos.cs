﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace tso.world.model
{
    public struct LotTilePos
    {
        public short x;
        public short y;
        public sbyte Level;

        public LotTilePos(short x, short y, sbyte level)
        {
            this.x = x; this.y = y; Level = level;
        }

        public int TileID
        {
            get
            {
                return (int)TileX | ((int)TileY << 8) | ((int)Level << 16);
            }
        }

        public static LotTilePos FromBigTile(short x, short y, sbyte level)
        {
            return new LotTilePos((short)((x << 4) + 8), (short)((y << 4) + 8), level);
        }

        //TODO: uses of the below indicate unsafe operations. We shouldn't have any of these by the time we go live.
        public static LotTilePos FromVec3(Vector3 pos)
        {
            return new LotTilePos((short)Math.Round(pos.X * 16), (short)Math.Round(pos.Y * 16), (sbyte)(Math.Round(pos.Z / 2.95) + 1));
        }

        public static int Distance(LotTilePos a, LotTilePos b)
        {
            return (int)Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y)); 
            //TODO: consider level? does anything need this?
        }

        public static LotTilePos operator +(LotTilePos c1, LotTilePos c2) //use for offsets ONLY!
        {
            return new LotTilePos((short)(c1.x+c2.x), (short)(c1.y+c2.y), (sbyte)(c1.Level+c2.Level));
        }

        public static LotTilePos operator -(LotTilePos c1, LotTilePos c2) //use for offsets ONLY!
        {
            return new LotTilePos((short)(c1.x - c2.x), (short)(c1.y - c2.y), (sbyte)(c1.Level - c2.Level));
        }

        public static bool operator ==(LotTilePos c1, LotTilePos c2) //are these necessary?
        {
            return equals(c1, c2);
        }

        public static bool operator !=(LotTilePos c1, LotTilePos c2)
        {
            return !equals(c1, c2);
        }

        private static bool equals(LotTilePos c1, LotTilePos c2)
        {
            return c1.x == c2.x && c1.y == c2.y && c1.Level == c2.Level;
        }

        public LotTilePos(LotTilePos pos) {
            x = pos.x;
            y = pos.y;
            Level = pos.Level;
        }

        public short TileX
        {
            get
            {
                return (short)(x >> 4);
            }
            set
            {
                x = (short)(value << 4);
            }
        }

        public short TileY
        {
            get
            {
                return (short)(y >> 4);
            }
            set
            {
                y = (short)(value << 4);
            }
        }

        public static LotTilePos OUT_OF_WORLD = new LotTilePos(-32768, -32768, 1);
    }
}
