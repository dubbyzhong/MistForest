using MistForest.Core.Model;
using Xunit;

namespace MistForest.Core.Tests
{
    public class MapCodeTests
    {
        [Fact]
        public void Encode_DefaultHeroesMap_HasSizeHeaderAndCrossWalls()
        {
            Map map = MapRules.DefaultMap(Mode.Heroes);
            string encoded = MapCode.Encode(map);

            Assert.Equal(227, encoded.Length);
            Assert.Equal("55", encoded[0..2]);

            foreach (int cell in new int[] { 1, 5, 13, 21, 25 })
            {
                Assert.Equal('1', encoded[2 + cell - 1]);
            }
            Assert.Equal('0', encoded[2 + 11 - 1]);
        }

        [Fact]
        public void EncodeDecode_RoundTrip_PreservesEverything()
        {
            Map original = MapRules.DefaultMap(Mode.Heroes);
            original.Cells[7] = Terrain.Lake;
            original.Cells[12] = Terrain.Whirlpool;
            original.HorizontalWalls[8] = true;
            original.VerticalWalls[9] = true;
            string encoded = MapCode.Encode(original);
            bool ok = MapCode.TryDecode(Mode.Heroes, encoded, out Map decoded);

            Assert.True(ok);
            Assert.Equal(original.Width, decoded.Width);
            Assert.Equal(original.Height, decoded.Height);
            Assert.Equal(original.Cells, decoded.Cells);
            Assert.Equal(original.HorizontalWalls, decoded.HorizontalWalls);
            Assert.Equal(original.VerticalWalls, decoded.VerticalWalls);
        }

        [Theory]
        [InlineData("")]
        [InlineData("55")]
        public void TryDecode_WrongLength_Fails(string text)
        {
            Assert.False(MapCode.TryDecode(Mode.Heroes, text, out Map map));
            Assert.Equal(5, map.Width);
        }

        [Fact]
        public void TryDecode_SizeHeaderZero_Fails()
        {
            string text = new string('0', 227);
            Assert.False(MapCode.TryDecode(Mode.Heroes, text, out Map map));
            Assert.Equal(5, map.Width);
        }

        [Fact]
        public void RealMapFiles_DecodeAndRoundTrip()
        {
            string[] files = Directory.GetFiles("MapFiles", "*.mistmap");
            Assert.True(files.Length >= 5);

            foreach (string file in files)
            {
                string[] lines = File.ReadAllLines(file);
                string[] header = lines[0].Split(' ');
                Assert.Equal("MIST_FOREST_MAP", header[0]);
                Assert.Equal("1", header[1]);

                string encoded = lines[1].Trim();
                Assert.True(MapCode.TryDecode(Mode.Heroes, encoded, out Map map), file);

                Assert.True(
                    MapCode.TryDecode(Mode.Heroes, MapCode.Encode(map), out Map again),
                    file
                );

                Assert.Equal(map.Width, again.Width);
                Assert.Equal(map.Height, again.Height);
                Assert.Equal(map.Cells, again.Cells);
                Assert.Equal(map.HorizontalWalls, again.HorizontalWalls);
                Assert.Equal(map.VerticalWalls, again.VerticalWalls);
            }
        }
    }
}
