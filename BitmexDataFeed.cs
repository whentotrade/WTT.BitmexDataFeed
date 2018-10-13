using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using WebSocket4Net;
using WTT.BitmexDataFeed.Properties;
using WTT.IDataFeed;

namespace WTT.BitmexDataFeed
{
    public class BitmexDataFeed : IDataFeed.IDataFeed
    {
        private readonly ctlLogin _loginForm = new ctlLogin();

        private readonly Queue<HistoryRequest> _requests = new Queue<HistoryRequest>();
        private readonly List<string> _symbols = new List<string>();

        private readonly AutoResetEvent m_OpenedEvent = new AutoResetEvent(false);

        //string format for dates is:  2018-09-21T00:00:00.0000000Z
        private readonly CultureInfo MyCultureInfo = new CultureInfo("en-EN");

        private bool _isWebClientBusy;
        private BitMEXApi bitmex;

        private WebSocket webSocket;

        private string bitmexKey { get; set; }

        private string bitmexSecret { get; set; }

        public event EventHandler<MessageEventArgs> OnNewMessage;
        public event EventHandler<DataFeedStatusEventArgs> OnNewStatus;
        public event EventHandler<DataFeedStatusTimeEventArgs> OnNewStatusTime;

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

        #region Login/logout

        public string Name => "Bitmex";

        public Control GetLoginControl()
        {
            return _loginForm;
        }

        public bool ValidateLoginParams()
        {
            //Check for valid ApiKey

            bitmexKey = _loginForm.ApiKey;
            Settings.Default.ApiKey = bitmexKey;

            bitmexSecret = _loginForm.Secret;
            Settings.Default.Secret = bitmexSecret;

            try
            {
                Settings.Default.Save();
            }
            catch
            {
            }

            return true;
        }

        //... BitMEX WebSocket connection opened
        private void OnConnected(object sender, EventArgs e)
        {
            FireConnectionStatus("Connected to BitMEX streaming websocket.");

            //... setup basic hello message
            //... nothing to do for this websocket

            m_OpenedEvent.Set();
        }

