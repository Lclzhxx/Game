// =============================================================
// 文件：PlayerCombatStateMachineTests.cs（E3-S1 验收 · EditMode 无头）
// 作用：对纯逻辑 PlayerCombatStateMachine 做确定性单测（零 UnityEngine 依赖）。
//       覆盖：Idle<->Move、Dodge（触发/无敌帧/冷却/结束回流）、Attack（触发/标志/冷却/不中断闪避）。
//       所有时序通过注入 dt 驱动，无 Time.deltaTime，无协程 -> CI / Unity Test Runner 可跑。
// 注：本环境无 Unity Editor，无法本地执行；请在 Unity Test Runner 或 CI 跑。
// =============================================================

using NUnit.Framework;

namespace MJ.Editor.Tests
{
    [TestFixture]
    public class PlayerCombatStateMachineTests
    {
        private static CombatStats DefaultStats()
        {
            return new CombatStats
            {
                moveSpeed = 5f,
                dodgeSpeed = 14f,
                dodgeDuration = 0.25f,
                dodgeCooldown = 0.8f,
                dodgeInvincibleTime = 0.3f,
                attackRange = 2.5f,
                attackDamage = 34f,
                attackCooldown = 0.4f
            };
        }

        private static InputSnapshot Idle()
        {
            return new InputSnapshot { moveX = 0f, moveZ = 0f, facingX = 0f, facingZ = 1f };
        }

        private static InputSnapshot Move(float x, float z)
        {
            return new InputSnapshot { moveX = x, moveZ = z, facingX = 0f, facingZ = 1f };
        }

        private static InputSnapshot DodgePressed()
        {
            return new InputSnapshot { moveX = 0f, moveZ = 0f, facingX = 0f, facingZ = 1f, dodgePressed = true };
        }

        private static InputSnapshot AttackPressed()
        {
            return new InputSnapshot { moveX = 0f, moveZ = 0f, facingX = 0f, facingZ = 1f, attackPressed = true };
        }

        // ---------- Idle <-> Move ----------
        [Test]
        public void Idle_WithMoveInput_TransitionsToMove()
        {
            var fsm = new PlayerCombatStateMachine(DefaultStats());
            fsm.Tick(0.016f, Move(1f, 0f));
            Assert.AreEqual(CombatState.Move, fsm.State);
        }

        [Test]
        public void Move_WithoutMoveInput_TransitionsToIdle()
        {
            var fsm = new PlayerCombatStateMachine(DefaultStats());
            fsm.Tick(0.016f, Move(1f, 0f));
            Assert.AreEqual(CombatState.Move, fsm.State);
            fsm.Tick(0.016f, Idle());
            Assert.AreEqual(CombatState.Idle, fsm.State);
        }

        // ---------- Dodge ----------
        [Test]
        public void Dodge_TriggersOnlyWhenCooldownReadyAndPressed()
        {
            var fsm = new PlayerCombatStateMachine(DefaultStats());
            // 初始冷却为 0 -> 应触发
            fsm.Tick(0.016f, DodgePressed());
            Assert.AreEqual(CombatState.Dodge, fsm.State);
            Assert.IsTrue(fsm.IsInvincible);
        }

        [Test]
        public void Dodge_InvincibleFramesActiveDuringDodge()
        {
            var fsm = new PlayerCombatStateMachine(DefaultStats());
            fsm.Tick(0.016f, DodgePressed());
            // 在 dodgeDuration(0.25) 内持续 tick（期间即使再按空格也应被冷却/状态挡住）
            for (int i = 0; i < 4; i++) fsm.Tick(0.05f, DodgePressed());
            Assert.AreEqual(CombatState.Dodge, fsm.State);
            // 无敌帧应覆盖 dodge 时长并延续到 dodgeInvincibleTime(0.3)
            Assert.IsTrue(fsm.IsInvincible, "i-frames 应覆盖 dodge 时长并延续到无敌时长");
        }

        [Test]
        public void Dodge_ReturnsToIdleAfterDuration()
        {
            var fsm = new PlayerCombatStateMachine(DefaultStats());
            fsm.Tick(0.016f, DodgePressed());
            int guard = 0;
            while (fsm.State == CombatState.Dodge && guard++ < 100) fsm.Tick(0.05f, Idle());
            Assert.AreNotEqual(CombatState.Dodge, fsm.State);
            Assert.AreEqual(CombatState.Idle, fsm.State);
        }

