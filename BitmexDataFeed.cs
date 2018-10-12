using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using WebSocket4Net;
using WTT.BitmexDataFeed;
using WTT.BitmexDataFeed.Properties;
using WTT.IDataFeed;

namespace WTT.BitmexDataFeed
{
    public class BitmexDataFeed : IDataFeed.IDataFeed
    {
        private readonly WebClient _client = new WebClient();

        private readonly ctlLogin _loginForm = new ctlLogin();

        private readonly Queue<HistoryRequest> _requests = new Queue<HistoryRequest>();
        private readonly List<string> _symbols = new List<string>();

        private bool _isWebClientBusy;

        private readonly coinApiSocketObject coinApiSocketMessage = new coinApiSocketObject();

        private WebSocket coinSocket;
        private readonly List<string> coinSocketDataType = new List<string>();

        private readonly AutoResetEvent m_OpenedEvent = new AutoResetEvent(false);

        //string format for dates is:  2018-09-21T00:00:00.0000000Z
        private readonly CultureInfo MyCultureInfo = new CultureInfo("en-EN");

        private string ApiKey { get; set; }

        public event EventHandler<MessageEventArgs> OnNewMessage;
        public event EventHandler<DataFeedStatusEventArgs> OnNewStatus;
        public event EventHandler<DataFeedStatusTimeEventArgs> OnNewStatusTime;

        public Control GetLoginControl()
        {
            return _loginForm;
        }

        #region Not supported DataFeed methods

        //... unused
        public double GetSymbolOffset(string symbol)
        {
            return 0.0;
        }

        #endregion

        #region Helper Functions

        private void FireConnectionStatus(string message)
        {
            OnNewMessage?.Invoke(this, new MessageEventArgs {Message = message, Icon = OutputIcon.Warning});
        }

        #endregion

        private class HistoryRequest
        {
            public ChartSelection ChartSelection { get; set; }
            public IHistorySubscriber HistorySubscriber { get; set; }
        }

        //...CoinAPI WebSocket Message class
        public class coinApiSocketObject
        {
            public string type { get; set; }
            public string apikey { get; set; }
            public bool heartbeat { get; set; }
            public List<string> subscribe_data_type { get; set; }
            public List<string> subscribe_filter_symbol_id { get; set; }
        }

        #region Login/logout

        public string Name => "CoinApi";

        public bool ValidateLoginParams()
        {
            //Check for valid ApiKey

            ApiKey = _loginForm.ApiKey;
            Settings.Default.ApiKey = ApiKey;

            try
            {
                Settings.Default.Save();
            }
            catch
            {
            }

            return true;
        }


        //... CoinApi WebSocket Connection Opened
        private void OnConnected(object sender, EventArgs e)
        {
            FireConnectionStatus("Connected to CoinAPI streaming socket.");

            //...setup basic hello message
            coinApiSocketMessage.apikey = ApiKey;
            coinApiSocketMessage.type = "hello";
            coinApiSocketMessage.heartbeat = false;

            coinSocketDataType.Add("trade"); //... change to other message type, check coinapi docs
            coinApiSocketMessage.subscribe_data_type = coinSocketDataType;

            m_OpenedEvent.Set();

        }


        //... CoinApi WebSocket Message Received
        private void OnMessage(object sender, MessageReceivedEventArgs e)
        {
            //Debug only
            //FireConnectionStatus("i:"+e.Message);

            var jsonMessage = e.Message;

            try
            {
                var unpacked = JsonConvert.DeserializeObject<dynamic>(jsonMessage);

                if (unpacked == null) return;


                var type = (string) unpacked.type;
                    
                switch (type)
                {
                    case "trade":
                        var priceUpate = new QuoteData();
                        priceUpate.Symbol = unpacked.symbol_id;
                        priceUpate.Price = unpacked.price;
                        var parsedDate = DateTime.Parse((string) unpacked.time_exchange, MyCultureInfo);
                        priceUpate.TradeTime = parsedDate; 

                        //Update charts
                        BrodcastQuote(priceUpate);

                        //Debug only
                        //FireConnectionStatus(priceUpate.Symbol + ": " + priceUpate.Price + " Time: " +
                        //                     priceUpate.TradeTime);
                        break;

                    case "error":
                        FireConnectionStatus((string) unpacked.message);
                        MessageBox.Show((string) unpacked.message, "CoinApi WebSocket Streaming Error");
                        break;
                }
            }
            catch
            {
                //could not decode data from streaming socket
                //should not happen, but just in case...
                FireConnectionStatus("Error unpacking quote.");
            }
        }

