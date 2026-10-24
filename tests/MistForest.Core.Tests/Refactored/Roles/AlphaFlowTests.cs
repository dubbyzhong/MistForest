using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    // Tests cover reusable role logic and general actions through Game.ExecuteAction.
    public class AlphaFlowTests
    {
        private static Game MakeGame()
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            List<PlayerBuilder> players = new()
            {
                New(new List<RoleBuilder>
                {
                    AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() },
                        board.At(3, 3)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() },
                        board.At(7, 7)!.Value)!,
                })!,
            };
            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            return builder.Build()!;
        }

        private static Alpha GetAlpha(Game game) => Assert.IsType<Alpha>(game.Roles[0]);

        private static void Move(Game game, int row, int col)
        {
            Alpha role = GetAlpha(game);
            GeneralMove move = GeneralMove.New(role.Unit.Id, game.Board.At(row, col)!.Value);
            Assert.True(role.JudgeAction(move, game));
            role.ExecuteAction(move, game);
        }

        [Fact]
        public static void ExecuteAction_SplitMovement_UpdatesBoardAndAutomaticallyEndsTurn()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Board.Node from = role.Unit.Position;
            Board.Node middle = game.Board.At(3, 4)!.Value;
            Board.Node target = game.Board.At(3, 5)!.Value;

            Assert.True(game.ExecuteAction(GeneralMove.New(role.Unit.Id, middle)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.False(role.IsFirstAction);
            Assert.Empty(game.Board.GetGrid(from)!.Units);
            Assert.Contains(role.Unit.Id, game.Board.GetGrid(middle)!.Units);

            Assert.True(game.ExecuteAction(GeneralMove.New(role.Unit.Id, target)));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Empty(game.Board.GetGrid(middle)!.Units);
            Assert.Contains(role.Unit.Id, game.Board.GetGrid(target)!.Units);
            Assert.Collection(game.Events,
                first => Assert.Equal(new[] { from, middle },
                    Assert.IsType<GeneralMoveEvent>(first).Path),
                second => Assert.Equal(new[] { middle, target },
                    Assert.IsType<GeneralMoveEvent>(second).Path));

            Assert.False(game.ExecuteAction(GeneralMove.New(role.Unit.Id, middle)));
            Assert.Equal(2, game.Events.Count);
        }

        [Fact]
        public static void ExecuteAction_InvalidMoveAndUnsupportedAction_DoNotConsumeFirstAction()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Board.Node from = role.Unit.Position;
            Board.Node target = game.Board.At(2, 3)!.Value;

            Assert.False(game.ExecuteAction(
                GeneralMove.New(role.Unit.Id, game.Board.At(3, 6)!.Value)));
            Assert.False(game.ExecuteAction(WolfBite.New(role.Unit.Id, target)));
            Assert.True(role.IsFirstAction);
            Assert.Equal(from, role.Unit.Position);
            Assert.Empty(game.Events);
            Assert.Equal(0, game.CurrentPlayerId);

            Assert.True(game.ExecuteAction(GeneralBuildFort.New(role.Unit.Id, target)));
            Assert.IsType<Building.Fort>(Assert.Single(game.Buildings));
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void Builder_CreatesAlphaAndIndependentRoleClone()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Alpha clone = Assert.IsType<Alpha>(role.Clone());

            Assert.Equal(0, role.Id);
            Assert.Equal(0, role.PlayerId);
            Assert.Equal(role.Id, role.Unit.RoleId);
            Assert.Equal(AlphaConsts.AlphaHealth, role.Unit.Health);
            Assert.Equal(1, role.Unit.AttackRange);
            Assert.Equal(game.Board.At(3, 3)!.Value, role.Unit.Position);
            Assert.NotSame(role.Unit, clone.Unit);
            Assert.Equal(role.Unit.Position, clone.Unit.Position);
            Assert.True(clone.IsFirstAction);
        }

        [Fact]
        public static void Builder_RejectsWrongUnitsAndIncompleteConfiguration()
        {
            Board board = Board.New(3, 3);
            Board.Node position = board.At(2, 2)!.Value;
            Assert.Null(AlphaBuilder.New(Array.Empty<UnitBuilder>(), position));
            Assert.Null(AlphaBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, position));
            Assert.Null(AlphaBuilder.New(new UnitBuilder[]
            {
                new AlphaUnitBuilder(), new AlphaUnitBuilder(),
            }, position));
            Assert.Null(new AlphaUnitBuilder().Build());
            Assert.Null(AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, position)!.Build());
        }

        [Fact]
        public static void MoveTwoSquares_ConsumesBudgetAndUpdatesBoardAndEvent()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Board.Node from = role.Unit.Position;
            Board.Node to = game.Board.At(3, 5)!.Value;

            Move(game, 3, 5);

            Assert.Equal(to, role.Unit.Position);
            Assert.Empty(game.Board.GetGrid(from)!.Units);
            Assert.Contains(role.Unit.Id, game.Board.GetGrid(to)!.Units);
            Assert.False(role.IsFirstAction);
            GeneralMoveEvent moveEvent = Assert.IsType<GeneralMoveEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { from, game.Board.At(3, 4)!.Value, to }, moveEvent.Path);
            Assert.False(role.JudgeAction(GeneralMove.New(0, game.Board.At(3, 6)!.Value), game));
            Assert.Empty(role.Unit.GetView(game).CanMoveGrids);
            Assert.False(role.HasActionLeft(game));
        }

        [Fact]
        public static void SplitMovement_AndClonePreserveRemainingDistance()
        {
            Game game = MakeGame();
            Move(game, 3, 4);
            Alpha role = GetAlpha(game);
            Alpha clone = Assert.IsType<Alpha>(role.Clone());
            Board.Node oneAway = game.Board.At(3, 5)!.Value;
            Board.Node twoAway = game.Board.At(3, 6)!.Value;

            Assert.False(clone.IsFirstAction);
            Assert.True(clone.JudgeAction(GeneralMove.New(0, oneAway), game));
            Assert.False(clone.JudgeAction(GeneralMove.New(0, twoAway), game));

            Move(game, 3, 5);
            Assert.Empty(role.Unit.GetView(game).CanMoveGrids);
            Assert.NotEmpty(clone.Unit.GetView(game).CanMoveGrids);
        }

        [Fact]
        public static void Movement_UsesPathLengthAndRejectsCurrentSquare()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            // Going around this wall would need four steps instead of two.
            game.Board.GetGrid(game.Board.At(3, 4)!.Value)!.Terrain = Terrain.Wall;

            Assert.False(role.JudgeAction(GeneralMove.New(0, role.Unit.Position), game));
            Assert.False(role.JudgeAction(GeneralMove.New(0, game.Board.At(3, 4)!.Value), game));
            Assert.False(role.JudgeAction(GeneralMove.New(0, game.Board.At(3, 5)!.Value), game));
            Assert.True(role.JudgeAction(GeneralMove.New(0, game.Board.At(2, 4)!.Value), game));
        }

        [Fact]
        public static void Movement_PreventsBuildingUntilRoundReset()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Board.Node target = game.Board.At(2, 3)!.Value;
            Assert.True(role.JudgeAction(GeneralBuildFort.New(0, target), game));

            Move(game, 3, 4);
            target = game.Board.At(2, 4)!.Value;
            Assert.False(role.JudgeAction(GeneralBuildFort.New(0, target), game));
            Assert.Empty(role.Unit.GetView(game).CanBuildFortGrids);

            role.Unit.EndRound(game);

            Assert.True(role.IsFirstAction);
            Assert.True(role.JudgeAction(GeneralBuildFort.New(0, target), game));
            Assert.True(role.JudgeAction(GeneralMove.New(0, game.Board.At(3, 6)!.Value), game));
        }

        [Fact]
        public static void BuildFort_EndsUnitActionsAndViewCloneOwnsItsArrays()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            AlphaUnitView view = Assert.IsType<AlphaUnitView>(role.Unit.GetView(game));
            AlphaUnitView clone = Assert.IsType<AlphaUnitView>(view.Clone());
            Board.Node originalMove = view.CanMoveGrids[0].node;
            Board.Node originalFort = view.CanBuildFortGrids[0];
            Board.Node originalWall = view.CanBuildWallGrids[0];
            clone.CanMoveGrids[0] = (role.Unit.Position, 0, null);
            clone.CanBuildFortGrids[0] = role.Unit.Position;
            clone.CanBuildWallGrids[0] = role.Unit.Position;
            Assert.Equal(originalMove, view.CanMoveGrids[0].node);
            Assert.Equal(originalFort, view.CanBuildFortGrids[0]);
            Assert.Equal(originalWall, view.CanBuildWallGrids[0]);

            GeneralBuildFort build = GeneralBuildFort.New(0, game.Board.At(2, 3)!.Value);
            Assert.True(game.ExecuteAction(build));

            Assert.True(role.Unit.End);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.False(role.HasActionLeft(game));
            Assert.False(role.JudgeAction(GeneralMove.New(0, game.Board.At(3, 4)!.Value), game));
            Assert.Empty(role.Unit.GetView(game).CanMoveGrids);
            Assert.IsType<Building.Fort>(game.Buildings.Single());
        }

        [Fact]
        public static void BuildWall_CreatesWallAndConsumesTurn()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Board.Node target = game.Board.At(2, 3)!.Value;
            GeneralBuildWall build = GeneralBuildWall.New(0, new[] { target })!;

            Assert.True(game.ExecuteAction(build));

            Building.Wall wall = Assert.IsType<Building.Wall>(game.Buildings.Single());
            Assert.Equal(target, wall.Pos);
            Assert.Equal(0, wall.Owner);
            Assert.False(role.HasActionLeft(game));
            Assert.False(role.IsFirstAction);
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void Demolish_OnlyAllowsAlliedBuildingAndConsumesTurn()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Board.Node allied = game.Board.At(2, 3)!.Value;
            Board.Node enemy = game.Board.At(3, 4)!.Value;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(allied)!);
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(2)!.SetPos(enemy)!);

            Assert.False(game.ExecuteAction(GeneralDemolish.New(0, enemy)));
            Assert.True(role.IsFirstAction);
            Assert.Empty(game.Events);
            GeneralDemolish demolish = GeneralDemolish.New(0, allied);
            Assert.True(game.ExecuteAction(demolish));

            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.IsType<Building.Fort>(game.Buildings[1]);
            Assert.False(role.HasActionLeft(game));
            Assert.IsType<GeneralDemolishEvent>(Assert.Single(game.Events));
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Theory]
        [InlineData("self", true)]
        [InlineData("allied-fort", true)]
        [InlineData("allied-wall", true)]
        [InlineData("enemy-fort", false)]
        [InlineData("rubble", false)]
        [InlineData("out-of-range", false)]
        [InlineData("diagonal", false)]
        [InlineData("line-wall", false)]
        public static void DemolishView_TargetsMatchActionValidation(string situation, bool canDemolish)
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Board.Node target = situation switch
            {
                "self" => role.Unit.Position,
                "out-of-range" => game.Board.At(3, 5)!.Value,
                "diagonal" => game.Board.At(2, 4)!.Value,
                _ => game.Board.At(3, 4)!.Value,
            };
            BuildingBuilder builder = situation == "allied-wall" ? WallBuilder.New() : FortBuilder.New();
            game.AddBuilding(builder.SetOwner(situation == "enemy-fort" ? 1 : 0)!
                .SetHealth(2)!.SetPos(target)!);
            if (situation == "rubble")
            {
                game.Buildings[0].Destroyed(game, 0);
            }
            if (situation == "line-wall")
            {
                int wall = game.Board.VWallAt(role.Unit.Position)!.Value;
                Assert.True(game.Board.VerticalWalls.SetInner(wall / 64, 1ul << (wall % 64), out _));
            }

            UnitView view = role.Unit.GetView(game);

            Assert.Equal(canDemolish, role.JudgeAction(GeneralDemolish.New(role.Unit.Id, target), game));
            if (canDemolish)
            {
                Assert.Equal(new[] { target }, view.CanDemolishGrids);
            }
            else
            {
                Assert.Empty(view.CanDemolishGrids);
            }
            Assert.Empty(game.Events);
            Assert.True(role.Unit.IsFirstAction);
        }

        [Fact]
        public static void DemolishView_CloneOwnsTargetArrayAndExistingViewKeepsSnapshot()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Board.Node target = game.Board.At(3, 4)!.Value;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(target)!);
            UnitView view = role.Unit.GetView(game);
            UnitView clone = view.Clone();

            Assert.Equal(new[] { target }, clone.CanDemolishGrids);
            Assert.NotSame(view.CanDemolishGrids, clone.CanDemolishGrids);
            clone.CanDemolishGrids[0] = role.Unit.Position;
            Assert.Equal(new[] { target }, view.CanDemolishGrids);
            Assert.True(game.ExecuteAction(GeneralDemolish.New(role.Unit.Id, target)));
            Assert.Empty(role.Unit.GetView(game).CanDemolishGrids);
            Assert.Equal(new[] { target }, view.CanDemolishGrids);
        }

        [Theory]
        [InlineData("move")]
        [InlineData("blood")]
        [InlineData("end")]
        [InlineData("surrender")]
        public static void DemolishView_AfterAnAction_HasNoTargets(string kind)
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Board.Node target = game.Board.At(3, 4)!.Value;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(target)!);
            Assert.Contains(target, role.Unit.GetView(game).CanDemolishGrids);
            MistForest.Core.GameRefactored.Action action = kind switch
            {
                "move" => GeneralMove.New(role.Unit.Id, target),
                "blood" => AlphaBlood.New(role.Unit.Id),
                "end" => GeneralEnd.New(role.Unit.Id),
                "surrender" => GeneralSurrender.New(role.Unit.Id),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
            Assert.True(game.ExecuteAction(action));

            Assert.Empty(role.Unit.GetView(game).CanDemolishGrids);
            Assert.False(role.JudgeAction(GeneralDemolish.New(role.Unit.Id, target), game));
        }

        [Fact]
        public static void EndAndSurrender_ReuseExistingUnitAndPlayerBehavior()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            GeneralEnd end = GeneralEnd.New(0);
            Assert.True(game.ExecuteAction(end));
            Assert.False(role.HasActionLeft(game));
            Assert.True(role.Unit.Alive);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.IsType<GeneralEndEvent>(Assert.Single(game.Events));

            game = MakeGame();
            role = GetAlpha(game);
            Board.Node position = role.Unit.Position;
            GeneralSurrender surrender = GeneralSurrender.New(0);
            Assert.True(game.ExecuteAction(surrender));
            Assert.False(game.Players[0].Alive);
            Assert.True(game.Players[1].Alive);
            Assert.Empty(game.Board.GetGrid(position)!.Units);
            Assert.IsType<GeneralSurrenderEvent>(Assert.Single(game.Events));
        }
    }
}
