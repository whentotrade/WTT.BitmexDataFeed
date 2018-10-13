# BitMEX WhenToTrade Datafeed  
Datafeed source library for the BitMEX crypto trading platform to integrate with WhenToTrade Charting and Cycle analysis platform.

### Setup
- Compile library or download pre-build library **WTT.BitmexDataFeed.zip** from [release page] 
- Copy complete build folder **WTT.BitmexDataFeed** into local installation path c:\wtt\datafeeds\
- Restart WTT charting app and select new datafeed **Bitmex** from WTT login dropdown
- Requests to BitMEX API are rate limited to 300 requests per 5 minutes. This counter refills continuously. If you provide no Apikey/Secret, your ratelimit is 150/5minutes.
- Please create your [API key] via your [BitMEX Account] 

### How to write my own datafeed integration?
Use this repository as template. Change and connect to the datafeed of your choice. Rename the repository and copy the build output into the WTT installation folder like this repository. There are no limits!


### References
 - [Book]: Decoding the Hidden Market Rhythm
 - [Charting Platform]: whentotrade website
 - [API Documentation]: API Reference Guide
 - [BitMEX Account]: Register your BitMEX account
 - [API key]: Create your API key/secret pair
  
  [Book]: <http://a.co/d/i9YlX4c>
  [Charting Platform]: <https://www.whentotrade.com>
  [API Documentation]: <https://www.bitmex.com/app/apiOverview>
  [API key]: <https://www.bitmex.com/app/apiKeys>
  [BitMEX Account]: <https://www.bitmex.com/register>
  [release page]: <https://github.com/whentotrade/WTT.BitmexDataFeed/releases>
