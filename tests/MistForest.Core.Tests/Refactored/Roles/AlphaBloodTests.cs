using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;
using GameAction = MistForest.Core.GameRefactored.Action;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class AlphaBloodTests
    {
        private static Game MakeGame(bool withAlly, params (int row, int col, bool detectable, bool alive)[] targets)
            => MakeGame(withAlly, 0, targets);

        private static Game MakeGame(bool withAlly, int firstPlayerId,
            params (int row, int col, bool detectable, bool alive)[] targets)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            List<RoleBuilder> own = new()
            {
                AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, board.At(3, 3)!.Value)!,
            };
            List<RoleBuilder> enemies = new()
            {
                new DetectionRoleBuilder(targets.Select(target => (UnitBuilder)new DetectionUnitBuilder(
                    board.At(target.row, target.col)!.Value, target.detectable, target.alive)).ToArray()),
            };
            if (withAlly)
            {
                own.Add(WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(3, 4)!.Value)!);
                enemies.Add(WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(7, 7)!.Value)!);
            }
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(new List<PlayerBuilder> { New(own)!, New(enemies)! })));
            Assert.NotNull(builder.SetFirstPlayerId(firstPlayerId));
            return builder.Build()!;
        }

        private static AlphaUnit Alpha(Game game) => Assert.IsType<AlphaUnit>(game.GetUnit(0));
        private static AlphaBloodResultEvent Result(Game game) => Assert.Single(game.Events.OfType<AlphaBloodResultEvent>());

        [Fact]
        public static void Blood_UsesDistanceDespiteWallsAndKeepsPositionSnapshotPrivate()
        {
            Game game = MakeGame(false, (3, 5, true, true), (7, 7, true, true));
            Node origin = Alpha(game).Position;
            Node target = game.Board.At(3, 5)!.Value;
            game.Board.GetGrid(game.Board.At(3, 4)!.Value)!.Terrain = Terrain.Wall;
            int wall = game.Board.VWallAt(origin)!.Value;
            Assert.True(game.Board.VerticalWalls.SetInner(wall / 64, 1ul << (wall % 64), out _));

            Assert.True(game.ExecuteAction(AlphaBlood.New(0)));

            Assert.Equal(origin, Assert.Single(game.Events.OfType<AlphaBloodEvent>()).Position);
            Assert.Equal(target, Result(game).Target);
            Assert.Single(game.GetEventsForPlayer(0).OfType<AlphaBloodResultEvent>());
            Assert.Empty(game.GetEventsForPlayer(1).OfType<AlphaBloodResultEvent>());
            Assert.Single(game.GetEventsForPlayer(1).OfType<AlphaBloodEvent>());
            Assert.Empty(game.GetEventsForPlayer(-1));
            Assert.Empty(game.GetEventsForPlayer(2));

            Unit enemy = game.GetUnit(1)!;
            game.Board.GetGrid(enemy.Position)!.Units.Remove(enemy.Id);
            enemy.Position = game.Board.At(6, 6)!.Value;
            game.Board.GetGrid(enemy.Position)!.Units.Add(enemy.Id);
            Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(2, 3)!.Value)));
            Assert.Equal(target, Result(game).Target);
            Assert.Equal(origin, Assert.Single(game.Events.OfType<AlphaBloodEvent>()).Position);
        }

        [Fact]
        public static void Blood_ExcludesAlliesDeadUnitsAndUndetectableUnits()
        {
            Game game = MakeGame(true, (3, 3, false, true), (3, 4, true, false), (3, 5, true, true));

            Assert.True(game.ExecuteAction(AlphaBlood.New(0)));

            Assert.Equal(game.Board.At(3, 5), Result(game).Target);
            Assert.True(game.GetUnit(1)!.CanBeDetected);
        }

        [Fact]
        public static void Blood_NoDetectableEnemy_RecordsNullResultAndStillStartsCooldown()
        {
            Game game = MakeGame(false, (3, 4, false, true));
            Assert.True(game.ExecuteAction(AlphaBlood.New(0)));
            Assert.Null(Result(game).Target);
            Assert.Equal(2, Alpha(game).BloodCooldown);
            Assert.Equal(0, game.CurrentPlayerId);
        }

        [Fact]
        public static void Blood_TiedNearestEnemies_AreChosenUsingGameRandomOnlyOnExecution()
        {
            HashSet<Node> chosen = new();
            for (ulong seed = 1; seed <= 32; ++seed)
            {
                Game game = MakeGame(false, (3, 4, true, true), (4, 3, true, true), (7, 7, true, true));
                game.Board.Random = GameRandom.New(seed)!.Value;
                GameRandom expectedRandom = game.Board.Random;
                Node[] candidates = { game.Board.At(3, 4)!.Value, game.Board.At(4, 3)!.Value };
                Node expected = candidates[expectedRandom.NextInt(0, candidates.Length)!.Value];
                _ = Alpha(game).GetView(game);
                _ = Alpha(game).GetView(game);
                Assert.True(game.Roles[0].JudgeAction(AlphaBlood.New(0), game));

                Assert.True(game.ExecuteAction(AlphaBlood.New(0)));

                Assert.Equal(expected, Result(game).Target);
                chosen.Add(Result(game).Target!.Value);
            }
            Assert.Equal(2, chosen.Count);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Blood_LeavesMovementAndBothAttackChoicesAvailable(bool rush)
        {
            Game game = MakeGame(false, (3, 4, true, true));
            Assert.True(game.ExecuteAction(AlphaBlood.New(0)));
            Assert.False(Alpha(game).IsFirstAction);
            AlphaUnitView view = (AlphaUnitView)Alpha(game).GetView(game);
            Assert.NotEmpty(view.CanMoveGrids);
            Assert.NotEmpty(view.CanAttackGrids);
            Assert.NotEmpty(view.CanAttackRushGrids);
            Assert.False(view.CanBlood);
            Assert.Equal(2, view.BloodCooldown);
            GameAction attack = rush
                ? AlphaAttackRush.New(0, game.GetUnit(1)!.Position)
                : AlphaAttack.New(0, game.GetUnit(1)!.Position);
            Assert.True(game.ExecuteAction(attack));
            Assert.Equal(1, game.GetUnit(1)!.Health);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Equal(2, Alpha(game).BloodCooldown);
        }

        [Fact]
        public static void Blood_RejectsRepeatAndUseAfterMovementWithoutSideEffects()
        {
            Game game = MakeGame(false, (7, 7, true, true));
            Assert.True(game.ExecuteAction(AlphaBlood.New(0)));
            Assert.False(game.ExecuteAction(AlphaBlood.New(0)));
            Assert.Equal(2, game.Events.Count);
            Assert.Equal(2, Alpha(game).BloodCooldown);

            game = MakeGame(false, (7, 7, true, true));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(2, 3)!.Value)));
            Assert.False(game.ExecuteAction(AlphaBlood.New(0)));
            Assert.Single(game.Events);
            Assert.Equal(0, Alpha(game).BloodCooldown);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public static void Blood_CooldownSkipsCastRoundAndTicksOnlyAtRoundEndEvenWhenAnotherRoleActs(int firstPlayerId)
        {
            Game game = MakeGame(true, firstPlayerId, (7, 6, true, true));
            int enemyId = game.Players[1].Roles[0].GetUnits()[0].Id;
            if (firstPlayerId == 1)
            {
                Assert.True(game.ExecuteAction(GeneralEnd.New(enemyId)));
            }
            Assert.True(game.ExecuteAction(AlphaBlood.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(2, Alpha(game).BloodCooldown);
            if (firstPlayerId == 0)
            {
                Assert.True(game.ExecuteAction(GeneralEnd.New(enemyId)));
            }
            Assert.Equal(2, Alpha(game).BloodCooldown);
            for (int remaining = 2; remaining > 0; --remaining)
            {
                if (firstPlayerId == 1)
                {
                    Assert.True(game.ExecuteAction(GeneralEnd.New(enemyId)));
                    Assert.Equal(remaining, Alpha(game).BloodCooldown);
                }
                Assert.False(game.ExecuteAction(AlphaBlood.New(0)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
                if (firstPlayerId == 0)
                {
                    Assert.Equal(remaining, Alpha(game).BloodCooldown);
                    Assert.True(game.ExecuteAction(GeneralEnd.New(enemyId)));
                }
                Assert.Equal(remaining - 1, Alpha(game).BloodCooldown);
            }
            if (firstPlayerId == 1)
            {
                Assert.True(game.ExecuteAction(GeneralEnd.New(enemyId)));
            }
            Assert.True(((AlphaUnitView)Alpha(game).GetView(game)).CanBlood);
            Assert.True(game.ExecuteAction(AlphaBlood.New(0)));
        }

        [Fact]
        public static void Blood_ClonePreservesCooldownAndCastRoundFlagWithoutSharingState()
        {
            Game game = MakeGame(false, (7, 7, true, true));
            Assert.True(game.ExecuteAction(AlphaBlood.New(0)));
            AlphaUnit clone = (AlphaUnit)Alpha(game).Clone();
            AlphaUnitView view = (AlphaUnitView)Alpha(game).GetView(game);
            AlphaUnitView viewClone = (AlphaUnitView)view.Clone();
            Assert.False(viewClone.CanBlood);
            Assert.Equal(2, viewClone.BloodCooldown);
            clone.EndRound(game);
            Assert.Equal(2, clone.BloodCooldown);
            clone.EndRound(game);
            Assert.Equal(1, clone.BloodCooldown);
            Assert.Equal(2, Alpha(game).BloodCooldown);
        }

        [Fact]
        public static void Blood_IsAvailableWhenNoSpatialActionIsPossible()
        {
            Game game = MakeGame(false, (7, 7, true, true));
            foreach ((int row, int col) in new[] { (2, 3), (4, 3), (3, 2), (3, 4) })
            {
                game.Board.GetGrid(game.Board.At(row, col)!.Value)!.Terrain = Terrain.Wall;
            }
            AlphaUnitView view = (AlphaUnitView)Alpha(game).GetView(game);
            Assert.Empty(view.CanMoveGrids);
            Assert.Empty(view.CanAttackGrids);
            Assert.Empty(view.CanBuildFortGrids);
            Assert.True(view.CanBlood);
            Assert.True(game.Roles[0].HasActionLeft(game));
            Assert.True(game.ExecuteAction(AlphaBlood.New(0)));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void HasActionLeft_WhenTrappedAndBloodIsCoolingDown_DependsOnDemolition(bool withAlliedFort)
        {
            Game game = MakeGame(false, (7, 7, true, true));
            Assert.True(game.ExecuteAction(AlphaBlood.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            AlphaUnit alpha = Alpha(game);
            if (withAlliedFort)
            {
                game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(alpha.Position)!);
            }
            foreach ((int row, int col) in new[] { (2, 3), (4, 3), (3, 2), (3, 4) })
            {
                game.Board.GetGrid(game.Board.At(row, col)!.Value)!.Terrain = Terrain.Wall;
            }
            AlphaUnitView view = (AlphaUnitView)alpha.GetView(game);
            Assert.False(view.CanBlood);
            Assert.Empty(view.CanMoveGrids);
            Assert.Empty(view.CanAttackGrids);
            Assert.Empty(view.CanAttackRushGrids);
            Assert.Empty(view.CanBuildFortGrids);
            Assert.Empty(view.CanBuildWallGrids);
            GeneralDemolish demolish = GeneralDemolish.New(alpha.Id, alpha.Position);
            Assert.Equal(withAlliedFort, game.Roles[0].JudgeAction(demolish, game));

            Assert.Equal(withAlliedFort, game.Roles[0].HasActionLeft(game));
            if (withAlliedFort)
            {
                Assert.True(game.ExecuteAction(demolish));
                Assert.IsType<Building.Rubble>(game.Buildings[0]);
                Assert.False(game.Roles[0].HasActionLeft(game));
                Assert.Equal(1, game.CurrentPlayerId);
            }
        }

        private sealed class DetectionUnit : Unit
        {
            private readonly bool _detectable;
            public override bool CanBeDetected => _detectable;
            public DetectionUnit(int id, int roleId, Node position, bool detectable, bool alive)
            {
                NewBase(this, id, roleId, position);
                Health = alive ? 3 : 0;
                _detectable = detectable;
            }
            public override Unit Clone()
            {
                DetectionUnit target = new(Id, RoleId, Position, _detectable, Alive);
                CloneBase(target);
                return target;
            }
            public override UnitView GetView(Game game) => throw new NotSupportedException();
            public override bool HasAction(GameAction action) => action is GeneralEnd;
        }

        private sealed class DetectionUnitBuilder : UnitBuilder
        {
            private readonly bool _detectable;
            private readonly bool _alive;
            public DetectionUnitBuilder(Node position, bool detectable, bool alive)
            {
                SetPosition(position);
                _detectable = detectable;
                _alive = alive;
            }
            public override Unit? Build() => Built ??= new DetectionUnit(Id!.Value, RoleId!.Value,
                Position!.Value, _detectable, _alive);
        }

        private sealed class DetectionRole : Role
        {
            private readonly Unit[] _units;
            public DetectionRole(int id, int playerId, Unit[] units)
            {
                NewBase(this, id, playerId);
                _units = units;
            }
            public override Role Clone() => new DetectionRole(Id, PlayerId, _units.Select(unit => unit.Clone()).ToArray());
            public override ReadOnlySpan<Unit> GetUnits() => _units;
            public override bool HasActionLeft(Game game) => false;
            public override bool JudgeAction(GameAction action, Game game) => action is GeneralEnd;
            public override void ExecuteAction(GameAction action, Game game)
            {
                foreach (Unit unit in _units) unit.GeneralEnd((GeneralEnd)action, game);
            }
        }

        private sealed class DetectionRoleBuilder : RoleBuilder
        {
            public DetectionRoleBuilder(UnitBuilder[] units) : base(units) { }
            public override bool CanInitialStay(Board board) => true;
            public override Role? Build() => Built ??= new DetectionRole(Id!.Value, PlayerId!.Value,
                UnitBuilder.Select(unit => unit.Build()!).ToArray());
        }
    }
}