        public bool Login()
        {
            //...Set the header CoinApi API key
            try
            {
                _client.Headers.Clear();
            }
            catch
            {
            }

            _client.Headers.Set("x-coinapi-key", ApiKey);

            //...Initiate the WebSocket
            var socketAddress = Settings.Default.WebSocket;

            coinSocket = new WebSocket(socketAddress);
            coinSocket.Opened += OnConnected;
            //not implemented yet: websocket.Error += new EventHandler<ErrorEventArgs>(websocket_Error);
            //not implemented yet: websocket.Closed += new EventHandler(websocket_Closed);
            coinSocket.MessageReceived += OnMessage;
            coinSocket.Open();

            if (!m_OpenedEvent.WaitOne(10000))
            {
                FireConnectionStatus("Failed to Opened CoinApi socket session on time.");
                return false;
            }

            return true;
        }

        public bool Logout()
        {
            coinSocket?.Close();

            FireConnectionStatus("Disconnected from " + Name);
            return true;
        }

        #endregion

        #region Realtime-feed

        private readonly List<IDataSubscriber> _subscribers = new List<IDataSubscriber>();

        public void InitDataSubscriber(IDataSubscriber subscriber)
        {
            lock (_subscribers)
            {
                if (!_subscribers.Contains(subscriber))
                    _subscribers.Add(subscriber);
            }
        }

        public void RemoveDataSubscriber(IDataSubscriber subscriber)
        {
            lock (_subscribers)
            {
                if (_subscribers.Contains(subscriber))
                {
                    _subscribers.Remove(subscriber);
                    subscriber.UnSubscribeAll();
                }
            }
        }

        public void Subscribe(string symbol)
        {
            //...failsafe...
            if (coinSocket == null) return;

            symbol = symbol.ToUpper();

            if (_symbols.Contains(symbol)) return;

            lock (_symbols)
            {
                _symbols.Add(symbol);
            }

            FireConnectionStatus("Listening to " + symbol);

            //...send out the message to listen for data
            coinApiSocketMessage.subscribe_filter_symbol_id = _symbols;
            var _coinApiSocketMessage = JsonConvert.SerializeObject(coinApiSocketMessage);
            coinSocket.Send(_coinApiSocketMessage);
        }

        public void UnSubscribe(string symbol)
        {
            //failsafe...
            if (coinSocket == null) return;

            if (!_symbols.Contains(symbol))
                return;

            lock (_subscribers)
            {
                if (_subscribers.Any(s => s.IsSymbolWatching(symbol)))
                    return;
            }

            lock (_symbols)
            {
                _symbols.Remove(symbol);
            }

            FireConnectionStatus("De-Listening to " + symbol);

            //...send out the message to listen for data

            //... failsafe, never listen to all incoming messages, keep the last to listen
            if (_symbols.Count == 0) return;

            coinApiSocketMessage.subscribe_filter_symbol_id = _symbols;
            var _coinApiSocketMessage = JsonConvert.SerializeObject(coinApiSocketMessage);
            coinSocket.Send(_coinApiSocketMessage);


        }

        private void BrodcastQuote(QuoteData quote)
        {
            lock (_subscribers)
            {
                foreach (var subscriber in _subscribers) subscriber.OnPriceUpdate(quote);
            }
        }

        #endregion

        #region History

        public void GetHistory(ChartSelection selection, IHistorySubscriber subscriber)
        {
            var request = new HistoryRequest {ChartSelection = selection, HistorySubscriber = subscriber};
            request.ChartSelection.Symbol = request.ChartSelection.Symbol.ToUpper();

            if (!ValidateRequest(request))
            {
                SendNoHistory(request);
                return;
            }

            ThreadPool.QueueUserWorkItem(state => ProcessRequest(request));
        }

        private bool ValidateRequest(HistoryRequest request)
        {
            return request != null &&
                   request.ChartSelection != null &&
                   !string.IsNullOrEmpty(request.ChartSelection.Symbol) &&
                   (request.ChartSelection.Periodicity == EPeriodicity.Daily ||
                    request.ChartSelection.Periodicity == EPeriodicity.Hourly ||
                    request.ChartSelection.Periodicity == EPeriodicity.Minutely
                   ) &&
                   request.HistorySubscriber != null &&
                   request.ChartSelection.ChartType == EChartType.TimeBased;
        }

        private void ProcessRequest(HistoryRequest request)
        {
            _requests.Enqueue(request);
            ProcessNextRequest();
        }

        private void ProcessNextRequest()
        {
            if (_isWebClientBusy || _requests.Count == 0)
                return;

            _isWebClientBusy = true;

            var _currentRequest = _requests.Dequeue();

            var isFail = false;
            var response = string.Empty;


            try
            {
                //DEBUG: FireConnectionStatus(FormRequestString(_currentRequest.ChartSelection));
                response = _client.DownloadString(FormRequestString(_currentRequest.ChartSelection));
            }
            catch (WebException exception)
            {
                try
                {
                    if (exception.Response is HttpWebResponse httpWebResponse)
                    {
                        var resp = new StreamReader(httpWebResponse.GetResponseStream()).ReadToEnd();
                        dynamic obj = JsonConvert.DeserializeObject(resp);
                        
                        MessageBox.Show(resp, "CoinApi Error");
                    }
                }
                catch
                {
                }

                isFail = true;
            }

            if (isFail || string.IsNullOrEmpty(response))
                SendNoHistory(_currentRequest);
            else
                ProcessResponseAndSend(_currentRequest, response);

            _isWebClientBusy = false;

            ProcessNextRequest();
        }

