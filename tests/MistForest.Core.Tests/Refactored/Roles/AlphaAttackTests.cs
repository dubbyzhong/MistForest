using MistForest.Core.Consts;
using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameMap.Board;
using static MistForest.Core.GameRefactored.PlayerBuilder;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class AlphaAttackTests
    {
        private static Game MakeGame(bool withAlly = false, bool extraAttacker = false)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            List<RoleBuilder> roles = new()
            {
                AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, board.At(3, 3)!.Value)!,
            };
            if (withAlly)
            {
                roles.Add(WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() },
                    board.At(3, 4)!.Value)!);
            }
            List<PlayerBuilder> players = new() { New(roles)! };
            if (extraAttacker)
            {
                players.Add(New(new List<RoleBuilder>
                {
                    AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() },
                        board.At(2, 4)!.Value)!,
                })!);
            }
            List<RoleBuilder> enemyRoles = new()
            {
                WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() },
                    withAlly ? board.At(7, 7)!.Value : board.At(3, 4)!.Value)!,
            };
            if (withAlly)
            {
                enemyRoles.Add(AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() },
                    board.At(7, 6)!.Value)!);
            }
            players.Add(New(enemyRoles)!);
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            return builder.Build()!;
        }

        private static Alpha GetAlpha(Game game) => Assert.IsType<Alpha>(game.Roles[0]);

        [Fact]
        public static void Attack_DamagesEnemyWithoutMovingAndEndsTurn()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Unit enemy = game.GetUnit(1)!;
            Node from = role.Unit.Position;

            Assert.True(game.ExecuteAction(AlphaAttack.New(role.Unit.Id, enemy.Position)));

            Assert.Equal(WolfConsts.WolfHealth - AlphaConsts.AlphaAttackDamage, enemy.Health);
            Assert.Equal(AlphaConsts.AlphaAttackDamage, enemy.HighestDirectDamage);
            Assert.Equal(from, role.Unit.Position);
            Assert.True(role.Unit.End);
            Assert.Equal(1, game.CurrentPlayerId);
            AlphaAttackEvent attackEvent = Assert.IsType<AlphaAttackEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { enemy.Id }, attackEvent.UnitIds);
            Assert.Equal(role.Unit.Id, attackEvent.AttackerId);
            Assert.Null(attackEvent.BuildingId);
            Assert.Equal(enemy.Position, attackEvent.Target);
            Assert.False(game.ExecuteAction(AlphaAttack.New(role.Unit.Id, enemy.Position)));
            Assert.False(role.JudgeAction(GeneralMove.New(role.Unit.Id, enemy.Position), game));
            Assert.Empty(((AlphaUnitView)role.Unit.GetView(game)).CanAttackGrids);
            Assert.Empty(role.Unit.GetView(game).CanMoveGrids);
            Assert.Single(game.Events);
        }

        [Fact]
        public static void Move_PreventsAttackButAllowsRemainingMovement()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Node middle = game.Board.At(2, 3)!.Value;
            Assert.True(game.ExecuteAction(GeneralMove.New(role.Unit.Id, middle)));

            Assert.False(game.ExecuteAction(AlphaAttack.New(role.Unit.Id, game.Board.At(2, 4)!.Value)));
            Assert.Empty(((AlphaUnitView)role.Unit.GetView(game)).CanAttackGrids);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Single(game.Events);
            Assert.True(game.ExecuteAction(GeneralMove.New(role.Unit.Id, game.Board.At(2, 4)!.Value)));
            Assert.Equal(1, game.CurrentPlayerId);
        }

        [Fact]
        public static void Attack_DoesNotRequireFirstActionWhenNoMovementOrAttackWasUsed()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Assert.True(game.ExecuteAction(AlphaBlood.New(role.Unit.Id)));

            Assert.False(role.IsFirstAction);
            Assert.True(game.ExecuteAction(AlphaAttack.New(role.Unit.Id, game.GetUnit(1)!.Position)));
            Assert.Equal(1, game.GetUnit(1)!.Health);
        }

        [Theory]
        [InlineData("self")]
        [InlineData("range")]
        [InlineData("diagonal")]
        [InlineData("lake")]
        [InlineData("terrain-wall")]
        [InlineData("line-wall")]
        public static void Attack_InvalidTarget_DoesNotConsumeAction(string obstruction)
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Node target = game.Board.At(3, 4)!.Value;
            switch (obstruction)
            {
                case "self": target = role.Unit.Position; break;
                case "range": target = game.Board.At(3, 5)!.Value; break;
                case "diagonal": target = game.Board.At(2, 4)!.Value; break;
                case "lake": game.Board.GetGrid(target)!.Terrain = Terrain.Lake; break;
                case "terrain-wall": game.Board.GetGrid(target)!.Terrain = Terrain.Wall; break;
                case "line-wall":
                    int wall = game.Board.VWallAt(role.Unit.Position)!.Value;
                    Assert.True(game.Board.VerticalWalls.SetInner(wall / 64, 1ul << (wall % 64), out _));
                    break;
            }

            Assert.False(game.ExecuteAction(AlphaAttack.New(role.Unit.Id, target)));
            Assert.DoesNotContain(target, ((AlphaUnitView)role.Unit.GetView(game)).CanAttackGrids);
            Assert.Empty(game.Events);
            Assert.True(role.IsFirstAction);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Equal(WolfConsts.WolfHealth, game.GetUnit(1)!.Health);
        }

        [Fact]
        public static void Attack_EmptyGrid_RecordsTargetWithEmptyVictims()
        {
            Game game = MakeGame();
            Node target = game.Board.At(2, 3)!.Value;

            Assert.True(game.ExecuteAction(AlphaAttack.New(0, target)));

            AlphaAttackEvent attackEvent = Assert.IsType<AlphaAttackEvent>(Assert.Single(game.Events));
            Assert.Empty(attackEvent.UnitIds);
            Assert.Null(attackEvent.BuildingId);
            Assert.Equal(0, attackEvent.AttackerId);
            Assert.Equal(target, attackEvent.Target);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Equal(WolfConsts.WolfHealth, game.GetUnit(1)!.Health);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Attack_DoesNotDamageAlliedUnitOrBuilding(bool withBuilding)
        {
            Game game = MakeGame(withAlly: true);
            Unit ally = game.GetUnit(1)!;
            if (withBuilding)
            {
                game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(3)!.SetPos(ally.Position)!);
            }

            Assert.True(game.ExecuteAction(AlphaAttack.New(0, ally.Position)));

            Assert.Equal(WolfConsts.WolfHealth, ally.Health);
            Assert.Equal(0, ally.HighestDirectDamage);
            AlphaAttackEvent attackEvent = Assert.IsType<AlphaAttackEvent>(Assert.Single(game.Events));
            Assert.Empty(attackEvent.UnitIds);
            Assert.Null(attackEvent.BuildingId);
            Assert.Equal(ally.Position, attackEvent.Target);
            if (withBuilding)
            {
                Assert.Equal(3, game.Buildings[0].Health);
                Assert.Equal(0, game.Buildings[0].HighestDirectDamage);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public static void Attack_EnemyBuilding_AbsorbsDamageWithoutOverflow(int health)
        {
            Game game = MakeGame();
            Unit enemy = game.GetUnit(1)!;
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(health)!.SetPos(enemy.Position)!);
            Assert.Contains(enemy.Position, ((AlphaUnitView)GetAlpha(game).Unit.GetView(game)).CanAttackGrids);

            Assert.True(game.ExecuteAction(AlphaAttack.New(0, enemy.Position)));

            Assert.Equal(WolfConsts.WolfHealth, enemy.Health);
            Assert.Equal(0, enemy.HighestDirectDamage);
            Assert.Equal(0, game.Board.GetGrid(enemy.Position)!.GridBuilding);
            AlphaAttackEvent attackEvent = Assert.IsType<AlphaAttackEvent>(Assert.Single(game.Events));
            Assert.Empty(attackEvent.UnitIds);
            Assert.Equal(0, attackEvent.BuildingId);
            Assert.Equal(0, attackEvent.AttackerId);
            Assert.Equal(enemy.Position, attackEvent.Target);
            if (health <= AlphaConsts.AlphaAttackDamage)
            {
                Assert.IsType<Building.Rubble>(game.Buildings[0]);
            }
            else
            {
                Assert.Equal(health - AlphaConsts.AlphaAttackDamage, game.Buildings[0].Health);
            }
        }

        [Fact]
        public static void Attack_MultipleEnemiesInOneGrid_AggregatesVictimsIntoOneEvent()
        {
            Game game = MakeGame(extraAttacker: true);
            Unit firstEnemy = game.GetUnit(1)!;
            Unit secondEnemy = game.GetUnit(2)!;
            game.Board.GetGrid(firstEnemy.Position)!.Units.Remove(firstEnemy.Id);
            firstEnemy.Position = secondEnemy.Position;
            game.Board.GetGrid(secondEnemy.Position)!.Units.Add(firstEnemy.Id);

            Assert.True(game.ExecuteAction(AlphaAttack.New(0, secondEnemy.Position)));

            Assert.Equal(1, firstEnemy.Health);
            Assert.Equal(1, secondEnemy.Health);
            AlphaAttackEvent attackEvent = Assert.IsType<AlphaAttackEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { 1, 2 }, attackEvent.UnitIds.OrderBy(id => id));
            Assert.Equal(0, attackEvent.AttackerId);
            Assert.Null(attackEvent.BuildingId);
            Assert.Equal(secondEnemy.Position, attackEvent.Target);
        }

        [Fact]
        public static void Attack_Rubble_DoesNotShieldEnemy()
        {
            Game game = MakeGame();
            Unit enemy = game.GetUnit(1)!;
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(1)!.SetPos(enemy.Position)!);
            game.Buildings[0].Destroyed(game, 1);

            Assert.True(game.ExecuteAction(AlphaAttack.New(0, enemy.Position)));

            Assert.Equal(1, enemy.Health);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Attack_DirectDamageDoesNotStackWithinRoundAndResetsNextRound(bool withBuilding)
        {
            Game game = MakeGame(extraAttacker: true);
            Unit enemy = game.GetUnit(2)!;
            Node target = enemy.Position;
            if (withBuilding)
            {
                game.AddBuilding(FortBuilder.New().SetOwner(2)!.SetHealth(5)!.SetPos(target)!);
            }

            Assert.True(game.ExecuteAction(AlphaAttack.New(0, target)));
            Assert.True(game.ExecuteAction(AlphaAttack.New(1, target)));
            Assert.Equal(withBuilding ? 5 - AlphaConsts.AlphaAttackDamage : 1,
                withBuilding ? game.Buildings[0].Health : enemy.Health);
            Assert.True(game.ExecuteAction(GeneralEnd.New(enemy.Id)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Equal(0, withBuilding ? game.Buildings[0].HighestDirectDamage : enemy.HighestDirectDamage);

            Assert.True(game.ExecuteAction(AlphaAttack.New(0, target)));
            if (withBuilding)
            {
                Assert.Equal(1, game.Buildings[0].Health);
            }
            else
            {
                Assert.False(enemy.Alive);
                Assert.Empty(game.Board.GetGrid(target)!.Units);
            }
        }

        [Fact]
        public static void AttackRush_AttacksBeforeMovingAndUpdatesBothGridsAndEvents()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Unit enemy = game.GetUnit(1)!;
            Node from = role.Unit.Position;
            Node target = enemy.Position;

            Assert.True(game.ExecuteAction(AlphaAttackRush.New(role.Unit.Id, target)));

            Assert.Equal(target, role.Unit.Position);
            Assert.Empty(game.Board.GetGrid(from)!.Units);
            Assert.Equal(new[] { 0, 1 }, game.Board.GetGrid(target)!.Units.OrderBy(id => id));
            Assert.Equal(AlphaConsts.AlphaHealth, role.Unit.Health);
            Assert.Equal(1, enemy.Health);
            Assert.True(role.Unit.End);
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.Collection(game.Events,
                first =>
                {
                    AlphaAttackEvent attack = Assert.IsType<AlphaAttackEvent>(first);
                    Assert.Equal(role.Unit.Id, attack.AttackerId);
                    Assert.Equal(new[] { enemy.Id }, attack.UnitIds);
                    Assert.Null(attack.BuildingId);
                    Assert.Equal(target, attack.Target);
                },
                second =>
                {
                    GeneralMoveEvent move = Assert.IsType<GeneralMoveEvent>(second);
                    Assert.Equal(role.Unit.Id, move.UnitId);
                    Assert.Equal(new[] { from, target }, move.Path);
                });
            Assert.False(role.JudgeAction(AlphaAttack.New(role.Unit.Id, target), game));
            Assert.False(role.JudgeAction(AlphaAttackRush.New(role.Unit.Id, from), game));
            Assert.False(role.JudgeAction(GeneralMove.New(role.Unit.Id, from), game));
            Assert.Empty(((AlphaUnitView)role.Unit.GetView(game)).CanAttackRushGrids);
        }

        [Fact]
        public static void AttackRush_DoesNotRequireFirstActionButRejectsPriorMovement()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Assert.True(game.ExecuteAction(AlphaBlood.New(role.Unit.Id)));
            Assert.True(game.ExecuteAction(AlphaAttackRush.New(role.Unit.Id, game.GetUnit(1)!.Position)));

            game = MakeGame();
            role = GetAlpha(game);
            Node middle = game.Board.At(2, 3)!.Value;
            Node target = game.Board.At(2, 4)!.Value;
            Assert.True(game.ExecuteAction(GeneralMove.New(role.Unit.Id, middle)));
            Assert.False(game.ExecuteAction(AlphaAttackRush.New(role.Unit.Id, target)));
            Assert.Equal(middle, role.Unit.Position);
            Assert.Single(game.Events);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Empty(((AlphaUnitView)role.Unit.GetView(game)).CanAttackRushGrids);
        }

        [Theory]
        [InlineData("self")]
        [InlineData("range")]
        [InlineData("diagonal")]
        [InlineData("lake")]
        [InlineData("terrain-wall")]
        [InlineData("vertical-wall")]
        [InlineData("horizontal-wall")]
        [InlineData("enemy-fort")]
        [InlineData("allied-wall")]
        [InlineData("enemy-wall")]
        public static void AttackRush_BlockedTarget_IsRejectedWithoutMovingOrConsumingAction(string obstruction)
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Node from = role.Unit.Position;
            Node target = game.GetUnit(1)!.Position;
            switch (obstruction)
            {
                case "self": target = from; break;
                case "range": target = game.Board.At(3, 5)!.Value; break;
                case "diagonal": target = game.Board.At(2, 4)!.Value; break;
                case "lake": game.Board.GetGrid(target)!.Terrain = Terrain.Lake; break;
                case "terrain-wall": game.Board.GetGrid(target)!.Terrain = Terrain.Wall; break;
                case "vertical-wall":
                    int vertical = game.Board.VWallAt(from)!.Value;
                    Assert.True(game.Board.VerticalWalls.SetInner(vertical / 64, 1ul << (vertical % 64), out _));
                    break;
                case "horizontal-wall":
                    target = game.Board.At(4, 3)!.Value;
                    int horizontal = game.Board.HWallAt(from)!.Value;
                    Assert.True(game.Board.HorizontalWalls.SetInner(horizontal / 64, 1ul << (horizontal % 64), out _));
                    break;
                case "enemy-fort":
                    game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(3)!.SetPos(target)!);
                    break;
                case "allied-wall":
                case "enemy-wall":
                    game.AddBuilding(WallBuilder.New().SetOwner(obstruction == "allied-wall" ? 0 : 1)!
                        .SetHealth(obstruction == "allied-wall" ? 1 : 3)!.SetPos(target)!);
                    break;
            }

            AlphaUnitView view = (AlphaUnitView)role.Unit.GetView(game);
            Assert.DoesNotContain(target, view.CanAttackRushGrids);
            if (obstruction is "enemy-fort" or "allied-wall" or "enemy-wall")
            {
                Assert.Contains(target, view.CanAttackGrids);
            }
            Assert.False(game.ExecuteAction(AlphaAttackRush.New(role.Unit.Id, target)));
            Assert.Equal(from, role.Unit.Position);
            Assert.Contains(role.Unit.Id, game.Board.GetGrid(from)!.Units);
            Assert.Empty(game.Events);
            Assert.True(role.IsFirstAction);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Equal(WolfConsts.WolfHealth, game.GetUnit(1)!.Health);
            if (game.Buildings.Count > 0)
            {
                Assert.Equal(obstruction == "allied-wall" ? 1 : 3, game.Buildings[0].Health);
            }
        }

        [Theory]
        [InlineData("ground")]
        [InlineData("whirlpool")]
        [InlineData("rubble")]
        public static void AttackRush_EnterableTarget_IsIncludedInViewAndCanBeEntered(string terrain)
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Node from = role.Unit.Position;
            Node target = game.Board.At(2, 3)!.Value;
            if (terrain == "whirlpool")
            {
                game.Board.GetGrid(target)!.Terrain = Terrain.Whirlpool;
            }
            if (terrain == "rubble")
            {
                game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(1)!.SetPos(target)!);
                game.Buildings[0].Destroyed(game, 0);
            }

            Assert.Contains(target, ((AlphaUnitView)role.Unit.GetView(game)).CanAttackRushGrids);
            Assert.True(game.ExecuteAction(AlphaAttackRush.New(role.Unit.Id, target)));
            Assert.Equal(target, role.Unit.Position);
            Assert.Empty(game.Board.GetGrid(from)!.Units);
            Assert.Equal(new[] { role.Unit.Id }, game.Board.GetGrid(target)!.Units);
            AlphaAttackEvent attack = Assert.IsType<AlphaAttackEvent>(game.Events[0]);
            Assert.Empty(attack.UnitIds);
            Assert.Null(attack.BuildingId);
            Assert.Equal(target, attack.Target);
            Assert.Equal(2, game.Events.Count);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(AlphaConsts.AlphaAttackDamage + 1)]
        public static void AttackRush_AlliedFort_IsRejectedWithoutSideEffects(int health)
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Node from = role.Unit.Position;
            Node target = game.GetUnit(1)!.Position;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(health)!.SetPos(target)!);

            AlphaUnitView view = (AlphaUnitView)role.Unit.GetView(game);
            Assert.Contains(target, view.CanAttackGrids);
            Assert.DoesNotContain(target, view.CanAttackRushGrids);
            Assert.True(game.Board.GetGrid(target)!.CanStay(0, game));

            AlphaAttackRush rush = AlphaAttackRush.New(role.Unit.Id, target);
            Assert.False(role.JudgeAction(rush, game));
            Assert.False(game.ExecuteAction(rush));
            Assert.Equal(from, role.Unit.Position);
            Assert.Equal(new[] { role.Unit.Id }, game.Board.GetGrid(from)!.Units);
            Assert.Equal(new[] { 1 }, game.Board.GetGrid(target)!.Units);
            Assert.Empty(game.Events);
            Assert.True(role.Unit.IsFirstAction);
            Assert.False(role.Unit.End);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Equal(WolfConsts.WolfHealth, game.GetUnit(1)!.Health);
            Assert.Equal(0, game.GetUnit(1)!.HighestDirectDamage);
            Assert.Equal(health, game.Buildings[0].Health);
            Assert.Equal(0, game.Buildings[0].HighestDirectDamage);
            Assert.True(role.JudgeAction(AlphaAttack.New(role.Unit.Id, target), game));
            Assert.True(role.JudgeAction(GeneralMove.New(role.Unit.Id, target), game));
        }

        [Fact]
        public static void AttackRush_BuildingIsDestroyedWhileAttackerIsStillAtOriginalPosition()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Node from = role.Unit.Position;
            Node target = game.GetUnit(1)!.Position;
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(1)!.SetPos(target)!);
            RushOrderBuilding building = new(target);
            game.Buildings[0] = building;

            Assert.True(game.ExecuteAction(AlphaAttackRush.New(role.Unit.Id, target)));

            Assert.Equal(from, building.AttackerPositionWhenDestroyed);
            Assert.Equal(target, role.Unit.Position);
            Assert.IsType<Building.Rubble>(game.Buildings[0]);
        }

        private sealed class RushOrderBuilding : Building
        {
            public Node? AttackerPositionWhenDestroyed { get; private set; }

            public RushOrderBuilding(Node position)
            {
                Id = 0;
                Owner = 1;
                Health = 1;
                Pos = position;
            }

            public override void Destroyed(Game game, int destroyer)
            {
                AttackerPositionWhenDestroyed = game.GetUnit(0)!.Position;
                base.Destroyed(game, destroyer);
            }
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(false, 2)]
        [InlineData(true, 1)]
        [InlineData(true, 2)]
        public static void AttackRush_DestroyableEnemyBuilding_IsClearedBeforeEntry(bool wall, int health)
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Node from = role.Unit.Position;
            Unit enemy = game.GetUnit(1)!;
            Node target = enemy.Position;
            BuildingBuilder building = wall ? WallBuilder.New() : FortBuilder.New();
            game.AddBuilding(building.SetOwner(1)!.SetHealth(health)!.SetPos(target)!);

            Assert.False(game.Board.GetGrid(target)!.CanStay(0, game));
            Assert.Contains(target, ((AlphaUnitView)role.Unit.GetView(game)).CanAttackRushGrids);
            Assert.True(game.ExecuteAction(AlphaAttackRush.New(role.Unit.Id, target)));

            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.Equal(target, role.Unit.Position);
            Assert.Empty(game.Board.GetGrid(from)!.Units);
            Assert.Contains(role.Unit.Id, game.Board.GetGrid(target)!.Units);
            Assert.Equal(WolfConsts.WolfHealth, enemy.Health);
            AlphaAttackEvent attack = Assert.IsType<AlphaAttackEvent>(game.Events[0]);
            Assert.Equal(0, attack.BuildingId);
            Assert.Empty(attack.UnitIds);
            Assert.IsType<GeneralMoveEvent>(game.Events[1]);
            Assert.Equal(2, game.Events.Count);
        }

        [Fact]
        public static void AttackRush_PredictionAccountsForAlreadyRecordedDirectDamage()
        {
            Game game = MakeGame(extraAttacker: true);
            Node target = game.GetUnit(2)!.Position;
            game.AddBuilding(FortBuilder.New().SetOwner(2)!.SetHealth(3)!.SetPos(target)!);
            Assert.True(game.ExecuteAction(AlphaAttack.New(0, target)));
            Assert.Equal(1, game.Buildings[0].Health);

            AlphaUnitView view = (AlphaUnitView)game.GetUnit(1)!.GetView(game);
            Assert.Contains(target, view.CanAttackGrids);
            Assert.DoesNotContain(target, view.CanAttackRushGrids);
            Assert.False(game.ExecuteAction(AlphaAttackRush.New(1, target)));
            Assert.Single(game.Events);
            Assert.Equal(1, game.Buildings[0].Health);
        }

        [Fact]
        public static void AttackRush_StaleView_DoesNotBypassValidationAtGameEntry()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Node from = role.Unit.Position;
            Node target = game.GetUnit(1)!.Position;
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(1)!.SetPos(target)!);
            AlphaAttackRush rush = AlphaAttackRush.New(role.Unit.Id, target);
            AlphaUnitView oldView = (AlphaUnitView)role.Unit.GetView(game);
            Assert.Contains(target, oldView.CanAttackRushGrids);

            // The board changes before the action is submitted, after the view was generated.
            game.Buildings[0].Health = 4;
            Assert.DoesNotContain(target, ((AlphaUnitView)role.Unit.GetView(game)).CanAttackRushGrids);
            Assert.False(game.ExecuteAction(rush));

            Assert.Equal(4, game.Buildings[0].Health);
            Assert.Equal(0, game.Buildings[0].HighestDirectDamage);
            Assert.Equal(from, role.Unit.Position);
            Assert.Contains(role.Unit.Id, game.Board.GetGrid(from)!.Units);
            Assert.DoesNotContain(role.Unit.Id, game.Board.GetGrid(target)!.Units);
            Assert.Empty(game.Events);
            Assert.True(role.Unit.IsFirstAction);
            Assert.False(role.Unit.End);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(game.ExecuteAction(AlphaAttack.New(role.Unit.Id, target)));
            Assert.Equal(2, game.Buildings[0].Health);
            Assert.Equal(WolfConsts.WolfHealth, game.GetUnit(1)!.Health);
            Assert.Equal(from, role.Unit.Position);
            AlphaAttackEvent attack = Assert.IsType<AlphaAttackEvent>(Assert.Single(game.Events));
            Assert.Equal(0, attack.BuildingId);
            Assert.Empty(attack.UnitIds);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void AttackView_DeadOrEndedUnit_HasNoAvailableActions(bool dead)
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            MistForest.Core.GameRefactored.Action action = dead
                ? GeneralSurrender.New(role.Unit.Id)
                : GeneralEnd.New(role.Unit.Id);
            Assert.True(game.ExecuteAction(action));

            AlphaUnitView view = (AlphaUnitView)role.Unit.GetView(game);

            Assert.Equal(!dead, role.Unit.Alive);
            Assert.Empty(view.CanAttackGrids);
            Assert.Empty(view.CanAttackRushGrids);
            Assert.Empty(view.CanMoveGrids);
            Assert.Empty(view.CanBuildFortGrids);
            Assert.Empty(view.CanBuildWallGrids);
            Assert.Empty(view.CanDemolishGrids);
            Assert.False(view.CanBlood);
            Assert.False(role.HasActionLeft(game));
        }

        [Fact]
        public static void AttackRush_KillingTarget_RemovesVictimButKeepsAttackerInTargetGrid()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            Unit enemy = game.GetUnit(1)!;
            Node target = enemy.Position;
            Assert.True(game.ExecuteAction(AlphaAttack.New(role.Unit.Id, target)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(enemy.Id)));

            Assert.True(game.ExecuteAction(AlphaAttackRush.New(role.Unit.Id, target)));

            Assert.False(enemy.Alive);
            Assert.Equal(new[] { role.Unit.Id }, game.Board.GetGrid(target)!.Units);
            Assert.Equal(target, role.Unit.Position);
            AlphaAttackEvent attack = game.Events.OfType<AlphaAttackEvent>().Last();
            Assert.Equal(new[] { enemy.Id }, attack.UnitIds);
        }

        [Fact]
        public static void AttackView_CloneOwnsAttackTargetsAndUnitClonePreservesAttackExhaustion()
        {
            Game game = MakeGame();
            Alpha role = GetAlpha(game);
            AlphaUnitView view = (AlphaUnitView)role.Unit.GetView(game);
            AlphaUnitView clone = (AlphaUnitView)view.Clone();
            Node original = view.CanAttackGrids[0];
            Node originalRush = view.CanAttackRushGrids[0];
            clone.CanAttackGrids[0] = role.Unit.Position;
            clone.CanAttackRushGrids[0] = role.Unit.Position;
            Assert.Equal(original, view.CanAttackGrids[0]);
            Assert.Equal(originalRush, view.CanAttackRushGrids[0]);

            Assert.True(game.ExecuteAction(AlphaAttack.New(role.Unit.Id, game.GetUnit(1)!.Position)));
            Alpha roleClone = (Alpha)role.Clone();
            Assert.Empty(((AlphaUnitView)roleClone.Unit.GetView(game)).CanAttackGrids);
            Assert.Empty(((AlphaUnitView)roleClone.Unit.GetView(game)).CanAttackRushGrids);
            Assert.False(roleClone.HasActionLeft(game));
        }
    }
}