        //... BitMEX WebSocket Message Received
        private void OnMessage(object sender, MessageReceivedEventArgs e)
        {
            //Debug only
            //FireConnectionStatus("i:"+e.Message);

            var jsonMessage = e.Message;

            try
            {
                var unpacked = JsonConvert.DeserializeObject<dynamic>(jsonMessage);

                if (unpacked == null) return;

                var table = (string) unpacked.table;
                var action = (string) unpacked.action;

                if (table == "trade" && action == "insert")
                    foreach (var trade in unpacked.data)
                    {
                        var priceUpate = new QuoteData();
                        priceUpate.Symbol = trade.symbol;
                        priceUpate.Price = trade.price;
                        var parsedDate = DateTime.Parse((string) trade.timestamp, MyCultureInfo);
                        priceUpate.TradeTime = parsedDate;
                        priceUpate.TradedVolume = (long)trade.size;

                        //...update charts
                        BrodcastQuote(priceUpate);
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
            //... setup the web API connection
            bitmex = new BitMEXApi(bitmexKey, bitmexSecret);

            //...Initiate the WebSocket
            var socketAddress = "wss://www.bitmex.com/realtime";

            webSocket = new WebSocket(socketAddress);
            webSocket.Opened += OnConnected;
            //not implemented yet: websocket.Error += new EventHandler<ErrorEventArgs>(websocket_Error);
            //not implemented yet: websocket.Closed += new EventHandler(websocket_Closed);
            webSocket.MessageReceived += OnMessage;
            webSocket.Open();

            if (!m_OpenedEvent.WaitOne(10000))
            {
                FireConnectionStatus("Failed to open BitMEX websocket on time.");
                return false;
            }


            return true;
        }

        public bool Logout()
        {
            webSocket?.Close();

            FireConnectionStatus("Disconnected from " + Name);
            return true;
        }

        #endregion

        #region Realtime-feed

        //...Bitmex WebSocket Message class
        public class BitmexSocketMessage
        {
            public string op { get; set; }
            public List<string> args { get; set; }
        }

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
            if (webSocket == null) return;

            symbol = symbol.ToUpper();

            if (_symbols.Contains(symbol)) return;

            lock (_symbols)
            {
                _symbols.Add(symbol);
            }

            FireConnectionStatus("Listening to " + symbol);

            //...send out the message to listen for data
            var message = new BitmexSocketMessage {op = "subscribe", args = new List<string> {$"trade:{symbol}"}};

            var _webSocketMessage = JsonConvert.SerializeObject(message);
            webSocket.Send(_webSocketMessage);
        }

        public void UnSubscribe(string symbol)
        {
            //failsafe...
            if (webSocket == null) return;

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

            //...send out the message to de-listen for data 
            var message = new BitmexSocketMessage {op = "unsubscribe", args = new List<string> {$"trade:{symbol}"}};
            var _webSocketMessage = JsonConvert.SerializeObject(message);

            webSocket.Send(_webSocketMessage);
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

        private class HistoryRequest
        {
            public ChartSelection ChartSelection { get; set; }
            public IHistorySubscriber HistorySubscriber { get; set; }
        }


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

            var datasets = new List<dynamic>();

            try
            {
                var paginating = true;
                var pageSize = 750; //... max page count to receive via one API call
                var totalBarCounter = 0;
                var start = 0;
                var endingTime =
                    DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"); //ensure we get always the latest current bar

                do
                {
                    //DEBUG: FireConnectionStatus(FormRequestString(_currentRequest.ChartSelection));
                    try
                    {
                        var param = FormRequest(_currentRequest.ChartSelection, start, pageSize, endingTime);
                        var auth = !(string.IsNullOrEmpty(bitmexKey) || string.IsNullOrEmpty(bitmexSecret));

                        response = bitmex.Query("GET", "/trade/bucketed", param, auth);

                        //get the data from the API call
                        var current_dataset =
                            JsonConvert.DeserializeObject<dynamic>(response);

                        //check for data
                        if (((ICollection) current_dataset).Count < pageSize) paginating = false;

                        //add to API return array
                        datasets.Add(current_dataset);
                        totalBarCounter += ((ICollection) current_dataset).Count;

                        //...check if we have enough data
                        if (totalBarCounter >= _currentRequest.ChartSelection.Bars)
                            paginating = false;

                        start += pageSize;
                    }
                    catch (Exception ex)
                    {
                        paginating = false;
                        FireConnectionStatus("Error getting history API data for " +
                                             _currentRequest.ChartSelection.Symbol);
                    }
                } while (paginating);

                FireConnectionStatus("Received bars: " + totalBarCounter);
            }
            catch (Exception ex)
            {
                try
                {
                    FireConnectionStatus("Error: " + ex.Message);
                }
                catch
                {
                }

                isFail = true;
            }

            if (isFail)
                SendNoHistory(_currentRequest);
            else
                ProcessResponseAndSend(_currentRequest, datasets);

            _isWebClientBusy = false;

            ProcessNextRequest();
        }

        //...generate API GET request format
        private Dictionary<string, string> FormRequest(ChartSelection selection, int start, int count,
            string endingTime)
        {
            //API Endpoint, e.g.
            //https://www.bitmex.com/api/v1/trade/bucketed?binSize=1m&partial=false&count=100&reverse=false&startTime=2018-10-10

            //Set correct binSize
            //available options: 1m,5m,1h,1d
            var ep = "";
            switch (selection.Periodicity)
            {
                case EPeriodicity.Hourly:
                    if ((int) selection.Interval != 1)
                    {
                        MessageBox.Show("BitemexApi supports only 1m, 5m, 1h, 1d.");
                        return null;
                    }

                    ep = $"{(int) selection.Interval}h";
                    break;
                case EPeriodicity.Minutely:
                    if ((int) selection.Interval != 1 && (int) selection.Interval != 5)
                    {
                        MessageBox.Show("BitemexApi supports only 1m, 5m, 1h, 1d.");
                        return null;
                    }

                    ep = $"{(int) selection.Interval}m";
                    break;
                case EPeriodicity.Daily:
                    if ((int) selection.Interval != 1)
                    {
                        MessageBox.Show("BitemexApi supports only 1m, 5m, 1h, 1d.");
                        return null;
                    }

                    ep = $"{(int) selection.Interval}d";
                    break;

                default:
                    MessageBox.Show("BitemexApi supports only 1m, 5m, 1h, 1d.");
                    return null;
            }

            //debug only
            //FireConnectionStatus("Get: " + requestUrl);

            //always ensure we have the last active bar


            var param = new Dictionary<string, string>();
            param["symbol"] = selection.Symbol;
            param["binSize"] = ep; //... available options: 1m,5m,1h,1d
            param["partial"] =
                true.ToString(); //... will send in-progress (incomplete) bins for the current time period
            param["start"] = start.ToString();
            param["count"] = count.ToString();

            //param["filter"] = "{\"open\":true}";
            //param["columns"] = "";

            param["reverse"] = true.ToString();
            //param["startTime"] = "";
            param["endTime"] = endingTime;

            return param;
        }

        private void SendNoHistory(HistoryRequest request)
        {
            ThreadPool.QueueUserWorkItem(
                state => request.HistorySubscriber.OnHistoryIncome(request.ChartSelection.Symbol, new List<BarData>()));
        }

        private void ProcessResponseAndSend(HistoryRequest request, List<dynamic> datasets)
        {
            var periodicity = request.ChartSelection.Periodicity;
            var interval = (int) request.ChartSelection.Interval;

            //...failsafe
            if (datasets == null)
            {
                SendNoHistory(request);
                return;
            }

            //check for data
            if (datasets.Count < 1)
            {
                MessageBox.Show("Error: not enough data");
                SendNoHistory(request);
                return;
            }

            //... convert API return values into bars
            var retBars = new List<BarData>();
            foreach (var set in datasets)
            {
                IEnumerable<BarData> _retBars = FromOHLCVBars(set);

                foreach (var newbar in _retBars)
                {
                    //... correct bar time to start-time of bar
                    //... returned API timestamp for bin is "closing-time" of bar
                    switch (periodicity)
                    {
                        case EPeriodicity.Hourly:
                            newbar.TradeDate = newbar.TradeDate.AddHours(-1 * interval);
                            break;
                        case EPeriodicity.Minutely:
                            newbar.TradeDate = newbar.TradeDate.AddMinutes(-1 * interval);
                            break;
                        case EPeriodicity.Daily:
                            newbar.TradeDate = newbar.TradeDate.AddDays(-1 * interval);
                            break;
                    }

                    //... skip overlapping bars based on different requests send
                    if (retBars.Any(b => b.TradeDate == newbar.TradeDate))
                        continue;

                    retBars.Add(newbar);
                }
            }

            if (retBars.Count < 4)
            {
                MessageBox.Show("Error: not enough data");
                SendNoHistory(request);
                return;
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
                    //info: timestamp is closing of bar time! (=next bar starting time!)
                    var parsedDate = DateTime.Parse((string) observation.timestamp, MyCultureInfo);

                    barData.TradeDate = parsedDate;

                    barData.Open = (double) observation.open;
                    barData.High = (double) observation.high;
                    barData.Low = (double) observation.low;
                    barData.Close = (double) observation.close;
                    barData.Volume = (double) observation.volume;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Exception in data parsing:" + ex.Message + "--" + observation.timestamp);
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