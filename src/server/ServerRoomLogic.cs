using Common;
using LiteNetLib;

namespace Server;

// Handles loading and unloading rooms, along with everything in them, for clients.
sealed class ServerRoomLogic
{
    enum RoomState : byte { Abstract, Unsynced, Synced }

    record struct ServerRoom(int? LastVisit);

    record struct TrackedPeer(AbstractCreature Player, NetPeer NetPeer, RoomState[] RoomStates)
    {
        public readonly List<int> RealizedRooms = [];
        public readonly List<int> RealizedObjects = [];
    }

    static readonly RoomRealizer fake = new(null, null);
    static float PerformanceEstimate(AbstractRoom room, float budget = 1500f)
    {
        fake.performanceBudget = budget;
        return fake.RoomPerformanceEstimation(room);
    } 

    ServerSession Session => game.session as ServerSession;

    readonly RainWorldGame game;
    readonly List<TrackedPeer> trackedPeers = [];

    ServerRoom[] rooms = default;

    // TODO: re-abstractizing rooms lol

    public void BroadcastRelevant<T>(int room, T packet, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered) where T : IPacket
    {
        foreach (TrackedPeer peer in trackedPeers) {
            if (peer.RoomStates[room] == RoomState.Synced) {
                peer.NetPeer.Send(packet, deliveryMethod);
            }
        }
    }

    public ServerRoomLogic(RainWorldGame game)
    {
        this.game = game;

        On.AbstractCreature.Realize += SyncRealize;
        On.AbstractPhysicalObject.Realize += SyncRealize;
        On.AbstractPhysicalObject.Abstractize += SyncAbstractize;
        On.AbstractPhysicalObject.Move += SyncMove;
    }

    private void SyncRealize(On.AbstractCreature.orig_Realize orig, AbstractCreature self)
    {
        orig(self);
        if (self.realizedObject != null) {
            foreach (TrackedPeer peer in trackedPeers) {
                LoadObject(peer, self.realizedObject);
            }
        }
    }

    private void SyncRealize(On.AbstractPhysicalObject.orig_Realize orig, AbstractPhysicalObject self)
    {
        orig(self);
        if (self.realizedObject != null) {
            foreach (TrackedPeer peer in trackedPeers) {
                LoadObject(peer, self.realizedObject);
            }
        }
    }

    private void SyncAbstractize(On.AbstractPhysicalObject.orig_Abstractize orig, AbstractPhysicalObject self, WorldCoordinate coord)
    {
        if (self.realizedObject != null) {
            foreach (TrackedPeer peer in trackedPeers) {
                UnloadObject(peer, self.ID());
            }
        }
        orig(self, coord);
    }

    private void SyncMove(On.AbstractPhysicalObject.orig_Move orig, AbstractPhysicalObject self, WorldCoordinate newCoord)
    {
        orig(self, newCoord);

        if (self.realizedObject != null) {
            foreach (TrackedPeer peer in trackedPeers) {
                if (peer.RoomStates[newCoord.room] == RoomState.Abstract) {
                    UnloadObject(peer, self.ID());
                }
            }
        }
    }

    // Load an object if it's relevant to the client
    private void LoadObject(TrackedPeer peer, PhysicalObject o)
    {
        if (peer.RoomStates[o.abstractPhysicalObject.Room.index] == RoomState.Synced) {
            Introduce(peer, o);
        }
    }

    // Unload an object if it's relevant to the client
    private void UnloadObject(TrackedPeer peer, int id)
    {
        int i = peer.RealizedObjects.IndexOf(id);
        if (i >= 0) {
            peer.RealizedObjects.RemoveAt(i);
            peer.NetPeer.Send(new DestroyObject(id));
        }
    }

    // Unload an entire room for a peer
    private void UnloadForPeer(TrackedPeer peer, AbstractRoom room)
    {
        peer.RoomStates[room.index] = RoomState.Abstract;
        if (peer.RealizedRooms.Remove(room.index)) {
            // TODO allow servers to control client rooms
            // peer.NetPeer.Send(new AbstractizeRoom(room.name));
        }

        foreach (var entity in room.entities) {
            if (entity is AbstractPhysicalObject apo && apo.realizedObject != null) {
                UnloadObject(peer, apo.ID());
            }
        }
    }

    // Unload an entire room for every peer, and the server
    private void UnloadForServer(AbstractRoom room)
    {
        rooms[room.index].LastVisit = null;

        foreach (var entity in room.entities) {
            if (entity is AbstractPhysicalObject apo && apo.realizedObject != null) {
                foreach (var peer in trackedPeers) {
                    UnloadObject(peer, apo.ID());
                }
            }
        }

        foreach (var peer in trackedPeers) {
            peer.RoomStates[room.index] = RoomState.Abstract;
            if (peer.RealizedRooms.Remove(room.index)) {
                // TODO allow servers to control client rooms
                // peer.NetPeer.Send(new AbstractizeRoom(room.name));
            }
        }

        room.Abstractize();
    }

