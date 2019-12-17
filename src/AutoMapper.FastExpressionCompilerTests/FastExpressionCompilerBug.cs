using FastExpressionCompiler;
using NUnit.Framework;

namespace AutoMapper.FastExpressionCompilerTests
{
    [TestFixture]
    public class FastExpressionCompilerBug
    {
        public class Source
        {
            public int Value { get; set; }
        }

        public class Dest
        {
            public int Value { get; set; }
        }

        [Test]
        public void ShouldWork()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<Source, Dest>());
            var mapper = config.CreateMapper();
            var expression = mapper.ConfigurationProvider.BuildExecutionPlan(typeof(Source), typeof(Dest));
            var fs = expression.Compile();
            var ff = expression.CompileFast(true);

            var source = new Source { Value = 5 };
            var dest = mapper.Map<Dest>(source);

            Assert.AreEqual(source.Value, dest.Value);
        }
    }
}
