namespace MistForest.Core.GameMap
{
    public abstract record GameMode
    {
        public sealed record Scout : GameMode
        {
            public static readonly Scout Instance = new();
        }

        public sealed record Heroes : GameMode
        {
            public static readonly Heroes Instance = new();
        }

        public sealed record SelfDefine : GameMode
        {
            public int Height { get; private set; }

            public int Width { get; private set; }

            public static SelfDefine New(int height, int width)
            {
                return new() { Height = height, Width = width };
            }
        }
    }
}
