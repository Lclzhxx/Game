// =============================================================
// 文件：TelemetryTests.cs（E0-S6 验收 · EditMode 无头）
// 作用：纯逻辑单测——聚合器滑动平均 / 滑动窗口截断 / 单位换算正确性。
//       不依赖任何渲染，满足「零渲染依赖 / 无头可跑（CI 可跑）」。
// 注：本环境无 Unity Editor，无法本地执行；请在 Unity Test Runner 或 CI 跑。
// =============================================================

using UnityEngine;
using NUnit.Framework;
using MJ.Services;

namespace MJ.Services.Tests
{
    [TestFixture]
    public class FrameStatsAggregatorTests
    {
        [Test]
        public void NoData_ReturnsZeroAndHasDataFalse()
        {
            var agg = new FrameStatsAggregator(8);
            agg.GetAverages(out float at, out float fps, out int dc, out int sp, out float ink);
            Assert.AreEqual(0f, at);
            Assert.AreEqual(0f, fps);
            Assert.AreEqual(0, dc);
            Assert.AreEqual(0, sp);
            Assert.AreEqual(0f, ink);
            Assert.IsFalse(agg.HasData);
            Assert.AreEqual(0, agg.SampleCount);
        }

        [Test]
        public void SingleSample_AveragesEqualSample()
        {
            var agg = new FrameStatsAggregator(8);
            agg.Record(16.6667f, 1200, 40, 2.5f);
            agg.GetAverages(out float at, out float fps, out int dc, out int sp, out float ink);
            Assert.AreEqual(16.6667f, at, 1e-3);
            Assert.AreEqual(60.0f, fps, 1e-2); // 1000 / 16.6667 ≈ 60.000X
            Assert.AreEqual(1200, dc);
            Assert.AreEqual(40, sp);
            Assert.AreEqual(2.5f, ink, 1e-4);
            Assert.IsTrue(agg.HasData);
            Assert.AreEqual(1, agg.SampleCount);
        }

        [Test]
        public void SlidingWindow_CapsAtCapacity_KeepsRecentFrames()
        {
            // 容量 4，推 6 帧；应只保留最后 4 帧（i=2,3,4,5）
            var agg = new FrameStatsAggregator(4);
            agg.Record(10, 100, 10, 1);
            agg.Record(20, 200, 20, 2);
            agg.Record(30, 300, 30, 3);
            agg.Record(40, 400, 40, 4);
            agg.Record(50, 500, 50, 5);
            agg.Record(60, 600, 60, 6);

            Assert.AreEqual(4, agg.SampleCount, "样本数应被窗口容量截断");
            agg.GetAverages(out float at, out float fps, out int dc, out int sp, out float ink);
            // 最近 4 帧：frameTime 30,40,50,60 => 均值 45
            Assert.AreEqual(45f, at, 1e-4);
            Assert.AreEqual(450, dc);             // (300+400+500+600)/4
            Assert.AreEqual(45, sp);              // (30+40+50+60)/4
            Assert.AreEqual(4.5f, ink, 1e-4);     // (3+4+5+6)/4
            Assert.AreEqual(FrameStatsAggregator.MsToFps(45f), fps, 1e-3);
        }

        [Test]
        public void UnitConversion_MsToFps_And_Reverse()
        {
            Assert.AreEqual(60f, FrameStatsAggregator.MsToFps(1000f / 60f), 1e-3);
            Assert.AreEqual(1000f / 60f, FrameStatsAggregator.FpsToMs(60f), 1e-3);
            // 零 / 负值保护：避免除零与 NaN
            Assert.AreEqual(0f, FrameStatsAggregator.MsToFps(0f));
            Assert.AreEqual(0f, FrameStatsAggregator.MsToFps(-1f));
            Assert.AreEqual(0f, FrameStatsAggregator.FpsToMs(0f));
            Assert.IsFalse(float.IsNaN(FrameStatsAggregator.MsToFps(0f)));
            Assert.IsFalse(float.IsInfinity(FrameStatsAggregator.MsToFps(0f)));
        }
    }
}
