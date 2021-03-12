﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FishingTrawler.Objects
{
    // TODO: Look at Boat, BoatTunnel, BoatJourney
    internal static class Trawler
    {
        private static int _boatOffset;

        internal static Vector2 GetTrawlerPosition()
        {
            // Enable this line to test moving to the right: _boatOffset++;
            return new Vector2(5300f + _boatOffset, 2640f);
        }

        internal static void Reset()
        {
            _boatOffset = 0;
        }
    }
}
