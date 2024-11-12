using Common;

namespace Client;

sealed class ClientRoomLogic(RainWorldGame game)
{
    ClientSession Session => game.session as ClientSession;

    public bool TryFind<T>(int id, out T obj) where T : PhysicalObject
    {
        if (Session.Objects.TryGetValue(id, out var something) && something is T t) {
            obj = t;
            return true;
        }
        obj = null!;
        return false;
    }

    public void UpdateRoomLogic()
    {
        // Follow our own player
        game.cameras[0].followAbstractCreature = Session.MyPlayer;

        var room = Session.MyPlayer?.Room.realizedRoom;
        if (room != null && game.cameras[0].room != room) {
            game.cameras[0].MoveCamera(room, 0);
        }

        // TODO realize/abstractize room logic
        //foreach (var packet in RealizeRoom.All()) {
        //    game.world.ActivateRoom(packet.Index);
        //}

        //foreach (var packet in AbstractizeRoom.All()) {
        //    game.world.GetAbstractRoom(packet.Index).Abstractize();
        //}

        foreach (var packet in DestroyObject.Queue.All()) {
            if (TryFind(packet.ID, out PhysicalObject obj) && !obj.slatedForDeletetion) {
                Log($"Server destroyed {obj.ToDebugString()}");

                obj.abstractPhysicalObject.Destroy();
                obj.Destroy();
                Session.Objects.Remove(packet.ID);
            }
        }

        foreach (var packet in KillCreature.Queue.All()) {
            if (Session.Objects.TryGetValue(packet.ID, out var obj) && obj is Creature crit) {
                Log($"Server killed {obj.ToDebugString()}");

                crit.Die();
            }
        }

        IntroduceObjects();

        ReadUpdatePackets();
    }

    public void PostUpdate()
    {
        DoUpdatePostRoom();
    }

    private void DoUpdatePostRoom()
    {
        // Update some things after object updates, to give the client a chance to run sfx/vfx and stuff.
        // These are mostly "corrective" packets that ensure the client's world is up-to-date with the server.
        foreach (var packet in Grab.Queue.All()) {
            if (TryFind(packet.GrabberID, out Creature grabber) && grabber.grasps != null && packet.GraspUsed < grabber.grasps.Length && TryFind(packet.GrabbedID, out PhysicalObject grabbed)) {
                grabber.ReleaseGrasp(packet.GraspUsed);

                Creature.Grasp.Shareability share = new(packet.Shareability);

                grabber.Grab(grabbed, packet.GraspUsed, packet.ChunkGrabbed, share, packet.Dominance, true, packet.Pacifying == 1);
            }
        }

        foreach (var packet in Release.Queue.All()) {
            if (TryFind(packet.GrabberID, out Creature grabber) && grabber.grasps != null && packet.GraspUsed < grabber.grasps.Length && TryFind(packet.GrabbedID, out PhysicalObject grabbed)) {
                // If grabbing the object, but in a desynced grasp, then switch grasps.
                var serverGrasp = grabber.grasps[packet.GraspUsed];
                var clientGrasp = grabber.grasps.FirstOrDefault(g => g?.grabbed == grabbed);
                if (serverGrasp?.grabbed != grabbed && clientGrasp != null) {
                    grabber.SwitchGrasps(packet.GraspUsed, clientGrasp.graspUsed);
                }
                grabber.ReleaseGrasp(packet.GraspUsed);
            }
        }
    }

    private void IntroduceObjects()
    {
        foreach (var packet in RealizePlayer.Queue.All()) {
            AbstractCreature p = new(game.world, StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Slugcat), null, new(packet.Room, 0, 0, -1), new(-1, packet.PlayerID));
            p.state = new PlayerState(p, playerNumber: packet.PlayerID, slugcatCharacter: SlugcatStats.Name.White, false);
            p.Room.AddEntity(p);
            p.RealizeInRoom();

            Session.Objects[packet.PlayerID] = p.realizedObject;
            Session.AddPlayer(p);

            Log($"Realized player {packet.PlayerID}");
        }
    }

    private void ReadUpdatePackets()
    {
        foreach (var packet in UpdatePlayer.Queue.All()) {
            if (TryFind(packet.PlayerID, out Player _)) {
                Session.PlayerUpdates[packet.PlayerID] = packet;
            }
        }
    }
}