        [Test]
        public void Dodge_ReturnsToMoveWhenMovingAtEnd()
        {
            var fsm = new PlayerCombatStateMachine(DefaultStats());
            fsm.Tick(0.016f, DodgePressed());
            int guard = 0;
            while (fsm.State == CombatState.Dodge && guard++ < 100) fsm.Tick(0.05f, Move(1f, 0f));
            Assert.AreEqual(CombatState.Move, fsm.State);
        }

        [Test]
        public void Dodge_CooldownBlocksImmediateReDodge()
        {
            var fsm = new PlayerCombatStateMachine(DefaultStats());
            fsm.Tick(0.016f, DodgePressed());                 // 进入闪避
            int guard = 0;
            while (fsm.State == CombatState.Dodge && guard++ < 100) fsm.Tick(0.05f, Idle()); // 越过 dodge 时长回到 Idle
            Assert.AreEqual(CombatState.Idle, fsm.State);
            // 立即再按空格：闪避冷却(0.8) 仍 > 0 -> 不应再次闪避
            fsm.Tick(0.016f, DodgePressed());
            Assert.AreNotEqual(CombatState.Dodge, fsm.State, "冷却未就绪不应再次闪避");
            Assert.AreEqual(CombatState.Idle, fsm.State);
        }

        // ---------- Attack ----------
        [Test]
        public void Attack_TriggersOnlyWhenCooldownReadyAndPressed()
        {
            var fsm = new PlayerCombatStateMachine(DefaultStats());
            fsm.Tick(0.016f, AttackPressed());
            Assert.AreEqual(CombatState.Attack, fsm.State);
            Assert.IsTrue(fsm.AttackTriggeredThisTick);
        }

        [Test]
        public void Attack_SetsTriggeredFlagForShellToConsume()
        {
            var fsm = new PlayerCombatStateMachine(DefaultStats());
            fsm.Tick(0.016f, AttackPressed());
            Assert.IsTrue(fsm.AttackTriggeredThisTick);
            // 下一帧标志复位
            fsm.Tick(0.016f, Idle());
            Assert.IsFalse(fsm.AttackTriggeredThisTick);
        }

        [Test]
        public void Attack_CooldownBlocksImmediateReAttack()
        {
            var fsm = new PlayerCombatStateMachine(DefaultStats());
            fsm.Tick(0.016f, AttackPressed());   // 触发
            fsm.Tick(0.016f, AttackPressed());   // 冷却未就绪，立即再按
            Assert.IsFalse(fsm.AttackTriggeredThisTick, "攻击冷却未就绪不应再次触发");
            Assert.AreNotEqual(CombatState.Attack, fsm.State);
        }

        [Test]
        public void Attack_DoesNotTriggerDuringDodge()
        {
            var fsm = new PlayerCombatStateMachine(DefaultStats());
            fsm.Tick(0.016f, DodgePressed());     // 进入闪避
            fsm.Tick(0.016f, AttackPressed());    // 闪避中按攻击
            Assert.AreEqual(CombatState.Dodge, fsm.State);
            Assert.IsFalse(fsm.AttackTriggeredThisTick, "闪避中不触发普攻");
        }

        [Test]
        public void Attack_AfterCooldownCanTriggerAgain()
        {
            var fsm = new PlayerCombatStateMachine(DefaultStats());
            fsm.Tick(0.016f, AttackPressed());    // 触发
            fsm.Tick(0.016f, Idle());             // 结算回 Idle
            for (int i = 0; i < 10; i++) fsm.Tick(0.05f, Idle()); // 越过攻击冷却 0.4
            Assert.LessOrEqual(fsm.AttackCooldownRemaining, 0.0001f);
            fsm.Tick(0.016f, AttackPressed());    // 冷却就绪，再次触发
            Assert.IsTrue(fsm.AttackTriggeredThisTick);
            Assert.AreEqual(CombatState.Attack, fsm.State);
        }
    }
}
