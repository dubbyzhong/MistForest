using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;
using GameAction = MistForest.Core.GameRefactored.Action;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class ArmigerArmorTests
    {
        private static Game MakeGame(int firstPlayerId = 0, bool withAlly = false)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            List<RoleBuilder> own = new()
            {
                ArmigerBuilder.New(new UnitBuilder[] { new ArmigerUnitBuilder() }, board.At(3, 3)!.Value)!,
            };
            List<RoleBuilder> enemies = new()
            {
                InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, board.At(3, 4)!.Value)!,
            };
            if (withAlly)
            {
                own.Add(WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(1, 1)!.Value)!);
                enemies.Add(WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(7, 7)!.Value)!);
            }
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(new List<PlayerBuilder> { New(own)!, New(enemies)! })));
            Assert.NotNull(builder.SetFirstPlayerId(firstPlayerId));
            return builder.Build()!;
        }

        private static Armiger Role(Game game) => Assert.IsType<Armiger>(game.Roles[0]);
        private static ArmigerUnit Unit(Game game) => Role(game).Unit;
        private static ArmigerUnitView View(Game game) => Assert.IsType<ArmigerUnitView>(Unit(game).GetView(game));
        private static int EnemyId(Game game) => game.Players[1].Roles[0].GetUnits()[0].Id;

        private static void PrepareArmor(Game game, int armor)
        {
            if (game.CurrentPlayerId != 0)
            {
                Assert.True(game.ExecuteAction(GeneralEnd.New(EnemyId(game))));
            }
            for (int layer = 0; layer < (armor + 1) / 2; ++layer)
            {
                Assert.True(game.ExecuteAction(ArmigerArmorUp.New(0)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(EnemyId(game))));
            }
            if (armor % 2 != 0)
            {
                Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
                Assert.True(game.ExecuteAction(InkAttack.New(EnemyId(game), Unit(game).Position)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(EnemyId(game))));
            }
            Assert.Equal(armor, Unit(game).Armor);
            Assert.Equal(0, game.CurrentPlayerId);
        }

        [Theory]
        [InlineData(0, 2, 1)]
        [InlineData(1, 1, 2)]
        [InlineData(2, 1, 2)]
        [InlineData(3, 0, 3)]
        [InlineData(4, 0, 3)]
        public static void Armor_UsesHalfPointUnitsAndRoundsPartialLayersForRanges(
            int armor, int movementRange, int attackRange)
        {
            Game game = MakeGame();
            PrepareArmor(game, armor);

            ArmigerUnitView view = View(game);

            Assert.Equal(armor, view.Armor);
            Assert.Equal(attackRange, Unit(game).AttackRange);
            if (movementRange == 0)
            {
                Assert.Empty(view.CanMoveGrids);
                Assert.False(game.ExecuteAction(GeneralMove.New(0, game.Board.At(3, 2)!.Value)));
            }
            else
            {
                Assert.Equal(movementRange, view.CanMoveGrids.Max(entry => entry.distance));
                Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(3, 3 - movementRange)!.Value)));
            }
        }

        [Fact]
        public static void ArmorUp_AddsOneLayerPerTurnAndRejectsTheMaximum()
        {
            Game game = MakeGame();
            ArmigerUnitView initial = View(game);
            Assert.True(initial.CanArmorUp);
            Assert.Empty(initial.CanArmorDownAmounts);
            Assert.True(Unit(game).HasAction(ArmigerArmorUp.New(0)));
            Assert.True(Unit(game).HasAction(ArmigerArmorDown.New(0, 2)));
            for (int expected = 2; expected <= 4; expected += 2)
            {
                int count = game.Events.Count;
                Assert.True(game.ExecuteAction(ArmigerArmorUp.New(0)));

                Assert.Equal(expected, Unit(game).Armor);
                Assert.True(Unit(game).End);
                Assert.False(Role(game).HasActionLeft(game));
                Assert.False(View(game).CanArmorUp);
                Assert.Empty(View(game).CanArmorDownAmounts);
                Assert.Equal(1, game.CurrentPlayerId);
                Assert.Equal(count + 1, game.Events.Count);
                ArmigerArmorUpEvent changed = Assert.IsType<ArmigerArmorUpEvent>(game.Events.Last());
                Assert.Equal(0, changed.UnitId);
                Assert.Equal(expected - 2, changed.ArmorBefore);
                Assert.Equal(expected, changed.ArmorAfter);
                Assert.True(game.ExecuteAction(GeneralEnd.New(EnemyId(game))));
            }
            Assert.Equal(0, initial.Armor);
            Assert.True(initial.CanArmorUp);
            Assert.False(View(game).CanArmorUp);
            Assert.Equal(new[] { 2, 4 }, View(game).CanArmorDownAmounts);
            int before = game.Events.Count;
            Assert.False(game.ExecuteAction(ArmigerArmorUp.New(0)));
            Assert.Equal(4, Unit(game).Armor);
            Assert.Equal(before, game.Events.Count);
            Assert.True(Unit(game).IsFirstAction);
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(3, 4)]
        public static void ArmorUp_RestoresPartialArmorToTheNextWholeLayer(int armor, int expected)
        {
            Game game = MakeGame();
            PrepareArmor(game, armor);
            Assert.Equal(2, Unit(game).ArmorLockRounds);
            for (int turn = 0; turn < 2; ++turn)
            {
                Assert.False(game.ExecuteAction(ArmigerArmorUp.New(0)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(EnemyId(game))));
            }

            Assert.True(game.ExecuteAction(ArmigerArmorUp.New(0)));

            Assert.Equal(expected, Unit(game).Armor);
            ArmigerArmorUpEvent changed = Assert.IsType<ArmigerArmorUpEvent>(game.Events.Last());
            Assert.Equal(armor, changed.ArmorBefore);
            Assert.Equal(expected, changed.ArmorAfter);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void ArmorUp_IsRejectedAfterMovementOrUnloading(bool unloadFirst)
        {
            Game game = MakeGame();
            if (unloadFirst)
            {
                PrepareArmor(game, 2);
                Assert.True(game.ExecuteAction(ArmigerArmorDown.New(0, 2)));
            }
            else
            {
                Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(3, 2)!.Value)));
            }
            int count = game.Events.Count;

            Assert.False(View(game).CanArmorUp);
            Assert.False(game.ExecuteAction(ArmigerArmorUp.New(0)));
            Assert.Equal(count, game.Events.Count);
            Assert.Equal(0, Unit(game).Armor);
            Assert.NotEmpty(View(game).CanMoveGrids);
        }

        [Theory]
        [InlineData(0, 0, false)]
        [InlineData(0, 2, false)]
        [InlineData(1, 0, false)]
        [InlineData(1, 1, true)]
        [InlineData(1, 2, false)]
        [InlineData(2, 1, false)]
        [InlineData(2, 2, true)]
        [InlineData(3, 1, true)]
        [InlineData(3, 2, false)]
        [InlineData(3, 3, true)]
        [InlineData(4, 1, false)]
        [InlineData(4, 2, true)]
        [InlineData(4, 3, false)]
        [InlineData(4, 4, true)]
        [InlineData(4, -1, false)]
        [InlineData(4, int.MaxValue, false)]
        public static void ArmorDown_AcceptsInternalAmountsThatRemoveAnyPartialLayer(
            int armor, int amount, bool allowed)
        {
            Game game = MakeGame();
            PrepareArmor(game, armor);
            int eventsBefore = game.Events.Count;
            bool firstActionBefore = Unit(game).IsFirstAction;
            Assert.Equal(allowed, View(game).CanArmorDownAmounts.Contains(amount));

            Assert.Equal(allowed, game.ExecuteAction(ArmigerArmorDown.New(0, amount)));

            Assert.Equal(allowed ? armor - amount : armor, Unit(game).Armor);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Equal(eventsBefore + (allowed ? 1 : 0), game.Events.Count);
            if (allowed)
            {
                ArmigerArmorDownEvent changed = Assert.IsType<ArmigerArmorDownEvent>(game.Events.Last());
                Assert.Equal(0, changed.UnitId);
                Assert.Equal(armor, changed.ArmorBefore);
                Assert.Equal(armor - amount, changed.ArmorAfter);
                Assert.False(Unit(game).IsFirstAction);
                Assert.False(Unit(game).End);
            }
            else
            {
                Assert.Equal(firstActionBefore, Unit(game).IsFirstAction);
            }
        }

        [Fact]
        public static void UnloadingAfterMovement_OnlyAddsTheNewRemainingMovementAllowance()
        {
            Game game = MakeGame();
            PrepareArmor(game, 2);
            Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(3, 2)!.Value)));
            Assert.Empty(View(game).CanMoveGrids);
            Assert.Equal(new[] { 2 }, View(game).CanArmorDownAmounts);
            Assert.True(Role(game).HasActionLeft(game));

            Assert.True(game.ExecuteAction(ArmigerArmorDown.New(0, 2)));

            Assert.All(View(game).CanMoveGrids, entry => Assert.Equal(1, entry.distance));
            Assert.DoesNotContain(View(game).CanMoveGrids, entry => entry.node == game.Board.At(1, 2)!.Value);
            Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(3, 1)!.Value)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.NotEmpty(View(game).CanThrustGrids);
            Assert.True(game.ExecuteAction(ArmigerThrust.New(0, game.Board.At(3, 2)!.Value)));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Empty(View(game).CanMoveGrids);
            Assert.Empty(View(game).CanArmorDownAmounts);
            Assert.Equal(1, Unit(game).AttackRange);
        }

        [Fact]
        public static void Unloading_RemainsAvailableWhenOtherActionsAreBlocked()
        {
            Game game = MakeGame();
            PrepareArmor(game, 4);
            foreach ((int row, int col) in new[] { (2, 3), (4, 3), (3, 2), (3, 4) })
            {
                game.Board.SetTerrain(game.Board.At(row, col)!.Value, Terrain.Wall);
            }
            ArmigerUnitView view = View(game);
            Assert.Empty(view.CanMoveGrids);
            Assert.Empty(view.CanBuildFortGrids);
            Assert.Empty(view.CanBuildWallGrids);
            Assert.Empty(view.CanDemolishGrids);
            Assert.False(view.CanArmorUp);
            Assert.Equal(new[] { 2, 4 }, view.CanArmorDownAmounts);
            Assert.True(Role(game).HasActionLeft(game));

            Assert.True(game.ExecuteAction(ArmigerArmorDown.New(0, 2)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(ArmigerArmorDown.New(0, 2)));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.False(Role(game).HasActionLeft(game));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public static void ArmorLock_BlocksTwoOwnTurnsAndTicksWhenAnotherRoleActs(
            bool hitBeforeOwnTurn, bool useOtherRole)
        {
            Game game = MakeGame(firstPlayerId: hitBeforeOwnTurn ? 1 : 0, withAlly: useOtherRole);
            int ownActor = useOtherRole ? 1 : 0;
            int enemy = EnemyId(game);
            if (!hitBeforeOwnTurn)
            {
                Assert.True(game.ExecuteAction(GeneralEnd.New(ownActor)));
            }
            Assert.True(game.ExecuteAction(InkAttack.New(enemy, Unit(game).Position)));
            Assert.Equal(2, Unit(game).ArmorLockRounds);
            Assert.True(game.ExecuteAction(GeneralEnd.New(enemy)));
            for (int remaining = 2; remaining > 0; --remaining)
            {
                Assert.Equal(0, game.CurrentPlayerId);
                Assert.Equal(remaining, Unit(game).ArmorLockRounds);
                Assert.False(View(game).CanArmorUp);
                Assert.False(game.ExecuteAction(ArmigerArmorUp.New(0)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(ownActor)));
                Assert.Equal(hitBeforeOwnTurn ? remaining - 1 : remaining, Unit(game).ArmorLockRounds);
                Assert.True(game.ExecuteAction(GeneralEnd.New(enemy)));
                Assert.Equal(remaining - 1, Unit(game).ArmorLockRounds);
            }
            Assert.True(View(game).CanArmorUp);
            Assert.True(game.ExecuteAction(ArmigerArmorUp.New(0)));
            Assert.Equal(2, Unit(game).Armor);
        }

        [Fact]
        public static void ArmorOnlyDamage_StartsLockAndRefreshesInk()
        {
            Game game = MakeGame();
            PrepareArmor(game, 2);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            int enemy = EnemyId(game);
            Assert.True(game.ExecuteAction(InkDash.New(enemy, game.Board.At(2, 3)!.Value)));

            Assert.True(game.ExecuteAction(InkAttack.New(enemy, Unit(game).Position)));

            Assert.Equal(3, Unit(game).Health);
            Assert.Equal(1, Unit(game).Armor);
            Assert.Equal(2, Unit(game).ArmorLockRounds);
            Assert.Equal(0, Assert.IsType<Ink>(game.Players[1].Roles[0]).Unit.DashCooldown);
        }

        [Fact]
        public static void BuildingProtection_DoesNotStartLock()
        {
            Game game = MakeGame(firstPlayerId: 1);
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(1)!.SetPos(Unit(game).Position)!);

            Assert.True(game.ExecuteAction(InkAttack.New(EnemyId(game), Unit(game).Position)));

            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.Equal(3, Unit(game).Health);
            Assert.Equal(0, Unit(game).ArmorLockRounds);
            Assert.True(View(game).CanArmorUp);
            Assert.Equal(0, Unit(game).HighestDirectDamage);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void LaterHitAfterOwnTurn_OnlyRefreshesLockWhenItAddsDamage(bool stronger)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            RoleBuilder later = stronger
                ? WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(3, 2)!.Value)!
                : InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, board.At(3, 2)!.Value)!;
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(new List<PlayerBuilder>
            {
                New(new List<RoleBuilder>
                {
                    InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, board.At(3, 4)!.Value)!,
                })!,
                New(new List<RoleBuilder>
                {
                    ArmigerBuilder.New(new UnitBuilder[] { new ArmigerUnitBuilder() }, board.At(3, 3)!.Value)!,
                })!,
                New(new List<RoleBuilder> { later })!,
            })));
            Game game = builder.Build()!;
            ArmigerUnit unit = Assert.IsType<Armiger>(game.Roles[1]).Unit;
            Assert.True(game.ExecuteAction(InkAttack.New(0, unit.Position)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            GameAction hit = stronger ? WolfBite.New(2, unit.Position) : InkAttack.New(2, unit.Position);

            Assert.True(game.ExecuteAction(hit));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));

            Assert.Equal(stronger ? 1 : 2, unit.Health);
            Assert.Equal(stronger ? 2 : 1, unit.ArmorLockRounds);
        }

        [Fact]
        public static void DamageDuringOwnTurn_AndClonesSkipTheHitRound()
        {
            Game game = MakeGame();
            int enemyId = EnemyId(game);
            Ink enemy = Assert.IsType<Ink>(game.Players[1].Roles[0]);
            InkAttack hit = InkAttack.New(enemyId, Unit(game).Position);
            // Resolve damage during the owner's turn to model future effects such as ongoing damage.
            Assert.True(enemy.JudgeAction(hit, game));
            enemy.ExecuteAction(hit, game);
            Assert.Equal(2, Unit(game).ArmorLockRounds);
            Armiger clone = Assert.IsType<Armiger>(Role(game).Clone());
            clone.Unit.EndRound(game);
            Assert.Equal(2, clone.Unit.ArmorLockRounds);
            clone.Unit.EndRound(game);
            Assert.Equal(1, clone.Unit.ArmorLockRounds);
            Assert.Equal(2, Unit(game).ArmorLockRounds);

            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(enemyId)));

            Assert.Equal(2, Unit(game).ArmorLockRounds);
            Armiger afterRound = Assert.IsType<Armiger>(Role(game).Clone());
            afterRound.Unit.EndRound(game);
            Assert.Equal(1, afterRound.Unit.ArmorLockRounds);
            afterRound.Unit.EndRound(game);
            afterRound.Unit.EndRound(game);
            Assert.Equal(0, afterRound.Unit.ArmorLockRounds);
        }

        [Fact]
        public static void CloneAndView_PreservePartialArmorAndOwnUnloadOptions()
        {
            Game game = MakeGame();
            PrepareArmor(game, 3);
            Armiger clone = Assert.IsType<Armiger>(Role(game).Clone());
            ArmigerUnitView view = View(game);
            ArmigerUnitView viewClone = Assert.IsType<ArmigerUnitView>(view.Clone());
            Assert.Equal(3, clone.Unit.Armor);
            Assert.Equal(2, clone.Unit.ArmorLockRounds);
            Assert.Equal(3, viewClone.Armor);
            Assert.Equal(2, viewClone.ArmorLockRounds);
            Assert.False(viewClone.CanArmorUp);
            viewClone.CanArmorDownAmounts[0] = 99;
            Assert.Equal(new[] { 1, 3 }, view.CanArmorDownAmounts);
            Assert.Equal(new[] { 1, 3 }, ((ArmigerUnitView)clone.Unit.GetView(game)).CanArmorDownAmounts);

            Assert.True(game.ExecuteAction(ArmigerArmorDown.New(0, 1)));
            clone.Unit.EndRound(game);

            Assert.Equal(2, Unit(game).Armor);
            Assert.Equal(3, clone.Unit.Armor);
            Assert.Equal(1, clone.Unit.ArmorLockRounds);
            Assert.Equal(2, Unit(game).ArmorLockRounds);
            Assert.Equal(3, view.Armor);
            Assert.Equal(2, view.ArmorLockRounds);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void EndAndDeath_HideArmorSkills(bool surrender)
        {
            Game game = MakeGame();
            PrepareArmor(game, 2);
            GameAction action = surrender ? GeneralSurrender.New(0) : GeneralEnd.New(0);
            Assert.True(game.ExecuteAction(action));

            Assert.False(View(game).CanArmorUp);
            Assert.Empty(View(game).CanArmorDownAmounts);
            Assert.Equal(surrender ? 0 : 2, View(game).Armor);
            Assert.False(Role(game).JudgeAction(ArmigerArmorUp.New(0), game));
            Assert.False(Role(game).JudgeAction(ArmigerArmorDown.New(0, 2), game));
            Assert.False(game.ExecuteAction(ArmigerArmorUp.New(0)));
            Assert.False(game.ExecuteAction(ArmigerArmorDown.New(0, 2)));
        }
    }
}
