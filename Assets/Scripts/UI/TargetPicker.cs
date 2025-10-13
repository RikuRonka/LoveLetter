using System.Linq;
using UnityEngine;

public static class TargetPicker
{
    public static uint FirstOtherAliveNetId()
    {
        var me = Mirror.NetworkClient.localPlayer;
        var all = Object.FindObjectsOfType<PlayerNetwork>();
        var other = all.FirstOrDefault(p => p && p.gameObject != me?.gameObject);
        return other ? other.netId : 0;
    }
}
