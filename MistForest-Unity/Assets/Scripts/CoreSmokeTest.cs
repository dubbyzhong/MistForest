using MistForest.Core.Model;
using UnityEngine;

public static class CoreSmokeTest
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Run()
    {
        var map = new Map();
        Debug.Log(
            "[CoreSmokeTest] connect to the core lib successful: default map "
                + map.Width
                + "x"
                + map.Height
                + ", Scout mode "
                + MapInfo.CellCount(Mode.Scout)
                + " cells, "
                + ((int)Role.Sword + 1)
                + " roles"
        );
    }
}
