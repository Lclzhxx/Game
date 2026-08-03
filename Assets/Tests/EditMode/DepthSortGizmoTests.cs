// =============================================================
// 文件：DepthSortGizmoTests.cs（E2-S3 验收 · EditMode 无头）
// 作用：验证 gizmo 绘制的排序轴向量 == DepthSortBootstrap 推导轴
//      （≈ (0, -0.7071, -0.7071)，容差 1e-3），CI 可跑。
//      注：gizmo 的实际绘制（箭头/标签）为 Scene 视图人工目视项；
//          此处单测覆盖其唯一有逻辑、可量化的部分——轴向量计算。
// 注：本环境无 Unity Editor，无法本地执行；请在 Unity Test Runner 或 CI 跑。
// =============================================================

using UnityEngine;
using NUnit.Framework;

namespace MJ.Editor.Tests
{
    [TestFixture]
    public class DepthSortGizmoTests
    {
        // 验收：Gizmo 绘制轴 == DepthSortBootstrap 推导轴（EditMode，CI 可跑）
        [Test]
        public void SortAxis_DefaultOffset_MatchesExpectedYzAxis_Tolerance1e3()
        {
            Vector3 offset = new Vector3(0f, 14f, 14f);
            Vector3 axis = DepthSortBootstrap.DeriveAxis(offset);

            Vector3 expected = new Vector3(0f, -0.7071f, -0.7071f);
            Assert.AreEqual(expected.x, axis.x, 1e-3, "X 分量不符");
            Assert.AreEqual(expected.y, axis.y, 1e-3, "Y 分量不符");
            Assert.AreEqual(expected.z, axis.z, 1e-3, "Z 分量不符");
            // 轴必须为单位向量（归一化）
            Assert.AreEqual(1f, axis.magnitude, 1e-3, "轴应为单位向量");
        }

        [Test]
        public void SortAxis_GeneralOffset_IsNegativeNormalizedUnit()
        {
            Vector3 offset = new Vector3(0f, 10f, 10f);
            Vector3 axis = DepthSortBootstrap.DeriveAxis(offset);
            Assert.AreEqual(1f, axis.magnitude, 1e-4, "轴应为单位向量");
            // 轴 == -offset.normalized
            Vector3 negNorm = (-offset).normalized;
            Assert.AreEqual(negNorm.x, axis.x, 1e-4);
            Assert.AreEqual(negNorm.y, axis.y, 1e-4);
            Assert.AreEqual(negNorm.z, axis.z, 1e-4);
        }

        [Test]
        public void SortAxis_ZeroOffset_FallsBackGracefully()
        {
            // 近零向量：Unity 的 normalized 返回 (0,0,0)（不抛异常），
            // gizmo 画零长轴也不会崩，与 DepthSortBootstrap 对近零 offset 的容错一致。
            Vector3 axis = DepthSortBootstrap.DeriveAxis(Vector3.zero);
            Assert.AreEqual(0f, axis.magnitude, 1e-6);
        }
    }
}
