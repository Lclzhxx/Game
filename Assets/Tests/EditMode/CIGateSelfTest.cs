// =============================================================
// TEMPORARY SELF-CHECK TEST - DO NOT SHIP.
//
// Purpose: prove that the EditMode CI gate (S2-R7, ci.yml step 5)
// actually turns RED when a test fails. The gate is only trustworthy
// if it can be observed failing. This test fails on purpose.
//
// Lifecycle: once the gate self-check is verified on the real runner
// (a red CI run caused by this test), DELETE this file AND its .meta.
// Do not merge it into any release branch.
//
// See: production/sprints/sprint-02-plan.md (S2-R7), .github/workflows/ci.yml
// =============================================================

using NUnit.Framework;

namespace MJ.Tests.EditMode
{
    public class CIGateSelfTest
    {
        // Intentional failure. Proves S2-R7 gate catches test failures
        // (Unity test-runner exit code 2 -> ci.yml step 5 throws -> CI red).
        [Test]
        public void S2_R7_GateSelfCheck_ForceFail()
        {
            Assert.Fail(
                "TEMP CI gate self-check: this failure is INTENTIONAL. " +
                "It proves the S2-R7 EditMode gate turns red on test failure. " +
                "Remove CIGateSelfTest.cs (and .meta) after the gate self-check passes.");
        }
    }
}
