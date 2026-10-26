using MistForest.Core.GameMap;
using MistForest.Core.GameRefactored;
using MistForest.Core.GameRefactored.Roles;
using MistForest.Core.Utils;
using Xunit;
using static MistForest.Core.GameRefactored.PlayerBuilder;
using GameAction = MistForest.Core.GameRefactored.Action;

namespace MistForest.Core.Tests.Refactored.Roles
{
    public class UnitArmorTests
    {
        private static Game MakeGame(string attacker = "ink", int armor = 0, int health = 3,
            bool withSecondAttacker = false)
        {
            GameMode mode = GameMode.SelfDefine.New(7, 7);
            Board board = Board.FromMode(mode, out _)!;
            Board.Node start = board.At(3, 3)!.Value;
            RoleBuilder attackerBuilder = attacker switch
            {
                "ink" => InkBuilder.New(new UnitBuilder[] { new InkUnitBuilder() }, start)!,
                "wolf" => WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, start)!,
                "alpha" => AlphaBuilder.New(new UnitBuilder[] { new AlphaUnitBuilder() }, start)!,
                "sniper" => SniperBuilder.New(new UnitBuilder[] { new SniperUnitBuilder() }, start)!,
                _ => throw new ArgumentException("Unsupported attacker", nameof(attacker)),
            };
            List<PlayerBuilder> players = new() { New(new List<RoleBuilder> { attackerBuilder })! };
            if (withSecondAttacker)
            {
                players.Add(New(new List<RoleBuilder>
                {
                    WolfBuilder.New(new UnitBuilder[] { new WolfUnitBuilder() }, board.At(3, 5)!.Value)!,
                })!);
            }
            players.Add(New(new List<RoleBuilder>
            {
                new ArmoredRoleBuilder(new ArmoredUnitBuilder(board.At(3, 4)!.Value, health, armor)),
            })!);
            GameBuilder builder = new();
            Assert.NotNull(builder.SetMode(Box.New(mode)));
            Assert.NotNull(builder.SetBoard(Box.New(board)));
            Assert.NotNull(builder.SetPlayers(Box.New(players)));
            return builder.Build()!;
        }

        private static ArmoredUnit Target(Game game)
            => Assert.IsType<ArmoredUnit>(game.GetUnit(game.Players.Length - 1));

        private static InkUnit InkUnit(Game game) => Assert.IsType<Ink>(game.Roles[0]).Unit;

        private static GameAction Attack(string attacker, int callerId, Board.Node target)
        {
            return attacker switch
            {
                "ink" => InkAttack.New(callerId, target),
                "wolf" => WolfBite.New(callerId, target),
                "alpha" => AlphaAttack.New(callerId, target),
                "sniper" => SniperExecute.New(callerId, target),
                _ => throw new ArgumentException("Unsupported attacker", nameof(attacker)),
            };
        }

        [Theory]
        [InlineData("ink", 0, 0, 2)]
        [InlineData("ink", 1, 0, 3)]
        [InlineData("ink", 2, 1, 3)]
        [InlineData("ink", 4, 3, 3)]
        [InlineData("wolf", 0, 0, 1)]
        [InlineData("wolf", 1, 0, 2)]
        [InlineData("wolf", 2, 0, 3)]
        [InlineData("wolf", 6, 4, 3)]
        [InlineData("alpha", 1, 0, 2)]
        public static void DirectAttack_AbsorbsArmorBeforeDamagingHealth(
            string attacker, int armor, int expectedArmor, int expectedHealth)
        {
            Game game = MakeGame(attacker, armor);
            ArmoredUnit target = Target(game);

            Assert.True(game.ExecuteAction(Attack(attacker, 0, target.Position)));

            Assert.Equal(expectedArmor, target.Armor);
            Assert.Equal(expectedHealth, target.Health);
            Assert.Equal(attacker == "ink" ? 1 : 2, target.HighestDirectDamage);
            AttackEvent attacked = Assert.IsAssignableFrom<AttackEvent>(Assert.Single(game.Events));
            Assert.Equal(new[] { target.Id }, attacked.UnitIds);
            Assert.Null(attacked.BuildingId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public static void LethalDamage_ClampsHealthAndRemovesUnitFromGrid(int armor)
        {
            Game game = MakeGame("wolf", armor, health: 1);
            ArmoredUnit target = Target(game);
            Board.Node position = target.Position;

            Assert.True(game.ExecuteAction(WolfBite.New(0, position)));

            Assert.False(target.Alive);
            Assert.Equal(0, target.Health);
            Assert.Equal(0, target.Armor);
            Assert.DoesNotContain(target.Id, game.Board.GetGrid(position)!.Units);
        }

        [Theory]
        [InlineData("ink", 3)]
        [InlineData("wolf", 2)]
        public static void EqualStrengthAttacks_DoNotSpendArmorAgain(string attacker, int expectedArmor)
        {
            Game game = MakeGame(attacker, armor: 4);
            ArmoredUnit target = Target(game);
            for (int shot = 0; shot < 3; ++shot)
            {
                Assert.True(game.ExecuteAction(Attack(attacker, 0, target.Position)));
                Assert.Equal(expectedArmor, target.Armor);
                Assert.Equal(3, target.Health);
            }
        }

        [Theory]
        [InlineData(0, 0, 1)]
        [InlineData(1, 0, 2)]
        [InlineData(3, 1, 3)]
        public static void StrongerAttackFromAnotherPlayer_OnlyDealsTheUncoveredDifference(
            int armor, int expectedArmor, int expectedHealth)
        {
            Game game = MakeGame(armor: armor, withSecondAttacker: true);
            ArmoredUnit target = Target(game);
            Assert.True(game.ExecuteAction(InkAttack.New(0, target.Position)));
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));
            Assert.Equal(1, game.CurrentPlayerId);

            Assert.True(game.ExecuteAction(WolfBite.New(1, target.Position)));

            Assert.Equal(expectedArmor, target.Armor);
            Assert.Equal(expectedHealth, target.Health);
            Assert.Equal(2, target.HighestDirectDamage);
            Assert.True(game.ExecuteAction(WolfBite.New(1, target.Position)));
            Assert.Equal(expectedArmor, target.Armor);
            Assert.Equal(expectedHealth, target.Health);
        }

