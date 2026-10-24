using System;

namespace MistForest.Core.Protocol
{
    [Serializable]
    public class WelcomeMsg
    {
        public string Type { get; set; } = "welcome";
        public int You { get; set; }
    }

    [Serializable]
    public class StateMsg
    {
        public string Type { get; set; } = "state";
        public int CurrentPlayer { get; set; }
        public UnitStateMsg[] Units { get; set; } = Array.Empty<UnitStateMsg>();
    }

    [Serializable]
    public class UnitStateMsg
    {
        public int UnitId { get; set; }
        public int PlayerId { get; set; }
        public int Pos { get; set; }
        public int Health { get; set; }
        public bool Alive { get; set; }
    }

    [Serializable]
    public class EnvelopeMsg
    {
        public string Type { get; set; } = "";
    }

    [Serializable]
    public class MoveMsg
    {
        public string Type { get; set; } = "move";
        public int Unit { get; set; }
        public int Pos { get; set; }
    }

    [Serializable]
    public class EventMsg
    {
        public string Type { get; set; } = "event";
        public string Kind { get; set; } = "";
        public int Unit { get; set; } = -1;
        public int Target { get; set; } = -1;
        public int[] Cells { get; set; } = Array.Empty<int>();
        public bool Success { get; set; } = false;
    }

    [Serializable]
    public class RejectMsg
    {
        public string Type { get; set; } = "reject";
        public string Reason { get; set; } = "";
    }

    [Serializable]
    public class ScoutRowMsg
    {
        public string Type { get; set; } = "scoutRow";
        public int Unit { get; set; }
        public int Row { get; set; }
    }

    [Serializable]
    public class HelloMsg
    {
        public string Type = "hello";
    }
}
