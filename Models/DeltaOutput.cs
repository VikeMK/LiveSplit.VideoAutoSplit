﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSplit.VAS.Models.Delta
{
    public struct DeltaOutput
    {
        public static readonly DeltaOutput Blank = new DeltaOutput();

        private DeltaManager Manager;

        internal DeltaHistory History => Manager.History;
        internal int HistorySize => History.Count;

        internal int OriginalIndex { get; private set; }
        internal int FrameIndex { get; private set; }
        internal double FrameRate { get; }

        private IEnumerable<int> _FeatureIndexes;
        private IEnumerable<int> FeatureIndexes
        {
            get
            {
                var featureIndexes = _FeatureIndexes;
                _FeatureIndexes = null;
                return featureIndexes;
            }
            set
            {
                _FeatureIndexes = value;
            }
        }

        internal DeltaOutput(DeltaManager manager, int index, double frameRate)
        {
            Manager = manager;
            OriginalIndex = index;
            FrameIndex = index % Manager.History.Count;
            FrameRate = frameRate;

            _FeatureIndexes = null;
        }

        internal bool IsBlank
        {
            get
            {
                try
                {
                    return Manager == null;
                }
                catch { return true; }
            }
        }

        // Name is questionable
        private int IndexFromOffset(int offset)
        {
            return (OriginalIndex - offset) % HistorySize;
        }

        // Name is questionable
        private int FrameOffset(int milliseconds)
        {
            try
            {
                return Math.Max(1, (int)Math.Round(FrameRate * milliseconds / 1000d));
            }
            catch (Exception e)
            {
                var millisecondOffsetLimit = (int)Math.Ceiling((HistorySize - 1) / FrameRate * 1000);
                if (milliseconds < 0)
                    throw new IndexOutOfRangeException("Offset cannot be negative.");
                else if (milliseconds > millisecondOffsetLimit)
                    throw new IndexOutOfRangeException(
                        "Offset cannot exceed the history's count, which is currently " +
                        HistorySize.ToString() +
                        ", and this is trying to access previous frame #" +
                        Math.Round(FrameRate * milliseconds / 1000d).ToString() +
                        ".");
                else
                    throw e;
            }
        }

        private void FrameOffsets(int startMilliseconds, int duration, out int startOffset, out int endOffset)
        {
            if (startMilliseconds <= 0)
            {
                startOffset = 0;
                startMilliseconds = 0;
            }
            else
                startOffset = FrameOffset(startMilliseconds);

            if (duration <= 0)
                endOffset = FrameOffset(startMilliseconds + Manager.DefaultOffset);
            else
                endOffset = FrameOffset(startMilliseconds + duration);
        }

        private IEnumerable<double> GetDeltaRange(int startMilliseconds, int duration, IEnumerable<int> indexes = null)
        {
            int startOffset;
            int endOffset;
            FrameOffsets(startMilliseconds, duration, out startOffset, out endOffset);

            var featureIndexes = indexes?.Count() == 0 ? FeatureIndexes : indexes ?? FeatureIndexes;
            var featureCount = featureIndexes.Count();

            var offsetDiff = endOffset - startOffset;

            if (offsetDiff > 0)
            {
                for (int i = 0; i < endOffset - startOffset; i++)
                {
                    var frameIndex = IndexFromOffset(i + startOffset);
                    for (int n = 0; n < featureCount; n++)
                    {
                        var t = n + i * featureCount;
                        yield return Manager.History[frameIndex, featureIndexes.ElementAt(n)]; // Todo: Handle NaN.
                    }
                }
            }
            else
                yield return double.NaN;
        }

        private IEnumerable<double> GetDeltaRange(Func<IEnumerable<double>, double> func, int startMilliseconds, int duration, IEnumerable<int> indexes)
        {
            int startOffset;
            int endOffset;
            FrameOffsets(startMilliseconds, duration, out startOffset, out endOffset);

            var featureIndexes = indexes.Count() == 0 ? FeatureIndexes : indexes;
            var featureCount = featureIndexes.Count();

            var offsetDiff = endOffset - startOffset;

            if (offsetDiff > 0)
            {
                var featureArray = new double[featureCount];

                for (int i = 0; i < offsetDiff; i++)
                {
                    var frameIndex = IndexFromOffset(i + startOffset);
                    for (int n = 0; n < featureCount; n++)
                    {
                        featureArray[n] = Manager.History[frameIndex, featureIndexes.ElementAt(n)]; // Todo: Handle NaN.
                    }
                    yield return func(featureArray);
                }
            }
            else
                yield return double.NaN;
        }

        private IEnumerable<double> GetDeltaRangeInverse(Func<IEnumerable<double>, double> func, int startMilliseconds, int duration, IEnumerable<int> indexes)
        {
            int startOffset;
            int endOffset;
            FrameOffsets(startMilliseconds, duration, out startOffset, out endOffset);

            var featureIndexes = indexes.Count() == 0 ? FeatureIndexes : indexes;
            var featureCount = featureIndexes.Count();

            var offsetDiff = endOffset - startOffset;

            if (offsetDiff > 0)
            {
                var indexArray = new double[offsetDiff];

                for (int i = 0; i < featureCount; i++)
                {
                    for (int n = 0; n < offsetDiff; n++)
                    {
                        var frameIndex = IndexFromOffset(n + startOffset);
                        indexArray[n] = Manager.History[frameIndex, featureIndexes.ElementAt(i)];
                    }
                    yield return func(indexArray);
                }
            }
            else
                yield return double.NaN;
        }

        private IEnumerable<double> MinMany(int startMilliseconds, int endMilliseconds)
        {
            var featureIndexes = FeatureIndexes;
            return GetDeltaRange((x) => { return x.Min(); }, startMilliseconds, endMilliseconds - startMilliseconds, featureIndexes);
        }

        private IEnumerable<double> MinManyInverse(int startMilliseconds, int endMilliseconds)
        {
            var featureIndexes = FeatureIndexes;
            return GetDeltaRangeInverse((x) => { return x.Min(); }, startMilliseconds, endMilliseconds - startMilliseconds, featureIndexes);
        }

        private IEnumerable<double> MaxMany(int startMilliseconds, int endMilliseconds)
        {
            var featureIndexes = FeatureIndexes;
            return GetDeltaRange((x) => { return x.Max(); }, startMilliseconds, endMilliseconds - startMilliseconds, featureIndexes);
        }

        private IEnumerable<double> MaxManyInverse(int startMilliseconds, int endMilliseconds)
        {
            var featureIndexes = FeatureIndexes;
            return GetDeltaRangeInverse((x) => { return x.Max(); }, startMilliseconds, endMilliseconds - startMilliseconds, featureIndexes);
        }

        #region VASL Syntax

        public double current // Single index only
        {
            get
            {
                return Manager.History[FrameIndex, FeatureIndexes.First()];
            }
        }

        public double old(int milliseconds = 0) // Single index only
        {
            if (milliseconds <= 0)
                milliseconds = Manager.DefaultOffset;

            var prevFrameIndex = IndexFromOffset(FrameOffset(milliseconds));
            return Manager.History[prevFrameIndex, FeatureIndexes.First()];
        }

        // For the below, actual timestamps will be used once splitting can be offset'd.
        public void pause(double milliseconds = 0d)
        {
            var untilDate = milliseconds > 0d ? Manager.History[FrameIndex].FrameEnd.AddMilliseconds(milliseconds) : DateTime.MaxValue;
            //var untilDate = milliseconds > 0d ? TimeStamp.CurrentDateTime.Time.AddMilliseconds(milliseconds) : DateTime.MaxValue;
            foreach (var f in FeatureIndexes)
            {
                Manager.CompiledFeatures.PauseFeature(f, untilDate);
            }
        }

        public void resume(double milliseconds = 0d)
        {
            var untilDate = milliseconds > 0d ? Manager.History[FrameIndex].FrameEnd.AddMilliseconds(milliseconds) : DateTime.MaxValue;
            foreach (var f in FeatureIndexes)
            {
                Manager.CompiledFeatures.ResumeFeature(f, untilDate);
            }
        }

        public void pauseAll()
        {
            var untilDate = DateTime.MaxValue;
            for (int i = 0; i < Manager.CompiledFeatures.FeatureCount; i++)
            {
                Manager.CompiledFeatures.PauseFeature(i, untilDate);
            }
        }

        public bool isPaused
        {
            get
            {
                var result = false;
                foreach (var f in FeatureIndexes)
                {
                    if (double.IsNaN(Manager.History[FrameIndex, f]))
                    {
                        result = true;
                        break;
                    }
                }
                return result;
            }
        }

        public double min(int milliseconds = 0)
        {
            return min(0, milliseconds);
        }

        public double min(int startMilliseconds, int endMilliseconds)
        {
            var range = GetDeltaRange(startMilliseconds, endMilliseconds - startMilliseconds);
            return range.Min();
        }

        public double max(int milliseconds = 0)
        {
            return max(0, milliseconds);
        }

        public double max(int startMilliseconds, int endMilliseconds)
        {
            var range = GetDeltaRange(startMilliseconds, endMilliseconds - startMilliseconds);
            return range.Max();
        }

        public double average(int milliseconds = 0)
        {
            return average(0, milliseconds);
        }

        public double average(int startMilliseconds, int endMilliseconds)
        {
            var range = GetDeltaRange(startMilliseconds, endMilliseconds - startMilliseconds);
            return range.Average();
        }

        public double stdev(int milliseconds = 0)
        {
            return stdev(0, milliseconds);
        }

        public double stdev(int startMilliseconds, int endMilliseconds)
        {
            var range = GetDeltaRange(startMilliseconds, endMilliseconds - startMilliseconds);
            return range.StdDev();
        }

        public double delta(int milliseconds = 0)
        {
            return delta(0, milliseconds);
        }

        public double delta(int startMilliseconds, int endMilliseconds) // Single index only
        {
            var featureIndex = FeatureIndexes.First();

            double start;
            if (startMilliseconds <= 0)
            {
                start = this[featureIndex].current;
                startMilliseconds = 0;
            }
            else
                start = this[featureIndex].old(startMilliseconds);

            return start / this[featureIndex].old(endMilliseconds - startMilliseconds);
        }

        // Incomplete
        public double dupeDelta(int milliseconds = 0) // Single index only
        {
            var featureIndex = FeatureIndexes.First();

            return this[featureIndex].min(milliseconds) / this[featureIndex].max(milliseconds, milliseconds * 2);
        }

        /*
        // DEBUGGING
        public double jitter // Single index only
        {
            get
            {
                return Benchmarks[FrameIndex, FeatureIndexes[0]] * FrameRate;
            }
        }
        */

        public DeltaOutput this[params int[] numbers]
        {
            get
            {
                FeatureIndexes = numbers;
                return this;
            }
        }

        // Small performance booster since this is used far more often
        public DeltaOutput this[string str]
        {
            get
            {
                IEnumerable<int> i;
                if (!Manager.CompiledFeatures.IndexNames.TryGetValue(str, out i))
                    throw new ArgumentException("This name does not exist.");

                FeatureIndexes = i;
                return this;
            }
        }

        public DeltaOutput this[params string[] strings]
        {
            get
            {
                List<int> numbers = new List<int>();
                for (int n = 0; n < strings.Length; n++)
                {
                    IEnumerable<int> i;
                    if (!Manager.CompiledFeatures.IndexNames.TryGetValue(strings[n], out i))
                        throw new ArgumentException("This name does not exist.");
                    numbers.AddRange(i);
                }
                FeatureIndexes = numbers.Distinct();
                return this;
            }
        }

        public DeltaOutput this[int number, params string[] strings]
        {
            get
            {
                OriginalIndex = number;
                FrameIndex = number % Manager.History.Count;
                return this[strings];
            }
        }

        // Great naming. No one will ever get them mixed up.
        public double maxMin(int milliseconds = 0)
        {
            return MinMany(0, milliseconds).Max();
        }

        public double minMax(int milliseconds = 0)
        {
            return MaxMany(0, milliseconds).Min();
        }

        public double maxMinInverse(int milliseconds = 0)
        {
            return MinManyInverse(0, milliseconds).Max();
        }

        public double minMaxInverse(int milliseconds = 0)
        {
            return MaxManyInverse(0, milliseconds).Min();
        }

        #endregion VASL Syntax

    }
}
