using System;
using cAlgo.API;

namespace cAlgo
{
    [Indicator(IsOverlay = false, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class My_RSI_Strategy : Indicator
    {
        [Output("Buy Signal", LineColor = "Lime", PlotType = PlotType.Points, Thickness = 5)]
        public IndicatorDataSeries BuySignalOut { get; set; }

        [Output("Exit Buy Signal", LineColor = "Red", PlotType = PlotType.Points, Thickness = 5)]
        public IndicatorDataSeries ExitBuySignalOut { get; set; }

        [Output("Sell Signal", LineColor = "Orange", PlotType = PlotType.Points, Thickness = 5)]
        public IndicatorDataSeries SellSignalOut { get; set; }

        [Output("Exit Sell Signal", LineColor = "DeepSkyBlue", PlotType = PlotType.Points, Thickness = 5)]
        public IndicatorDataSeries ExitSellSignalOut { get; set; }

        private My_Kmean_RSI _baseIndicator;
        private My_RSI_Trend_Vote _bigTrendIndicator;
        private BaseIndicatorSettings _settings;

        private bool _hasBuyPosition;
        private bool _hasSellPosition;
        private bool _bullishTrendTraded;
        private bool _bearishTrendTraded;
        private int _lastTrendDirection;

        private double _buyEntryRsi;
        private double _sellEntryRsi;
        private double _totalRsiGain;
        private double _totalRsiLoss;

        private int _currentLossStreak;
        private int _maxConsecutiveLosses;
        private DateTime _maxConsecutiveLossesLastTime;

        private int _closedTrades;
        private int _winningTrades;
        private DateTime _firstTradeDate;

        protected override void Initialize()
        {
            _settings = CreateSettings();
            _baseIndicator = CreateBaseIndicator();
            _bigTrendIndicator = CreateBigTrendIndicator();

            _hasBuyPosition = false;
            _hasSellPosition = false;
            _bullishTrendTraded = false;
            _bearishTrendTraded = false;
            _lastTrendDirection = 0;

            _buyEntryRsi = double.NaN;
            _sellEntryRsi = double.NaN;
            _totalRsiGain = 0;
            _totalRsiLoss = 0;

            _currentLossStreak = 0;
            _maxConsecutiveLosses = 0;
            _maxConsecutiveLossesLastTime = DateTime.MinValue;

            _closedTrades = 0;
            _winningTrades = 0;
            _firstTradeDate = DateTime.MinValue;

            UpdateDisplay();
        }

        public override void Calculate(int index)
        {
            ResetSignals(index);

            if (!HasEnoughBars(index))
                return;

            if (!HasValidBaseValues(index))
                return;

            if (!HasValidTrendValue(index))
                return;

            int currentTrendDirection = GetTrendDirection(index);

            if (HasTrendChanged(currentTrendDirection))
            {
                _bullishTrendTraded = false;
                _bearishTrendTraded = false;
            }

            _lastTrendDirection = currentTrendDirection;

            // =====================================================
            // BUY SECTION
            // =====================================================
            if (!_hasBuyPosition && !_bullishTrendTraded && ShouldBuy(index))
            {
                BuySignalOut[index] = _baseIndicator.RsiOut[index];
                _buyEntryRsi = _baseIndicator.RsiOut[index];
                _hasBuyPosition = true;
                _bullishTrendTraded = true;
                if (_firstTradeDate == DateTime.MinValue)
                    _firstTradeDate = Bars.OpenTimes[index];
            }

            // =====================================================
            // BUY EXIT SECTION
            // =====================================================
            if (_hasBuyPosition && ShouldExitBuy(index))
            {
                double exitRsi = _baseIndicator.RsiOut[index];
                ExitBuySignalOut[index] = exitRsi;
                UpdateTradeMetrics(index, exitRsi - _buyEntryRsi);
                _hasBuyPosition = false;
                _buyEntryRsi = double.NaN;
            }

            // =====================================================
            // SELL SECTION
            // =====================================================
            if (!_hasSellPosition && !_bearishTrendTraded && ShouldSell(index))
            {
                SellSignalOut[index] = _baseIndicator.RsiOut[index];
                _sellEntryRsi = _baseIndicator.RsiOut[index];
                _hasSellPosition = true;
                _bearishTrendTraded = true;
                if (_firstTradeDate == DateTime.MinValue)
                    _firstTradeDate = Bars.OpenTimes[index];
            }

            // =====================================================
            // SELL EXIT SECTION
            // =====================================================
            if (_hasSellPosition && ShouldExitSell(index))
            {
                double exitRsi = _baseIndicator.RsiOut[index];
                ExitSellSignalOut[index] = exitRsi;
                UpdateTradeMetrics(index, _sellEntryRsi - exitRsi);
                _hasSellPosition = false;
                _sellEntryRsi = double.NaN;
            }

            // =====================================================
            // STATS DISPLAY SECTION
            // =====================================================
            UpdateDisplay();
        }

        private void ResetSignals(int index)
        {
            BuySignalOut[index] = double.NaN;
            ExitBuySignalOut[index] = double.NaN;
            SellSignalOut[index] = double.NaN;
            ExitSellSignalOut[index] = double.NaN;
        }

        private void UpdateTradeMetrics(int index, double rsiPnl)
        {
            _closedTrades++;

            if (rsiPnl > 0)
            {
                _totalRsiGain += rsiPnl;
                _winningTrades++;
                _currentLossStreak = 0;
                return;
            }

            if (rsiPnl < 0)
            {
                _totalRsiLoss += Math.Abs(rsiPnl);
                _currentLossStreak++;

                if (_currentLossStreak > _maxConsecutiveLosses)
                {
                    _maxConsecutiveLosses = _currentLossStreak;
                    _maxConsecutiveLossesLastTime = Bars.OpenTimes[index];
                }
            }
        }

        private void UpdateDisplay()
        {
            string profitFactorText = _totalRsiLoss == 0
                ? "RSI Profit Factor: N/A"
                : $"RSI Profit Factor: {_totalRsiGain / _totalRsiLoss:0.00}";

            string winRateText = _closedTrades == 0
                ? "Win Rate: N/A"
                : $"Win Rate: {((double)_winningTrades / _closedTrades) * 100:0.00}%";

            string maxLossText = $"Max Consecutive Losses: {_maxConsecutiveLosses}";

            string maxLossDateText = _maxConsecutiveLossesLastTime == DateTime.MinValue
                ? "Last Max Loss Date: N/A"
                : $"Last Max Loss Date: {_maxConsecutiveLossesLastTime:yyyy-MM-dd HH:mm}";

            string totalTradesText = $"Total Trades: {_closedTrades}";

            string firstTradeDateText = _firstTradeDate == DateTime.MinValue
                ? "First Trade Date: N/A"
                : $"First Trade Date: {_firstTradeDate:yyyy-MM-dd HH:mm}";

            Chart.DrawStaticText(
                "rsiStats",
                profitFactorText + "\n" +
                winRateText + "\n" +
                maxLossText + "\n" +
                maxLossDateText + "\n" +
                totalTradesText + "\n" +
                firstTradeDateText,
                VerticalAlignment.Top,
                HorizontalAlignment.Right,
                Color.Gold);
        }

        private bool HasEnoughBars(int index)
        {
            return index > 0;
        }

        private bool HasValidBaseValues(int index)
        {
            return !double.IsNaN(_baseIndicator.RsiOut[index]) &&
                   !double.IsNaN(_baseIndicator.RsiOut[index - 1]) &&
                   !double.IsNaN(_baseIndicator.ShortThresholdOut[index]) &&
                   !double.IsNaN(_baseIndicator.ShortThresholdOut[index - 1]) &&
                   !double.IsNaN(_baseIndicator.LongThresholdOut[index]) &&
                   !double.IsNaN(_baseIndicator.LongThresholdOut[index - 1]);
        }

        private bool HasValidTrendValue(int index)
        {
            return !double.IsNaN(_bigTrendIndicator.TrendOut3[index]);
        }

        private int GetTrendDirection(int index)
        {
            double trend = _bigTrendIndicator.TrendOut3[index];

            if (trend > 0)
                return 1;

            if (trend < 0)
                return -1;

            return 0;
        }

        private bool HasTrendChanged(int currentTrendDirection)
        {
            return currentTrendDirection != _lastTrendDirection;
        }

        private bool ShouldBuy(int index)
        {
            return IsBullishTrend(index) && HasCrossedUpLowerThreshold(index);
        }

        private bool ShouldExitBuy(int index)
        {
            return HasCrossedUpUpperThreshold(index) || HasCrossedDownLowerThreshold(index);
        }

        private bool ShouldSell(int index)
        {
            return IsBearishTrend(index) && HasCrossedDownUpperThreshold(index);
        }

        private bool ShouldExitSell(int index)
        {
            return HasCrossedDownLowerThreshold(index) || HasCrossedUpUpperThreshold(index);
        }

        private bool IsBullishTrend(int index)
        {
            return _bigTrendIndicator.TrendOut3[index] > 0;
        }

        private bool IsBearishTrend(int index)
        {
            return _bigTrendIndicator.TrendOut3[index] < 0;
        }

        private bool HasCrossedUpLowerThreshold(int index)
        {
            double previousRsi = _baseIndicator.RsiOut[index - 1];
            double currentRsi = _baseIndicator.RsiOut[index];

            double previousLowerThreshold = _baseIndicator.ShortThresholdOut[index - 1];
            double currentLowerThreshold = _baseIndicator.ShortThresholdOut[index];

            return previousRsi <= previousLowerThreshold &&
                   currentRsi > currentLowerThreshold;
        }

        private bool HasCrossedDownLowerThreshold(int index)
        {
            double previousRsi = _baseIndicator.RsiOut[index - 1];
            double currentRsi = _baseIndicator.RsiOut[index];

            double previousLowerThreshold = _baseIndicator.ShortThresholdOut[index - 1];
            double currentLowerThreshold = _baseIndicator.ShortThresholdOut[index];

            return previousRsi >= previousLowerThreshold &&
                   currentRsi < currentLowerThreshold;
        }

        private bool HasCrossedUpUpperThreshold(int index)
        {
            double previousRsi = _baseIndicator.RsiOut[index - 1];
            double currentRsi = _baseIndicator.RsiOut[index];

            double previousUpperThreshold = _baseIndicator.LongThresholdOut[index - 1];
            double currentUpperThreshold = _baseIndicator.LongThresholdOut[index];

            return previousRsi <= previousUpperThreshold &&
                   currentRsi > currentUpperThreshold;
        }

        private bool HasCrossedDownUpperThreshold(int index)
        {
            double previousRsi = _baseIndicator.RsiOut[index - 1];
            double currentRsi = _baseIndicator.RsiOut[index];

            double previousUpperThreshold = _baseIndicator.LongThresholdOut[index - 1];
            double currentUpperThreshold = _baseIndicator.LongThresholdOut[index];

            return previousRsi >= previousUpperThreshold &&
                   currentRsi < currentUpperThreshold;
        }

        private My_Kmean_RSI CreateBaseIndicator()
        {
            return Indicators.GetIndicator<My_Kmean_RSI>(
                _settings.Source,
                _settings.RsiLength,
                _settings.Smooth,
                _settings.SmoothPeriod,
                _settings.LookbackWindow,
                _settings.ExtramLongOffset,
                _settings.ExtramShortOffset
            );
        }

        private My_RSI_Trend_Vote CreateBigTrendIndicator()
        {
            return Indicators.GetIndicator<My_RSI_Trend_Vote>(_settings.Source);
        }

        private BaseIndicatorSettings CreateSettings()
        {
            return new BaseIndicatorSettings
            {
                Source = Bars.ClosePrices,
                RsiLength = 14,
                Smooth = true,
                SmoothPeriod = 8,
                LookbackWindow = 3000,
                ExtramLongOffset = 22,
                ExtramShortOffset = 22
            };
        }

        private class BaseIndicatorSettings
        {
            public DataSeries Source { get; set; }
            public int RsiLength { get; set; }
            public bool Smooth { get; set; }
            public int SmoothPeriod { get; set; }
            public int LookbackWindow { get; set; }
            public double ExtramLongOffset { get; set; }
            public double ExtramShortOffset { get; set; }
        }
    }
}