    int TotalRoomCount()
    {
        Region region = game.overWorld.regions[^1];
        return region.firstRoomIndex + region.numberOfRooms;
    }

    public void UpdateRoomLogic()
    {
        rooms ??= new ServerRoom[TotalRoomCount()];

        UpdatePeerList();

        foreach (TrackedPeer peer in trackedPeers) {
            UpdatePeer(peer);
        }

        foreach (TrackedPeer peer in trackedPeers) {
            foreach (int roomIndex in peer.RealizedRooms) {
                AbstractRoom room = game.world.GetAbstractRoom(roomIndex);
                if (peer.RoomStates[roomIndex] == RoomState.Unsynced && room.realizedRoom.fullyLoaded) {
                    peer.RoomStates[roomIndex] = RoomState.Synced;

                    Introduce(peer, room.realizedRoom);
                }
                if (peer.Player.Room.index == roomIndex) {
                    rooms[roomIndex].LastVisit = Packets.LocalTick;
                }
            }
        }

        ReadInputPackets();
    }

    public void PostUpdate()
    {
        SyncUpdates();
    }

    private void UpdatePeerList()
    {
        // Look for new untracked peers
        foreach (var peer in Session.peers.Keys) {
            var player = Session.GetPlayer(peer);
            if (player != null && !trackedPeers.Any(p => p.Player.ID == player.ID)) {
                trackedPeers.Add(new(player, peer, new RoomState[TotalRoomCount()]));
            }
        }
        // Remove old peers that have disconnected
        for (int i = trackedPeers.Count - 1; i >= 0; i--) {
            if (trackedPeers[i].NetPeer.ConnectionState == ConnectionState.Disconnected) {
                trackedPeers.RemoveAt(i);
            }
        }
    }

    private void UpdatePeer(TrackedPeer peer)
    {
        Realize(peer, peer.Player.Room);

        foreach (var connection in peer.Player.Room.connections) {
            if (connection > -1) {
                Realize(peer, game.world.GetAbstractRoom(connection));
            }
        }
    }

    private void Realize(TrackedPeer peer, AbstractRoom room)
    {
        if (peer.RoomStates[room.index] == RoomState.Abstract) {
            peer.RoomStates[room.index] = RoomState.Unsynced;
            peer.RealizedRooms.Add(room.index);

            // TODO Tell client to realize the room if it hasn't already
            // peer.NetPeer.Send(new RealizeRoom(room.name));

            // Start realizing the room on the server
            room.world.ActivateRoom(room);
        }
    }

    private void Introduce(TrackedPeer peer, Room room)
    {
        foreach (var entity in room.abstractRoom.entities) {
            if (entity is AbstractPhysicalObject apo && apo.realizedObject != null) {
                Introduce(peer, apo.realizedObject);
            }
        }
    }

    private void Introduce(TrackedPeer peer, PhysicalObject realizedObject)
    {
        int id = realizedObject.ID();
        if (peer.RealizedObjects.Contains(id)) {
            return;
        }

        peer.RealizedObjects.Add(id);

        if (realizedObject is not Creature crit) {
            return;
        }

        IntroduceSpecific(peer, id, crit);

        if (crit.grasps != null) {
            foreach (var grasp in crit.grasps) {
                if (grasp?.grabbed == null) {
                    continue;
                }

                Introduce(peer, grasp.grabbed);

                peer.NetPeer.Send(new Grab(grasp.grabbed.ID(), crit.ID(), grasp.dominance,
                    (byte)grasp.graspUsed, (byte)grasp.chunkGrabbed, (byte)(grasp.pacifying ? 1 : 0), grasp.shareability.ToString()));
            }
        }

        if (crit.dead) {
            peer.NetPeer.Send(new KillCreature(crit.ID()));
        }
    }

    // Introduces a specific kind of object.
    // Used when implementations vary (e.g. players vs items vs mobs)
    private static void IntroduceSpecific(TrackedPeer peer, int id, Creature crit)
    {
        if (crit is Player p) {
            peer.NetPeer.Send(new RealizePlayer(id, p.room.abstractRoom.index));
            peer.NetPeer.Send(new UpdatePlayer(id, Utils.InputToInt(p.input[0]),
                p.Head().pos, p.Head().vel,
                p.Body().pos, p.Body().vel
                ));
        }
    }

    private void ReadInputPackets()
    {
        // TODO read player inputs!
    }

    private void SyncUpdates()
    {
        // TODO for each client
        //        for each client (that isnt the above client)
        //           send input and update packets
    }
}