using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class SniperFlowTests
    {
        private static Game MakeSniperGame(
            int startX,
            int startY,
            int opponentX = 4,
            int opponentY = 4,
            int? thirdX = null,
            int? thirdY = null
        )
        {
            Board board = Board.FromMode(GameMode.SelfDefine.New(5, 5), out _)!;
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box<GameMode>.New(GameMode.SelfDefine.New(5, 5))));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            List<PlayerBuilder> players = new()
            {
                New(
                    roles: new List<RoleBuilder>
                    {
                        SniperBuilder.New(
                            new UnitBuilder[] { new SniperUnitBuilder() },
                            board.At(startX, startY)!.Value
                        )!,
                    }
                )!,
                New(
                    roles: new List<RoleBuilder>
                    {
                        SniperBuilder.New(
                            new UnitBuilder[] { new SniperUnitBuilder() },
                            board.At(opponentX, opponentY)!.Value
                        )!,
                    }
                )!,
            };
            if (thirdX is int x && thirdY is int y)
            {
                players.Add(
                    New(
                        roles: new List<RoleBuilder>
                        {
                            SniperBuilder.New(
                                new UnitBuilder[] { new SniperUnitBuilder() },
                                board.At(x, y)!.Value
                            )!,
                        }
                    )!
                );
            }
            Assert.NotNull(
                builder.SetPlayers(Box.New(players))
            );
            return builder.Build()!;
        }

        [Fact]
        public static void ExecuteAction_SniperMove_UpdatesGridAndAppendsEvent()
        {
            Game game = MakeSniperGame(2, 2);
            Board board = game.Board;
            Board.Node from = board.At(2, 2)!.Value;
            Board.Node to = board.At(2, 3)!.Value;

            Assert.Equal(new[] { 0 }, board.GetGrid(from)!.Units);

            SniperUnit sniper = (SniperUnit)game.GetUnit(0)!;
            SniperUnitView view = Assert.IsType<SniperUnitView>(sniper.GetView(game));
            Assert.DoesNotContain(view.CanMoveGrids, entry => entry.node == from);
            Assert.Contains(view.CanMoveGrids, entry => entry.node == to);

            game.ExecuteAction(GeneralMove.New(callerUnitId: 0, target: to));

            Assert.Empty(board.GetGrid(from)!.Units);
            Assert.Equal(new[] { 0 }, board.GetGrid(to)!.Units);

            GeneralMoveEvent moveEvent = Assert.IsType<GeneralMoveEvent>(game.Events.Single());
            Assert.Equal(0, moveEvent.UnitId);
            Assert.Equal(new List<Board.Node> { from, to }, moveEvent.Path);
        }

        [Fact]
        public static void UnitEndRound_WhirlpoolAttachPathIncludesStartingSquare()
        {
            Game game = MakeSniperGame(2, 2, opponentX: 5, opponentY: 5);
            Board board = game.Board;
            Board.Node start = board.At(2, 2)!.Value;
            Board.Node whirlpool = board.At(2, 3)!.Value;
            Assert.True(board.SetTerrain(whirlpool, Terrain.Whirlpool));

            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));

            Assert.Equal(whirlpool, game.GetUnit(0)!.Position);
            WhirlpoolAttachEvent attachEvent = Assert.IsType<WhirlpoolAttachEvent>(game.Events.Last());
            Assert.Equal(0, attachEvent.UnitId);
            Assert.Equal(new[] { start, whirlpool }, attachEvent.Path);
        }

        [Fact]
        public static void ExecuteAction_SniperSecondMove_InSameTurn_IsRejected()
        {
            Game game = MakeSniperGame(2, 2);
            Board board = game.Board;
            Board.Node to = board.At(3, 2)!.Value;
            game.ExecuteAction(GeneralMove.New(callerUnitId: 0, target: to));
            Assert.Single(game.Events);
            Assert.Equal(1, game.CurrentPlayerId);

            Board.Node further = board.At(4, 2)!.Value;
            game.ExecuteAction(GeneralMove.New(callerUnitId: 0, target: further));
            Assert.Single(game.Events);
            Assert.Equal(new[] { 0 }, board.GetGrid(to)!.Units);
            Assert.Empty(board.GetGrid(further)!.Units);
        }

        [Fact]
        public static void ExecuteAction_SniperMove_OutOfRange_IsRejected()
        {
            Game game = MakeSniperGame(2, 2);
            Board board = game.Board;
            Board.Node far = board.At(4, 4)!.Value;
            game.ExecuteAction(GeneralMove.New(callerUnitId: 0, target: far));
            Assert.Empty(game.Events);
            Assert.Equal(new[] { 0 }, board.GetGrid(board.At(2, 2)!.Value)!.Units);
        }

        [Fact]
        public static void SniperExecute_KillsTargetAndAppendsEvent()
        {
            Game game = MakeSniperGame(2, 2, opponentX: 3, opponentY: 3, thirdX: 5, thirdY: 1);
            Board board = game.Board;
            Board.Node target = board.At(3, 3)!.Value;

            Assert.Equal(new[] { 1 }, board.GetGrid(target)!.Units);
            game.ExecuteAction(SniperExecute.New(callerUnitId: 0, target: target));

            Assert.False(game.GetUnit(1)!.Alive);
            Assert.Empty(board.GetGrid(target)!.Units);
            Assert.False(game.GetUnit(0)!.HasActionLeft(game));
            ExecuteEvent executeEvent = Assert.IsType<ExecuteEvent>(Assert.Single(game.Events));
            Assert.Equal(1, executeEvent.UnitId);
            Assert.Equal(0, executeEvent.ExecutorId);
        }

        [Fact]
        public static void ExecuteAction_GeneralEnd_AppendsEventAndAdvancesPlayer()
        {
            Game game = MakeSniperGame(2, 2);

            game.ExecuteAction(GeneralEnd.New(callerUnitId: 0));

            GeneralEndEvent endEvent = Assert.IsType<GeneralEndEvent>(Assert.Single(game.Events));
            Assert.Equal(0, endEvent.UnitId);
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void ExecuteAction_GeneralSurrender_KillsUnitsAndAdvancesPlayer()
        {
            Game game = MakeSniperGame(2, 2, thirdX: 5, thirdY: 1);
            Board board = game.Board;
            Board.Node surrenderedUnitPosition = board.At(2, 2)!.Value;

            game.ExecuteAction(GeneralSurrender.New(callerUnitId: 0));

            Assert.False(game.GetUnit(0)!.Alive);
            Assert.Empty(board.GetGrid(surrenderedUnitPosition)!.Units);
            GeneralSurrenderEvent surrenderEvent = Assert.IsType<GeneralSurrenderEvent>(
                Assert.Single(game.Events)
            );
            Assert.Equal(0, surrenderEvent.UnitId);
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void SniperExecute_TargetListExcludesLakeAndIncludesWhirlpool()
        {
            Game game = MakeSniperGame(2, 2);
            Board board = game.Board;
            Board.Node lake = board.At(2, 3)!.Value;
            Board.Node whirlpool = board.At(3, 2)!.Value;
            board.SetTerrain(lake, Terrain.Lake);
            board.SetTerrain(whirlpool, Terrain.Whirlpool);

            SniperUnit sniper = (SniperUnit)game.GetUnit(0)!;
            SniperUnitView view = Assert.IsType<SniperUnitView>(sniper.GetView(game));

            Assert.DoesNotContain(lake, view.CanExecuteGirds);
            Assert.Contains(whirlpool, view.CanExecuteGirds);
        }

        [Fact]
        public static void SniperExecute_LakeTargetIsRejectedWithoutConsumingShot()
        {
            Game game = MakeSniperGame(2, 2);
            Board board = game.Board;
            Board.Node lake = board.At(2, 3)!.Value;
            board.SetTerrain(lake, Terrain.Lake);

            Assert.False(
                game.ExecuteAction(SniperExecute.New(callerUnitId: 0, target: lake))
            );
            Assert.True(game.GetUnit(0)!.HasActionLeft(game));
            Assert.Empty(game.Events);
        }

        [Fact]
        public static void Sniper_AttackAndMove_AreMutuallyExclusive()
        {
            Game attackGame = MakeSniperGame(2, 2);
            Board.Node attackTarget = attackGame.Board.At(2, 3)!.Value;
            Assert.True(attackGame.ExecuteAction(SniperExecute.New(0, attackTarget)));
            Assert.False(attackGame.Roles[0].JudgeAction(
                GeneralMove.New(0, attackGame.Board.At(3, 2)!.Value), attackGame));
            Assert.False(attackGame.Roles[0].JudgeAction(
                SniperExecute.New(0, attackTarget), attackGame));

            Game moveGame = MakeSniperGame(2, 2);
            Assert.True(moveGame.ExecuteAction(
                GeneralMove.New(0, moveGame.Board.At(3, 2)!.Value)));
            Assert.False(moveGame.Roles[0].JudgeAction(
                SniperExecute.New(0, moveGame.Board.At(3, 3)!.Value), moveGame));
        }

        [Fact]
        public static void Sniper_Clone_PreservesConsumedAttack()
        {
            Game game = MakeSniperGame(2, 2);
            Board.Node target = game.Board.At(2, 3)!.Value;
            Assert.True(game.ExecuteAction(SniperExecute.New(0, target)));

            Sniper clone = (Sniper)game.Roles[0].Clone();

            Assert.False(clone.JudgeAction(SniperExecute.New(0, target), game));
            Assert.False(clone.JudgeAction(
                GeneralMove.New(0, game.Board.At(3, 2)!.Value), game));
            Assert.False(clone.HasActionLeft(game));
        }

        [Fact]
        public static void Sniper_NewRound_RestoresAttack()
        {
            Game game = MakeSniperGame(2, 2);
            Board.Node target = game.Board.At(2, 3)!.Value;
            Assert.True(game.GetUnit(0)!.IsFirstAction);
            Assert.True(game.ExecuteAction(SniperExecute.New(0, target)));
            Assert.False(game.GetUnit(0)!.IsFirstAction);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(0, game.CurrentPlayerId);

            Assert.True(game.GetUnit(0)!.IsFirstAction);
            Assert.True(game.Roles[0].IsFirstAction);
            Assert.True(game.GetUnit(0)!.HasActionLeft(game));
            Assert.True(game.ExecuteAction(SniperExecute.New(0, target)));
        }

        [Fact]
        public static void FirstAction_BuildAndDemolish_AllowedOnlyBeforeActing()
        {
            Game game = MakeSniperGame(2, 2);
            Board.Node buildTarget = game.Board.At(3, 2)!.Value;
            Board.Node ownFort = game.Board.At(2, 1)!.Value;
            game.AddBuilding(FortBuilder.New()
                .SetHealth(1)!
                .SetOwner(0)!
                .SetPos(ownFort)!);
            Role role = game.Roles[0];

            Assert.True(role.IsFirstAction);
            Assert.True(role.JudgeAction(GeneralBuildFort.New(0, buildTarget), game));
            Assert.True(role.JudgeAction(GeneralDemolish.New(0, ownFort), game));

            Assert.True(game.ExecuteAction(SniperExecute.New(0, game.Board.At(2, 3)!.Value)));

            Assert.False(role.IsFirstAction);
            Assert.False(role.JudgeAction(GeneralBuildFort.New(0, buildTarget), game));
            Assert.False(role.JudgeAction(GeneralDemolish.New(0, ownFort), game));
            Assert.False(role.Clone().IsFirstAction);
        }

        [Fact]
        public static void FirstAction_MultiUnitRole_IsFalseAfterAnyUnitActs()
        {
            Game game = MakeSniperGame(2, 2);
            Unit untouched = WolfUnit.New(2, 0, game.Board.At(3, 2)!.Value);
            Role group = new FirstActionRoleProbe(0, 0, game.GetUnit(0)!, untouched);
            Assert.True(group.IsFirstAction);

            Assert.True(game.ExecuteAction(SniperExecute.New(0, game.Board.At(2, 3)!.Value)));

            Assert.True(untouched.IsFirstAction);
            Assert.False(group.IsFirstAction);
        }

        [Fact]
        public static void FirstAction_LocksPlayerToTheRoleThatStartedActing()
        {
            GameMode mode = GameMode.SelfDefine.New(5, 5);
            Board board = Board.FromMode(mode, out _)!;
            Unit[] units = new Unit[]
            {
                new FirstActionUnitProbe(0, 0, board.At(2, 2)!.Value),
                new FirstActionUnitProbe(1, 1, board.At(3, 3)!.Value),
                new FirstActionUnitProbe(2, 2, board.At(4, 4)!.Value),
            };
            var first = new FirstActionRoleProbe(0, 0, units[0]);
            var other = new FirstActionRoleProbe(1, 0, units[1]);
            var enemy = new FirstActionRoleProbe(2, 1, units[2]);
            Game game = Game.New(mode, board,
                new Role[] { first, other, enemy }, units,
                new[]
                {
                    Player.New(0, null, new Role[] { first, other }),
                    Player.New(1, null, new Role[] { enemy }),
                },
                Game.GameCount.SingleGameCount.New(2, 2), 0);

            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.False(first.IsFirstAction);
            Assert.True(other.IsFirstAction);
            Assert.False(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(0, other.Executions);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(2, first.Executions);
        }

        // Minimal role double: keep the turn open to exercise role selection.
        private sealed class FirstActionRoleProbe : Role
        {
            private readonly Unit[] _units;
            public int Executions { get; private set; }

            public FirstActionRoleProbe(int id, int playerId, params Unit[] units)
            {
                NewBase(this, id, playerId);
                _units = units;
            }

            public override Role Clone() => new FirstActionRoleProbe(
                Id, PlayerId, _units.Select(unit => unit.Clone()).ToArray());

            public override ReadOnlySpan<Unit> GetUnits() => _units;
            public override bool HasActionLeft(Game game) => true;
            public override bool JudgeAction(MistForest.Core.GameRefactored.Action action, Game game)
                => action is GeneralEnd;

            public override void ExecuteAction(MistForest.Core.GameRefactored.Action action, Game game)
            {
                ++Executions;
                ((FirstActionUnitProbe)_units[0]).MarkAction();
            }
        }

        private sealed class FirstActionUnitProbe : Unit
        {
            public FirstActionUnitProbe(int id, int roleId, Board.Node position)
            {
                NewBase(this, id, roleId, position);
                Health = 3;
            }

            public void MarkAction() => IsFirstAction = false;

            public override Unit Clone()
            {
                var clone = new FirstActionUnitProbe(Id, RoleId, Position);
                CloneBase(clone);
                return clone;
            }

            public override bool HasAction(MistForest.Core.GameRefactored.Action action)
                => action is GeneralEnd;

            public override UnitView GetView(Game game) => throw new NotSupportedException();
        }

        [Fact]
        public static void Sniper_UnableToMove_CanStillAttack()
        {
            Game game = MakeSniperGame(2, 2);
            Board board = game.Board;
            foreach (var (row, col) in new[] { (1, 2), (3, 2), (2, 1), (2, 3) })
            {
                board.SetTerrain(board.At(row, col)!.Value, Terrain.Lake);
            }
            SniperUnit sniper = (SniperUnit)game.GetUnit(0)!;
            SniperUnitView view = (SniperUnitView)sniper.GetView(game);
            Assert.Empty(view.CanMoveGrids);
            Assert.Contains(board.At(4, 2)!.Value, view.CanExecuteGirds);

            Assert.True(sniper.HasActionLeft(game));
            Assert.True(game.ExecuteAction(SniperExecute.New(0, board.At(4, 2)!.Value)));
        }

        [Fact]
        public static void Sniper_ExplicitlyEnded_RejectsMoveAndAttack()
        {
            Game game = MakeSniperGame(2, 2);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));

            Assert.False(game.Roles[0].JudgeAction(
                GeneralMove.New(0, game.Board.At(3, 2)!.Value), game));
            Assert.False(game.Roles[0].JudgeAction(
                SniperExecute.New(0, game.Board.At(2, 3)!.Value), game));
        }
    }
}
