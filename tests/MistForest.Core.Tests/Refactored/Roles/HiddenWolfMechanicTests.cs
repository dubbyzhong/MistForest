using System.Reflection;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;
using GameAction = MistForest.Core.GameRefactored.Action;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class HiddenWolfMechanicTests
    {
        private static Game MakeGame(bool extraRoles = false, bool hiddenEnemy = false)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            List<RoleBuilder> own = new()
            {
                HiddenWolfBuilder.New(new UnitBuilder[] { new HiddenWolfUnitBuilder() }, board.At(3, 3)!.Value)!,
            };
            List<RoleBuilder> enemy = new()
            {
                hiddenEnemy
                    ? HiddenWolfBuilder.New(new UnitBuilder[] { new HiddenWolfUnitBuilder() }, board.At(7, 7)!.Value)!
                    : WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(7, 7)!.Value)!,
            };
            if (extraRoles)
            {
                own.Add(WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(1, 1)!.Value)!);
                enemy.Add(AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, board.At(7, 6)!.Value)!);
            }
            GameBuilder builder = new();
            builder.SetMode(Box.New(mode))!.SetBoard(Box.New(board))!.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(own)!, New(enemy)!,
            }));
            return builder.Build()!;
        }

        private static HiddenWolf Role(Game game) => Assert.IsType<HiddenWolf>(game.Roles[0]);
        private static HiddenWolfUnitView View(Game game) => Assert.IsType<HiddenWolfUnitView>(Role(game).Unit.GetView(game));
        private static Board.Node At(Game game, int row, int col) => game.Board.At(row, col)!.Value;
        private static int EnemyId(Game game) => game.Players[1].Roles[0].GetUnits()[0].Id;

        private static void NextOwnTurn(Game game)
        {
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(EnemyId(game))));
            Assert.Equal(0, game.CurrentPlayerId);
        }

        private static void Place(Game game, Unit unit, Board.Node position)
        {
            game.Board.GetGrid(unit.Position)!.Units.Remove(unit.Id);
            unit.Position = position;
            game.Board.GetGrid(position)!.Units.Add(unit.Id);
        }

        [Fact]
        public static void Transform_PreservesOrdinaryActionButLocksRoleAndOnlyResetsAtOwnTurnStart()
        {
            Game game = MakeGame(extraRoles: true);
            HiddenWolf role = Role(game);
            HiddenWolfUnitView initial = View(game);
            Assert.Equal(HiddenWolfForm.Human, initial.Form);
            Assert.True(initial.CanTransform);
            Assert.False(role.HasStartedTurn);

            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));

            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(role.IsFirstAction);
            Assert.True(role.HasStartedTurn);
            Assert.True(role.Unit.HasTransformedThisTurn);
            Assert.Equal(HiddenWolfForm.Wolf, View(game).Form);
            Assert.False(View(game).CanTransform);
            Assert.True(role.Unit.CanBeDetected);
            Assert.Equal(1, role.Unit.AttackRange);
            Assert.Equal(2, View(game).CanMoveGrids.Max(entry => entry.distance));
            Assert.Equal(0, role.Unit.TemporaryShield);
            Assert.False(game.ExecuteAction(HiddenWolfTransform.New(0)));
            Assert.False(game.ExecuteAction(GeneralMove.New(1, At(game, 1, 2))));
            Assert.False(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.True(role.IsFirstAction);
            HiddenWolf clone = Assert.IsType<HiddenWolf>(role.Clone());
            Assert.True(clone.IsFirstAction);
            Assert.True(clone.HasStartedTurn);

            NextOwnTurn(game);
            Assert.Equal(HiddenWolfForm.Wolf, role.Unit.Form);
            Assert.False(role.HasStartedTurn);
            Assert.False(role.Unit.HasTransformedThisTurn);
            Assert.True(View(game).CanTransform);
            Assert.True(game.ExecuteAction(GeneralMove.New(1, At(game, 1, 2))));
            Assert.False(game.ExecuteAction(HiddenWolfTransform.New(0)));
            Assert.True(clone.HasStartedTurn);
            Assert.Equal(HiddenWolfForm.Human, initial.Form);
        }

        [Theory]
        [InlineData("fort", false)]
        [InlineData("wall", false)]
        [InlineData("demolish", false)]
        [InlineData("fort", true)]
        [InlineData("wall", true)]
        [InlineData("demolish", true)]
        public static void Transform_AllowsFirstActionConstructionAndDemolition(string kind, bool returnHuman)
        {
            Game game = MakeGame();
            Board.Node target = At(game, 2, 3);
            if (kind == "demolish")
            {
                game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(4)!.SetPos(target)!);
            }
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
            if (returnHuman)
            {
                NextOwnTurn(game);
                Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
                Assert.Equal(2, Role(game).Unit.TemporaryShield);
            }
            GameAction action = kind switch
            {
                "fort" => GeneralBuildFort.New(0, target),
                "wall" => GeneralBuildWall.New(0, new[] { target })!,
                _ => GeneralDemolish.New(0, target),
            };
            Assert.Contains(target, kind switch
            {
                "fort" => View(game).CanBuildFortGrids,
                "wall" => View(game).CanBuildWallGrids,
                _ => View(game).CanDemolishGrids,
            });
            Assert.True(game.ExecuteAction(action));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.True(Role(game).Unit.End);
            Assert.False(Role(game).IsFirstAction);
            Assert.False(View(game).CanTransform);
            Assert.Equal(kind switch
            {
                "fort" => typeof(Building.Fort),
                "wall" => typeof(Building.Wall),
                _ => typeof(Building.Rubble),
            }, game.Buildings[0].GetType());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WolfMove_CanUseTwoGridsTogetherOrSeparatelyButCannotAttackAfterTwo(bool split)
        {
            Game game = MakeGame();
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
            if (split)
            {
                Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 3, 4))));
                Assert.Equal(0, game.CurrentPlayerId);
                Assert.False(View(game).CanTransform);
                Assert.NotEmpty(View(game).CanAttackGrids);
                Assert.All(View(game).CanMoveGrids, entry => Assert.Equal(1, entry.distance));
            }
            Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 3, 5))));
            Assert.Equal(At(game, 3, 5), Role(game).Unit.Position);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Empty(View(game).CanAttackGrids);
            Assert.Empty(View(game).CanMoveGrids);
            Assert.False(Role(game).JudgeAction(HiddenWolfAttack.New(0, At(game, 3, 6)), game));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WolfAttack_AllowsOnlyOneExtraMovementGridWithOrWithoutAnOpeningStep(bool openingStep)
        {
            Game game = MakeGame();
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
            if (openingStep)
            {
                Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 3, 4))));
            }
            Board.Node target = At(game, openingStep ? 2 : 3, openingStep ? 4 : 4);
            Assert.True(game.ExecuteAction(HiddenWolfAttack.New(0, target)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Empty(View(game).CanAttackGrids);
            Assert.False(View(game).CanTransform);
            Assert.All(View(game).CanMoveGrids, entry => Assert.Equal(1, entry.distance));
            Assert.False(game.ExecuteAction(GeneralMove.New(0, At(game, 3, openingStep ? 6 : 5))));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 4, openingStep ? 4 : 3))));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Empty(View(game).CanMoveGrids);
        }

        [Fact]
        public static void WolfAttack_AutomaticallyEndsWhenExtraMovementIsBlocked()
        {
            Game game = MakeGame();
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
            foreach ((int row, int col) in new[] { (2, 3), (4, 3), (3, 2), (3, 4) })
            {
                game.Board.SetTerrain(At(game, row, col), Terrain.Wall);
            }
            Assert.True(game.ExecuteAction(HiddenWolfAttack.New(0, Role(game).Unit.Position)));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.False(Role(game).HasActionLeft(game));
        }

        [Fact]
        public static void ReturningHuman_GrantsShieldAndRestoresHumanActionAndDetectionRules()
        {
            Game game = MakeGame();
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
            NextOwnTurn(game);
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
            Assert.Equal(HiddenWolfForm.Human, Role(game).Unit.Form);
            Assert.Equal(2, View(game).TemporaryShield);
            Assert.Equal(2, Role(game).Unit.TemporaryShield);
            Assert.False(Role(game).Unit.CanBeDetected);
            Assert.Equal(2, Role(game).Unit.AttackRange);
            Assert.All(View(game).CanMoveGrids, entry => Assert.Equal(1, entry.distance));
            Assert.False(game.ExecuteAction(HiddenWolfTransform.New(0)));
            Assert.Equal(2, Role(game).Unit.TemporaryShield);
            Assert.True(game.ExecuteAction(HiddenWolfAttack.New(0, At(game, 3, 5))));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Empty(game.Events.OfType<HiddenWolfInstinctEvent>());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WolfClone_PreservesFormPostAttackBudgetAndIndependentViewSnapshots(bool openingStep)
        {
            Game game = MakeGame();
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
            if (openingStep)
            {
                Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 3, 4))));
            }
            Board.Node attackPosition = Role(game).Unit.Position;
            Assert.True(game.ExecuteAction(HiddenWolfAttack.New(0, At(game, 2, openingStep ? 4 : 3))));
            HiddenWolf clone = Assert.IsType<HiddenWolf>(Role(game).Clone());
            HiddenWolfUnit unitClone = Assert.IsType<HiddenWolfUnit>(Role(game).Unit.Clone());
            HiddenWolfUnitView snapshot = View(game);
            HiddenWolfUnitView viewClone = Assert.IsType<HiddenWolfUnitView>(snapshot.Clone());
            Assert.Equal(HiddenWolfForm.Wolf, clone.Unit.Form);
            Assert.True(clone.HasStartedTurn);
            Assert.True(clone.Unit.HasTransformedThisTurn);
            Assert.False(viewClone.CanTransform);
            Assert.Equal(HiddenWolfForm.Wolf, viewClone.Form);
            Assert.All(clone.Unit.GetView(game).CanMoveGrids, entry => Assert.Equal(1, entry.distance));
            Assert.All(unitClone.GetView(game).CanMoveGrids, entry => Assert.Equal(1, entry.distance));
            viewClone.CanMoveGrids[0] = (At(game, 7, 7), 9, null);
            Assert.NotEqual(viewClone.CanMoveGrids[0], snapshot.CanMoveGrids[0]);
            Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 4, openingStep ? 4 : 3))));
            Assert.Equal(attackPosition, clone.Unit.Position);
            Assert.NotEmpty(clone.Unit.GetView(game).CanMoveGrids);
            HiddenWolf exhausted = Assert.IsType<HiddenWolf>(Role(game).Clone());
            Assert.Empty(exhausted.Unit.GetView(game).CanMoveGrids);
            Assert.False(exhausted.HasActionLeft(game));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void WolfMovement_RestoresFullPreAttackBudgetOnTheNextTurn(bool openingStep)
        {
            Game game = MakeGame();
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
            if (openingStep)
            {
                Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 3, 4))));
            }
            Assert.True(game.ExecuteAction(HiddenWolfAttack.New(0, Role(game).Unit.Position)));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 4, openingStep ? 4 : 3))));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(GeneralEnd.New(EnemyId(game))));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Equal(2, View(game).CanMoveGrids.Max(entry => entry.distance));
            Assert.NotEmpty(View(game).CanAttackGrids);
            Assert.True(View(game).CanTransform);
            Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 6, openingStep ? 4 : 3))));
            Assert.Equal(1, game.CurrentPlayerId);
        }

        // The chosen target remains private implementation state; only its distance is published.
        private static Unit? ChooseNearest(Game game, Board.Node origin)
            => (Unit?)typeof(HiddenWolfUnit).GetMethod("NearestEnemy", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(Role(game).Unit, new object[] { origin, game });

        [Fact]
        public static void InstinctRandom_SelectsBothTiedTargetsAndDrawsOnlyDuringExecution()
        {
            HashSet<int> selected = new();
            for (ulong seed = 1; seed <= 32; ++seed)
            {
                Game game = MakeGame(extraRoles: true);
                Board.Node origin = At(game, 3, 4);
                Place(game, game.GetUnit(1)!, origin); // An allied unit must not win at distance zero.
                Place(game, game.GetUnit(2)!, At(game, 2, 4));
                Place(game, game.GetUnit(3)!, At(game, 4, 4));
                Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
                GameRandom expected = GameRandom.New(seed)!.Value;
                int expectedId = 2 + expected.NextInt(0, 2)!.Value;
                game.Board.Random = GameRandom.New(seed)!.Value;
                Unit chosen = Assert.IsAssignableFrom<Unit>(ChooseNearest(game, origin));
                Assert.Equal(expectedId, chosen.Id);
                selected.Add(chosen.Id);
                GameRandom actual = game.Board.Random;
                Assert.Equal(expected.NextInt(0, int.MaxValue), actual.NextInt(0, int.MaxValue));

                // Re-run through the actual game entry with the same seed.
                game.Board.Random = GameRandom.New(seed)!.Value;
                expected = GameRandom.New(seed)!.Value;
                for (int query = 0; query < 3; ++query)
                {
                    _ = View(game);
                    Assert.True(Role(game).HasActionLeft(game));
                    Assert.True(Role(game).JudgeAction(HiddenWolfAttack.New(0, origin), game));
                }
                actual = game.Board.Random;
                GameRandom untouched = expected;
                Assert.Equal(untouched.NextInt(0, int.MaxValue), actual.NextInt(0, int.MaxValue));
                Assert.True(game.ExecuteAction(HiddenWolfAttack.New(0, origin)));
                _ = expected.NextInt(0, 2);
                actual = game.Board.Random;
                Assert.Equal(expected.NextInt(0, int.MaxValue), actual.NextInt(0, int.MaxValue));
                HiddenWolfInstinctEvent result = Assert.Single(game.Events.OfType<HiddenWolfInstinctEvent>());
                Assert.Equal(1, result.Distance);
                Assert.DoesNotContain(result, game.GetEventsForPlayer(1));
            }
            Assert.Equal(new[] { 2, 3 }, selected.OrderBy(id => id));
        }

        [Theory]
        [InlineData("undetectable")]
        [InlineData("dead")]
        [InlineData("farther")]
        public static void InstinctRandom_ExcludesInvalidAndFartherTargetsWithoutDrawingForOneCandidate(string excluded)
        {
            Game game = MakeGame(extraRoles: true, hiddenEnemy: excluded == "undetectable");
            Board.Node origin = At(game, 3, 4);
            Place(game, game.GetUnit(1)!, origin);
            Place(game, game.GetUnit(2)!, excluded == "farther" ? At(game, 3, 7) : origin);
            Place(game, game.GetUnit(3)!, At(game, 3, 5));
            if (excluded == "dead")
            {
                typeof(Unit).GetProperty(nameof(Unit.Health))!.SetValue(game.GetUnit(2)!, 0);
            }
            GameRandom expected = GameRandom.New(17)!.Value;
            game.Board.Random = expected;
            Assert.Same(game.GetUnit(3), ChooseNearest(game, origin));
            GameRandom actual = game.Board.Random;
            Assert.Equal(expected.NextInt(0, int.MaxValue), actual.NextInt(0, int.MaxValue));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void InstinctRandom_NoDrawForZeroOrOneDetectableEnemy(bool hiddenEnemy)
        {
            Game game = MakeGame(hiddenEnemy: hiddenEnemy);
            GameRandom expected = GameRandom.New(23)!.Value;
            game.Board.Random = expected;
            Unit? selected = ChooseNearest(game, At(game, 3, 4));
            Assert.Same(hiddenEnemy ? null : game.GetUnit(EnemyId(game)), selected);
            GameRandom actual = game.Board.Random;
            Assert.Equal(expected.NextInt(0, int.MaxValue), actual.NextInt(0, int.MaxValue));
        }

        [Theory]
        [InlineData("human")]
        [InlineData("wolf")]
        [InlineData("dead")]
        [InlineData("survivor")]
        public static void Instinct_IsPrivateAndMeasuresFromAttackGridAfterDamage(string enemyForm)
        {
            Game game = MakeGame(hiddenEnemy: enemyForm is "human" or "wolf");
            Unit enemy = game.GetUnit(EnemyId(game))!;
            if (enemyForm == "wolf")
            {
                // Prepare the opposing form before the current player's attack.
                HiddenWolf opponent = Assert.IsType<HiddenWolf>(game.Roles[1]);
                opponent.ExecuteAction(HiddenWolfTransform.New(enemy.Id), game);
            }
            Place(game, enemy, At(game, 3, 4));
            if (enemyForm == "dead")
            {
                // First reduce the enemy to half a health point without spending the game turn.
                HiddenWolfAttack opening = HiddenWolfAttack.New(0, enemy.Position);
                Role(game).ExecuteAction(opening, game);
                enemy.EndRound(game);
                Role(game).Unit.EndRound(game);
                game.Events.Clear();
            }
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
            Board.Node target = enemyForm is "dead" or "survivor" ? enemy.Position : At(game, 2, 3);
            Assert.True(game.ExecuteAction(HiddenWolfAttack.New(0, target)));
            HiddenWolfInstinctEvent result = Assert.Single(game.Events.OfType<HiddenWolfInstinctEvent>());
            Assert.Equal(enemyForm switch { "wolf" => 2, "survivor" => 0, _ => (int?)null }, result.Distance);
            Assert.Equal(0, result.PlayerId);
            Assert.Equal(0, result.UnitId);
            Assert.Contains(result, game.GetEventsForPlayer(0));
            Assert.DoesNotContain(result, game.GetEventsForPlayer(1));
            Assert.DoesNotContain(typeof(HiddenWolfInstinctEvent).GetProperties(), property =>
                property.Name is "Target" or "Position" or "Role");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void View_AndValidationAgreeThroughEveryWolfActionPhase(bool openingStep)
        {
            Game game = MakeGame();
            Assert.True(game.ExecuteAction(HiddenWolfTransform.New(0)));
            CheckTargets();
            if (openingStep)
            {
                Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 3, 4))));
                CheckTargets();
            }
            Assert.True(game.ExecuteAction(HiddenWolfAttack.New(0, Role(game).Unit.Position)));
            CheckTargets();
            Assert.True(game.ExecuteAction(GeneralMove.New(0, At(game, 4, openingStep ? 4 : 3))));
            CheckTargets();

            void CheckTargets()
            {
                HiddenWolfUnitView view = View(game);
                Assert.Equal(view.CanTransform, Role(game).JudgeAction(HiddenWolfTransform.New(0), game));
                for (int row = 1; row <= game.Board.Height; ++row)
                for (int col = 1; col <= game.Board.Width; ++col)
                {
                    Board.Node node = At(game, row, col);
                    Assert.Equal(view.CanMoveGrids.Any(entry => entry.node == node),
                        Role(game).JudgeAction(GeneralMove.New(0, node), game));
                    Assert.Equal(view.CanAttackGrids.Contains(node),
                        Role(game).JudgeAction(HiddenWolfAttack.New(0, node), game));
                }
            }
        }
    }
}