        [Fact]
        public static void EndRound_PreservesArmorAndResetsHighestDamage()
        {
            Game game = MakeGame(armor: 4);
            ArmoredUnit target = Target(game);
            Assert.True(game.ExecuteAction(InkAttack.New(0, target.Position)));
            Assert.Equal(3, target.Armor);
            Assert.Equal(1, target.HighestDirectDamage);
            Assert.True(game.ExecuteAction(GeneralEnd.New(0)));

            Assert.True(game.ExecuteAction(GeneralEnd.New(target.Id)));

            Assert.Equal(3, target.Armor);
            Assert.Equal(3, target.Health);
            Assert.Equal(0, target.HighestDirectDamage);
            Assert.True(game.ExecuteAction(InkAttack.New(0, target.Position)));
            Assert.Equal(2, target.Armor);
            Assert.Equal(3, target.Health);
        }

        [Fact]
        public static void Clone_PreservesArmorAndDamageHistoryWithoutSharingState()
        {
            Game game = MakeGame("wolf", armor: 3);
            ArmoredUnit original = Target(game);
            Assert.True(game.ExecuteAction(WolfBite.New(0, original.Position)));

            ArmoredUnit unitClone = Assert.IsType<ArmoredUnit>(original.Clone());
            ArmoredRole roleClone = Assert.IsType<ArmoredRole>(game.Roles[1].Clone());
            ArmoredUnit roleUnitClone = Assert.IsType<ArmoredUnit>(Assert.Single(roleClone.GetUnits().ToArray()));
            original.RestoreArmor(4);
            foreach (ArmoredUnit clone in new[] { unitClone, roleUnitClone })
            {
                Assert.NotSame(original, clone);
                Assert.Equal(1, clone.Armor);
                Assert.Equal(3, clone.Health);
                Assert.Equal(2, clone.HighestDirectDamage);
                clone.EndRound(game);
                Assert.Equal(1, clone.Armor);
                Assert.Equal(0, clone.HighestDirectDamage);
            }
            Assert.Equal(4, original.Armor);
            Assert.Equal(2, original.HighestDirectDamage);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public static void Ink_ArmorOnlyDamageRefreshesDashButCoveredDamageDoesNot(int armor)
        {
            Game game = MakeGame(armor: armor);
            ArmoredUnit target = Target(game);
            Board.Node start = InkUnit(game).Position;
            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(3, 5)!.Value)));
            Assert.Equal(2, InkUnit(game).DashCooldown);

            Assert.True(game.ExecuteAction(InkAttack.New(0, target.Position)));

            Assert.Equal(3, target.Health);
            Assert.Equal(armor - 1, target.Armor);
            Assert.Equal(0, InkUnit(game).DashCooldown);
            Assert.True(game.ExecuteAction(InkDash.New(0, start)));
            Assert.True(game.ExecuteAction(InkAttack.New(0, target.Position)));
            Assert.Equal(3, target.Health);
            Assert.Equal(armor - 1, target.Armor);
            Assert.Equal(2, InkUnit(game).DashCooldown);
        }

        [Fact]
        public static void RestoringArmor_DoesNotClearHighestDamageOrRefreshInkOnCoveredHit()
        {
            Game game = MakeGame();
            ArmoredUnit target = Target(game);
            Assert.True(game.ExecuteAction(InkAttack.New(0, target.Position)));
            target.RestoreArmor(2);
            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(3, 5)!.Value)));

            Assert.True(game.ExecuteAction(InkAttack.New(0, target.Position)));

            Assert.Equal(2, target.Armor);
            Assert.Equal(2, target.Health);
            Assert.Equal(1, target.HighestDirectDamage);
            Assert.Equal(2, InkUnit(game).DashCooldown);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public static void Building_ShieldsArmorAndDoesNotRefreshInkUntilNextPellet(int buildingOwner)
        {
            Game game = MakeGame(armor: 4);
            ArmoredUnit target = Target(game);
            game.AddBuilding(FortBuilder.New().SetOwner(buildingOwner)!.SetHealth(1)!.SetPos(target.Position)!);
            Assert.True(game.ExecuteAction(InkDash.New(0, game.Board.At(2, 4)!.Value)));

            Assert.True(game.ExecuteAction(InkAttack.New(0, target.Position)));

            Assert.IsType<Building.Rubble>(game.Buildings[0]);
            Assert.Equal(4, target.Armor);
            Assert.Equal(3, target.Health);
            Assert.Equal(0, target.HighestDirectDamage);
            Assert.Equal(2, InkUnit(game).DashCooldown);
            InkAttackEvent buildingHit = Assert.Single(game.Events.OfType<InkAttackEvent>());
            Assert.Empty(buildingHit.UnitIds);
            Assert.Equal(0, buildingHit.BuildingId);

            Assert.True(game.ExecuteAction(InkAttack.New(0, target.Position)));
            Assert.Equal(3, target.Armor);
            Assert.Equal(3, target.Health);
            Assert.Equal(0, InkUnit(game).DashCooldown);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(6)]
        public static void SniperExecution_BypassesAndClearsArmor(int armor)
        {
            Game game = MakeGame("sniper", armor);
            ArmoredUnit target = Target(game);
            Board.Node position = target.Position;

            Assert.True(game.ExecuteAction(SniperExecute.New(0, position)));

            Assert.False(target.Alive);
            Assert.Equal(0, target.Health);
            Assert.Equal(0, target.Armor);
            Assert.DoesNotContain(target.Id, game.Board.GetGrid(position)!.Units);
            Assert.Equal(new[] { target.Id }, Assert.IsType<ExecuteEvent>(Assert.Single(game.Events)).UnitIds);
        }

        private sealed class ArmoredUnit : Unit
        {
            public ArmoredUnit(int id, int roleId, Board.Node position, int health, int armor)
            {
                NewBase(this, id, roleId, position);
                Health = health;
                Armor = armor;
            }

            public void RestoreArmor(int armor) => Armor = armor;

            public void EndAction(GeneralEnd action, Game game)
            {
                IsFirstAction = false;
                GeneralEnd(action, game);
            }

            public override Unit Clone()
            {
                ArmoredUnit target = new(Id, RoleId, Position, 0, 0);
                CloneBase(target);
                return target;
            }

            public override bool HasAction(GameAction action) => action is GeneralEnd;
            public override UnitView GetView(Game game) => throw new NotSupportedException();
        }

        private sealed class ArmoredUnitBuilder : UnitBuilder
        {
            private readonly int _health;
            private readonly int _armor;

            public ArmoredUnitBuilder(Board.Node position, int health, int armor)
            {
                SetPosition(position);
                _health = health;
                _armor = armor;
            }

            public override Unit? Build()
                => Built ??= new ArmoredUnit(Id!.Value, RoleId!.Value, Position!.Value, _health, _armor);
        }

        private sealed class ArmoredRole : Role
        {
            private readonly Unit[] _units;

            public ArmoredRole(int id, int playerId, Unit unit)
            {
                NewBase(this, id, playerId);
                _units = new[] { unit };
            }

            public override Role Clone() => new ArmoredRole(Id, PlayerId, _units[0].Clone());
            public override ReadOnlySpan<Unit> GetUnits() => _units;
            public override bool HasActionLeft(Game game) => false;
            public override bool JudgeAction(GameAction action, Game game)
                => _units[0].Alive && !_units[0].End && action is GeneralEnd;
            public override void ExecuteAction(GameAction action, Game game)
                => ((ArmoredUnit)_units[0]).EndAction((GeneralEnd)action, game);
        }

        private sealed class ArmoredRoleBuilder : RoleBuilder
        {
            public ArmoredRoleBuilder(ArmoredUnitBuilder unit) : base(new UnitBuilder[] { unit }) { }
            public override bool CanInitialStay(Board board)
                => board.GetGrid(UnitBuilder[0].Position!.Value)!.CanInitialStay();
            public override Role? Build()
                => Built ??= new ArmoredRole(Id!.Value, PlayerId!.Value, UnitBuilder[0].Build()!);
        }
    }
}
