namespace MistForest.Core.GameMap
{
    public abstract record GameMode
    {
        public sealed record Scout : GameMode;

        public sealed record Heroes : GameMode;

        public sealed record SelfDefine : GameMode
        {
            public int Height { get; }

            public int Width { get; }

            private SelfDefine(int height, int width)
            {
                Height = height;
                Width = width;
            }

            public static SelfDefine New(int height, int width) => new(height, width);
        }
    }
}
