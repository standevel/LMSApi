using System;
using System.Collections.Generic;
using System.Linq;

namespace LMS.Api.Services;

public static class CafeteriaOrderStateMachine
{
    private static readonly HashSet<CafeteriaOrderStatus> TerminalStatuses =
    [
        CafeteriaOrderStatus.Claimed,
        CafeteriaOrderStatus.Cancelled
    ];

    private static readonly Dictionary<CafeteriaOrderStatus, HashSet<CafeteriaOrderStatus>> AllowedTransitions =
        new()
        {
            [CafeteriaOrderStatus.New] = [CafeteriaOrderStatus.Preparing, CafeteriaOrderStatus.Cancelled],
            [CafeteriaOrderStatus.Preparing] = [CafeteriaOrderStatus.ReadyForPickup, CafeteriaOrderStatus.Cancelled],
            [CafeteriaOrderStatus.ReadyForPickup] = [CafeteriaOrderStatus.Claimed, CafeteriaOrderStatus.Cancelled],
            [CafeteriaOrderStatus.Claimed] = [],
            [CafeteriaOrderStatus.Cancelled] = []
        };

    public static bool CanTransition(CafeteriaOrderStatus current, CafeteriaOrderStatus next) =>
        AllowedTransitions.TryGetValue(current, out var allowed) && allowed.Contains(next);

    public static bool IsTerminal(CafeteriaOrderStatus status) => TerminalStatuses.Contains(status);

    public static bool ValidateTransition(CafeteriaOrderStatus current, CafeteriaOrderStatus next, out string error)
    {
        if (IsTerminal(current))
        {
            error = $"Order is already terminal ('{current}') and cannot be modified.";
            return false;
        }

        if (current == next)
        {
            error = $"Order is already in status '{current}'.";
            return false;
        }

        if (!CanTransition(current, next))
        {
            error = $"Invalid transition from '{current}' to '{next}'. Allowed: [{string.Join(", ", AllowedTransitions[current])}].";
            return false;
        }

        if (next == CafeteriaOrderStatus.Claimed && current != CafeteriaOrderStatus.ReadyForPickup)
        {
            error = "An order can only be claimed once it is ReadyForPickup.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
