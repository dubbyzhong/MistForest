using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class InkDashTests
    {
        private static Game MakeGame(bool withAlly = false, int firstPlayerId = 0,
            int enemyRow = 7, int enemyCol = 7, int startRow = 3, int startCol = 3)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            List<RoleBuilder> own = new()
            {
                InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, board.At(startRow, startCol)!.Value)!,
            };
            List<RoleBuilder> enemies = new()
            {
                WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(enemyRow, enemyCol)!.Value)!,
            };
            if (withAlly)
            {
                own.Add(WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(1, 1)!.Value)!);
                enemies.Add(AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, board.At(7, 6)!.Value)!);
            }
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(new List<PlayerBuilder> { New(own)!, New(enemies)! })));
            Assert.NotNull(builder.SetFirstPlayerId(firstPlayerId));
            return builder.Build()!;
        }

        private static Ink Role(Game game) => Assert.IsType<Ink>(game.Roles[0]);
        private static InkUnit Unit(Game game) => Role(game).Unit;
        private static InkUnitView View(Game game) => Assert.IsType<InkUnitView>(Unit(game).GetView(game));

        private static void PlaceUnit(Game game, MistForest.Core.GameRefactored.Unit unit, Node target)
        {
            game.Board.GetGrid(unit.Position)!.Units.Remove(unit.Id);
            unit.Position = target;
            game.Board.GetGrid(target)!.Units.Add(unit.Id);
        }

        [Fact]
        public static void Dash_UpdatesBothStepsWithoutDamageOrSpendingOrdinaryBudgets()
        {
            Game game = MakeGame(enemyRow: 3, enemyCol: 5);
            Node origin = Unit(game).Position;
            Node middle = game.Board.At(3, 4)!.Value;
            Node target = game.Board.At(3, 5)!.Value;
            Assert.Contains(target, View(game).CanDashGrids);

            Assert.True(game.ExecuteAction(InkDash.New(0, target)));

            Assert.Equal(target, Unit(game).Position);
            Assert.Empty(game.Board.GetGrid(origin)!.Units);
            Assert.Empty(game.Board.GetGrid(middle)!.Units);
            Assert.Equal(new[] { 0, 1 }, game.Board.GetGrid(target)!.Units.OrderBy(id => id));
            Assert.Equal(3, Unit(game).Health);
            Assert.Equal(3, game.GetUnit(1)!.Health);
            Assert.Equal(0, game.GetUnit(1)!.HighestDirectDamage);
            Assert.Equal(0, View(game).AttackUsed);
            Assert.NotEmpty(View(game).CanMoveGrids);
            Assert.NotEmpty(View(game).CanAttackGrids);
            Assert.Empty(View(game).CanBuildFortGrids);
            Assert.Empty(View(game).CanBuildWallGrids);
            Assert.Empty(View(game).CanDemolishGrids);
            Assert.Equal(2, View(game).DashCooldown);
            Assert.Empty(View(game).CanDashGrids);
            Assert.False(Unit(game).IsFirstAction);
            Assert.Equal(0, game.CurrentPlayerId);
            GeneralMoveEvent moved = Assert.IsType<GeneralMoveEvent>(Assert.Single(game.Events));
            Assert.Equal(0, moved.UnitId);
            Assert.Equal(new[] { origin, middle, target }, moved.Path);
            Assert.False(game.ExecuteAction(InkDash.New(0, origin)));
            Assert.Single(game.Events);
        }

        [Theory]
        [InlineData("self")]
        [InlineData("one-step")]
        [InlineData("three-steps")]
        [InlineData("knight-shape")]
        [InlineData("lake")]
        [InlineData("terrain-wall")]
        [InlineData("enemy-fort")]
        [InlineData("allied-wall")]
        [InlineData("middle-wall")]
        [InlineData("middle-fort")]
        [InlineData("first-line-wall")]
        [InlineData("second-line-wall")]
        [InlineData("out-of-board")]
        public static void Dash_InvalidPath_IsRejectedWithoutSideEffects(string obstruction)
        {
            Game game = MakeGame();
            Node origin = Unit(game).Position;
            Node middle = game.Board.At(3, 4)!.Value;
            Node target = game.Board.At(3, 5)!.Value;
            switch (obstruction)
            {
                case "self": target = origin; break;
                case "one-step": target = middle; break;
                case "three-steps": target = game.Board.At(3, 6)!.Value; break;
                case "knight-shape": target = game.Board.At(5, 4)!.Value; break;
                case "lake": game.Board.GetGrid(target)!.Terrain = Terrain.Lake; break;
                case "terrain-wall": game.Board.GetGrid(target)!.Terrain = Terrain.Wall; break;
                case "enemy-fort":
                    game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(1)!.SetPos(target)!);
                    break;
                case "allied-wall":
                    game.AddBuilding(WallBuilder.New().SetOwner(0)!.SetHealth(1)!.SetPos(target)!);
                    break;
                case "middle-wall": game.Board.GetGrid(middle)!.Terrain = Terrain.Wall; break;
                case "middle-fort":
                    game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(1)!.SetPos(middle)!);
                    break;
                case "first-line-wall":
                case "second-line-wall":
                    int wall = game.Board.VWallAt(obstruction == "first-line-wall" ? origin : middle)!.Value;
                    Assert.True(game.Board.VerticalWalls.SetInner(wall / 64, 1ul << (wall % 64), out _));
                    break;
                case "out-of-board":
                    target = Board.FromMode(GameMode.SelfDefine.New(8, 8), out _)!.At(8, 8)!.Value;
                    break;
            }

            Assert.DoesNotContain(target, View(game).CanDashGrids);
            Assert.False(game.ExecuteAction(InkDash.New(0, target)));

            Assert.Equal(origin, Unit(game).Position);
            Assert.Equal(new[] { 0 }, game.Board.GetGrid(origin)!.Units);
            Assert.Equal(0, Unit(game).DashCooldown);
            Assert.Equal(0, View(game).AttackUsed);
            Assert.True(Unit(game).IsFirstAction);
            Assert.False(Unit(game).End);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Empty(game.Events);
            Assert.All(game.Buildings, building =>
            {
                Assert.Equal(1, building.Health);
                Assert.Equal(0, building.HighestDirectDamage);
            });
        }

        [Theory]
        [InlineData("turn")]
        [InlineData("allied-forts")]
        [InlineData("rubble")]
        [InlineData("whirlpool")]
        [InlineData("alternative-path")]
        [InlineData("edge")]
        public static void Dash_LegalTarget_ProducesAValidTwoStepPath(string kind)
        {
            Game game = MakeGame(startRow: kind == "edge" ? 1 : 3, startCol: kind == "edge" ? 1 : 3);
            Node origin = Unit(game).Position;
            Node target = kind switch
            {
                "turn" or "alternative-path" => game.Board.At(4, 4)!.Value,
                "edge" => game.Board.At(1, 3)!.Value,
                _ => game.Board.At(3, 5)!.Value,
            };
            if (kind == "allied-forts")
            {
                game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(3)!.SetPos(game.Board.At(3, 4)!.Value)!);
                game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(3)!.SetPos(target)!);
            }
            if (kind == "rubble")
            {
                game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(1)!.SetPos(game.Board.At(3, 4)!.Value)!);
                game.Buildings[0].Destroyed(game, 0);
            }
            if (kind == "whirlpool")
            {
                game.Board.GetGrid(target)!.Terrain = Terrain.Whirlpool;
            }
            if (kind == "alternative-path")
            {
                game.Board.GetGrid(game.Board.At(3, 4)!.Value)!.Terrain = Terrain.Wall;
            }

            Assert.Contains(target, View(game).CanDashGrids);
            Assert.True(game.ExecuteAction(InkDash.New(0, target)));

            GeneralMoveEvent moved = Assert.IsType<GeneralMoveEvent>(Assert.Single(game.Events));
            Assert.Equal(3, moved.Path.Length);
            Assert.Equal(origin, moved.Path[0]);
            Assert.Equal(target, moved.Path[2]);
            Assert.Equal(2, game.Board.Distance(origin, target));
            for (int step = 1; step < moved.Path.Length; ++step)
            {
                Assert.Equal(1, game.Board.Distance(moved.Path[step - 1], moved.Path[step]));
                Assert.True(game.Board.GetGrid(moved.Path[step])!.CanStay(0, game));
                Assert.True(game.Board.CanLineShot(moved.Path[step - 1], moved.Path[step], true,
                    node => game.Board.GetGrid(node)!.CanStay(0, game)));
            }
            Assert.Equal(target, Unit(game).Position);
            if (kind == "allied-forts")
            {
                Assert.All(game.Buildings, building => Assert.Equal(3, building.Health));
            }
        }

        [Fact]
        public static void Dash_AttackAndOrdinaryMoveRemainIndependentAndRangesFollowPosition()
        {
            Game game = MakeGame(enemyRow: 3, enemyCol: 6);
            Node enemy = game.GetUnit(1)!.Position;
            Assert.DoesNotContain(enemy, View(game).CanAttackGrids);

            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(3, 5)!.Value)));
            Assert.Contains(enemy, View(game).CanAttackGrids);
            Assert.True(game.ExecuteAction(InkAttack.New(0, enemy)));
            Assert.Equal(0, Unit(game).DashCooldown);
            Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(2, 5)!.Value)));
            Assert.Empty(View(game).CanMoveGrids);
            Assert.NotEmpty(View(game).CanDashGrids);
            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(2, 3)!.Value)));

            Assert.Equal(1, View(game).AttackUsed);
            Assert.Equal(2, Unit(game).DashCooldown);
            Assert.Equal(2, game.GetUnit(1)!.Health);
            Assert.Empty(View(game).CanMoveGrids);
            Assert.DoesNotContain(enemy, View(game).CanAttackGrids);
            Assert.Equal(3, game.Events.OfType<GeneralMoveEvent>().Count());
            Assert.Single(game.Events.OfType<InkAttackEvent>());
            Assert.Equal(0, game.CurrentPlayerId);
        }

        [Fact]
        public static void PelletDamage_RefreshesDashRepeatedlyButCoveredDamageDoesNot()
        {
            Game game = MakeGame(withAlly: true, enemyRow: 4, enemyCol: 4);
            Node first = game.GetUnit(2)!.Position;
            Node second = game.Board.At(4, 6)!.Value;
            PlaceUnit(game, game.GetUnit(3)!, second);
            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(3, 5)!.Value)));
            Assert.Equal(2, Unit(game).DashCooldown);

            Assert.True(game.ExecuteAction(InkAttack.New(0, first)));
            Assert.Equal(0, Unit(game).DashCooldown);
            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(5, 5)!.Value)));
            Assert.True(game.ExecuteAction(InkAttack.New(0, first)));
            Assert.Equal(2, game.GetUnit(2)!.Health);
            Assert.Equal(2, Unit(game).DashCooldown);
            Assert.False(game.ExecuteAction(InkDash.New(0, game.Board.At(5, 3)!.Value)));
            Assert.True(game.ExecuteAction(InkAttack.New(0, second)));
            Assert.Equal(0, Unit(game).DashCooldown);
            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(5, 3)!.Value)));

            Assert.Equal(3, View(game).AttackUsed);
            Assert.Equal(2, Unit(game).DashCooldown);
            Assert.Equal(2, game.GetUnit(3)!.Health);
            Assert.Equal(3, game.Events.OfType<GeneralMoveEvent>().Count());
            Assert.Equal(3, game.Events.OfType<InkAttackEvent>().Count());
        }

        [Theory]
        [InlineData("empty")]
        [InlineData("friendly-unit")]
        [InlineData("enemy-fort")]
        [InlineData("allied-fort")]
        [InlineData("enemy-wall")]
        [InlineData("allied-wall")]
        [InlineData("protected-enemy")]
        public static void PelletWithoutUnitDamage_DoesNotRefreshDash(string kind)
        {
            Game game = MakeGame(withAlly: true, enemyRow: 4, enemyCol: 4);
            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(3, 5)!.Value)));
            Node target = game.Board.At(2, 5)!.Value;
            if (kind == "friendly-unit")
            {
                PlaceUnit(game, game.GetUnit(1)!, target);
            }
            if (kind is "enemy-fort" or "allied-fort" or "enemy-wall" or "allied-wall")
            {
                BuildingBuilder builder = kind.EndsWith("wall") ? WallBuilder.New() : FortBuilder.New();
                game.AddBuilding(builder.SetOwner(kind.StartsWith("allied") ? 0 : 1)!.SetHealth(3)!.SetPos(target)!);
            }
            if (kind == "protected-enemy")
            {
                target = game.Board.At(4, 5)!.Value;
                PlaceUnit(game, game.GetUnit(2)!, target);
                game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(1)!.SetPos(target)!);
            }

            Assert.True(game.ExecuteAction(InkAttack.New(0, target)));

            Assert.Equal(2, Unit(game).DashCooldown);
            Assert.Equal(1, View(game).AttackUsed);
            Assert.Empty(View(game).CanDashGrids);
            Assert.Equal(3, game.GetUnit(1)!.Health);
            Assert.Equal(3, game.GetUnit(2)!.Health);
            Assert.False(game.ExecuteAction(InkDash.New(0, game.Board.At(5, 5)!.Value)));
            Assert.Equal(2, game.Events.Count);
        }

        [Fact]
        public static void PelletKillingEnemy_StillRefreshesDash()
        {
            Game game = MakeGame(withAlly: true, enemyRow: 4, enemyCol: 4);
            Node target = game.GetUnit(2)!.Position;
            for (int round = 0; round < 2; ++round)
            {
                Assert.True(game.ExecuteAction(InkAttack.New(0, target)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            }
            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(3, 5)!.Value)));

            Assert.True(game.ExecuteAction(InkAttack.New(0, target)));

            Assert.False(game.GetUnit(2)!.Alive);
            Assert.Equal(0, Unit(game).DashCooldown);
            Assert.NotEmpty(View(game).CanDashGrids);
            Assert.Equal(0, game.CurrentPlayerId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public static void Cooldown_SkipsCastRoundAndTicksAtRoundEndWhenAnotherRoleActs(int firstPlayerId)
        {
            Game game = MakeGame(withAlly: true, firstPlayerId: firstPlayerId);
            int enemyId = game.Players[1].Roles[0].GetUnits()[0].Id;
            if (firstPlayerId == 1)
            {
                Assert.True(game.ExecuteAction(GeneralEnd.New(enemyId)));
            }
            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(3, 5)!.Value)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(2, Unit(game).DashCooldown);
            if (firstPlayerId == 0)
            {
                Assert.True(game.ExecuteAction(GeneralEnd.New(enemyId)));
            }
            Assert.Equal(2, Unit(game).DashCooldown);
            for (int remaining = 2; remaining > 0; --remaining)
            {
                if (firstPlayerId == 1)
                {
                    Assert.True(game.ExecuteAction(GeneralEnd.New(enemyId)));
                    Assert.Equal(remaining, Unit(game).DashCooldown);
                }
                Assert.False(game.ExecuteAction(InkDash.New(0, game.Board.At(3, 3)!.Value)));
                Assert.Empty(View(game).CanDashGrids);
                Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
                if (firstPlayerId == 0)
                {
                    Assert.Equal(remaining, Unit(game).DashCooldown);
                    Assert.True(game.ExecuteAction(GeneralEnd.New(enemyId)));
                }
                Assert.Equal(remaining - 1, Unit(game).DashCooldown);
            }
            if (firstPlayerId == 1)
            {
                Assert.True(game.ExecuteAction(GeneralEnd.New(enemyId)));
            }
            Assert.Contains(game.Board.At(3, 3)!.Value, View(game).CanDashGrids);
            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(3, 3)!.Value)));
        }

        [Fact]
        public static void Clone_PreservesCooldownAndCastRoundFlagWithoutSharingState()
        {
            Game game = MakeGame();
            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(3, 5)!.Value)));
            Ink clone = (Ink)Role(game).Clone();
            InkUnitView view = View(game);
            InkUnitView viewClone = (InkUnitView)view.Clone();
            Assert.Equal(2, viewClone.DashCooldown);
            Assert.Empty(viewClone.CanDashGrids);

            clone.Unit.EndRound(game);
            Assert.Equal(2, clone.Unit.DashCooldown);
            clone.Unit.EndRound(game);
            Assert.Equal(1, clone.Unit.DashCooldown);
            Assert.Equal(2, Unit(game).DashCooldown);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Ink afterRound = (Ink)Role(game).Clone();
            afterRound.Unit.EndRound(game);
            Assert.Equal(1, afterRound.Unit.DashCooldown);
            afterRound.Unit.EndRound(game);
            afterRound.Unit.EndRound(game);
            Assert.Equal(0, afterRound.Unit.DashCooldown);
            Assert.Equal(2, Unit(game).DashCooldown);
            Assert.Equal(2, viewClone.DashCooldown);
        }

        [Fact]
        public static void ViewClone_OwnsDashTargetsAndKeepsCooldownSnapshot()
        {
            Game game = MakeGame();
            InkUnitView view = View(game);
            InkUnitView clone = (InkUnitView)view.Clone();
            Node original = view.CanDashGrids[0];
            clone.CanDashGrids[0] = Unit(game).Position;

            Assert.NotSame(view.CanDashGrids, clone.CanDashGrids);
            Assert.Equal(original, view.CanDashGrids[0]);
            Assert.True(game.ExecuteAction(InkDash.New(0, original)));
            Assert.Equal(2, View(game).DashCooldown);
            Assert.Empty(View(game).CanDashGrids);
            Assert.Equal(0, view.DashCooldown);
            Assert.NotEmpty(view.CanDashGrids);
        }

        [Fact]
        public static void ViewQueries_DoNotOverwriteAValidatedDashPath()
        {
            Game game = MakeGame();
            InkDash dash = InkDash.New(0, game.Board.At(3, 5)!.Value);
            Assert.True(Role(game).JudgeAction(dash, game));
            _ = View(game);
            _ = View(game);

            Role(game).ExecuteAction(dash, game);

            GeneralMoveEvent moved = Assert.IsType<GeneralMoveEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { game.Board.At(3, 3)!.Value, game.Board.At(3, 4)!.Value, dash.Target }, moved.Path);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, false)]
        public static void DashView_UsesUnitOwnerWhenAnotherPlayerIsActing(int fortOwner, bool canDash)
        {
            Game game = MakeGame(firstPlayerId: 1);
            Node target = game.Board.At(3, 5)!.Value;
            game.AddBuilding(FortBuilder.New().SetOwner(fortOwner)!.SetHealth(3)!
                .SetPos(game.Board.At(3, 4)!.Value)!);

            Assert.Equal(canDash, View(game).CanDashGrids.Contains(target));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(canDash, game.ExecuteAction(InkDash.New(0, target)));
        }
    }
}
