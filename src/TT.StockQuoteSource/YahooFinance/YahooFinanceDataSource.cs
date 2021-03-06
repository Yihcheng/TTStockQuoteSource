﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TT.StockQuoteSource.Contracts;

namespace TT.StockQuoteSource.YahooFinance
{
    /// <summary>
    /// class of YahooFinanceDataSource
    /// </summary>
    public class YahooFinanceDataSource : StockDataSourceBase
    {
        private readonly IStockQuoteParser _parser;

        /// <summary>
        /// ctor of YahooFinanceDataSource
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="operations"></param>
        /// <param name="parser"></param>
        public YahooFinanceDataSource(IConfiguration configuration, IStockQuoteDataSourceOperations operations, IStockQuoteParser parser)
            : base(configuration, Contracts.StockQuoteSource.Yahoo, operations)
        {
            _parser = parser;
        }

        /// <summary>
        /// Get the most recent stock quote from Yahoo Finance
        /// </summary>
        /// <param name="country"></param>
        /// <param name="stockId"></param>
        /// <param name="writeToErrorLogAction"></param>
        /// <returns></returns>
        public override async Task<IStockQuoteFromDataSource> GetMostRecentQuoteAsync(Country country, string stockId, Action<Exception> writeToErrorLogAction)
        {
            string stockFullId = country == Country.USA ? stockId : $"{stockId}.{country.GetShortName()}";
            string yahooUrl = string.Format(Configuration["YahooFinanceURL"], stockFullId);
            (string HtmlContent, IReadOnlyList<Cookie> Cookies) response = await GetHttpContentAsync(yahooUrl).ConfigureAwait(false);

            return _parser.ParseSingleQuote(country, stockId, response.HtmlContent, writeToErrorLogAction);
        }

        /// <summary>
        /// Get historical stock quotes from Yahoo Finance
        /// </summary>
        /// <param name="country"></param>
        /// <param name="stockId"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="writeToErrorLogAction"></param>
        /// <returns></returns>
        public override async Task<IReadOnlyList<IStockQuoteFromDataSource>> GetHistoricalQuotesAsync(Country country, string stockId, DateTime start, DateTime end, Action<Exception> writeToErrorLogAction)
        {
            string stockFullId = country == Country.USA ? stockId : $"{stockId}.{country.GetShortName()}";
            string yahooSingleQuoteUrl = string.Format(Configuration["YahooFinanceURL"], stockFullId);
            (string HtmlContent, IReadOnlyList<Cookie> Cookies) response = await GetHttpContentAsync(yahooSingleQuoteUrl).ConfigureAwait(false);
            IStockQuoteFromDataSource yahooQuote = _parser.ParseSingleQuote(country, stockId, response.HtmlContent, writeToErrorLogAction);
            IReadOnlyList<Cookie> cookies = response.Cookies;

            if (!(yahooQuote is YahooFinanceDataResult yahooResult) || string.IsNullOrEmpty(yahooResult.Crumb))
            {
                return null;
            }

            string startTimestamp = new DateTime(start.Year, start.Month, start.Day, 0, 0, 0).ToUnixTimestamp();
            string endTimestamp = new DateTime(end.Year, end.Month, end.Day, 23, 59, 59).ToUnixTimestamp();
            string yahooHistoricalUrl = string.Format(Configuration["YahooHistoricalDataUrl"], stockFullId, startTimestamp, endTimestamp, yahooResult.Crumb);
            (string HtmlContent, IReadOnlyList<Cookie> Cookies) response2 = await GetHttpContentAsync(yahooHistoricalUrl, cookies).ConfigureAwait(false);

            IReadOnlyList<IStockQuoteFromDataSource> quotes = _parser.ParseMultiQuotes(country, stockId, response2.HtmlContent, writeToErrorLogAction);

            return quotes.Where(a => a.TradeDateTime >= start && a.TradeDateTime <= end).ToList();
        }
    }
}
