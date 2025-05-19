using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace ImprovedPentominoSolver.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void SolveSelectedPieces()
        {
            var method = typeof(Program).GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var originalOut = Console.Out;
            var originalIn = Console.In;
            using var writer = new StringWriter();
            using var reader = new StringReader(string.Empty);
            Console.SetOut(writer);
            Console.SetIn(reader);
            try
            {
                method.Invoke(null, new object[] { new[] { "-l", "-y", "-v", "-t", "-w", "-z" } });
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetIn(originalIn);
            }

            var output = writer.ToString();
            Assert.Contains("Solution found!", output);
            Assert.Contains("boardField is 5, 6", output);
            Assert.Contains("T V V V L", output);
        }

        [Fact]
        public void SolveSelectedPiecesCombined()
        {
            var method = typeof(Program).GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var originalOut = Console.Out;
            var originalIn = Console.In;
            using var writer = new StringWriter();
            using var reader = new StringReader(string.Empty);
            Console.SetOut(writer);
            Console.SetIn(reader);
            try
            {
                method.Invoke(null, new object[] { new[] { "-lyvtwz" } });
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetIn(originalIn);
            }

            var output = writer.ToString();
            Assert.Contains("Solution found!", output);
            Assert.Contains("boardField is 5, 6", output);
            Assert.Contains("T V V V L", output);
        }
    }
}
