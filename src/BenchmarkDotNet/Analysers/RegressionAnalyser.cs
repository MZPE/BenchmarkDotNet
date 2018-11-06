﻿using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Mathematics.StatisticalTesting;
using BenchmarkDotNet.Reports;

namespace BenchmarkDotNet.Analysers
{
    public class RegressionAnalyser : AnalyserBase
    {
        public static readonly IAnalyser Default = new RegressionAnalyser(RelativeThreshold.Default);
        
        public RegressionAnalyser(Threshold threshold) => Threshold = threshold;

        public override string Id => nameof(RegressionAnalyser);

        private Threshold Threshold { get; }

        protected override IEnumerable<Conclusion> AnalyseSummary(Summary summary)
        {
            foreach (var sameMethod in summary.BenchmarksCases.GroupBy(benchmark => benchmark.Descriptor))
            foreach (var sameArguments in sameMethod.GroupBy(benchmark => benchmark.Parameters))
            {
                var benchmarks = sameArguments.ToArray();
                if (benchmarks.Length != 2)
                    continue;

                var baseline = benchmarks.Single(benchmark => benchmark.Job.Meta.Baseline);
                var diff = benchmarks.Single(benchmark => !benchmark.Job.Meta.Baseline);
                
                var x = summary[baseline].ResultStatistics.GetOriginalValues().ToArray();
                var y = summary[diff].ResultStatistics.GetOriginalValues().ToArray();

                if (x.Length > y.Length)
                    x = x.Take(y.Length).ToArray();
                else if (y.Length > x.Length)
                    y = y.Take(x.Length).ToArray();
                
                var welchTestResult = StatisticalTestHelper.CalculateTost(WelchTest.Instance, x, y, Threshold);
                var mannWhitneyTestResult = StatisticalTestHelper.CalculateTost(MannWhitneyTest.Instance, x, y, Threshold);

                var conclusions = new[] { welchTestResult.Conclusion, mannWhitneyTestResult.Conclusion };
                
                if (conclusions.All(conclusion => conclusion == EquivalenceTestConclusion.Same))
                    yield return Conclusion.CreateHint(Id, $"Same for {baseline.Descriptor.GetFilterName()} {baseline.Parameters.DisplayInfo}");
                else if (conclusions.All(conclusion => conclusion == EquivalenceTestConclusion.Slower))
                    yield return Conclusion.CreateError(Id, $"Slower for {baseline.Descriptor.GetFilterName()} {baseline.Parameters.DisplayInfo}");
                else if (conclusions.All(conclusion => conclusion == EquivalenceTestConclusion.Faster))
                    yield return Conclusion.CreateHint(Id, $"Faster for {baseline.Descriptor.GetFilterName()} {baseline.Parameters.DisplayInfo}");
                else
                    yield return Conclusion.CreateHint(Id, $"Not sure, MannWhitneyTest={mannWhitneyTestResult.Conclusion} but WelchTest={welchTestResult.Conclusion} for {baseline.Descriptor.GetFilterName()} {baseline.Parameters.DisplayInfo}");
            }
        }
    }
}