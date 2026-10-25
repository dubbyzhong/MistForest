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
    public class InkAttackTests
    {
        private static Game MakeGame(bool withAlly = false, bool withThirdPlayer = false,
            int startRow = 3, int startCol = 3, int firstPlayerId = 0)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            List<RoleBuilder> own = new()
            {
                InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, board.At(startRow, startCol)!.Value)!,
            };
            List<RoleBuilder> enemies = new()
            {
                WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() },
                    withAlly ? board.At(7, 7)!.Value : board.At(3, 4)!.Value)!,
            };
            if (withAlly)
            {
                own.Add(WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(3, 4)!.Value)!);
                enemies.Add(AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, board.At(7, 6)!.Value)!);
            }
            List<PlayerBuilder> players = new() { New(own)!, New(enemies)! };
            if (withThirdPlayer)
            {
                players.Add(New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(4, 4)!.Value)!,
                })!);
            }
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            Assert.NotNull(builder.SetFirstPlayerId(firstPlayerId));
            return builder.Build()!;
        }

        private static Ink GetInk(Game game) => Assert.IsType<Ink>(game.Roles[0]);
        private static InkUnitView View(Game game) => Assert.IsType<InkUnitView>(GetInk(game).Unit.GetView(game));

        private static void PlaceUnit(Game game, Unit unit, Node target)
        {
            game.Board.GetGrid(unit.Position)!.Units.Remove(unit.Id);
            unit.Position = target;
            game.Board.GetGrid(target)!.Units.Add(unit.Id);
        }

        [Fact]
        public static void Attack_DamagesEnemyAndRecordsOneEventWithoutEndingTurn()
        {
            Game game = MakeGame();
            Ink role = GetInk(game);
            Unit enemy = game.GetUnit(1)!;
            Node origin = role.Unit.Position;

            Assert.True(game.ExecuteAction(InkAttack.New(role.Unit.Id, enemy.Position)));

            Assert.Equal(WolfConsts.WolfHealth - InkConsts.InkAttackDamage, enemy.Health);
            Assert.Equal(InkConsts.InkAttackDamage, enemy.HighestDirectDamage);
            Assert.Equal(origin, role.Unit.Position);
            Assert.Equal(InkConsts.InkHealth, role.Unit.Health);
            Assert.Equal(3, role.Unit.MaxAttackAction);
            Assert.False(role.Unit.End);
            Assert.False(role.IsFirstAction);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Equal(1, View(game).AttackUsed);
            Assert.Equal(role.Unit.MaxAttackAction, View(game).MaxAttackAction);
            InkAttackEvent attack = Assert.IsType<InkAttackEvent>(Assert.Single(game.Events));
            Assert.Equal(role.Unit.Id, attack.AttackerId);
            Assert.Equal(new[] { enemy.Id }, attack.UnitIds);
            Assert.Null(attack.BuildingId);
            Assert.Equal(enemy.Position, attack.Target);
        }

        [Theory]
        [InlineData(3, 3, 9)]
        [InlineData(1, 1, 4)]
        public static void AttackRange_IncludesSelfAndEightNeighborsWithinBoard(int row, int col, int count)
        {
            Game game = MakeGame(startRow: row, startCol: col);
            Node[] targets = View(game).CanAttackGrids;

            Assert.Equal(count, targets.Length);
            Assert.Contains(GetInk(game).Unit.Position, targets);
            for (int dr = -1; dr <= 1; ++dr)
            {
                for (int dc = -1; dc <= 1; ++dc)
                {
                    if (game.Board.At(row + dr, col + dc) is Node target)
                    {
                        Assert.Contains(target, targets);
                    }
                }
            }
        }

        [Theory]
        [InlineData("range")]
        [InlineData("lake")]
        [InlineData("terrain-wall")]
        [InlineData("vertical-wall")]
        [InlineData("horizontal-wall")]
        [InlineData("diagonal-terrain")]
        [InlineData("diagonal-buildings")]
        [InlineData("diagonal-line-walls")]
        [InlineData("out-of-board")]
        public static void Attack_InvalidTarget_IsRejectedWithoutConsumingPellet(string obstruction)
        {
            Game game = MakeGame();
            Ink role = GetInk(game);
            Node origin = role.Unit.Position;
            Node target = game.Board.At(3, 4)!.Value;
            switch (obstruction)
            {
                case "range": target = game.Board.At(3, 5)!.Value; break;
                case "lake": game.Board.GetGrid(target)!.Terrain = Terrain.Lake; break;
                case "terrain-wall": game.Board.GetGrid(target)!.Terrain = Terrain.Wall; break;
                case "vertical-wall":
                    int vertical = game.Board.VWallAt(origin)!.Value;
                    Assert.True(game.Board.VerticalWalls.SetInner(vertical / 64, 1ul << (vertical % 64), out _));
                    break;
                case "horizontal-wall":
                    target = game.Board.At(4, 3)!.Value;
                    int horizontal = game.Board.HWallAt(origin)!.Value;
                    Assert.True(game.Board.HorizontalWalls.SetInner(horizontal / 64, 1ul << (horizontal % 64), out _));
                    break;
                case "diagonal-terrain":
                    target = game.Board.At(4, 4)!.Value;
                    game.Board.GetGrid(game.Board.At(3, 4)!.Value)!.Terrain = Terrain.Wall;
                    game.Board.GetGrid(game.Board.At(4, 3)!.Value)!.Terrain = Terrain.Wall;
                    break;
                case "diagonal-buildings":
                    target = game.Board.At(4, 4)!.Value;
                    game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(3)!.SetPos(game.Board.At(3, 4)!.Value)!);
                    game.AddBuilding(WallBuilder.New().SetOwner(1)!.SetHealth(3)!.SetPos(game.Board.At(4, 3)!.Value)!);
                    break;
                case "diagonal-line-walls":
                    target = game.Board.At(4, 4)!.Value;
                    int v = game.Board.VWallAt(origin)!.Value;
                    int h = game.Board.HWallAt(origin)!.Value;
                    Assert.True(game.Board.VerticalWalls.SetInner(v / 64, 1ul << (v % 64), out _));
                    Assert.True(game.Board.HorizontalWalls.SetInner(h / 64, 1ul << (h % 64), out _));
                    break;
                case "out-of-board":
                    target = Board.FromMode(GameMode.SelfDefine.New(8, 8), out _)!.At(8, 8)!.Value;
                    break;
            }

            Assert.False(game.ExecuteAction(InkAttack.New(role.Unit.Id, target)));

            Assert.DoesNotContain(target, View(game).CanAttackGrids);
            Assert.Equal(0, View(game).AttackUsed);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(role.IsFirstAction);
            Assert.False(role.Unit.End);
            Assert.Equal(origin, role.Unit.Position);
            Assert.Equal(WolfConsts.WolfHealth, game.GetUnit(1)!.Health);
            Assert.Empty(game.Events);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Attack_DiagonalWithOneOpenCorner_RemainsLegal(bool building)
        {
            Game game = MakeGame();
            Node corner = game.Board.At(3, 4)!.Value;
            Node target = game.Board.At(4, 4)!.Value;
            if (building)
            {
                game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(3)!.SetPos(corner)!);
            }
            else
            {
                game.Board.GetGrid(corner)!.Terrain = Terrain.Wall;
            }

            Assert.Contains(target, View(game).CanAttackGrids);
            Assert.True(game.ExecuteAction(InkAttack.New(0, target)));
            Assert.Equal(1, View(game).AttackUsed);
        }

        [Fact]
        public static void Attack_ThreePelletsDoNotStackAndFourthIsRejectedWhileMovementRemains()
        {
            Game game = MakeGame();
            Ink role = GetInk(game);
            Unit enemy = game.GetUnit(1)!;
            for (int shot = 0; shot < 3; ++shot)
            {
                Assert.True(game.ExecuteAction(InkAttack.New(0, enemy.Position)));
                Assert.Equal(2, enemy.Health);
                Assert.Equal(shot + 1, View(game).AttackUsed);
            }

            Assert.False(role.JudgeAction(InkAttack.New(0, enemy.Position), game));
            Assert.False(game.ExecuteAction(InkAttack.New(0, enemy.Position)));
            Assert.Equal(3, game.Events.Count);
            Assert.Empty(View(game).CanAttackGrids);
            Assert.NotEmpty(View(game).CanMoveGrids);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(role.HasActionLeft(game));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(2, 3)!.Value)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.True(role.HasActionLeft(game));
            Assert.NotEmpty(View(game).CanDashGrids);
            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(2, 1)!.Value)));
            Assert.Equal(1, game.CurrentPlayerId);
            Assert.False(role.HasActionLeft(game));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void AttackAndMove_HaveIndependentQuotasInEitherOrder(bool moveFirst)
        {
            Game game = MakeGame();
            Node moveTarget = game.Board.At(2, 3)!.Value;
            Node enemy = game.GetUnit(1)!.Position;
            if (moveFirst)
            {
                Assert.True(game.ExecuteAction(GeneralMove.New(0, moveTarget)));
                Assert.Equal(0, View(game).AttackUsed);
                Assert.Contains(enemy, View(game).CanAttackGrids);
            }
            Assert.True(game.ExecuteAction(InkAttack.New(0, enemy)));
            if (!moveFirst)
            {
                Assert.True(game.ExecuteAction(GeneralMove.New(0, moveTarget)));
            }
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Equal(1, View(game).AttackUsed);
            Assert.True(game.ExecuteAction(InkAttack.New(0, enemy)));
            Assert.True(game.ExecuteAction(InkAttack.New(0, enemy)));
            Assert.Equal(3, View(game).AttackUsed);
            Assert.Equal(moveTarget, GetInk(game).Unit.Position);
            Assert.Equal(2, game.GetUnit(1)!.Health);
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.NotEmpty(View(game).CanDashGrids);
            Assert.Equal(3, game.Events.OfType<InkAttackEvent>().Count());
            Assert.Single(game.Events.OfType<GeneralMoveEvent>());
        }

        [Fact]
        public static void AttackRange_IsRecomputedAfterMovement()
        {
            Game game = MakeGame();
            Node enemy = game.GetUnit(1)!.Position;
            Assert.Contains(enemy, View(game).CanAttackGrids);
            Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(3, 2)!.Value)));

            Assert.DoesNotContain(enemy, View(game).CanAttackGrids);
            Assert.False(game.ExecuteAction(InkAttack.New(0, enemy)));
            Assert.Equal(0, View(game).AttackUsed);
            Assert.Single(game.Events);
            Assert.True(game.ExecuteAction(InkAttack.New(0, game.Board.At(2, 1)!.Value)));
        }

        [Fact]
        public static void Attack_DifferentEnemiesEachTakeTheirOwnDamage()
        {
            Game game = MakeGame(withThirdPlayer: true);
            Assert.True(game.ExecuteAction(InkAttack.New(0, game.GetUnit(1)!.Position)));
            Assert.True(game.ExecuteAction(InkAttack.New(0, game.GetUnit(2)!.Position)));

            Assert.Equal(2, game.GetUnit(1)!.Health);
            Assert.Equal(2, game.GetUnit(2)!.Health);
            Assert.Equal(2, View(game).AttackUsed);
            Assert.Equal(2, game.Events.Count);
        }

        [Fact]
        public static void Attack_SelfGrid_DamagesEnemiesAndProtectsAlliesAndCaster()
        {
            Game game = MakeGame(withAlly: true);
            Node target = GetInk(game).Unit.Position;
            Unit ally = game.GetUnit(1)!;
            Unit enemy = game.GetUnit(2)!;
            PlaceUnit(game, ally, target);
            PlaceUnit(game, enemy, target);

            Assert.True(game.ExecuteAction(InkAttack.New(0, target)));

            Assert.Equal(InkConsts.InkHealth, GetInk(game).Unit.Health);
            Assert.Equal(0, GetInk(game).Unit.HighestDirectDamage);
            Assert.Equal(WolfConsts.WolfHealth, ally.Health);
            Assert.Equal(0, ally.HighestDirectDamage);
            Assert.Equal(2, enemy.Health);
            InkAttackEvent attack = Assert.IsType<InkAttackEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { enemy.Id }, attack.UnitIds);
            Assert.Equal(target, attack.Target);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void Attack_EmptyOrFriendlyGrid_ConsumesPelletAndRecordsEmptyVictims(bool friendly)
        {
            Game game = MakeGame(withAlly: friendly);
            Node target = friendly ? game.GetUnit(1)!.Position : game.Board.At(2, 3)!.Value;

            Assert.True(game.ExecuteAction(InkAttack.New(0, target)));

            InkAttackEvent attack = Assert.IsType<InkAttackEvent>(Assert.Single(game.Events));
            Assert.Empty(attack.UnitIds);
            Assert.Null(attack.BuildingId);
            Assert.Equal(target, attack.Target);
            Assert.Equal(1, View(game).AttackUsed);
            Assert.Equal(WolfConsts.WolfHealth, game.GetUnit(1)!.Health);
        }

        [Theory]
        [InlineData(false, 1, 0)]
        [InlineData(false, 5, 0)]
        [InlineData(true, 1, 0)]
        [InlineData(true, 5, 0)]
        [InlineData(false, 1, 1)]
        [InlineData(false, 5, 1)]
        [InlineData(true, 1, 1)]
        [InlineData(true, 5, 1)]
        public static void Attack_BuildingOfEitherOwner_TakesDoubleDamageAndShieldsUnits(bool wall, int health, int owner)
        {
            Game game = MakeGame();
            Unit enemy = game.GetUnit(1)!;
            BuildingBuilder builder = wall ? WallBuilder.New() : FortBuilder.New();
            game.AddBuilding(builder.SetOwner(owner)!.SetHealth(health)!.SetPos(enemy.Position)!);

            Assert.True(game.ExecuteAction(InkAttack.New(0, enemy.Position)));

            Assert.Equal(WolfConsts.WolfHealth, enemy.Health);
            Assert.Equal(0, enemy.HighestDirectDamage);
            if (health <= InkConsts.InkBuildingAttackDamage)
            {
                Assert.IsType<Building.Rubble>(game.Buildings[0]);
            }
            else
            {
                Assert.Equal(health - InkConsts.InkBuildingAttackDamage, game.Buildings[0].Health);
                Assert.Equal(InkConsts.InkBuildingAttackDamage, game.Buildings[0].HighestDirectDamage);
            }
            InkAttackEvent attack = Assert.IsType<InkAttackEvent>(Assert.Single(game.Events));
            Assert.Equal(0, attack.BuildingId);
            Assert.Empty(attack.UnitIds);
            Assert.Equal(enemy.Position, attack.Target);
            Assert.Equal(1, View(game).AttackUsed);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public static void Attack_AfterBuildingIsDestroyed_NextPelletCanDamagePreviouslyProtectedUnit(int owner)
        {
            Game game = MakeGame();
            Unit enemy = game.GetUnit(1)!;
            Node target = enemy.Position;
            game.AddBuilding(FortBuilder.New().SetOwner(owner)!.SetHealth(1)!.SetPos(target)!);

            Assert.True(game.ExecuteAction(InkAttack.New(0, target)));
            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.Equal(WolfConsts.WolfHealth, enemy.Health);
            Assert.Equal(0, enemy.HighestDirectDamage);
            Assert.True(game.ExecuteAction(InkAttack.New(0, target)));
            Assert.Equal(2, enemy.Health);
            Assert.True(game.ExecuteAction(InkAttack.New(0, target)));
            Assert.Equal(2, enemy.Health);

            InkAttackEvent[] attacks = game.Events.OfType<InkAttackEvent>().ToArray();
            Assert.Equal(3, attacks.Length);
            Assert.Equal(0, attacks[0].BuildingId);
            Assert.Empty(attacks[0].UnitIds);
            Assert.Null(attacks[1].BuildingId);
            Assert.Equal(new[] { enemy.Id }, attacks[1].UnitIds);
            Assert.Equal(3, View(game).AttackUsed);
        }

        [Fact]
        public static void Attack_AlliedBuildingCanBeDestroyedWithoutHarmingItsAlliedOccupant()
        {
            Game game = MakeGame(withAlly: true);
            Unit ally = game.GetUnit(1)!;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(ally.Position)!);

            Assert.True(game.ExecuteAction(InkAttack.New(0, ally.Position)));

            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.Equal(WolfConsts.WolfHealth, ally.Health);
            Assert.Equal(0, ally.HighestDirectDamage);
            InkAttackEvent attack = Assert.IsType<InkAttackEvent>(Assert.Single(game.Events));
            Assert.Equal(0, attack.BuildingId);
            Assert.Empty(attack.UnitIds);
        }

        [Fact]
        public static void Attack_Rubble_DoesNotShieldEnemy()
        {
            Game game = MakeGame();
            Unit enemy = game.GetUnit(1)!;
            game.AddBuilding(FortBuilder.New().SetOwner(1)!.SetHealth(1)!.SetPos(enemy.Position)!);
            game.Buildings[0].Destroyed(game, 1);

            Assert.True(game.ExecuteAction(InkAttack.New(0, enemy.Position)));

            Assert.Equal(2, enemy.Health);
            InkAttackEvent attack = Assert.IsType<InkAttackEvent>(Assert.Single(game.Events));
            Assert.Null(attack.BuildingId);
            Assert.Equal(new[] { enemy.Id }, attack.UnitIds);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public static void Attack_BuildingDamageDoesNotStackAndResetsWithAttackQuotaNextRound(int owner)
        {
            Game game = MakeGame();
            Node target = game.GetUnit(1)!.Position;
            game.AddBuilding(FortBuilder.New().SetOwner(owner)!.SetHealth(5)!.SetPos(target)!);
            for (int shot = 0; shot < 3; ++shot)
            {
                Assert.True(game.ExecuteAction(InkAttack.New(0, target)));
                Assert.Equal(3, game.Buildings[0].Health);
            }
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
            Assert.Equal(0, game.Buildings[0].HighestDirectDamage);
            Assert.Equal(0, View(game).AttackUsed);
            Assert.NotEmpty(View(game).CanAttackGrids);

            Assert.True(game.ExecuteAction(InkAttack.New(0, target)));

            Assert.Equal(1, game.Buildings[0].Health);
            Assert.Equal(WolfConsts.WolfHealth, game.GetUnit(1)!.Health);
        }

        [Fact]
        public static void Attack_AfterStrongerDirectAttack_ConsumesPelletWithoutAddingDamage()
        {
            Game game = MakeGame(withThirdPlayer: true, firstPlayerId: 2);
            Unit enemy = game.GetUnit(1)!;
            Assert.True(game.ExecuteAction(WolfBite.New(2, enemy.Position)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            Assert.Equal(0, game.CurrentPlayerId);
            Assert.Equal(1, enemy.Health);

            Assert.True(game.ExecuteAction(InkAttack.New(0, enemy.Position)));

            Assert.Equal(1, enemy.Health);
            Assert.Equal(WolfConsts.WolfBiteDamage, enemy.HighestDirectDamage);
            Assert.Equal(1, View(game).AttackUsed);
        }

        [Fact]
        public static void Attack_MultipleVictimsCanDieWithoutInvalidatingGridIteration()
        {
            Game game = MakeGame(withThirdPlayer: true);
            Unit first = game.GetUnit(1)!;
            Unit second = game.GetUnit(2)!;
            Node target = first.Position;
            PlaceUnit(game, second, target);
            for (int round = 0; round < 2; ++round)
            {
                Assert.True(game.ExecuteAction(InkAttack.New(0, target)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(1)));
                Assert.True(game.ExecuteAction(GeneralEnd.New(2)));
            }

            Assert.True(game.ExecuteAction(InkAttack.New(0, target)));

            Assert.False(first.Alive);
            Assert.False(second.Alive);
            Assert.Empty(game.Board.GetGrid(target)!.Units);
            InkAttackEvent attack = Assert.IsType<InkAttackEvent>(game.Events.Last());
            Assert.Equal(new[] { first.Id, second.Id }, attack.UnitIds);
            Assert.Equal(3, game.Events.OfType<InkAttackEvent>().Count());
        }

        [Fact]
        public static void Attack_PreventsFirstActionBuildingAndDemolition()
        {
            Game game = MakeGame();
            Node allied = game.Board.At(2, 3)!.Value;
            Node empty = game.Board.At(3, 2)!.Value;
            game.AddBuilding(FortBuilder.New().SetOwner(0)!.SetHealth(2)!.SetPos(allied)!);
            Assert.True(game.ExecuteAction(InkAttack.New(0, game.GetUnit(1)!.Position)));

            Assert.False(game.ExecuteAction(GeneralBuildFort.New(0, empty)));
            Assert.False(game.ExecuteAction(GeneralBuildWall.New(0, new[] { empty })!));
            Assert.False(game.ExecuteAction(GeneralDemolish.New(0, allied)));
            Assert.Empty(View(game).CanBuildFortGrids);
            Assert.Empty(View(game).CanBuildWallGrids);
            Assert.Empty(View(game).CanDemolishGrids);
            Assert.Equal(1, View(game).AttackUsed);
            Assert.Single(game.Events);
        }

        [Fact]
        public static void Attack_ClonesPreserveQuotaAndOwnTheirTargetArrays()
        {
            Game game = MakeGame();
            Ink role = GetInk(game);
            Node target = game.GetUnit(1)!.Position;
            Assert.True(game.ExecuteAction(InkAttack.New(0, target)));
            Assert.True(game.ExecuteAction(GeneralMove.New(0, game.Board.At(2, 3)!.Value)));
            Ink clone = (Ink)role.Clone();
            InkUnitView view = View(game);
            InkUnitView viewClone = (InkUnitView)view.Clone();
            Node original = view.CanAttackGrids[0];
            viewClone.CanAttackGrids[0] = game.Board.At(7, 1)!.Value;

            Assert.Equal(original, view.CanAttackGrids[0]);
            Assert.NotSame(view.CanAttackGrids, viewClone.CanAttackGrids);
            Assert.Equal(3, viewClone.MaxAttackAction);
            Assert.Equal(1, viewClone.AttackUsed);
            Assert.True(game.ExecuteAction(InkAttack.New(0, target)));
            Assert.True(game.ExecuteAction(InkAttack.New(0, target)));
            Assert.Empty(View(game).CanAttackGrids);
            Assert.Equal(3, View(game).AttackUsed);
            Assert.Equal(1, view.AttackUsed);
            Assert.Equal(1, ((InkUnitView)clone.Unit.GetView(game)).AttackUsed);
            Assert.True(clone.JudgeAction(InkAttack.New(0, target), game));
            Assert.False(role.JudgeAction(InkAttack.New(0, target), game));
        }

        [Fact]
        public static void AttackEvent_FactoryCopiesVictimIds()
        {
            Node target = Board.New(3, 3).At(2, 2)!.Value;
            int[] victims = { 1, 2 };
            InkAttackEvent attack = InkAttackEvent.New(0, victims, 3, target);
            victims[0] = 99;

            Assert.Equal(0, attack.AttackerId);
            Assert.Equal(new[] { 1, 2 }, attack.UnitIds);
            Assert.Equal(3, attack.BuildingId);
            Assert.Equal(target, attack.Target);
        }
    }
}
