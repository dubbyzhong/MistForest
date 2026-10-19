using MistForest.Core.Utils;
using Xunit;

namespace MistForest.Core.Tests.Refactored.Utils
{
    public class BoxTests
    {
        [Fact]
        public static void New_Take_RoundTrip()
        {
            Box<string> box = Box.New("hello");
            Assert.Equal("hello", box.Take());
        }

        [Fact]
        public static void TryTake_EmptiesTheBox()
        {
            Box<string> box = Box.New("hello");
            Assert.Equal("hello", box.TryTake());
            Assert.Null(box.TryTake());
        }

        [Fact]
        public static void Empty_TryTake_ReturnsDefault()
        {
            Assert.Null(Box<string>.Empty.TryTake());
        }

        [Fact]
        public static void Map_TransformsValue()
        {
            Assert.Equal("hello!", Box.New("hello").Map(s => s + "!").Take());
            Assert.Null(Box<string>.Empty.Map(s => s + "!").Take());
        }

        [Fact]
        public static void ImplicitConversion_ReadsInner()
        {
            string? full = Box.New("hello");
            Assert.Equal("hello", full);
            string? empty = Box<string>.Empty;
            Assert.Null(empty);
        }

        [Fact]
        public static void From_Ref_MovesValueOut()
        {
            string? value = "hello";
            var box = Box.From(ref value);
            Assert.Null(value);
            Assert.Equal("hello", box.Take());

            string? nullValue = null;
            Assert.Null(Box.From(ref nullValue).TryTake());
        }
    }
}
