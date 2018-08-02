using Microsoft.Xrm.Sdk;
using sf365;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace SF365.Plugins
{
    [CrmPluginRegistration("sf365_updateexchangerates",
        "none",
        StageEnum.PostOperation,
        ExecutionModeEnum.Synchronous,
        "",
        "sf365_updateexchangerate",
        1000,
        IsolationModeEnum.Sandbox
     )]
    public class ExchangeRateActionPlugin : PluginBase
    {
        public ExchangeRateActionPlugin() : base(typeof(ExchangeRateActionPlugin))
        {

        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localcontext)
        {
            var execute = Task.Run(async () => await UpdateExchangRates(localcontext));
            Task.WaitAll(execute);

        }

        private async Task UpdateExchangRates(LocalPluginContext localcontext)
        {
            ITracingService tracer = localcontext.TracingService;
            var currencyList = (from c in new XrmSvc(localcontext.OrganizationService).CreateQuery<TransactionCurrency>()
                                select new TransactionCurrency
                                {
                                    ISOCurrencyCode = c.ISOCurrencyCode,
                                    TransactionCurrencyId = c.TransactionCurrencyId
                                }).ToArray();

            var baseCurrency = "CAD";
            var currencyISOCodes = string.Join(",", currencyList.Select(c => c.ISOCurrencyCode).Where(c => c != baseCurrency).ToArray());
            tracer.Trace(currencyISOCodes.ToString());


            //WebProxy oWebProxy = new System.Net.WebProxy("net-inspect-1.dhltd.corp:8080",true);
            //oWebProxy.Credentials = System.Net.CredentialCache.DefaultCredentials;
            
            //var httpClientHandler = new HttpClientHandler()
            //{
            //    Proxy = oWebProxy,
            //};

            //if (true)
            //{
            //    httpClientHandler.PreAuthenticate = true;
            //    httpClientHandler.UseDefaultCredentials = false;

            //    // *** These creds are given to the web server, not the proxy server ***
            //    //httpClientHandler.Credentials = new NetworkCredential(
            //    //    userName: serverUserName,
            //    //    password: serverPassword);
            //}


            using (HttpClient client = new HttpClient(/*handler:httpClientHandler, disposeHandler: true*/))
            {

                //var requestUri = new Uri("https://api.fixer.io/latest?symbols=" + String.Join(",", currencyISOCodes));

                var requestUri = new Uri("https://exchangeratesapi.io/api/latest?base=CAD&symbols=" + String.Join(",", currencyISOCodes));
                //var requestUri = new Uri("http://free.currencyconverterapi.com/api/v5/convert?q=" + String.Join("_", currencyISOCodes)+ "&compact=y");
                tracer.Trace(requestUri.ToString());

                var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                

                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    throw new InvalidPluginExecutionException("Exchangerate service returned " + response);

               
                var json = response.Content.ReadAsStringAsync().Result;
                var setting = new DataContractJsonSerializerSettings()
                {
                    UseSimpleDictionaryFormat = true
                };

                ExchangeRateResult result = null;
                using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(json)))
                {         
                    var serializer = new DataContractJsonSerializer(typeof(ExchangeRateResult), setting);
                    result = (ExchangeRateResult)serializer.ReadObject(ms);
                }

                foreach (var currency in currencyList.Where(c => c.ISOCurrencyCode != baseCurrency))
                {
                    if (result.rates.Keys.Contains(currency.ISOCurrencyCode))
                    {
                        var update = new TransactionCurrency
                        {
                            TransactionCurrencyId = currency.TransactionCurrencyId,
                            ExchangeRate = (decimal?)result.rates[currency.ISOCurrencyCode]
                        };
                        localcontext.OrganizationService.Update(update);
                    }
                }
            }
        }
    }
}
