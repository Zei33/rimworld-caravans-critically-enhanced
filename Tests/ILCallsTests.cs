using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace CaravansCriticallyEnhanced.Tests
{
    /// <summary>
    /// Holds the IL walker to its own integrity checks, which every IL assertion in the suite relies on.
    /// </summary>
    /// <remarks>
    /// Copied from the three walker tests in Simple Improve's <c>ImproveSiteTests</c>. A walker that
    /// misreads an instruction length returns a short call list, and "this method does not call that"
    /// then passes for the wrong reason, so the walker has to throw, and these show that it does.
    /// </remarks>
    [TestFixture]
    public class ILCallsTests
    {
        [Test]
        public void EveryMethodInTheModCanBeWalkedToItsEnd()
        {
            var failures = new List<string>();
            int walked = 0;

            foreach (MethodBase method in ILCalls.AllModMethods())
            {
                walked++;
                try
                {
                    ILCalls.CalledBy(method);
                }
                catch (ILWalkException exception)
                {
                    failures.Add(ILCalls.Describe(method) + ": " + exception.Message);
                }
            }

            Assert.That(walked, Is.GreaterThan(20), "The walk saw almost none of the mod.");
            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void TheWalkerHandlesTheOneInstructionThatIsNotAFixedLength()
        {
            // switch carries a count and then that many jump targets. A walk that gets its length
            // wrong reads the jump table as instructions; the branch-target check inside the walk is
            // what catches it.
            WalkResult walk = ILCalls.Read(
                typeof(ILCallsTests).GetMethod(nameof(DenseSwitch), BindingFlags.NonPublic | BindingFlags.Static));

            Assert.That(
                walk.Opcodes, Does.Contain(ILCalls.Switch),
                "DenseSwitch no longer compiles to a jump table, so this test exercises nothing.");

            Assert.That(
                walk.Calls.Select(ILCalls.Describe).ToList(),
                Is.EqualTo(new List<string> { "System.Math.Abs" }));
        }

        [Test]
        public void AWalkThatLandsInTheMiddleOfAnInstructionIsAFailure()
        {
            //   0: br.s +2      two bytes, so the next instruction is at 2 and the target is 2 + 2
            //   2: ldc.i4 0     five bytes, occupying 2 to 6
            //   7: ret
            // Offset 4 is inside the ldc.i4 operand, so the walk must refuse it even though it decodes
            // cleanly to the last byte.
            var landsMidOperand = new byte[] { 0x2B, 0x02, 0x20, 0x00, 0x00, 0x00, 0x00, 0x2A };
            MethodBase context = typeof(ILCallsTests).GetMethod(nameof(DenseSwitch), BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(
                () => ILCalls.Walk(landsMidOperand, context),
                Throws.TypeOf<ILWalkException>().With.Message.Contains("offset 4"));

            // The control: the same body with the branch aimed at the ret walks without complaint.
            var landsOnRet = new byte[] { 0x2B, 0x05, 0x20, 0x00, 0x00, 0x00, 0x00, 0x2A };

            Assert.That(() => ILCalls.Walk(landsOnRet, context), Throws.Nothing);
        }

        [Test]
        public void TheIntegerLoadedBeforeACallOrAStoreIsReadWhateverItsEncoding()
        {
            // ldc.i4.m1 to ldc.i4.8 carry the value in the opcode, ldc.i4.s in one signed byte and
            // ldc.i4 in four, so each encoding is read here, and a non-constant reads as null.
            WalkResult calls = ILCalls.Read(typeof(ILCallsTests).GetMethod(nameof(PassesConstants), BindingFlags.NonPublic | BindingFlags.Static));
            List<int?> passed = calls.Calls.Select((c, i) => c.Name == nameof(Consume) ? calls.IntBeforeCall[i] : -999).Where(v => v != -999).ToList();
            Assert.That(passed, Is.EqualTo(new int?[] { -1, 5, 100, -100, 250, 100000, null, null }), "The last is a conditional, so where a branch lands.");

            WalkResult stores = ILCalls.Read(typeof(ILCallsTests).GetMethod(nameof(StoresConstants), BindingFlags.NonPublic | BindingFlags.Static));
            Assert.That(
                stores.Fields.Where(f => f.IsStore).Select(f => f.Field.Name + "=" + (f.IntBefore?.ToString() ?? "null")).ToList(),
                Is.EqualTo(new[] { "Number=140", "Flag=0", "Flag=1", "Number=null", "Number=null" }),
                "The last is a conditional: the store is where a branch lands, so no one constant is its value.");
        }

        private static void PassesConstants(int variable)
        {
            Consume(-1);
            Consume(5);
            Consume(100);
            Consume(-100);
            Consume(250);
            Consume(100000);
            Consume(variable);
            Consume(variable > 0 ? 250 : 0);
        }

        private static void StoresConstants(Sample sample, int variable)
        {
            sample.Number = 140;
            sample.Flag = false;
            sample.Flag = true;
            sample.Number = variable;
            sample.Number = variable > 0 ? 140 : 0;
        }

        private static void Consume(int value)
        {
            GC.KeepAlive(value);
        }

        private sealed class Sample
        {
            public int Number;

            public bool Flag;
        }

        /// <summary>
        /// A method whose body contains a real IL <c>switch</c> instruction.
        /// </summary>
        /// <param name="value">The branch to take.</param>
        /// <returns>A number nobody reads.</returns>
        private static int DenseSwitch(int value)
        {
            int result;

            switch (value)
            {
                case 0: result = 10; break;
                case 1: result = 11; break;
                case 2: result = 12; break;
                case 3: result = 13; break;
                case 4: result = 14; break;
                case 5: result = 15; break;
                case 6: result = 16; break;
                case 7: result = 17; break;
                case 8: result = 18; break;
                case 9: result = 19; break;
                default: result = -1; break;
            }

            return Math.Abs(result);
        }
    }
}
