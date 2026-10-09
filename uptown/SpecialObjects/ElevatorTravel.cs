using System;
using Microsoft.Xna.Framework;

namespace uptown.SpecialObjects;

// Either endpoint can start a trip. The arrival endpoint plays the occupied sheet backward.
public sealed class ElevatorTravel
{
    private Elevator source, destination;
    private float elapsed;
    private bool arrived;
    public bool Active => source != null;

    public bool Begin(Elevator from, Elevator to, Player player)
    {
        if (Active || player.IsDead || player.IsInElevator || ReferenceEquals(from, to)
            || string.IsNullOrWhiteSpace(from.PairId) || from.PairId != to.PairId || !from.CanEnter(player)) return false;
        source = from; destination = to; elapsed = 0; arrived = false;
        player.BeginElevatorTravel(from.Position);
        source.SetOccupied(0, false);
        return true;
    }

    public void Update(GameTime time, Player player)
    {
        if (!Active) return;
        elapsed += Math.Max(0, (float)time.ElapsedGameTime.TotalSeconds);
        if (!arrived && elapsed >= Elevator.TravelTime - 0.000001f)
        {
            arrived = true;
            source.ClearOccupied();
            player.TeleportTo(destination.Position);
        }
        if (arrived) destination.SetOccupied(Math.Max(0, elapsed - Elevator.TravelTime), true);
        else source.SetOccupied(elapsed, false);
        if (elapsed < Elevator.TravelTime * 2 - 0.000001f) return;
        destination.ClearOccupied();
        player.EndElevatorTravel();
        source = destination = null;
    }
}
