﻿using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FishingTrawler.Objects
{
    internal class Trawler
    {
        internal Vector2 _boatPosition;
        internal int _boatDirection;
        internal int _boatOffset;
        internal Event _boatEvent;
        internal int nonBlockingPause;
        internal float _nextBubble;
        internal float _nextSlosh;
        internal float _nextSmoke;
        internal bool _boatAnimating;
        internal bool _closeGate;
        internal readonly GameLocation location;

        public Trawler()
        {

        }

        public Trawler(GameLocation location)
        {
            this.location = location;
            this._boatPosition = GetStartingPosition();
        }

        internal Vector2 GetStartingPosition()
        {
            if (location is null || location is Beach)
            {
                return new Vector2(80f, 41f) * 64f;
            }
            else
            {
                return new Vector2(3f, 42f) * 64f;
            }
        }

        internal Vector2 GetTrawlerPosition()
        {
            // Enable this line to test moving to the right:
            //_boatOffset++;
            return _boatPosition + new Vector2(_boatOffset, 0f);
        }

        internal void Reset()
        {
            this._nextSmoke = 0f;
            this._nextBubble = 0f;
            this._boatAnimating = false;
            this._boatPosition = GetStartingPosition();
            this._boatOffset = 0;
            this._boatDirection = 0;
            this._closeGate = false;
        }

        internal void TriggerDepartureEvent()
        {
            if (ModEntry.murphyNPC != null)
            {
                ModEntry.murphyNPC = null;
            }

            string id = location.currentEvent is null ? "Empty" : location.currentEvent.id.ToString();
            ModEntry.monitor.Log($"Starting event for {Game1.player.Name}: {location.currentEvent is null} | {id}", LogLevel.Trace);

            string eventString = "/-1000 -1000/farmer 0 0 0/playMusic none/fade/viewport -5000 -5000/warp farmer -100 -100/locationSpecificCommand despawn_murphy/locationSpecificCommand close_gate/changeMapTile Back 87 40 14/changeMapTile Buildings 87 41 19/changeMapTile Buildings 87 42 24/changeMapTile Buildings 87 43 4/fade/viewport 83 38/locationSpecificCommand non_blocking_pause 1000/playSound furnace/locationSpecificCommand animate_boat_start/locationSpecificCommand non_blocking_pause 1000/locationSpecificCommand boat_depart/fade/viewport -5000 -5000/changeMapTile Back 87 40 18/changeMapTile Buildings 87 41 14/changeMapTile Buildings 87 42 19/changeMapTile Buildings 87 43 24/locationSpecificCommand warp_to_cabin/end warpOut";
            if (location is IslandSouthEast)
            {
                eventString = "/-1000 -1000/farmer 0 0 0/playMusic none/fade/viewport -5000 -5000/warp farmer -100 -100/locationSpecificCommand despawn_murphy/locationSpecificCommand close_gate/changeMapTile Back 10 41 14/changeMapTile Buildings 10 42 19/changeMapTile Buildings 10 43 24/changeMapTile Buildings 10 44 4/fade/viewport 22 39/locationSpecificCommand non_blocking_pause 1000/playSound furnace/locationSpecificCommand animate_boat_start/locationSpecificCommand non_blocking_pause 1000/locationSpecificCommand boat_depart/fade/viewport -5000 -5000/changeMapTile Back 10 41 18/changeMapTile Buildings 10 42 14/changeMapTile Buildings 10 43 19/changeMapTile Buildings 10 44 24/locationSpecificCommand warp_to_cabin/end warpOut";
            }

            if (Context.IsMultiplayer)
            {
                // Force close menu
                if (Game1.player.hasMenuOpen)
                {
                    Game1.activeClickableMenu = null;
                }

                Game1.player.locationBeforeForcedEvent.Value = "Custom_TrawlerCabin";
                Farmer farmerActor = (Game1.player.NetFields.Root as NetRoot<Farmer>).Clone().Value;

                Action performForcedEvent = delegate
                {
                    Game1.warpingForForcedRemoteEvent = true;
                    Game1.player.completelyStopAnimatingOrDoingAction();

                    farmerActor.currentLocation = location;
                    farmerActor.completelyStopAnimatingOrDoingAction();
                    farmerActor.UsingTool = false;
                    farmerActor.items.Clear();
                    farmerActor.hidden.Value = false;
                    Event @event = new Event(eventString, ModEntry.BOAT_DEPART_EVENT_ID, farmerActor);
                    @event.showWorldCharacters = false;
                    @event.showGroundObjects = true;
                    @event.ignoreObjectCollisions = false;
                    Game1.currentLocation.startEvent(@event);
                    Game1.warpingForForcedRemoteEvent = false;
                    string value = Game1.player.locationBeforeForcedEvent.Value;
                    Game1.player.locationBeforeForcedEvent.Value = null;
                    @event.setExitLocation("Custom_TrawlerCabin", 8, 5);
                    Game1.player.locationBeforeForcedEvent.Value = value;
                    Game1.player.orientationBeforeEvent = 0;
                };
                Game1.remoteEventQueue.Add(performForcedEvent);

                return;
            }

            _boatEvent = new Event(eventString, ModEntry.BOAT_DEPART_EVENT_ID, Game1.player);
            _boatEvent.showWorldCharacters = false;
            _boatEvent.showGroundObjects = true;
            _boatEvent.ignoreObjectCollisions = false;
            _boatEvent.setExitLocation("Custom_TrawlerCabin", 8, 5);
            Game1.player.locationBeforeForcedEvent.Value = "Custom_TrawlerCabin";

            Event boatEvent = this._boatEvent;
            boatEvent.onEventFinished = (Action)Delegate.Combine(boatEvent.onEventFinished, new Action(OnBoatEventEnd));
            location.currentEvent = this._boatEvent;
            _boatEvent.checkForNextCommand(location, Game1.currentGameTime);

            Game1.eventUp = true;
        }

        internal void StartDeparture(Farmer who)
        {
            List<Farmer> farmersToDepart = GetFarmersToDepart();

            ModEntry.mainDeckhand = who;
            ModEntry.numberOfDeckhands = farmersToDepart.Count();
            ModEntry.monitor.Log($"There are {farmersToDepart.Count()} farm hands departing!", LogLevel.Trace);

            location.modData[ModEntry.MURPHY_ON_TRIP] = "true";

            TriggerDepartureEvent();

            if (Context.IsMultiplayer)
            {
                // Send out trigger event to relevant players
                ModEntry.AlertPlayersOfDeparture(who.UniqueMultiplayerID, farmersToDepart);
            }
        }

        internal void OnBoatEventEnd()
        {
            if (this._boatEvent == null)
            {
                return;
            }
            foreach (NPC actor in this._boatEvent.actors)
            {
                actor.shouldShadowBeOffset = false;
                actor.drawOffset.X = 0f;
            }
            foreach (Farmer farmerActor in this._boatEvent.farmerActors)
            {
                farmerActor.shouldShadowBeOffset = false;
                farmerActor.drawOffset.X = 0f;
            }
            this.Reset();
            this._boatEvent = null;
        }

        internal List<Farmer> GetFarmersToDepart()
        {
            Rectangle zoneOfDeparture = new Rectangle(82, 26, 10, 16);
            if (location is IslandSouthEast)
            {
                zoneOfDeparture = new Rectangle(5, 31, 10, 16);
            }
            return location.farmers.Where(f => zoneOfDeparture.Contains(f.getTileX(), f.getTileY()) && !ModEntry.HasFarmerGoneSailing(f)).ToList();
        }
    }
}