        //...generate WebAPI GET request format string
        private string FormRequestString(ChartSelection selection)
        {
            //API Endpoint, e.g.
            //https://rest.coinapi.io/v1/ohlcv/BITSTAMP_SPOT_BTC_EUR/latest?period_id=1DAY&limit=200

            //Set correct endpoint
            var ep = "";
            switch (selection.Periodicity)
            {
                case EPeriodicity.Hourly:
                    ep = $"{(int) selection.Interval}HRS";
                    break;
                case EPeriodicity.Minutely:
                    ep = $"{(int) selection.Interval}MIN";
                    break;
                case EPeriodicity.Daily:
                    ep = $"{(int) selection.Interval}DAY";
                    break;

                default:
                    MessageBox.Show("CoinApi supports only days, hours and minutes");
                    return "";
            }

            var requestUrl =
                $"{Settings.Default.APIUrl}v1/ohlcv/{selection.Symbol}/latest?period_id={ep}&limit={selection.Bars}";

            //... Alternative request with start- and end-dates
            //var requestUrl = string.Format("/v1/ohlcv/{0}/history?period_id={1}&time_start={2}&time_end={3}", symbolId, periodId, start.ToString(dateFormat), end.ToString(dateFormat));


            //string requestUrl = Properties.Settings.Default.APIUrl +
            //    "v1/ohlcv/" + selection.Symbol+ "/latest?" + "period_id=" + ep + "&limit=" + selection.Bars;  

            //debug only
            //FireConnectionStatus("Get: " + requestUrl);

            return requestUrl;
        }

        private void SendNoHistory(HistoryRequest request)
        {
            ThreadPool.QueueUserWorkItem(
                state => request.HistorySubscriber.OnHistoryIncome(request.ChartSelection.Symbol, new List<BarData>()));
        }

        private void ProcessResponseAndSend(HistoryRequest request, string message)
        {
            //failsafe...
            if (string.IsNullOrEmpty(message))
            {
                SendNoHistory(request);
                return;
            }


            var datasets = new List<dynamic>();

            //get the data from the API call
            var master_cryptodataset =
                JsonConvert.DeserializeObject<dynamic>(message);

            //check for data
            if (((ICollection) master_cryptodataset).Count < 4)
            {
                MessageBox.Show("Error: not enough data");
                SendNoHistory(request);
                return;
            }

            //add to API return array
            datasets.Add(master_cryptodataset);

            //... convert API return values into bars
            var retBars = new List<BarData>();
            foreach (var set in datasets)
            {
                IEnumerable<BarData> _retBars = FromOHLCVBars(set);

                foreach (var newbar in _retBars)
                {
                    //... skip overlapping bars based on different requests send
                    if (retBars.Any(b => b.TradeDate == newbar.TradeDate))
                        continue;

                    retBars.Add(newbar);
                }
            }

            //...order
            retBars = retBars.OrderBy(data => data.TradeDate).ToList();

            //... if request`s bars amount was <= 0 then send all bars
            if (request.ChartSelection.Bars > 0)
                if (retBars.Count > request.ChartSelection.Bars)
                    retBars = retBars.Skip(retBars.Count - request.ChartSelection.Bars).ToList();

            ThreadPool.QueueUserWorkItem(
                state => request.HistorySubscriber.OnHistoryIncome(request.ChartSelection.Symbol, retBars));
        }

        //...Convert bar data message from API to internal bar data object
        private IEnumerable<BarData> FromOHLCVBars(dynamic records)
        {
            var count = ((ICollection) records).Count;

            var retBars = new List<BarData>(count);

            foreach (var observation in records)
            {
                var barData = new BarData();

                try
                {
                    var parsedDate = DateTime.Parse((string) observation.time_period_start, MyCultureInfo);

                    barData.TradeDate = parsedDate;

                    barData.Open = (double) observation.price_open;
                    barData.High = (double) observation.price_high;
                    barData.Low = (double) observation.price_low;
                    barData.Close = (double) observation.price_close;
                    barData.Volume = (double) observation.volume_traded;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Exception in data parsing:" + ex.Message + "--" + observation.time_period_start);
                    continue;
                }

                if (barData.TradeDate == default(DateTime))
                    continue;

                if (barData.Close != 0) retBars.Add(barData); // Dont add empty field with no close...!
            }

            return retBars;
        }

        #endregion
    }
}