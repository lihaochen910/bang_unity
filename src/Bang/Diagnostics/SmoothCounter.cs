﻿using System;


namespace Bang.Diagnostics
{
    /// <summary>
    /// Class used to smooth the counter of performance ticks.
    /// </summary>
    public class SmoothCounter
    {
        private int _index = 0;

        private double _totalDeltaTime = 0;
        private double[] _previousTime;

        private double _longestTime = 0;

        private int _totalEntitiesCount = 0;
        private int[] _previousEntityCount;

        private readonly int _sampleSize;

        /// <summary>
        /// Average of counter time value over the sample size.
        /// </summary>
        public int AverageTime => (int)MathF.Round((float)(_totalDeltaTime / _sampleSize));

        /// <summary>
        /// Average of entities over the sample size.
        /// </summary>
        public int AverageEntities => (int)MathF.Round(_totalEntitiesCount / (float)_sampleSize);

        /// <summary>
        /// Maximum value over the sample size.
        /// </summary>
        public double MaximumTime => _longestTime;

        /// <summary>
        /// Creates a new <see cref="SmoothCounter"/>.
        /// </summary>
        /// <param name="size">Default batch size when averaging the last frames for the FPS.</param>
        public SmoothCounter(int size = 500) => (_sampleSize, _previousTime, _previousEntityCount) = (size, new double[size], new int[size]);

        /// <summary>
        /// Clear the counter track.
        /// </summary>
        public void Clear()
        {
            _index = 0;

            _totalDeltaTime = 0;
            _totalEntitiesCount = 0;

            _longestTime = 0;

            _previousTime = new double[_sampleSize];
            _previousEntityCount = new int[_sampleSize];
        }

        /// <summary>
        /// Update the smooth counter for the FPS report.
        /// </summary>
        /// <param name="ms">Time for the operation.</param>
        /// <param name="totalEntities">Total of entities pulled for this system.</param>
        public void Update(double ms, int totalEntities)
        {
            _index++;

            if (_index == _sampleSize)
            {
                _index = 0;
                _longestTime = 0;
            }

            if (ms > _longestTime)
            {
                _longestTime = ms;
            }

            _totalDeltaTime -= _previousTime[_index];
            _totalDeltaTime += ms;

            _previousTime[_index] = ms;

            _totalEntitiesCount -= _previousEntityCount[_index];
            _totalEntitiesCount += totalEntities;

            _previousEntityCount[_index] = totalEntities;
        }
    }
}