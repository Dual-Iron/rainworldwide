using Common;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using System.Data;

namespace Client.Hooks;

sealed class ObjectHooks
{
    public void Hook()
    {
        // Set chunk positions etc. Pre- and post-update stuff.
        On.Player.Update += SyncPlayer;
    }

    private void SyncPlayer(On.Player.orig_Update orig, Player p, bool eu)
    {
        orig(p, eu);

        // TODO: fix input being spread across every scug :)

        if (Game().session is not ClientSession sess) return;
        if (p.IsMe() || !sess.PlayerUpdates.TryGetValue(p.ID(), out var packet)) return;

        sess.PlayerUpdates.Remove(p.ID());

        p.input[0] = InputFromInt(packet.Input);
        p.Head().pos = Vector2.Lerp(p.Head().pos, packet.HeadPos, 0.8f);
        p.Head().vel = packet.HeadVel;
        p.Body().pos = Vector2.Lerp(p.Body().pos, packet.BodyPos, 0.8f);
        p.Body().vel = packet.BodyVel;

        //p.standing = packet.Standing;
        //p.bodyMode = (Player.BodyModeIndex)packet.BodyMode;
        //p.animation = (Player.AnimationIndex)packet.Animation;
        //p.animationFrame = packet.AnimationFrame;
        //p.flipDirection = packet.FlipDirection;
        //p.lastFlipDirection = packet.FlipDirectionLast;
    }
}
